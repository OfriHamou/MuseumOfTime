using TMPro;
using UnityEngine;

/// <summary>
/// A controls card shown at the start of a scene and dismissed on the first
/// meaningful input, or after a timeout.
///
/// The pause menu already lists the controls, but a player who has never seen
/// the game has no reason to press Escape looking for them - the bindings go
/// well beyond WASD (era travel on Q/R, slow time on CTRL, camera toggle on C)
/// and none of them are guessable. Showing them once, unprompted, is the
/// difference between "I don't understand what to do" and playing.
///
/// It runs on unscaled time so it behaves the same whether or not slow-time is
/// engaged, and it never blocks input - it is a card, not a modal.
/// </summary>
public sealed class ControlsHintCard : MonoBehaviour
{
    [SerializeField] private CanvasGroup group;

    [Tooltip("Seconds fully visible before it starts fading on its own.")]
    [SerializeField] private float holdSeconds = 9f;

    [SerializeField] private float fadeSeconds = 1.2f;

    [Tooltip("Dismiss as soon as the player actually moves or looks.")]
    [SerializeField] private bool dismissOnInput = true;

    private PlayerInputReader inputReader;
    private float elapsed;
    private bool dismissing;

    private void Awake()
    {
        if (group == null)
        {
            group = GetComponent<CanvasGroup>();
        }

        inputReader = FindAnyObjectByType<PlayerInputReader>();
    }

    private void OnEnable()
    {
        elapsed = 0f;
        dismissing = false;

        if (group != null)
        {
            group.alpha = 1f;
        }
    }

    private void Update()
    {
        if (group == null)
        {
            return;
        }

        // Unscaled: the card must not linger for three times as long just
        // because the player is holding the Chrono Hourglass.
        elapsed += Time.unscaledDeltaTime;

        if (!dismissing && ShouldDismiss())
        {
            dismissing = true;
        }

        if (!dismissing)
        {
            return;
        }

        group.alpha -= Time.unscaledDeltaTime / Mathf.Max(0.01f, fadeSeconds);

        if (group.alpha <= 0f)
        {
            gameObject.SetActive(false);
        }
    }

    private bool ShouldDismiss()
    {
        if (elapsed >= holdSeconds)
        {
            return true;
        }

        if (!dismissOnInput || inputReader == null)
        {
            return false;
        }

        // A couple of seconds' grace, so the card cannot be wiped out by the
        // stray mouse movement that happens as the window takes focus.
        if (elapsed < 2f)
        {
            return false;
        }

        return inputReader.MoveInput.sqrMagnitude > 0.04f ||
               inputReader.JumpPressed ||
               inputReader.InteractPressed;
    }
}
