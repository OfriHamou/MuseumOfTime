using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Gives every thing the player is asked to interact with an actual object.
///
///   Unity.exe -batchmode -quit -projectPath . ^
///             -executeMethod InteractableObjectBuilder.BuildFromCommandLine
///
/// Before this, the things the game asks you to walk into or pick up were not
/// really there:
///
///   - The scene exits had ZERO renderers. The way to the next level was an
///     invisible trigger volume with a point light floating in it. The
///     objective said "walk into the exit" and there was nothing to walk into.
///   - The Time Lens was a 14 cm sphere. The Time Shards were 10 cm cubes.
///     In a deliberately dim museum those are specks, not objects, and they
///     look like whatever primitive they happen to be rather than the thing
///     they are named after.
///
/// So: the exits become framed paintings you step through, and each pickup
/// becomes a recognisable object built from primitives - a lens with a brass
/// ring and a glass disc, an hourglass with two bulbs and three posts, a
/// faceted shard, a toothed gear.
///
/// Runs late in the rebuild, after CollectibleLookBuilder has created the
/// bobbing "Beacon" holder, and replaces the placeholder shape inside it.
///
/// Idempotent.
/// </summary>
public static class InteractableObjectBuilder
{
    private static readonly string[] ScenePaths =
    {
        "Assets/Scenes/MuseumNight.unity",
        "Assets/Scenes/FrozenCity.unity",
        "Assets/Scenes/ClockCore.unity",
    };

    [MenuItem("Museum of Time/Build Interactable Objects")]
    public static void BuildMenu() { Build(); }

    public static void BuildFromCommandLine() { Build(); }

    private static void Build()
    {
        foreach (string scenePath in ScenePaths)
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(scenePath) == null) { continue; }

