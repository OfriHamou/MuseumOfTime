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

    /// <summary>
    /// Guarantees an instance exists no matter which scene the game starts in.
    ///
    /// The three gameplay scenes never contained a GameManager of their own -
    /// only MainMenu and Victory did - so the singleton only ever existed if
    /// the player happened to enter through the menu. Pressing Play directly
    /// in MuseumNight (which is what the test suite and every dev iteration
    /// does) left Instance null, and every component that subscribes to
    /// StateChanged in Start() - the HUD, the shard SFX/VFX cues, the item
    /// icons - silently bound to nothing and never updated again.
    ///
    /// AfterSceneLoad runs once the first scene's Awake/OnEnable have
    /// finished but before any Start(), so a scene that DOES carry its own
    /// GameManager still wins and nothing is duplicated or destroyed.
    /// </summary>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void EnsureExists()
    {
        if (Instance != null)
        {
            return;
        }

        var bootstrapped = new GameObject("GameManager (bootstrapped)");
        bootstrapped.AddComponent<GameManager>();
    }

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
        // If another GameManager already exists, retire this duplicate.
        if (Instance != null && Instance != this)
        {
            // Destroying the whole GameObject would take its unrelated
            // neighbours with it - in MainMenu the same object also carries
            // SceneLoader - so only drop the duplicate component unless this
            // object exists solely to host it.
            if (GetComponents<Component>().Length <= 2)
            {
                Destroy(gameObject);
            }
            else
            {
                Destroy(this);
            }

            return;
        }

        Instance = this;

        // Keeps this object alive when another scene is loaded.
        DontDestroyOnLoad(gameObject);

        state.ClampValues();
    }

    [Header("Chrono Energy regeneration")]
    [Tooltip("Energy recovered per second once the player stops spending it.")]
    [SerializeField] private float energyRegenPerSecond = 6f;

    [Tooltip("Quiet period after the last spend before regeneration resumes.")]
    [SerializeField] private float energyRegenDelay = 1.5f;

    private float energyIdleSince;

    /// <summary>True while energy is recovering. Read by the HUD.</summary>
    public bool IsEnergyRegenerating =>
        state.currentEnergy < state.maxEnergy &&
        Time.unscaledTime - energyIdleSince >= energyRegenDelay;

    private void Update()
    {
        // Unscaled time continues to count correctly even if time is slowed.
        state.playTimeSeconds += Time.unscaledDeltaTime;

        RegenerateEnergy();
    }

    /// <summary>
    /// Energy is what makes the time powers a choice, but it was one-way:
    /// nothing anywhere put it back except dying. Era switching costs 8, an
    /// orb costs 5 and the Hourglass drains per second, so the ClockCore
    /// fight alone needs roughly fifty with no misses - on top of whatever
    /// FrozenCity's three-era puzzle already spent. A player who never died
    /// could arrive at the boss unable to switch era or throw anything, with
    /// no way to recover: the run was over, and nothing on screen said why.
    ///
    /// The plan asks for exactly this ("regenerate slowly while not using
    /// powers"), so the resource still forces the moment-to-moment choice -
    /// spending it now means not having it in ten seconds - without any
    /// possibility of a dead end.
    ///
    /// Unscaled, so holding the Hourglass cannot make refilling cheaper than
    /// draining. Deliberately silent about StateChanged on frames where the
    /// value would not visibly move, so the HUD is not rebuilt every frame.
    /// </summary>
    private void RegenerateEnergy()
    {
        if (state.currentEnergy >= state.maxEnergy)
        {
            return;
        }

        if (Time.unscaledTime - energyIdleSince < energyRegenDelay)
        {
            return;
        }

        float before = state.currentEnergy;

        state.currentEnergy = Mathf.Min(
            state.maxEnergy,
            state.currentEnergy + (energyRegenPerSecond * Time.unscaledDeltaTime));

        // A whole point of the bar, or the top-up that fills it.
        if (Mathf.FloorToInt(state.currentEnergy) > Mathf.FloorToInt(before) ||
            Mathf.Approximately(state.currentEnergy, state.maxEnergy))
        {
            NotifyStateChanged();
        }
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
        energyIdleSince = Time.unscaledTime;
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

        // Say what the power is FOR, at the moment it is handed over.
        //
        // Era travel was introduced as two keys and an energy cost and never
        // once as a reason. "Why do I need to go to the past and the future?"
        // is a fair question if nothing ever answers it - the answer is the
        // whole premise, so it should be said out loud rather than left to be
        // inferred from a puzzle three scenes later.
        HudMessageFeed.Post(
            "TIME LENS: the same place exists in three times. Q and R move " +
            "between them - something lost in one era may still be there in " +
            "another.",
            HudMessageFeed.Tone.Good);

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