using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The gameplay HUD: health, energy, shards, era and the two acquired items.
/// Subscribes to GameManager.StateChanged rather than polling in Update, so
/// the health/energy/score-driven elements only touch the UI when something
/// actually changed.
///
/// The one exception is the detection meter: WardenAI has no "detection
/// changed" event to subscribe to (Phase 4 code, out of scope to extend
/// here), so that one value is read each frame purely for display - nothing
/// here writes back into game or AI state.
/// </summary>
public sealed class HUDController : MonoBehaviour
{
    [SerializeField] private Image healthFill;
    [SerializeField] private Image energyFill;

    [Tooltip("Numbers beside the bars. A bar at 20% is a sliver that reads " +
             "as empty, and a player who thinks they are at zero and sees " +
             "nothing happen concludes the game is broken.")]
    [SerializeField] private TMP_Text healthValueText;
    [SerializeField] private TMP_Text energyValueText;
    [SerializeField] private TMP_Text shardText;
    [SerializeField] private TMP_Text eraText;
    [SerializeField] private GameObject timeLensIcon;
    [SerializeField] private GameObject hourglassIcon;
    [SerializeField] private GameObject detectionMeterRoot;
    [SerializeField] private Image detectionFill;

    [Header("Interaction prompt")]
    [SerializeField] private GameObject interactPromptRoot;
    [SerializeField] private TMP_Text interactPromptText;

    [Header("Objective")]
    [SerializeField] private TMP_Text objectiveText;
    [SerializeField] private TMP_Text objectiveHintText;

    [Header("Temporal Erosion warning (ClockCore's final phase)")]
    [SerializeField] private GameObject erosionWarningRoot;
    [SerializeField] private CanvasGroup erosionWarningGroup;
    [SerializeField] private TMP_Text erosionWarningTitle;
    [SerializeField] private TMP_Text erosionWarningStatus;

    [Header("Collector boss bar (ClockCore only)")]
    [SerializeField] private GameObject bossBarRoot;
    [SerializeField] private TMP_Text bossBarTitleText;
    [SerializeField] private TMP_Text bossBarLabelText;
    [SerializeField] private Image bossBarFill;

    private WardenAI[] wardens;
    private PlayerInteractor interactor;
    private ObjectiveTracker objectives;
    private Collector collector;

    private void Start()
    {
        wardens = FindObjectsByType<WardenAI>(FindObjectsSortMode.None);
        interactor = GetComponent<PlayerInteractor>();

        objectives = ObjectiveTracker.Instance;
        if (objectives == null)
        {
            objectives = FindAnyObjectByType<ObjectiveTracker>();
        }

        if (objectives != null)
        {
            objectives.Changed += RefreshObjective;
            RefreshObjective();
        }

        if (detectionMeterRoot != null)
        {
            detectionMeterRoot.SetActive(false);
        }

        if (interactPromptRoot != null)
        {
            interactPromptRoot.SetActive(false);
        }

        collector = FindAnyObjectByType<Collector>();

        if (erosionWarningRoot != null)
        {
            erosionWarningRoot.SetActive(false);
        }

        if (bossBarRoot != null)
        {
            bossBarRoot.SetActive(false);

            if (bossBarTitleText != null)
            {
                bossBarTitleText.text = "COLLECTOR";
            }
        }

        if (GameManager.Instance != null)
        {
            GameManager.Instance.StateChanged += Refresh;
            Refresh();
        }
    }

    private void OnDestroy()
    {
        if (GameManager.Instance != null)
        {
            GameManager.Instance.StateChanged -= Refresh;
        }

        if (objectives != null)
        {
            objectives.Changed -= RefreshObjective;
        }
    }

    /// <summary>
    /// Mirrors ObjectiveTracker onto the HUD. Event-driven rather than polled:
    /// the tracker only raises Changed when the line actually differs.
    /// </summary>
    private void RefreshObjective()
    {
        if (objectives == null)
        {
            return;
        }

        if (objectiveText != null)
        {
            objectiveText.text = objectives.Objective;
        }

        if (objectiveHintText != null)
        {
            objectiveHintText.text = objectives.Hint;
        }
    }

    private void Refresh()
    {
        GameState state = GameManager.Instance.State;

        if (healthFill != null) healthFill.fillAmount = (float)state.currentHealth / state.maxHealth;
        if (energyFill != null) energyFill.fillAmount = state.currentEnergy / state.maxEnergy;

        if (healthValueText != null)
        {
            healthValueText.text = state.currentHealth + " / " + state.maxHealth;
        }

        if (energyValueText != null)
        {
            energyValueText.text = Mathf.RoundToInt(state.currentEnergy) + " / " +
                                   Mathf.RoundToInt(state.maxEnergy);
        }
        if (shardText != null) shardText.text = state.timeShards.ToString();
        if (eraText != null) eraText.text = state.currentEra.ToString();
        if (timeLensIcon != null) timeLensIcon.SetActive(state.hasTimeLens);
        if (hourglassIcon != null) hourglassIcon.SetActive(state.hasChronoHourglass);
    }

