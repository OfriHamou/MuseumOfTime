using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Phase 5, Step 5.2: the HUD and the pause menu, both in MuseumNight - the
/// only scene with a Player to test them against.
///
///   Unity.exe -batchmode -quit -projectPath . ^
///             -executeMethod HudBuilder.BuildFromCommandLine
///
/// Idempotent, and reuses MenuUIBuilder's Canvas/EventSystem/button helpers.
/// </summary>
public static class HudBuilder
{
    /// <summary>
    /// Every gameplay scene, not just MuseumNight.
    ///
    /// This used to build the HUD in MuseumNight alone, which left FrozenCity
    /// and ClockCore with no health/energy/shard readout, no EventSystem and
    /// no pause menu at all - the player could not see their own state or
    /// pause for two of the three scenes.
    /// </summary>
    private static readonly string[] ScenePaths =
    {
        "Assets/Scenes/MuseumNight.unity",
        "Assets/Scenes/FrozenCity.unity",
        "Assets/Scenes/ClockCore.unity",
    };

    [MenuItem("Museum of Time/Build HUD and Pause Menu")]
    public static void BuildMenu() { Build(); }

    public static void BuildFromCommandLine() { Build(); }

    private static void Build()
    {
        foreach (string scenePath in ScenePaths)
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(scenePath) == null)
            {
                Debug.LogWarning("HUD: no scene at " + scenePath + ", skipped.");
                continue;
            }

