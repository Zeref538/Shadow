using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Editor-only tool: sets up the prop prefabs and lays out the course.
// Nothing in here ships in the game - the built .exe only runs the four
// supplied scripts (CharacterController2D, playermovement, CameraFollow, trap).
//
//   Shadow > 1. Set Up Props     adds physics to the prefabs in Assets/Prefabs
//   Shadow > 2. Build Course     copies Course.unity's player, camera and
//                                background into Level.unity and builds the
//                                course around them
//
// Course.unity itself is never overwritten.
public static class CourseBuilder
{
    const string Prefabs = "Assets/Prefabs/";
    const string SourceScene = "Assets/Scenes/Course.unity";
    const string OutScene = "Assets/Scenes/Level.unity";

    static readonly string[] Tiles =
    {
        "ground_left", "ground_mid", "ground_right", "ledge", "edge_broken", "slab",
        "block_small", "block_rune", "pillar_base", "pillar_mid", "pillar_top",
        "plat_left", "plat_mid", "plat_right", "plat_single", "seesaw_pivot",
    };

    // ------------------------------------------------------------------ props

    [MenuItem("Shadow/1. Set Up Props")]
    public static void SetUpProps()
    {
        int ground = LayerMask.NameToLayer("Ground");

        foreach (var t in Tiles)
            EditPrefab(t, go => SetLayer(go, ground));

        // Pushable. Heavier = slower to shove, which is a puzzle on its own.
        EditPrefab("crate",        go => { Solid2D(go); Body(go, 3f);   SetLayer(go, ground); });
        EditPrefab("crate_small",  go => { Solid2D(go); Body(go, 1.5f); SetLayer(go, ground); });
        EditPrefab("stone_block",  go => { Solid2D(go); Body(go, 4f);   SetLayer(go, ground); });
        EditPrefab("barrel",       go => { Body(go, 2f);                SetLayer(go, ground); });
        EditPrefab("bridge_plank", go => { Solid2D(go); Body(go, 1f);   SetLayer(go, ground); });

        // Falling platforms: pinned to the world by a joint that holds the
        // platform's own weight but snaps under the player's. The weakest one
        // gives way fastest.
        EditPrefab("falling_intact",     go => Falling(go, 16f, topOnly: true));
        EditPrefab("falling_cracked",    go => Falling(go, 12f, topOnly: false));
        EditPrefab("falling_half_left",  go => Falling(go, 12f, topOnly: false));
        EditPrefab("falling_half_right", go => Falling(go, 20f, topOnly: false));

        // Seesaw plank pivots on its centre, limited so it rocks, not spins.
        EditPrefab("seesaw_plank", go =>
        {
            Solid2D(go);
            Body(go, 2f);
            SetLayer(go, ground);
            var hinge = Ensure<HingeJoint2D>(go);
            hinge.anchor = Local(go).center;
            hinge.useLimits = true;
            // 12 degrees: steeper than ~20 and the stone slides off, because the
            // slope (tan 22 = 0.40) beats the stone's default friction (0.4).
            hinge.limits = new JointAngleLimits2D { min = -12f, max = 12f };
        });

        // Swinging hazards: heavy so the player can't stop them, no angular
        // damping so they never slow down, pinned at the top of the chain.
        foreach (var n in new[] { "spiked_log", "spiked_ball", "pendulum_axe" })
            EditPrefab(n, go =>
            {
                if (go.GetComponent<Collider2D>() == null) go.AddComponent<PolygonCollider2D>();
                var rb = Body(go, 20f);
                rb.angularDamping = 0f;
                rb.linearDamping = 0f;
                var hinge = Ensure<HingeJoint2D>(go);
                var b = Local(go);
                hinge.anchor = new Vector2(b.center.x, b.max.y - 0.08f);
                Ensure<trap>(go);
            });

        AssetDatabase.SaveAssets();
        Debug.Log("CourseBuilder: props set up");
    }

    static void Falling(GameObject go, float breakForce, bool topOnly)
    {
        foreach (var c3d in go.GetComponents<Collider>()) Object.DestroyImmediate(c3d);
        if (topOnly)
        {
            // Only the stone slab is solid - not the crystals hanging under it,
            // or the player would stand on thin air below the surface.
            foreach (var c in go.GetComponents<Collider2D>()) Object.DestroyImmediate(c);
            var b = Local(go);
            var box = go.AddComponent<BoxCollider2D>();
            box.size = new Vector2(b.size.x * 0.94f, b.size.y * 0.36f);
            box.offset = new Vector2(b.center.x, b.max.y - box.size.y / 2f);
        }
        else if (go.GetComponent<Collider2D>() == null)
            go.AddComponent<PolygonCollider2D>();

        Body(go, 1f);
        var joint = Ensure<FixedJoint2D>(go);
        joint.connectedBody = null;                       // pinned to the world
        joint.autoConfigureConnectedAnchor = true;
        joint.breakForce = breakForce;
        joint.breakAction = JointBreakAction2D.Destroy;
        SetLayer(go, LayerMask.NameToLayer("Ground"));
    }

    // A 3D BoxCollider looks identical in the Inspector but 2D physics can't
    // see it, so the player walks straight through. Swap it for the 2D one.
    static void Solid2D(GameObject go)
    {
        foreach (var c3d in go.GetComponents<Collider>()) Object.DestroyImmediate(c3d);
        if (go.GetComponent<Collider2D>() != null) return;
        var b = Local(go);
        var box = go.AddComponent<BoxCollider2D>();
        box.size = b.size;
        box.offset = b.center;
    }

    static Rigidbody2D Body(GameObject go, float mass)
    {
        var rb = Ensure<Rigidbody2D>(go);
        rb.bodyType = RigidbodyType2D.Dynamic;
        rb.useAutoMass = false;
        rb.mass = mass;
        rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        rb.interpolation = RigidbodyInterpolation2D.Interpolate;
        return rb;
    }