    private void Update()
    {
        UpdateInteractPrompt();
        UpdateErosionWarning();
        UpdateBossBar();

        if (detectionMeterRoot == null || wardens == null || wardens.Length == 0)
        {
            return;
        }

        float highest = 0f;

        foreach (WardenAI warden in wardens)
        {
            if (warden != null)
            {
                highest = Mathf.Max(highest, warden.DetectionLevel);
            }
        }

        detectionMeterRoot.SetActive(highest > 0.01f);

        if (detectionFill != null)
        {
            detectionFill.fillAmount = highest;
        }
    }

    /// <summary>
    /// Keeps the temporal erosion warning honest: a countdown while the
    /// grace window is a real, fixed number of seconds, then a plain
    /// "health decaying" status once it starts actually being continuous
    /// damage rather than a timer - a fake countdown there would lie about
    /// how the mechanic works. Hidden the instant erosion is not currently
    /// hurting the player (phase over, or the Hourglass is holding it off),
    /// so the warning going away is itself the feedback that whatever the
    /// player just did worked.
    /// </summary>
    private void UpdateErosionWarning()
    {
        if (erosionWarningRoot == null)
        {
            return;
        }

        bool show = collector != null && collector.ErosionWarningActive;

        if (erosionWarningRoot.activeSelf != show)
        {
            erosionWarningRoot.SetActive(show);
        }

        if (!show)
        {
            return;
        }

        bool inGrace = collector.ErosionInGrace;

        if (erosionWarningTitle != null)
        {
            erosionWarningTitle.text = inGrace ? "TIMELINE COLLAPSE" : "TEMPORAL EROSION";
        }

        if (erosionWarningStatus != null)
        {
            if (inGrace)
            {
                erosionWarningStatus.text = collector.ErosionGraceRemaining.ToString("0.0");
            }
            else
            {
                // Erosion resuming because the player let go of Ctrl on
                // purpose (to aim, to breathe) and erosion resuming because
                // the energy bar ran dry underneath them read identically
                // otherwise - the second one needs its own label, or a
                // player doing everything right still cannot tell they are
                // one second from being unable to afford Slow Time at all.
                bool lowEnergy = GameManager.Instance != null &&
                    GameManager.Instance.State.currentEnergy <=
                    GameManager.Instance.State.maxEnergy * 0.15f;

                erosionWarningStatus.text = lowEnergy
                    ? "HEALTH DECAYING - LOW ENERGY"
                    : "HEALTH DECAYING";
            }
        }

        if (erosionWarningGroup != null)
        {
            float healthFraction = GameManager.Instance != null
                ? (float)GameManager.Instance.State.currentHealth / GameManager.Instance.State.maxHealth
                : 1f;

            // Pulses faster the closer health gets to zero, so the warning
            // visibly sharpens as death approaches instead of sitting at one
            // constant intensity the whole phase.
            float pulseHz = Mathf.Lerp(1.2f, 3.5f, 1f - healthFraction);
            erosionWarningGroup.alpha = 0.55f + 0.45f * Mathf.Abs(Mathf.Sin(Time.time * pulseHz * Mathf.PI));
        }
    }

    [Tooltip("How close the player has to be to the Collector before its bar " +
             "appears - close enough to mean 'the fight is on', not the " +
             "instant the player enters the chamber from a distance.")]
    [SerializeField] private float bossBarShowDistance = 22f;

    /// <summary>
    /// Shows the boss bar once the player is near enough to the Collector to
    /// be fighting it, and keeps it tracking real hits landed - never a
    /// rejected or wrong-era one, since Collector only counts BossHitsLanded
    /// on its own accepted-hit paths. Hidden again once defeated.
    /// </summary>
    private void UpdateBossBar()
    {
        if (bossBarRoot == null || collector == null)
        {
            return;
        }

        bool nearFight = collector.BossBarActive &&
            Vector3.Distance(transform.position, collector.transform.position) <= bossBarShowDistance;

        if (bossBarRoot.activeSelf != nearFight)
        {
            bossBarRoot.SetActive(nearFight);
        }

        if (!nearFight)
        {
            return;
        }

        if (bossBarLabelText != null)
        {
            bossBarLabelText.text = collector.BossBarLabel;
        }

        if (bossBarFill != null)
        {
            bossBarFill.fillAmount = collector.BossProgressFraction;
        }
    }

    /// <summary>
    /// Shows what the thing under the crosshair would do if E were pressed.
    /// Polled rather than event-driven for the same reason as the detection
    /// meter: PlayerInteractor raycasts per frame and has no change event.
    /// </summary>
    private void UpdateInteractPrompt()
    {
        if (interactPromptRoot == null)
        {
            return;
        }

        string prompt = interactor != null ? interactor.CurrentPrompt : null;
        bool show = !string.IsNullOrEmpty(prompt);

        if (interactPromptRoot.activeSelf != show)
        {
            interactPromptRoot.SetActive(show);
        }

        if (show && interactPromptText != null && interactPromptText.text != prompt)
        {
            interactPromptText.text = prompt;
        }
    }
}
