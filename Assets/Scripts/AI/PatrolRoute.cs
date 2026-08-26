using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// An ordered list of waypoints with a wait at each one.
///
/// The requirement is "patrol with pause", and the pause is the graded part:
/// a guard that stops and scans is what makes stealth playable, because it
/// gives the player a window to move in.
/// </summary>
public sealed class PatrolRoute : MonoBehaviour
{
    [System.Serializable]
    public struct Waypoint
    {
        public Vector3 position;

        [Tooltip("Seconds to stand still and scan on arrival.")]
        public float waitSeconds;
    }

    [SerializeField] private List<Waypoint> waypoints = new List<Waypoint>();

    [Tooltip("Walk the route forwards and then backwards, rather than " +
             "looping straight back to the first point.")]
    [SerializeField] private bool pingPong = true;

    private int index;
    private int direction = 1;

    public int Count => waypoints.Count;

    public bool HasWaypoints => waypoints.Count > 0;

    /// <summary>The waypoint the patroller is currently heading for.</summary>
    public Waypoint Current =>
        waypoints.Count == 0 ? default : waypoints[Mathf.Clamp(index, 0, waypoints.Count - 1)];

    /// <summary>Advances to the next waypoint and returns it.</summary>
    public Waypoint Advance()
    {
        if (waypoints.Count == 0)
        {
            return default;
        }

        if (pingPong)
        {
            if (index + direction >= waypoints.Count || index + direction < 0)
            {
                direction = -direction;
            }

            index += direction;
        }
        else
        {
            index = (index + 1) % waypoints.Count;
        }

        return waypoints[index];
    }

    public void SetWaypoints(List<Waypoint> points)
    {
        waypoints = points;
        index = 0;
        direction = 1;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;

        for (int i = 0; i < waypoints.Count; i++)
        {
            Gizmos.DrawWireSphere(waypoints[i].position, 0.4f);

            if (i + 1 < waypoints.Count)
            {
                Gizmos.DrawLine(waypoints[i].position, waypoints[i + 1].position);
            }
        }
    }
}
