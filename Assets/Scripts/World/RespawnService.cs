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
    [SerializeField] private float energyOnRespawn = 60f;

    /// <summary>How many times the player has been sent back.</summary>
    public int RespawnCount { get; private set; }

    /// <summary>True if the last respawn used an anchor rather than the start.</summary>
    public bool LastUsedAnchor { get; private set; }

    private void Awake()
    {
        Instance = this;
    }

    private void OnEnable()
    {
        if (GameManager.Instance != null)
        {
            GameManager.Instance.PlayerDied += Respawn;
        }
    }

    private void OnDisable()
    {
        if (GameManager.Instance != null)
        {
            GameManager.Instance.PlayerDied -= Respawn;
        }
    }

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

        if (state != null && state.hasCheckpoint)
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

        RespawnCount++;
    }

    /// <summary>
    /// The CharacterController owns the transform and will overwrite a plain
    /// position assignment, so it has to be switched off across the move.
    /// </summary>
    private static void Teleport(GameObject player, Vector3 destination)
    {
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
