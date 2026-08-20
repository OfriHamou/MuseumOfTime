using System;
using UnityEngine;

/// <summary>
/// Central manager that stores the current game state.
/// It survives scene changes and prevents duplicate instances.
/// </summary>
[DefaultExecutionOrder(-100)]
public sealed class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    [Header("Persistent Game State")]
    [SerializeField]
    private GameState state = new GameState();

    public GameState State => state;

    /// <summary>
    /// Future UI scripts can listen to this event and refresh the HUD.
    /// </summary>
    public event Action StateChanged;

    /// <summary>
    /// Future respawn code will listen to this event.
    /// </summary>
    public event Action PlayerDied;

    private void Awake()
    {
        // If another GameManager already exists, destroy this duplicate.
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        // Keeps this object alive when another scene is loaded.
        DontDestroyOnLoad(gameObject);

        state.ClampValues();
    }

    private void Update()
    {
        // Unscaled time continues to count correctly even if time is slowed.
        state.playTimeSeconds += Time.unscaledDeltaTime;
    }

    public void ResetGame()
    {
        state.ResetToDefaults();
        NotifyStateChanged();

        Debug.Log("Game state reset.", this);
    }

    public void AddScore(int amount)
    {
        if (amount <= 0)
        {
            return;
        }

        state.score += amount;
        NotifyStateChanged();
    }

    public void RemoveScore(int amount)
    {
        if (amount <= 0)
        {
            return;
        }

        state.score = Mathf.Max(0, state.score - amount);
        NotifyStateChanged();
    }

    public void TakeDamage(int amount)
    {
        if (amount <= 0 || state.currentHealth <= 0)
        {
            return;
        }

        state.currentHealth =
            Mathf.Max(0, state.currentHealth - amount);

        NotifyStateChanged();

        if (state.currentHealth == 0)
        {
            state.deaths++;
            NotifyStateChanged();
            PlayerDied?.Invoke();

            Debug.Log("Player has died.", this);
        }
    }

    public void Heal(int amount)
    {
        if (amount <= 0 || state.currentHealth <= 0)
        {
            return;
        }

        state.currentHealth =
            Mathf.Min(state.maxHealth, state.currentHealth + amount);

        NotifyStateChanged();
    }

    public void RestoreFullHealth()
    {
        state.currentHealth = state.maxHealth;
        NotifyStateChanged();
    }

    public bool SpendEnergy(float amount)
    {
        if (amount <= 0f)
        {
            return true;
        }

        if (state.currentEnergy < amount)
        {
            return false;
        }

        state.currentEnergy -= amount;
        NotifyStateChanged();

        return true;
    }

    public void RestoreEnergy(float amount)
    {
        if (amount <= 0f)
        {
            return;
        }

        state.currentEnergy =
            Mathf.Min(state.maxEnergy, state.currentEnergy + amount);

        NotifyStateChanged();
    }

    public void RestoreFullEnergy()
    {
        state.currentEnergy = state.maxEnergy;
        NotifyStateChanged();
    }

    public void AddTimeShard(int amount = 1)
    {
        if (amount <= 0)
        {
            return;
        }

        state.timeShards += amount;

        // Every shard also rewards score.
        state.score += 100 * amount;

        NotifyStateChanged();
    }

    public void AcquireTimeLens()
    {
        if (state.hasTimeLens)
        {
            return;
        }

        state.hasTimeLens = true;
        state.score += 250;

        NotifyStateChanged();

        Debug.Log("Time Lens acquired.", this);
    }

    public void AcquireChronoHourglass()
    {
        if (state.hasChronoHourglass)
        {
            return;
        }

        state.hasChronoHourglass = true;
        state.score += 250;

        NotifyStateChanged();

        Debug.Log("Chrono Hourglass acquired.", this);
    }

    public void RegisterDetection()
    {
        state.detectedCount++;
        RemoveScore(50);
    }

    public void SaveCheckpoint(
        string sceneName,
        Vector3 checkpointPosition)
    {
        if (string.IsNullOrWhiteSpace(sceneName))
        {
            Debug.LogWarning(
                "Checkpoint scene name cannot be empty.",
                this);

            return;
        }

        state.hasCheckpoint = true;
        state.checkpointSceneName = sceneName;
        state.checkpointPosition = checkpointPosition;

        NotifyStateChanged();

        Debug.Log(
            $"Checkpoint saved in {sceneName} at {checkpointPosition}.",
            this);
    }

    private void NotifyStateChanged()
    {
        state.ClampValues();
        StateChanged?.Invoke();
    }
}