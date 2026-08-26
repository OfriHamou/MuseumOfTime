using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Pause on Escape. Resume, Restart Scene, Controls, Main Menu, Quit.
///
/// The one real bug risk here (flagged in the Step 3.6 slow-time work): the
/// timeScale that Resume restores must be exactly 1, never "whatever it was
/// before". If Escape is pressed while the Chrono Hourglass is active,
/// naively restoring a cached pre-pause value would leak 0.3 back into
/// normal play once the player un-pauses.
///
/// Two more real bugs found in manual testing:
///
/// 1. PlayerCameraRig locks and hides the OS cursor for gameplay look and
///    never releases it on its own - Time.timeScale = 0 does not touch the
///    cursor at all. Without explicitly unlocking it here, the pause menu is
///    visible but nothing can click it.
/// 2. All five buttons were wired from Assets/Editor/HudBuilder.cs via
///    button.onClick.AddListener(...) - called from an Editor batch-mode
///    script, which registers a NON-PERSISTENT UnityEvent listener. That
///    registration lives only in the memory of the batch process that made
///    it and is never serialized into the saved scene; the instant that
///    process exits (immediately, since it is "-batchmode -quit"), the
///    listener is gone. Every button in this menu had zero working
///    listeners the moment anyone actually pressed Play. The fix is the
///    same pattern MainMenuController and VictoryScreenController already
///    use correctly: the MonoBehaviour wires its own buttons in Awake(),
///    which runs fresh every time the scene loads, so the listener always
///    exists at runtime regardless of how the scene was built.
/// </summary>
public sealed class PauseMenuController : MonoBehaviour
{
    [SerializeField] private GameObject panel;
    [SerializeField] private SceneLoader sceneLoader;
    [SerializeField] private GameObject controlsPanel;

    [SerializeField] private Button resumeButton;
    [SerializeField] private Button restartButton;
    [SerializeField] private Button controlsButton;
    [SerializeField] private Button controlsBackButton;
    [SerializeField] private Button mainMenuButton;
    [SerializeField] private Button quitButton;

    private PlayerInputReader inputReader;
    private bool isPaused;

    public bool IsPaused => isPaused;

    private void Awake()
    {
        inputReader = FindFirstObjectByType<PlayerInputReader>();

        if (resumeButton != null) resumeButton.onClick.AddListener(OnResumeButton);
        if (restartButton != null) restartButton.onClick.AddListener(OnRestartScene);
        if (controlsButton != null) controlsButton.onClick.AddListener(OnOpenControls);
        if (controlsBackButton != null) controlsBackButton.onClick.AddListener(OnCloseControls);
        if (mainMenuButton != null) mainMenuButton.onClick.AddListener(OnMainMenu);
        if (quitButton != null) quitButton.onClick.AddListener(OnQuit);

        if (panel != null) panel.SetActive(false);
        if (controlsPanel != null) controlsPanel.SetActive(false);
    }

    private void Update()
    {
        if (inputReader == null || !inputReader.PausePressed)
        {
            return;
        }

        // The Controls sub-panel covers the button that opened it, the same
        // way the Main Menu's does - Escape has to close that first, not
        // resume play out from under it.
        if (controlsPanel != null && controlsPanel.activeSelf)
        {
            OnCloseControls();
        }
        else
        {
            Toggle();
        }
    }

    private void Toggle()
    {
        if (isPaused)
        {
            Resume();
        }
        else
        {
            Pause();
        }
    }

    private void Pause()
    {
        isPaused = true;

        if (panel != null)
        {
            panel.SetActive(true);
        }

        Time.timeScale = 0f;

        // See the class comment: without this the menu is visible but
        // there is no visible, movable cursor to click it with.
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    public void Resume()
    {
        isPaused = false;

        if (panel != null) panel.SetActive(false);
        if (controlsPanel != null) controlsPanel.SetActive(false);

        // Exactly 1. Never a cached "value before pause" - that is precisely
        // how a slow-time hold would leak into normal play after Resume.
        Time.timeScale = 1f;

        // Hand gameplay look back exactly the way PlayerCameraRig itself
        // sets it up on enable.
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    public void OnResumeButton() => Resume();

    public void OnOpenControls()
    {
        if (controlsPanel != null)
        {
            controlsPanel.SetActive(true);
        }
    }

    public void OnCloseControls()
    {
        if (controlsPanel != null)
        {
            controlsPanel.SetActive(false);
        }
    }

    public void OnRestartScene()
    {
        Time.timeScale = 1f;
        sceneLoader.RestartCurrentScene();
    }

    public void OnMainMenu()
    {
        Time.timeScale = 1f;
        sceneLoader.LoadMainMenu();
    }

    public void OnQuit() => sceneLoader.QuitGame();
}
