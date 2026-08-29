using TMPro;
using UnityEngine;

/// <summary>
/// A persistent world-space objective line at a doorway, so the player
/// always has a next goal. Prefers <see cref="ObjectiveTracker"/> - the same
/// live, per-scene-aware objective the HUD banner shows - so this plaque is
/// never stuck blank waiting for a manually-authored RoomEntryTrigger that
/// may not exist yet at this point in the level (FrozenCity's spawn plaque
/// had exactly this problem: its scene's only RoomEntryTrigger sits near the
/// tower at the far end, and had also been left with its default,
/// MuseumNight-specific placeholder text). Falls back to
/// <see cref="RoomEntryTrigger.CurrentObjective"/> only if no tracker exists.
/// </summary>
[RequireComponent(typeof(TextMeshPro))]
public sealed class WorldObjectiveText : MonoBehaviour
{
    private TextMeshPro label;
    private string lastObjective;

    [Tooltip("Closer than this and the line fades out, so a wide world quad " +
             "cannot fill the screen when the player walks into it.")]
    [SerializeField] private float nearFadeDistance = 3.2f;

    private void Awake()
    {
        label = GetComponent<TextMeshPro>();
    }

    private void Update()
    {
        string current = ObjectiveTracker.Instance != null
            ? ObjectiveTracker.Instance.Objective
            : RoomEntryTrigger.CurrentObjective;

        if (current != lastObjective)
        {
            lastObjective = current;
            label.text = string.IsNullOrEmpty(lastObjective)
                ? string.Empty
                : "Objective: " + lastObjective;
        }

        Camera cam = Camera.main;
        if (cam != null)
        {
            // Yaw-only: also pitching to face a camera above or below reads
            // as tilted/hung crooked for a flat plaque (same fix already
            // applied to Billboard.cs and WorldSignpost.cs).
            Vector3 toCamera = transform.position - cam.transform.position;
            toCamera.y = 0f;
            if (toCamera.sqrMagnitude > 0.0001f)
            {
                transform.rotation = Quaternion.LookRotation(toCamera);
            }

            // Fade out at close range. This is a wide world-space quad, so
            // from a couple of metres away perspective turns it into letters
            // that span the screen and hide the room behind them.
            float toCameraDist = Vector3.Distance(transform.position, cam.transform.position);
            float half = nearFadeDistance * 0.5f;

            label.alpha = toCameraDist >= nearFadeDistance
                ? 1f
                : Mathf.Clamp01((toCameraDist - half) / half);
        }
    }
}
