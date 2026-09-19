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
    const float PPU = 110f;          // character is ~220px tall in a 256 canvas -> 2 units
    const float TilePPU = 156f;      // ground_mid is 313px wide -> 2 world units per tile
    const float BgPPU = 63f;         // backgrounds stand 14 units tall
    const float GroundH = 1.51f;     // ground_mid 236px / TilePPU
    const float PlatSpriteH = 2.0f;  // plat tiles incl. hanging vines
    const float PlatSolidH = 0.5f;   // only the slab on top is standable
    const float LevelLength = 132f;

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

        foreach (var path in PngsIn("Assets/Sprites/tiles"))
            ApplyImport(path, TilePPU, new Vector2(0.5f, 0.5f), SpriteMeshType.FullRect);

        foreach (var path in PngsIn("Assets/Sprites/backgrounds"))
            ApplyImport(path, BgPPU, new Vector2(0.5f, 0.5f), SpriteMeshType.FullRect);

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
        // Bilinear, not Point: this is painted art that gets scaled down.
        // Point is for pixel art, where hard pixel edges are the intent.
        importer.filterMode = FilterMode.Bilinear;
        importer.textureCompression = TextureImporterCompression.CompressedHQ;
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

    static Sprite Tile(string n) =>
        AssetDatabase.LoadAssetAtPath<Sprite>($"Assets/Sprites/tiles/{n}.png");

    // A run of tiles: middle repeated, decorative end caps, one collider.
    // colliderH lets a platform be thick art with a thin standable top.
    static void MakeRun(Transform parent, int layer, string name,
                        string mid, string left, string right,
                        float cx, float topY, float w, float spriteH,
                        float colliderH, int order)
    {
        var go = new GameObject(name) { layer = layer };
        go.transform.SetParent(parent);
        go.transform.position = new Vector3(cx, topY - spriteH / 2f, 0f);

        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = Tile(mid);
        sr.drawMode = SpriteDrawMode.Tiled;
        sr.size = new Vector2(w, spriteH);
        sr.sortingOrder = order;

        var bc = go.AddComponent<BoxCollider2D>();
        bc.size = new Vector2(w, colliderH);
        // Collider sits flush with the top of the art, not the middle of it.
        bc.offset = new Vector2(0f, spriteH / 2f - colliderH / 2f);

        foreach (var (tile, side) in new[] { (left, -1f), (right, 1f) })
        {
            if (string.IsNullOrEmpty(tile)) continue;
            var sprite = Tile(tile);
            if (sprite == null) continue;
            float capW = sprite.bounds.size.x;
            var cap = new GameObject(name + (side < 0 ? "_L" : "_R"));
            cap.transform.SetParent(go.transform);
            cap.transform.position = new Vector3(cx + side * (w - capW) / 2f,
                                                 topY - spriteH / 2f, 0f);
            var csr = cap.AddComponent<SpriteRenderer>();
            csr.sprite = sprite;
            csr.sortingOrder = order + 1;
        }
    }

    static void BuildScene(AnimatorController controller, int groundLayer)
    {
        var scene = EditorSceneManager.OpenScene(TemplateScene, OpenSceneMode.Single);

        // Remove anything a previous run left behind.
        foreach (var name in new[] { "Background", "Level", "Ground", "Spikes", "Spikes (2)", "Player" })
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
        // Nearer layers scroll faster; that speed difference is what the eye
        // reads as depth. Sky barely moves, bamboo races past.
        var layers = new[]
        {
            ("bg1_sky",  0.10f, 24f, -100),
            ("bg2_far",  0.30f, 18f,  -90),
            ("bg3_mid",  0.55f, 16f,  -80),
            ("bg4_near", 0.85f, 16f,  -20),
        };
        foreach (var (file, factor, height, order) in layers)
        {
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(
                $"Assets/Sprites/backgrounds/{file}.png");
            if (sprite == null) continue;
            var go = new GameObject(file);
            go.transform.SetParent(bg.transform);
            go.transform.position = new Vector3(LevelLength / 2f, height / 2f - 3f, 0f);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.drawMode = SpriteDrawMode.Tiled;
            sr.size = new Vector2(LevelLength + 80f, height);
            sr.sortingOrder = order;
            go.AddComponent<Parallax>().factor = factor;
        }

        var level = new GameObject("Level");

        // Solid ground runs, with gaps between them the player must jump.
        // Each entry is (xStart, xEnd). Top surface sits at y = 0.
        var runs = new[]
        {
            (0f, 18f), (22f, 40f), (45f, 70f), (74f, 100f), (104f, 132f)
        };
        foreach (var (x0, x1) in runs)
            MakeRun(level.transform, groundLayer, "Ground",
                    "ground_mid", "ground_left", "ground_right",
                    (x0 + x1) / 2f, 0f, x1 - x0, GroundH, GroundH, -10);

        // Floating platforms: (centreX, topY, width). Heights are stepped so
        // each high one is reachable from the platform before it.
        var platforms = new[]
        {
            (19.5f, 2.2f, 3f),  (26f, 3.0f, 4f),  (32f, 5.0f, 3f),
            (41.5f, 2.5f, 3f),  (50f, 3.5f, 4f),  (56f, 6.0f, 3f),
            (63f, 4.0f, 4f),    (71.5f, 2.8f, 3f),(80f, 3.2f, 4f),
            (87f, 5.5f, 3f),    (94f, 3.0f, 4f),  (101.5f, 2.5f, 3.5f),
            (110f, 4.0f, 4f),   (118f, 6.0f, 3f), (125f, 3.0f, 4f)
        };
        foreach (var (x, top, w) in platforms)
        {
            bool small = w <= 2.2f;
            MakeRun(level.transform, groundLayer, "Platform",
                    small ? "plat_single" : "plat_mid",
                    small ? null : "plat_left",
                    small ? null : "plat_right",
                    x, top, w, PlatSpriteH, PlatSolidH, -5);
        }

        // Hazards on the ground runs, placed clear of the gap edges.
        var spikeXs = new[] { 8f, 13f, 30f, 35f, 52f, 60f, 66f, 78f, 92f, 97f, 112f, 122f };
        var spikeSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/spikes.png");
        foreach (var x in spikeXs)
        {
            var sp = new GameObject("Spikes");
            sp.transform.SetParent(level.transform);
            sp.transform.position = new Vector3(x, 0f, 0f);
            var sr = sp.AddComponent<SpriteRenderer>();
            sr.sprite = spikeSprite;
            sr.sortingOrder = 5;
            // Box, not polygon: the player should not be able to drop between
            // the spike tips and stand safely inside the hazard.
            var bc = sp.AddComponent<BoxCollider2D>();
            bc.size = new Vector2(2f, 1f);
            bc.offset = new Vector2(0f, 0.5f);
        }

        var player = new GameObject("Player");
        player.transform.position = new Vector3(0f, 0.2f, 0f);
        var pSr = player.AddComponent<SpriteRenderer>();
        pSr.sprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/idle/idle01.png");
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
        so.FindProperty("m_JumpForce").floatValue = 800f;
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
