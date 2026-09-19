using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;

// Builds the playable scene on top of the URP template's SampleScene, so the
// Main Camera and Global Light 2D stay correctly configured.
// Menu: Shadow -> Build Game Scene
public static class BuildGameScene
{
    const string TemplateScene = "Assets/Scenes/SampleScene.unity";
    const string OutScene = "Assets/Scenes/Game.unity";
    const string AnimDir = "Assets/Animations";
    const float CharHeight = 2f;     // player height in world units
    const float PPU = 128f;          // 256px frames / 2 units
    const float GroundLength = 60f;

    [MenuItem("Shadow/Build Game Scene")]
    public static void Build()
    {
        int groundLayer = EnsureLayer("Ground");
        ConfigureSprites();

        Directory.CreateDirectory(AnimDir);
        var idle = MakeClip("Idle", "Assets/Sprites/idle", 10f, true, true);
        var run = MakeClip("Run", "Assets/Sprites/run", 12f, true);
        var jump = MakeClip("Jump", "Assets/Sprites/jump", 10f, false);
        var controller = MakeController(idle, run, jump);

        BuildScene(controller, groundLayer);
        Debug.Log("Shadow: scene built at " + OutScene);
    }

    // Never use ?? on GetComponent: Unity's == reports missing components as
    // null, but the reference itself is not null, so ?? keeps the dud.
    static T Ensure<T>(GameObject go) where T : Component =>
        go.TryGetComponent<T>(out var c) ? c : go.AddComponent<T>();

