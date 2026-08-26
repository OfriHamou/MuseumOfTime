using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Wires the Phase 3 systems into MuseumNight: the player components, the era
/// manager, the trigger volumes, the interactables and the Time Anchors.
///
///   Unity.exe -batchmode -quit -projectPath . ^
///             -executeMethod CoreSystemsBuilder.BuildFromCommandLine
///
/// Idempotent.
/// </summary>
public static class CoreSystemsBuilder
{
    private const string ScenePath = "Assets/Scenes/MuseumNight.unity";
    private const string OrbPrefabPath = "Assets/Prefabs/World/ChronoOrb.prefab";

    [MenuItem("Museum of Time/Build Core Systems")]
    public static void BuildMenu() { Build(); }

    public static void BuildFromCommandLine() { Build(); }

    private static void Build()
    {
        GameObject orbPrefab = CreateOrbPrefab();

        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

        GameObject player = GameObject.Find("Player");
        if (player == null)
        {
            Debug.LogError("CORE FAILED: no Player in the scene.");
            return;
        }

        // The player must be tagged, or triggers and respawn cannot find it.
        player.tag = "Player";

        Ensure<PlayerInteractor>(player);
        Ensure<ChronoHourglass>(player);

        ChronoOrbLauncher launcher = Ensure<ChronoOrbLauncher>(player);
        var launcherSo = new SerializedObject(launcher);
        launcherSo.FindProperty("orbPrefab").objectReferenceValue = orbPrefab;
        launcherSo.ApplyModifiedPropertiesWithoutUndo();

        // ---- Managers -----------------------------------------------------
        GameObject managers = FindOrCreate("--- MANAGERS --- ", null);

        GameObject eraObject = FindOrCreate("EraManager", managers);
        EraManager era = Ensure<EraManager>(eraObject);

        // MuseumNight is deliberately single-era until the Clock breaks, but
        // the manager still has to exist so era-bound objects have something
        // to subscribe to.
        var eraSo = new SerializedObject(era);
        eraSo.FindProperty("startingEra").enumValueIndex = (int)TimeEra.Present;
        eraSo.FindProperty("eraTravelUnlocked").boolValue = false;
        eraSo.ApplyModifiedPropertiesWithoutUndo();

        GameObject respawnObject = FindOrCreate("RespawnService", managers);
        Ensure<RespawnService>(respawnObject);

        // ---- Triggers -----------------------------------------------------
        GameObject triggers = FindOrCreate("Triggers", null);
        ClearChildren(triggers);

        MakeTrigger<RoomEntryTrigger>(triggers, "Trigger_MainGallery",
            new Vector3(0f, 1.5f, 2f), new Vector3(12f, 3f, 8f));

        MakeTrigger<TutorialTrigger>(triggers, "Trigger_TutorialMove",
            new Vector3(0f, 1.5f, -7f), new Vector3(8f, 3f, 4f));

        MakeTrigger<EraZoneTrigger>(triggers, "Trigger_ClockChamber",
            new Vector3(-9f, 1.5f, 8f), new Vector3(8f, 3f, 6f));

        MakeTrigger<HazardTrigger>(triggers, "Trigger_TemporalRift",
            new Vector3(9f, 1.5f, -6f), new Vector3(4f, 3f, 4f));

        // ---- Interactables -------------------------------------------------
        GameObject collectibles = FindOrCreate("Collectibles", null);
        ClearChildren(collectibles);

        MakeInteractable<ExhibitPlaque>(collectibles, "Plaque_ClockOfCreation",
            new Vector3(-7f, 1.4f, 8f));

        MakeInteractable<ShardPickup>(collectibles, "TimeShard_A",
            new Vector3(4f, 1f, 4f));

        MakeInteractable<ShardPickup>(collectibles, "TimeShard_B",
            new Vector3(-4f, 1f, -4f));

        // The Time Lens sits in the curator's office, upstairs, so reaching it
        // requires the staircase.
        GameObject lens = MakeInteractable<ItemPickup>(collectibles,
            "TimeLens", new Vector3(9f, 5.6f, 6f));

        var lensSo = new SerializedObject(lens.GetComponent<ItemPickup>());
        lensSo.FindProperty("item").enumValueIndex = (int)ItemPickup.Kind.TimeLens;
        lensSo.ApplyModifiedPropertiesWithoutUndo();

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);

        Debug.Log("CORE OK: interactor, hourglass, orb launcher, era manager, " +
                  "respawn service, 4 triggers, 4 interactables.");
    }

    private static GameObject CreateOrbPrefab()
    {
        var orb = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        orb.name = "ChronoOrb";
        orb.transform.localScale = Vector3.one * 0.22f;

        Object.DestroyImmediate(orb.GetComponent<SphereCollider>());
        orb.AddComponent<SphereCollider>().radius = 0.5f;

        var body = orb.AddComponent<Rigidbody>();
        body.mass = 0.5f;
        body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

        orb.AddComponent<ChronoOrb>();

        Material brass = AssetDatabase.LoadAssetAtPath<Material>(
            "Assets/Materials/Museum/MuseumBrass.mat");

        if (brass != null)
        {
            orb.GetComponent<MeshRenderer>().sharedMaterial = brass;
        }

        GameObject saved = PrefabUtility.SaveAsPrefabAsset(orb, OrbPrefabPath);
        Object.DestroyImmediate(orb);
        return saved;
    }

    private static GameObject MakeTrigger<T>(
        GameObject parent, string name, Vector3 position, Vector3 size)
        where T : Component
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent.transform, false);
        go.transform.position = position;

        BoxCollider box = go.AddComponent<BoxCollider>();
        box.isTrigger = true;
        box.size = size;

        go.AddComponent<T>();
        return go;
    }

    private static GameObject MakeInteractable<T>(
        GameObject parent, string name, Vector3 position)
        where T : Component
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        go.transform.SetParent(parent.transform, false);
        go.transform.position = position;
        go.transform.localScale = new Vector3(0.4f, 0.4f, 0.1f);

        go.AddComponent<T>();
        return go;
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