    static Bounds Local(GameObject go) => go.GetComponent<SpriteRenderer>().sprite.bounds;

    static T Ensure<T>(GameObject go) where T : Component =>
        go.TryGetComponent<T>(out var c) ? c : go.AddComponent<T>();

    static void SetLayer(GameObject go, int layer)
    {
        foreach (var t in go.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = layer;
    }

    static string PrefabPath(string name)
    {
        foreach (var n in new[] { name, name + "_0" })
            if (File.Exists(Prefabs + n + ".prefab")) return Prefabs + n + ".prefab";
        return null;
    }

    static void EditPrefab(string name, System.Action<GameObject> fix)
    {
        var path = PrefabPath(name);
        if (path == null) { Debug.Log($"CourseBuilder: no prefab for {name}, skipped"); return; }
        var root = PrefabUtility.LoadPrefabContents(path);
        fix(root);
        PrefabUtility.SaveAsPrefabAsset(root, path);
        PrefabUtility.UnloadPrefabContents(root);
    }

    // ----------------------------------------------------------------- course

    static Transform level;
    static float jumpHeight, jumpRange;      // measured, not assumed

    [MenuItem("Shadow/2. Build Course")]
    public static void BuildCourse()
    {
        var scene = EditorSceneManager.OpenScene(SourceScene, OpenSceneMode.Single);
        var keep = new HashSet<string> { "Main Camera", "Global Light 2D", "Background", "Player" };
        foreach (var root in scene.GetRootGameObjects())
            if (!keep.Contains(root.name)) Object.DestroyImmediate(root);

        var player = GameObject.Find("Player");
        MeasureJump(player);
        Debug.Log($"CourseBuilder: jump height {jumpHeight:F2}, range {jumpRange:F2}");

        level = new GameObject("Level").transform;
        float x = Course();

        player.transform.position = new Vector3(2f, 0.05f, 0f);

        // The background came from the old 160-unit level. Stretch every
        // layer sideways to cover this course, with a margin at each end.
        var bg = GameObject.Find("Background");
        if (bg != null)
            foreach (var sr in bg.GetComponentsInChildren<SpriteRenderer>())
            {
                sr.size = new Vector2(x + 80f, sr.size.y);
                var p = sr.transform.position;
                sr.transform.position = new Vector3(x / 2f, p.y, p.z);
            }

        // Background music: looping, starts with the scene.
        var music = Object.FindFirstObjectByType<AudioSource>();
        if (music == null) music = GameObject.Find("Main Camera").AddComponent<AudioSource>();
        music.clip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/bgm.wav");
        music.loop = true;
        music.playOnAwake = true;
        music.volume = 0.5f;

        // The KillZone: solid, not a trigger - trap.cs listens for collisions
        // and reloads the scene, which puts the player back on the starting
        // line with every prop reset. It sits just under the lowest floor
        // (the well, ~-4.5) so a fall restarts quickly, and runs 40 units past
        // each end so walking off either edge of the map lands on it too.
        var kill = new GameObject("KillZone");
        kill.transform.position = new Vector3(x / 2f, -9f, 0f);
        kill.AddComponent<BoxCollider2D>().size = new Vector2(x + 80f, 2f);
        kill.AddComponent<trap>();

        EditorSceneManager.SaveScene(scene, OutScene);
        // trap.cs reloads by build index; a scene not in this list has none.
        EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(OutScene, true) };
        Debug.Log($"CourseBuilder: course built, {x:F0} units long, saved to {OutScene}");
    }

    // Push the real player straight up with the real jump force in a
    // private simulation and watch how high and how long it flies.
    static void MeasureJump(GameObject player)
    {
        var rb = player.GetComponent<Rigidbody2D>();
        var cc = new SerializedObject(player.GetComponent<CharacterController2D>());
        float force = cc.FindProperty("m_JumpForce").floatValue;
        float runSpeed = player.GetComponent<playermovement>().runSpeed;

        var start = player.transform.position;
        var mode = Physics2D.simulationMode;
        Physics2D.simulationMode = SimulationMode2D.Script;

        player.transform.position = new Vector3(0f, 900f, 0f);   // clear of everything
        Physics2D.SyncTransforms();
        rb.linearVelocity = Vector2.zero;
        rb.AddForce(new Vector2(0f, force));
        float top = 900f, t = 0f;
        do
        {
            Physics2D.Simulate(Time.fixedDeltaTime);
            t += Time.fixedDeltaTime;
            top = Mathf.Max(top, rb.position.y);
        } while (rb.position.y > 900f - 0.001f && t < 5f);

        jumpHeight = top - 900f;
        jumpRange = runSpeed * Time.fixedDeltaTime * 10f * t;    // same maths as CharacterController2D

        rb.linearVelocity = Vector2.zero;
        player.transform.position = start;
        Physics2D.SyncTransforms();
        Physics2D.simulationMode = mode;
    }

    // Eighteen sections, each built around a different idea, sized for
    // roughly 2-3 minutes of play (the player runs at ~8 units/s; puzzles and
    // timing the hazards take up the rest). Floor level is y = 0; every
    // section starts and ends there so they can go in any order.
    static float Course()
    {
        float x = 0f;
        x = Section("01 Warm-up",          x, WarmUp);
        x = Section("02 Ruined stairway",  x, RuinedStairway);
        x = Section("03 Crate climb",      x, CrateClimb);
        x = Section("04 Crumbling way",    x, CrumblingWay);
        x = Section("05 Bounce pits",      x, BouncePits);
        x = Section("06 Blade corridor",   x, BladeCorridor);
        x = Section("07 Seesaw",           x, Seesaw);
        x = Section("08 Boulder valley",   x, BoulderValley);
        x = Section("09 Rope bridge",      x, RopeBridge);
        x = Section("10 Low tunnel",       x, LowTunnel);
        x = Section("11 Iron gauntlet",    x, IronGauntlet);
        x = Section("12 Drop the crate",   x, DropTheCrate);
        x = Section("13 Hanging platforms", x, HangingPlatforms);
        x = Section("14 Pillar hop",       x, PillarHop);
        x = Section("15 Collapse climb",   x, CollapseClimb);
        x = Section("16 Seesaw bridge",    x, SeesawBridge);
        x = Section("17 Swinging pillars", x, SwingingPillars);
        x = Section("18 The well",         x, TheWellAndFinish);
        return x;
    }

