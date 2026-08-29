using UnityEngine;

/// <summary>
/// Puts Noa back after a failure. From scene two onward that means the last
/// armed Time Anchor, not the start of the level, which is the whole point of
/// the teleport requirement.
///
/// Health is restored, energy partly restored, and a little score is taken
/// away, so failing costs something without being punishing.
/// </summary>
public sealed class RespawnService : MonoBehaviour
{
    public static RespawnService Instance { get; private set; }

    [SerializeField] private Transform sceneStart;
    [SerializeField] private int scorePenalty = 40;

    [Tooltip("Dying again within this long of a respawn means the place they " +
             "were sent to is not safe, so the run ends instead of looping.")]
    [SerializeField] private float unsafeRespawnSeconds = 4f;

    private float lastRespawnAt = -999f;
    [SerializeField] private float energyOnRespawn = 60f;

    /// <summary>How many times the player has been sent back.</summary>
    public int RespawnCount { get; private set; }

    /// <summary>True if the last respawn used an anchor rather than the start.</summary>
    public bool LastUsedAnchor { get; private set; }

    private void Awake()
    {
        Instance = this;
    }

    private bool subscribed;

    private void OnEnable()
    {
        TrySubscribe();
    }

    private void Start()
    {
        // Again here, because OnEnable is too early.
        //
        // No scene contains a GameManager - it is created by a
        // RuntimeInitializeOnLoadMethod(AfterSceneLoad) bootstrap, which runs
        // AFTER every scene object's Awake and OnEnable. So at OnEnable
        // GameManager.Instance is null, the subscription silently did not
        // happen, and nothing in the game was listening for PlayerDied.
        //
        // Health reached zero and absolutely nothing occurred - and because
        // TakeDamage returns early once health is zero, the player was then
        // stuck alive at zero permanently, unable to die or be sent back.
        TrySubscribe();
    }

    private void Update()
    {
        // Two backstops, because dying is not something that may quietly fail.
        TrySubscribe();

        if (!subscribed || GameManager.Instance == null || dying)
        {
            return;
        }

        // If health is at zero and no death is in progress, run one. This
        // catches any path that empties health without raising the event -
        // a loaded save, a direct write, an event lost to ordering.
        if (GameManager.Instance.State.currentHealth <= 0)
        {
            OnPlayerDied();
        }
    }

    private void TrySubscribe()
    {
        if (subscribed || GameManager.Instance == null)
        {
            return;
        }

        GameManager.Instance.PlayerDied += OnPlayerDied;
        subscribed = true;
    }

    private void OnDisable()
    {
        if (subscribed && GameManager.Instance != null)
        {
            GameManager.Instance.PlayerDied -= OnPlayerDied;
        }

        subscribed = false;
    }

    /// <summary>
    /// Shows the death screen, THEN respawns.
    ///
    /// Dying used to be invisible: PlayerDied went straight to Respawn, the
    /// player was teleported and healed, and nothing on screen ever said so.
    /// The world simply jumped. Reaching zero health is the most important
    /// thing the game has to tell you, and it was the one thing it did not.
    /// </summary>
    private bool dying;

    private void OnPlayerDied()
    {
        if (dying)
        {
            return;
        }

        dying = true;

        if (DeathOverlay.Instance == null)
        {
            // No overlay in this scene - still better to respawn than to
            // leave the player stuck at zero health.
            Respawn();
            dying = false;
            return;
        }

        StartCoroutine(DieThenRespawn());
    }

    private System.Collections.IEnumerator DieThenRespawn()
    {
        // A death is a GAME OVER unless there is somewhere earned to go back
        // to.
        //
        // T21 wants failure to return the player to their last Time Anchor,
        // and it still does - but anchors only exist from FrozenCity onward,
        // so in the museum there is nothing to return to and "respawn" just
        // meant being dropped back where you started with no acknowledgement
        // that you had died at all.
        //
        // The second condition catches the other half of the problem: if the
        // player dies again within moments of coming back, the place they were
        // sent to is not safe, and sending them there again just loops. End
        // the run instead of looping it.
        bool haveAnchor = GameManager.Instance != null &&
                          GameManager.Instance.State.hasCheckpoint;

        bool diedRightAfterRespawning =
            RespawnCount > 0 &&
            Time.unscaledTime - lastRespawnAt < unsafeRespawnSeconds;

        bool gameOver = !haveAnchor || diedRightAfterRespawning;

        yield return DeathOverlay.Instance.Show(LastCauseOfDeath, gameOver);

        LastCauseOfDeath = "";

        if (gameOver)
        {
            GameOver();
            dying = false;
            yield break;
        }

        Respawn();
        dying = false;
    }

