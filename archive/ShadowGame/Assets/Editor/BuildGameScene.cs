using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;

// Rebuilds the whole playable scene from the raw sprites in Assets/Sprites.
// Menu: Shadow -> Build Game Scene. Safe to re-run; it overwrites its own output.
public static class BuildGameScene
{
    const string ScenePath = "Assets/Scenes/Game.unity";
    const string AnimDir = "Assets/Animations";
    const float CharHeightUnits = 2f;   // how tall the player is in world units
    const float GroundLength = 60f;

    [MenuItem("Shadow/Build Game Scene")]
    public static void Build()
    {
        int groundLayer = EnsureLayer("Ground");
        ConfigureSprites();
        string spikePath = CreateSpikeSprite();
        string musicPath = CreateMusic();

        Directory.CreateDirectory(AnimDir);
        var idle = MakeClip("Idle", "Assets/Sprites/idle", 10f, true);
        var run = MakeClip("Run", "Assets/Sprites/run", 12f, true);
        var jump = MakeClip("Jump", "Assets/Sprites/jump", 10f, false);
        var controller = MakeController(idle, run, jump);

        BuildScene(controller, groundLayer, spikePath, musicPath);
        Debug.Log("Shadow: scene built. Open Assets/Scenes/Game.unity and press Play.");
    }

    // --- layers -----------------------------------------------------------
    static int EnsureLayer(string name)
    {
        int existing = LayerMask.NameToLayer(name);
        if (existing >= 0) return existing;

        var tagManager = new SerializedObject(
            AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
        var layers = tagManager.FindProperty("layers");
        for (int i = 8; i < layers.arraySize; i++)
        {
            var slot = layers.GetArrayElementAtIndex(i);
            if (string.IsNullOrEmpty(slot.stringValue))
            {
                slot.stringValue = name;
                tagManager.ApplyModifiedProperties();
                return i;
            }
        }
        throw new System.Exception("No free layer slot for " + name);
    }

    // --- sprite import ----------------------------------------------------
    static void ConfigureSprites()
    {
        // One pixels-per-unit for every character frame, derived from idle1,
        // so idle/run/jump all end up the same size on screen.
        var first = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Sprites/idle/idle1.png");
        float ppu = first != null ? first.height / CharHeightUnits : 100f;

        foreach (var folder in new[] { "idle", "run", "jump" })
            foreach (var path in PngsIn("Assets/Sprites/" + folder))
                ApplyImport(path, ppu, new Vector2(0.5f, 0f), SpriteMeshType.Tight);

        ApplyImport("Assets/Sprites/ground.png",
            Tex("Assets/Sprites/ground.png").height, new Vector2(0.5f, 0.5f), SpriteMeshType.FullRect);
        ApplyImport("Assets/Sprites/bg.png",
            Tex("Assets/Sprites/bg.png").height / 12f, new Vector2(0.5f, 0.5f), SpriteMeshType.FullRect);
    }

    static Texture2D Tex(string p) => AssetDatabase.LoadAssetAtPath<Texture2D>(p);

    static void ApplyImport(string path, float ppu, Vector2 pivot, SpriteMeshType mesh)
    {
        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        if (importer == null) return;

        var s = new TextureImporterSettings();
        importer.ReadTextureSettings(s);
        s.textureType = TextureImporterType.Sprite;
        s.spriteMode = (int)SpriteImportMode.Single;
        s.spritePixelsPerUnit = Mathf.Max(1f, ppu);
        s.spriteAlignment = (int)SpriteAlignment.Custom;
        s.spritePivot = pivot;
        s.spriteMeshType = mesh;
        s.spriteExtrude = 1;
        s.readable = false;
        importer.SetTextureSettings(s);
        importer.maxTextureSize = 2048;
        importer.SaveAndReimport();
    }

    static IEnumerable<string> PngsIn(string dir) =>
        Directory.GetFiles(dir, "*.png")
                 .Select(p => p.Replace('\\', '/'))
                 .OrderBy(p => p, System.StringComparer.OrdinalIgnoreCase);

    // --- animation --------------------------------------------------------
    static AnimationClip MakeClip(string name, string folder, float fps, bool loop)
    {
        var sprites = PngsIn(folder)
            .Select(AssetDatabase.LoadAssetAtPath<Sprite>)
            .Where(s => s != null).ToArray();
        if (sprites.Length == 0) throw new System.Exception("No sprites in " + folder);

        var clip = new AnimationClip { frameRate = fps };
        var binding = new EditorCurveBinding
        {
            type = typeof(SpriteRenderer),
            path = "",
            propertyName = "m_Sprite"
        };
        var keys = sprites.Select((s, i) => new ObjectReferenceKeyframe
        {
            time = i / fps,
            value = s
        }).ToArray();
        AnimationUtility.SetObjectReferenceCurve(clip, binding, keys);

        var settings = AnimationUtility.GetAnimationClipSettings(clip);
        settings.loopTime = loop;
        AnimationUtility.SetAnimationClipSettings(clip, settings);

        string path = $"{AnimDir}/{name}.anim";
        AssetDatabase.DeleteAsset(path);
        AssetDatabase.CreateAsset(clip, path);
        return clip;
    }

    static AnimatorController MakeController(AnimationClip idle, AnimationClip run, AnimationClip jump)
    {
        string path = $"{AnimDir}/Player.controller";
        AssetDatabase.DeleteAsset(path);
        var ac = AnimatorController.CreateAnimatorControllerAtPath(path);
        ac.AddParameter("Speed", AnimatorControllerParameterType.Float);
        ac.AddParameter("jump", AnimatorControllerParameterType.Bool);

        var sm = ac.layers[0].stateMachine;
        var sIdle = sm.AddState("Idle");   sIdle.motion = idle;
        var sRun = sm.AddState("Run");     sRun.motion = run;
        var sJump = sm.AddState("Jump");   sJump.motion = jump;
        sm.defaultState = sIdle;

        var toRun = sIdle.AddTransition(sRun);
        toRun.hasExitTime = false; toRun.duration = 0f;
        toRun.AddCondition(AnimatorConditionMode.Greater, 0.1f, "Speed");

        var toIdle = sRun.AddTransition(sIdle);
        toIdle.hasExitTime = false; toIdle.duration = 0f;
        toIdle.AddCondition(AnimatorConditionMode.Less, 0.1f, "Speed");

        // Any state -> Jump, so it fires from Idle or Run alike.
        var toJump = sm.AddAnyStateTransition(sJump);
        toJump.hasExitTime = false; toJump.duration = 0f;
        toJump.canTransitionToSelf = false;
        toJump.AddCondition(AnimatorConditionMode.If, 0f, "jump");

        // Jump clip does not loop; when it finishes, fall back to Idle.
        var jumpDone = sJump.AddTransition(sIdle);
        jumpDone.hasExitTime = true; jumpDone.exitTime = 1f; jumpDone.duration = 0.05f;

        AssetDatabase.SaveAssets();
        return ac;
    }

    // --- generated assets -------------------------------------------------
    static string CreateSpikeSprite()
    {
        const string path = "Assets/Sprites/spikes.png";
        const int w = 128, h = 128;
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        var px = new Color32[w * h];
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                // Two triangles side by side -> classic spike strip.
                float local = (x % (w / 2)) / (float)(w / 2);      // 0..1 across one spike
                float edge = 1f - Mathf.Abs(local - 0.5f) * 2f;    // peak in the middle
                bool inside = y < edge * h;
                byte shade = (byte)(120 + 100 * (1f - y / (float)h));
                px[y * w + x] = inside
                    ? new Color32(shade, shade, (byte)(shade + 20), 255)
                    : new Color32(0, 0, 0, 0);
            }
        tex.SetPixels32(px);
        tex.Apply();
        File.WriteAllBytes(path, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);
        AssetDatabase.ImportAsset(path);
        ApplyImport(path, 64f, new Vector2(0.5f, 0f), SpriteMeshType.Tight);
        return path;
    }

