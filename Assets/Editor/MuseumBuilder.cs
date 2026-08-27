using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Builds the two-storey museum in MuseumNight: the requirement is a designed
/// building with two floors, textures of our own choosing, and a staircase
/// that can actually be walked up.
///
///   Unity.exe -batchmode -quit -projectPath . ^
///             -executeMethod MuseumBuilder.BuildFromCommandLine
///
/// Everything is placed in metres against Noa's 1.7m, so the scale pass is
/// satisfied by construction rather than corrected afterwards:
///   step rise 0.17, step run 0.30   (a comfortable real staircase)
///   doorways 2.1 high, railing 1.1, ground floor ceiling 5.0
///
/// Idempotent: the Museum root is deleted and rebuilt each run.
/// </summary>
public static class MuseumBuilder
{
    private const string ScenePath = "Assets/Scenes/MuseumNight.unity";
    private const string MaterialFolder = "Assets/Materials/Museum";

    // Building envelope, in metres.
    private const float Width = 30f;      // along X
    private const float Depth = 20f;      // along Z
    private const float FloorHeight = 5f; // ground floor ceiling height
    private const float WallThickness = 0.4f;

    // Staircase, sized like a real one.
    private const float StepRise = 0.17f;
    private const float StepRun = 0.30f;
    private const float StairWidth = 2.0f;

    private static Material marble;
    private static Material wood;
    private static Material plaster;
    private static Material brass;

    [MenuItem("Museum of Time/Build Museum (two storeys)")]
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

        CreateMaterials();

        // Rebuild from scratch so the result never depends on run order.
        GameObject existing = GameObject.Find("Museum");
        if (existing != null)
        {
            Object.DestroyImmediate(existing);
        }

        var museum = new GameObject("Museum");

        GameObject architecture = GameObject.Find("Architecture");
        if (architecture != null)
        {
            museum.transform.SetParent(architecture.transform, false);
        }

        BuildGroundFloor(museum.transform);
        BuildPerimeterWalls(museum.transform);
        BuildUpperFloor(museum.transform);
        BuildStaircase(museum.transform);
        BuildMezzanineRailing(museum.transform);
        BuildInteriorWalls(museum.transform);

