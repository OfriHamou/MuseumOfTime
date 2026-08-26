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

            // Finding the Lens is the moment the museum breaks open and time
            // travel becomes available.
            if (EraManager.Instance != null)
            {
                EraManager.Instance.Unlock();
            }
        }
        else
        {
            GameManager.Instance.AcquireChronoHourglass();
        }

        Destroy(gameObject);
    }
}
