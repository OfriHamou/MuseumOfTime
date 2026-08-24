using UnityEngine;

[System.Serializable]
public sealed class GameState
{
    [Header("Player Stats")]
    [Min(1)]
    public int maxHealth = 100;

    [Min(0)]
    public int currentHealth = 100;

    [Min(1f)]
    public float maxEnergy = 100f;

    [Min(0f)]
    public float currentEnergy = 100f;

    [Header("Progress")]
    [Min(0)]
    public int score;

    [Min(0)]
    public int timeShards;

    [Min(0)]
    public int detectedCount;

    [Min(0)]
    public int deaths;

    [Min(0f)]
    public float playTimeSeconds;

    [Header("Acquired Items")]
    public bool hasTimeLens;
    public bool hasChronoHourglass;

    [Header("Checkpoint")]
    public bool hasCheckpoint;
    public string checkpointSceneName = "";
    public Vector3 checkpointPosition = Vector3.zero;

    /// <summary>
    /// Restores all data to the values of a new game.
    /// </summary>
    public void ResetToDefaults()
    {
        maxHealth = 100;
        currentHealth = maxHealth;

        maxEnergy = 100f;
        currentEnergy = maxEnergy;

        score = 0;
        timeShards = 0;
        detectedCount = 0;
        deaths = 0;
        playTimeSeconds = 0f;

        hasTimeLens = false;
        hasChronoHourglass = false;

        hasCheckpoint = false;
        checkpointSceneName = "";
        checkpointPosition = Vector3.zero;
    }

    /// <summary>
    /// Prevents invalid values, such as negative health.
    /// </summary>
    public void ClampValues()
    {
        maxHealth = Mathf.Max(1, maxHealth);
        currentHealth = Mathf.Clamp(currentHealth, 0, maxHealth);

        maxEnergy = Mathf.Max(1f, maxEnergy);
        currentEnergy = Mathf.Clamp(currentEnergy, 0f, maxEnergy);

        score = Mathf.Max(0, score);
        timeShards = Mathf.Max(0, timeShards);
        detectedCount = Mathf.Max(0, detectedCount);
        deaths = Mathf.Max(0, deaths);
        playTimeSeconds = Mathf.Max(0f, playTimeSeconds);
    }
}