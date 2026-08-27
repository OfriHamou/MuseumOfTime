using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Draws the museum's actual footprint onto the minimap.
///
///   Unity.exe -batchmode -quit -projectPath . ^
///             -executeMethod MinimapGeometryBuilder.BuildFromCommandLine
///
/// The minimap camera renders an allow-list of exactly the `Minimap` layer,
/// which is the right way to keep a hidden Time Anchor invisible by
/// construction. But NOTHING was ever put on that layer except the player's
/// own marker, so the map was a single arrow on a flat dark background - it
/// told the player their heading and nothing else.
///
/// T18 asks for a minimap that gives ORIENTATION for a whole scene, and an
/// empty map orients nobody. This generates flat plates from the museum's real
/// geometry, so the map cannot drift out of step with the building: change a
/// wall and re-run, and the map changes with it.
///
/// Idempotent.
/// </summary>
public static class MinimapGeometryBuilder
{
    private const string ScenePath = "Assets/Scenes/MuseumNight.unity";
    private const string Root = "MinimapGeometry";
    private const string MaterialFolder = "Assets/Materials/UI";

    /// <summary>Plate height. Below the player marker (y = 1.08) so the marker draws on top.</summary>
    private const float PlateHeight = 0.35f;

    [MenuItem("Museum of Time/Build Minimap Geometry")]
    public static void BuildMenu() { Build(); }

    public static void BuildFromCommandLine() { Build(); }

    private static void Build()
    {
        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

        int layer = LayerMask.NameToLayer("Minimap");
        if (layer < 0)
        {
            Debug.LogError("MINIMAP GEO FAILED: no 'Minimap' layer.");
            return;
        }

        GameObject root = GameObject.Find(Root);
        if (root != null) { Object.DestroyImmediate(root); }

        root = new GameObject(Root);

        Material wallMat = Flat("MinimapWall", new Color(0.62f, 0.68f, 0.80f));
        Material floorMat = Flat("MinimapFloor", new Color(0.13f, 0.15f, 0.21f));
        Material stairMat = Flat("MinimapStair", new Color(0.40f, 0.45f, 0.56f));
        Material goalMat = Flat("MinimapGoal", new Color(0.92f, 0.78f, 0.42f));
        Material exitMat = Flat("MinimapExit", new Color(0.45f, 0.85f, 0.55f));

        int plates = 0;

        GameObject museum = GameObject.Find("Museum");
        if (museum != null)
        {
            foreach (MeshRenderer r in museum.GetComponentsInChildren<MeshRenderer>(true))
            {
                string n = r.gameObject.name;

                Material mat;
                float height;

                if (n.Contains("Slab")) { mat = floorMat; height = PlateHeight - 0.15f; }
                else if (n.StartsWith("Step")) { mat = stairMat; height = PlateHeight - 0.05f; }
                else { mat = wallMat; height = PlateHeight; }

                // Steps are drawn as one block rather than 32 slivers.
                if (n.StartsWith("Step") && !n.EndsWith("00")) { continue; }

                Bounds b = r.bounds;
                Vector3 size = n.StartsWith("Step")
                    ? new Vector3(b.size.x, 0.1f, 7f)   // the staircase run
                    : new Vector3(b.size.x, 0.1f, b.size.z);

                Plate(root, layer, "Map_" + n,
                      new Vector3(b.center.x, height, b.center.z), size, mat);

                plates++;
            }
        }

        // Objectives and the way out, so the map answers "where next".
        plates += MarkAll(root, layer, "ShardPickup", goalMat, 0.9f);
        plates += MarkAll(root, layer, "ItemPickup", goalMat, 1.1f);
        plates += MarkAll(root, layer, "SceneExitTrigger", exitMat, 1.4f);

        SizePlayerMarker();

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);

        Debug.Log("MINIMAP GEO OK: " + plates + " plates on the Minimap layer.");
    }

    /// <summary>
    /// The player marker was 0.6 x 0.9 units against a 32-unit-wide
    /// orthographic view - about four pixels in a 190 px map, which is
    /// indistinguishable from a map plate. A minimap whose "you are here" is
    /// invisible orients nobody, which is the whole of T18.
    /// </summary>
    private static void SizePlayerMarker()
    {
        GameObject marker = GameObject.Find("MinimapMarker");
        if (marker == null) { return; }

        // Longer than it is wide, so the map reads a HEADING, not just a spot.
        marker.transform.localScale = new Vector3(1.5f, 0.1f, 2.6f);

        // Above every map plate so it is never occluded by one.
        Vector3 local = marker.transform.localPosition;
        marker.transform.localPosition = new Vector3(local.x, 2.2f, local.z);

        EditorUtility.SetDirty(marker);
    }

    /// <summary>Drops a marker under every component of the given type name.</summary>
    private static int MarkAll(GameObject root, int layer, string componentTypeName,
                               Material material, float size)
    {
        System.Type type = typeof(ShardPickup).Assembly.GetType(componentTypeName);
        if (type == null) { return 0; }

        Object[] found = Object.FindObjectsByType(type, FindObjectsInactive.Include);
        int count = 0;

        foreach (Object o in found)
        {
            var component = o as Component;
            if (component == null) { continue; }

            Vector3 p = component.transform.position;

            Plate(root, layer, "Map_" + component.gameObject.name,
                  new Vector3(p.x, PlateHeight + 0.2f, p.z),
                  new Vector3(size, 0.1f, size), material);

            count++;
        }

        return count;
    }

    private static void Plate(GameObject root, int layer, string name,
                              Vector3 position, Vector3 size, Material material)
    {
        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        go.layer = layer;
        go.transform.SetParent(root.transform, true);
        go.transform.position = position;
        go.transform.localScale = size;

        // Map plates are drawn, never touched.
        Collider collider = go.GetComponent<Collider>();
        if (collider != null) { Object.DestroyImmediate(collider); }

        MeshRenderer renderer = go.GetComponent<MeshRenderer>();
        renderer.sharedMaterial = material;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;
    }

    /// <summary>
    /// An unlit material: the minimap camera sees no scene lights, so a Lit
    /// shader would render every plate black.
    /// </summary>
    private static Material Flat(string name, Color colour)
    {
        if (!AssetDatabase.IsValidFolder(MaterialFolder))
        {
            AssetDatabase.CreateFolder("Assets/Materials", "UI");
        }

        string path = MaterialFolder + "/" + name + ".mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);

        if (mat == null)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
            mat = new Material(shader) { name = name };
            AssetDatabase.CreateAsset(mat, path);
        }

        mat.SetColor("_BaseColor", colour);
        EditorUtility.SetDirty(mat);

        return mat;
    }
}
