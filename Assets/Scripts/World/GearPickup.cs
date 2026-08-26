using UnityEngine;

/// <summary>
/// The gear itself - the first step of the three-era puzzle. Findable only
/// in the Past: the renderer and collider are toggled rather than the
/// GameObject, so the object keeps ticking and is ready the moment the
/// player steps back into the Past, instead of needing to be re-found.
/// </summary>
public sealed class GearPickup : MonoBehaviour, IInteractable
{
    public string Prompt => "Take the gear";

    public bool CanInteract => true;

    private void Update()
    {
        bool inPast = EraManager.Instance != null && EraManager.Instance.CurrentEra == TimeEra.Past;

        foreach (Renderer renderer in GetComponentsInChildren<Renderer>())
        {
            renderer.enabled = inPast;
        }

        Collider collider = GetComponent<Collider>();
        if (collider != null)
        {
            collider.enabled = inPast;
        }
    }

    public void Interact(GameObject interactor)
    {
        if (GearPuzzle.Instance == null)
        {
            return;
        }

        GearPuzzle.Instance.CollectGear();
        Destroy(gameObject);
    }
}
