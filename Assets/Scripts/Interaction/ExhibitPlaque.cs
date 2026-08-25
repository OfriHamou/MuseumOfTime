using UnityEngine;

/// <summary>
/// An exhibit plaque. These carry the in-game tutorial: the museum makes
/// instruction text diegetic and world-space rather than a HUD overlay, which
/// is what the "instructions in 3D" requirement is after.
/// </summary>
public sealed class ExhibitPlaque : MonoBehaviour, IInteractable
{
    [TextArea]
    [SerializeField] private string title = "The Clock of Creation";

    [TextArea]
    [SerializeField] private string body =
        "Every exhibit here is a moment, kept.";

    [SerializeField] private int scoreForReading = 25;

    private bool read;

    public string Prompt => read ? "Read again" : "Read the plaque";

    public bool CanInteract => true;

    /// <summary>What the last-read plaque said. Shown by the HUD.</summary>
    public static string LastRead { get; private set; } = "";

    public void Interact(GameObject interactor)
    {
        LastRead = title + " - " + body;

        if (read)
        {
            return;
        }

        read = true;

        if (GameManager.Instance != null)
        {
            GameManager.Instance.AddScore(scoreForReading);
        }
    }
}
