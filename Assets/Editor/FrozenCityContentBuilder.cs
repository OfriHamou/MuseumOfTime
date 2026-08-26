using System.Collections.Generic;
using Unity.AI.Navigation;
using Unity.Cinemachine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

/// <summary>
/// Phase 6, Step 6.2: places the Phase 3/4 systems that already exist and
/// work in MuseumNight into FrozenCity, the scene the requirement-placement
/// table (Implementation_Plan.md Part 3) actually assigns them to - T21's
/// hidden anchors are explicitly forbidden in MuseumNight and only valid
/// "from the second scene onward".
///
///   Unity.exe -batchmode -quit -projectPath . ^
///             -executeMethod FrozenCityContentBuilder.BuildFromCommandLine
///
/// Includes the GDD's three-era gear puzzle ("the tower bell never rang, so
/// the moment cannot continue"): find the gear in the Past, install it in
/// the Present, verify it in the Future. Completing it is what reveals the
/// Chrono Hourglass - the puzzle actually gates the scene's reward rather
/// than sitting next to it. The bell itself is a real HingeJoint, ringable
/// by the existing generic orb-wakes-any-HingeJoint collision logic from
/// Step 3.3, independent of the puzzle.
/// </summary>
public static class FrozenCityContentBuilder
{
    private const string ScenePath = "Assets/Scenes/FrozenCity.unity";

    [MenuItem("Museum of Time/Build FrozenCity Content (Phase 6)")]
    public static void BuildMenu() { Build(); }

    public static void BuildFromCommandLine() { Build(); }

    private static void Build()
    {
        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

        Terrain terrain = Terrain.activeTerrain;
        if (terrain == null)
        {
            Debug.LogError("FROZENCITY FAILED: no Terrain in the scene - run Build FrozenCity Terrain first.");
            return;
        }

        GameObject player = BuildPlayerAndCameras(terrain);
        if (player == null)
        {
            return;
        }

        BuildManagers();
        int anchorCount = BuildAnchors(terrain);
        BuildBell();
        BuildFrozenStatue(terrain);
        GameObject hourglass = BuildHourglassPickup(terrain);
        BuildGearPuzzle(terrain, hourglass);
        BuildExit(terrain);
        int agentsPlaced = BuildNavigationAndAgents(terrain);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);