    static string CreateMusic()
    {
        const string path = "Assets/Audio/bgm.wav";
        const int rate = 44100;
        // Simple 8-bar chiptune loop. Semitone offsets from A3.
        int[] melody = { 0, 4, 7, 12, 7, 4, 0, 4, -1, 3, 7, 12, 7, 3, -1, 3 };
        float noteLen = 0.35f;
        int total = (int)(rate * noteLen * melody.Length);
        var samples = new short[total];

        for (int n = 0; n < melody.Length; n++)
        {
            float freq = 220f * Mathf.Pow(2f, melody[n] / 12f);
            float bass = 110f * Mathf.Pow(2f, melody[n] / 12f);
            int start = (int)(n * noteLen * rate);
            int len = (int)(noteLen * rate);
            for (int i = 0; i < len && start + i < total; i++)
            {
                float t = i / (float)rate;
                float env = Mathf.Min(1f, i / (rate * 0.01f)) * (1f - i / (float)len);
                float lead = Mathf.Sign(Mathf.Sin(2f * Mathf.PI * freq * t)) * 0.18f;
                float low = Mathf.Sign(Mathf.Sin(2f * Mathf.PI * bass * t)) * 0.10f;
                samples[start + i] = (short)((lead + low) * env * short.MaxValue);
            }
        }

        Directory.CreateDirectory("Assets/Audio");
        using (var fs = new FileStream(path, FileMode.Create))
        using (var bw = new BinaryWriter(fs))
        {
            int dataBytes = samples.Length * 2;
            bw.Write(System.Text.Encoding.ASCII.GetBytes("RIFF"));
            bw.Write(36 + dataBytes);
            bw.Write(System.Text.Encoding.ASCII.GetBytes("WAVEfmt "));
            bw.Write(16); bw.Write((short)1); bw.Write((short)1);
            bw.Write(rate); bw.Write(rate * 2); bw.Write((short)2); bw.Write((short)16);
            bw.Write(System.Text.Encoding.ASCII.GetBytes("data"));
            bw.Write(dataBytes);
            foreach (var s in samples) bw.Write(s);
        }
        AssetDatabase.ImportAsset(path);
        return path;
    }

