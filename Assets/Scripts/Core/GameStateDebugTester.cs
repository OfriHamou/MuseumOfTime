using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Temporary testing component.
/// Remove it before the final submission.
/// </summary>
public sealed class GameStateDebugTester : MonoBehaviour
{
    [ContextMenu("Test/Add 100 Score")]
    private void AddScore()
    {
        GameManager.Instance.AddScore(100);
        PrintState();
    }

    [ContextMenu("Test/Damage Player By 25")]
    private void DamagePlayer()
    {
        GameManager.Instance.TakeDamage(25);
        PrintState();
    }

    [ContextMenu("Test/Heal Player By 10")]
    private void HealPlayer()
    {
        GameManager.Instance.Heal(10);
        PrintState();
    }

    [ContextMenu("Test/Spend 20 Energy")]
    private void SpendEnergy()
    {
        bool success =
            GameManager.Instance.SpendEnergy(20f);

        Debug.Log($"Energy spent successfully: {success}", this);
        PrintState();
    }

    [ContextMenu("Test/Restore 10 Energy")]
    private void RestoreEnergy()
    {
        GameManager.Instance.RestoreEnergy(10f);
        PrintState();
    }

    [ContextMenu("Test/Add Time Shard")]
    private void AddTimeShard()
    {
        GameManager.Instance.AddTimeShard();
        PrintState();
    }

    [ContextMenu("Test/Acquire Time Lens")]
    private void AcquireTimeLens()
    {
        GameManager.Instance.AcquireTimeLens();
        PrintState();
    }

    [ContextMenu("Test/Acquire Chrono Hourglass")]
    private void AcquireChronoHourglass()
    {
        GameManager.Instance.AcquireChronoHourglass();
        PrintState();
    }

    [ContextMenu("Test/Register Detection")]
    private void RegisterDetection()
    {
        GameManager.Instance.RegisterDetection();
        PrintState();
    }

    [ContextMenu("Test/Reset Game")]
    private void ResetGame()
    {
        GameManager.Instance.ResetGame();
        PrintState();
    }

    [ContextMenu("Test/Load MuseumNight")]
    private void LoadMuseumNight()
    {
        SceneManager.LoadScene("MuseumNight");
    }

    [ContextMenu("Test/Load FrozenCity")]
    private void LoadFrozenCity()
    {
        SceneManager.LoadScene("FrozenCity");
    }

    [ContextMenu("Test/Load ClockCore")]
    private void LoadClockCore()
    {
        SceneManager.LoadScene("ClockCore");
    }

    [ContextMenu("Test/Print Current State")]
    private void PrintState()
    {
        if (GameManager.Instance == null)
        {
            Debug.LogError("GameManager does not exist.", this);
            return;
        }

        GameState state = GameManager.Instance.State;

        Debug.Log(
            $"Scene: {SceneManager.GetActiveScene().name}\n" +
            $"Health: {state.currentHealth}/{state.maxHealth}\n" +
            $"Energy: {state.currentEnergy}/{state.maxEnergy}\n" +
            $"Score: {state.score}\n" +
            $"Time Shards: {state.timeShards}\n" +
            $"Time Lens: {state.hasTimeLens}\n" +
            $"Chrono Hourglass: {state.hasChronoHourglass}\n" +
            $"Detections: {state.detectedCount}\n" +
            $"Deaths: {state.deaths}",
            this);
    }
}