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
        }
    }
}
