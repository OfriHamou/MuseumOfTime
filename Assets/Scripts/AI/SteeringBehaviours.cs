using UnityEngine;

/// <summary>
/// The classic steering behaviours, named after the textbook so they are
/// obvious in a code review.
///
/// The requirement asks for a clear steering element - seek, flee or pursue -
/// and pathfinding alone is arguably not steering. These compute a target
/// point which the caller then feeds to its NavMeshAgent, so the agent still
/// walks a legal path but chooses a smarter destination.
/// </summary>
public static class SteeringBehaviours
{
    /// <summary>Head straight for a point.</summary>
    public static Vector3 Seek(Vector3 self, Vector3 target)
    {
        return target;
    }

    /// <summary>Move directly away from a point, by a given distance.</summary>
    public static Vector3 Flee(Vector3 self, Vector3 threat, float distance)
    {
        Vector3 away = self - threat;

        // Degenerate case: standing exactly on the threat. Pick any direction
        // rather than normalising a zero vector.
        if (away.sqrMagnitude < 0.0001f)
        {
            away = Vector3.forward;
        }

        return self + (away.normalized * distance);
    }

    /// <summary>
    /// Aim at where the target is GOING to be, not where it is.
    ///
    /// This is what separates pursue from a naive chase: the pursuer cuts the
    /// corner and intercepts, instead of trailing along behind and never
    /// catching anything that moves at its own speed.
    /// </summary>
    public static Vector3 Pursue(
        Vector3 self,
        Vector3 targetPosition,
        Vector3 targetVelocity,
        float selfSpeed)
    {
        float distance = Vector3.Distance(self, targetPosition);

        // Look further ahead the further away the target is, but never so far
        // that the prediction becomes nonsense.
        float lookAhead = selfSpeed > 0.01f
            ? Mathf.Min(distance / selfSpeed, 2f)
            : 0f;

        return targetPosition + (targetVelocity * lookAhead);
    }
}
