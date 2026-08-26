using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Phase 5, Step 5.3: the minimap, in MuseumNight - T18 asks for one whole
/// scene with full coverage, and this is the scene that has a Player to
/// follow.
///
///   Unity.exe -batchmode -quit -projectPath . ^
///             -executeMethod MinimapBuilder.BuildFromCommandLine
///
/// The camera renders the "Minimap" layer ONLY (an allow-list, not a
/// deny-list), which is also what keeps hidden Time Anchors off it for free:
/// nothing is ever put on that layer unless it is meant to be seen from
/// above. Excluded from the CinemachineBrain camera's own mask so the
/// player-marker never leaks into third/first person view, and never
/// promoted to a CinemachineCamera itself, so it stays the third camera
/// alongside the two T19 asks for, not a fourth gameplay view.
/// </summary>
public static class MinimapBuilder
{
    private const string ScenePath = "Assets/Scenes/MuseumNight.unity";
    private const string RenderTexturePath = "Assets/Textures/MinimapRT.renderTexture";
    private const string MinimapLayerName = "Minimap";

    [MenuItem("Museum of Time/Build Minimap")]
    public static void BuildMenu() { Build(); }

    public static void BuildFromCommandLine() { Build(); }

    private static void Build()
    {
        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

        GameObject player = GameObject.Find("Player");
        if (player == null)
        {
            Debug.LogError("MINIMAP FAILED: no Player in the scene.");
            return;
        }

        int minimapLayer = LayerMask.NameToLayer(MinimapLayerName);
        if (minimapLayer < 0)
        {
            Debug.LogError(
                "MINIMAP FAILED: no '" + MinimapLayerName + "' layer defined in " +
                "Project Settings > Tags and Layers.");
            return;
        }

        RenderTexture renderTexture = GetOrCreateRenderTexture();

        // ---- The marker: the only thing the minimap camera actually sees ----
        GameObject marker = MenuUIBuilder.FindOrCreate("MinimapMarker", player);
        marker.layer = minimapLayer;
        marker.transform.localPosition = new Vector3(0f, 1f, 0f);
        marker.transform.localScale = new Vector3(0.6f, 0.1f, 0.9f);
        marker.transform.localRotation = Quaternion.identity;

        MeshFilter filter = MenuUIBuilder.Ensure<MeshFilter>(marker);
        if (filter.sharedMesh == null)
        {
            filter.sharedMesh = GetPrimitiveMesh(PrimitiveType.Cube);
        }

        MeshRenderer markerRenderer = MenuUIBuilder.Ensure<MeshRenderer>(marker);
        markerRenderer.sharedMaterial = GetOrCreateUnlitMaterial();

        Collider markerCollider = marker.GetComponent<Collider>();
        if (markerCollider != null)
        {
            Object.DestroyImmediate(markerCollider);
        }

        // ---- The camera: orthographic, top-down, Minimap layer only ---------
        GameObject cameraGo = MenuUIBuilder.FindOrCreate("MinimapCamera", null);
        cameraGo.tag = "Untagged";

        Camera cam = MenuUIBuilder.Ensure<Camera>(cameraGo);
        cam.orthographic = true;
        cam.orthographicSize = 16f;
        cam.cullingMask = 1 << minimapLayer;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.05f, 0.05f, 0.08f, 1f);
        cam.targetTexture = renderTexture;
        cam.nearClipPlane = 1f;
        cam.farClipPlane = 60f;

        // The URP additional-camera-data companion component; without it the
        // camera renders with default URP settings, which is fine here, but
        // Ensure<> still needs to run so later re-runs do not add a duplicate.
        MenuUIBuilder.Ensure<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>(cameraGo);

        MinimapController controller = MenuUIBuilder.Ensure<MinimapController>(cameraGo);
        var controllerSo = new SerializedObject(controller);
        controllerSo.FindProperty("target").objectReferenceValue = player.transform;
        controllerSo.FindProperty("height").floatValue = 30f;
        controllerSo.ApplyModifiedPropertiesWithoutUndo();

        cameraGo.transform.position = player.transform.position + new Vector3(0f, 30f, 0f);
        cameraGo.transform.rotation = Quaternion.Euler(90f, 0f, 0f);

        // ---- Keep the marker out of the normal gameplay cameras -------------
        GameObject mainCamera = GameObject.Find("MainCamera");
        if (mainCamera != null)
        {
            Camera gameplayCam = mainCamera.GetComponent<Camera>();
            if (gameplayCam != null)
            {
                gameplayCam.cullingMask &= ~(1 << minimapLayer);
            }
        }

        // ---- The on-screen display, bottom-right so it clears the HUD -------
        GameObject canvasGo = MenuUIBuilder.FindOrCreateCanvas("HUDCanvas");

        GameObject frame = MenuUIBuilder.FindOrCreate("MinimapFrame", canvasGo);
        RectTransform frameRect = MenuUIBuilder.EnsureRect(frame);
        frameRect.anchorMin = frameRect.anchorMax = new Vector2(1f, 0f);
        frameRect.pivot = new Vector2(1f, 0f);
        frameRect.anchoredPosition = new Vector2(-24, 24);
        frameRect.sizeDelta = new Vector2(190, 190);
        MenuUIBuilder.Ensure<Image>(frame).color = new Color(1f, 1f, 1f, 0.25f);

        GameObject display = MenuUIBuilder.FindOrCreate("MinimapDisplay", frame);
        RectTransform displayRect = MenuUIBuilder.EnsureRect(display);
        displayRect.anchorMin = Vector2.zero;
        displayRect.anchorMax = Vector2.one;
        displayRect.offsetMin = new Vector2(5, 5);
        displayRect.offsetMax = new Vector2(-5, -5);

        RawImage rawImage = MenuUIBuilder.Ensure<RawImage>(display);
        rawImage.texture = renderTexture;

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);

        Debug.Log("MINIMAP OK: orthographic camera on the '" + MinimapLayerName +
                   "' layer only, following and rotating with Noa, always on.");
    }

    private static RenderTexture GetOrCreateRenderTexture()
    {
        RenderTexture existing = AssetDatabase.LoadAssetAtPath<RenderTexture>(RenderTexturePath);
        if (existing != null)
        {
            return existing;
        }

        var rt = new RenderTexture(512, 512, 16) { name = "MinimapRT" };
        AssetDatabase.CreateAsset(rt, RenderTexturePath);
        return rt;
    }

    private static Material unlitMaterialCache;

    private static Material GetOrCreateUnlitMaterial()
    {
        if (unlitMaterialCache != null)
        {
            return unlitMaterialCache;
        }

        const string path = "Assets/Materials/UI/MinimapMarker.mat";
        Material existing = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (existing != null)
        {
            unlitMaterialCache = existing;
            return existing;
        }

        Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
        var material = new Material(shader) { name = "MinimapMarker" };
        material.color = new Color(0.9f, 0.85f, 0.2f);

        System.IO.Directory.CreateDirectory("Assets/Materials/UI");
        AssetDatabase.CreateAsset(material, path);

        unlitMaterialCache = material;
        return material;
    }

    private static Mesh GetPrimitiveMesh(PrimitiveType type)
    {
        GameObject temp = GameObject.CreatePrimitive(type);
        Mesh mesh = temp.GetComponent<MeshFilter>().sharedMesh;
        Object.DestroyImmediate(temp);
        return mesh;
    }
}
