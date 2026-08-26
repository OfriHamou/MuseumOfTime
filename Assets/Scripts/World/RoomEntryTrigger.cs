using UnityEngine;

/// <summary>Trigger 1: entering a hall sets the current objective.</summary>
public sealed class RoomEntryTrigger : PlayerTrigger
{
    [SerializeField] private string roomName = "Main Gallery";
    [SerializeField] private string objective = "Reach the Clock of Creation";

    public static string CurrentRoom { get; private set; } = "";
    public static string CurrentObjective { get; private set; } = "";

    protected override void OnPlayerEntered(GameObject player)
    {
        CurrentRoom = roomName;
        CurrentObjective = objective;
    }
}