    static Transform section;
    static float Section(string name, float x, System.Func<float, float> build)
    {
        section = new GameObject(name).transform;
        section.SetParent(level);
        float end = build(x);
        Debug.Log($"CourseBuilder: {name,-18} x {x,6:F1} -> {end,6:F1}");
        return end;
    }

    // Safe distances: well inside what the measured jump can do, so a normal
    // player makes it and a careless one doesn't.
    static float Gap(float f) => jumpRange * f;
    static float Rise(float f) => jumpHeight * f;

    // Flat ground, gaps that widen, a hurdle and a step up and down.
    static float WarmUp(float x)
    {
        x = Run(x, x + 16f, 0f, leftCap: true);   // the start line
        foreach (var g in new[] { 0.40f, 0.55f, 0.65f })
            x = Run(x + Gap(g), x + Gap(g) + 8f, 0f);
        float end = Run(x, x + 22f, 0f);
        PutCentre("block_small", x + 6f, 0f);            // hurdle, sitting on the floor
        FloorSpikes(x + 12f, x + 14f, 0f);               // then one strip of floor spikes to jump
        float step = Rise(0.45f);
        var face = Column(end, -3f, step);
        float top = Run(Right(face) - 0.05f, Right(face) + 8f, step);
        return Run(top + Gap(0.25f), top + Gap(0.25f) + 8f, 0f);
    }

    // Loose stones up over a pit, a rest at the top, stones back down,
    //    then a second, steeper climb on narrow platforms.
    static float RuinedStairway(float x)
    {
        float y = 0f;
        foreach (var n in new[] { "ledge", "slab", "block_rune", "edge_broken", "slab" })
        {
            y += Rise(0.36f);
            x = Right(Put(n, x + Gap(0.42f), y));
        }
        x = PlatRun(x + Gap(0.30f), 2, y);
        foreach (var n in new[] { "block_rune", "ledge", "edge_broken" })
        {
            y -= Rise(0.40f);
            x = Right(Put(n, x + Gap(0.45f), Mathf.Max(y, Rise(0.2f))));
        }
        x = Run(x + Gap(0.25f), x + Gap(0.25f) + 6f, 0f);
        y = 0f;
        foreach (var n in new[] { "block_rune", "ledge", "slab", "edge_broken" })
        {
            y += Rise(0.45f);
            x = Right(Put(n, x + Gap(0.40f), y));
        }
        return Run(x + Gap(0.30f), x + Gap(0.30f) + 8f, 0f);
    }

    // A two-storey climb. The first wall is too tall to jump: push the
    //    crate against it and climb up. Up there the second wall is too tall
    //    again, and the stone block is the step - push it over and climb on.
    static float CrateClimb(float x)
    {
        float y = 0f;
        foreach (var box in new[] { "crate", "stone_block" })
        {
            float faceX = Run(x, x + 16f, y);
            PutUnder(box, x + 4f, y);
            y += Wall(box);
            x = Right(Column(faceX, -3f, y)) - 0.05f;      // column reaches the ground
        }
        float top = Run(x, x + 8f, y);
        return Run(top + Gap(0.2f), top + Gap(0.2f) + 8f, 0f);      // drop back down
    }

    // Too tall to jump from the floor, easy from on top of `box`.
    static float Wall(string box) => (Rise(1.04f) + Height(Load(box)) + Rise(0.82f)) / 2f;

    // A long chain of platforms that give way under you, up and down in
    //    height, with one solid rest stop in the middle.
    static float CrumblingWay(float x)
    {
        string[] chain = { "falling_intact", "falling_cracked", "falling_half_left", "falling_half_right" };
        float[] heights = { 0.12f, 0.30f, 0.18f, 0.40f };
        for (int i = 0; i < 4; i++) x = Right(Put(chain[i], x + Gap(0.42f), Rise(heights[i])));
        x = Right(Put("ledge", x + Gap(0.42f), Rise(0.25f)));              // rest stop
        for (int i = 3; i >= 0; i--) x = Right(Put(chain[i], x + Gap(0.42f), Rise(heights[3 - i])));
        return Run(x + Gap(0.30f), x + Gap(0.30f) + 8f, 0f);
    }

    // A long corridor with three spiked logs out of step with each other,
    //    so each one has to be timed on its own.
    static float BladeCorridor(float x)
    {
        float end = Run(x, x + 52f, 0f);
        float[] logs = { 8f, 17f, 26f, 35f, 44f };
        float[] angles = { 80f, -70f, 85f, -80f, 75f };
        for (int i = 0; i < logs.Length; i++) Swing("spiked_log", x + logs[i], 0f, angles[i]);
        FloorSpikes(x + 20.5f, x + 22f, 0f);            // jump these while timing the logs
        FloorSpikes(x + 38.5f, x + 40f, 0f);
        return end;
    }

    // The stone on the seesaw holds the far end up as a step to a ledge
    //    too high to reach from the floor.
    static float Seesaw(float x)
    {
        float floorEnd = Run(x, x + 18f, 0f);
        var pivot = Put("seesaw_pivot", x + 8f, 0f, useColliderTop: false);
        float pivotTop = Top(pivot, collider: false);
        float cx = (Left(pivot) + Right(pivot)) / 2f;
        var plank = PutCentre("seesaw_plank", cx, pivotTop);
        PutUnder("stone_block", Left(plank) + 0.8f, Top(plank, collider: true) + 0.02f);
        float ledgeY = pivotTop + Rise(0.55f) + 1.2f;
        float ledgeX = Right(plank) + 0.6f;
        var face = Column(ledgeX, -3f, ledgeY);
        float top = Run(Right(face) - 0.05f, Right(face) + 10f, ledgeY);
        float down = Run(top + Gap(0.2f), top + Gap(0.2f) + 6f, 0f);
        return Mathf.Max(down, floorEnd);
    }

