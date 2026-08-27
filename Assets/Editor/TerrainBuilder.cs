using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Sculpts the FrozenCity terrain. The requirement is a Terrain built by us,
/// so the heightmap is generated here rather than imported: a shallow valley
/// holding the city, with raised outskirts framing the clock tower so it is
/// visible from the spawn point.
///
///   Unity.exe -batchmode -quit -projectPath . ^
///             -executeMethod TerrainBuilder.BuildFromCommandLine
///
/// Resolution is deliberately 513, not 2049. The brief judges the build on
/// its weight, and heightmap data grows with the square of this number.
/// </summary>
public static class TerrainBuilder
{
    private const string ScenePath = "Assets/Scenes/FrozenCity.unity";
    private const string AssetFolder = "Assets/Terrain";

    private const int HeightmapResolution = 513;
    private const float TerrainSize = 200f;
    private const float TerrainHeight = 40f;

    [MenuItem("Museum of Time/Build FrozenCity Terrain")]
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
        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

        Directory.CreateDirectory(AssetFolder);

        TerrainData data = CreateTerrainData();

        SculptHeights(data);

        // Commit the sculpt to disk BEFORE painting, and this ordering is
        // load-bearing rather than tidiness.
        //
        // CreateTerrainData deletes and recreates the .asset, so what comes
        // back is a fresh, FLAT TerrainData and SetHeights only changes it in
        // memory. PaintLayers then writes three PNGs and calls
        // TextureImporter.SaveAndReimport, which runs an AssetDatabase import
        // - and that reloads the terrain asset from its still-flat on-disk
        // state, throwing the sculpt away.
        //
        // The result was a terrain with no relief at all (T6 asks for a
        // sculpted one), and only on the SECOND rebuild in a row: the first
        // survived because the previous run's asset was still on disk. It
        // looks identical to a plane in every screenshot and nothing warned.
        EditorUtility.SetDirty(data);
        AssetDatabase.SaveAssets();

        PaintLayers(data);

        EditorUtility.SetDirty(data);
        AssetDatabase.SaveAssets();

        GameObject existing = GameObject.Find("FrozenCityTerrain");
        if (existing != null)
        {
            Object.DestroyImmediate(existing);
        }

        GameObject terrainObject = Terrain.CreateTerrainGameObject(data);
        terrainObject.name = "FrozenCityTerrain";

        // Centre the terrain on the origin so the spawn point sits in the valley.
        terrainObject.transform.position =
            new Vector3(-TerrainSize / 2f, 0f, -TerrainSize / 2f);

        GameObject environment = GameObject.Find("--- ENVIRONMENT --- ");
        if (environment != null)
        {
            terrainObject.transform.SetParent(environment.transform, true);
        }

