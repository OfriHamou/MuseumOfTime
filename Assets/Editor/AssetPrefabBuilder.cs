using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Turns the meshes exported from Blender into usable Unity prefabs:
///
///   - the Voronoi shards become FracturedObject prefabs, one collider and
///     rigidbody per shard;
///   - the decimated tiers become prefabs with a real LODGroup.
///
///   Unity.exe -batchmode -quit -projectPath . ^
///             -executeMethod AssetPrefabBuilder.BuildFromCommandLine
///
/// Idempotent: prefabs are written fresh each run.
/// </summary>
public static class AssetPrefabBuilder
{
    private const string FracturedSource = "Assets/Models/Fractured";
    private const string LodSource = "Assets/Models/LOD";
    private const string PrefabFolder = "Assets/Prefabs/World";

    // Screen-relative heights at which each tier takes over. LOD2 covers
    // everything below 8%, and nothing culls entirely.
    private static readonly float[] LodTransitions = { 0.6f, 0.25f, 0.02f };

    [MenuItem("Museum of Time/Build Fracture and LOD Prefabs")]
    public static void BuildMenu()
    {
        Build();
    }

    public static void BuildFromCommandLine()
    {
        Build();
    }

    private static void Build()
    {
        Directory.CreateDirectory(PrefabFolder);
        AssetDatabase.Refresh();

        Material marble = AssetDatabase.LoadAssetAtPath<Material>(
            "Assets/Materials/Museum/MuseumMarble.mat");

        BuildFracturePrefab("ClockOfCreation", marble);
        BuildFracturePrefab("FrozenStatue", marble);

        BuildLodPrefab("MarbleStatue", marble);
        BuildLodPrefab("StoneColumn", marble);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
    }

    // -----------------------------------------------------------------
    // Voronoi shards -> a breakable prefab
    // -----------------------------------------------------------------

    private static void BuildFracturePrefab(string label, Material material)
    {
        string path = FracturedSource + "/" + label + ".fbx";
        GameObject imported = AssetDatabase.LoadAssetAtPath<GameObject>(path);

        if (imported == null)
        {
            Debug.LogError("PREFAB FAILED: missing " + path +
                           ". Run Tools/voronoi_fracture.py first.");
            return;
        }

        var root = new GameObject(label);

        // The intact version: a single hull standing in for the whole object.
        var intact = new GameObject("Intact");
        intact.transform.SetParent(root.transform, false);

        // The shards, each its own rigid body.
        var shards = new GameObject("Shards");
        shards.transform.SetParent(root.transform, false);

        GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(imported);
        List<MeshFilter> pieces = instance
            .GetComponentsInChildren<MeshFilter>()
            .Where(f => f.sharedMesh != null)
            .ToList();

        int count = 0;

        foreach (MeshFilter piece in pieces)
        {
            var shard = new GameObject(piece.gameObject.name);
            shard.transform.SetParent(shards.transform, false);
            shard.transform.localPosition = piece.transform.position;
            shard.transform.localRotation = piece.transform.rotation;

            shard.AddComponent<MeshFilter>().sharedMesh = piece.sharedMesh;
            shard.AddComponent<MeshRenderer>().sharedMaterial = material;

            // Convex, because a non-convex MeshCollider cannot have a
            // Rigidbody and every shard needs to be thrown by the explosion.
            MeshCollider collider = shard.AddComponent<MeshCollider>();
            collider.sharedMesh = piece.sharedMesh;
            collider.convex = true;

            Rigidbody body = shard.AddComponent<Rigidbody>();
            body.isKinematic = true;   // released by FracturedObject.Break
            body.mass = 0.6f;

            count++;
        }

        // The unbroken silhouette: reuse the first shard's mesh only as a
        // placeholder stand-in until real art exists.
        if (pieces.Count > 0)
        {
            var whole = new GameObject("Whole");
            whole.transform.SetParent(intact.transform, false);
            whole.AddComponent<MeshFilter>().sharedMesh = pieces[0].sharedMesh;
            whole.AddComponent<MeshRenderer>().sharedMaterial = material;
        }

        Object.DestroyImmediate(instance);

        FracturedObject fractured = root.AddComponent<FracturedObject>();
        var so = new SerializedObject(fractured);
        so.FindProperty("intact").objectReferenceValue = intact;
        so.FindProperty("shards").objectReferenceValue = shards;
        so.ApplyModifiedPropertiesWithoutUndo();

        string prefabPath = PrefabFolder + "/" + label + ".prefab";
        PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
        Object.DestroyImmediate(root);

        Debug.Log("FRACTURE PREFAB OK: " + label + " with " + count + " shards");
    }

    // -----------------------------------------------------------------
    // Decimated tiers -> a prefab with a LODGroup
    // -----------------------------------------------------------------

    private static void BuildLodPrefab(string label, Material material)
    {
        string path = LodSource + "/" + label + ".fbx";
        GameObject imported = AssetDatabase.LoadAssetAtPath<GameObject>(path);

        if (imported == null)
        {
            Debug.LogError("PREFAB FAILED: missing " + path +
                           ". Run Tools/lod_generate.py first.");
            return;
        }

        var root = new GameObject(label);

        GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(imported);

        // The exporter names them <label>_LOD0/1/2; sort so tier order is
        // by name and not by whatever order the importer happened to use.
        List<MeshFilter> tiers = instance
            .GetComponentsInChildren<MeshFilter>()
            .Where(f => f.sharedMesh != null)
            .OrderBy(f => f.gameObject.name)
            .ToList();

        if (tiers.Count < 3)
        {
            Debug.LogError("PREFAB FAILED: " + label + " has " + tiers.Count +
                           " meshes, expected 3 LOD tiers.");
            Object.DestroyImmediate(instance);
            Object.DestroyImmediate(root);
            return;
        }

        var lods = new LOD[3];
        var counts = new int[3];

        for (int i = 0; i < 3; i++)
        {
            var tier = new GameObject(label + "_LOD" + i);
            tier.transform.SetParent(root.transform, false);

            tier.AddComponent<MeshFilter>().sharedMesh = tiers[i].sharedMesh;
            MeshRenderer renderer = tier.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;

            counts[i] = tiers[i].sharedMesh.triangles.Length / 3;
            lods[i] = new LOD(LodTransitions[i], new Renderer[] { renderer });
        }

        LODGroup group = root.AddComponent<LODGroup>();
        group.SetLODs(lods);
        group.RecalculateBounds();

        Object.DestroyImmediate(instance);

        string prefabPath = PrefabFolder + "/" + label + ".prefab";
        PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
        Object.DestroyImmediate(root);

        Debug.Log("LOD PREFAB OK: " + label + " tris " +
                  counts[0] + " / " + counts[1] + " / " + counts[2]);
    }
}