    // A low ceiling so there's no jumping. Shove the stone block off the
    //    end, cross the gap, then do it again with a crate in a second tunnel.
    static float LowTunnel(float x)
    {
        foreach (var block in new[] { "stone_block", "crate" })
        {
            float end = Run(x, x + 22f, 0f);
            for (float cx = x + 3f; cx < end - 3f;)
                cx = Right(PutUnder("slab", cx, 2.6f)) - 0.05f;
            PutUnder(block, x + 5f, 0f);
            x = Run(end + Gap(0.55f), end + Gap(0.55f) + 8f, 0f);
        }
        return x;
    }

    // Up a step onto a ledge with a crate on it. The far wall is too tall to
    // jump and sits down on the lower floor, so the crate has to be shoved
    // off the end of the ledge first, then pushed across to the wall.
    static float DropTheCrate(float x)
    {
        float ledgeY = Rise(0.45f);
        float faceX = Run(x, x + 6f, 0f);
        float ledgeStart = Right(Column(faceX, -3f, ledgeY)) - 0.05f;
        float ledgeEnd = Run(ledgeStart, ledgeStart + 12f, ledgeY);
        PutUnder("crate", ledgeStart + 4f, ledgeY);
        float wallX = Run(ledgeEnd - 0.05f, ledgeEnd + 14f, 0f);
        float wall = Wall("crate");
        float top = Run(Right(Column(wallX, -3f, wall)) - 0.05f, wallX + 10f, wall);
        return Run(top + Gap(0.2f), top + Gap(0.2f) + 8f, 0f);
    }

    // Falling platforms that climb upwards over a pit, then a high walkway
    // with a spiked log swinging across it.
    static float CollapseClimb(float x)
    {
        string[] chain = { "falling_cracked", "falling_half_right", "falling_intact", "falling_half_left", "falling_cracked" };
        float y = 0f;
        foreach (var n in chain)
        {
            y += Rise(0.30f);
            x = Right(Put(n, x + Gap(0.38f), y));
        }
        float start = x + Gap(0.30f);
        float end = Run(start, start + 16f, y);
        Swing("spiked_log", start + 9f, y, 80f);
        FloorSpikes(start + 4f, start + 5.5f, y);
        return Run(end + Gap(0.2f), end + Gap(0.2f) + 8f, 0f);
    }

    // Two free seesaws on stone columns over a deep pit. Nothing holds them
    // level - they tip as soon as you land, so hop across without stopping.
    static float SeesawBridge(float x)
    {
        x = Run(x, x + 8f, 0f);
        float plankW = Width(Load("seesaw_plank"));
        for (int i = 0; i < 2; i++)
        {
            float cx = x + Gap(0.40f) + plankW / 2f;
            var col = Column(cx - Width(Load("pillar_top")) / 2f, -12f, 0f);
            float colCx = (Left(col) + Right(col)) / 2f;
            var pivot = PutCentre("seesaw_pivot", colCx, Top(col, collider: true));
            var plank = PutCentre("seesaw_plank", colCx, Top(pivot, collider: false));
            x = Right(plank);
        }
        return Run(x + Gap(0.30f), x + Gap(0.30f) + 10f, 0f);
    }

    // Stone columns with a spiked ball swinging over every gap: time each
    // jump for when the ball is on the far side.
    static float SwingingPillars(float x)
    {
        float[] heights = { 1.0f, 1.6f, 1.0f, 2.0f, 1.2f };
        float prevTop = 0f;
        float prevRight = x;
        for (int i = 0; i < heights.Length; i++)
        {
            float gap = Gap(0.46f);                 // keeps neighbouring balls from colliding
            var col = Column(prevRight + gap, -12f, heights[i]);
            Swing("spiked_ball", prevRight + gap / 2f, Mathf.Min(prevTop, heights[i]), i % 2 == 0 ? 80f : -80f);
            prevTop = heights[i];
            prevRight = Right(col);
        }
        return Run(prevRight + Gap(0.35f), prevRight + Gap(0.35f) + 8f, 0f);
    }

    // A valley made of two ramps with a spiked boulder rolling back and forth
    // in it. It starts part-way down one slope, so it never has the energy
    // to climb out to the rims: wait on the rim, then run across while it
    // rolls away. Pure physics - the boulder only carries trap.cs.
    static float BoulderValley(float x)
    {
        var down = Ramp(x, 0f, descending: true);
        float lowY = RampLowTop(down);
        float bottomStart = Right(down) - 0.3f;
        float bottomEnd = Run(bottomStart, bottomStart + 4f, lowY);
        var up = Ramp(bottomEnd - 0.3f, 0f, descending: false);
        Boulder(down, 0.15f);                            // high start: fast, reaches close to the rims
        return Run(Right(up) - 0.05f, Right(up) + 8f, 0f);
    }

    // ramp.png: 640x259 at 100 px/unit, low on the left, high on the right.
    // Its collider follows the stone top, not the hanging vines. Points are
    // in pixels (y down) and converted to local units around the centre.
    const string RampSprite = "Assets/Sprites/tiles/ruin/ramp.png";
    static readonly Vector2[] RampPx = { new Vector2(4, 218), new Vector2(4, 204), new Vector2(575, 3), new Vector2(636, 3), new Vector2(636, 258) };
    static Vector2 RampLocal(Vector2 px) => new Vector2((px.x - 320f) / 100f, (129.5f - px.y) / 100f);