    // --- scene ------------------------------------------------------------
    static void BuildScene(AnimatorController controller, int groundLayer, string spikePath, string musicPath)
    {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        // Camera
        var camGo = new GameObject("Main Camera");
        camGo.tag = "MainCamera";
        var cam = camGo.AddComponent<Camera>();
        cam.orthographic = true;
        cam.orthographicSize = 5f;
        cam.backgroundColor = new Color(0.15f, 0.18f, 0.28f);
        camGo.AddComponent<AudioListener>();
        camGo.transform.position = new Vector3(0, 1, -10);

        // Background music
        var music = camGo.AddComponent<AudioSource>();
        music.clip = AssetDatabase.LoadAssetAtPath<AudioClip>(musicPath);
        music.loop = true;
        music.playOnAwake = true;
        music.volume = 0.5f;

        // Background image
        var bg = new GameObject("Background");
        var bgSr = bg.AddComponent<SpriteRenderer>();
        bgSr.sprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/bg.png");
        bgSr.drawMode = SpriteDrawMode.Tiled;
        bgSr.size = new Vector2(GroundLength + 20f, 14f);
        bgSr.sortingOrder = -100;
        bg.transform.position = new Vector3(GroundLength / 3f, 3f, 0);

        // Ground
        var ground = new GameObject("Ground");
        ground.layer = groundLayer;
        var gSr = ground.AddComponent<SpriteRenderer>();
        gSr.sprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/ground.png");
        gSr.drawMode = SpriteDrawMode.Tiled;
        gSr.size = new Vector2(GroundLength, 2f);
        gSr.sortingOrder = -10;
        ground.transform.position = new Vector3(GroundLength / 3f, -1f, 0);
        var gCol = ground.AddComponent<BoxCollider2D>();
        gCol.size = new Vector2(GroundLength, 2f);

        // Hazard
        var spikes = new GameObject("Spikes");
        var sSr = spikes.AddComponent<SpriteRenderer>();
        sSr.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(spikePath);
        sSr.sortingOrder = 5;
        spikes.transform.position = new Vector3(6f, 0f, 0f);
        spikes.AddComponent<PolygonCollider2D>();

        var spikes2 = Object.Instantiate(spikes);
        spikes2.name = "Spikes (2)";
        spikes2.transform.position = new Vector3(13f, 0f, 0f);

        // Player
        var player = new GameObject("Player");
        player.transform.position = new Vector3(0f, 0.2f, 0f);
        var pSr = player.AddComponent<SpriteRenderer>();
        pSr.sprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/idle/idle1.png");
        pSr.sortingOrder = 10;

        var rb = player.AddComponent<Rigidbody2D>();
        rb.freezeRotation = true;
        rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        rb.gravityScale = 3f;

        var body = player.AddComponent<CapsuleCollider2D>();
        body.size = new Vector2(CharHeightUnits * 0.45f, CharHeightUnits * 0.95f);
        body.offset = new Vector2(0f, CharHeightUnits * 0.475f);

        var anim = player.AddComponent<Animator>();
        anim.runtimeAnimatorController = controller;
        anim.applyRootMotion = false;

        var groundCheck = new GameObject("GroundCheck").transform;
        groundCheck.SetParent(player.transform);
        groundCheck.localPosition = new Vector3(0f, 0.05f, 0f);

        var ceilingCheck = new GameObject("CeilingCheck").transform;
        ceilingCheck.SetParent(player.transform);
        ceilingCheck.localPosition = new Vector3(0f, CharHeightUnits, 0f);

        var cc = player.AddComponent<CharacterController2D>();
        var so = new SerializedObject(cc);
        so.FindProperty("m_JumpForce").floatValue = 900f;
        so.FindProperty("m_AirControl").boolValue = true;
        so.FindProperty("m_WhatIsGround").intValue = 1 << groundLayer;
        so.FindProperty("m_GroundCheck").objectReferenceValue = groundCheck;
        so.FindProperty("m_CeilingCheck").objectReferenceValue = ceilingCheck;
        so.ApplyModifiedPropertiesWithoutUndo();

        var pm = player.AddComponent<playermovement>();
        pm.controller = cc;
        pm.anime = anim;
        pm.runSpeed = 40f;

        // Camera follows the player
        var follow = camGo.AddComponent<CameraFollow>();
        follow.followObject = player;
        follow.followOffset = new Vector2(2f, 2f);
        follow.speed = 6f;

        Directory.CreateDirectory("Assets/Scenes");
        EditorSceneManager.SaveScene(scene, ScenePath);
        EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
        AssetDatabase.SaveAssets();
    }
}
