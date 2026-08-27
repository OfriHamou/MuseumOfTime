using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Turns every collectible into something a player can actually find and take.
///
///   Unity.exe -batchmode -quit -projectPath . ^
///             -executeMethod CollectibleLookBuilder.BuildFromCommandLine
///
/// Before this, every pickup in the game - both story items and every Time
/// Shard - was the same 0.4 x 0.4 x 0.1 untextured plate with a matching
/// collider, no light, no label and no distinguishing shape, sitting in a
/// deliberately dim museum. The interaction logic was fine; there was simply
/// nothing to see and nothing saying which key to press, and the hit box was
/// small enough that the look-ray missed it from most angles.
///
/// Each pickup now gets a distinct floating, spinning, emissive shape, its own
/// point light, a world-space label naming it and the key, and a collider big
/// enough to aim at.
///
/// Idempotent.
/// </summary>
public static class CollectibleLookBuilder
{
    private static readonly string[] ScenePaths =
    {
        "Assets/Scenes/MuseumNight.unity",
        "Assets/Scenes/FrozenCity.unity",
        "Assets/Scenes/ClockCore.unity",
    };

    [MenuItem("Museum of Time/Build Collectible Look")]
    public static void BuildMenu() { Build(); }

    public static void BuildFromCommandLine() { Build(); }

    private static void Build()
    {
        Material shardMat = Emissive("PickupShard", new Color(0.42f, 0.92f, 0.90f), 3.2f);
        Material lensMat = Emissive("PickupLens", new Color(0.50f, 0.78f, 1f), 3.6f);
        Material hourglassMat = Emissive("PickupHourglass", new Color(1f, 0.80f, 0.36f), 3.6f);
        Material plaqueMat = Emissive("PickupPlaque", new Color(0.95f, 0.85f, 0.55f), 1.2f);

        foreach (string scenePath in ScenePaths)
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(scenePath) == null) { continue; }

            Scene scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            int count = 0;

            foreach (MonoBehaviour mb in Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (!(mb is IInteractable)) { continue; }

                Material mat = plaqueMat;
                PrimitiveType shape = PrimitiveType.Cube;
                float scale = 0.34f;
                Color lightColour = new Color(0.95f, 0.85f, 0.55f);

                if (mb is ShardPickup)
                {
                    mat = shardMat;
                    shape = PrimitiveType.Cube;
                    scale = 0.30f;
                    lightColour = new Color(0.42f, 0.92f, 0.90f);
                }
                else if (mb is ItemPickup item)
                {
                    bool isLens = IsLens(item);
                    mat = isLens ? lensMat : hourglassMat;
                    shape = isLens ? PrimitiveType.Sphere : PrimitiveType.Capsule;
                    scale = 0.42f;
                    lightColour = isLens
                        ? new Color(0.50f, 0.78f, 1f)
                        : new Color(1f, 0.80f, 0.36f);
                }
                else if (mb.GetType().Name == "GearPickup")
                {
                    mat = Emissive("PickupGear", new Color(0.95f, 0.72f, 0.32f), 2.6f);
                    shape = PrimitiveType.Cylinder;
                    scale = 0.40f;
                    lightColour = new Color(0.95f, 0.72f, 0.32f);
                }
                else if (mb.GetType().Name == "GearSocket")
                {
                    // The socket is part of the tower, so it keeps its own
                    // mesh - it only needs a light and a label saying what it
                    // is and which key fits the gear into it.
                    LabelOnly(mb.gameObject, new Color(0.95f, 0.72f, 0.32f));
                    count++;
                    continue;
                }
                else if (!(mb is ExhibitPlaque))
                {
                    // Doors and the like keep their own art.
                    continue;
                }

                Dress(mb.gameObject, mat, shape, scale, lightColour, mb is ExhibitPlaque);
                count++;
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);

