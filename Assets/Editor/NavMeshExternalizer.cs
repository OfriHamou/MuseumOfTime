using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

/// <summary>
/// Root-cause fix for the gameplay scenes going empty/binary after every
/// pull: each NavMeshSurface's baked NavMeshData was embedded directly in
/// the scene. Unity cannot text-serialize NavMesh data, so ANY scene holding
/// it inline is forced to binary for the WHOLE file, regardless of the
/// project's ForceText EditorSettings - confirmed by direct diagnostic (a
/// brand-new scene saved as text; a fresh copy of a scene with embedded
/// NavMeshData did not), not assumed.
///
/// <see cref="SaveExternal"/> is called by NavigationBuilder and both
/// per-scene content builders immediately after baking, so a future rebuild
/// can never reintroduce the embedded state. <see cref="Run"/> is the
/// standalone recovery entry point for scenes that already have embedded
/// data (e.g. from before this fix existed).
///
///   Unity.exe -batchmode -quit -projectPath . ^
///             -executeMethod NavMeshExternalizer.Run
/// </summary>
public static class NavMeshExternalizer
{
    private const string NavMeshFolder = "Assets/NavMesh";

    private static readonly string[] ScenePaths =
    {
        "Assets/Scenes/MuseumNight.unity",
        "Assets/Scenes/FrozenCity.unity",
        "Assets/Scenes/ClockCore.unity",
    };

    [MenuItem("Museum of Time/Externalize Embedded NavMesh Data (recovery)")]
    public static void RunMenu() { Run(); }

    public static void Run()
    {
        if (!AssetDatabase.IsValidFolder(NavMeshFolder))
        {
            AssetDatabase.CreateFolder("Assets", "NavMesh");
        }

        foreach (string path in ScenePaths)
        {
            ExternalizeScene(path);
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log("EXTERNALIZE NAVMESH OK.");
    }

    private static void ExternalizeScene(string scenePath)
    {
        Scene scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
        string sceneName = System.IO.Path.GetFileNameWithoutExtension(scenePath);
        int extracted = 0;

        foreach (NavMeshSurface surface in Object.FindObjectsByType<NavMeshSurface>(
                     FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (SaveExternal(surface, sceneName))
            {
                extracted++;
            }
        }

        if (extracted > 0)
        {
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        Debug.Log("EXTERNALIZE NAVMESH [" + scenePath + "]: " + extracted + " NavMeshData asset(s) extracted.");
    }

    /// <summary>
    /// If the surface's baked NavMeshData is embedded (no asset path), moves
    /// it to Assets/NavMesh/&lt;sceneName&gt;_&lt;surfaceName&gt;.asset and
    /// re-points the surface at the external file. Call this right after
    /// BuildNavMesh() in any builder that bakes navmesh for one of the
    /// gameplay scenes. Returns true if it actually extracted something.
    /// </summary>
    public static bool SaveExternal(NavMeshSurface surface, string sceneName)
    {
        if (!AssetDatabase.IsValidFolder(NavMeshFolder))
        {
            AssetDatabase.CreateFolder("Assets", "NavMesh");
        }

        NavMeshData data = surface.navMeshData;
        if (data == null)
        {
            return false;
        }

        string existingPath = AssetDatabase.GetAssetPath(data);
        if (!string.IsNullOrEmpty(existingPath))
        {
            return false; // already external
        }

        // Fixed, deterministic path (not GenerateUniqueAssetPath) so a
        // repeated rebuild overwrites the same file instead of accumulating
        // orphaned "_1", "_2", ... copies on every run.
        string assetPath = NavMeshFolder + "/" + sceneName + "_" + surface.name + ".asset";
        AssetDatabase.DeleteAsset(assetPath);
        AssetDatabase.CreateAsset(data, assetPath);
        EditorUtility.SetDirty(surface);

        Debug.Log("EXTERNALIZE NAVMESH: '" + surface.name + "' in " + sceneName + " -> " + assetPath);
        return true;
    }
}
