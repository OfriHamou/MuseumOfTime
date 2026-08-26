using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// Wires the main menu buttons to <see cref="SceneLoader"/>. Button clicks
/// are subscribed here in code rather than through Inspector events, for the
/// same reason PlayerInputReader subscribes to actions in code: an empty
/// Inspector call list fails silently, a missing reference here throws.
/// </summary>
public sealed class MainMenuController : MonoBehaviour
{
    [SerializeField] private SceneLoader sceneLoader;
    [SerializeField] private Button newGameButton;
    [SerializeField] private Button continueButton;
    [SerializeField] private Button controlsButton;
    [SerializeField] private Button quitButton;
    [SerializeField] private GameObject controlsPanel;
    [SerializeField] private Button controlsBackButton;

    private void Awake()
    {
        if (newGameButton != null) newGameButton.onClick.AddListener(OnNewGame);
        if (continueButton != null) continueButton.onClick.AddListener(OnContinue);
        if (controlsButton != null) controlsButton.onClick.AddListener(OnOpenControls);
        if (controlsBackButton != null) controlsBackButton.onClick.AddListener(OnCloseControls);
        if (quitButton != null) quitButton.onClick.AddListener(OnQuit);
    }

    private void Start()
    {
        // Continue only makes sense once something has actually been saved.
        if (continueButton != null)
        {
            continueButton.interactable = SaveService.Exists;
        }

        if (controlsPanel != null)
        {
            controlsPanel.SetActive(false);
        }
    }

    private void Update()
    {
        // The Controls panel covers the button that opened it (both are
        // centered on the same canvas), so the Back button and Escape are
        // the only ways back.
        if (controlsPanel != null && controlsPanel.activeSelf &&
            Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            OnCloseControls();
        }
    }

    private void OnNewGame() => sceneLoader.StartNewGame();

    private void OnContinue() => sceneLoader.ContinueGame();

    private void OnOpenControls()
    {
        if (controlsPanel != null)
        {
            controlsPanel.SetActive(true);
        }
    }

    private void OnCloseControls()
    {
        if (controlsPanel != null)
        {
            controlsPanel.SetActive(false);
        }
    }

    private void OnQuit() => sceneLoader.QuitGame();
}