    /// <summary>
    /// Ends the run and returns to the main menu.
    /// </summary>
    private void GameOver()
    {
        if (GameManager.Instance != null)
        {
            // So the menu's Continue button does not drop them straight back
            // into a dead state.
            GameManager.Instance.ResetGame();
        }

        var loader = FindFirstObjectByType<SceneLoader>();

        if (loader != null)
        {
            loader.LoadMainMenu();
            return;
        }

        UnityEngine.SceneManagement.SceneManager.LoadScene("MainMenu");
    }

    /// <summary>
    /// What killed the player, set by whatever did it so the death screen can
    /// name it. "You died" with no cause teaches nothing.
    /// </summary>
    public static string LastCauseOfDeath { get; set; } = "";

    /// <summary>Sends the player back to the last anchor, or the scene start.</summary>
    public void Respawn()
    {
        GameObject player = GameObject.FindGameObjectWithTag("Player");

        if (player == null)
        {
            player = FindFirstObjectByType<PlayerController>()?.gameObject;
        }

        if (player == null)
        {
            return;
        }

        GameState state = GameManager.Instance != null
            ? GameManager.Instance.State
            : null;

        Vector3 destination;

        // The checkpoint has to belong to THIS scene. hasCheckpoint/
        // checkpointPosition persist in GameState across scene transitions
        // (by design - Continue and cross-scene progress depend on that), so
        // a player who armed an anchor late in FrozenCity and then walked
        // through the portal into ClockCore was carrying FrozenCity's raw
        // world coordinates. The first death in ClockCore before arming a
        // ClockCore anchor dropped them at those old coordinates inside the
        // NEW scene's geometry - not the designed spawn, not a real
        // checkpoint, just whatever ClockCore happens to have at that point
        // in space. Comparing the scene name is the whole fix: a checkpoint
        // from a different scene is not a checkpoint here.
        bool checkpointBelongsToThisScene = state != null && state.hasCheckpoint &&
            state.checkpointSceneName == UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;

        if (checkpointBelongsToThisScene)
        {
            destination = state.checkpointPosition + Vector3.up;
            LastUsedAnchor = true;

            // Back in the era the anchor was armed in, not whichever one the
            // player happened to die in.
            if (EraManager.Instance != null)
            {
                EraManager.Instance.SetEra(state.checkpointEra);
            }
        }
        else
        {
            destination = sceneStart != null
                ? sceneStart.position
                : Vector3.up;

            LastUsedAnchor = false;
        }

        Teleport(player, destination);

        if (GameManager.Instance != null)
        {
            GameManager.Instance.RestoreFullHealth();
            GameManager.Instance.RestoreEnergy(energyOnRespawn);
            GameManager.Instance.RemoveScore(scorePenalty);
        }

        ClearTheAreaOfHunters();

        lastRespawnAt = Time.unscaledTime;
        RespawnCount++;
    }

    /// <summary>
    /// Sends every hunter back to its round before the player is put back on
    /// their feet.
    ///
    /// A respawn is supposed to be a second chance. Without this it is not
    /// one: the Warden that just killed the player is still standing on the
    /// respawn point with a full detection meter, so it catches them again
    /// immediately and the run is over regardless of what the player does.
    /// This was measured in a real play session - twenty-one captures in a
    /// row and no way out of it.
    /// </summary>
    private static void ClearTheAreaOfHunters()
    {
        foreach (WardenAI warden in Object.FindObjectsByType<WardenAI>(
                     FindObjectsSortMode.None))
        {
            warden.ReturnToPatrol();
        }

        foreach (ShadowAI shadow in Object.FindObjectsByType<ShadowAI>(
                     FindObjectsSortMode.None))
        {
            shadow.Freeze(2f);
        }
    }

    /// <summary>
    /// The CharacterController owns the transform and will overwrite a plain
    /// position assignment, so it has to be switched off across the move.
    /// </summary>
    private static void Teleport(GameObject player, Vector3 destination)
    {
        // Drop any speed the fall built up first, or the character is placed
        // correctly and then driven straight back through the floor.
        var movement = player.GetComponent<PlayerController>();

        if (movement != null)
        {
            movement.ResetFallVelocity();
        }

        var controller = player.GetComponent<CharacterController>();

        if (controller != null)
        {
            controller.enabled = false;
        }

        player.transform.position = destination;

        if (controller != null)
        {
            controller.enabled = true;
        }
    }
}
