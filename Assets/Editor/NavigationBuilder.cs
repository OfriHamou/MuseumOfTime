using System.Collections.Generic;
using Unity.AI.Navigation;
using UnityEditor.Animations;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

/// <summary>
/// Builds the navigation for Phase 4.
///
/// The requirement is two agent types on different routes, each with its own
/// bake. All three parts matter and only the first is obvious:
///
///   1. Two agent types, with genuinely different dimensions.
///   2. Two separate NavMeshSurface components, baked independently. One
///      surface carrying two agents would look similar in the Scene view but
///      would not be a separate bake.
///   3. Routes that actually differ. That is arranged here through geometry:
///      a narrow gap and a low ledge that the small, agile Shadow can use and
///      the larger, stiffer Warden cannot, so the two navmeshes genuinely
///      disagree about where it is possible to walk.
///
/// Unity's NavMesh baking IS Recast: com.unity.ai.navigation wraps the
/// Recast/Detour library, which is what the brief means by that word.
///
///   Unity.exe -batchmode -quit -projectPath . ^
///             -executeMethod NavigationBuilder.BuildFromCommandLine
/// </summary>
public static class NavigationBuilder
{
    private const string ScenePath = "Assets/Scenes/MuseumNight.unity";

    private const string WardenAgentName = "WardenAgent";
    private const string ShadowAgentName = "ShadowAgent";

    [MenuItem("Museum of Time/Build Navigation (two agent types)")]
    public static void BuildMenu() { Build(); }

    public static void BuildFromCommandLine() { Build(); }

    private static void Build()
    {
        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

        RemoveOrphanAgentTypes();

        int wardenId = EnsureAgentType(
            WardenAgentName, radius: 0.5f, height: 2.0f, climb: 0.4f, slope: 45f);

        int shadowId = EnsureAgentType(
            ShadowAgentName, radius: 0.3f, height: 1.2f, climb: 0.9f, slope: 60f);

        if (wardenId == shadowId)
        {
            Debug.LogError("NAV FAILED: the two agent types resolved to the " +
                           "same id, so there is only one type.");
            return;
        }

        BuildObstacleCourse();

        GameObject root = FindOrCreate("Navigation", null);

        NavMeshSurface wardenSurface =
            MakeSurface(root, "NavMesh_Warden", wardenId);

        NavMeshSurface shadowSurface =
            MakeSurface(root, "NavMesh_Shadow", shadowId);

        // Two bakes, run separately. This is the "separate bake" clause.
        wardenSurface.BuildNavMesh();
        shadowSurface.BuildNavMesh();

        SpawnAgents(wardenId, shadowId);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);

