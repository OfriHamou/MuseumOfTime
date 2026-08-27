using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Brings FrozenCity and ClockCore up to the per-scene requirement coverage
/// that Part 3 of the implementation plan assigns them.
///
///   Unity.exe -batchmode -quit -projectPath . ^
///             -executeMethod SceneGuidanceBuilder.BuildFromCommandLine
///
/// A scene-by-scene audit found these actually missing:
///
///   T2  "in-game tutorial, dynamic text, in 3D" - Part 3 places this in all
///       three gameplay scenes ("era-switch prompts" in FrozenCity, "boss
///       phase callouts" in ClockCore). MuseumNight had nine world-space
///       TextMeshPro plaques; the other two scenes had ZERO. A player who
///       reached scene 2 was never told that Q/R switch era, which is the
///       game's signature mechanic and the thing the whole scene is built on.
///
///   T3  Part 3 lists "era zones, tower entry" for FrozenCity and "arena
///       phase triggers" for ClockCore. FrozenCity had only a Time Anchor
///       trigger and its exit; ClockCore had only anchors.
///
///   T5  Part 3 lists "inverted swinging exhibits" for ClockCore, which had
///       no HingeJoint at all.
///
/// T3 and T5 were already satisfied globally by MuseumNight, so these are
/// coverage rather than compliance fixes - but the per-scene table is also
/// exactly what the GDD has to state under S8, so it should be true.
///
/// Idempotent.
/// </summary>
public static class SceneGuidanceBuilder
{
    [MenuItem("Museum of Time/Build Scene Guidance (scenes 2-3)")]
    public static void BuildMenu() { Build(); }

    public static void BuildFromCommandLine() { Build(); }

    private static void Build()
    {
        BuildMuseumNight();
        BuildFrozenCity();
        BuildClockCore();

        Debug.Log("=== SCENE GUIDANCE COMPLETE ===");
    }

    // ------------------------------------------------------------------

    /// <summary>
    /// Signposts the one route out of MuseumNight.
    ///
    /// The objective says to reach the curator's office up on the mezzanine.
    /// The only way up is a single unlit ramp in the far WEST corner of a dark
    /// thirty-metre hall, while the office itself is at the east end - so a
    /// player follows the objective, walks east, finds no way up, and is
    /// stuck. Playing it, that is exactly what happened: several minutes of
    /// wandering a room that gave no indication of where the stairs were.
    ///
    /// A lit sign at the foot of the ramp and another at the top turn "find
    /// the stairs" back into a thing you can actually do.
    /// </summary>
    private static void BuildMuseumNight()
    {
        const string path = "Assets/Scenes/MuseumNight.unity";
        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(path) == null) { return; }

