using UnityEngine;

/// <summary>Trigger 5: silently arms a hidden Time Anchor.</summary>
[RequireComponent(typeof(TimeAnchor))]
public sealed class TimeAnchorTrigger : PlayerTrigger
{
    protected override void OnPlayerEntered(GameObject player)
    {
        GetComponent<TimeAnchor>().Arm();
    }
}
