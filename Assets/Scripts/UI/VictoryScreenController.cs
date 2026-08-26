using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The GDD's Ending 1 run summary. All four values already exist on
/// GameState (Step 3.9), so this only has to read and format them once.
/// </summary>
public sealed class VictoryScreenController : MonoBehaviour
{
    [SerializeField] private SceneLoader sceneLoader;
    [SerializeField] private TMP_Text scoreText;
    [SerializeField] private TMP_Text shardsText;
    [SerializeField] private TMP_Text detectionsText;
    [SerializeField] private TMP_Text playtimeText;
    [SerializeField] private Button mainMenuButton;
    [SerializeField] private Button quitButton;

    private void Awake()
    {
        if (mainMenuButton != null) mainMenuButton.onClick.AddListener(OnMainMenu);
        if (quitButton != null) quitButton.onClick.AddListener(OnQuit);
    }

    private void Start()
    {
        if (GameManager.Instance == null)
        {
            return;
        }

        GameState state = GameManager.Instance.State;

        if (scoreText != null) scoreText.text = "Score: " + state.score;
        if (shardsText != null) shardsText.text = "Time Shards: " + state.timeShards;
        if (detectionsText != null) detectionsText.text = "Times Detected: " + state.detectedCount;
        if (playtimeText != null) playtimeText.text = "Playtime: " + FormatPlaytime(state.playTimeSeconds);
    }

    private static string FormatPlaytime(float seconds)
    {
        int total = Mathf.Max(0, Mathf.FloorToInt(seconds));
        return string.Format("{0:00}:{1:00}", total / 60, total % 60);
    }

    private void OnMainMenu() => sceneLoader.LoadMainMenu();

    private void OnQuit() => sceneLoader.QuitGame();
}
