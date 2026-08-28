using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Tells the player they died.
///
/// Nothing did. `GameManager` raised `PlayerDied` at zero health and the only
/// thing listening was `RespawnService`, which teleported Noa to the last
/// anchor and restored her health - silently. From the player's side the world
/// simply jumped: no screen, no sound cue, no sentence. Reaching zero health
/// and being sent back is the single most important piece of feedback a game
/// owes you, and this one gave none of it.
///
/// So: a full-screen fade with the cause, held long enough to read, then the
/// respawn. RespawnService waits for this before it moves anybody, so the
/// player sees the death happen rather than discovering it afterwards.
/// </summary>
public sealed class DeathOverlay : MonoBehaviour
{
    public static DeathOverlay Instance { get; private set; }

    [SerializeField] private CanvasGroup group;
    [SerializeField] private Image backdrop;
    [SerializeField] private TMP_Text headlineText;
    [SerializeField] private TMP_Text detailText;

    [Tooltip("Seconds the screen is held before the respawn happens.")]
    [SerializeField] private float holdSeconds = 2.2f;

    [SerializeField] private float fadeInSeconds = 0.35f;
    [SerializeField] private float fadeOutSeconds = 0.5f;

    /// <summary>How many times this overlay has been shown. Used by tests.</summary>
    public int ShowCount { get; private set; }

    /// <summary>True while the death screen is on show.</summary>
    public bool IsShowing { get; private set; }

    /// <summary>The last headline displayed. Used by tests.</summary>
    public string LastHeadline { get; private set; } = "";

    /// <summary>A literal line break for the detail copy.</summary>
    private const string NewLine = "\n";

    private void Awake()
    {
        Instance = this;

        if (group != null)
        {
            group.alpha = 0f;
            group.blocksRaycasts = false;
        }
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    /// <summary>
    /// Shows the death screen, holds it, then fades out. The caller awaits
    /// this so the respawn does not happen behind the fade.
    /// </summary>
    public System.Collections.IEnumerator Show(string cause, bool gameOver = false)
    {
        ShowCount++;
        IsShowing = true;
        LastHeadline = gameOver ? "GAME OVER" : "YOU DIED";

        if (headlineText != null)
        {
            headlineText.text = LastHeadline;
        }

        if (detailText != null)
        {
            string outcome = gameOver
                ? "Returning to the main menu."
                : "Returning you to your last Time Anchor.";

            detailText.text = string.IsNullOrWhiteSpace(cause)
                ? outcome
                : cause + NewLine + outcome;
        }

        // Unscaled throughout: the Chrono Hourglass may well have been running
        // when this happened, and a death screen that plays at a third speed
        // because the player happened to be holding Ctrl is its own bug.
        yield return Fade(0f, 1f, fadeInSeconds);

        float until = Time.unscaledTime + holdSeconds;

        while (Time.unscaledTime < until)
        {
            yield return null;
        }

        yield return Fade(1f, 0f, fadeOutSeconds);

        IsShowing = false;
    }

    private System.Collections.IEnumerator Fade(float from, float to, float seconds)
    {
        if (group == null)
        {
            yield break;
        }

        group.blocksRaycasts = to > 0.5f;

        float elapsed = 0f;

        while (elapsed < seconds)
        {
            elapsed += Time.unscaledDeltaTime;
            group.alpha = Mathf.Lerp(from, to, Mathf.Clamp01(elapsed / seconds));
            yield return null;
        }

        group.alpha = to;
        group.blocksRaycasts = to > 0.5f;
    }
}
