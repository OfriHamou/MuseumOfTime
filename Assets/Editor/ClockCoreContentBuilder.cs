using System.Collections.Generic;
using Unity.AI.Navigation;
using Unity.Cinemachine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

/// <summary>
/// Phase 6, Step 6.3: the infrastructure ClockCore needs to be a real,
/// walkable, testable scene - a Player, both AI agent types present on
/// their own navmeshes (Part 3's requirement-placement table lists "both"
/// for this scene), era travel already unlocked, and two more Time Anchors.
///
///   Unity.exe -batchmode -quit -projectPath . ^
///             -executeMethod ClockCoreContentBuilder.BuildFromCommandLine
///
/// Includes the three-phase Collector boss fight (Assets/Scripts/AI/Collector.cs):
/// shielded in the Past (break it with the Chrono Orb), summons the scene's
/// Warden in the Present, and erodes Noa's health in the Future unless the
/// Chrono Hourglass is active - the era switch is the fight's own mechanic,
/// not a side ability, matching the plan's framing. Defeating it loads
/// Victory directly.
/// </summary>
public static class ClockCoreContentBuilder
{
    private const string ScenePath = "Assets/Scenes/ClockCore.unity";

    [MenuItem("Museum of Time/Build ClockCore Content (Phase 6)")]
    public static void BuildMenu() { Build(); }

    public static void BuildFromCommandLine() { Build(); }

    private static void Build()
    {
        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

        BuildFloor();
        GameObject player = BuildPlayerAndCameras();
        if (player == null)
        {
            return;
        }

        BuildManagers();
        int anchorCount = BuildAnchors();
        int agentsPlaced = BuildNavigationAndAgents();
        BuildCollector();

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);

