using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Materials and particle look: normal maps on the museum surfaces, and a
/// real sprite on the particle material.
///
///   Unity.exe -batchmode -quit -projectPath . ^
///             -executeMethod SurfaceAndVfxLookBuilder.BuildFromCommandLine
///
/// Two defects this fixes, both found by rendering the game and looking at it:
///
///   1. Every ParticleSystem shared a material with NO texture. URP's
///      unlit particle shader with no _BaseMap draws each particle as a
///      fully opaque white QUAD, so the Chronological Shadow's drift effect
///      rendered as a cluster of solid white boxes around the player rather
///      than a soft haze. A radial sprite with an alpha falloff is what that
///      shader expects.
///
///   2. The particle material was built with `new Material(shader)` and never
///      saved as an asset, so every scene serialised its own private copy and
///      no two could be fixed at once. It is a real asset now.
///
///   3. The museum materials had albedo but no normal maps. That is
///      survivable under flat ambient, but once real directional light and
///      pooled spots are in play, a surface with no normal detail reads as
///      painted cardboard.
/// </summary>
public static class SurfaceAndVfxLookBuilder
{
    private const string ParticleTexturePath = "Assets/VFX/ParticleSoft.png";
    private const string ParticleMaterialPath = "Assets/VFX/VfxParticleSoft.mat";

    private static readonly string[] ScenePaths =
    {
        "Assets/Scenes/MuseumNight.unity",
        "Assets/Scenes/FrozenCity.unity",
        "Assets/Scenes/ClockCore.unity",
        "Assets/Scenes/MainMenu.unity",
        "Assets/Scenes/Victory.unity",
    };

    [MenuItem("Museum of Time/Build Surface and VFX Look")]
    public static void BuildMenu() { Build(); }

    public static void BuildFromCommandLine() { Build(); }

    private static void Build()
    {
        ConfigureNormalMapImports();
        ApplyMuseumNormalMaps();

        Material particleMat = BuildParticleMaterial();

        foreach (string scenePath in ScenePaths)
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(scenePath) == null) { continue; }

