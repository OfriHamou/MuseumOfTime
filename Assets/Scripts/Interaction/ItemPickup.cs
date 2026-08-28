using UnityEngine;

/// <summary>
/// One of the two items that carry between scenes. The Time Lens is found in
/// MuseumNight and needed in FrozenCity; the Chrono Hourglass is found in
/// FrozenCity and needed in ClockCore. That chain is what ties the three
/// scenes into one game instead of three demos.
/// </summary>
public sealed class ItemPickup : MonoBehaviour, IInteractable
{
    public enum Kind
    {
        TimeLens,
        ChronoHourglass,
    }

    [SerializeField] private Kind item = Kind.TimeLens;

    public string Prompt =>
        item == Kind.TimeLens
            ? "Take the Time Lens"
            : "Take the Chrono Hourglass";

    public bool CanInteract => true;

    public void Interact(GameObject interactor)
    {
        if (GameManager.Instance == null)
        {
            return;
        }

        if (item == Kind.TimeLens)
        {
            GameManager.Instance.AcquireTimeLens();

            // Era travel is unlocked earlier now, by an EraZoneTrigger near
            // the Temporal Seal puzzle (MuseumTimeSealPuzzle) - solving that
            // puzzle needs Q/R, and the Lens is now the puzzle's REWARD, not
            // the thing that unlocks the ability. Calling Unlock() here too
            // would be harmless (it is just an idempotent flag) but is no
            // longer the actual unlock moment, so it is not duplicated here.
            HudMessageFeed.Post("Time Lens acquired", HudMessageFeed.Tone.Good);
            HudMessageFeed.Post("Objective: Reach the portal", HudMessageFeed.Tone.Good);
        }
        else
        {
            GameManager.Instance.AcquireChronoHourglass();
        }

        Destroy(gameObject);
    }
}
