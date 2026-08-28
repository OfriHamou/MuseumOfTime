using UnityEditor;
using UnityEngine;

/// <summary>
/// Keeps the Blender-authored FBX imports on the settings the prefab builder
/// expects, and reports the resulting real-world sizes.
///
///   Unity.exe -batchmode -quit -projectPath . ^
///             -executeMethod ModelScaleFixBuilder.BuildFromCommandLine
///
/// The FBX files themselves were always correct. Each one carries per-node
/// transforms of rotation (89.98, 0, 0) - Blender's Z-up to Unity's Y-up - and
/// scale 100, which the importer's fileScale of 0.01 cancels back to 1. An
/// instantiated FBX is therefore a correctly sized, upright 4 m column.
///
/// What was broken is that AssetPrefabBuilder rebuilt the prefabs from the raw
/// sharedMesh of each node onto brand-new GameObjects with identity
/// transforms, discarding BOTH of those. The Voronoi shards (T10) and the LOD
/// tiers (T11) ended up as ~1 cm objects lying on their side in a 30 m museum:
/// present in the hierarchy, invisible in play, and breaking S10's scale rule.
/// The fix lives in AssetPrefabBuilder; this pass only guarantees the import
/// settings it depends on and prints the sizes as evidence.
/// </summary>
public static class ModelScaleFixBuilder
{
    private static readonly string[] ModelPaths =
    {
        "Assets/Models/Fractured/ClockOfCreation.fbx",
        "Assets/Models/Fractured/FrozenStatue.fbx",
        "Assets/Models/LOD/MarbleStatue.fbx",
        "Assets/Models/LOD/StoneColumn.fbx",
    };

    [MenuItem("Museum of Time/Fix Model Import Scale")]
    public static void BuildMenu() { Build(); }

    public static void BuildFromCommandLine() { Build(); }

    private static void Build()
    {
        foreach (string path in ModelPaths)
        {
            var importer = AssetImporter.GetAtPath(path) as ModelImporter;
            if (importer == null)
            {
                Debug.LogWarning("SCALE: no ModelImporter at " + path);
                continue;
            }

            bool changed = false;

            // fileScale (0.01) is what cancels the FBX nodes' own scale of
            // 100. Turning it off does not make the models bigger in any
            // useful sense - it makes them 100x too big.
            if (!importer.useFileScale)
            {
                importer.useFileScale = true;
                changed = true;
            }

            if (!Mathf.Approximately(importer.globalScale, 1f))
            {
                importer.globalScale = 1f;
                changed = true;
            }

            if (importer.bakeAxisConversion)
            {
                importer.bakeAxisConversion = false;
                changed = true;
            }

            // Shards need readable geometry for convex MeshColliders.
            if (!importer.isReadable)
            {
                importer.isReadable = true;
                changed = true;
            }

            if (importer.importNormals != ModelImporterNormals.Calculate)
            {
                importer.importNormals = ModelImporterNormals.Calculate;
                importer.normalSmoothingAngle = 40f;
                changed = true;
            }

            if (changed)
            {
                importer.SaveAndReimport();
            }

            Report(path);
        }

        RecalculateLodBounds();

        AssetDatabase.SaveAssets();
        Debug.Log("=== MODEL SCALE FIX COMPLETE ===");
    }

    private static void Report(string path)
    {
        var go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (go == null) { return; }

        // Measured on an INSTANCE, not on sharedMesh.bounds: the mesh bounds
        // are pre-transform and say 0.01 units for a model that instantiates
        // at 4 m. Measuring the instance is what actually answers "how big is
        // this in the game".
        var inst = (GameObject)PrefabUtility.InstantiatePrefab(go);
        inst.transform.position = Vector3.zero;
        inst.transform.rotation = Quaternion.identity;

        var bounds = new Bounds();
        bool any = false;
        int triangles = 0;

        foreach (MeshFilter mf in inst.GetComponentsInChildren<MeshFilter>(true))
        {
            if (mf.sharedMesh == null) { continue; }
            triangles += mf.sharedMesh.triangles.Length / 3;
        }

        foreach (Renderer r in inst.GetComponentsInChildren<Renderer>(true))
        {
            if (!any) { bounds = r.bounds; any = true; }
            else { bounds.Encapsulate(r.bounds); }
        }

        Debug.Log("SCALE: " + System.IO.Path.GetFileName(path) +
                  " -> instantiated size " + bounds.size.ToString("F2") +
                  ", " + triangles + " tris");

        Object.DestroyImmediate(inst);
    }

    /// <summary>
    /// LODGroup screen-relative transition heights are derived from the
    /// group's bounding size, which was captured when the meshes were 100x too
    /// small. Recalculating makes the tiers switch at sane distances.
    /// </summary>
    private static void RecalculateLodBounds()
    {
        string[] prefabs =
        {
            "Assets/Prefabs/World/StoneColumn.prefab",
            "Assets/Prefabs/World/MarbleStatue.prefab",
        };

        foreach (string path in prefabs)
        {
            GameObject root = PrefabUtility.LoadPrefabContents(path);
            if (root == null) { continue; }

            var group = root.GetComponent<LODGroup>();
            if (group != null)
            {
                group.RecalculateBounds();
                PrefabUtility.SaveAsPrefabAsset(root, path);
                Debug.Log("SCALE: recalculated LOD bounds on " +
                          System.IO.Path.GetFileName(path) +
                          " -> size " + group.size.ToString("F2"));
            }

            PrefabUtility.UnloadPrefabContents(root);
        }
    }
}
