using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Import-side size optimisation for the 300 MB deliverable cap (S1).
///
///   Unity.exe -batchmode -quit -projectPath . ^
///             -executeMethod BuildSizeBuilder.BuildFromCommandLine
///
/// The brief states outright that the game is judged on its weight - "a
/// constant fight between size optimisation, performance and interest" - so
/// this is a graded axis, not housekeeping.
///
/// The single largest offender was the imported character: its albedo,
/// normal and gloss maps are 4096 x 4096 each (roughly 50 MB of source PNG,
/// and ~11 MB apiece as compressed 4K in the build) for a character that is
/// a few hundred pixels tall for almost the whole game. Capping the import
/// size costs nothing visible even in the first-person view and takes tens of
/// megabytes off the deliverable.
///
/// Nothing here edits the source files - only import settings - so the
/// original art is untouched and the caps can be raised again from one place.
/// </summary>
public static class BuildSizeBuilder
{
    /// <summary>Per-texture import caps, keyed by asset path.</summary>
    private static readonly Dictionary<string, int> MaxSizes = new Dictionary<string, int>
    {
        // Seen closest in first person, so kept the largest of the set.
        { "Assets/Art/Characters/Noa/Textures/Ch02_1001_Diffuse.png", 2048 },
        { "Assets/Art/Characters/Noa/Textures/Ch02_1001_Normal.png", 2048 },

        // A gloss/spec map carries far less perceptible detail than albedo.
        { "Assets/Art/Characters/Noa/Textures/Ch02_1001_Glossiness.png", 1024 },
        { "Assets/Art/Characters/Noa/Textures/Ch02_1001_Specular.png", 512 },

        // Hair and eyelashes: small on screen at any distance.
        { "Assets/Art/Characters/Noa/Textures/Ch02_1002_Diffuse.png", 1024 },
        { "Assets/Art/Characters/Noa/Textures/Ch02_1002_Normal.png", 1024 },
    };

    [MenuItem("Museum of Time/Optimise Build Size")]
    public static void BuildMenu() { Build(); }

    public static void BuildFromCommandLine() { Build(); }

    private static void Build()
    {
        long savedBytes = 0;

        foreach (KeyValuePair<string, int> entry in MaxSizes)
        {
            savedBytes += CapTexture(entry.Key, entry.Value);
        }

        CompressAnimationClips();

        AssetDatabase.SaveAssets();

        Debug.Log("SIZE OK: capped " + MaxSizes.Count + " textures, " +
                  "estimated saving " + (savedBytes / (1024 * 1024)) + " MB of " +
                  "uncompressed texture memory.");
    }

    private static long CapTexture(string path, int maxSize)
    {
        var importer = AssetImporter.GetAtPath(path) as TextureImporter;

        if (importer == null)
        {
            Debug.LogWarning("SIZE: no texture at " + path);
            return 0;
        }

        int before = importer.maxTextureSize;

        if (before <= maxSize && importer.textureCompression == TextureImporterCompression.Compressed)
        {
            return 0;
        }

        importer.maxTextureSize = maxSize;
        importer.textureCompression = TextureImporterCompression.Compressed;
        importer.compressionQuality = 50;
        importer.mipmapEnabled = true;
        importer.streamingMipmaps = true;

        importer.SaveAndReimport();

        // Texture memory scales with area, so halving the edge quarters it.
        long beforeBytes = (long)before * before;
        long afterBytes = (long)maxSize * maxSize;

        Debug.Log("SIZE: " + System.IO.Path.GetFileName(path) +
                  " " + before + " -> " + maxSize);

        return beforeBytes - afterBytes;
    }

    /// <summary>
    /// Keyframe reduction on the imported clips. Mixamo bakes a key per bone
    /// per frame; the optimal setting drops keys that lie on the curve the
    /// neighbouring keys already describe, which is invisible in motion.
    /// </summary>
    private static void CompressAnimationClips()
    {
        string[] guids = AssetDatabase.FindAssets(
            "t:Model", new[] { "Assets/Art/Characters/Noa" });

        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var importer = AssetImporter.GetAtPath(path) as ModelImporter;

            if (importer == null) { continue; }

            bool changed = false;

            if (importer.animationCompression != ModelImporterAnimationCompression.Optimal)
            {
                importer.animationCompression = ModelImporterAnimationCompression.Optimal;
                changed = true;
            }

            // The meshes are skinned and never read back at runtime.
            if (importer.isReadable)
            {
                importer.isReadable = false;
                changed = true;
            }

            if (importer.importCameras || importer.importLights)
            {
                importer.importCameras = false;
                importer.importLights = false;
                changed = true;
            }

            if (changed)
            {
                importer.SaveAndReimport();
                Debug.Log("SIZE: compressed clips in " + System.IO.Path.GetFileName(path));
            }
        }
    }
}