            Scene scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            int fixedCount = ApplyParticleMaterial(particleMat);
            WireRuntimeVfxMaterial(particleMat);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);

            Debug.Log("SURFACE OK: " + scene.name + " (" + fixedCount + " particle renderers)");
        }

        ApplyParticleMaterialToPrefabs(particleMat);

        AssetDatabase.SaveAssets();
        Debug.Log("=== SURFACE AND VFX LOOK COMPLETE ===");
    }

    // ------------------------------------------------------------------
    // Normal maps
    // ------------------------------------------------------------------

    /// <summary>
    /// A normal map imported as a plain colour texture is sampled as if it
    /// were albedo and produces garbage lighting, so the importer has to be
    /// told what these are before anything references them.
    /// </summary>
    private static void ConfigureNormalMapImports()
    {
        string[] names = { "MuseumMarble", "MuseumWood", "MuseumPlaster", "MuseumBrass" };

        foreach (string name in names)
        {
            string path = "Assets/Materials/Museum/" + name + "_Normal.png";
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) { continue; }

            if (importer.textureType != TextureImporterType.NormalMap)
            {
                importer.textureType = TextureImporterType.NormalMap;
                importer.wrapMode = TextureWrapMode.Repeat;
                importer.filterMode = FilterMode.Trilinear;
                importer.anisoLevel = 4;
                importer.SaveAndReimport();
                Debug.Log("SURFACE: imported " + name + "_Normal as a NormalMap.");
            }
        }

        // The albedos want trilinear + aniso too, or they shimmer badly at the
        // grazing angles a first-person camera spends most of its time at.
        foreach (string name in names)
        {
            string path = "Assets/Materials/Museum/" + name + ".png";
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) { continue; }

            if (importer.filterMode != FilterMode.Trilinear || importer.anisoLevel < 4)
            {
                importer.filterMode = FilterMode.Trilinear;
                importer.anisoLevel = 8;
                importer.wrapMode = TextureWrapMode.Repeat;
                importer.SaveAndReimport();
            }
        }
    }

    private static void ApplyMuseumNormalMaps()
    {
        ApplyNormal("MuseumMarble", 0.6f, 0.62f, 0f);
        ApplyNormal("MuseumWood", 1.0f, 0.32f, 0f);
        ApplyNormal("MuseumPlaster", 1.1f, 0.08f, 0f);
        ApplyNormal("MuseumBrass", 0.5f, 0.86f, 1f);
    }

    private static void ApplyNormal(string name, float scale, float smoothness, float metallic)
    {
        string matPath = "Assets/Materials/Museum/" + name + ".mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
        if (mat == null) { return; }

        var normal = AssetDatabase.LoadAssetAtPath<Texture>(
            "Assets/Materials/Museum/" + name + "_Normal.png");

        if (normal != null && mat.HasProperty("_BumpMap"))
        {
            mat.SetTexture("_BumpMap", normal);
            mat.SetTextureScale("_BumpMap", mat.GetTextureScale("_BaseMap"));

            if (mat.HasProperty("_BumpScale"))
            {
                mat.SetFloat("_BumpScale", scale);
            }

            // URP's Lit shader only samples _BumpMap when this keyword is on.
            // Setting the texture alone is silently ignored.
            mat.EnableKeyword("_NORMALMAP");
        }

        if (mat.HasProperty("_Smoothness")) { mat.SetFloat("_Smoothness", smoothness); }
        if (mat.HasProperty("_Metallic")) { mat.SetFloat("_Metallic", metallic); }

        EditorUtility.SetDirty(mat);
        Debug.Log("SURFACE: normal-mapped " + name);
    }

    // ------------------------------------------------------------------
    // Particles
    // ------------------------------------------------------------------

    private static Material BuildParticleMaterial()
    {
        var importer = AssetImporter.GetAtPath(ParticleTexturePath) as TextureImporter;
        if (importer != null && importer.alphaIsTransparency == false)
        {
            importer.alphaIsTransparency = true;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.SaveAndReimport();
        }

        var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(ParticleTexturePath);

        var mat = AssetDatabase.LoadAssetAtPath<Material>(ParticleMaterialPath);
        if (mat == null)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            if (shader == null) { shader = Shader.Find("Universal Render Pipeline/Unlit"); }

            mat = new Material(shader) { name = "VfxParticleSoft" };
            AssetDatabase.CreateAsset(mat, ParticleMaterialPath);
        }

        if (tex != null && mat.HasProperty("_BaseMap"))
        {
            mat.SetTexture("_BaseMap", tex);
        }

        // Additive: particles are light, not paint. With alpha blending the
        // soft sprite still reads as a grey smudge over dark interiors.
        mat.SetFloat("_Surface", 1f);   // Transparent
        mat.SetFloat("_Blend", 1f);     // Additive
        mat.SetFloat("_ZWrite", 0f);
        mat.SetFloat("_AlphaClip", 0f);
        mat.SetColor("_BaseColor", Color.white);

        mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        mat.EnableKeyword("_ALPHAPREMULTIPLY_ON");
        mat.DisableKeyword("_ALPHATEST_ON");

        mat.SetOverrideTag("RenderType", "Transparent");
        mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;

        EditorUtility.SetDirty(mat);
        return mat;
    }

    /// <summary>
    /// GameplayVfx creates its bursts at RUNTIME, so they never pass through
    /// ApplyParticleMaterial above. Handing it the authored material is what
    /// stops those bursts falling back to a textureless white quad.
    /// </summary>
    private static void WireRuntimeVfxMaterial(Material mat)
    {
        var vfx = Object.FindObjectsByType<GameplayVfx>(
            FindObjectsInactive.Include, FindObjectsSortMode.None);

        foreach (GameplayVfx v in vfx)
        {
            var so = new SerializedObject(v);
            SerializedProperty p = so.FindProperty("particleMaterialAsset");

            if (p != null)
            {
                p.objectReferenceValue = mat;
                so.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(v);
            }
        }
    }

    private static int ApplyParticleMaterial(Material mat)
    {
        var renderers = Object.FindObjectsByType<ParticleSystemRenderer>(
            FindObjectsInactive.Include, FindObjectsSortMode.None);

        int count = 0;

        foreach (ParticleSystemRenderer r in renderers)
        {
            r.sharedMaterial = mat;
            r.renderMode = ParticleSystemRenderMode.Billboard;
            r.alignment = ParticleSystemRenderSpace.View;
            EditorUtility.SetDirty(r);
            count++;
        }

        return count;
    }

    private static void ApplyParticleMaterialToPrefabs(Material mat)
    {
        string[] guids = AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Prefabs" });

        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            GameObject root = PrefabUtility.LoadPrefabContents(path);

            var renderers = root.GetComponentsInChildren<ParticleSystemRenderer>(true);
            if (renderers.Length > 0)
            {
                foreach (ParticleSystemRenderer r in renderers)
                {
                    r.sharedMaterial = mat;
                    r.renderMode = ParticleSystemRenderMode.Billboard;
                }

                PrefabUtility.SaveAsPrefabAsset(root, path);
                Debug.Log("SURFACE: particle material on prefab " + System.IO.Path.GetFileName(path));
            }

            PrefabUtility.UnloadPrefabContents(root);
        }
    }
}
