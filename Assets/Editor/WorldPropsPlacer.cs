using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Places the fracture and LOD prefabs into MuseumNight. The LOD props are
/// instanced several times each, because LOD only earns its keep when the
/// same mesh appears repeatedly at different distances.
/// </summary>
public static class WorldPropsPlacer
{
    private const string ScenePath = "Assets/Scenes/MuseumNight.unity";

    [MenuItem("Museum of Time/Place World Props")]
    public static void PlaceMenu() { Place(); }

    public static void PlaceFromCommandLine() { Place(); }

    private static void Place()
    {
        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

        Replace("Destructibles", "ClockOfCreation",
            new[] { new Vector3(-9f, 1.2f, 8f) });

        // Columns line the main hall; statues stand along the east wall.
        Replace("LODObjects", "StoneColumn", new[]
        {
            new Vector3(-6f, 0f, -6f), new Vector3(-6f, 0f, 0f),
            new Vector3(-6f, 0f, 6f),  new Vector3(6f, 0f, -6f),
            new Vector3(6f, 0f, 0f),   new Vector3(6f, 0f, 6f),
        });

        Replace("LODObjects", "MarbleStatue", new[]
        {
            new Vector3(11f, 0f, -4f), new Vector3(11f, 0f, 2f),
            new Vector3(11f, 0f, 8f),
        }, append: true);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
    }

    private static void Replace(
        string parentName, string prefabName, Vector3[] positions,
        bool append = false)
    {
        GameObject parent = GameObject.Find(parentName);
        if (parent == null)
        {
            parent = new GameObject(parentName);
        }

        if (!append)
        {
            for (int i = parent.transform.childCount - 1; i >= 0; i--)
            {
                Object.DestroyImmediate(parent.transform.GetChild(i).gameObject);
            }
        }

        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
            "Assets/Prefabs/World/" + prefabName + ".prefab");

        if (prefab == null)
        {
            Debug.LogError("PLACE FAILED: no prefab " + prefabName);
            return;
        }

        foreach (Vector3 position in positions)
        {
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            instance.transform.SetParent(parent.transform, false);
            instance.transform.position = position;
        }

        Debug.Log("PLACED " + positions.Length + "x " + prefabName +
                  " under " + parentName);
    }
}