        BuildClockTower();

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);

        float relief = MeasureRelief(data);

        if (relief < 0.01f)
        {
            Debug.LogError(
                "TERRAIN FLAT: the heightmap has no relief (" + relief.ToString("F4") +
                "). T6 requires a sculpted terrain, and a flat one looks " +
                "identical to a plane in every screenshot.");
        }

        Debug.Log(
            "TERRAIN OK: " + TerrainSize + "x" + TerrainSize + "m, " +
            HeightmapResolution + " heightmap, max height " + TerrainHeight +
            "m, " + data.terrainLayers.Length + " paint layers, relief " +
            relief.ToString("F3") + ".");
    }

    /// <summary>
    /// Peak-to-trough spread of the heightmap, 0 to 1. Zero means a plane.
    /// </summary>
    private static float MeasureRelief(TerrainData data)
    {
        int res = data.heightmapResolution;
        float[,] heights = data.GetHeights(0, 0, res, res);

        float low = 1f;
        float high = 0f;

        for (int y = 0; y < res; y += 4)
        {
            for (int x = 0; x < res; x += 4)
            {
                float h = heights[y, x];
                if (h < low) { low = h; }
                if (h > high) { high = h; }
            }
        }

        return high - low;
    }

    private static TerrainData CreateTerrainData()
    {
        string path = AssetFolder + "/FrozenCityTerrainData.asset";
        AssetDatabase.DeleteAsset(path);

        var data = new TerrainData
        {
            heightmapResolution = HeightmapResolution,
            baseMapResolution = 512,
            alphamapResolution = 512,
        };

        data.SetDetailResolution(256, 16);
        data.size = new Vector3(TerrainSize, TerrainHeight, TerrainSize);

        AssetDatabase.CreateAsset(data, path);
        return AssetDatabase.LoadAssetAtPath<TerrainData>(path);
    }

    /// <summary>
    /// A bowl: low and flat in the middle where the city stands, rising
    /// towards the edges. Perlin noise breaks up the rim so it does not read
    /// as a machined dish.
    /// </summary>
    private static void SculptHeights(TerrainData data)
    {
        int res = data.heightmapResolution;
        var heights = new float[res, res];

        for (int y = 0; y < res; y++)
        {
            for (int x = 0; x < res; x++)
            {
                // Normalised distance from the centre, 0 at the middle.
                float nx = (x / (float)(res - 1)) - 0.5f;
                float ny = (y / (float)(res - 1)) - 0.5f;
                float distance = Mathf.Sqrt((nx * nx) + (ny * ny)) / 0.5f;

                // Flat floor out to 35% of the radius, then rising outskirts.
                float bowl = Mathf.SmoothStep(0f, 1f,
                    Mathf.InverseLerp(0.35f, 1.0f, distance));

                float ridges = Mathf.PerlinNoise(x * 0.02f, y * 0.02f) * 0.35f;
                float detail = Mathf.PerlinNoise(x * 0.09f, y * 0.09f) * 0.06f;

                // The valley floor keeps a little relief so it is not a plane.
                float height = (bowl * (0.45f + ridges)) + (detail * bowl) +
                               (detail * 0.4f);

                heights[y, x] = Mathf.Clamp01(height);
            }
        }

        data.SetHeights(0, 0, heights);
    }

    /// <summary>
    /// Three layers, chosen by height: cobbled streets on the valley floor,
    /// frozen dirt on the slopes, snow on the heights.
    /// </summary>
    private static void PaintLayers(TerrainData data)
    {
        TerrainLayer cobble = MakeLayer(
            "FrozenCobble",
            Cobbles(new Color(0.38f, 0.38f, 0.40f), new Color(0.29f, 0.29f, 0.32f)),
            4f);

        TerrainLayer dirt = MakeLayer(
            "FrozenDirt",
            NoiseTexture(new Color(0.34f, 0.30f, 0.26f), 0.10f),
            8f);

        TerrainLayer snow = MakeLayer(
            "Snow",
            NoiseTexture(new Color(0.92f, 0.94f, 0.97f), 0.05f),
            10f);

        data.terrainLayers = new[] { cobble, dirt, snow };

        int res = data.alphamapResolution;
        var map = new float[res, res, 3];

        for (int y = 0; y < res; y++)
        {
            for (int x = 0; x < res; x++)
            {
                // Sample the sculpted height at this alphamap texel.
                float h = data.GetInterpolatedHeight(
                    x / (float)(res - 1),
                    y / (float)(res - 1)) / TerrainHeight;

                float cobbleWeight = 1f - Mathf.InverseLerp(0.02f, 0.12f, h);
                float snowWeight = Mathf.InverseLerp(0.30f, 0.55f, h);
                float dirtWeight = 1f - cobbleWeight - snowWeight;

                dirtWeight = Mathf.Max(dirtWeight, 0.001f);

                float total = cobbleWeight + dirtWeight + snowWeight;

                map[y, x, 0] = cobbleWeight / total;
                map[y, x, 1] = dirtWeight / total;
                map[y, x, 2] = snowWeight / total;
            }
        }

        data.SetAlphamaps(0, 0, map);
    }

    private static TerrainLayer MakeLayer(string name, Texture2D texture, float tileSize)
    {
        string texPath = AssetFolder + "/" + name + ".png";
        File.WriteAllBytes(texPath, texture.EncodeToPNG());
        Object.DestroyImmediate(texture);
        AssetDatabase.ImportAsset(texPath);

        var importer = (TextureImporter)AssetImporter.GetAtPath(texPath);
        importer.wrapMode = TextureWrapMode.Repeat;
        importer.maxTextureSize = 512;
        importer.SaveAndReimport();

        string layerPath = AssetFolder + "/" + name + ".terrainlayer";
        AssetDatabase.DeleteAsset(layerPath);

        var layer = new TerrainLayer
        {
            diffuseTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(texPath),
            tileSize = new Vector2(tileSize, tileSize),
        };

        AssetDatabase.CreateAsset(layer, layerPath);
        return AssetDatabase.LoadAssetAtPath<TerrainLayer>(layerPath);
    }

    private static Texture2D Cobbles(Color stone, Color mortar)
    {
        const int size = 256;
        var tex = new Texture2D(size, size);

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                // Offset every other row so the stones interlock.
                int row = y / 16;
                int offset = (row % 2) * 8;
                bool seam = ((x + offset) % 16 < 2) || (y % 16 < 2);

                float grain = Mathf.PerlinNoise(x * 0.15f, y * 0.15f) * 0.12f;
                tex.SetPixel(x, y, (seam ? mortar : stone) * (0.94f + grain));
            }
        }

        tex.Apply();
        return tex;
    }

    private static Texture2D NoiseTexture(Color baseColor, float strength)
    {
        const int size = 256;
        var tex = new Texture2D(size, size);

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float n = Mathf.PerlinNoise(x * 0.10f, y * 0.10f) * strength;
                float fine = Mathf.PerlinNoise(x * 0.4f, y * 0.4f) * (strength * 0.5f);
                tex.SetPixel(x, y, baseColor * (0.95f + n + fine));
            }
        }

        tex.Apply();
        return tex;
    }

    /// <summary>
    /// A placeholder clock tower so the valley has the landmark the scene is
    /// built around. Step 6.2 turns it into the bell puzzle.
    /// </summary>
    private static void BuildClockTower()
    {
        GameObject existing = GameObject.Find("ClockTower");
        if (existing != null)
        {
            Object.DestroyImmediate(existing);
        }

        var tower = new GameObject("ClockTower");
        tower.transform.position = new Vector3(0f, 0f, 35f);

        Material stone = AssetDatabase.LoadAssetAtPath<Material>(
            "Assets/Materials/Museum/MuseumPlaster.mat");

        GameObject shaft = GameObject.CreatePrimitive(PrimitiveType.Cube);
        shaft.name = "Shaft";
        shaft.transform.SetParent(tower.transform, false);
        shaft.transform.localPosition = new Vector3(0f, 12f, 0f);
        shaft.transform.localScale = new Vector3(6f, 24f, 6f);
        shaft.GetComponent<MeshRenderer>().sharedMaterial = stone;

        GameObject belfry = GameObject.CreatePrimitive(PrimitiveType.Cube);
        belfry.name = "Belfry";
        belfry.transform.SetParent(tower.transform, false);
        belfry.transform.localPosition = new Vector3(0f, 26f, 0f);
        belfry.transform.localScale = new Vector3(8f, 4f, 8f);
        belfry.GetComponent<MeshRenderer>().sharedMaterial = stone;
    }
}