    static GameObject Ramp(float left, float rimY, bool descending)
    {
        var go = new GameObject(descending ? "ramp_down" : "ramp_up");
        go.transform.SetParent(section);
        go.AddComponent<SpriteRenderer>().sprite = AssetDatabase.LoadAssetAtPath<Sprite>(RampSprite);
        if (descending) go.transform.localScale = new Vector3(-1f, 1f, 1f);
        var edge = go.AddComponent<EdgeCollider2D>();
        var pts = new Vector2[RampPx.Length];
        for (int i = 0; i < pts.Length; i++) pts[i] = RampLocal(RampPx[i]);
        edge.points = pts;
        go.layer = LayerMask.NameToLayer("Ground");
        go.transform.position = new Vector3(left + 3.2f, rimY - RampLocal(RampPx[2]).y, 0f);
        Physics2D.SyncTransforms();
        return go;
    }

    static float RampLowTop(GameObject ramp) => ramp.transform.TransformPoint(RampLocal(RampPx[1])).y;

    // The spiked boulder, resting on a ramp's slope `t` of the way down from
    // the rim.
    static void Boulder(GameObject ramp, float t)
    {
        var sprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/props/spiked_boulder.png");
        Vector2 low = ramp.transform.TransformPoint(RampLocal(RampPx[1]));
        Vector2 high = ramp.transform.TransformPoint(RampLocal(RampPx[2]));
        Vector2 along = (low - high).normalized;
        Vector2 normal = new Vector2(-along.y, along.x);
        if (normal.y < 0f) normal = -normal;
        float r = Mathf.Min(sprite.bounds.size.x, sprite.bounds.size.y) * 0.42f;   // the ball, not the spike tips
        var go = new GameObject("spiked_boulder");
        go.transform.SetParent(section);
        go.AddComponent<SpriteRenderer>().sprite = sprite;
        go.transform.position = Vector2.Lerp(high, low, t) + normal * (r + 0.02f);
        go.AddComponent<CircleCollider2D>().radius = r;
        var rb = go.AddComponent<Rigidbody2D>();
        rb.mass = 5f;
        rb.linearDamping = 0f;
        rb.angularDamping = 0f;
        rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        rb.interpolation = RigidbodyInterpolation2D.Interpolate;
        go.AddComponent<trap>();
        Physics2D.SyncTransforms();
    }

    // Two pits too wide to jump, each with a bounce pad on a column at the
    // bottom and a higher ledge on the far side. Drop onto the pad and it
    // throws you back up - steer across to the ledge while you're in the air.
    static float BouncePits(float x)
    {
        x = Run(x, x + 8f, 0f);
        float y = 0f;
        foreach (float target in new[] { 2.0f, 3.5f })
        {
            float padX = x + 4.5f;                       // too far to jump straight across
            var col = Column(padX - Width(Load("pillar_top")) / 2f, -12f, y - 3f);
            BouncePad((Left(col) + Right(col)) / 2f, Top(col, collider: true));
            var face = Column(padX + 4.5f, -12f, target);
            x = Run(Right(face) - 0.05f, Right(face) + 6f, target);
            y = target;
        }
        return Run(x + Gap(0.2f), x + Gap(0.2f) + 8f, 0f);
    }

    // A rope bridge of loose planks hinged end to end across a gap too wide
    // to jump, pinned only at the two banks, so it sags and sways as you
    // cross. A spiked log swings over the middle.
    static float RopeBridge(float x)
    {
        float bank = Run(x, x + 8f, 0f);
        var post = SpriteObj("bridge_post", "Assets/Sprites/props/bridge_post.png");
        post.transform.position = new Vector3(bank - 0.9f, 1.2f, 0f);    // decoration on each bank
        float end = PlankChain(bank + 0.05f, 0f, 9);
        var post2 = SpriteObj("bridge_post", "Assets/Sprites/props/bridge_post.png");
        post2.transform.position = new Vector3(end + 0.9f, 1.2f, 0f);
        Swing("spiked_log", bank + (end - bank) * 0.33f, 0f, 80f);
        Swing("spiked_log", bank + (end - bank) * 0.70f, 0f, -80f);
        return Run(end + 0.1f, end + 12f, 0f);
    }

    // Stone slabs hung from chains over a pit. They hang still until you
    // land, then swing with you on them; the last two already swing.
    static float HangingPlatforms(float x)
    {
        x = Run(x, x + 8f, 0f);
        float[] start = { 0f, 14f, 14f, 14f };       // same tilt: the swingers move in step
        foreach (float angle in start)
            x = HangingPlatform(x + Gap(0.40f), 0.3f, angle);
        return Run(x + Gap(0.30f), x + Gap(0.30f) + 10f, 0f);
    }

    // ------------------------------------------------ pasted-in props
    // All at 100 px per unit. Collider boxes are in pixels (y down) of the
    // saved sprite and cover only what you can stand on.

    static GameObject SpriteObj(string name, string path)
    {
        var go = new GameObject(name);
        go.transform.SetParent(section);
        go.AddComponent<SpriteRenderer>().sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
        return go;
    }

    static Vector2 Px(GameObject go, float px, float py)
    {
        var r = go.GetComponent<SpriteRenderer>().sprite.rect;
        return new Vector2((px - r.width / 2f) / 100f, (r.height / 2f - py) / 100f);
    }

    static BoxCollider2D PxBox(GameObject go, float x0, float y0, float x1, float y1)
    {
        var a = Px(go, x0, y0);
        var b = Px(go, x1, y1);
        var box = go.AddComponent<BoxCollider2D>();
        box.offset = (a + b) / 2f;
        box.size = new Vector2(Mathf.Abs(b.x - a.x), Mathf.Abs(b.y - a.y));
        return box;
    }

    // bounce_pad.png 250x132: the glowing cap is the spring. Bounciness 1
    // sends you back up about as high as you fell from.
    static void BouncePad(float cx, float floorY)
    {
        var go = SpriteObj("bounce_pad", "Assets/Sprites/props/bounce_pad.png");
        go.transform.position = new Vector3(cx, floorY - Px(go, 0f, 131f).y, 0f);
        var box = PxBox(go, 20f, 24f, 230f, 52f);
        box.sharedMaterial = BouncyMaterial();
        go.layer = LayerMask.NameToLayer("Ground");
        Physics2D.SyncTransforms();
    }