        Scene scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);

        GameObject signs = Root("Wayfinding");

        Signpost(signs, "Sign_RampFoot",
                 new Vector3(-12.2f, 2.4f, -5.0f),
                 "<color=#FFD98A>RAMP TO THE MEZZANINE</color>\nThe Time Lens is up here");

        Signpost(signs, "Sign_RampTop",
                 new Vector3(-12.2f, 7.2f, 1.5f),
                 "<color=#FFD98A>CURATOR'S OFFICE</color>\nFollow the mezzanine EAST");

        Signpost(signs, "Sign_OfficeDoor",
                 new Vector3(7.4f, 7.2f, 6.0f),
                 "<color=#FFD98A>CURATOR'S OFFICE</color>\nThe Time Lens is inside");

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);

        Debug.Log("WAYFINDING OK: MuseumNight signposted (3 signs)");
    }

    /// <summary>
    /// A world-space sign with its own light, so it reads in a dark room.
    /// </summary>
    private static void Signpost(GameObject parent, string name, Vector3 where, string message)
    {
        GameObject go = Child(parent, name);
        go.transform.position = where;

        Text(go, message);

        // A signpost keeps the words it was given. WorldObjectiveText also
        // billboards, but it REWRITES the text with the current objective, so
        // using it here turned every sign into a screen-wide copy of the
        // objective line instead of the direction it was placed to give.
        Ensure<WorldSignpost>(go);

        // Signs sit close to eye level and are read at a couple of metres, so
        // the plaque-sized range is right. The 2.2 the shared Text() helper
        // uses is for text meant to be read across a room.
        var tmp = go.GetComponent<TMPro.TextMeshPro>();
        if (tmp != null)
        {
            tmp.fontSizeMin = 0.28f;
            tmp.fontSizeMax = 0.62f;

            var rt = go.GetComponent<RectTransform>();
            if (rt != null) { rt.sizeDelta = new Vector2(4.4f, 1.5f); }
        }

        Transform existingGlow = go.transform.Find("SignGlow");
        if (existingGlow != null) { Object.DestroyImmediate(existingGlow.gameObject); }

        var glow = new GameObject("SignGlow");
        glow.transform.SetParent(go.transform, false);
        glow.transform.localPosition = Vector3.zero;

        Light light = glow.AddComponent<Light>();
        light.type = LightType.Point;
        light.color = new Color(1f, 0.85f, 0.55f);
        light.intensity = 9f;
        light.range = 12f;
        light.shadows = LightShadows.None;

        go.SetActive(true);
    }

    // ------------------------------------------------------------------

    private static void BuildFrozenCity()
    {
        const string path = "Assets/Scenes/FrozenCity.unity";
        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(path) == null) { return; }

        Scene scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);

        GameObject plaques = Root("TutorialPlaques");
        GameObject triggers = Root("Triggers");

        Vector3 spawn = PlayerSpawn(new Vector3(0f, 0f, -18f));

        // The objective line, permanently visible at the street entrance.
        GameObject objective = Child(plaques, "Plaque_Objective");
        objective.transform.position = spawn + new Vector3(0f, 2.6f, 4f);
        Text(objective, "Objective: reach the clock tower and repair the gear");
        Ensure<WorldObjectiveText>(objective);
        objective.SetActive(true);

        Plaque(plaques, triggers, "Era",
               spawn + new Vector3(0f, 2.2f, 8f), new Vector3(10f, 4f, 6f),
               "Press Q and R to travel between Past, Present and Future.\n" +
               "Each switch costs energy - {energy}% remaining.");

        Plaque(plaques, triggers, "Lens",
               spawn + new Vector3(6f, 2.2f, 16f), new Vector3(8f, 4f, 6f),
               "The Time Lens reveals hidden Time Anchors.\n" +
               "Walk through one to set your return point.");

        Plaque(plaques, triggers, "Gear",
               new Vector3(-6f, 2.2f, 10f), new Vector3(8f, 4f, 6f),
               "Find the gear in the Past. Fit it in the Present.\n" +
               "Check that it still turns in the Future.");

        Plaque(plaques, triggers, "Bell",
               new Vector3(0f, 2.6f, 24f), new Vector3(10f, 5f, 8f),
               "Left mouse throws the Chrono Orb. Ring the tower bell with it.");

        // T3: an era zone that unlocks era travel, and the tower entry room.
        MakeTrigger<EraZoneTrigger>(triggers, "Trigger_EraZone",
            spawn + new Vector3(0f, 1.5f, 8f), new Vector3(14f, 4f, 8f),
            t =>
            {
                var so = new SerializedObject(t);
                SerializedProperty p = so.FindProperty("unlocksEraTravel");
                if (p != null) { p.boolValue = true; }
                so.ApplyModifiedPropertiesWithoutUndo();
            });

        MakeTrigger<RoomEntryTrigger>(triggers, "Trigger_TowerEntry",
            new Vector3(0f, 1.5f, 26f), new Vector3(10f, 4f, 6f), null);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("GUIDANCE OK: FrozenCity (4 plaques + objective, era zone, tower entry)");
    }

    // ------------------------------------------------------------------

    private static void BuildClockCore()
    {
        const string path = "Assets/Scenes/ClockCore.unity";
        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(path) == null) { return; }

        Scene scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);

        GameObject plaques = Root("TutorialPlaques");
        GameObject triggers = Root("Triggers");

        Vector3 spawn = PlayerSpawn(new Vector3(0f, 0f, -15f));

        GameObject objective = Child(plaques, "Plaque_Objective");
        objective.transform.position = spawn + new Vector3(0f, 2.8f, 3f);
        Text(objective, "Objective: undo the Collector");
        Ensure<WorldObjectiveText>(objective);
        objective.SetActive(true);

        // The three boss phases, each explained where the player meets it.
        Plaque(plaques, triggers, "PhasePast",
               new Vector3(0f, 2.4f, -6f), new Vector3(14f, 5f, 8f),
               "PAST - the Collector is shielded.\n" +
               "Press Q to reach the Past and break the shield with the Orb.");

        Plaque(plaques, triggers, "PhasePresent",
               new Vector3(-7f, 2.4f, 2f), new Vector3(10f, 5f, 8f),
               "PRESENT - it calls a Warden.\n" +
               "Stay out of the cone of vision, or freeze it with the Orb.");

        Plaque(plaques, triggers, "PhaseFuture",
               new Vector3(7f, 2.4f, 2f), new Vector3(10f, 5f, 8f),
               "FUTURE - time itself erodes you. Health {health}.\n" +
               "Hold Ctrl to slow time; only then can a hit finish this.");

        // T3: the arena phase trigger.
        MakeTrigger<RoomEntryTrigger>(triggers, "Trigger_ArenaEntry",
            new Vector3(0f, 1.5f, -4f), new Vector3(18f, 4f, 6f), null);

        // T5: the inverted museum's swinging exhibits, hung from the ceiling
        // at y = 8 so they swing above the arena floor.
        BuildSwingingExhibit("SwingingExhibit_A", new Vector3(-9f, 8f, -2f));
        BuildSwingingExhibit("SwingingExhibit_B", new Vector3(9f, 8f, -2f));

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("GUIDANCE OK: ClockCore (3 phase callouts + objective, arena trigger, 2 hinges)");
    }

    /// <summary>
    /// A pendulum on a real HingeJoint: an anchor block fixed to the ceiling
    /// and a weighted bob hanging from it, free to swing about Z.
    /// </summary>
    private static void BuildSwingingExhibit(string name, Vector3 ceilingPoint)
    {
        GameObject root = Root("Hinges");
        GameObject exhibit = Child(root, name);
        exhibit.transform.position = ceilingPoint;

        Material brass = AssetDatabase.LoadAssetAtPath<Material>(
            "Assets/Materials/Museum/MuseumBrass.mat");

        // The bob, 2.5 m below the ceiling mount.
        GameObject bob = Child(exhibit, "Bob");
        MeshFilter filter = Ensure<MeshFilter>(bob);
        MeshRenderer renderer = Ensure<MeshRenderer>(bob);

        GameObject template = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        filter.sharedMesh = template.GetComponent<MeshFilter>().sharedMesh;
        Object.DestroyImmediate(template);

        renderer.sharedMaterial = brass;
        bob.transform.position = ceilingPoint + new Vector3(0f, -2.5f, 0f);
        bob.transform.localScale = Vector3.one * 0.9f;

        SphereCollider collider = Ensure<SphereCollider>(bob);
        collider.radius = 0.5f;

        Rigidbody body = Ensure<Rigidbody>(bob);
        body.mass = 8f;
        body.isKinematic = false;
        body.linearDamping = 0.05f;

        HingeJoint hinge = Ensure<HingeJoint>(bob);
        hinge.connectedBody = null;                        // fixed to the world
        hinge.anchor = new Vector3(0f, 2.78f, 0f);         // up at the ceiling
        hinge.axis = new Vector3(0f, 0f, 1f);
        hinge.useSpring = false;
        hinge.useMotor = false;

        // Start it off-centre so it is already swinging when the player
        // arrives - a motionless pendulum reads as a static prop.
        bob.transform.position = ceilingPoint + new Vector3(1.1f, -2.3f, 0f);
    }

    // ------------------------------------------------------------------
    // Helpers
    // ------------------------------------------------------------------

    private static Vector3 PlayerSpawn(Vector3 fallback)
    {
        GameObject marker = GameObject.Find("PlayerSpawn");
        if (marker != null) { return marker.transform.position; }

        GameObject player = GameObject.FindWithTag("Player");
        return player != null ? player.transform.position : fallback;
    }

    private static void Plaque(GameObject plaqueRoot, GameObject triggerRoot,
                               string key, Vector3 position, Vector3 triggerSize,
                               string message)
    {
        GameObject plaque = Child(plaqueRoot, "Plaque_" + key);
        plaque.transform.position = position;
        Text(plaque, message);

        WorldTutorialText tutorial = Ensure<WorldTutorialText>(plaque);
        var so = new SerializedObject(tutorial);
        so.FindProperty("fadeDistance").floatValue = 9f;
        so.FindProperty("fadeSpeed").floatValue = 4f;
        so.ApplyModifiedPropertiesWithoutUndo();

        // Revealed by its trigger, exactly as MuseumNight's plaques are.
        plaque.SetActive(false);

        MakeTrigger<TutorialTrigger>(triggerRoot, "Trigger_Tutorial" + key,
            position, triggerSize,
            t =>
            {
                var tso = new SerializedObject(t);
                tso.FindProperty("textObject").objectReferenceValue = plaque;

                SerializedProperty msg = tso.FindProperty("message");
                if (msg != null) { msg.stringValue = message; }

                tso.ApplyModifiedPropertiesWithoutUndo();
            });
    }

    private static void Text(GameObject go, string message)
    {
        TextMeshPro tmp = Ensure<TextMeshPro>(go);
        tmp.text = message;
        tmp.enableAutoSizing = true;
        tmp.fontSizeMin = 0.6f;
        tmp.fontSizeMax = 2.2f;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = new Color(0.95f, 0.93f, 0.82f);

        var rt = go.GetComponent<RectTransform>();
        if (rt != null) { rt.sizeDelta = new Vector2(7f, 2.4f); }
    }

    private static void MakeTrigger<T>(GameObject parent, string name,
                                       Vector3 position, Vector3 size,
                                       System.Action<T> configure)
        where T : Component
    {
        GameObject go = Child(parent, name);
        go.transform.position = position;

        BoxCollider box = Ensure<BoxCollider>(go);
        box.isTrigger = true;
        box.size = size;

        T component = Ensure<T>(go);
        configure?.Invoke(component);

        EditorUtility.SetDirty(go);
    }

    private static GameObject Root(string name)
    {
        GameObject go = GameObject.Find(name);
        return go != null ? go : new GameObject(name);
    }

    private static GameObject Child(GameObject parent, string name)
    {
        Transform t = parent.transform.Find(name);
        if (t != null) { return t.gameObject; }

        var go = new GameObject(name);
        go.transform.SetParent(parent.transform, true);
        return go;
    }

    private static T Ensure<T>(GameObject go) where T : Component
    {
        T c = go.GetComponent<T>();
        return c != null ? c : go.AddComponent<T>();
    }
}
