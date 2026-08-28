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

        // EVERY collider, not just the first.
        //
        // GetComponent returns one, and repeated rebuilds had left eighteen on
        // this object - including a 2.2 x 5.9 m box. Seventeen of them stayed
        // enabled in every era, so the "missing" gear was still a solid,
        // invisible, six-metre wall standing in the street in the Present and
        // the Future.
        foreach (Collider collider in GetComponents<Collider>())
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
