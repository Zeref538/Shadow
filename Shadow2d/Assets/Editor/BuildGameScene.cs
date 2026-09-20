using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;

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
    const float PlatSolidH = 0.5f;   // standable band on a platform; vines hang below
    const float LevelLength = 160f;
    // Swap this one word to reskin the whole level: ruin, ruin_vines,
    // cavern, ice, moss, lava. All six sets share the same 16 tile names.
    const string TileSet = "moss";

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

        // Tight, not FullRect: PolygonCollider2D copies the sprite's physics
        // shape, and FullRect would give it a plain rectangle.
        foreach (var path in PngsIn("Assets/Sprites/tiles", true))
            ApplyImport(path, TilePPU, new Vector2(0.5f, 0.5f), SpriteMeshType.Tight, true);

        foreach (var path in PngsIn("Assets/Sprites/effects"))
            ApplyImport(path, 165f, new Vector2(0.5f, 0f), SpriteMeshType.FullRect);

        foreach (var path in PngsIn("Assets/Sprites/backgrounds"))
            ApplyImport(path, BgPPU, new Vector2(0.5f, 0.5f), SpriteMeshType.FullRect);

    }

    static void ApplyImport(string path, float ppu, Vector2 pivot, SpriteMeshType mesh,
                            bool physicsShape = false)
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
        s.spriteExtrude = 0;   // any padding here shows as a gap when tiles repeat
        s.spriteGenerateFallbackPhysicsShape = physicsShape;
        // Ignore near-transparent wisps so the outline follows solid stone,
        // not every stray vine pixel the player could snag on.
        s.alphaIsTransparency = true;
        importer.SetTextureSettings(s);
        // Bilinear, not Point: this is painted art that gets scaled down.
        // Point is for pixel art, where hard pixel edges are the intent.
        importer.filterMode = FilterMode.Bilinear;
        importer.maxTextureSize = 2048;
        importer.SaveAndReimport();
    }

    static IEnumerable<string> PngsIn(string dir, bool recurse = false) =>
        Directory.GetFiles(dir, "*.png",
            recurse ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly)
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
        AssetDatabase.LoadAssetAtPath<Sprite>($"Assets/Sprites/tiles/{TileSet}/{n}.png");

    static TileData.T Info(string n) => TileData.Map[$"{TileSet}/{n}"];

    // Drop one tile so its flat standable top lands exactly on topY.
    // Returns the object so callers can parent decoration to it.
    static GameObject Place(Transform parent, string tile, float cx, float topY,
                            int order, bool flip = false)
    {
        var t = Info(tile);
        var go = new GameObject(tile);
        go.transform.SetParent(parent);
        go.transform.position = new Vector3(cx, topY + t.surface - t.h / 2f, 0f);
        if (flip) go.transform.localScale = new Vector3(-1f, 1f, 1f);
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = Tile(tile);
        sr.sortingOrder = order;
        return go;
    }

    // A horizontal stretch of tiles from x0 to x1 with its top at topY.
    // ONE collider spans the whole stretch: a row of separate box colliders
    // leaves hairline seams that a running player catches on.
    static void Stretch(Transform parent, int layer, string name,
                        string mid, string left, string right,
                        float x0, float x1, float topY, float colliderH, int order)
    {
        var group = new GameObject(name) { layer = layer };
        group.transform.SetParent(parent);
        group.transform.position = new Vector3((x0 + x1) / 2f, topY, 0f);

        // A composite welds the child outlines into one continuous shape, so
        // the player cannot catch on the hairline seam between two tiles.
        group.AddComponent<Rigidbody2D>().bodyType = RigidbodyType2D.Static;
        var comp = group.AddComponent<CompositeCollider2D>();
        comp.geometryType = CompositeCollider2D.GeometryType.Polygons;
        comp.generationType = CompositeCollider2D.GenerationType.Synchronous;

        float x = x0;
        if (left != null)
        {
            var lt = Info(left);
            Outline(Place(group.transform, left, x + lt.w / 2f, topY, order + 1), layer);
            x += lt.w;
        }
        float rightW = right != null ? Info(right).w : 0f;
        var mt = Info(mid);
        while (x + mt.w <= x1 - rightW + 0.01f)
        {
            Outline(Place(group.transform, mid, x + mt.w / 2f, topY, order), layer);
            x += mt.w;
        }
        if (right != null)
            Outline(Place(group.transform, right, x1 - rightW / 2f, topY, order + 1), layer);
    }

    // Trace the sprite's own outline. Feeds a composite when there is one.
    static void Outline(GameObject go, int layer)
    {
        go.layer = layer;
        var pc = go.AddComponent<PolygonCollider2D>();
        pc.usedByComposite = go.transform.parent != null
                          && go.transform.parent.GetComponent<CompositeCollider2D>() != null;
    }

    // A single tile that you can stand on, collider traced from its pixels.
    static void Solid(Transform parent, int layer, string tile,
                      float cx, float topY, int order, bool flip = false)
    {
        Outline(Place(parent, tile, cx, topY, order, flip), layer);
    }

    // Decoration only, no collider. Aligned by the sprite's bottom edge.
    static void Decor(Transform parent, string tile, float cx, float bottomY, int order)
    {
        var t = Info(tile);
        var go = new GameObject(tile + "_decor");
        go.transform.SetParent(parent);
        go.transform.position = new Vector3(cx, bottomY + t.h / 2f, 0f);
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = Tile(tile);
        sr.sortingOrder = order;
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
        cam.orthographicSize = 6f;
        cam.transform.position = new Vector3(0f, 1f, -10f);
        // Solid colour, not Skybox: a 2D game has no skybox, and the leftover
        // default is the dark blue that shows through every gap in the art.
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.58f, 0.80f, 0.92f);   // daytime sky

        var music = Ensure<AudioSource>(cam.gameObject);
        music.clip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/bgm.wav");
        music.loop = true;
        music.playOnAwake = true;
        music.volume = 0.5f;

        // Daylight. The template's Global Light 2D is dim and cold, which is
        // what made everything read as night.
        var globalLight = GameObject.Find("Global Light 2D");
        if (globalLight != null && globalLight.TryGetComponent<Light2D>(out var l2d))
        {
            l2d.color = new Color(1f, 0.98f, 0.92f);   // warm daylight
            l2d.intensity = 1.15f;
        }

        var bg = new GameObject("Background");
        // Nearer layers scroll faster; that speed difference is what the eye
        // reads as depth. Sky barely moves, bamboo races past.
        var layers = new[]
        {
            ("bg1_sky",  0.10f, -3.0f, -100),
            ("bg2_far",  0.30f, -3.5f,  -90),
            ("bg3_mid",  0.55f, -4.0f,  -80),
            ("bg4_near", 0.85f, -4.5f,  -20),
        };
        foreach (var (file, factor, bottom, order) in layers)
        {
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(
                $"Assets/Sprites/backgrounds/{file}.png");
            if (sprite == null) continue;
            var go = new GameObject(file);
            go.transform.SetParent(bg.transform);
            // Use the sprite's own height so it only repeats sideways. Give it
            // a different height and Tiled mode starts stacking copies upward,
            // which is how you get a second horizon floating in the sky.
            float h = sprite.bounds.size.y;
            go.transform.position = new Vector3(LevelLength / 2f, bottom + h / 2f, 0f);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.drawMode = SpriteDrawMode.Tiled;
            sr.size = new Vector2(LevelLength + 80f, h);
            sr.sortingOrder = order;
            go.AddComponent<Parallax>().factor = factor;
        }

        var level = new GameObject("Level");
        var L = level.transform;

        // ---- solid ground, with gaps to cross ----
        var runs = new[] { (0f, 14f), (20f, 34f), (46f, 58f),
                           (72f, 86f), (102f, 116f), (132f, 158f) };
        foreach (var (x0, x1) in runs)
            Stretch(L, groundLayer, "Ground", "ground_mid", "ground_left", "ground_right",
                    x0, x1, 0f, 1.0f, -10);

        // ---- wide platform stretches ----
        var platRuns = new[] { (24f, 30f, 4.2f), (88f, 94f, 2.4f), (120f, 127f, 3.4f) };
        foreach (var (x0, x1, y) in platRuns)
            Stretch(L, groundLayer, "Platform", "plat_mid", "plat_left", "plat_right",
                    x0, x1, y, PlatSolidH, -5);

        // ---- single standable tiles: the parkour route ----
        // (tile, x, topY) - every remaining tile in the set gets used.
        var pieces = new (string tile, float x, float y)[]
        {
            ("plat_single", 17f,  1.8f),
            ("slab",        37f,  2.2f),
            ("ledge",       40.5f,3.6f),
            ("block_small", 43.5f,2.0f),
            ("edge_broken", 61f,  1.8f),
            ("slab",        64f,  3.2f),
            ("plat_single", 67f,  4.6f),
            ("ledge",       70f,  2.8f),
            ("block_small", 96.5f,4.0f),
            ("slab",        99f,  2.6f),
            ("block_rune",  110f, 3.6f),
            ("edge_broken", 118f, 1.6f),
            ("pillar_top",  130f, 4.8f),
            ("slab",        144f, 3.0f),
            ("plat_single", 148f, 4.6f),
            ("block_rune",  152f, 3.2f),
        };
        foreach (var (tile, x, y) in pieces)
            Solid(L, groundLayer, tile, x, y, -5);

        // ---- stairs, used as a real step up off the ground ----
        Solid(L, groundLayer, "stairs", 80f, 1.4f, -5);
        Solid(L, groundLayer, "stairs", 136f, 1.4f, -5, flip: true);

        // ---- pillars: decoration behind the action, plus one you can climb ----
        var decor = new GameObject("Decor");
        decor.transform.SetParent(L);
        foreach (var px in new[] { 6f, 30f, 52f, 78f, 108f, 140f })
        {
            Decor(decor.transform, "pillar_base", px, 0f, -30);
            Decor(decor.transform, "pillar_mid", px, 1.6f, -30);
            Decor(decor.transform, "pillar_top", px, 3.4f, -30);
        }
        // the climbable one under the standable pillar_top at x=130
        Decor(decor.transform, "pillar_base", 130f, 0f, -6);
        Decor(decor.transform, "pillar_mid", 130f, 1.6f, -6);

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

        // CameraFollow.cs (the one supplied with the assignment) is still in
        // the project, but it uses MoveTowards and feels stiff. This one
        // eases and leads the player instead.
        var oldFollow = cam.GetComponent<CameraFollow>();
        if (oldFollow != null) Object.DestroyImmediate(oldFollow);

        var follow = Ensure<PlayerCamera>(cam.gameObject);
        follow.target = player.transform;
        follow.smoothTime = 0.18f;
        follow.lookAhead = 2.5f;
        follow.offset = new Vector2(0f, 1.2f);
        follow.minY = 1.5f;


        EditorSceneManager.SaveScene(scene, OutScene);
        EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(OutScene, true) };
        AssetDatabase.SaveAssets();
    }
}
