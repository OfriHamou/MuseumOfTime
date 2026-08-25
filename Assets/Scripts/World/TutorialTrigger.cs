using UnityEngine;

/// <summary>Trigger 2: reveals the world-space tutorial text for one verb.</summary>
public sealed class TutorialTrigger : PlayerTrigger
{
    [SerializeField] private GameObject textObject;

    [TextArea]
    [SerializeField] private string message = "Hold W to walk";

    public static string LastMessage { get; private set; } = "";

    protected override void OnPlayerEntered(GameObject player)
    {
        LastMessage = message;

        if (textObject != null)
        {
            textObject.SetActive(true);
        }
    }
}
