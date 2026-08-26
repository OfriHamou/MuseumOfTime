using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Phase 5, Step 5.1: the main menu and the victory screen.
///
///   Unity.exe -batchmode -quit -projectPath . ^
///             -executeMethod MenuUIBuilder.BuildFromCommandLine
///
/// Idempotent - re-running it finds and reuses the Canvas it already built
/// instead of adding a second one.
/// </summary>
public static class MenuUIBuilder
{
    private const string MainMenuScenePath = "Assets/Scenes/MainMenu.unity";
    private const string VictoryScenePath = "Assets/Scenes/Victory.unity";

    [MenuItem("Museum of Time/Build Menus (Main Menu and Victory)")]
    public static void BuildMenu() { Build(); }

    public static void BuildFromCommandLine() { Build(); }

    private static void Build()
    {
        BuildMainMenu();
        BuildVictory();

        Debug.Log("MENUS OK: MainMenu (New Game/Continue/Controls/Quit) and " +
                   "Victory (score/shards/detections/playtime) built.");
    }

    // -----------------------------------------------------------------
    // MainMenu
    // -----------------------------------------------------------------

    private static void BuildMainMenu()
    {
        Scene scene = EditorSceneManager.OpenScene(MainMenuScenePath, OpenSceneMode.Single);

        EnsureEventSystem();

        GameObject camera = GameObject.Find("Main Camera");
        if (camera != null)
        {
            Ensure<MenuCameraDrift>(camera);
        }

        GameObject canvasGo = FindOrCreateCanvas("MainMenuCanvas");
        Transform canvas = canvasGo.transform;

        CreateText(canvas, "Title", "Museum of Time", 64,
            new Vector2(0, 300), new Vector2(900, 120));

        Button newGame = CreateButton(canvas, "NewGameButton", "New Game", new Vector2(0, 100));
        Button continueGame = CreateButton(canvas, "ContinueButton", "Continue", new Vector2(0, 20));
        Button controls = CreateButton(canvas, "ControlsButton", "Controls", new Vector2(0, -60));
        Button quit = CreateButton(canvas, "QuitButton", "Quit", new Vector2(0, -140));

        GameObject controlsPanel = BuildControlsPanel(canvas, out Button controlsBack);

        GameObject uiManager = FindOrCreate("UIManager", null);
        SceneLoader loader = Ensure<SceneLoader>(uiManager);
        MainMenuController controller = Ensure<MainMenuController>(uiManager);

        var so = new SerializedObject(controller);
        so.FindProperty("sceneLoader").objectReferenceValue = loader;
        so.FindProperty("newGameButton").objectReferenceValue = newGame;
        so.FindProperty("continueButton").objectReferenceValue = continueGame;
        so.FindProperty("controlsButton").objectReferenceValue = controls;
        so.FindProperty("quitButton").objectReferenceValue = quit;
        so.FindProperty("controlsPanel").objectReferenceValue = controlsPanel;
        so.FindProperty("controlsBackButton").objectReferenceValue = controlsBack;
        so.ApplyModifiedPropertiesWithoutUndo();

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
    }

    private static GameObject BuildControlsPanel(Transform canvas, out Button backButton)
    {
        GameObject panel = FindOrCreate("ControlsPanel", canvas.gameObject);
        var rt = EnsureRect(panel);
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = new Vector2(560, 420);

        Image bg = Ensure<Image>(panel);
        bg.color = new Color(0f, 0f, 0f, 0.85f);

        const string controlsText =
            "Move  WASD\nRun  Shift\nJump  Space\nLook  Mouse\n" +
            "Interact  E\nThrow Chrono Orb  Left Mouse\n" +
            "Era Back / Forward  Q / R\nSlow Time  Ctrl\n" +
            "Camera Toggle  C\nTime Journal  Tab\nPause  Escape";

        CreateText(panel.transform, "ControlsText", controlsText, 26,
            new Vector2(0, 30), new Vector2(520, 320));

        // The panel covers ControlsButton once open - a dedicated way back
        // is not optional here, found missing in manual testing.
        backButton = CreateButton(panel.transform, "ControlsBackButton", "Back", new Vector2(0, -170));

        return panel;
    }

    // -----------------------------------------------------------------
    // Victory
    // -----------------------------------------------------------------