    static int EnsureLayer(string name)
    {
        int existing = LayerMask.NameToLayer(name);
        if (existing >= 0) return existing;

        var tm = new SerializedObject(
            AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
        var layers = tm.FindProperty("layers");
        for (int i = 8; i < layers.arraySize; i++)
        {
            var slot = layers.GetArrayElementAtIndex(i);
            if (string.IsNullOrEmpty(slot.stringValue))
            {
                slot.stringValue = name;
                tm.ApplyModifiedProperties();
                return i;
            }
        }
        throw new System.Exception("No free layer slot for " + name);
    }

    static void ConfigureSprites()
    {
        foreach (var folder in new[] { "idle", "run", "jump" })
            foreach (var path in PngsIn("Assets/Sprites/" + folder))
                ApplyImport(path, PPU, new Vector2(0.5f, 0f), SpriteMeshType.Tight);

        ApplyImport("Assets/Sprites/spikes.png", 64f, new Vector2(0.5f, 0f), SpriteMeshType.Tight);

        var ground = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Sprites/ground.png");
        if (ground != null)
            ApplyImport("Assets/Sprites/ground.png", ground.height, new Vector2(0.5f, 0.5f), SpriteMeshType.FullRect);

        var bg = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Sprites/bg.png");
        if (bg != null)
            ApplyImport("Assets/Sprites/bg.png", bg.height / 12f, new Vector2(0.5f, 0.5f), SpriteMeshType.FullRect);
    }

    static void ApplyImport(string path, float ppu, Vector2 pivot, SpriteMeshType mesh)
    {
        var importer = AssetImporter.GetAtPath(path) as TextureImporter;
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
        importer.SetTextureSettings(s);
        importer.maxTextureSize = 2048;
        importer.SaveAndReimport();
    }

    static IEnumerable<string> PngsIn(string dir) =>
        Directory.GetFiles(dir, "*.png")
                 .Select(p => p.Replace('\\', '/'))
                 .OrderBy(p => p, System.StringComparer.OrdinalIgnoreCase);

    static AnimationClip MakeClip(string name, string folder, float fps, bool loop, bool pingPong = false)
    {
        var sprites = PngsIn(folder).Select(AssetDatabase.LoadAssetAtPath<Sprite>)
                                    .Where(s => s != null).ToArray();
        if (sprites.Length == 0) throw new System.Exception("No sprites in " + folder);
        // Ping-pong: 1..6 then 5..2, skipping both endpoints so the loop does not stutter.
        if (pingPong && sprites.Length > 2)
            sprites = sprites.Concat(sprites.Reverse().Skip(1).Take(sprites.Length - 2)).ToArray();

        var clip = new AnimationClip { frameRate = fps };
        var binding = new EditorCurveBinding
        {
            type = typeof(SpriteRenderer),
            path = "",
            propertyName = "m_Sprite"
        };
        AnimationUtility.SetObjectReferenceCurve(clip, binding,
            sprites.Select((s, i) => new ObjectReferenceKeyframe { time = i / fps, value = s }).ToArray());

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
        var sIdle = sm.AddState("Idle"); sIdle.motion = idle;
        var sRun = sm.AddState("Run"); sRun.motion = run;
        var sJump = sm.AddState("Jump"); sJump.motion = jump;
        sm.defaultState = sIdle;

        var toRun = sIdle.AddTransition(sRun);
        toRun.hasExitTime = false; toRun.duration = 0f;
        toRun.AddCondition(AnimatorConditionMode.Greater, 0.1f, "Speed");

        var toIdle = sRun.AddTransition(sIdle);
        toIdle.hasExitTime = false; toIdle.duration = 0f;
        toIdle.AddCondition(AnimatorConditionMode.Less, 0.1f, "Speed");

        var toJump = sm.AddAnyStateTransition(sJump);
        toJump.hasExitTime = false; toJump.duration = 0f;
        toJump.canTransitionToSelf = false;
        toJump.AddCondition(AnimatorConditionMode.If, 0f, "jump");

        var jumpDone = sJump.AddTransition(sIdle);
        jumpDone.hasExitTime = true; jumpDone.exitTime = 1f; jumpDone.duration = 0.05f;

        AssetDatabase.SaveAssets();
        return ac;
    }

    static void BuildScene(AnimatorController controller, int groundLayer)
    {
        var scene = EditorSceneManager.OpenScene(TemplateScene, OpenSceneMode.Single);

        // Remove anything a previous run left behind.
        foreach (var name in new[] { "Background", "Ground", "Spikes", "Spikes (2)", "Player" })
        {
            var old = GameObject.Find(name);
            if (old != null) Object.DestroyImmediate(old);
        }

        var cam = Camera.main;
        cam.orthographic = true;
        cam.orthographicSize = 5f;
        cam.transform.position = new Vector3(0f, 1f, -10f);

        var music = Ensure<AudioSource>(cam.gameObject);
        music.clip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/bgm.wav");
        music.loop = true;
        music.playOnAwake = true;
        music.volume = 0.5f;

        var bg = new GameObject("Background");
        var bgSr = bg.AddComponent<SpriteRenderer>();
        bgSr.sprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/bg.png");
        bgSr.drawMode = SpriteDrawMode.Tiled;
        bgSr.size = new Vector2(GroundLength + 20f, 14f);
        bgSr.sortingOrder = -100;
        bg.transform.position = new Vector3(GroundLength / 3f, 3f, 0f);

        var ground = new GameObject("Ground") { layer = groundLayer };
        var gSr = ground.AddComponent<SpriteRenderer>();
        gSr.sprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/ground.png");
        gSr.drawMode = SpriteDrawMode.Tiled;
        gSr.size = new Vector2(GroundLength, 2f);
        gSr.sortingOrder = -10;
        ground.transform.position = new Vector3(GroundLength / 3f, -1f, 0f);
        ground.AddComponent<BoxCollider2D>().size = new Vector2(GroundLength, 2f);

        var spikes = new GameObject("Spikes");
        var sSr = spikes.AddComponent<SpriteRenderer>();
        sSr.sprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/spikes.png");
        sSr.sortingOrder = 5;
        spikes.transform.position = new Vector3(6f, 0f, 0f);
        spikes.AddComponent<PolygonCollider2D>();

        var spikes2 = Object.Instantiate(spikes);
        spikes2.name = "Spikes (2)";
        spikes2.transform.position = new Vector3(13f, 0f, 0f);

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
        body.size = new Vector2(CharHeight * 0.45f, CharHeight * 0.95f);
        body.offset = new Vector2(0f, CharHeight * 0.475f);

        var anim = player.AddComponent<Animator>();
        anim.runtimeAnimatorController = controller;
        anim.applyRootMotion = false;

        var groundCheck = new GameObject("GroundCheck").transform;
        groundCheck.SetParent(player.transform);
        groundCheck.localPosition = new Vector3(0f, 0.05f, 0f);

        var ceilingCheck = new GameObject("CeilingCheck").transform;
        ceilingCheck.SetParent(player.transform);
        ceilingCheck.localPosition = new Vector3(0f, CharHeight, 0f);

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

        var follow = Ensure<CameraFollow>(cam.gameObject);
        follow.followObject = player;
        follow.followOffset = new Vector2(2f, 2f);
        follow.speed = 6f;

        EditorSceneManager.SaveScene(scene, OutScene);
        EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(OutScene, true) };
        AssetDatabase.SaveAssets();
    }
}
