using TMPro;
using UnityEngine;

/// <summary>
/// A persistent world-space objective line at a doorway, so the player
/// always has a next goal. Reads RoomEntryTrigger's static
/// <see cref="RoomEntryTrigger.CurrentObjective"/> (Step 3.2) rather than
/// duplicating objective text here.
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
        if (RoomEntryTrigger.CurrentObjective != lastObjective)
        {
            lastObjective = RoomEntryTrigger.CurrentObjective;
            label.text = string.IsNullOrEmpty(lastObjective)
                ? string.Empty
                : "Objective: " + lastObjective;
        }

        Camera cam = Camera.main;
        if (cam != null)
        {
            transform.rotation = Quaternion.LookRotation(transform.position - cam.transform.position);

            // Fade out at close range. This is a wide world-space quad, so
            // from a couple of metres away perspective turns it into letters
            // that span the screen and hide the room behind them.
            float toCamera = Vector3.Distance(transform.position, cam.transform.position);
            float half = nearFadeDistance * 0.5f;

            label.alpha = toCamera >= nearFadeDistance
                ? 1f
                : Mathf.Clamp01((toCamera - half) / half);
        }
    }
}