        int steps = Mathf.CeilToInt(FloorHeight / StepRise);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);

        Debug.Log(
            "MUSEUM OK: " + Width + "x" + Depth + "m, two floors, " +
            "upper slab at y=" + FloorHeight + ", staircase of " + steps +
            " steps at " + StepRise + "m rise / " + StepRun + "m run.");
    }

    // -----------------------------------------------------------------
    // Materials. Textures are generated here rather than downloaded, so
    // they are genuinely ours and the repository stays small.
    // -----------------------------------------------------------------

    private static void CreateMaterials()
    {
        Directory.CreateDirectory(MaterialFolder);

        marble = MakeMaterial(
            "MuseumMarble",
            Marble(new Color(0.90f, 0.89f, 0.86f), new Color(0.55f, 0.56f, 0.58f)),
            new Vector2(6f, 4f),
            smoothness: 0.65f);

        wood = MakeMaterial(
            "MuseumWood",
            Planks(new Color(0.36f, 0.24f, 0.15f), new Color(0.28f, 0.18f, 0.11f)),
            new Vector2(4f, 2f),
            smoothness: 0.35f);

        plaster = MakeMaterial(
            "MuseumPlaster",
            Noise(new Color(0.80f, 0.78f, 0.74f), 0.06f),
            new Vector2(4f, 2f),
            smoothness: 0.1f);

        brass = MakeMaterial(
            "MuseumBrass",
            Noise(new Color(0.72f, 0.56f, 0.24f), 0.03f),
            Vector2.one,
            smoothness: 0.85f,
            metallic: 0.9f);
    }

    private static Material MakeMaterial(
        string name,
        Texture2D texture,
        Vector2 tiling,
        float smoothness,
        float metallic = 0f)
    {
        string texPath = MaterialFolder + "/" + name + ".png";
        File.WriteAllBytes(texPath, texture.EncodeToPNG());
        Object.DestroyImmediate(texture);
        AssetDatabase.ImportAsset(texPath);

        var importer = (TextureImporter)AssetImporter.GetAtPath(texPath);
        importer.wrapMode = TextureWrapMode.Repeat;
        importer.filterMode = FilterMode.Bilinear;
        // 1024 is plenty for a greybox and keeps the build inside its budget.
        importer.maxTextureSize = 1024;
        importer.SaveAndReimport();

        Texture2D imported = AssetDatabase.LoadAssetAtPath<Texture2D>(texPath);

        string matPath = MaterialFolder + "/" + name + ".mat";
        var material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        material.SetTexture("_BaseMap", imported);
        material.SetTextureScale("_BaseMap", tiling);
        material.SetFloat("_Smoothness", smoothness);
        material.SetFloat("_Metallic", metallic);

        AssetDatabase.DeleteAsset(matPath);
        AssetDatabase.CreateAsset(material, matPath);

        return AssetDatabase.LoadAssetAtPath<Material>(matPath);
    }

    /// <summary>
    /// Classic turbulence-marble: layered Perlin noise warps a sine-wave
    /// banding pattern into organic veins, instead of the flat checkerboard
    /// this used to be (which read as an unfinished placeholder, not stone).
    /// </summary>
    private static Texture2D Marble(Color baseColor, Color veinColor)
    {
        const int size = 256;
        var tex = new Texture2D(size, size);

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float turbulence = 0f;
                float freq = 0.015f;
                float amp = 1f;

                for (int octave = 0; octave < 4; octave++)
                {
                    turbulence += Mathf.PerlinNoise(x * freq, y * freq) * amp;
                    freq *= 2.1f;
                    amp *= 0.5f;
                }

                float vein = Mathf.Sin((x + y) * 0.045f + turbulence * 6f);
                float t = Mathf.Clamp01(Mathf.Abs(vein));
                // Sharpen so veins read as thin lines rather than a broad blend.
                t = Mathf.Pow(t, 4f);

                Color c = Color.Lerp(veinColor, baseColor, t);

                float grain = Mathf.PerlinNoise(x * 0.2f, y * 0.2f) * 0.04f;
                tex.SetPixel(x, y, c * (0.98f + grain));
            }
        }

        tex.Apply();
        return tex;
    }

    private static Texture2D Planks(Color a, Color b)
    {
        const int size = 256;
        var tex = new Texture2D(size, size);

        for (int y = 0; y < size; y++)
        {
            int plank = y / 32;
            Color baseColor = plank % 2 == 0 ? a : b;

            for (int x = 0; x < size; x++)
            {
                // Grain runs along the plank, plus a dark seam between them.
                float grain = Mathf.PerlinNoise(x * 0.05f, plank * 3.7f) * 0.18f;
                Color c = baseColor * (0.9f + grain);

                if (y % 32 == 0)
                {
                    c *= 0.55f;
                }

                tex.SetPixel(x, y, c);
            }
        }

        tex.Apply();
        return tex;
    }

    private static Texture2D Noise(Color baseColor, float strength)
    {
        const int size = 256;
        var tex = new Texture2D(size, size);

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float n = Mathf.PerlinNoise(x * 0.12f, y * 0.12f) * strength;
                tex.SetPixel(x, y, baseColor * (1f - (strength * 0.5f) + n));
            }
        }

        tex.Apply();
        return tex;
    }

    // -----------------------------------------------------------------
    // Geometry
    // -----------------------------------------------------------------

    private static GameObject Box(
        Transform parent,
        string name,
        Vector3 centre,
        Vector3 size,
        Material material)
    {
        GameObject box = GameObject.CreatePrimitive(PrimitiveType.Cube);
        box.name = name;
        box.transform.SetParent(parent, false);
        box.transform.localPosition = centre;
        box.transform.localScale = size;
        box.GetComponent<MeshRenderer>().sharedMaterial = material;
        return box;
    }

    private static void BuildGroundFloor(Transform root)
    {
        Box(root, "GroundFloorSlab",
            new Vector3(0f, -0.1f, 0f),
            new Vector3(Width, 0.2f, Depth),
            marble);
    }

    private static void BuildPerimeterWalls(Transform root)
    {
        float h = FloorHeight * 2f;      // walls rise past the upper floor
        float y = h / 2f;

        Box(root, "WallNorth",
            new Vector3(0f, y, Depth / 2f),
            new Vector3(Width, h, WallThickness), plaster);

        Box(root, "WallSouthLeft",
            new Vector3(-Width / 4f - 1f, y, -Depth / 2f),
            new Vector3(Width / 2f - 2f, h, WallThickness), plaster);

        Box(root, "WallSouthRight",
            new Vector3(Width / 4f + 1f, y, -Depth / 2f),
            new Vector3(Width / 2f - 2f, h, WallThickness), plaster);

        // Lintel over the entrance leaves a 2.1m doorway, as a real one would.
        Box(root, "EntranceLintel",
            new Vector3(0f, 2.1f + ((h - 2.1f) / 2f), -Depth / 2f),
            new Vector3(4f, h - 2.1f, WallThickness), plaster);

        Box(root, "WallWest",
            new Vector3(-Width / 2f, y, 0f),
            new Vector3(WallThickness, h, Depth), plaster);

        Box(root, "WallEast",
            new Vector3(Width / 2f, y, 0f),
            new Vector3(WallThickness, h, Depth), plaster);
    }

    /// <summary>
    /// The upper slab covers the eastern half only. The western half stays
    /// open so the mezzanine looks down into the main hall, which is what
    /// makes the two floors read as one space.
    /// </summary>
    private static void BuildUpperFloor(Transform root)
    {
        Box(root, "UpperFloorSlab",
            new Vector3(Width / 4f, FloorHeight, 0f),
            new Vector3(Width / 2f, 0.2f, Depth),
            wood);
    }

    private static void BuildStaircase(Transform root)
    {
        var stairs = new GameObject("Staircase");
        stairs.transform.SetParent(root, false);

        int stepCount = Mathf.CeilToInt(FloorHeight / StepRise);

        // Runs north along the west wall, arriving at the mezzanine edge.
        float startZ = -Depth / 2f + 2f;
        float x = -Width / 2f + (StairWidth / 2f) + WallThickness;

        for (int i = 0; i < stepCount; i++)
        {
            float y = (i + 1) * StepRise;
            float z = startZ + (i * StepRun);

            GameObject step = Box(stairs.transform, "Step" + i.ToString("00"),
                new Vector3(x, y - (StepRise / 2f), z),
                new Vector3(StairWidth, StepRise, StepRun),
                marble);

            // The treads are decoration only. A CharacterController's step-up
            // is unreliable when the same Move also carries gravity downward,
            // so leaving colliders here means Noa jams against the first tread
            // instead of climbing. The ramp below is the real walking surface.
            Object.DestroyImmediate(step.GetComponent<Collider>());
        }

        // The walkway below already covers this ground, so a separate landing
        // box only produced two coplanar top faces fighting each other.
        float landingZ = startZ + (stepCount * StepRun) + 1f;

        // A solid invisible wedge under the treads.
        //
        // Two earlier attempts failed and are worth recording. Leaving
        // colliders on the individual treads jams Noa against the first one:
        // a CharacterController's step-up is unreliable when the same Move
        // also carries gravity downward. Replacing them with a thin sloped
        // plate failed differently - a thin plate has an underside, and she
        // simply walked beneath it. A thick wedge whose top face is the
        // staircase and whose body reaches below the floor has neither
        // problem: it is a plain 29 degree slope, well inside the
        // controller's 50 degree slope limit, so no step-up is needed.
        float run = stepCount * StepRun;
        float slope = Mathf.Atan2(FloorHeight, run) * Mathf.Rad2Deg;
        const float wedgeThickness = 4f;

        float rampLength = Mathf.Sqrt((run * run) + (FloorHeight * FloorHeight));

        GameObject ramp = Box(stairs.transform, "StairRamp",
            Vector3.zero,
            new Vector3(StairWidth, wedgeThickness, rampLength),
            marble);

        ramp.transform.localRotation = Quaternion.Euler(-slope, 0f, 0f);

        // Midpoint of the walking surface, then pushed down by half the
        // wedge so that surface ends up on top.
        Vector3 surfaceMid = new Vector3(x, FloorHeight / 2f, startZ + (run / 2f));
        ramp.transform.localPosition =
            surfaceMid - (ramp.transform.up * (wedgeThickness / 2f));

        ramp.GetComponent<MeshRenderer>().enabled = false;

        Debug.Log("Stair ramp slope: " + slope.ToString("0.0") +
                  " degrees, surface from y=0 to y=" + FloorHeight);

        // A walkway joining the landing to the upper slab. It has to start at
        // the stairs' own x, not at the centre of the building: the first
        // version spanned only the middle of the plan, so Noa climbed the
        // stairs, ran out of floor, and fell straight back to the ground.
        // Stop at the upper slab's west edge rather than running under it.
        // Overlapping the slab put two floor surfaces in the same plane.
        float walkwayWest = x - (StairWidth / 2f);
        float walkwayEast = 0f;
        float walkwayWidth = walkwayEast - walkwayWest;

        Box(stairs.transform, "Walkway",
            new Vector3(walkwayWest + (walkwayWidth / 2f),
                        FloorHeight,
                        landingZ),
            new Vector3(walkwayWidth, 0.2f, 3f),
            wood);
    }

    private static void BuildMezzanineRailing(Transform root)
    {
        var railing = new GameObject("MezzanineRailing");
        railing.transform.SetParent(root, false);

        // 1.1m is the usual real-world guard height.
        const float railHeight = 1.1f;

        Box(railing.transform, "RailEdge",
            new Vector3(0f, FloorHeight + (railHeight / 2f), 0f),
            new Vector3(0.1f, railHeight, Depth),
            brass);
    }

    private static void BuildInteriorWalls(Transform root)
    {
        var interior = new GameObject("InteriorWalls");
        interior.transform.SetParent(root, false);

        // Curator's office on the upper floor, in the north-east corner.
        Box(interior.transform, "OfficeWallSouth",
            new Vector3(Width / 4f + 2f, FloorHeight + 1.5f, 2f),
            new Vector3(Width / 4f, 3f, WallThickness),
            wood);

        Box(interior.transform, "OfficeWallWest",
            new Vector3(Width / 4f - 3f, FloorHeight + 1.5f, 5f),
            new Vector3(WallThickness, 3f, 6f),
            wood);

        // The Clock of Creation chamber, ground floor, north wall.
        // Ends 0.3m short of the north wall's inner face (z = 9.8). Running
        // it right into the wall put two coplanar faces inside each other.
        Box(interior.transform, "ClockChamberWest",
            new Vector3(-4f, 2.5f, 5.85f),
            new Vector3(WallThickness, 5f, 7.5f),
            plaster);
    }
}
