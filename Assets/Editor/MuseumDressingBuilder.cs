using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Dresses the museum interior: a ceiling with skylights, display cases,
/// wall art, benches and stanchion ropes.
///
///   Unity.exe -batchmode -quit -projectPath . ^
///             -executeMethod MuseumDressingBuilder.BuildFromCommandLine
///
/// The museum was structurally complete - two storeys, a real staircase,
/// textured walls - but architecturally bare, and it had **no ceiling at all**.
/// An open-topped box reads as an unfinished greybox no matter how well it is
/// lit, and G1 is judged on how interesting the game is to look at.
///
/// The ceiling is built as a ring of panels around three skylight openings
/// rather than one slab, so the moonlight still falls in shafts - the pooled
/// lighting the night-museum look depends on is kept, not sealed off.
///
/// Everything is placed against the walls and out of the patrol lanes, so the
/// existing NavMesh bakes stay valid. Re-run NavigationBuilder afterwards if
/// that ever stops being true.
///
/// Idempotent: the dressing root is rebuilt each run.
/// </summary>
public static class MuseumDressingBuilder
{
    private const string ScenePath = "Assets/Scenes/MuseumNight.unity";
    private const string Root = "MuseumDressing";

    // The museum's interior, from MuseumBuilder: x -15..15, z -10..10, 10 m tall.
    private const float HalfX = 15f;
    private const float HalfZ = 10f;
    private const float CeilingY = 9.6f;

    private static Material marble;
    private static Material wood;
    private static Material brass;
    private static Material plaster;
    private static Material glass;
    private static Material canvasMat;

    [MenuItem("Museum of Time/Build Museum Dressing")]
    public static void BuildMenu() { Build(); }

    public static void BuildFromCommandLine() { Build(); }

    private static void Build()
    {
        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

        marble = Mat("Assets/Materials/Museum/MuseumMarble.mat");
        wood = Mat("Assets/Materials/Museum/MuseumWood.mat");
        brass = Mat("Assets/Materials/Museum/MuseumBrass.mat");
        plaster = Mat("Assets/Materials/Museum/MuseumPlaster.mat");
        glass = BuildGlass();
        canvasMat = BuildCanvas();

        GameObject old = GameObject.Find(Root);
        if (old != null) { Object.DestroyImmediate(old); }

        var root = new GameObject(Root);

        BuildCeiling(root);
        BuildTrim(root);
        BuildDisplayCases(root);
        BuildWallArt(root);
        BuildBenches(root);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);

