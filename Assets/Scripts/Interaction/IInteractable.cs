using UnityEngine;

/// <summary>
/// Anything Noa can use with the Interact key. One interface keeps the player
/// side ignorant of what it is looking at: plaques, doors, pickups and levers
/// all arrive through here.
/// </summary>
public interface IInteractable
{
    /// <summary>Line shown while this is targeted, e.g. "Read the plaque".</summary>
    string Prompt { get; }

    /// <summary>False when the thing exists but is not usable yet.</summary>
    bool CanInteract { get; }

    void Interact(GameObject interactor);
}
