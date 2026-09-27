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

    // Ten sections, each built around a different idea, sized for roughly
    // 2-3 minutes of play (the player runs at ~8 units/s; puzzles and
    // timing the hazards take up the rest). Floor level is y = 0; every
    // section starts and ends there so they can go in any order.
    static float Course()
    {
        float x = 0f;
        x = Section("01 Warm-up",          x, WarmUp);
        x = Section("02 Ruined stairway",  x, RuinedStairway);
        x = Section("03 Crate climb",      x, CrateClimb);
        x = Section("04 Crumbling way",    x, CrumblingWay);
        x = Section("05 Blade corridor",   x, BladeCorridor);
        x = Section("06 Seesaw",           x, Seesaw);
        x = Section("07 Low tunnel",       x, LowTunnel);
        x = Section("08 Pillar hop",       x, PillarHop);
        x = Section("09 Iron gauntlet",    x, IronGauntlet);
        x = Section("10 The well",         x, TheWellAndFinish);
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

    // 1. Flat ground, gaps that widen, a hurdle and a step up and down.
    static float WarmUp(float x)
    {
        x = Run(x, x + 16f, 0f);
        foreach (var g in new[] { 0.30f, 0.40f, 0.50f })
            x = Run(x + Gap(g), x + Gap(g) + 8f, 0f);
        float end = Run(x, x + 14f, 0f);
        PutCentre("block_small", x + 6f, 0f);            // hurdle, sitting on the floor
        float step = Rise(0.45f);
        var face = Column(end, -3f, step);
        float top = Run(Right(face) - 0.05f, Right(face) + 8f, step, leftCap: false);
        return Run(top + Gap(0.25f), top + Gap(0.25f) + 8f, 0f);
    }

    // 2. Loose stones up over a pit, a rest at the top, stones back down,
    //    then a second, steeper climb on narrow platforms.
    static float RuinedStairway(float x)
    {
        float y = 0f;
        foreach (var n in new[] { "ledge", "slab", "block_rune", "edge_broken", "slab" })
        {
            y += Rise(0.30f);
            x = Right(Put(n, x + Gap(0.33f), y));
        }
        x = PlatRun(x + Gap(0.30f), 2, y);
        foreach (var n in new[] { "block_rune", "ledge", "edge_broken" })
        {
            y -= Rise(0.35f);
            x = Right(Put(n, x + Gap(0.35f), Mathf.Max(y, Rise(0.2f))));
        }
        x = Run(x + Gap(0.25f), x + Gap(0.25f) + 6f, 0f);
        y = 0f;
        for (int i = 0; i < 4; i++)
        {
            y += Rise(0.40f);
            x = PlatRun(x + Gap(0.30f), 0, y);
        }
        return Run(x + Gap(0.30f), x + Gap(0.30f) + 8f, 0f);
    }

    // 3. A two-storey climb. The first wall is too tall to jump: push the
    //    crate against it and climb up. Up there the second wall is too tall
    //    again, and the stone block is the step - push it over and climb on.
    static float CrateClimb(float x)
    {
        float y = 0f;
        foreach (var box in new[] { "crate", "stone_block" })
        {
            float faceX = Run(x, x + 16f, y, leftCap: y == 0f);
            PutUnder(box, x + 4f, y);
            y += Wall(box);
            x = Right(Column(faceX, -3f, y)) - 0.05f;      // column reaches the ground
        }
        float top = Run(x, x + 8f, y, leftCap: false);
        return Run(top + Gap(0.2f), top + Gap(0.2f) + 8f, 0f);      // drop back down
    }

    // Too tall to jump from the floor, easy from on top of `box`.
    static float Wall(string box) => (Rise(1.04f) + Height(Load(box)) + Rise(0.82f)) / 2f;

    // 4. A long chain of platforms that give way under you, up and down in
    //    height, with one solid rest stop in the middle.
    static float CrumblingWay(float x)
    {
        string[] chain = { "falling_intact", "falling_cracked", "falling_half_left", "falling_half_right" };
        float[] heights = { 0.12f, 0.30f, 0.18f, 0.40f };
        for (int i = 0; i < 4; i++) x = Right(Put(chain[i], x + Gap(0.30f), Rise(heights[i])));
        x = PlatRun(x + Gap(0.30f), 0, Rise(0.25f));                       // rest stop
        for (int i = 3; i >= 0; i--) x = Right(Put(chain[i], x + Gap(0.30f), Rise(heights[3 - i])));
        return Run(x + Gap(0.30f), x + Gap(0.30f) + 8f, 0f);
    }

    // 5. A long corridor with three spiked logs out of step with each other,
    //    so each one has to be timed on its own.
    static float BladeCorridor(float x)
    {
        float end = Run(x, x + 44f, 0f);
        Swing("spiked_log", x + 9f, 0f, 55f);
        Swing("spiked_log", x + 21f, 0f, -35f);
        Swing("spiked_log", x + 33f, 0f, 70f);
        return end;
    }

    // 6. The stone on the seesaw holds the far end up as a step to a ledge
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
        float top = Run(Right(face) - 0.05f, Right(face) + 10f, ledgeY, leftCap: false);
        float down = Run(top + Gap(0.2f), top + Gap(0.2f) + 6f, 0f);
        return Mathf.Max(down, floorEnd);
    }

    // 7. A low ceiling so there's no jumping. Shove the stone block off the
    //    end, cross the gap, then do it again with a crate in a second tunnel.
    static float LowTunnel(float x)
    {
        foreach (var block in new[] { "stone_block", "crate" })
        {
            float end = Run(x, x + 22f, 0f);
            for (float cx = x + 3f; cx < end - 3f;)
                cx = Right(PutUnder("slab", cx, 2.6f)) - 0.05f;
            PutUnder(block, x + 5f, 0f);
            x = Run(end + Gap(0.40f), end + Gap(0.40f) + 8f, 0f);
        }
        return x;
    }

    // 8. Stone columns of uneven height over a deep drop.
    static float PillarHop(float x)
    {
        float[] heights = { 0.6f, 2.0f, 1.0f, 2.6f, 1.4f, 3.0f, 2.0f, 0.8f, 1.8f };
        foreach (var h in heights) x = Right(Column(x + Gap(0.38f), -12f, h));
        return Run(x + Gap(0.35f), x + Gap(0.35f) + 8f, 0f);
    }

    // 9. Platforms over a pit with two spiked balls sweeping the gaps, then a
    //    loose plank bridge with a crate sitting on it.
    static float IronGauntlet(float x)
    {
        x = PlatRun(x + Gap(0.35f), 1, Rise(0.15f));
        float gap1 = x;
        x = PlatRun(x + Gap(0.42f), 2, Rise(0.30f));
        Swing("spiked_ball", gap1 + Gap(0.42f) / 2f, Rise(0.15f), 50f);
        float gap2 = x;
        x = PlatRun(x + Gap(0.42f), 1, Rise(0.20f));
        Swing("spiked_ball", gap2 + Gap(0.42f) / 2f, Rise(0.20f), -45f);
        x = Run(x + Gap(0.30f), x + Gap(0.30f) + 8f, 0f);
        var plank = PutUnder("bridge_plank", x - 0.6f, 0f);
        PutUnder("crate_small", Left(plank) + 0.9f, Top(plank, collider: true) + 0.02f);
        float right = Right(plank) - 0.6f;
        return Run(right, right + 8f, 0f, leftCap: false);   // no pillar where the plank rests
    }

    // 10. Drop into the well, climb out on the barrel, and run to the wall
    //     at the end of the course.
    static float TheWellAndFinish(float x)
    {
        float barrelH = Height(Load("barrel"));
        float depth = (Rise(1.04f) + barrelH + Rise(0.82f)) / 2f;
        float lip = Run(x, x + 8f, 0f);
        float floorEnd = Run(lip, lip + 10f, -depth);
        PutUnder("barrel", lip + 3f, -depth);
        var face = Column(floorEnd, -depth - 3f, 0f);
        float end = Run(Right(face) - 0.05f, Right(face) + 16f, 0f, leftCap: false);
        Column(end - 1f, 0f, Rise(1.6f));
        return end;
    }

    // ---------------------------------------------------------------- placing

    // ground_left is the run's end cap: a walkway with a tall broken pillar
    // on its left. Its walkway - not the pillar - goes at `y`. Right after a
    // column there's already an edge, so leftCap: false starts with plain
    // walkway instead.
    static float Run(float x0, float x1, float y, bool leftCap = true)
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
        if (mids == 0) return Right(Put("plat_single", x, y));
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
        float lowest = floorY + 0.7f;                         // clears the floor, not the player
        hazard.transform.position += new Vector3(cx - b.center.x, lowest - b.min.y, 0f);
        var hinge = hazard.GetComponent<HingeJoint2D>();
        var pin = hazard.transform.TransformPoint(hinge.anchor);
        PutCentre("ceiling_bracket", pin.x, pin.y - 0.15f);
        hazard.transform.RotateAround(pin, Vector3.forward, startAngle);
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