            Debug.Log("COLLECTIBLE OK: " + scene.name + " (" + count + " pickups dressed)");
        }

        AssetDatabase.SaveAssets();
        Debug.Log("=== COLLECTIBLE LOOK COMPLETE ===");
    }

    private static bool IsLens(ItemPickup item)
    {
        var so = new SerializedObject(item);
        SerializedProperty p = so.FindProperty("item");

        return p == null || p.enumValueIndex == (int)ItemPickup.Kind.TimeLens;
    }

    /// <summary>
    /// Adds a light and a world label to an interactable that must keep its
    /// existing mesh - the gear socket is part of the clock tower.
    /// </summary>
    private static void LabelOnly(GameObject go, Color lightColour)
    {
        Transform existing = go.transform.Find("Label");
        if (existing != null) { Object.DestroyImmediate(existing.gameObject); }

        Transform oldGlow = go.transform.Find("Glow");
        if (oldGlow != null) { Object.DestroyImmediate(oldGlow.gameObject); }

        var lightGo = new GameObject("Glow");
        lightGo.transform.SetParent(go.transform, false);
        lightGo.transform.localPosition = Vector3.zero;

        Light light = lightGo.AddComponent<Light>();
        light.type = LightType.Point;
        light.color = lightColour;
        light.intensity = 4f;
        light.range = 8f;
        light.shadows = LightShadows.None;

        var labelGo = new GameObject("Label");
        labelGo.transform.SetParent(go.transform, false);
        labelGo.transform.localPosition = new Vector3(0f, 1.1f, 0f);

        var text = labelGo.AddComponent<TextMeshPro>();
        text.text = "";
        text.fontSize = 1f;
        text.alignment = TextAlignmentOptions.Center;
        text.richText = true;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.color = new Color(0.96f, 0.97f, 1f);

        var rect = labelGo.GetComponent<RectTransform>();
        if (rect != null) { rect.sizeDelta = new Vector2(5f, 1.2f); }

        var beacon = go.GetComponent<PickupBeacon>();
        if (beacon == null) { beacon = go.AddComponent<PickupBeacon>(); }

        var so = new SerializedObject(beacon);
        Set(so, "visual", null);      // keeps its own mesh: no bob, no spin
        Set(so, "label", text);
        so.ApplyModifiedPropertiesWithoutUndo();

        EditorUtility.SetDirty(go);
    }

    private static void Dress(GameObject go, Material material, PrimitiveType shape,
                              float scale, Color lightColour, bool isPlaque)
    {
        // The original flat plate renderer is replaced, not added to.
        var oldRenderer = go.GetComponent<MeshRenderer>();
        if (oldRenderer != null) { Object.DestroyImmediate(oldRenderer); }

        var oldFilter = go.GetComponent<MeshFilter>();
        if (oldFilter != null) { Object.DestroyImmediate(oldFilter); }

        Transform existing = go.transform.Find("Beacon");
        if (existing != null) { Object.DestroyImmediate(existing.gameObject); }

        var beacon = new GameObject("Beacon");
        beacon.transform.SetParent(go.transform, false);
        beacon.transform.localPosition = Vector3.zero;
        beacon.layer = go.layer;

        // ---- The visible object ------------------------------------------
        GameObject visual = GameObject.CreatePrimitive(shape);
        visual.name = "Visual";
        visual.layer = go.layer;
        visual.transform.SetParent(beacon.transform, false);
        visual.transform.localScale = Vector3.one * scale;
        visual.transform.localRotation = Quaternion.Euler(45f, 0f, 45f);
        visual.GetComponent<MeshRenderer>().sharedMaterial = material;

        // The pickup's own collider does the raycasting; a second one here
        // would let the look-ray hit the visual instead and miss the script.
        Collider visualCollider = visual.GetComponent<Collider>();
        if (visualCollider != null) { Object.DestroyImmediate(visualCollider); }

        // ---- Something to see it by ---------------------------------------
        if (!isPlaque)
        {
            var lightGo = new GameObject("Glow");
            lightGo.transform.SetParent(beacon.transform, false);
            lightGo.transform.localPosition = Vector3.zero;

            Light light = lightGo.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = lightColour;
            light.intensity = 4.5f;
            light.range = 7f;
            light.shadows = LightShadows.None;
        }

        // ---- The label -----------------------------------------------------
        var labelGo = new GameObject("Label");
        labelGo.transform.SetParent(go.transform, false);
        labelGo.transform.localPosition = new Vector3(0f, 0.62f, 0f);

        var text = labelGo.AddComponent<TextMeshPro>();
        text.text = "";
        text.fontSize = 1f;
        text.alignment = TextAlignmentOptions.Center;
        text.richText = true;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.color = new Color(0.96f, 0.97f, 1f);

        var labelRect = labelGo.GetComponent<RectTransform>();
        if (labelRect != null) { labelRect.sizeDelta = new Vector2(5f, 1.2f); }

        // ---- A hit box big enough to aim at --------------------------------
        var box = go.GetComponent<BoxCollider>();
        if (box == null) { box = go.AddComponent<BoxCollider>(); }

        box.isTrigger = false;
        box.center = Vector3.zero;

        // BoxCollider.size is LOCAL. These pickup objects carry their own
        // scale, so a literal 0.7 became 0.28 in world space - a target under
        // 30 cm across in a dim room. Dividing by lossyScale makes the number
        // mean metres.
        Vector3 lossy = go.transform.lossyScale;
        float world = Mathf.Max(0.8f, scale * 2.2f);

        box.size = new Vector3(
            world / Mathf.Max(0.0001f, Mathf.Abs(lossy.x)),
            world / Mathf.Max(0.0001f, Mathf.Abs(lossy.y)),
            world / Mathf.Max(0.0001f, Mathf.Abs(lossy.z)));

        // ---- Behaviour ------------------------------------------------------
        var beaconComponent = go.GetComponent<PickupBeacon>();
        if (beaconComponent == null) { beaconComponent = go.AddComponent<PickupBeacon>(); }

        var so = new SerializedObject(beaconComponent);
        Set(so, "visual", beacon.transform);
        Set(so, "label", text);
        so.ApplyModifiedPropertiesWithoutUndo();

        EditorUtility.SetDirty(go);
    }

    private static void Set(SerializedObject so, string field, Object value)
    {
        SerializedProperty p = so.FindProperty(field);
        if (p != null) { p.objectReferenceValue = value; }
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
        mat.SetFloat("_Smoothness", 0.85f);
        mat.SetFloat("_Metallic", 0f);

        mat.EnableKeyword("_EMISSION");
        mat.SetColor("_EmissionColor", colour * intensity);
        mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;

        EditorUtility.SetDirty(mat);
        return mat;
    }
}
