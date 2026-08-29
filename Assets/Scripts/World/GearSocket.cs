using UnityEngine;

/// <summary>
/// The tower's gear mechanism - one physical object serves both remaining
/// puzzle steps, since installing the gear and later verifying it hold
/// happen at the same place, just in different eras.
/// </summary>
public sealed class GearSocket : MonoBehaviour, IInteractable
{
    public string Prompt
    {
        get
        {
            if (GearPuzzle.Instance == null)
            {
                return "The gear socket";
            }

            if (!GearPuzzle.Instance.HasGear)
            {
                return "An empty gear socket";
            }

            if (!GearPuzzle.Instance.Installed)
            {
                return "Install the gear";
            }

            return !GearPuzzle.Instance.Verified ? "Verify the mechanism" : "The mechanism holds";
        }
    }

    public bool CanInteract => true;

    public void Interact(GameObject interactor)
    {
        if (GearPuzzle.Instance == null)
        {
            return;
        }

        if (!GearPuzzle.Instance.Installed)
        {
            if (!GearPuzzle.Instance.TryInstall())
            {
                HudMessageFeed.Post(
                    GearPuzzle.Instance.HasGear
                        ? "The tower still waits for its missing tooth."
                        : "There is nowhere to seat a gear you do not have yet.",
                    HudMessageFeed.Tone.Bad);
            }
        }
        else if (!GearPuzzle.Instance.Verified)
        {
            if (!GearPuzzle.Instance.TryVerify())
            {
                HudMessageFeed.Post(
                    "Only a later hour will show if the repair held.",
                    HudMessageFeed.Tone.Bad);
            }
        }
    }
}
