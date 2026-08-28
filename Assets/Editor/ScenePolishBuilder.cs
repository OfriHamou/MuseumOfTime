using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Presentation pass: dresses the three gameplay scenes so they read as
/// intentional places rather than blockout, using only assets already in the
/// repository (the MarbleStatue / StoneColumn LOD prefabs, the museum
/// materials) plus a handful of small dressing materials created here.
///
///   Unity.exe -batchmode -quit -projectPath . ^
///             -executeMethod ScenePolishBuilder.BuildFromCommandLine
///
/// GAMEPLAY SAFETY. This adds NO gameplay and changes NO gameplay logic. All
/// dressing goes under a single "SceneDressing" root that is cleared and
/// rebuilt each run, and every dressing prop is DECORATIVE (colliders
/// stripped) so it can never block a NavMeshAgent or the player - the one
/// exception is ClockCore's four containment walls, which sit on the arena
/// edge outside where agents roam and only stop the player walking off. No
/// navmesh is re-baked. The only touches to existing objects are cosmetic:
/// assigning a material to an enemy/pickup/anchor renderer.
/// </summary>
public static class ScenePolishBuilder
{
    private const string DressingRoot = "SceneDressing";
    private const string MatFolder = "Assets/Materials/Dressing";

    private const string MarblePrefab = "Assets/Prefabs/World/MarbleStatue.prefab";
    private const string ColumnPrefab = "Assets/Prefabs/World/StoneColumn.prefab";

    // Shared material handles, filled by EnsureMaterials.
    private static Material marble, plaster, brass, roof, warden, shadowMat, shardGlow, lensGlow, collectorMat, shieldMat, facade;

    [MenuItem("Museum of Time/Polish Scenes (dressing)")]
    public static void BuildMenu() { Build(); }

    public static void BuildFromCommandLine() { Build(); }

    private static void Build()
    {
        EnsureMaterials();

        PolishMuseumNight();
        PolishFrozenCity();
        PolishClockCore();

        Debug.Log("SCENE POLISH OK: MuseumNight, FrozenCity and ClockCore dressed with existing assets.");
    }

    // -----------------------------------------------------------------
    // Materials
    // -----------------------------------------------------------------

    private static void EnsureMaterials()
    {
        Directory.CreateDirectory(MatFolder);

        marble = Load("Assets/Materials/Museum/MuseumMarble.mat");
        plaster = Load("Assets/Materials/Museum/MuseumPlaster.mat");
        brass = Load("Assets/Materials/Museum/MuseumBrass.mat");

        roof = Mat("Roof", new Color(0.17f, 0.17f, 0.20f), null, 0.1f, 0.2f);
        warden = Mat("Warden", new Color(0.22f, 0.22f, 0.26f), null, 0.25f, 0.35f);
        shadowMat = Mat("Shadow", new Color(0.16f, 0.13f, 0.28f), new Color(0.18f, 0.12f, 0.4f), 0f, 0.5f);
        shardGlow = Mat("ShardGlow", new Color(0.35f, 0.85f, 1f), new Color(0.25f, 0.75f, 1f), 0f, 0.7f);
        lensGlow = Mat("LensGlow", new Color(1f, 0.78f, 0.35f), new Color(0.95f, 0.6f, 0.2f), 0.3f, 0.6f);
        collectorMat = Mat("Collector", new Color(0.14f, 0.06f, 0.06f), new Color(0.35f, 0.05f, 0.05f), 0.3f, 0.4f);
        shieldMat = Mat("Shield", new Color(0.85f, 0.8f, 0.45f), new Color(0.6f, 0.5f, 0.18f), 0.6f, 0.8f);
        facade = FacadeMaterial();
    }