    static PhysicsMaterial2D BouncyMaterial()
    {
        const string path = "Assets/Settings/Bouncy.physicsMaterial2D";
        var mat = AssetDatabase.LoadAssetAtPath<PhysicsMaterial2D>(path);
        if (mat != null) return mat;
        mat = new PhysicsMaterial2D("Bouncy") { bounciness = 1f, friction = 0.4f };
        AssetDatabase.CreateAsset(mat, path);
        return mat;
    }

    // bridge_plank_rope.png 160x26: rope loops at x=4 and x=156 are where the
    // hinges go. The first and last loops are pinned to the world.
    static float PlankChain(float left, float topY, int count)
    {
        Rigidbody2D prev = null;
        float x = left;
        int ground = LayerMask.NameToLayer("Ground");
        for (int i = 0; i < count; i++)
        {
            var go = SpriteObj("bridge_plank", "Assets/Sprites/props/bridge_plank_rope.png");
            var loopL = Px(go, 4f, 13f);
            go.transform.position = new Vector3(x - loopL.x, topY - Px(go, 0f, 2f).y, 0f);
            PxBox(go, 2f, 2f, 158f, 24f);
            go.layer = ground;
            var rb = go.AddComponent<Rigidbody2D>();
            rb.mass = 0.4f;
            rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            rb.interpolation = RigidbodyInterpolation2D.Interpolate;
            var hinge = go.AddComponent<HingeJoint2D>();
            hinge.anchor = loopL;
            hinge.connectedBody = prev;                  // null = pinned to the bank
            hinge.autoConfigureConnectedAnchor = true;
            prev = rb;
            x = go.transform.TransformPoint(Px(go, 156f, 13f)).x;
        }
        var last = prev.gameObject;
        var pin = last.AddComponent<HingeJoint2D>();
        pin.anchor = Px(last, 156f, 13f);
        pin.connectedBody = null;                        // pinned to the far bank
        pin.autoConfigureConnectedAnchor = true;
        Physics2D.SyncTransforms();
        return x;
    }

    // hanging_platform.png 360x536: ring centre at (180, 29), slab from
    // y=393 to 445. Hinged at the ring, which hangs from a ceiling bracket.
    static float HangingPlatform(float left, float slabTop, float startAngle)
    {
        var go = SpriteObj("hanging_platform", "Assets/Sprites/props/hanging_platform.png");
        go.transform.position = new Vector3(left + 1.8f, slabTop - Px(go, 0f, 393f).y, 0f);
        PxBox(go, 14f, 393f, 346f, 445f);
        go.layer = LayerMask.NameToLayer("Ground");
        var rb = go.AddComponent<Rigidbody2D>();
        rb.mass = 2f;
        rb.angularDamping = 0.05f;
        rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        rb.interpolation = RigidbodyInterpolation2D.Interpolate;
        var hinge = go.AddComponent<HingeJoint2D>();
        hinge.anchor = Px(go, 180f, 29f);
        hinge.connectedBody = null;
        Vector2 ring = go.transform.TransformPoint(hinge.anchor);
        PutCentre("ceiling_bracket", ring.x, ring.y - 0.15f);
        float right = left + 3.6f;
        go.transform.RotateAround(ring, Vector3.forward, startAngle);
        Physics2D.SyncTransforms();
        return right;
    }

    // Stone columns of uneven height over a deep drop.
    static float PillarHop(float x)
    {
        float[] heights = { 0.6f, 2.0f, 1.0f, 2.6f, 1.4f, 3.0f, 2.0f, 0.8f, 1.8f, 3.2f, 1.2f };
        foreach (var h in heights) x = Right(Column(x + Gap(0.48f), -12f, h));
        return Run(x + Gap(0.35f), x + Gap(0.35f) + 8f, 0f);
    }

    // Platforms over a pit with two spiked balls sweeping the gaps.
    static float IronGauntlet(float x)
    {
        x = PlatRun(x + Gap(0.35f), 1, Rise(0.15f));
        float gap1 = x;
        x = PlatRun(x + Gap(0.52f), 2, Rise(0.30f));
        Swing("spiked_ball", gap1 + Gap(0.52f) / 2f, Rise(0.15f), 80f);
        float gap2 = x;
        x = PlatRun(x + Gap(0.52f), 1, Rise(0.20f));
        Swing("spiked_ball", gap2 + Gap(0.52f) / 2f, Rise(0.20f), -80f);
        return Run(x + Gap(0.30f), x + Gap(0.30f) + 10f, 0f);
    }

    // Drop into the well, climb out on the barrel, and run to the wall
    // at the end of the course.
    static float TheWellAndFinish(float x)
    {
        float barrelH = Height(Load("barrel"));
        float depth = (Rise(1.04f) + barrelH + Rise(0.82f)) / 2f;
        float lip = Run(x, x + 8f, 0f);
        float floorEnd = Run(lip, lip + 10f, -depth);
        PutUnder("barrel", lip + 3f, -depth);
        var face = Column(floorEnd, -depth - 3f, 0f);
        float end = Run(Right(face) - 0.05f, Right(face) + 16f, 0f);
        FloorSpikes(end - 9f, end - 7.5f, 0f);           // one last jump before the end wall
        Column(end - 1f, 0f, Rise(1.6f));
        return end;
    }

    // ---------------------------------------------------------------- placing