            BuildForScene(scenePath);
        }
    }

    private static void BuildForScene(string scenePath)
    {
        Scene scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);

        MenuUIBuilder.EnsureEventSystem();

        GameObject canvasGo = MenuUIBuilder.FindOrCreateCanvas("HUDCanvas");
        Transform canvas = canvasGo.transform;

        // ---- Health / energy / shards / era, top-left ----------------------
        Image healthFill = CreateBar(canvas, "HealthBar", new Color(0.75f, 0.15f, 0.15f), new Vector2(30, -30));
        Image energyFill = CreateBar(canvas, "EnergyBar", new Color(0.15f, 0.45f, 0.75f), new Vector2(30, -70));

        TMP_Text shardText = CreateCornerText(
            canvas, "ShardCountText", "0", new Vector2(0, 1), new Vector2(30, -110));

        TMP_Text eraText = CreateCornerText(
            canvas, "EraText", "Present", new Vector2(0, 1), new Vector2(30, -150));

        // ---- Item icons, top-right ------------------------------------------
        GameObject lensIcon = CreateIcon(canvas, "TimeLensIcon", new Color(0.6f, 0.85f, 1f), new Vector2(-70, -30));
        GameObject hourglassIcon = CreateIcon(canvas, "HourglassIcon", new Color(1f, 0.85f, 0.4f), new Vector2(-30, -30));

        // ---- Detection meter, top-center, hidden until something is looking ----
        GameObject detectionRoot = MenuUIBuilder.FindOrCreate("DetectionMeter", canvasGo);
        RectTransform detectionRect = MenuUIBuilder.EnsureRect(detectionRoot);
        detectionRect.anchorMin = detectionRect.anchorMax = new Vector2(0.5f, 1f);
        detectionRect.pivot = new Vector2(0.5f, 1f);
        detectionRect.anchoredPosition = new Vector2(0, -30);
        detectionRect.sizeDelta = new Vector2(220, 22);
        MenuUIBuilder.Ensure<Image>(detectionRoot).color = new Color(0f, 0f, 0f, 0.4f);

        Image detectionFill = CreateFillChild(detectionRoot.transform, "Fill", new Color(0.85f, 0.2f, 0.2f));

        // ---- Pause panel -----------------------------------------------------
        GameObject pausePanel = MenuUIBuilder.FindOrCreate("PauseMenuPanel", canvasGo);
        RectTransform pauseRect = MenuUIBuilder.EnsureRect(pausePanel);
        pauseRect.anchorMin = pauseRect.anchorMax = new Vector2(0.5f, 0.5f);
        pauseRect.pivot = new Vector2(0.5f, 0.5f);
        pauseRect.anchoredPosition = Vector2.zero;
        pauseRect.sizeDelta = new Vector2(400, 420);
        MenuUIBuilder.Ensure<Image>(pausePanel).color = new Color(0f, 0f, 0f, 0.85f);

        Button resume = MenuUIBuilder.CreateButton(pausePanel.transform, "ResumeButton", "Resume", new Vector2(0, 140));
        Button restart = MenuUIBuilder.CreateButton(pausePanel.transform, "RestartButton", "Restart Scene", new Vector2(0, 60));
        Button controls = MenuUIBuilder.CreateButton(pausePanel.transform, "PauseControlsButton", "Controls", new Vector2(0, -20));
        Button mainMenu = MenuUIBuilder.CreateButton(pausePanel.transform, "PauseMainMenuButton", "Main Menu", new Vector2(0, -100));
        Button quit = MenuUIBuilder.CreateButton(pausePanel.transform, "PauseQuitButton", "Quit", new Vector2(0, -180));

        GameObject controlsPanel = MenuUIBuilder.FindOrCreate("PauseControlsPanel", canvasGo);
        RectTransform controlsRect = MenuUIBuilder.EnsureRect(controlsPanel);
        controlsRect.anchorMin = controlsRect.anchorMax = new Vector2(0.5f, 0.5f);
        controlsRect.pivot = new Vector2(0.5f, 0.5f);
        controlsRect.anchoredPosition = Vector2.zero;
        controlsRect.sizeDelta = new Vector2(560, 420);
        MenuUIBuilder.Ensure<Image>(controlsPanel).color = new Color(0f, 0f, 0f, 0.9f);

        const string controlsText =
            "Move  WASD\nRun  Shift\nJump  Space\nLook  Mouse\n" +
            "Interact  E\nThrow Chrono Orb  Left Mouse\n" +
            "Era Back / Forward  Q / R\nSlow Time  Ctrl\n" +
            "Camera Toggle  C\nTime Journal  Tab\nPause  Escape";

        MenuUIBuilder.CreateText(controlsPanel.transform, "ControlsText", controlsText, 24,
            new Vector2(0, 30), new Vector2(520, 320));

        // The panel covers PauseControlsButton once open, the same way the
        // Main Menu's does - a dedicated way back is not optional here.
        Button controlsBack = MenuUIBuilder.CreateButton(
            controlsPanel.transform, "PauseControlsBackButton", "Back", new Vector2(0, -170));

        // ---- Wire the controller components ----------------------------------
        GameObject player = GameObject.Find("Player");
        if (player == null)
        {
            Debug.LogError("HUD FAILED: no Player in " + scene.name + ".");
            return;
        }

        HUDController hud = MenuUIBuilder.Ensure<HUDController>(player);
        var hudSo = new SerializedObject(hud);
        hudSo.FindProperty("healthFill").objectReferenceValue = healthFill;
        hudSo.FindProperty("energyFill").objectReferenceValue = energyFill;
        hudSo.FindProperty("shardText").objectReferenceValue = shardText;
        hudSo.FindProperty("eraText").objectReferenceValue = eraText;
        hudSo.FindProperty("timeLensIcon").objectReferenceValue = lensIcon;
        hudSo.FindProperty("hourglassIcon").objectReferenceValue = hourglassIcon;
        hudSo.FindProperty("detectionMeterRoot").objectReferenceValue = detectionRoot;
        hudSo.FindProperty("detectionFill").objectReferenceValue = detectionFill;
        hudSo.ApplyModifiedPropertiesWithoutUndo();

        GameObject uiManager = MenuUIBuilder.FindOrCreate("UIManager", null);
        SceneLoader loader = MenuUIBuilder.Ensure<SceneLoader>(uiManager);
        PauseMenuController pause = MenuUIBuilder.Ensure<PauseMenuController>(uiManager);

        // Wire the FIELD REFERENCES only. PauseMenuController.Awake() attaches
        // the actual onClick.AddListener(...) calls itself, at runtime, every
        // time the scene loads - the same pattern MainMenuController and
        // VictoryScreenController already use correctly.
        //
        // The previous version of this method called button.onClick.AddListener(...)
        // directly, from this Editor batch-mode script. That registers a
        // NON-PERSISTENT UnityEvent listener that lives only in the memory of
        // the batch process that made it and is never serialized into the
        // saved scene - the instant that process exits, the listener is gone,
        // and every pause-menu button had zero working listeners the moment
        // anyone actually pressed Play. Found in manual testing.
        var pauseSo = new SerializedObject(pause);
        pauseSo.FindProperty("panel").objectReferenceValue = pausePanel;
        pauseSo.FindProperty("sceneLoader").objectReferenceValue = loader;
        pauseSo.FindProperty("controlsPanel").objectReferenceValue = controlsPanel;
        pauseSo.FindProperty("resumeButton").objectReferenceValue = resume;
        pauseSo.FindProperty("restartButton").objectReferenceValue = restart;
        pauseSo.FindProperty("controlsButton").objectReferenceValue = controls;
        pauseSo.FindProperty("controlsBackButton").objectReferenceValue = controlsBack;
        pauseSo.FindProperty("mainMenuButton").objectReferenceValue = mainMenu;
        pauseSo.FindProperty("quitButton").objectReferenceValue = quit;
        pauseSo.ApplyModifiedPropertiesWithoutUndo();

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);

        Debug.Log("HUD OK (" + scene.name + "): health/energy/shards/era/items/" +
                   "detection meter, pause menu with Resume/Restart/Controls/" +
                   "Main Menu/Quit.");
    }

    private static Image CreateBar(Transform canvas, string name, Color color, Vector2 anchoredPosition)
    {
        GameObject go = MenuUIBuilder.FindOrCreate(name, canvas.gameObject);
        RectTransform rt = MenuUIBuilder.EnsureRect(go);
        rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
        rt.pivot = new Vector2(0f, 1f);
        rt.anchoredPosition = anchoredPosition;
        rt.sizeDelta = new Vector2(220, 28);

        Image image = MenuUIBuilder.Ensure<Image>(go);
        image.color = color;
        image.type = Image.Type.Filled;
        image.fillMethod = Image.FillMethod.Horizontal;
        image.fillAmount = 1f;

        return image;
    }

    private static Image CreateFillChild(Transform parent, string name, Color color)
    {
        GameObject go = MenuUIBuilder.FindOrCreate(name, parent.gameObject);
        RectTransform rt = MenuUIBuilder.EnsureRect(go);
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        Image image = MenuUIBuilder.Ensure<Image>(go);
        image.color = color;
        image.type = Image.Type.Filled;
        image.fillMethod = Image.FillMethod.Horizontal;
        image.fillAmount = 0f;

        return image;
    }

    private static GameObject CreateIcon(Transform canvas, string name, Color color, Vector2 anchoredPosition)
    {
        GameObject go = MenuUIBuilder.FindOrCreate(name, canvas.gameObject);
        RectTransform rt = MenuUIBuilder.EnsureRect(go);
        rt.anchorMin = rt.anchorMax = new Vector2(1f, 1f);
        rt.pivot = new Vector2(1f, 1f);
        rt.anchoredPosition = anchoredPosition;
        rt.sizeDelta = new Vector2(36, 36);

        MenuUIBuilder.Ensure<Image>(go).color = color;
        go.SetActive(false);

        return go;
    }

    private static TMP_Text CreateCornerText(
        Transform canvas, string name, string text, Vector2 anchor, Vector2 anchoredPosition)
    {
        GameObject go = MenuUIBuilder.FindOrCreate(name, canvas.gameObject);
        TextMeshProUGUI tmp = MenuUIBuilder.Ensure<TextMeshProUGUI>(go);
        tmp.text = text;
        tmp.fontSize = 22;
        tmp.alignment = TextAlignmentOptions.TopLeft;
        tmp.color = Color.white;

        RectTransform rt = MenuUIBuilder.EnsureRect(go);
        rt.anchorMin = rt.anchorMax = anchor;
        rt.pivot = anchor;
        rt.anchoredPosition = anchoredPosition;
        rt.sizeDelta = new Vector2(260, 30);

        return tmp;
    }
}
