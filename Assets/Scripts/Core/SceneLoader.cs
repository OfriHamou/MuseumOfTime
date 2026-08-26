using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Provides reusable scene-loading functions for menus,
/// portals, buttons and victory screens.
/// </summary>
public sealed class SceneLoader : MonoBehaviour
{
    public void StartNewGame()
    {
        if (GameManager.Instance != null)
        {
            GameManager.Instance.ResetGame();
        }

        LoadScene("MuseumNight");
    }

    /// <summary>
    /// Loads the serialized save from Step 3.9 and resumes at whichever scene
    /// it was written from, falling back to MuseumNight for a save that
    /// predates the checkpoint fields.
    /// </summary>
    public void ContinueGame()
    {
        if (!SaveService.Load())
        {
            Debug.LogWarning("No save to continue from.", this);
            return;
        }

        GameState state = GameManager.Instance.State;

        string target = state.hasCheckpoint && !string.IsNullOrWhiteSpace(state.checkpointSceneName)
            ? state.checkpointSceneName
            : "MuseumNight";

        LoadScene(target);
    }

    public void LoadScene(string sceneName)
    {
        if (string.IsNullOrWhiteSpace(sceneName))
        {
            Debug.LogError("Scene name cannot be empty.", this);
            return;
        }

        if (!Application.CanStreamedLevelBeLoaded(sceneName))
        {
            Debug.LogError(
                $"Scene '{sceneName}' is not available in the Build Scene List.",
                this);

            return;
        }

        SceneManager.LoadScene(sceneName);
    }

    public void LoadNextScene()
    {
        int currentIndex =
            SceneManager.GetActiveScene().buildIndex;

        int nextIndex = currentIndex + 1;

        if (nextIndex >= SceneManager.sceneCountInBuildSettings)
        {
            Debug.LogWarning("There is no next scene.", this);
            return;
        }

        SceneManager.LoadScene(nextIndex);
    }

    public void RestartCurrentScene()
    {
        Scene currentScene = SceneManager.GetActiveScene();
        SceneManager.LoadScene(currentScene.name);
    }

    public void LoadMainMenu()
    {
        LoadScene("MainMenu");
    }

    public void LoadVictory()
    {
        LoadScene("Victory");
    }

    public void QuitGame()
    {
        Debug.Log("Quit requested.", this);
        Application.Quit();
    }
}