    // ground_left is an end cap: a walkway with a tall broken pillar on its
    // left. It's used once, at the very start of the course (leftCap: true);
    // every other run starts with plain walkway. Its walkway - not the
    // pillar - goes at `y`.
    static float Run(float x0, float x1, float y, bool leftCap = false)
    {
        GameObject go;
        if (leftCap)
        {
            go = Put("ground_left", x0, y);
            go.transform.position += Vector3.up * (y - Walkway(go));
            Physics2D.SyncTransforms();
        }
        else go = Put("ground_mid", x0, y);
        float x = Right(go) - 0.05f;           // overlap a hair so edges meet
        float rightW = Width(Load("ground_right"));
        while (x + rightW < x1)
        {
            go = Put("ground_mid", x, y);
            x = Right(go) - 0.05f;
        }
        return Right(Put("ground_right", x, y));
    }

    // Highest collider point on the right half of the piece: the walkway,
    // ignoring anything sticking up on the left.
    static float Walkway(GameObject go)
    {
        float mid = go.GetComponent<Renderer>().bounds.center.x;
        float best = float.MinValue;
        foreach (var c in go.GetComponentsInChildren<Collider2D>())
            foreach (var p in Points(c))
            {
                var w = c.transform.TransformPoint(p);
                if (w.x > mid && w.y > best) best = w.y;
            }
        return best;
    }

    static float PlatRun(float x, int mids, float y)
    {
        x = Right(Put("plat_left", x, y)) - 0.05f;
        for (int i = 0; i < mids; i++) x = Right(Put("plat_mid", x, y)) - 0.05f;
        return Right(Put("plat_right", x, y));
    }

    // A stone column whose top surface sits at `top`, filled down to `bottom`.
    static GameObject Column(float x, float bottom, float top)
    {
        var cap = Put("pillar_top", x, top);
        float cx = (Left(cap) + Right(cap)) / 2f;
        float y = cap.GetComponent<Renderer>().bounds.min.y + 0.05f;
        while (y > bottom - 0.5f)
        {
            var mid = PutCentre("pillar_mid", cx, y, byRendererTop: true);
            y = mid.GetComponent<Renderer>().bounds.min.y + 0.05f;
        }
        return cap;
    }

    static void Swing(string name, float cx, float floorY, float startAngle)
    {
        var hazard = PrefabUtility.InstantiatePrefab(Load(name), section) as GameObject;
        var b = hazard.GetComponent<Renderer>().bounds;
        float lowest = floorY + 1.3f;                         // clears the floor, not the player
        hazard.transform.position += new Vector3(cx - b.center.x, lowest - b.min.y, 0f);
        var hinge = hazard.GetComponent<HingeJoint2D>();
        var pin = hazard.transform.TransformPoint(hinge.anchor);
        PutCentre("ceiling_bracket", pin.x, pin.y - 0.15f);
        hazard.transform.RotateAround(pin, Vector3.forward, startAngle);
    }

    // A row of floor spikes from Assets/Sprites/props/spikes_floor.png, each
    // with trap.cs so touching one restarts the level. Skipped (with a note
    // in the Console) until that sprite has been added.
    const string SpikeSprite = "Assets/Sprites/props/spikes_floor.png";
    static void FloorSpikes(float left, float right, float floorY)
    {
        var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(SpikeSprite);
        if (sprite == null) { Debug.Log($"CourseBuilder: no {SpikeSprite} yet, floor spikes skipped"); return; }
        for (float x = left; x < right;)
        {
            var go = new GameObject("spikes_floor");
            go.transform.SetParent(section);
            go.AddComponent<SpriteRenderer>().sprite = sprite;
            var b = sprite.bounds;
            go.transform.position = new Vector3(x - b.min.x, floorY - b.min.y, 0f);
            go.AddComponent<PolygonCollider2D>();
            go.AddComponent<trap>();
            x += b.size.x - 0.02f;
        }
        Physics2D.SyncTransforms();
    }

    // Place by the walkable top (collider) at `top`, left edge at `left`.
    static GameObject Put(string name, float left, float top, bool useColliderTop = true)
    {
        var go = Spawn(name);
        var r = go.GetComponent<Renderer>().bounds;
        float t = useColliderTop ? ColliderTop(go) ?? r.max.y : r.min.y;
        go.transform.position += new Vector3(left - r.min.x, top - t, 0f);
        Physics2D.SyncTransforms();
        return go;
    }

    // Centred on cx, with the picture's top (or bottom) edge at y.
    static GameObject PutCentre(string name, float cx, float y, bool byRendererTop = false)
    {
        var go = Spawn(name);
        var r = go.GetComponent<Renderer>().bounds;
        float t = byRendererTop ? r.max.y : r.min.y;
        go.transform.position += new Vector3(cx - r.center.x, y - t, 0f);
        Physics2D.SyncTransforms();
        return go;
    }

    // Ceiling piece: its lowest collider point sits at `underside`.
    static GameObject PutUnder(string name, float left, float underside)
    {
        var go = Spawn(name);
        var r = go.GetComponent<Renderer>().bounds;
        float b = ColliderBottom(go) ?? r.min.y;
        go.transform.position += new Vector3(left - r.min.x, underside - b, 0f);
        Physics2D.SyncTransforms();
        return go;
    }

    static GameObject Spawn(string name)
    {
        var go = PrefabUtility.InstantiatePrefab(Load(name), section) as GameObject;
        go.transform.position = Vector3.zero;
        go.transform.rotation = Quaternion.identity;
        Physics2D.SyncTransforms();
        return go;
    }

    static readonly Dictionary<string, GameObject> cache = new();

    static GameObject Load(string name)
    {
        if (cache.TryGetValue(name, out var hit) && hit != null) return hit;
        var path = PrefabPath(name);
        var go = path != null ? AssetDatabase.LoadAssetAtPath<GameObject>(path) : MakeMissing(name);
        return cache[name] = go;
    }