        Debug.Log("CLOCKCORE OK: greybox floor, player, era travel unlocked, " +
                   anchorCount + " more Time Anchors, " + agentsPlaced +
                   " AI agents present on their own navmeshes, three-phase Collector boss.");
    }

    // -----------------------------------------------------------------
    // The Collector: three phases, one per era. Reuses the Warden already
    // placed by BuildNavigationAndAgents as the one "summoned" in Phase 2,
    // rather than spawning a second, separately-baked enemy.
    // -----------------------------------------------------------------

    private static void BuildCollector()
    {
        GameObject existing = GameObject.Find("Collector");
        if (existing != null)
        {
            Object.DestroyImmediate(existing);
        }

        GameObject collector = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        collector.name = "Collector";
        collector.transform.position = new Vector3(0f, 1f, 8f);

        Material material = AssetDatabase.LoadAssetAtPath<Material>(
            "Assets/Materials/Museum/MuseumBrass.mat");
        if (material != null)
        {
            collector.GetComponent<MeshRenderer>().sharedMaterial = material;
        }

        // Static geometry, deliberately no Rigidbody: the orb (which has one)
        // still reports OnCollisionEnter to a static collider, and the
        // Collector should not go flying when it is hit.
        Object.DestroyImmediate(collector.GetComponent<CapsuleCollider>());
        collector.AddComponent<CapsuleCollider>();

        GameObject shield = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        shield.name = "Shield";
        shield.transform.SetParent(collector.transform, false);
        shield.transform.localScale = new Vector3(1.6f, 1.1f, 1.6f);
        Object.DestroyImmediate(shield.GetComponent<Collider>());

        Material glass = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Museum/MuseumBrass.mat");
        if (glass != null)
        {
            shield.GetComponent<MeshRenderer>().sharedMaterial = glass;
        }

        collector.AddComponent<SceneLoader>();
        Collector boss = collector.AddComponent<Collector>();

        GameObject warden = GameObject.Find("TimeWarden");

        var so = new SerializedObject(boss);
        so.FindProperty("shieldVisual").objectReferenceValue = shield;
        so.FindProperty("summonedWarden").objectReferenceValue = warden;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    // -----------------------------------------------------------------
    // A greybox arena. ClockCore had no geometry at all before this -
    // not even a floor - so nothing here could be walked on or tested.
    // -----------------------------------------------------------------

    private static void BuildFloor()
    {
        GameObject existing = GameObject.Find("Floor");
        if (existing != null)
        {
            Object.DestroyImmediate(existing);
        }

        GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
        floor.name = "Floor";
        floor.transform.position = new Vector3(0f, -0.5f, 0f);
        floor.transform.localScale = new Vector3(40f, 1f, 40f);

        Material material = AssetDatabase.LoadAssetAtPath<Material>(
            "Assets/Materials/Museum/MuseumMarble.mat");
        if (material != null)
        {
            floor.GetComponent<MeshRenderer>().sharedMaterial = material;
        }

        GameObjectUtility.SetStaticEditorFlags(floor, StaticEditorFlags.NavigationStatic);

        GameObject architecture = GameObject.Find("Architecture");
        if (architecture != null)
        {
            floor.transform.SetParent(architecture.transform, true);
        }
    }

    // -----------------------------------------------------------------
    // Player and cameras - identical pattern to FrozenCityContentBuilder,
    // minus terrain height sampling (the floor here is flat).
    // -----------------------------------------------------------------

    private static GameObject BuildPlayerAndCameras()
    {
        GameObject player = GameObject.Find("Player");

        if (player == null)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabBuilder.PrefabPath);

            if (prefab == null)
            {
                Debug.LogError("CLOCKCORE FAILED: Player prefab missing - run Build Player Prefab first.");
                return null;
            }

            player = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        }

        GameObject spawnMarker = GameObject.Find("PlayerSpawn");
        Vector3 spawnPos = spawnMarker != null ? spawnMarker.transform.position : new Vector3(0f, 0.1f, -15f);
        spawnPos.y = 0.1f;
        player.transform.SetPositionAndRotation(spawnPos, Quaternion.identity);

        GameObject mainCamera = FindOrCreate("MainCamera", null);
        mainCamera.tag = "MainCamera";
        Ensure<Camera>(mainCamera).nearClipPlane = 0.05f;
        mainCamera.GetComponent<Camera>().farClipPlane = 300f;
        Ensure<AudioListener>(mainCamera);
        Ensure<CinemachineBrain>(mainCamera);

        Transform pivot = player.transform.Find("CameraPivot");

        GameObject thirdPerson = FindOrCreate("ThirdPersonCamera", null);
        CinemachineCamera tpCam = Ensure<CinemachineCamera>(thirdPerson);
        tpCam.Follow = pivot;
        tpCam.LookAt = pivot;
        tpCam.Lens.FieldOfView = 60f;

        CinemachineThirdPersonFollow follow = Ensure<CinemachineThirdPersonFollow>(thirdPerson);
        follow.ShoulderOffset = new Vector3(0.5f, 0f, 0f);
        follow.VerticalArmLength = 0.15f;
        follow.CameraDistance = 2.6f;

        var rigSo = new SerializedObject(player.GetComponent<PlayerCameraRig>());
        rigSo.FindProperty("thirdPersonCamera").objectReferenceValue = tpCam;
        rigSo.ApplyModifiedPropertiesWithoutUndo();

        return player;
    }

    private static void BuildManagers()
    {
        GameObject eraObject = FindOrCreate("EraManager", null);
        EraManager era = Ensure<EraManager>(eraObject);

        var eraSo = new SerializedObject(era);
        eraSo.FindProperty("startingEra").enumValueIndex = (int)TimeEra.Present;
        // Both items are already held by the time Noa reaches ClockCore.
        eraSo.FindProperty("eraTravelUnlocked").boolValue = true;
        eraSo.ApplyModifiedPropertiesWithoutUndo();

        GameObject respawnObject = FindOrCreate("RespawnService", null);
        Ensure<RespawnService>(respawnObject);
    }

    private static int BuildAnchors()
    {
        GameObject parent = FindOrCreate("Checkpoints", null);
        ClearChildren(parent);

        MakeAnchor(parent, "TimeAnchor_EastWing", new Vector3(12f, 0.05f, 8f));
        MakeAnchor(parent, "TimeAnchor_WestWing", new Vector3(-12f, 0.05f, -8f));

        return parent.transform.childCount;
    }

    private static void MakeAnchor(GameObject parent, string name, Vector3 position)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent.transform, false);
        go.transform.position = position;

        BoxCollider box = go.AddComponent<BoxCollider>();
        box.isTrigger = true;
        box.size = new Vector3(1.5f, 2f, 1.5f);

        GameObject visual = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        visual.name = "LensVisual";
        visual.transform.SetParent(go.transform, false);
        visual.transform.localScale = Vector3.one * 0.4f;
        Object.DestroyImmediate(visual.GetComponent<Collider>());

        Material glass = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Museum/MuseumBrass.mat");
        if (glass != null)
        {
            visual.GetComponent<MeshRenderer>().sharedMaterial = glass;
        }

        TimeAnchor anchor = go.AddComponent<TimeAnchor>();
        var so = new SerializedObject(anchor);
        so.FindProperty("lensVisual").objectReferenceValue = visual;
        so.ApplyModifiedPropertiesWithoutUndo();

        go.AddComponent<TimeAnchorTrigger>();
    }

    // -----------------------------------------------------------------
    // Both agent types present, on their own bakes - reuses the same
    // project-wide WardenAgent/ShadowAgent types FrozenCity's builder does.
    // -----------------------------------------------------------------

    private static int BuildNavigationAndAgents()
    {
        int wardenId = ReadAgentTypeId("WardenAgent");
        int shadowId = ReadAgentTypeId("ShadowAgent");

        if (wardenId == int.MinValue || shadowId == int.MinValue || wardenId == shadowId)
        {
            Debug.LogError("CLOCKCORE FAILED: could not resolve WardenAgent/ShadowAgent ids " +
                            "from ProjectSettings/NavMeshAreas.asset - run " +
                            "Museum of Time > Build Navigation (two agent types) once first. " +
                            "Skipping AI placement.");
            return 0;
        }

        GameObject navRoot = FindOrCreate("Navigation", null);

        NavMeshSurface wardenSurface = MakeSurface(navRoot, "NavMesh_Warden", wardenId);
        NavMeshSurface shadowSurface = MakeSurface(navRoot, "NavMesh_Shadow", shadowId);

        wardenSurface.BuildNavMesh();
        shadowSurface.BuildNavMesh();

        SpawnAgents(wardenId, shadowId);

        return 2;
    }

    /// <summary>
    /// Same rationale as FrozenCityContentBuilder.ReadAgentTypeId: neither
    /// UnityEngine.AI.NavMesh's runtime API nor a fresh SerializedObject
    /// read of NavMeshProjectSettings reliably reflected
    /// ProjectSettings/NavMeshAreas.asset from a cold -batchmode process
    /// when this was diagnosed; reading the settings file's own YAML does.
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

    private static void SpawnAgents(int wardenId, int shadowId)
    {
        GameObject enemies = FindOrCreate("Enemies", null);
        ClearChildren(enemies);

        GameObject warden = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        warden.name = "TimeWarden";
        warden.transform.SetParent(enemies.transform, false);
        warden.transform.position = new Vector3(6f, 1.1f, 6f);
        Object.DestroyImmediate(warden.GetComponent<CapsuleCollider>());

        NavMeshAgent wardenAgent = warden.AddComponent<NavMeshAgent>();
        wardenAgent.agentTypeID = wardenId;
        wardenAgent.radius = 0.5f;
        wardenAgent.height = 2f;
        wardenAgent.speed = 2.2f;

        PatrolRoute route = warden.AddComponent<PatrolRoute>();
        route.SetWaypoints(new List<PatrolRoute.Waypoint>
        {
            new PatrolRoute.Waypoint { position = new Vector3(6f, 0f, 6f), waitSeconds = 2.5f },
            new PatrolRoute.Waypoint { position = new Vector3(6f, 0f, -6f), waitSeconds = 2.5f },
            new PatrolRoute.Waypoint { position = new Vector3(-6f, 0f, -6f), waitSeconds = 2.5f },
            new PatrolRoute.Waypoint { position = new Vector3(-6f, 0f, 6f), waitSeconds = 2.5f },
        });

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

        GameObject shadow = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        shadow.name = "ChronologicalShadow";
        shadow.transform.SetParent(enemies.transform, false);
        shadow.transform.position = new Vector3(-6f, 0.7f, 6f);
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