    private static void BuildVictory()
    {
        Scene scene = EditorSceneManager.OpenScene(VictoryScenePath, OpenSceneMode.Single);

        EnsureEventSystem();

        GameObject canvasGo = FindOrCreateCanvas("VictoryCanvas");
        Transform canvas = canvasGo.transform;

        CreateText(canvas, "Title", "Time Returns to Its Course", 48,
            new Vector2(0, 320), new Vector2(1000, 100));

        TMP_Text scoreText = CreateText(canvas, "ScoreText", "Score: 0", 30,
            new Vector2(0, 180), new Vector2(600, 50)).GetComponent<TMP_Text>();

        TMP_Text shardsText = CreateText(canvas, "ShardsText", "Time Shards: 0", 30,
            new Vector2(0, 120), new Vector2(600, 50)).GetComponent<TMP_Text>();

        TMP_Text detectionsText = CreateText(canvas, "DetectionsText", "Times Detected: 0", 30,
            new Vector2(0, 60), new Vector2(600, 50)).GetComponent<TMP_Text>();

        TMP_Text playtimeText = CreateText(canvas, "PlaytimeText", "Playtime: 00:00", 30,
            new Vector2(0, 0), new Vector2(600, 50)).GetComponent<TMP_Text>();

        Button mainMenu = CreateButton(canvas, "MainMenuButton", "Main Menu", new Vector2(0, -120));
        Button quit = CreateButton(canvas, "QuitButton", "Quit", new Vector2(0, -200));

        GameObject uiManager = FindOrCreate("UIManager", null);
        SceneLoader loader = Ensure<SceneLoader>(uiManager);
        VictoryScreenController controller = Ensure<VictoryScreenController>(uiManager);

        var so = new SerializedObject(controller);
        so.FindProperty("sceneLoader").objectReferenceValue = loader;
        so.FindProperty("scoreText").objectReferenceValue = scoreText;
        so.FindProperty("shardsText").objectReferenceValue = shardsText;
        so.FindProperty("detectionsText").objectReferenceValue = detectionsText;
        so.FindProperty("playtimeText").objectReferenceValue = playtimeText;
        so.FindProperty("mainMenuButton").objectReferenceValue = mainMenu;
        so.FindProperty("quitButton").objectReferenceValue = quit;
        so.ApplyModifiedPropertiesWithoutUndo();

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
    }

    // -----------------------------------------------------------------
    // Shared UI helpers (also used by HudBuilder/MinimapBuilder/TutorialTextBuilder)
    // -----------------------------------------------------------------

    internal static void EnsureEventSystem()
    {
        if (Object.FindFirstObjectByType<EventSystem>() != null)
        {
            return;
        }

        // The New Input System's UI module, not the legacy StandaloneInputModule -
        // T12 says only the new Input System, and that includes menu navigation.
        new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
    }

    internal static GameObject FindOrCreateCanvas(string name)
    {
        GameObject existing = GameObject.Find(name);
        if (existing != null)
        {
            return existing;
        }

        var go = new GameObject(name, typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        Canvas canvas = go.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;

        CanvasScaler scaler = go.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);

        return go;
    }

    internal static GameObject CreateText(
        Transform parent, string name, string text, float fontSize,
        Vector2 anchoredPosition, Vector2 sizeDelta)
    {
        GameObject go = FindOrCreate(name, parent.gameObject);
        TextMeshProUGUI tmp = Ensure<TextMeshProUGUI>(go);

        tmp.text = text;
        tmp.fontSize = fontSize;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = Color.white;

        RectTransform rt = EnsureRect(go);
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = anchoredPosition;
        rt.sizeDelta = sizeDelta;

        return go;
    }

    internal static Button CreateButton(Transform parent, string name, string label, Vector2 anchoredPosition)
    {
        GameObject go = FindOrCreate(name, parent.gameObject);
        Ensure<Image>(go).color = new Color(1f, 1f, 1f, 0.12f);
        Button button = Ensure<Button>(go);

        RectTransform rt = EnsureRect(go);
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = anchoredPosition;
        rt.sizeDelta = new Vector2(280, 60);

        CreateText(go.transform, "Label", label, 24, Vector2.zero, new Vector2(260, 50));

        return button;
    }

    /// <summary>
    /// Finds a child by name under a specific parent (or a root object when
    /// parent is null). Deliberately not a scene-wide GameObject.Find: this
    /// is called with generic names like "Label" for every button, and a
    /// global search would silently reparent the wrong one on a re-run.
    /// </summary>
    internal static GameObject FindOrCreate(string name, GameObject parent)
    {
        Transform existing = parent != null
            ? parent.transform.Find(name)
            : GameObject.Find(name)?.transform;

        if (existing != null)
        {
            return existing.gameObject;
        }

        var created = new GameObject(name);

        if (parent != null)
        {
            created.transform.SetParent(parent.transform, false);
        }

        return created;
    }

    internal static T Ensure<T>(GameObject target) where T : Component
    {
        T component = target.GetComponent<T>();
        return component == null ? target.AddComponent<T>() : component;
    }

    internal static RectTransform EnsureRect(GameObject go)
    {
        RectTransform rt = go.GetComponent<RectTransform>();
        return rt == null ? go.AddComponent<RectTransform>() : rt;
    }
}