    // plat_single was drawn but never saved as a prefab: build it from the
    // sprite with a traced polygon collider, sized like plat_mid.
    static GameObject MakeMissing(string name)
    {
        if (name != "plat_single") throw new System.Exception($"CourseBuilder: no prefab for {name}");
        var sprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/tiles/ruin/plat_single.png");
        var go = new GameObject("plat_single");
        go.AddComponent<SpriteRenderer>().sprite = sprite;
        go.AddComponent<PolygonCollider2D>();
        go.transform.localScale = Load("plat_mid").transform.localScale;
        SetLayer(go, LayerMask.NameToLayer("Ground"));
        var path = Prefabs + "plat_single.prefab";
        var prefab = PrefabUtility.SaveAsPrefabAsset(go, path);
        Object.DestroyImmediate(go);
        Debug.Log("CourseBuilder: made plat_single prefab with a polygon collider");
        return prefab;
    }

    static float Left(GameObject go) => go.GetComponent<Renderer>().bounds.min.x;
    static float Right(GameObject go) => go.GetComponent<Renderer>().bounds.max.x;
    static float Top(GameObject go, bool collider) =>
        collider ? ColliderTop(go) ?? go.GetComponent<Renderer>().bounds.max.y
                 : go.GetComponent<Renderer>().bounds.max.y;

    static float Width(GameObject prefab)
    {
        var sr = prefab.GetComponent<SpriteRenderer>();
        return sr.sprite.bounds.size.x * Mathf.Abs(prefab.transform.localScale.x);
    }

    static float Height(GameObject prefab)
    {
        var sr = prefab.GetComponent<SpriteRenderer>();
        return sr.sprite.bounds.size.y * Mathf.Abs(prefab.transform.localScale.y);
    }

    static float? ColliderTop(GameObject go) => Extreme(go, top: true);
    static float? ColliderBottom(GameObject go) => Extreme(go, top: false);

    static float? Extreme(GameObject go, bool top)
    {
        float? best = null;
        foreach (var c in go.GetComponentsInChildren<Collider2D>())
            foreach (var p in Points(c))
            {
                float y = c.transform.TransformPoint(p).y;
                if (best == null || (top ? y > best : y < best)) best = y;
            }
        return best;
    }

    static IEnumerable<Vector2> Points(Collider2D c)
    {
        switch (c)
        {
            case EdgeCollider2D e:
                foreach (var p in e.points) yield return p + e.offset;
                break;
            case PolygonCollider2D pc:
                for (int i = 0; i < pc.pathCount; i++)
                    foreach (var p in pc.GetPath(i)) yield return p + pc.offset;
                break;
            case BoxCollider2D b:
                var h = b.size / 2f;
                yield return b.offset + new Vector2(-h.x, h.y);
                yield return b.offset + h;
                yield return b.offset - h;
                yield return b.offset + new Vector2(h.x, -h.y);
                break;
            case CircleCollider2D cc:
                yield return cc.offset + Vector2.up * cc.radius;
                yield return cc.offset + Vector2.down * cc.radius;
                break;
        }
    }

    // Run the saved level for a few seconds with no player and report
    // anything that misbehaves on its own. The scene is reopened afterwards
    // without saving, so the simulation never leaks into the file.
    public static void CheckPhysics()
    {
        var scene = EditorSceneManager.OpenScene(OutScene, OpenSceneMode.Single);
        var player = GameObject.Find("Player");
        if (player != null) player.SetActive(false);

        var bodies = Object.FindObjectsByType<Rigidbody2D>();
        var start = new Dictionary<Rigidbody2D, (Vector2 pos, float rot)>();
        foreach (var rb in bodies) start[rb] = (rb.position, rb.rotation);

        var mode = Physics2D.simulationMode;
        Physics2D.simulationMode = SimulationMode2D.Script;
        for (int i = 0; i < 200; i++) Physics2D.Simulate(0.02f);   // 4 seconds
        Physics2D.simulationMode = mode;

        // Anything tilted to the same angle as a seesaw, and only a short
        // way down, is resting on it and riding it - not falling off.
        float seesawTilt = -1f;
        foreach (var rb in bodies)
            if (rb.name.StartsWith("seesaw_plank"))
                seesawTilt = Mathf.Abs(Mathf.DeltaAngle(start[rb].rot, rb.rotation));

        int problems = 0;
        foreach (var rb in bodies)
        {
            var (pos, rot) = start[rb];
            float drop = pos.y - rb.position.y;
            float turned = Mathf.Abs(Mathf.DeltaAngle(rot, rb.rotation));
            string n = rb.name.Replace("(Clone)", "");
            bool swings = rb.GetComponent<HingeJoint2D>() != null && rb.GetComponent<trap>() != null;
            bool falls = rb.GetComponent<FixedJoint2D>() != null || n.StartsWith("falling");
            bool jointGone = n.StartsWith("falling") && rb.GetComponent<FixedJoint2D>() == null;

            string verdict = "ok";
            bool riding = seesawTilt > 1f && Mathf.Abs(turned - seesawTilt) < 1f && drop < 1f;
            if (rb.position.y < -10f) verdict = "FELL THROUGH THE WORLD";
            else if (riding) verdict = "ok (riding the seesaw)";
            else if (jointGone || (falls && drop > 0.1f)) verdict = "DROPPED WITHOUT A PLAYER";
            else if (swings && turned < 5f) verdict = "NOT SWINGING";
            else if (!swings && !falls && n != "seesaw_plank" && drop > 0.3f) verdict = "SANK OR SLID OFF";
            else if (drop < -0.3f) verdict = "POPPED OUT OF SOMETHING";
            if (!verdict.StartsWith("ok")) problems++;
            Debug.Log($"PHYSICS {n,-20} drop {drop,6:F2}  turned {turned,6:F1}  {verdict}");
        }
        Debug.Log($"PHYSICS {bodies.Length} bodies, {problems} problems");
        EditorSceneManager.OpenScene(OutScene, OpenSceneMode.Single);    // discard the simulated state
    }

    // Batch entry point: both steps in one headless run.
    public static void Batch()
    {
        SetUpProps();
        BuildCourse();
        CheckPhysics();
    }
}