            Scene scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);

            int portals = BuildExitPortals();
            int pickups = BuildPickupModels();

            BuildCollectorLabel();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);

            Debug.Log("INTERACTABLES OK: " + scene.name +
                      " (" + portals + " portals, " + pickups + " pickups)");
        }

        AssetDatabase.SaveAssets();
        Debug.Log("=== INTERACTABLE OBJECTS COMPLETE ===");
    }

    // ==================================================================
    // The way out: a framed painting you step through.
    // ==================================================================

    private static int BuildExitPortals()
    {
        int built = 0;

        foreach (SceneExitTrigger exit in Object.FindObjectsByType<SceneExitTrigger>(
                     FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            GameObject holder = Replace(exit.gameObject, "ExitPortal");

            // Which way does the player approach from? Every exit in this game
            // is reached from the -Z side, and the frame reads best facing the
            // room rather than the wall.
            holder.transform.localRotation = Quaternion.identity;

            Material frameMat = Load("Assets/Materials/Museum/MuseumBrass.mat");
            Material canvasMat = Emissive("PortalCanvas",
                new Color(0.30f, 0.55f, 0.95f), 0.85f);

            const float width = 2.6f;
            const float height = 3.2f;
            const float bar = 0.22f;

            // The canvas: what the player actually walks into.
            GameObject canvas = Cube(holder, "PortalCanvas",
                new Vector3(0f, 0f, 0f),
                new Vector3(width - bar, height - bar, 0.08f), canvasMat);

            canvas.transform.localRotation = Quaternion.identity;

            // The frame around it.
            Cube(holder, "FrameTop", new Vector3(0f, (height * 0.5f) - (bar * 0.5f), 0f),
                 new Vector3(width, bar, 0.3f), frameMat);
            Cube(holder, "FrameBottom", new Vector3(0f, (-height * 0.5f) + (bar * 0.5f), 0f),
                 new Vector3(width, bar, 0.3f), frameMat);
            Cube(holder, "FrameLeft", new Vector3((-width * 0.5f) + (bar * 0.5f), 0f, 0f),
                 new Vector3(bar, height, 0.3f), frameMat);
            Cube(holder, "FrameRight", new Vector3((width * 0.5f) - (bar * 0.5f), 0f, 0f),
                 new Vector3(bar, height, 0.3f), frameMat);

            // A light so the frame reads in a dark room.
            var glow = new GameObject("PortalGlow");
            glow.transform.SetParent(holder.transform, false);
            glow.transform.localPosition = new Vector3(0f, 0f, -0.8f);

            Light light = glow.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = new Color(0.45f, 0.72f, 1f);
            light.intensity = 9f;
            light.range = 12f;
            light.shadows = LightShadows.None;

            // And a plate naming where it goes.
            string destination = DestinationOf(exit);

            GameObject plate = new GameObject("PortalLabel");
            plate.transform.SetParent(holder.transform, false);
            plate.transform.localPosition = new Vector3(0f, (height * 0.5f) + 0.45f, -0.1f);

            var text = plate.AddComponent<TextMeshPro>();
            text.text = destination;
            text.fontSize = 1f;
            text.enableAutoSizing = true;
            text.fontSizeMin = 0.3f;
            text.fontSizeMax = 0.7f;
            text.alignment = TextAlignmentOptions.Center;
            text.color = new Color(0.96f, 0.97f, 1f);
            text.textWrappingMode = TextWrappingModes.NoWrap;

            var rect = plate.GetComponent<RectTransform>();
            if (rect != null) { rect.sizeDelta = new Vector2(5f, 1.2f); }

            plate.AddComponent<WorldSignpost>();

            built++;
        }

        return built;
    }

    private static string DestinationOf(SceneExitTrigger exit)
    {
        var so = new SerializedObject(exit);
        SerializedProperty p = so.FindProperty("targetScene");

        string target = p != null ? p.stringValue : "";

        switch (target)
        {
            case "FrozenCity": return "TO THE FROZEN CITY";
            case "ClockCore": return "TO THE CLOCK CORE";
            default: return "WAY OUT";
        }
    }

    /// <summary>
    /// Puts the fight's current rule on the boss itself.
    ///
    /// The objective banner explains the phase, but it is at the top of the
    /// screen while the player is looking at the Collector - and when the
    /// shield breaks the rule changes silently, so orbs that just worked stop
    /// working with nothing to say why.
    /// </summary>
    private static void BuildCollectorLabel()
    {
        Collector collector = Object.FindFirstObjectByType<Collector>();

        if (collector == null) { return; }

        Transform existing = collector.transform.Find("PhaseLabel");
        if (existing != null) { Object.DestroyImmediate(existing.gameObject); }

        var go = new GameObject("PhaseLabel");
        go.transform.SetParent(collector.transform, false);
        go.transform.localPosition = new Vector3(0f, 2.6f, 0f);

        var text = go.AddComponent<TextMeshPro>();
        text.text = "";
        text.fontSize = 1f;
        text.alignment = TextAlignmentOptions.Center;
        text.richText = true;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.color = new Color(0.96f, 0.97f, 1f);

        var rect = go.GetComponent<RectTransform>();
        if (rect != null) { rect.sizeDelta = new Vector2(9f, 2.4f); }

        var component = go.AddComponent<CollectorPhaseLabel>();

        var so = new SerializedObject(component);
        SerializedProperty p = so.FindProperty("collector");
        if (p != null) { p.objectReferenceValue = collector; }
        so.ApplyModifiedPropertiesWithoutUndo();

        // A forgiving catch area, so a near miss with an arcing projectile
        // counts. It is a trigger, so it does not change where anyone can walk.
        Transform oldVolume = collector.transform.Find("HitVolume");
        if (oldVolume != null) { Object.DestroyImmediate(oldVolume.gameObject); }

        var volume = new GameObject("HitVolume");
        volume.transform.SetParent(collector.transform, false);
        volume.transform.localPosition = Vector3.zero;

        SphereCollider sphere = volume.AddComponent<SphereCollider>();
        sphere.isTrigger = true;
        sphere.radius = 1.9f;

        var hit = volume.AddComponent<CollectorHitVolume>();

        var vso = new SerializedObject(hit);
        SerializedProperty vp = vso.FindProperty("collector");
        if (vp != null) { vp.objectReferenceValue = collector; }
        vso.ApplyModifiedPropertiesWithoutUndo();

        Debug.Log("COLLECTOR LABEL + HIT VOLUME OK");
    }

    // ==================================================================
    // The things you pick up.
    // ==================================================================

    private static int BuildPickupModels()
    {
        int built = 0;

        foreach (MonoBehaviour mb in Object.FindObjectsByType<MonoBehaviour>(
                     FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (!(mb is IInteractable)) { continue; }

            string typeName = mb.GetType().Name;

            // One collider, correctly sized, whatever earlier builders left.
            //
            // Rebuilds had stacked eighteen BoxColliders on the gear, the
            // largest 2.2 x 5.9 m. Running last means this cleans up after all
            // of them rather than guessing which builder is responsible.
            NormaliseCollider(mb.gameObject);

            // The holder CollectibleLookBuilder made, which bobs and spins.
            Transform beacon = mb.transform.Find("Beacon");
            if (beacon == null) { continue; }

            Transform placeholder = beacon.Find("Visual");
            if (placeholder != null) { Object.DestroyImmediate(placeholder.gameObject); }

            var model = new GameObject("Visual");
            model.transform.SetParent(beacon, false);
            model.layer = mb.gameObject.layer;

            // Pickups carry their own scale, so build in metres and divide the
            // parent's contribution back out.
            Vector3 lossy = beacon.lossyScale;
            model.transform.localScale = new Vector3(
                1f / Mathf.Max(0.0001f, Mathf.Abs(lossy.x)),
                1f / Mathf.Max(0.0001f, Mathf.Abs(lossy.y)),
                1f / Mathf.Max(0.0001f, Mathf.Abs(lossy.z)));

            if (mb is ShardPickup)
            {
                BuildShard(model);
            }
            else if (mb is ItemPickup item)
            {
                if (IsLens(item)) { BuildLens(model); }
                else { BuildHourglass(model); }
            }
            else if (typeName == "GearPickup")
            {
                BuildGear(model);
            }
            else
            {
                // Plaques and doors keep their own art.
                Object.DestroyImmediate(model);
                continue;
            }

            EditorUtility.SetDirty(mb.gameObject);
            built++;
        }

        return built;
    }

    /// <summary>
    /// Leaves exactly one collider on an interactable, sized in metres.
    /// </summary>
    private static void NormaliseCollider(GameObject go)
    {
        List<Collider> colliders = new List<Collider>(go.GetComponents<Collider>());

        for (int i = colliders.Count - 1; i >= 1; i--)
        {
            Object.DestroyImmediate(colliders[i]);
        }

        var box = go.GetComponent<BoxCollider>();

        if (box == null)
        {
            foreach (Collider stale in go.GetComponents<Collider>())
            {
                Object.DestroyImmediate(stale);
            }

            box = go.AddComponent<BoxCollider>();
        }

        box.isTrigger = false;
        box.center = Vector3.zero;

        // BoxCollider.size is local, and these objects carry their own scale,
        // so divide it back out to make the number mean metres.
        Vector3 lossy = go.transform.lossyScale;
        const float metres = 1.1f;

        box.size = new Vector3(
            metres / Mathf.Max(0.0001f, Mathf.Abs(lossy.x)),
            metres / Mathf.Max(0.0001f, Mathf.Abs(lossy.y)),
            metres / Mathf.Max(0.0001f, Mathf.Abs(lossy.z)));
    }

    /// <summary>A faceted crystal: two cones base to base.</summary>
    private static void BuildShard(GameObject root)
    {
        Material mat = Emissive("ShardCrystal", new Color(0.42f, 0.92f, 0.90f), 1.0f);

        // A tapering spire rather than a lump: each section is narrower than
        // the one below it, so the silhouette reads as a crystal even at a
        // distance and even before the colour is visible.
        GameObject baseBlock = Shape(root, "Base", PrimitiveType.Cube, mat);
        baseBlock.transform.localScale = new Vector3(0.26f, 0.26f, 0.26f);
        baseBlock.transform.localPosition = new Vector3(0f, -0.18f, 0f);
        baseBlock.transform.localRotation = Quaternion.Euler(0f, 45f, 0f);

        GameObject middle = Shape(root, "Middle", PrimitiveType.Cube, mat);
        middle.transform.localScale = new Vector3(0.19f, 0.34f, 0.19f);
        middle.transform.localPosition = new Vector3(0f, 0.08f, 0f);
        middle.transform.localRotation = Quaternion.Euler(0f, 45f, 0f);

        GameObject tip = Shape(root, "Tip", PrimitiveType.Cube, mat);
        tip.transform.localScale = new Vector3(0.10f, 0.30f, 0.10f);
        tip.transform.localPosition = new Vector3(0f, 0.38f, 0f);
        tip.transform.localRotation = Quaternion.Euler(0f, 45f, 0f);

        root.transform.localRotation = Quaternion.Euler(14f, 0f, 10f);
    }

    /// <summary>A lens: a brass ring holding a glass disc, on a small foot.</summary>
    private static void BuildLens(GameObject root)
    {
        Material brass = Emissive("LensBrass", new Color(0.95f, 0.76f, 0.35f), 0.45f);
        Material glass = Emissive("LensGlass", new Color(0.50f, 0.82f, 1f), 0.95f);

        // The ring, made of eight short bars around a circle.
        for (int i = 0; i < 8; i++)
        {
            float angle = i * 45f;
            var segment = Shape(root, "Ring" + i, PrimitiveType.Cube, brass);

            float radians = angle * Mathf.Deg2Rad;
            segment.transform.localPosition = new Vector3(
                Mathf.Cos(radians) * 0.30f, Mathf.Sin(radians) * 0.30f, 0f);

            segment.transform.localRotation = Quaternion.Euler(0f, 0f, angle);
            segment.transform.localScale = new Vector3(0.10f, 0.26f, 0.10f);
        }

        GameObject disc = Shape(root, "Glass", PrimitiveType.Cylinder, glass);
        disc.transform.localScale = new Vector3(0.28f, 0.03f, 0.28f);
        disc.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);

        GameObject handle = Shape(root, "Handle", PrimitiveType.Cube, brass);
        handle.transform.localPosition = new Vector3(0f, -0.46f, 0f);
        handle.transform.localScale = new Vector3(0.09f, 0.34f, 0.09f);
    }

    /// <summary>An hourglass: two bulbs, two plates, three posts.</summary>
    private static void BuildHourglass(GameObject root)
    {
        Material brass = Emissive("HourglassBrass", new Color(0.95f, 0.78f, 0.34f), 0.45f);
        Material sand = Emissive("HourglassSand", new Color(1f, 0.86f, 0.45f), 0.95f);

        GameObject upper = Shape(root, "UpperBulb", PrimitiveType.Cylinder, sand);
        upper.transform.localScale = new Vector3(0.30f, 0.16f, 0.30f);
        upper.transform.localPosition = new Vector3(0f, 0.18f, 0f);

        GameObject waist = Shape(root, "Waist", PrimitiveType.Cylinder, sand);
        waist.transform.localScale = new Vector3(0.07f, 0.06f, 0.07f);

        GameObject lower = Shape(root, "LowerBulb", PrimitiveType.Cylinder, sand);
        lower.transform.localScale = new Vector3(0.30f, 0.16f, 0.30f);
        lower.transform.localPosition = new Vector3(0f, -0.18f, 0f);

        GameObject top = Shape(root, "TopPlate", PrimitiveType.Cylinder, brass);
        top.transform.localScale = new Vector3(0.40f, 0.03f, 0.40f);
        top.transform.localPosition = new Vector3(0f, 0.36f, 0f);

        GameObject bottom = Shape(root, "BottomPlate", PrimitiveType.Cylinder, brass);
        bottom.transform.localScale = new Vector3(0.40f, 0.03f, 0.40f);
        bottom.transform.localPosition = new Vector3(0f, -0.36f, 0f);

        for (int i = 0; i < 3; i++)
        {
            float radians = i * 120f * Mathf.Deg2Rad;
            GameObject post = Shape(root, "Post" + i, PrimitiveType.Cube, brass);

            post.transform.localPosition = new Vector3(
                Mathf.Cos(radians) * 0.33f, 0f, Mathf.Sin(radians) * 0.33f);

            post.transform.localScale = new Vector3(0.05f, 0.74f, 0.05f);
        }
    }

    /// <summary>A gear: a hub, a rim, and eight teeth.</summary>
    private static void BuildGear(GameObject root)
    {
        Material metal = Emissive("GearMetal", new Color(0.95f, 0.72f, 0.32f), 0.55f);

        GameObject rim = Shape(root, "Rim", PrimitiveType.Cylinder, metal);
        rim.transform.localScale = new Vector3(0.80f, 0.10f, 0.80f);
        rim.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);

        GameObject hub = Shape(root, "Hub", PrimitiveType.Cylinder, metal);
        hub.transform.localScale = new Vector3(0.30f, 0.14f, 0.30f);
        hub.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);

        for (int i = 0; i < 8; i++)
        {
            float angle = i * 45f;
            float radians = angle * Mathf.Deg2Rad;

            GameObject tooth = Shape(root, "Tooth" + i, PrimitiveType.Cube, metal);

            tooth.transform.localPosition = new Vector3(
                Mathf.Cos(radians) * 0.46f, Mathf.Sin(radians) * 0.46f, 0f);

            tooth.transform.localRotation = Quaternion.Euler(0f, 0f, angle);
            tooth.transform.localScale = new Vector3(0.22f, 0.18f, 0.22f);
        }
    }

    // ==================================================================

    private static GameObject Shape(GameObject parent, string name,
                                    PrimitiveType type, Material material)
    {
        GameObject go = GameObject.CreatePrimitive(type);
        go.name = name;
        go.transform.SetParent(parent.transform, false);
        go.layer = parent.layer;

        // The pickup's own collider does the raycasting; extra colliders here
        // would let the look-cast hit a decoration instead of the script.
        Collider collider = go.GetComponent<Collider>();
        if (collider != null) { Object.DestroyImmediate(collider); }

        go.GetComponent<MeshRenderer>().sharedMaterial = material;
        return go;
    }

    private static GameObject Cube(GameObject parent, string name, Vector3 localPosition,
                                   Vector3 localScale, Material material)
    {
        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        go.transform.SetParent(parent.transform, false);
        go.transform.localPosition = localPosition;
        go.transform.localScale = localScale;

        Collider collider = go.GetComponent<Collider>();
        if (collider != null) { Object.DestroyImmediate(collider); }

        if (material != null)
        {
            go.GetComponent<MeshRenderer>().sharedMaterial = material;
        }

        return go;
    }

    private static GameObject Replace(GameObject parent, string childName)
    {
        Transform existing = parent.transform.Find(childName);
        if (existing != null) { Object.DestroyImmediate(existing.gameObject); }

        var go = new GameObject(childName);
        go.transform.SetParent(parent.transform, false);
        go.transform.localPosition = Vector3.zero;
        return go;
    }

    private static bool IsLens(ItemPickup item)
    {
        var so = new SerializedObject(item);
        SerializedProperty p = so.FindProperty("item");

        return p == null || p.enumValueIndex == (int)ItemPickup.Kind.TimeLens;
    }

    private static Material Load(string path)
    {
        return AssetDatabase.LoadAssetAtPath<Material>(path);
    }

    private static Material Emissive(string name, Color colour, float intensity)
    {
        const string folder = "Assets/Materials/Dressing";
        string path = folder + "/" + name + ".mat";

        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);

        if (mat == null)
        {
            mat = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = name };
            AssetDatabase.CreateAsset(mat, path);
        }

        mat.SetColor("_BaseColor", colour);
        mat.SetFloat("_Smoothness", 0.8f);
        mat.SetFloat("_Metallic", 0.2f);

        mat.EnableKeyword("_EMISSION");
        mat.SetColor("_EmissionColor", colour * intensity);
        mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;

        EditorUtility.SetDirty(mat);
        return mat;
    }
}
