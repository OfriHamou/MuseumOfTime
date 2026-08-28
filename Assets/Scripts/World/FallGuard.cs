using UnityEngine;

/// <summary>
/// Catches a player who has left the world.
///
/// Nothing did. There was no kill plane, no out-of-bounds volume, and no
/// height check anywhere in the project, so a player who jumped off the
/// mezzanine and over the museum wall simply fell - forever. No damage, no
/// death, no respawn, no way back. The run was over and the only way out was
/// to quit the game.
///
/// It is an easy state to reach, too: the objective at that point says "leave
/// the museum", and jumping out of a building is a reasonable reading of that.
///
/// A fall past the kill height is treated as a death, which means it goes
/// through the death screen like any other - the player is told what happened
/// and returned to their last Time Anchor - rather than silently teleporting
/// them and leaving them to wonder what just occurred.
/// </summary>
public sealed class FallGuard : MonoBehaviour
{
    [Tooltip("Falling below this height counts as leaving the world. Every " +
             "floor in every scene sits at or above y=0, so this is far " +
             "enough down that no reachable geometry can trip it.")]
    [SerializeField] private float killHeight = -25f;

    /// <summary>How many times this has caught the player. Used by tests.</summary>
    public int CatchCount { get; private set; }

    private bool alreadyFalling;

    private void Update()
    {
        if (transform.position.y > killHeight)
        {
            // Back on solid ground (or at least back in the world), so the
            // guard re-arms for next time.
            alreadyFalling = false;
            return;
        }

        if (alreadyFalling)
        {
            // Still on the way down from a fall already registered. The death
            // screen holds for a couple of seconds before the respawn moves
            // anybody, and the player keeps falling behind it - which must not
            // count as a second death.
            return;
        }

        alreadyFalling = true;
        CatchCount++;

        RespawnService.LastCauseOfDeath = "You fell out of the world.";

        if (GameManager.Instance != null)
        {
            GameManager.Instance.TakeDamage(GameManager.Instance.State.maxHealth);
        }
    }
}
