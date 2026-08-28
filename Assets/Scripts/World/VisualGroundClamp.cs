using UnityEngine;

/// <summary>
/// Keeps a rendered enemy model flush with the real floor collider, independent
/// of wherever its NavMeshAgent parent's baked navmesh actually sits.
///
/// TimeWarden and ChronologicalShadow both stand on a NavMesh that was baked
/// 0.3-0.45m above the true floor mesh at different points along their routes
/// (confirmed by sampling NavMesh.SamplePosition against the floor collider at
/// several waypoints - the gap is not constant, so a single fixed local Y
/// offset on the visual cannot keep both a Present-room waypoint and a
/// Past-room waypoint looking grounded at the same time). The NavMeshAgent
/// itself is left exactly alone - it still paths and moves against its own
/// navmesh as always - only the rendered child is re-anchored to the actual
/// floor every frame.
/// </summary>
public sealed class VisualGroundClamp : MonoBehaviour
{
    [Tooltip("World-space gap kept between the model's local origin and the " +
             "floor. 0 for a flush stance; a small positive value for a " +
             "deliberate hover (e.g. Chrono Shadow).")]
    [SerializeField] private float footClearance = 0f;

    [SerializeField] private float maxRayDistance = 10f;

    private Transform owner;
    private LayerMask groundMask;
    private readonly RaycastHit[] hits = new RaycastHit[8];

    private void Awake()
    {
        owner = transform.parent != null ? transform.parent : transform;

        // Built in code, not just assigned in the Inspector - the enemy root
        // and the floor share the "Default" layer, so this is also what lets
        // FindFloorY tell the real floor apart from the enemy's own collider.
        groundMask = LayerMask.GetMask("Default");
    }

    private void LateUpdate()
    {
        if (!TryFindFloorY(out float floorY))
        {
            return;
        }

        float parentScaleY = owner.lossyScale.y;

        if (Mathf.Approximately(parentScaleY, 0f))
        {
            return;
        }

        Vector3 local = transform.localPosition;
        local.y = (floorY + footClearance - owner.position.y) / parentScaleY;
        transform.localPosition = local;
    }

    private bool TryFindFloorY(out float floorY)
    {
        Vector3 origin = transform.position + Vector3.up * 0.5f;
        int count = Physics.RaycastNonAlloc(
            origin, Vector3.down, hits, maxRayDistance, groundMask, QueryTriggerInteraction.Ignore);

        float bestDistance = float.MaxValue;
        floorY = 0f;
        bool found = false;

        for (int i = 0; i < count; i++)
        {
            RaycastHit hit = hits[i];

            // The enemy's own CapsuleCollider lives on the same "Default"
            // layer as the floor, so the first hit under a naive raycast is
            // almost always itself rather than the ground.
            if (hit.collider.transform.IsChildOf(owner))
            {
                continue;
            }

            if (hit.distance < bestDistance)
            {
                bestDistance = hit.distance;
                floorY = hit.point.y;
                found = true;
            }
        }

        return found;
    }
}
