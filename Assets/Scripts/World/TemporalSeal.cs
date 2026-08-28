using UnityEngine;

/// <summary>
/// One of MuseumNight's three Temporal Seals - a museum exhibit that only
/// answers Noa while she is standing in its own era. This is what teaches
/// Q/R naturally: pressing E in the wrong era gives a clear, specific reason
/// rather than doing nothing. Deliberately dumb - it knows its own era and
/// nothing else, so the puzzle stays three of these rather than a framework.
/// </summary>
public sealed class TemporalSeal : MonoBehaviour, IInteractable
{
    [SerializeField] private TimeEra requiredEra = TimeEra.Present;

    [Tooltip("The floating world-space sign above the seal. Auto-found in " +
             "children if left unassigned.")]
    [SerializeField] private TMPro.TextMeshPro label;

    [Tooltip("The riddle image quad (a child named RiddleImage). Auto-found " +
             "if left unassigned.")]
    [SerializeField] private MeshRenderer riddleImage;

    private static readonly Color NeutralTint = new Color(0.55f, 0.55f, 0.6f, 1f);
    private static readonly Color RestoredTint = new Color(1.4f, 1.3f, 0.85f, 1f);

    public bool IsRestored { get; private set; }

    /// <summary>Exposed so a test can drive the puzzle without guessing eras from names.</summary>
    public TimeEra RequiredEra => requiredEra;

    public string Prompt => IsRestored
        ? "Temporal Seal (restored)"
        : "Activate Temporal Seal";

    public bool CanInteract => !IsRestored;

    private void Awake()
    {
        if (label == null)
        {
            label = GetComponentInChildren<TMPro.TextMeshPro>();
        }

        if (riddleImage == null)
        {
            Transform found = transform.Find("RiddleImage");
            riddleImage = found != null ? found.GetComponent<MeshRenderer>() : null;
        }
    }

    private void Start()
    {
        RefreshLabel();
    }

    public void Interact(GameObject interactor)
    {
        if (IsRestored)
        {
            return;
        }

        if (EraManager.Instance == null || EraManager.Instance.CurrentEra != requiredEra)
        {
            HudMessageFeed.Post(
                "The timeline does not match this memory. Study the exhibit and shift time with Q / R.",
                HudMessageFeed.Tone.Bad);

            if (AudioManager.Instance != null)
            {
                AudioManager.Instance.Play(AudioManager.Sfx.SealRejected);
            }

            return;
        }

        IsRestored = true;
        RefreshLabel();

        if (MuseumTimeSealPuzzle.Instance != null)
        {
            MuseumTimeSealPuzzle.Instance.RegisterRestored();
        }
    }

    /// <summary>
    /// Keeps the floating sign in sync with restoration state, so a solved
    /// seal reads as solved from across the room rather than staying
    /// permanently on its neutral text.
    ///
    /// Deliberately era-blind: the label used to read "PAST SEAL / Requires:
    /// Past", which handed the puzzle's answer to the player before they had
    /// solved anything. The era clue now lives entirely in the riddle image
    /// (the RiddleImage quad this class also tints on restore), so this
    /// label only ever says which exhibit it is and whether it is solved -
    /// never which era it wants.
    /// </summary>
    private void RefreshLabel()
    {
        if (label == null)
        {
            return;
        }

        const string title = "TEMPORAL SEAL";

        // The checkmark glyph (U+2713) is not in LiberationSans SDF and
        // silently falls back to a hollow box, so the restored state is
        // carried by the color change and the word itself, not a symbol.
        label.text = IsRestored
            ? title + "\n<size=70%><color=#8CFF8C>RESTORED</color></size>"
            : title;

        if (riddleImage != null)
        {
            riddleImage.material.color = IsRestored ? RestoredTint : NeutralTint;
        }
    }
}