    /// <summary>
    /// A stone facade with a grid of frosted, lit-from-within windows,
    /// replacing the flat plaster wall FrozenCity's buildings used to have.
    /// </summary>
    private static Material FacadeMaterial()
    {
        string path = MatFolder + "/BuildingFacade.mat";
        Material existing = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (existing != null)
        {
            return existing;
        }

        const int size = 256;
        var tex = new Texture2D(size, size);
        var emissionTex = new Texture2D(size, size);
        Color stone = new Color(0.62f, 0.60f, 0.58f);
        Color mortar = new Color(0.45f, 0.44f, 0.42f);
        Color windowLit = new Color(0.95f, 0.75f, 0.35f);
        Color windowDark = new Color(0.12f, 0.14f, 0.18f);

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float grain = Mathf.PerlinNoise(x * 0.08f, y * 0.08f) * 0.08f;
                Color c = stone * (0.96f + grain);
                Color e = Color.black;

                bool mortarLine = (x % 32) < 2 || (y % 20) < 2;
                if (mortarLine)
                {
                    c = mortar;
                }

                // A window in the centre of every other stone block.
                int bx = x % 64, by = y % 80;
                bool inWindow = bx > 16 && bx < 48 && by > 24 && by < 64;
                if (inWindow)
                {
                    bool lit = ((x / 64) + (y / 80)) % 3 != 0;
                    c = lit ? windowLit : windowDark;
                    e = lit ? windowLit : Color.black;
                }

                tex.SetPixel(x, y, c);
                emissionTex.SetPixel(x, y, e);
            }
        }

        tex.Apply();
        emissionTex.Apply();

        Texture2D imported = SaveTexture(tex, "BuildingFacade");
        Texture2D importedEmission = SaveTexture(emissionTex, "BuildingFacadeEmission");

        var mat = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = "BuildingFacade" };
        mat.SetTexture("_BaseMap", imported);
        mat.SetTextureScale("_BaseMap", new Vector2(2f, 1.5f));
        mat.SetFloat("_Smoothness", 0.15f);
        mat.EnableKeyword("_EMISSION");
        mat.SetTexture("_EmissionMap", importedEmission);
        mat.SetTextureScale("_EmissionMap", new Vector2(2f, 1.5f));
        mat.SetColor("_EmissionColor", Color.white);
        mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;

        AssetDatabase.CreateAsset(mat, path);
        return AssetDatabase.LoadAssetAtPath<Material>(path);
    }

    private static Texture2D SaveTexture(Texture2D tex, string name)
    {
        byte[] png = tex.EncodeToPNG();
        Object.DestroyImmediate(tex);
        string texPath = MatFolder + "/" + name + ".png";
        File.WriteAllBytes(texPath, png);
        AssetDatabase.ImportAsset(texPath);

        var importer = (TextureImporter)AssetImporter.GetAtPath(texPath);
        importer.wrapMode = TextureWrapMode.Repeat;
        importer.filterMode = FilterMode.Bilinear;
        importer.maxTextureSize = 512;
        importer.SaveAndReimport();

        return AssetDatabase.LoadAssetAtPath<Texture2D>(texPath);
    }

    private static Material Load(string path)
    {
        return AssetDatabase.LoadAssetAtPath<Material>(path);
    }

    private static Material Mat(string name, Color baseColor, Color? emission, float metallic, float smoothness)
    {
        string path = MatFolder + "/" + name + ".mat";
        Material existing = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (existing != null)
        {
            return existing;
        }

        var m = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = name };
        m.SetColor("_BaseColor", baseColor);
        m.SetFloat("_Metallic", metallic);
        m.SetFloat("_Smoothness", smoothness);

        if (emission.HasValue)
        {
            m.EnableKeyword("_EMISSION");
            m.SetColor("_EmissionColor", emission.Value);
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
        }

        AssetDatabase.CreateAsset(m, path);
        return m;
    }

    // -----------------------------------------------------------------
    // MuseumNight - already the best-dressed scene (building, columns,
    // statues, hinge props, Phase 7 lighting). A light readability pass only.
    // -----------------------------------------------------------------

    private static void PolishMuseumNight()
    {
        Scene scene = EditorSceneManager.OpenScene("Assets/Scenes/MuseumNight.unity", OpenSceneMode.Single);

        Recolor("TimeShard_A", shardGlow);
        Recolor("TimeShard_B", shardGlow);
        Recolor("TimeLens", lensGlow);
        Recolor("Plaque_ClockOfCreation", brass);
        Recolor("TimeWarden", warden);
        Recolor("ChronologicalShadow", shadowMat);

        Save(scene);
    }

    // -----------------------------------------------------------------
    // FrozenCity - the frozen city that stopped before sunset: streets of
    // buildings leading to the clock tower, motionless citizens, a readable
    // tower landmark.
    // -----------------------------------------------------------------

    private static void PolishFrozenCity()
    {
        Scene scene = EditorSceneManager.OpenScene("Assets/Scenes/FrozenCity.unity", OpenSceneMode.Single);

        Terrain terrain = Terrain.activeTerrain;
        GameObject root = FreshRoot();

        // Streets: buildings flanking a clear central corridor (x within +-4)
        // that runs from spawn (z=-20) to the tower (z=35). Non-colliding, so
        // they never block the path or the patrols.
        int[] heights = { 6, 8, 5, 9, 7, 6 };
        int[] zs = { -12, -4, 4, 12, 20, 28 };
        for (int i = 0; i < zs.Length; i++)
        {
            BuildBuilding(root, terrain, new Vector3(-9f, 0f, zs[i]), 5f, heights[i], 4f);
            BuildBuilding(root, terrain, new Vector3(9f, 0f, zs[(i + 3) % zs.Length]), 5f, heights[(i + 2) % heights.Length], 4f);
        }

        // Motionless citizens - the city froze mid-life.
        Vector3[] citizens =
        {
            new Vector3(-12f, 0f, -8f), new Vector3(12f, 0f, -6f), new Vector3(-14f, 0f, 4f),
            new Vector3(13f, 0f, 8f), new Vector3(-10f, 0f, 16f), new Vector3(11f, 0f, 18f),
            new Vector3(-13f, 0f, 24f), new Vector3(12f, 0f, 28f), new Vector3(-7f, 0f, 33f),
            new Vector3(8f, 0f, 33f), new Vector3(14f, 0f, -14f), new Vector3(-15f, 0f, 12f),
        };
        for (int i = 0; i < citizens.Length; i++)
        {
            Prop(MarblePrefab, root, OnTerrain(terrain, citizens[i]),
                 Vector3.one * 0.9f, new Vector3(0f, i * 47f % 360f, 0f));
        }

        // Lanterns down the central path - a lit route to the tower.
        foreach (int z in new[] { -12, -2, 8, 18, 28 })
        {
            BuildLantern(root, OnTerrain(terrain, new Vector3(-2.5f, 0f, z)));
            BuildLantern(root, OnTerrain(terrain, new Vector3(2.5f, 0f, z)));
        }

        DressClockTower(root, terrain);

        // Readability of the gameplay pieces.
        Recolor("TimeWarden", warden);
        Recolor("ChronologicalShadow", shadowMat);
        Recolor("ChronoHourglass", lensGlow);
        RecolorChild("TimeAnchor_Overlook", "LensVisual", lensGlow);
        RecolorChild("TimeAnchor_TowerBase", "LensVisual", lensGlow);

        Save(scene);
    }

    private static void DressClockTower(GameObject root, Terrain terrain)
    {
        GameObject tower = GameObject.Find("ClockTower");
        if (tower == null)
        {
            return;
        }

        Vector3 basePos = tower.transform.position;   // (0,0,35)

        // A clock face on the approach (-Z) side, so the tower reads as a
        // clock and as the objective from spawn.
        //
        // It has to be LIT to do that job. In plain brass it was a dim grey
        // square on a grey tower in fog - invisible from the far end of the
        // city, which is precisely where the player needs to be able to pick
        // the objective out. A glowing dial is also the honest read: this is
        // the one clock in a city where time has stopped.
        Material dial = Emissive("ClockDial", new Color(1f, 0.86f, 0.55f), 2.6f);

        Cube(root, "ClockFace", basePos + new Vector3(0f, 16f, -3.2f),
             new Vector3(4f, 4f, 0.3f), Vector3.zero, dial, false);

        // A rim, so the lit dial reads as a face rather than a glowing panel.
        Cube(root, "ClockRim", basePos + new Vector3(0f, 16f, -3.15f),
             new Vector3(4.6f, 4.6f, 0.25f), Vector3.zero, roof, false);

        // Hands sit in front of the dial and stay dark, so they silhouette
        // against it instead of disappearing into it.
        Cube(root, "ClockHandHour", basePos + new Vector3(0f, 16f, -3.5f),
             new Vector3(0.18f, 1.4f, 0.18f), new Vector3(0f, 0f, 25f), roof, false);
        Cube(root, "ClockHandMinute", basePos + new Vector3(0f, 16f, -3.5f),
             new Vector3(0.14f, 2.1f, 0.14f), new Vector3(0f, 0f, 110f), roof, false);

        // And a light of its own, so the dial throws its colour onto the
        // tower face rather than looking like a decal stuck to it.
        GameObject glow = new GameObject("ClockFaceGlow");
        glow.transform.SetParent(root.transform, false);
        glow.transform.position = basePos + new Vector3(0f, 16f, -4.5f);

        Light dialLight = glow.AddComponent<Light>();
        dialLight.type = LightType.Point;
        dialLight.color = new Color(1f, 0.86f, 0.55f);
        dialLight.intensity = 12f;
        dialLight.range = 22f;
        dialLight.shadows = LightShadows.None;

        // A stepped roof cap over the belfry.
        Cube(root, "TowerRoof", basePos + new Vector3(0f, 28.5f, 0f),
             new Vector3(9f, 1.5f, 9f), Vector3.zero, roof, false);
        Cube(root, "TowerSpire", basePos + new Vector3(0f, 30.5f, 0f),
             new Vector3(4.5f, 3f, 4.5f), new Vector3(0f, 45f, 0f), roof, false);

        // Buttress columns at the tower corners.
        foreach (Vector3 corner in new[]
                 {
                     new Vector3(3.6f, 0f, 31.4f), new Vector3(-3.6f, 0f, 31.4f),
                     new Vector3(3.6f, 0f, 38.6f), new Vector3(-3.6f, 0f, 38.6f),
                 })
        {
            Prop(ColumnPrefab, root, OnTerrain(terrain, corner), Vector3.one, Vector3.zero);
        }
    }

    // -----------------------------------------------------------------
    // ClockCore - the inverted museum arena: enclosing walls, a ceiling with
    // statues hanging from it, columns framing the boss on a central dais.
    // -----------------------------------------------------------------

    private static void PolishClockCore()
    {
        Scene scene = EditorSceneManager.OpenScene("Assets/Scenes/ClockCore.unity", OpenSceneMode.Single);

        GameObject root = FreshRoot();

        // Containment walls on the 40x40 floor edge. COLLIDING - the one
        // exception - so the player cannot walk off the arena; they sit
        // outside where the agents patrol (the centre), so navmesh is untouched.
        Cube(root, "WallNorth", new Vector3(0f, 4f, 20f), new Vector3(40f, 8f, 1f), Vector3.zero, marble, true);
        Cube(root, "WallSouth", new Vector3(0f, 4f, -20f), new Vector3(40f, 8f, 1f), Vector3.zero, marble, true);
        Cube(root, "WallEast", new Vector3(20f, 4f, 0f), new Vector3(1f, 8f, 40f), Vector3.zero, marble, true);
        Cube(root, "WallWest", new Vector3(-20f, 4f, 0f), new Vector3(1f, 8f, 40f), Vector3.zero, marble, true);

        // The inverted museum: a marble ceiling, with statues and columns
        // hanging down from it.
        Cube(root, "Ceiling", new Vector3(0f, 8f, 0f), new Vector3(40f, 0.5f, 40f), Vector3.zero, marble, false);

        foreach (Vector3 p in new[] { new Vector3(10f, 8f, -6f), new Vector3(-10f, 8f, -6f), new Vector3(6f, 8f, 11f) })
        {
            Prop(MarblePrefab, root, p, Vector3.one, new Vector3(180f, 0f, 0f));   // upside down
        }
        foreach (Vector3 p in new[] { new Vector3(8f, 8f, 6f), new Vector3(-8f, 8f, 6f) })
        {
            Prop(ColumnPrefab, root, p, Vector3.one, new Vector3(180f, 0f, 0f));
        }

        // Upright columns framing the arena, and a few statues on the floor.
        foreach (Vector3 p in new[]
                 {
                     new Vector3(14f, 0f, 14f), new Vector3(-14f, 0f, 14f),
                     new Vector3(14f, 0f, -14f), new Vector3(-14f, 0f, -14f),
                     new Vector3(16f, 0f, 0f), new Vector3(-16f, 0f, 0f),
                     new Vector3(0f, 0f, 16f),

                     // Flanking the entrance rather than standing in it. A
                     // single column used to sit at (0, 0, -16) - one metre in
                     // front of the player spawn at (0, 0.1, -15) and directly
                     // between the third-person camera and Noa. It went
                     // unnoticed while the LOD models were importing at 1/100
                     // scale (a 4 cm pebble); at their real 4 m it blocks half
                     // the screen on the frame the scene opens.
                     new Vector3(-4.5f, 0f, -16f), new Vector3(4.5f, 0f, -16f),
                 })
        {
            Prop(ColumnPrefab, root, p, Vector3.one, Vector3.zero);
        }
        foreach (Vector3 p in new[] { new Vector3(14f, 0f, 0f), new Vector3(-14f, 0f, 0f), new Vector3(0f, 0f, 14f) })
        {
            Prop(MarblePrefab, root, p, Vector3.one, Vector3.zero);
        }

        // A dais under the Collector so the boss reads as the arena's focus.
        Cylinder(root, "CollectorDais", new Vector3(0f, 0.15f, 8f), new Vector3(3.2f, 0.15f, 3.2f), marble);

        // Clockwork: large brass gears mounted on the walls, facing the
        // arena, so the "final area" reads as the museum's own broken clock
        // mechanism rather than an empty box.
        BuildGear(root, new Vector3(-19.4f, 5f, -6f), new Vector3(0f, 90f, 0f), 3f, 12);
        BuildGear(root, new Vector3(19.4f, 8f, 5f), new Vector3(0f, -90f, 0f), 2.2f, 10);
        BuildGear(root, new Vector3(0f, 7.6f, -19.4f), Vector3.zero, 2.6f, 10);

        // Readability of the gameplay pieces.
        Recolor("Collector", collectorMat);
        RecolorChild("Collector", "Shield", shieldMat);
        Recolor("TimeWarden", warden);
        Recolor("ChronologicalShadow", shadowMat);
        RecolorChild("TimeAnchor_EastWing", "LensVisual", lensGlow);
        RecolorChild("TimeAnchor_WestWing", "LensVisual", lensGlow);

        Save(scene);
    }

    // -----------------------------------------------------------------
    // Building blocks
    // -----------------------------------------------------------------

    private static void BuildBuilding(GameObject root, Terrain terrain, Vector3 flat, float width, float height, float depth)
    {
        float groundY = TerrainHeight(terrain, flat);
        var body = Cube(root, "Building", new Vector3(flat.x, groundY + height / 2f, flat.z),
                        new Vector3(width, height, depth), Vector3.zero, facade, false);
        Cube(root, "Roof", body.transform.position + new Vector3(0f, height / 2f + 0.3f, 0f),
             new Vector3(width + 0.4f, 0.6f, depth + 0.4f), Vector3.zero, roof, false);
    }

    /// <summary>
    /// A wall-mounted decorative gear: a flat brass disc facing +Z by
    /// default, ringed with teeth, wrapped in one group so `euler` orients
    /// the whole assembly to face into the room from whichever wall it sits on.
    /// </summary>
    private static void BuildGear(GameObject root, Vector3 pos, Vector3 euler, float radius, int teeth)
    {
        var gearRoot = new GameObject("Gear");
        gearRoot.transform.SetParent(root.transform, false);
        gearRoot.transform.position = pos;
        gearRoot.transform.rotation = Quaternion.Euler(euler);

        GameObject disc = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        disc.name = "GearDisc";
        StripColliders(disc);
        disc.transform.SetParent(gearRoot.transform, false);
        disc.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        disc.transform.localScale = new Vector3(radius, 0.2f, radius);
        disc.GetComponent<MeshRenderer>().sharedMaterial = brass;

        float toothSize = radius * 0.22f;
        for (int i = 0; i < teeth; i++)
        {
            float angle = i * 360f / teeth;
            Quaternion rot = Quaternion.Euler(0f, 0f, angle);
            Vector3 dir = rot * Vector3.up;

            GameObject tooth = GameObject.CreatePrimitive(PrimitiveType.Cube);
            tooth.name = "GearTooth";
            StripColliders(tooth);
            tooth.transform.SetParent(gearRoot.transform, false);
            tooth.transform.localPosition = dir * (radius + toothSize * 0.4f);
            tooth.transform.localRotation = rot;
            tooth.transform.localScale = new Vector3(toothSize, toothSize, 0.3f);
            tooth.GetComponent<MeshRenderer>().sharedMaterial = brass;
        }

        // A small emissive hub, echoing the time-energy palette used elsewhere.
        GameObject hub = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        hub.name = "GearHub";
        StripColliders(hub);
        hub.transform.SetParent(gearRoot.transform, false);
        hub.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        hub.transform.localPosition = new Vector3(0f, 0f, -0.05f);
        hub.transform.localScale = new Vector3(radius * 0.22f, 0.25f, radius * 0.22f);
        hub.GetComponent<MeshRenderer>().sharedMaterial = lensGlow;
    }

    private static void BuildLantern(GameObject root, Vector3 pos)
    {
        Cube(root, "LanternPost", pos + new Vector3(0f, 1.1f, 0f),
             new Vector3(0.12f, 2.2f, 0.12f), Vector3.zero, roof, false);
        GameObject lamp = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        lamp.name = "LanternLamp";
        StripColliders(lamp);
        lamp.transform.SetParent(root.transform, false);
        lamp.transform.position = pos + new Vector3(0f, 2.3f, 0f);
        lamp.transform.localScale = Vector3.one * 0.35f;
        lamp.GetComponent<MeshRenderer>().sharedMaterial = lensGlow;

        Light light = lamp.AddComponent<Light>();
        light.type = LightType.Point;
        light.color = new Color(1f, 0.8f, 0.45f);
        light.intensity = 2.5f;
        light.range = 6f;
        light.shadows = LightShadows.None;
    }

    // -----------------------------------------------------------------
    // Primitive / prefab helpers
    // -----------------------------------------------------------------

    /// <summary>
    /// A cached emissive material under Assets/Materials/Dressing, so repeat
    /// runs reuse the asset instead of leaking a new one into the scene.
    /// </summary>
    private static Material Emissive(string name, Color colour, float intensity)
    {
        const string folder = "Assets/Materials/Dressing";
        string path = folder + "/" + name + ".mat";

        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);

        if (mat == null)
        {
            if (!AssetDatabase.IsValidFolder(folder))
            {
                AssetDatabase.CreateFolder("Assets/Materials", "Dressing");
            }

            mat = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = name };
            AssetDatabase.CreateAsset(mat, path);
        }

        mat.SetColor("_BaseColor", colour);
        mat.SetFloat("_Smoothness", 0.4f);
        mat.SetFloat("_Metallic", 0f);

        mat.EnableKeyword("_EMISSION");
        mat.SetColor("_EmissionColor", colour * intensity);
        mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;

        EditorUtility.SetDirty(mat);
        return mat;
    }

    private static GameObject Cube(GameObject root, string name, Vector3 pos, Vector3 scale, Vector3 euler, Material mat, bool collide)
    {
        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        if (!collide) { StripColliders(go); }
        go.transform.SetParent(root.transform, false);
        go.transform.position = pos;
        go.transform.localScale = scale;
        go.transform.rotation = Quaternion.Euler(euler);
        if (mat != null) { go.GetComponent<MeshRenderer>().sharedMaterial = mat; }
        return go;
    }

    private static void Cylinder(GameObject root, string name, Vector3 pos, Vector3 scale, Material mat)
    {
        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        go.name = name;
        StripColliders(go);
        go.transform.SetParent(root.transform, false);
        go.transform.position = pos;
        go.transform.localScale = scale;
        if (mat != null) { go.GetComponent<MeshRenderer>().sharedMaterial = mat; }
    }

    private static void Prop(string prefabPath, GameObject root, Vector3 pos, Vector3 scale, Vector3 euler)
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        if (prefab == null)
        {
            return;
        }

        var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        go.transform.SetParent(root.transform, true);
        go.transform.position = pos;
        go.transform.localScale = scale;
        go.transform.rotation = Quaternion.Euler(euler);
        StripColliders(go);
    }

    private static void StripColliders(GameObject go)
    {
        foreach (Collider c in go.GetComponentsInChildren<Collider>(true))
        {
            Object.DestroyImmediate(c);
        }
    }

    // -----------------------------------------------------------------
    // Cosmetic recolour of existing objects (no logic change)
    // -----------------------------------------------------------------

    private static void Recolor(string objectName, Material mat)
    {
        GameObject go = GameObject.Find(objectName);
        if (go == null || mat == null)
        {
            return;
        }

        foreach (MeshRenderer r in go.GetComponentsInChildren<MeshRenderer>(true))
        {
            r.sharedMaterial = mat;
        }
    }

    private static void RecolorChild(string parentName, string childName, Material mat)
    {
        GameObject parent = GameObject.Find(parentName);
        if (parent == null || mat == null)
        {
            return;
        }

        Transform child = parent.transform.Find(childName);
        if (child == null)
        {
            return;
        }

        foreach (MeshRenderer r in child.GetComponentsInChildren<MeshRenderer>(true))
        {
            r.sharedMaterial = mat;
        }
    }

    // -----------------------------------------------------------------
    // Terrain / scene plumbing
    // -----------------------------------------------------------------

    private static Vector3 OnTerrain(Terrain terrain, Vector3 flat)
    {
        return new Vector3(flat.x, TerrainHeight(terrain, flat), flat.z);
    }

    private static float TerrainHeight(Terrain terrain, Vector3 flat)
    {
        return terrain == null ? flat.y : terrain.transform.position.y + terrain.SampleHeight(flat);
    }

    private static GameObject FreshRoot()
    {
        GameObject root = GameObject.Find(DressingRoot);
        if (root != null)
        {
            Object.DestroyImmediate(root);
        }

        return new GameObject(DressingRoot);
    }

    private static void Save(Scene scene)
    {
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
    }
}
