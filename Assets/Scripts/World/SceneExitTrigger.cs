using UnityEngine;

/// <summary>
/// Leaves the current scene for the next one, gated on an acquired item.
///
/// Without this, MuseumNight and FrozenCity had a working item-acquisition
/// chain (Step 3.9) but no actual way to walk from one scene to the next -
/// S9's "coherent logical connection between all the scenes" needs a door,
/// not just a flag. Reuses <see cref="SceneLoader"/> rather than duplicating
/// its scene-load guard.
/// </summary>
[RequireComponent(typeof(SceneLoader))]
public sealed class SceneExitTrigger : PlayerTrigger
{
    public enum RequiredItem
    {
        None,
        TimeLens,
        ChronoHourglass,
    }

    [SerializeField] private RequiredItem requiredItem = RequiredItem.None;
    [SerializeField] private string targetScene = "";

    private SceneLoader sceneLoader;

    /// <summary>True once this exit has actually let the player through.</summary>
    public static bool LastExitSucceeded { get; private set; }

    protected override void Awake()
    {
        // Not onlyOnce (HazardTrigger's reason, applied here for a different
        // cause): walking in without the required item must not permanently
        // spend the trigger - the player has to be able to come back once
        // they actually have it.
        onlyOnce = false;

        base.Awake();
        sceneLoader = GetComponent<SceneLoader>();
    }

    protected override void OnPlayerEntered(GameObject player)
    {
        if (!HasRequiredItem())
        {
            LastExitSucceeded = false;

            // Walking into a locked exit and having nothing happen at all
            // reads as a bug, not a gate - say what is missing.
            HudMessageFeed.Post(RejectionMessage(), HudMessageFeed.Tone.Bad);
            return;
        }

        LastExitSucceeded = true;
        sceneLoader.LoadScene(targetScene);
    }

    private string RejectionMessage()
    {
        return requiredItem == RequiredItem.TimeLens
            ? "Portal unstable - acquire the Time Lens first"
            : "This path is not open yet - you need the Chrono Hourglass";
    }

    private bool HasRequiredItem()
    {
        if (requiredItem == RequiredItem.None)
        {
            return true;
        }

        if (GameManager.Instance == null)
        {
            return false;
        }

        GameState state = GameManager.Instance.State;

        return requiredItem == RequiredItem.TimeLens
            ? state.hasTimeLens
            : state.hasChronoHourglass;
    }
}
