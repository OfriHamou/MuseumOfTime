using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Gives every textured surface a consistent real-world texel density.
///
///   Unity.exe -batchmode -quit -projectPath . ^
///             -executeMethod SurfaceDensityBuilder.BuildFromCommandLine
///
/// The museum's materials each carried ONE fixed tiling (marble was 6x4) and
/// were then applied to objects of wildly different sizes - a 30x20 m floor
/// slab and a 1.5 m staircase step share MuseumMarble. On the floor that
/// stretched a 256 px marble tile across five metres, so the veining read as
/// camouflage rather than stone; on the steps the same material was far too
/// dense. Nothing in the scene looked like the material it was named after.
///
/// Rather than hand-authoring a material per object, this measures each
/// renderer's world size and assigns a shared material VARIANT whose tiling
/// gives the requested metres-per-tile. Variants are cached per (material,
/// tiling) pair, so the scene ends up with a handful of extra materials and
/// stays SRP-batchable - which a MaterialPropertyBlock would not.
/// </summary>
public static class SurfaceDensityBuilder
{
    private const string VariantFolder = "Assets/Materials/Museum/Generated";

    /// <summary>Metres of surface per texture repeat, per material.</summary>
    private static readonly Dictionary<string, float> MetresPerTile = new Dictionary<string, float>
    {
        { "MuseumMarble", 2.5f },
        { "MuseumPlaster", 3.0f },
        { "MuseumWood", 1.6f },
        { "MuseumBrass", 1.0f },
    };

    private static readonly string[] ScenePaths =
    {
        "Assets/Scenes/MuseumNight.unity",
        "Assets/Scenes/FrozenCity.unity",
        "Assets/Scenes/ClockCore.unity",
    };

    private static Dictionary<string, Material> variantCache;

    [MenuItem("Museum of Time/Build Surface Density")]
    public static void BuildMenu() { Build(); }

    public static void BuildFromCommandLine() { Build(); }

    private static void Build()
    {
        if (!AssetDatabase.IsValidFolder(VariantFolder))
        {
            AssetDatabase.CreateFolder("Assets/Materials/Museum", "Generated");
        }

        variantCache = new Dictionary<string, Material>();

        foreach (string scenePath in ScenePaths)
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(scenePath) == null) { continue; }

            Scene scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            int retiled = RetileOpenScene();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);

            Debug.Log("DENSITY OK: " + scene.name + " (" + retiled + " renderers retiled)");
        }

        AssetDatabase.SaveAssets();
        Debug.Log("=== SURFACE DENSITY COMPLETE (" + variantCache.Count + " variants) ===");
    }

    private static int RetileOpenScene()
    {
        int count = 0;

        foreach (MeshRenderer r in Object.FindObjectsByType<MeshRenderer>(FindObjectsInactive.Include))
        {
            Material source = r.sharedMaterial;
            if (source == null) { continue; }

            string baseName = StripVariantSuffix(source.name);
            if (!MetresPerTile.TryGetValue(baseName, out float metres)) { continue; }

            Material baseMaterial = LoadBase(baseName);
            if (baseMaterial == null) { continue; }

            Vector2 tiling = TilingFor(r, metres);
            Material variant = GetVariant(baseMaterial, baseName, tiling);

            if (r.sharedMaterial != variant)
            {
                r.sharedMaterial = variant;
                EditorUtility.SetDirty(r);
            }

            count++;
        }

        return count;
    }

    /// <summary>
    /// Unity's built-in cube maps every face to the full 0-1 UV range, so one
    /// tiling value has to serve all six. The two LARGEST dimensions are used,
    /// because those are the faces a player actually looks at - a floor slab's
    /// 30x20 top, not its 0.2 m edge.
    /// </summary>
    private static Vector2 TilingFor(Renderer r, float metresPerTile)
    {
        Vector3 size = r.bounds.size;

        float a = Mathf.Max(size.x, Mathf.Max(size.y, size.z));
        float c = Mathf.Min(size.x, Mathf.Min(size.y, size.z));
        float b = size.x + size.y + size.z - a - c;   // the middle one

        float u = Mathf.Max(1f, Mathf.Round(a / metresPerTile));
        float v = Mathf.Max(1f, Mathf.Round(b / metresPerTile));

        // Keep the variant count sane: snap to a coarse ladder so dozens of
        // near-identical steps all land on the same shared material.
        return new Vector2(Snap(u), Snap(v));
    }

    private static float Snap(float value)
    {
        float[] ladder = { 1f, 2f, 3f, 4f, 6f, 8f, 12f, 16f, 24f, 32f };

        float best = ladder[0];
        float bestDelta = Mathf.Abs(value - best);

        for (int i = 1; i < ladder.Length; i++)
        {
            float delta = Mathf.Abs(value - ladder[i]);
            if (delta < bestDelta) { best = ladder[i]; bestDelta = delta; }
        }

        return best;
    }

    private static Material GetVariant(Material baseMaterial, string baseName, Vector2 tiling)
    {
        string key = baseName + "_u" + tiling.x + "_v" + tiling.y;

        if (variantCache.TryGetValue(key, out Material cached) && cached != null)
        {
            return cached;
        }

        string path = VariantFolder + "/" + key + ".mat";
        var variant = AssetDatabase.LoadAssetAtPath<Material>(path);

        if (variant == null)
        {
            variant = new Material(baseMaterial) { name = key };
            AssetDatabase.CreateAsset(variant, path);
        }
        else
        {
            variant.CopyPropertiesFromMaterial(baseMaterial);
        }

        variant.SetTextureScale("_BaseMap", tiling);

        if (variant.HasProperty("_BumpMap"))
        {
            variant.SetTextureScale("_BumpMap", tiling);
        }

        EditorUtility.SetDirty(variant);
        variantCache[key] = variant;
        return variant;
    }

    private static Material LoadBase(string baseName)
    {
        return AssetDatabase.LoadAssetAtPath<Material>(
            "Assets/Materials/Museum/" + baseName + ".mat");
    }

    /// <summary>
    /// Lets the pass be re-run: a renderer already carrying "MuseumMarble_u12_v8"
    /// is resolved back to "MuseumMarble" and re-measured, rather than being
    /// treated as an unknown material and skipped.
    /// </summary>
    private static string StripVariantSuffix(string name)
    {
        int i = name.IndexOf("_u");
        return i > 0 ? name.Substring(0, i) : name;
    }
}
