using UnityEngine;

/// <summary>
/// Casts a ray from the active camera and offers whatever it hits to the
/// player.
///
/// The layer mask is built in code rather than assigned in the Inspector.
/// That is what the LayerMask requirement asks for, and it also means the set
/// of interactable layers can be discovered by reading the file instead of by
/// clicking through a component.
/// </summary>
[RequireComponent(typeof(PlayerInputReader))]
public sealed class PlayerInteractor : MonoBehaviour
{
    [SerializeField] private float range = 3f;

    private PlayerInputReader inputReader;
    private Transform cameraTransform;

    /// <summary>Layers a look-ray may hit. Built in Awake, in code.</summary>
    private LayerMask interactMask;

    private IInteractable current;

    /// <summary>Whatever is under the crosshair right now, or null.</summary>
    public IInteractable Current => current;

    /// <summary>Prompt for the current target, or an empty string.</summary>
    public string CurrentPrompt =>
        current == null ? string.Empty : current.Prompt;

    private void Awake()
    {
        inputReader = GetComponent<PlayerInputReader>();

        // Built here, deliberately. "Default" carries the world geometry and
        // "Interactable" is where plaques and pickups live.
        interactMask = LayerMask.GetMask("Default", "Interactable");
    }

    private void Update()
    {
        if (cameraTransform == null)
        {
            if (Camera.main == null)
            {
                return;
            }

            cameraTransform = Camera.main.transform;
        }

        current = FindTarget();

        if (current != null && current.CanInteract && inputReader.InteractPressed)
        {
            current.Interact(gameObject);
        }
    }

    private IInteractable FindTarget()
    {
        var ray = new Ray(cameraTransform.position, cameraTransform.forward);

        // The camera sits several metres behind Noa in third person, so the
        // ray is allowed to travel further than the interaction range.
        if (!Physics.Raycast(ray, out RaycastHit hit, range + 6f,
                             interactMask, QueryTriggerInteraction.Collide))
        {
            return null;
        }

        // Range is then measured from the player, not the camera, or third
        // person would let her reach things she is nowhere near.
        if (Vector3.Distance(transform.position, hit.point) > range)
        {
            return null;
        }

        return hit.collider.GetComponentInParent<IInteractable>();
    }
}
