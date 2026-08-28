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

    [Tooltip("Aim tolerance, in metres. Widens the look-cast so small pickups are reachable.")]
    [SerializeField] private float aimRadius = 0.35f;

    // Reused every frame; interaction runs in Update on every gameplay frame.
    private readonly RaycastHit[] hits = new RaycastHit[16];

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
        // A SPHERE cast, not a thin ray.
        //
        // In third person the camera sits half a metre off Noa's shoulder, so
        // camera.forward runs PARALLEL to the line from the player to whatever
        // is under the crosshair - standing two metres from a Time Shard, the
        // ray passed 0.54 m to the side of it and hit nothing. Combined with
        // pickups whose colliders are under 30 cm across, hitting one with a
        // zero-width ray was mostly luck.
        //
        // A cast with real width is the usual answer: it gives the aim the
        // tolerance a player expects without widening the interaction range.
        int count = Physics.SphereCastNonAlloc(
            ray, aimRadius, hits, range + 6f, interactMask, QueryTriggerInteraction.Collide);

        if (count == 0)
        {
            return null;
        }

        // RaycastNonAlloc does not sort, so nearest-first has to be done here.
        System.Array.Sort(hits, 0, count, HitDistanceComparer.Instance);

        for (int i = 0; i < count; i++)
        {
            RaycastHit hit = hits[i];

            // Noa's own colliders are always in the way: the cast starts at the
            // camera, which in third person sits BEHIND her, so the very first
            // solid thing it meets is her CharacterController. Treating that as
            // an occluder made every third-person interaction fail.
            if (hit.collider.transform.IsChildOf(transform))
            {
                continue;
            }

            var candidate = hit.collider.GetComponentInParent<IInteractable>();

            if (candidate != null)
            {
                // Range is measured from the player, not the camera, or third
                // person would let her reach things she is nowhere near.
                //
                // hit.point is (0,0,0) when a spherecast starts already
                // overlapping the collider, so fall back to the object itself.
                Vector3 where = hit.point == Vector3.zero
                    ? hit.collider.bounds.center
                    : hit.point;

                return Vector3.Distance(transform.position, where) > range
                    ? null
                    : candidate;
            }

            // Nothing interactable on this collider. A SOLID one is a wall and
            // genuinely blocks the look; a TRIGGER is an invisible volume and
            // must not.
            //
            // This was the bug that made pickups feel broken. The old code took
            // the single nearest hit with QueryTriggerInteraction.Collide, and
            // the museum is full of trigger volumes - room entry, eight tutorial
            // reveals, era zones - sitting on the Default layer inside the
            // interact mask. Any one of them between the camera and a pickup
            // swallowed the ray and returned a collider with no IInteractable,
            // so the prompt never appeared and E did nothing. Standing two
            // metres from a Time Shard, the ray was being eaten by
            // Trigger_MainGallery six metres away.
            if (!hit.collider.isTrigger)
            {
                return null;
            }
        }

        return null;
    }

    /// <summary>Sorts raycast hits nearest-first. Cached to avoid allocating.</summary>
    private sealed class HitDistanceComparer : System.Collections.Generic.IComparer<RaycastHit>
    {
        public static readonly HitDistanceComparer Instance = new HitDistanceComparer();

        public int Compare(RaycastHit a, RaycastHit b)
        {
            return a.distance.CompareTo(b.distance);
        }
    }
}