        Debug.Log("FROZENCITY OK: player, era travel unlocked, " + anchorCount +
                   " Time Anchors, hinge bell, frozen statue, gear puzzle " +
                   "gating the Chrono Hourglass, " + agentsPlaced +
                   " AI agents on their own navmeshes, exit to ClockCore.");
    }

    // -----------------------------------------------------------------
    // Player and cameras
    // -----------------------------------------------------------------

    private static GameObject BuildPlayerAndCameras(Terrain terrain)
    {
        GameObject player = GameObject.Find("Player");

        if (player == null)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabBuilder.PrefabPath);

            if (prefab == null)
            {
                Debug.LogError("FROZENCITY FAILED: Player prefab missing - run Build Player Prefab first.");
                return null;
            }

            player = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        }

        GameObject spawnMarker = GameObject.Find("PlayerSpawn");
        Vector3 spawnPos = spawnMarker != null
            ? spawnMarker.transform.position
            : new Vector3(0f, 0f, -20f);

        spawnPos.y = SampleHeight(terrain, spawnPos) + 0.1f;
        player.transform.SetPositionAndRotation(spawnPos, Quaternion.identity);

        GameObject mainCamera = FindOrCreate("MainCamera", null);
        mainCamera.tag = "MainCamera";
        Ensure<Camera>(mainCamera).nearClipPlane = 0.05f;
        mainCamera.GetComponent<Camera>().farClipPlane = 500f;
        Ensure<AudioListener>(mainCamera);
        Ensure<CinemachineBrain>(mainCamera);

        Transform pivot = player.transform.Find("CameraPivot");

        GameObject thirdPerson = FindOrCreate("ThirdPersonCamera", null);
        CinemachineCamera tpCam = Ensure<CinemachineCamera>(thirdPerson);
        tpCam.Follow = pivot;
        tpCam.LookAt = pivot;
        tpCam.Lens.FieldOfView = 60f;

        CinemachineThirdPersonFollow follow = Ensure<CinemachineThirdPersonFollow>(thirdPerson);
        follow.ShoulderOffset = new Vector3(0.5f, 0.2f, 0f);
        follow.VerticalArmLength = 0.2f;
        follow.CameraDistance = 4.5f;

        var rigSo = new SerializedObject(player.GetComponent<PlayerCameraRig>());
        rigSo.FindProperty("thirdPersonCamera").objectReferenceValue = tpCam;
        rigSo.ApplyModifiedPropertiesWithoutUndo();

        return player;
    }

    // -----------------------------------------------------------------
    // Managers: era travel already unlocked (the Lens was found in
    // MuseumNight), and respawn-to-anchor.
    // -----------------------------------------------------------------

    private static void BuildManagers()
    {
        GameObject eraObject = FindOrCreate("EraManager", null);
        EraManager era = Ensure<EraManager>(eraObject);

        var eraSo = new SerializedObject(era);
        eraSo.FindProperty("startingEra").enumValueIndex = (int)TimeEra.Present;
        // Unlike MuseumNight, era travel starts UNLOCKED here: by the time
        // Noa reaches FrozenCity she has already found the Time Lens.
        eraSo.FindProperty("eraTravelUnlocked").boolValue = true;
        eraSo.ApplyModifiedPropertiesWithoutUndo();

        GameObject respawnObject = FindOrCreate("RespawnService", null);
        Ensure<RespawnService>(respawnObject);
    }

    // -----------------------------------------------------------------
    // Time Anchors - T21, and only valid from this scene onward.
    // -----------------------------------------------------------------

    private static int BuildAnchors(Terrain terrain)
    {
        GameObject parent = FindOrCreate("Checkpoints", null);
        ClearChildren(parent);

        MakeAnchor(parent, "TimeAnchor_Overlook", terrain, new Vector3(20f, 0f, -10f));
        MakeAnchor(parent, "TimeAnchor_TowerBase", terrain, new Vector3(-12f, 0f, 22f));

        return parent.transform.childCount;
    }

    private static void MakeAnchor(GameObject parent, string name, Terrain terrain, Vector3 flatPosition)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent.transform, false);

        Vector3 position = flatPosition;
        position.y = SampleHeight(terrain, position) + 0.05f;
        go.transform.position = position;

        BoxCollider box = go.AddComponent<BoxCollider>();
        box.isTrigger = true;
        box.size = new Vector3(1.5f, 2f, 1.5f);

        // A small marker, visible only through the Time Lens - TimeAnchor
        // toggles this itself based on GameState.hasTimeLens.
        GameObject visual = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        visual.name = "LensVisual";
        visual.transform.SetParent(go.transform, false);
        visual.transform.localScale = Vector3.one * 0.4f;
        Object.DestroyImmediate(visual.GetComponent<Collider>());

        Material glass = AssetDatabase.LoadAssetAtPath<Material>(
            "Assets/Materials/Museum/MuseumBrass.mat");
        if (glass != null)
        {
            visual.GetComponent<MeshRenderer>().sharedMaterial = glass;
        }

        TimeAnchor anchor = go.AddComponent<TimeAnchor>();
        var anchorSo = new SerializedObject(anchor);
        anchorSo.FindProperty("lensVisual").objectReferenceValue = visual;
        anchorSo.ApplyModifiedPropertiesWithoutUndo();

        // TimeAnchorTrigger is what T3's trigger tally counts (TriggerLog);
        // TimeAnchor's own OnTriggerEnter is what actually arms it. Both
        // belong on the same object - see Assets/Scripts/World/TimeAnchorTrigger.cs.
        go.AddComponent<TimeAnchorTrigger>();
    }

    // -----------------------------------------------------------------
    // The hinge bell - T5 in this scene, and what T15's orb-ring beat needs.
    // Turns TerrainBuilder's placeholder Belfry into a real swinging bell.
    // -----------------------------------------------------------------

    private static void BuildBell()
    {
        GameObject belfry = GameObject.Find("Belfry");
        if (belfry == null)
        {
            Debug.LogWarning("No 'Belfry' object - run Build FrozenCity Terrain first. Skipping the bell.");
            return;
        }

        GameObject existingBell = GameObject.Find("TowerBell");
        if (existingBell != null)
        {
            Object.DestroyImmediate(existingBell);
        }

        GameObject bell = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        bell.name = "TowerBell";
        bell.transform.SetParent(belfry.transform, false);
        bell.transform.localPosition = new Vector3(0f, -1f, 0f);
        bell.transform.localScale = new Vector3(1.2f, 0.9f, 1.2f);

        Material brass = AssetDatabase.LoadAssetAtPath<Material>(
            "Assets/Materials/Museum/MuseumBrass.mat");
        if (brass != null)
        {
            bell.GetComponent<MeshRenderer>().sharedMaterial = brass;
        }

        Rigidbody body = bell.AddComponent<Rigidbody>();
        body.mass = 25f;
        body.angularDamping = 0.15f;

        HingeJoint hinge = bell.AddComponent<HingeJoint>();
        hinge.anchor = new Vector3(0f, 0.5f, 0f);
        hinge.axis = new Vector3(0f, 0f, 1f);
        hinge.useLimits = true;
        hinge.limits = new JointLimits { min = -25f, max = 25f };
        hinge.useSpring = true;
        hinge.spring = new JointSpring { spring = 20f, damper = 1f, targetPosition = 0f };
    }

    // -----------------------------------------------------------------
    // The second Voronoi fracture (T10 #2) and the second acquired item
    // (T9 #2), placed as real pickups/props rather than left as prefabs
    // nobody instantiates.
    // -----------------------------------------------------------------

    private static void BuildFrozenStatue(Terrain terrain)
    {
        GameObject parent = FindOrCreate("Destructibles", null);
        ClearChildren(parent);

        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
            "Assets/Prefabs/World/FrozenStatue.prefab");

        if (prefab == null)
        {
            Debug.LogWarning("FrozenStatue.prefab not found - run Build Fracture and LOD Prefabs first.");
            return;
        }

        GameObject statue = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent.transform);
        Vector3 position = new Vector3(8f, 0f, 5f);
        position.y = SampleHeight(terrain, position);
        statue.transform.position = position;
    }

    private static GameObject BuildHourglassPickup(Terrain terrain)
    {
        GameObject parent = FindOrCreate("Collectibles", null);
        ClearChildren(parent);

        GameObject hourglass = GameObject.CreatePrimitive(PrimitiveType.Cube);
        hourglass.name = "ChronoHourglass";
        hourglass.transform.SetParent(parent.transform, false);
        hourglass.transform.localScale = new Vector3(0.3f, 0.5f, 0.3f);

        Vector3 position = new Vector3(0f, 0f, 30f);
        position.y = SampleHeight(terrain, position) + 0.3f;
        hourglass.transform.position = position;

        Material brass = AssetDatabase.LoadAssetAtPath<Material>(
            "Assets/Materials/Museum/MuseumBrass.mat");
        if (brass != null)
        {
            hourglass.GetComponent<MeshRenderer>().sharedMaterial = brass;
        }

        ItemPickup pickup = hourglass.AddComponent<ItemPickup>();
        var so = new SerializedObject(pickup);
        so.FindProperty("item").enumValueIndex = (int)ItemPickup.Kind.ChronoHourglass;
        so.ApplyModifiedPropertiesWithoutUndo();

        return hourglass;
    }

    // -----------------------------------------------------------------
    // The three-era gear puzzle: "the tower bell never rang, so the moment
    // cannot continue." Find the gear in the Past, install it in the
    // Present, verify it in the Future - completing it is what reveals the
    // Chrono Hourglass built above, so the puzzle actually gates the reward
    // rather than sitting next to it decoratively.
    // -----------------------------------------------------------------

    private static void BuildGearPuzzle(Terrain terrain, GameObject rewardObject)
    {
        GameObject belfry = GameObject.Find("Belfry");
        Vector3 towerBase = belfry != null ? belfry.transform.position : new Vector3(0f, 0f, 35f);

        GameObject puzzleRoot = FindOrCreate("GearPuzzle", null);
        GearPuzzle puzzle = Ensure<GearPuzzle>(puzzleRoot);

        GameObject gear = FindOrCreate("Gear", puzzleRoot);
        Vector3 gearPos = towerBase + new Vector3(4f, 0f, -3f);
        gearPos.y = SampleHeight(terrain, gearPos) + 0.2f;
        SetupGearVisual(gear, gearPos, new Vector3(0.4f, 0.15f, 0.4f));
        Ensure<GearPickup>(gear);

        GameObject socket = FindOrCreate("GearSocket", puzzleRoot);
        Vector3 socketPos = towerBase + new Vector3(0f, 0f, -2.5f);
        socketPos.y = SampleHeight(terrain, socketPos) + 0.5f;
        SetupGearVisual(socket, socketPos, new Vector3(0.6f, 1f, 0.3f));
        Ensure<GearSocket>(socket);

        var puzzleSo = new SerializedObject(puzzle);
        puzzleSo.FindProperty("rewardObject").objectReferenceValue = rewardObject;
        puzzleSo.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void SetupGearVisual(GameObject go, Vector3 position, Vector3 scale)
    {
        MeshFilter filter = go.GetComponent<MeshFilter>();
        if (filter == null)
        {
            filter = go.AddComponent<MeshFilter>();
            filter.sharedMesh = GetPrimitiveMesh(PrimitiveType.Cylinder);
            go.AddComponent<MeshRenderer>();
            go.AddComponent<BoxCollider>();
        }

        go.transform.position = position;
        go.transform.localScale = scale;

        Material brass = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Museum/MuseumBrass.mat");
        if (brass != null)
        {
            go.GetComponent<MeshRenderer>().sharedMaterial = brass;
        }
    }

    private static Mesh GetPrimitiveMesh(PrimitiveType type)
    {
        GameObject temp = GameObject.CreatePrimitive(type);
        Mesh mesh = temp.GetComponent<MeshFilter>().sharedMesh;
        Object.DestroyImmediate(temp);
        return mesh;
    }

    // -----------------------------------------------------------------
    // The way out, gated on the reward this scene grants - S9's chain.
    // -----------------------------------------------------------------

    private static void BuildExit(Terrain terrain)
    {
        GameObject parent = FindOrCreate("Triggers", null);

        GameObject existing = parent.transform.Find("Exit_ToClockCore")?.gameObject;
        if (existing != null)
        {
            Object.DestroyImmediate(existing);
        }

        var exit = new GameObject("Exit_ToClockCore");
        exit.transform.SetParent(parent.transform, false);

        Vector3 position = new Vector3(0f, 0f, 48f);
        position.y = SampleHeight(terrain, position) + 1.5f;
        exit.transform.position = position;

        BoxCollider box = exit.AddComponent<BoxCollider>();
        box.isTrigger = true;
        box.size = new Vector3(4f, 3f, 2f);

        SceneLoader loader = exit.AddComponent<SceneLoader>();
        SceneExitTrigger trigger = exit.AddComponent<SceneExitTrigger>();

        var so = new SerializedObject(trigger);
        so.FindProperty("requiredItem").enumValueIndex = (int)SceneExitTrigger.RequiredItem.ChronoHourglass;
        so.FindProperty("targetScene").stringValue = "ClockCore";
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    // -----------------------------------------------------------------
    // Navigation: reuses the WardenAgent/ShadowAgent types already created
    // (as project-wide settings) by MuseumNight's NavigationBuilder - agent
    // TYPES are shared across the whole project; only the NavMeshSurface
    // BAKE is per-scene. A short wall-and-ledge pair, the same trick
    // NavigationBuilder used, makes the two bakes disagree about one route.
    // -----------------------------------------------------------------

    private static int BuildNavigationAndAgents(Terrain terrain)
    {
        int wardenId = ReadAgentTypeId("WardenAgent");
        int shadowId = ReadAgentTypeId("ShadowAgent");

        if (wardenId == int.MinValue || shadowId == int.MinValue || wardenId == shadowId)
        {
            Debug.LogError("FROZENCITY FAILED: could not resolve WardenAgent/ShadowAgent " +
                            "ids from ProjectSettings/NavMeshAreas.asset - run " +
                            "Museum of Time > Build Navigation (two agent types) once first. " +
                            "Skipping AI placement.");
            return 0;
        }

        BuildObstacleCourse(terrain);

        GameObject navRoot = FindOrCreate("Navigation", null);

        NavMeshSurface wardenSurface = MakeSurface(navRoot, "NavMesh_Warden", wardenId);
        NavMeshSurface shadowSurface = MakeSurface(navRoot, "NavMesh_Shadow", shadowId);

        wardenSurface.BuildNavMesh();
        shadowSurface.BuildNavMesh();

        SpawnAgents(terrain, wardenId, shadowId);

        return 2;
    }

    /// <summary>
    /// Reads an agent type's id straight out of ProjectSettings/NavMeshAreas.asset.
    ///
    /// Both NavigationBuilder.EnsureAgentType (a SerializedObject read/write
    /// against Unsupported.GetSerializedAssetInterfaceSingleton) and
    /// UnityEngine.AI.NavMesh's own GetSettingsCount()/GetSettingsNameFromID
    /// proved unreliable when called from this class in a fresh -batchmode
    /// process, even moments after NavigationBuilder.BuildFromCommandLine()
    /// had just run successfully in the same process and confirmed the file
    /// on disk was correct - a second, separate call still failed to find
    /// the existing entries and one attempt produced a collided id for both
    /// agent types. Reading the settings asset's plain YAML directly sidesteps
    /// whatever Editor-side caching caused that, at the cost of assuming the
    /// file's own m_SettingNames/m_Settings arrays stay in the same positional
    /// order Unity itself writes them in.
    /// </summary>
    private static int ReadAgentTypeId(string agentName)
    {
        const string path = "ProjectSettings/NavMeshAreas.asset";

        if (!System.IO.File.Exists(path))
        {
            return int.MinValue;
        }

        string[] lines = System.IO.File.ReadAllLines(path);

        int namesStart = System.Array.FindIndex(lines, l => l.Trim() == "m_SettingNames:");
        if (namesStart < 0)
        {
            return int.MinValue;
        }

        int nameIndex = -1;
        int count = 0;

        for (int i = namesStart + 1; i < lines.Length; i++)
        {
            string trimmed = lines[i].TrimStart();
            if (!trimmed.StartsWith("- "))
            {
                break;
            }

            if (trimmed.Substring(2).Trim() == agentName)
            {
                nameIndex = count;
                break;
            }

            count++;
        }

        if (nameIndex < 0)
        {
            return int.MinValue;
        }

        int settingsStart = System.Array.FindIndex(lines, l => l.Trim() == "m_Settings:");
        if (settingsStart < 0)
        {
            return int.MinValue;
        }

        int entryIndex = -1;

        for (int i = settingsStart + 1; i < lines.Length; i++)
        {
            string trimmed = lines[i].Trim();

            if (trimmed == "m_SettingNames:")
            {
                break;
            }

            if (!trimmed.StartsWith("agentTypeID:"))
            {
                continue;
            }

            entryIndex++;

            if (entryIndex == nameIndex)
            {
                string value = trimmed.Substring("agentTypeID:".Length).Trim();
                return int.TryParse(value, out int id) ? id : int.MinValue;
            }
        }

        return int.MinValue;
    }

    private static void BuildObstacleCourse(Terrain terrain)
    {
        GameObject existing = GameObject.Find("ShadowShortcut");
        if (existing != null)
        {
            Object.DestroyImmediate(existing);
        }

        var root = new GameObject("ShadowShortcut");

        Vector3 basePos = new Vector3(10f, 0f, -18f);
        float ground = SampleHeight(terrain, basePos);

        Material plaster = AssetDatabase.LoadAssetAtPath<Material>(
            "Assets/Materials/Museum/MuseumPlaster.mat");

        // A wall with a 0.8m slot: a 0.5m-radius Warden needs 1.0m of
        // clearance to fit through, a 0.3m-radius Shadow needs 0.6m.
        Block(root, "SlotWallLeft", basePos + new Vector3(0f, 1.5f, -1.4f),
              new Vector3(0.4f, 3f, 2.0f), plaster);
        Block(root, "SlotWallRight", basePos + new Vector3(0f, 1.5f, 1.4f),
              new Vector3(0.4f, 3f, 2.0f), plaster);

        // A 0.7m ledge on the far side: inside the Shadow's 0.9m climb,
        // outside the Warden's 0.4m.
        Block(root, "ShadowLedge", basePos + new Vector3(1.5f, ground + 0.35f, 0f),
              new Vector3(2.5f, 0.7f, 3f), plaster);
    }

    private static void Block(GameObject parent, string name, Vector3 position, Vector3 size, Material material)
    {
        GameObject box = GameObject.CreatePrimitive(PrimitiveType.Cube);
        box.name = name;
        box.transform.SetParent(parent.transform, false);
        box.transform.position = position;
        box.transform.localScale = size;

        if (material != null)
        {
            box.GetComponent<MeshRenderer>().sharedMaterial = material;
        }

        GameObjectUtility.SetStaticEditorFlags(box, StaticEditorFlags.NavigationStatic);
    }

    private static NavMeshSurface MakeSurface(GameObject parent, string name, int agentTypeId)
    {
        GameObject go = FindOrCreate(name, parent);

        NavMeshSurface surface = go.GetComponent<NavMeshSurface>();
        if (surface == null)
        {
            surface = go.AddComponent<NavMeshSurface>();
        }

        surface.agentTypeID = agentTypeId;
        surface.collectObjects = CollectObjects.All;
        surface.useGeometry = NavMeshCollectGeometry.RenderMeshes;

        return surface;
    }

    private static void SpawnAgents(Terrain terrain, int wardenId, int shadowId)
    {
        GameObject enemies = FindOrCreate("Enemies", null);
        ClearChildren(enemies);

        Vector3 wardenStart = new Vector3(6f, 0f, -20f);
        wardenStart.y = SampleHeight(terrain, wardenStart) + 1.1f;

        GameObject warden = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        warden.name = "TimeWarden";
        warden.transform.SetParent(enemies.transform, false);
        warden.transform.position = wardenStart;
        Object.DestroyImmediate(warden.GetComponent<CapsuleCollider>());

        NavMeshAgent wardenAgent = warden.AddComponent<NavMeshAgent>();
        wardenAgent.agentTypeID = wardenId;
        wardenAgent.radius = 0.5f;
        wardenAgent.height = 2f;
        wardenAgent.speed = 2.2f;

        PatrolRoute route = warden.AddComponent<PatrolRoute>();
        var waypoints = new List<PatrolRoute.Waypoint>();
        foreach (Vector3 flat in new[]
                 {
                     new Vector3(6f, 0f, -20f), new Vector3(6f, 0f, -5f),
                     new Vector3(-6f, 0f, -5f), new Vector3(-6f, 0f, -20f),
                 })
        {
            Vector3 p = flat;
            p.y = SampleHeight(terrain, p);
            waypoints.Add(new PatrolRoute.Waypoint { position = p, waitSeconds = 2.5f });
        }
        route.SetWaypoints(waypoints);

        warden.AddComponent<WardenAI>();

        Animator wardenAnimator = warden.AddComponent<Animator>();
        wardenAnimator.applyRootMotion = false;

        var wardenController = AssetDatabase.LoadAssetAtPath<UnityEditor.Animations.AnimatorController>(
            "Assets/Animations/Enemies/WardenController.controller");

        if (wardenController != null)
        {
            wardenAnimator.runtimeAnimatorController = wardenController;
        }

        WardenAnimatorDriver driver = warden.AddComponent<WardenAnimatorDriver>();
        var driverSo = new SerializedObject(driver);
        driverSo.FindProperty("animator").objectReferenceValue = wardenAnimator;
        driverSo.ApplyModifiedPropertiesWithoutUndo();

        Vector3 shadowStart = new Vector3(11f, 0f, -18f);
        shadowStart.y = SampleHeight(terrain, shadowStart) + 0.7f;

        GameObject shadow = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        shadow.name = "ChronologicalShadow";
        shadow.transform.SetParent(enemies.transform, false);
        shadow.transform.position = shadowStart;
        shadow.transform.localScale = new Vector3(0.6f, 0.6f, 0.6f);
        Object.DestroyImmediate(shadow.GetComponent<CapsuleCollider>());

        NavMeshAgent shadowAgent = shadow.AddComponent<NavMeshAgent>();
        shadowAgent.agentTypeID = shadowId;
        shadowAgent.radius = 0.3f;
        shadowAgent.height = 1.2f;
        shadowAgent.speed = 3.2f;

        shadow.AddComponent<ShadowAI>();
    }

    // -----------------------------------------------------------------
    // Shared helpers
    // -----------------------------------------------------------------

    private static float SampleHeight(Terrain terrain, Vector3 worldPosition)
    {
        return terrain.transform.position.y +
               terrain.SampleHeight(worldPosition);
    }

    private static GameObject FindOrCreate(string name, GameObject parent)
    {
        GameObject found = GameObject.Find(name);

        if (found == null)
        {
            found = new GameObject(name);

            if (parent != null)
            {
                found.transform.SetParent(parent.transform, false);
            }
        }

        return found;
    }

    private static void ClearChildren(GameObject parent)
    {
        for (int i = parent.transform.childCount - 1; i >= 0; i--)
        {
            Object.DestroyImmediate(parent.transform.GetChild(i).gameObject);
        }
    }

    private static T Ensure<T>(GameObject target) where T : Component
    {
        T component = target.GetComponent<T>();
        return component == null ? target.AddComponent<T>() : component;
    }
}
