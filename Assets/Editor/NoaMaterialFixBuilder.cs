using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Fixes Noa rendering nearly gray: the Mixamo FBX embeds real diffuse/normal
/// textures (JPEG/PNG data inside the .fbx, confirmed by inspecting the raw
/// file), but Unity's default FBX import never extracted them or wired them
/// into the two generated materials' Base Map slots, so both materials
/// (Ch02_body, Ch02_hair - shared by all 6 SkinnedMeshRenderers) rendered
/// with URP Lit's default grey base colour and no texture.
///
/// This extracts the embedded textures to real texture assets, clones the
/// two embedded materials into real external .mat assets, explicitly wires
/// Base Map (+ Normal Map where present) on a URP Lit shader, then remaps
/// the model's internal material references to the fixed external materials
/// via ModelImporter.AddRemap - not a flat recolour, the actual per-pixel
/// skin/hair/clothing/shoe texture atlases baked into the Mixamo download.
///
///   Unity.exe -batchmode -quit -projectPath . ^
///             -executeMethod NoaMaterialFixBuilder.BuildFromCommandLine
/// </summary>
public static class NoaMaterialFixBuilder
{
    private const string ModelPath = "Assets/Art/Characters/Noa/Model/Idle.fbx";
    private const string TexturesDir = "Assets/Art/Characters/Noa/Textures";
    private const string MaterialsDir = "Assets/Art/Characters/Noa/Materials";

    [MenuItem("Museum of Time/Fix Noa Materials")]
    public static void BuildMenu() { Build(); }

    public static void BuildFromCommandLine() { Build(); }

    private static void Build()
    {
        var importer = AssetImporter.GetAtPath(ModelPath) as ModelImporter;
        if (importer == null)
        {
            Debug.LogError("NOAMAT FAILED: no ModelImporter at " + ModelPath);
            return;
        }

        Directory.CreateDirectory(TexturesDir);
        Directory.CreateDirectory(MaterialsDir);

        bool extracted = importer.ExtractTextures(TexturesDir);
        AssetDatabase.Refresh();
        importer.SaveAndReimport();
        AssetDatabase.Refresh();

        if (!extracted)
        {
            Debug.LogWarning("NOAMAT: ExtractTextures reported no textures were extracted " +
                              "(they may already have been extracted by a previous run).");
        }

        SetNormalMapType();

        int remapped = RemapMaterials(importer);

        importer.SaveAndReimport();
        AssetDatabase.SaveAssets();

        Debug.Log("NOAMAT OK: textures under " + TexturesDir + ", " + remapped +
                   " material(s) fixed (Base Map + Normal Map on URP Lit) and remapped.");
    }

    /// <summary>Marks every extracted "*_Normal" texture as a Normal Map import type.</summary>
    private static void SetNormalMapType()
    {
        foreach (string guid in AssetDatabase.FindAssets("_Normal t:Texture2D", new[] { TexturesDir }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var texImporter = AssetImporter.GetAtPath(path) as TextureImporter;
            if (texImporter == null || texImporter.textureType == TextureImporterType.NormalMap)
            {
                continue;
            }

            texImporter.textureType = TextureImporterType.NormalMap;
            texImporter.SaveAndReimport();
        }
    }

    /// <summary>
    /// Clones each embedded material ("Ch02_body", "Ch02_hair") into a real
    /// external .mat asset with Base Map/Normal Map wired up, then remaps the
    /// model's internal reference to the fixed external material.
    /// </summary>
    private static int RemapMaterials(ModelImporter importer)
    {
        int count = 0;

        foreach (Object o in AssetDatabase.LoadAllAssetsAtPath(ModelPath))
        {
            if (!(o is Material embedded))
            {
                continue;
            }

            string destPath = AssetDatabase.GenerateUniqueAssetPath(MaterialsDir + "/" + embedded.name + ".mat");

            Material existing = AssetDatabase.LoadAssetAtPath<Material>(MaterialsDir + "/" + embedded.name + ".mat");
            Material fixedMat = existing != null ? existing : new Material(embedded);

            if (existing == null)
            {
                AssetDatabase.CreateAsset(fixedMat, destPath);
            }

            ApplyTextures(fixedMat);

            importer.AddRemap(new AssetImporter.SourceAssetIdentifier(embedded), fixedMat);
            count++;
        }

        return count;
    }

    private static void ApplyTextures(Material mat)
    {
        Shader urpLit = Shader.Find("Universal Render Pipeline/Lit");
        if (urpLit != null && mat.shader != urpLit)
        {
            mat.shader = urpLit;
        }

        Texture2D baseMap = LoadTexture(FindTextureFor(mat.name, "_Diffuse"));
        if (baseMap != null)
        {
            mat.SetTexture("_BaseMap", baseMap);
            mat.SetColor("_BaseColor", Color.white);
        }
        else
        {
            Debug.LogWarning("NOAMAT: no diffuse texture found for material '" + mat.name + "' - Base Map left unset.");
        }

        Texture2D normalMap = LoadTexture(FindTextureFor(mat.name, "_Normal"));
        if (normalMap != null)
        {
            mat.SetTexture("_BumpMap", normalMap);
            mat.EnableKeyword("_NORMALMAP");
        }

        EditorUtility.SetDirty(mat);
    }

    /// <summary>
    /// The two Mixamo materials are named after their FBX group id (Ch02_body
    /// -> Ch02_1001_*, Ch02_hair -> Ch02_1002_*); match the extracted texture
    /// whose name contains that group id and the requested map suffix.
    /// </summary>
    private static string FindTextureFor(string matName, string suffix)
    {
        string groupId = matName.IndexOf("hair", System.StringComparison.OrdinalIgnoreCase) >= 0 ? "1002" : "1001";

        foreach (string guid in AssetDatabase.FindAssets("t:Texture2D", new[] { TexturesDir }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            string name = Path.GetFileNameWithoutExtension(path);
            if (name.Contains(groupId) && name.Contains(suffix))
            {
                return path;
            }
        }

        return null;
    }

    private static Texture2D LoadTexture(string path)
    {
        return string.IsNullOrEmpty(path) ? null : AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }
}