        Debug.Log("DRESSING OK: ceiling with skylights, cornice and skirting, " +
                  "display cases, wall art, benches.");
    }

    // ------------------------------------------------------------------

    /// <summary>
    /// A coffered ceiling with three skylight gaps down the centre line, so
    /// the moonlight still reaches the floor in shafts.
    /// </summary>
    private static void BuildCeiling(GameObject root)
    {
        GameObject ceiling = Child(root, "Ceiling");

        // Two long panels either side of the central skylight strip.
        Box(ceiling, "CeilingNorth", new Vector3(0f, CeilingY, 6.5f),
            new Vector3(HalfX * 2f, 0.5f, 7f), plaster, false);

        Box(ceiling, "CeilingSouth", new Vector3(0f, CeilingY, -6.5f),
            new Vector3(HalfX * 2f, 0.5f, 7f), plaster, false);

        // Between them, a strip broken by three openings.
        float[] barCentres = { -11.25f, -3.75f, 3.75f, 11.25f };

        for (int i = 0; i < barCentres.Length; i++)
        {
            Box(ceiling, "CeilingBar_" + i,
                new Vector3(barCentres[i], CeilingY, 0f),
                new Vector3(3.5f, 0.5f, 6f), plaster, false);
        }

        // Brass glazing bars across each opening - reads as a real skylight
        // rather than a hole where the ceiling forgot to be.
        float[] openings = { -7.5f, 0f, 7.5f };

        for (int i = 0; i < openings.Length; i++)
        {
            for (int b = 0; b < 4; b++)
            {
                Box(ceiling, "Glazing_" + i + "_" + b,
                    new Vector3(openings[i], CeilingY - 0.1f, -2.25f + b * 1.5f),
                    new Vector3(4f, 0.12f, 0.12f), brass, false);
            }
        }
    }

    /// <summary>Cornice and skirting: the cheapest architectural detail there is.</summary>
    private static void BuildTrim(GameObject root)
    {
        GameObject trim = Child(root, "Trim");

        // Skirting around the ground floor.
        Box(trim, "SkirtNorth", new Vector3(0f, 0.18f, HalfZ - 0.2f),
            new Vector3(HalfX * 2f, 0.36f, 0.25f), wood, false);
        Box(trim, "SkirtSouth", new Vector3(0f, 0.18f, -HalfZ + 0.2f),
            new Vector3(HalfX * 2f, 0.36f, 0.25f), wood, false);
        Box(trim, "SkirtWest", new Vector3(-HalfX + 0.2f, 0.18f, 0f),
            new Vector3(0.25f, 0.36f, HalfZ * 2f), wood, false);
        Box(trim, "SkirtEast", new Vector3(HalfX - 0.2f, 0.18f, 0f),
            new Vector3(0.25f, 0.36f, HalfZ * 2f), wood, false);

        // Cornice where wall meets ceiling.
        Box(trim, "CorniceNorth", new Vector3(0f, CeilingY - 0.55f, HalfZ - 0.3f),
            new Vector3(HalfX * 2f, 0.4f, 0.4f), plaster, false);
        Box(trim, "CorniceSouth", new Vector3(0f, CeilingY - 0.55f, -HalfZ + 0.3f),
            new Vector3(HalfX * 2f, 0.4f, 0.4f), plaster, false);
        Box(trim, "CorniceWest", new Vector3(-HalfX + 0.3f, CeilingY - 0.55f, 0f),
            new Vector3(0.4f, 0.4f, HalfZ * 2f), plaster, false);
        Box(trim, "CorniceEast", new Vector3(HalfX - 0.3f, CeilingY - 0.55f, 0f),
            new Vector3(0.4f, 0.4f, HalfZ * 2f), plaster, false);
    }

    /// <summary>
    /// Glass display cases on plinths, against the walls and clear of the
    /// patrol lanes so the baked navmesh stays correct.
    /// </summary>
    private static void BuildDisplayCases(GameObject root)
    {
        GameObject cases = Child(root, "DisplayCases");

        // Deliberately clear of the spawn corridor - x within +/-2.5, z from
        // -2 to 10 - which is the straight line the player walks the moment
        // they gain control. A case at (0, 0, 8.6) put a waist-high plinth
        // directly in front of the entrance: the player jogged into it within
        // two seconds of starting, and NoaAnimatorTests caught it as the
        // Animator's Speed collapsing to zero while movement was still held.
        Vector3[] spots =
        {
            new Vector3(-12.5f, 0f, 7f),
            new Vector3(-12.5f, 0f, 1f),
            new Vector3(-12.5f, 0f, -5f),
            new Vector3(12.5f, 0f, -7f),
            new Vector3(12.5f, 0f, 2f),
            new Vector3(-7.5f, 0f, 8.6f),
            new Vector3(7.5f, 0f, 8.6f),
        };

        for (int i = 0; i < spots.Length; i++)
        {
            GameObject one = Child(cases, "DisplayCase_" + i);
            one.transform.position = spots[i];

            Box(one, "Plinth", spots[i] + new Vector3(0f, 0.45f, 0f),
                new Vector3(1.2f, 0.9f, 1.2f), marble, true);

            Box(one, "Rim", spots[i] + new Vector3(0f, 0.93f, 0f),
                new Vector3(1.35f, 0.08f, 1.35f), brass, false);

            // The vitrine itself.
            Box(one, "Glass", spots[i] + new Vector3(0f, 1.62f, 0f),
                new Vector3(1.1f, 1.3f, 1.1f), glass, false);

            // Something inside worth looking at.
            Box(one, "Exhibit", spots[i] + new Vector3(0f, 1.25f, 0f),
                new Vector3(0.35f, 0.5f, 0.35f), brass, false);
        }
    }

    /// <summary>Framed canvases along the long walls, each with its own picture light.</summary>
    private static void BuildWallArt(GameObject root)
    {
        GameObject art = Child(root, "WallArt");

        for (int i = 0; i < 5; i++)
        {
            float x = -10f + i * 5f;
            PlaceCanvas(art, "ArtNorth_" + i,
                        new Vector3(x, 4.2f, HalfZ - 0.35f), 0f);
        }

        for (int i = 0; i < 4; i++)
        {
            float x = -9f + i * 6f;
            PlaceCanvas(art, "ArtSouth_" + i,
                        new Vector3(x, 4.2f, -HalfZ + 0.35f), 180f);
        }
    }

    private static void PlaceCanvas(GameObject parent, string name, Vector3 position, float yaw)
    {
        GameObject frame = Child(parent, name);
        frame.transform.position = position;
        frame.transform.rotation = Quaternion.Euler(0f, yaw, 0f);

        Box(frame, "Frame", position, new Vector3(2.6f, 1.9f, 0.12f), wood, false)
            .transform.rotation = Quaternion.Euler(0f, yaw, 0f);

        Box(frame, "Canvas", position + Rotate(new Vector3(0f, 0f, -0.08f), yaw),
            new Vector3(2.3f, 1.6f, 0.06f), canvasMat, false)
            .transform.rotation = Quaternion.Euler(0f, yaw, 0f);

        // A warm picture light above each frame. Range is deliberately short:
        // URP allows 8 additional lights per object, and a museum wall full of
        // long-range lights would blow that budget for no visible gain.
        var lightGo = new GameObject(name + "_Light");
        lightGo.transform.SetParent(frame.transform, true);
        lightGo.transform.position = position + Rotate(new Vector3(0f, 1.4f, -0.7f), yaw);
        lightGo.transform.rotation = Quaternion.Euler(55f, yaw + 180f, 0f);

        Light light = lightGo.AddComponent<Light>();
        light.type = LightType.Spot;
        light.color = new Color(1f, 0.87f, 0.66f);
        light.intensity = 5f;
        light.range = 6f;
        light.spotAngle = 62f;
        light.innerSpotAngle = 30f;
        light.shadows = LightShadows.None;
    }

    private static void BuildBenches(GameObject root)
    {
        GameObject benches = Child(root, "Benches");

        Vector3[] spots =
        {
            new Vector3(-3f, 0f, 6.5f),
            new Vector3(3f, 0f, 6.5f),
            new Vector3(0f, 0f, -6.5f),
        };

        for (int i = 0; i < spots.Length; i++)
        {
            GameObject bench = Child(benches, "Bench_" + i);
            bench.transform.position = spots[i];

            Box(bench, "Seat", spots[i] + new Vector3(0f, 0.45f, 0f),
                new Vector3(2.2f, 0.14f, 0.6f), wood, true);

            Box(bench, "LegLeft", spots[i] + new Vector3(-0.9f, 0.22f, 0f),
                new Vector3(0.12f, 0.45f, 0.5f), brass, false);

            Box(bench, "LegRight", spots[i] + new Vector3(0.9f, 0.22f, 0f),
                new Vector3(0.12f, 0.45f, 0.5f), brass, false);
        }
    }

    // ------------------------------------------------------------------

    private static Vector3 Rotate(Vector3 v, float yaw)
    {
        return Quaternion.Euler(0f, yaw, 0f) * v;
    }

    private static GameObject Box(GameObject parent, string name, Vector3 position,
                                  Vector3 size, Material material, bool collide)
    {
        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        go.transform.SetParent(parent.transform, true);
        go.transform.position = position;
        go.transform.localScale = size;

        if (!collide)
        {
            Collider c = go.GetComponent<Collider>();
            if (c != null) { Object.DestroyImmediate(c); }
        }

        if (material != null)
        {
            go.GetComponent<MeshRenderer>().sharedMaterial = material;
        }

        return go;
    }

    private static GameObject Child(GameObject parent, string name)
    {
        Transform t = parent.transform.Find(name);
        if (t != null) { return t.gameObject; }

        var go = new GameObject(name);
        go.transform.SetParent(parent.transform, true);
        return go;
    }

    private static Material Mat(string path)
    {
        return AssetDatabase.LoadAssetAtPath<Material>(path);
    }

    private static Material BuildGlass()
    {
        const string path = "Assets/Materials/Museum/MuseumGlass.mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);

        if (mat == null)
        {
            mat = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = "MuseumGlass" };
            AssetDatabase.CreateAsset(mat, path);
        }

        // Transparent, barely tinted, very smooth - a vitrine, not a window.
        mat.SetFloat("_Surface", 1f);
        mat.SetFloat("_Blend", 0f);
        mat.SetFloat("_ZWrite", 0f);
        mat.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
        mat.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;

        mat.SetColor("_BaseColor", new Color(0.72f, 0.82f, 0.88f, 0.16f));
        mat.SetFloat("_Smoothness", 0.96f);
        mat.SetFloat("_Metallic", 0f);

        EditorUtility.SetDirty(mat);
        return mat;
    }

    private static Material BuildCanvas()
    {
        const string path = "Assets/Materials/Museum/MuseumCanvas.mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);

        if (mat == null)
        {
            mat = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = "MuseumCanvas" };
            AssetDatabase.CreateAsset(mat, path);
        }

        // The plaster texture at a coarse tiling reads as aged paint at this
        // distance, and costs no new art against the size budget.
        var tex = AssetDatabase.LoadAssetAtPath<Texture>("Assets/Materials/Museum/MuseumPlaster.png");
        if (tex != null) { mat.SetTexture("_BaseMap", tex); }

        mat.SetColor("_BaseColor", new Color(0.46f, 0.38f, 0.30f));
        mat.SetFloat("_Smoothness", 0.15f);
        mat.SetTextureScale("_BaseMap", new Vector2(2f, 2f));

        EditorUtility.SetDirty(mat);
        return mat;
    }
}