        Debug.Log(
            "NAV OK: " + Describe(wardenId) + " | " + Describe(shadowId) +
            " | two NavMeshSurfaces baked separately.");
    }

    // -----------------------------------------------------------------
    // Agent types
    // -----------------------------------------------------------------

    /// <summary>
    /// Finds an agent type by name, or creates one, and writes its dimensions
    /// IN PLACE.
    ///
    /// The obvious approach - RemoveSettings then CreateSettings - does not
    /// work: it mints a fresh agentTypeID every run, orphans the old entry,
    /// and leaves agents in the scene pointing at ids that no longer exist.
    /// Unity has no public setter for these fields, so the settings object has
    /// to be edited through SerializedObject instead.
    /// </summary>
    private static int EnsureAgentType(
        string name, float radius, float height, float climb, float slope)
    {
        Object asset = Unsupported.GetSerializedAssetInterfaceSingleton(
            "NavMeshProjectSettings");

        if (asset == null)
        {
            Debug.LogError("NAV FAILED: cannot reach NavMeshProjectSettings.");
            return -1;
        }

        var so = new SerializedObject(asset);
        SerializedProperty settings = so.FindProperty("m_Settings");
        SerializedProperty names = so.FindProperty("m_SettingNames");

        int index = IndexOfName(names, name);

        if (index < 0)
        {
            // CreateSettings appends a new entry with a valid unique id; the
            // dimensions are then written onto that entry rather than by
            // replacing it.
            NavMeshBuildSettings created = NavMesh.CreateSettings();
            so.Update();

            index = IndexOfAgentTypeId(settings, created.agentTypeID);

            if (index < 0)
            {
                Debug.LogError("NAV FAILED: new agent type " + name +
                               " did not appear in the settings list.");
                return -1;
            }

            if (names.arraySize <= index)
            {
                names.arraySize = index + 1;
            }

            names.GetArrayElementAtIndex(index).stringValue = name;
        }

        SerializedProperty entry = settings.GetArrayElementAtIndex(index);
        entry.FindPropertyRelative("agentRadius").floatValue = radius;
        entry.FindPropertyRelative("agentHeight").floatValue = height;
        entry.FindPropertyRelative("agentClimb").floatValue = climb;
        entry.FindPropertyRelative("agentSlope").floatValue = slope;

        so.ApplyModifiedPropertiesWithoutUndo();

        return entry.FindPropertyRelative("agentTypeID").intValue;
    }

    /// <summary>
    /// Deletes agent types left behind by an earlier failed attempt, so the
    /// project settings do not accumulate junk on every run.
    /// </summary>
    private static void RemoveOrphanAgentTypes()
    {
        Object asset = Unsupported.GetSerializedAssetInterfaceSingleton(
            "NavMeshProjectSettings");

        if (asset == null)
        {
            return;
        }

        var so = new SerializedObject(asset);
        SerializedProperty settings = so.FindProperty("m_Settings");
        SerializedProperty names = so.FindProperty("m_SettingNames");

        bool changed = false;

        for (int i = names.arraySize - 1; i >= 0; i--)
        {
            string entryName = names.GetArrayElementAtIndex(i).stringValue;

            // Humanoid is Unity's built-in and must stay. Anything still
            // called "New Agent" is debris from a previous run.
            if (!entryName.StartsWith("New Agent"))
            {
                continue;
            }

            names.DeleteArrayElementAtIndex(i);

            if (i < settings.arraySize)
            {
                settings.DeleteArrayElementAtIndex(i);
            }

            changed = true;
        }

        if (changed)
        {
            so.ApplyModifiedPropertiesWithoutUndo();
            Debug.Log("NAV: removed orphaned agent types from a previous run.");
        }
    }

    private static int IndexOfName(SerializedProperty names, string name)
    {
        for (int i = 0; i < names.arraySize; i++)
        {
            if (names.GetArrayElementAtIndex(i).stringValue == name)
            {
                return i;
            }
        }

        return -1;
    }

    private static int IndexOfAgentTypeId(SerializedProperty settings, int id)
    {
        for (int i = 0; i < settings.arraySize; i++)
        {
            SerializedProperty entry = settings.GetArrayElementAtIndex(i);

            if (entry.FindPropertyRelative("agentTypeID").intValue == id)
            {
                return i;
            }
        }

        return -1;
    }

    /// <summary>Reads back what is actually stored, not what we asked for.</summary>
    private static string Describe(int agentTypeId)
    {
        for (int i = 0; i < NavMesh.GetSettingsCount(); i++)
        {
            NavMeshBuildSettings s = NavMesh.GetSettingsByIndex(i);

            if (s.agentTypeID != agentTypeId)
            {
                continue;
            }

            return NavMesh.GetSettingsNameFromID(agentTypeId) +
                   "(id " + agentTypeId +
                   " r=" + s.agentRadius.ToString("0.00") +
                   " h=" + s.agentHeight.ToString("0.0") +
                   " climb=" + s.agentClimb.ToString("0.00") + ")";
        }

        return "MISSING(id " + agentTypeId + ")";
    }

    // -----------------------------------------------------------------
    // Geometry that makes the two routes differ
    // -----------------------------------------------------------------

    /// <summary>
    /// A shortcut only the Shadow can take: a gap too narrow for a 0.5m-radius
    /// Warden, behind a ledge too tall for its 0.4m climb. The Warden has to
    /// walk around, so the two bakes describe different worlds.
    /// </summary>
    private static void BuildObstacleCourse()
    {
        GameObject existing = GameObject.Find("ShadowShortcut");
        if (existing != null)
        {
            Object.DestroyImmediate(existing);
        }

        var root = new GameObject("ShadowShortcut");
        root.transform.position = Vector3.zero;

        const string plasterPath = "Assets/Materials/Museum/MuseumPlaster.mat";
        Material plaster = AssetDatabase.LoadAssetAtPath<Material>(plasterPath);
        if (plaster == null)
        {
            // A stale AssetDatabase index earlier in the same batch can miss
            // an asset another builder just created - one retry after a
            // refresh is enough to pick it up correctly.
            AssetDatabase.Refresh();
            plaster = AssetDatabase.LoadAssetAtPath<Material>(plasterPath);
        }

        // A wall across the east side with a 0.8m slot in it. A Warden needs
        // 1.0m of clearance for its diameter; a Shadow needs 0.6m.
        Block(root, "SlotWallLeft", new Vector3(10f, 1.5f, -1.4f),
              new Vector3(0.4f, 3f, 2.0f), plaster);

        Block(root, "SlotWallRight", new Vector3(10f, 1.5f, 1.4f),
              new Vector3(0.4f, 3f, 2.0f), plaster);

        // A 0.7m step up on the far side: inside the Shadow's 0.9m climb,
        // outside the Warden's 0.4m.
        Block(root, "ShadowLedge", new Vector3(11.5f, 0.35f, 0f),
              new Vector3(2.5f, 0.7f, 3f), plaster);
    }

    private static void Block(
        GameObject parent, string name, Vector3 position, Vector3 size,
        Material material)
    {
        GameObject box = GameObject.CreatePrimitive(PrimitiveType.Cube);
        box.name = name;
        box.transform.SetParent(parent.transform, false);
        box.transform.position = position;
        box.transform.localScale = size;

        if (material == null)
        {
            // A stale AssetDatabase lookup earlier in the batch can return
            // null here even when the asset exists on disk - never leave the
            // renderer on Unity's default (URP renders it as solid magenta).
            Debug.LogWarning("NAV: material missing for '" + name + "' - using a plain fallback.");
            material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            material.SetColor("_BaseColor", new Color(0.6f, 0.6f, 0.62f));
        }

        box.GetComponent<MeshRenderer>().sharedMaterial = material;

        GameObjectUtility.SetStaticEditorFlags(
            box, StaticEditorFlags.NavigationStatic);
    }

    // -----------------------------------------------------------------
    // Surfaces and agents
    // -----------------------------------------------------------------

    private static NavMeshSurface MakeSurface(
        GameObject parent, string name, int agentTypeId)
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

        for (int i = enemies.transform.childCount - 1; i >= 0; i--)
        {
            Object.DestroyImmediate(enemies.transform.GetChild(i).gameObject);
        }

        // ---- Warden: patrols the main hall ----
        GameObject warden = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        warden.name = "TimeWarden";
        warden.transform.SetParent(enemies.transform, false);
        warden.transform.position = new Vector3(-6f, 1.1f, -6f);
        Object.DestroyImmediate(warden.GetComponent<CapsuleCollider>());

        NavMeshAgent wardenAgent = warden.AddComponent<NavMeshAgent>();
        wardenAgent.agentTypeID = wardenId;
        wardenAgent.radius = 0.5f;
        wardenAgent.height = 2f;
        wardenAgent.speed = 2.2f;

        PatrolRoute route = warden.AddComponent<PatrolRoute>();
        route.SetWaypoints(new List<PatrolRoute.Waypoint>
        {
            new PatrolRoute.Waypoint
            { position = new Vector3(-6f, 0f, -6f), waitSeconds = 2.5f },
            new PatrolRoute.Waypoint
            { position = new Vector3(-6f, 0f, 6f), waitSeconds = 3f },
            new PatrolRoute.Waypoint
            { position = new Vector3(4f, 0f, 6f), waitSeconds = 2.5f },
            new PatrolRoute.Waypoint
            { position = new Vector3(4f, 0f, -6f), waitSeconds = 3f },
        });

        warden.AddComponent<WardenAI>();

        // The Animator authored by WardenAnimatorBuilder, plus the driver
        // that feeds it from the AI state.
        Animator wardenAnimator = warden.AddComponent<Animator>();
        wardenAnimator.applyRootMotion = false;

        var wardenController = AssetDatabase.LoadAssetAtPath<AnimatorController>(
            "Assets/Animations/Enemies/WardenController.controller");

        if (wardenController == null)
        {
            Debug.LogWarning("WardenController not found. Run " +
                             "Museum of Time > Build Warden Animator Controller.");
        }
        else
        {
            wardenAnimator.runtimeAnimatorController = wardenController;
        }

        WardenAnimatorDriver driver = warden.AddComponent<WardenAnimatorDriver>();
        var driverSo = new SerializedObject(driver);
        driverSo.FindProperty("animator").objectReferenceValue = wardenAnimator;
        driverSo.ApplyModifiedPropertiesWithoutUndo();

        // ---- Shadow: drifts near the shortcut it alone can use ----
        GameObject shadow = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        shadow.name = "ChronologicalShadow";
        shadow.transform.SetParent(enemies.transform, false);
        shadow.transform.position = new Vector3(8f, 0.7f, 0f);
        shadow.transform.localScale = new Vector3(0.6f, 0.6f, 0.6f);
        Object.DestroyImmediate(shadow.GetComponent<CapsuleCollider>());

        NavMeshAgent shadowAgent = shadow.AddComponent<NavMeshAgent>();
        shadowAgent.agentTypeID = shadowId;
        shadowAgent.radius = 0.3f;
        shadowAgent.height = 1.2f;
        shadowAgent.speed = 3.2f;

        shadow.AddComponent<ShadowAI>();
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
}
