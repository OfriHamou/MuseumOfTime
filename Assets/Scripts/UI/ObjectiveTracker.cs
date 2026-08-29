using System;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Works out what the player should be doing right now, and says so.
///
/// The game had a world-space objective plaque near each spawn and eight
/// proximity tutorial plaques, but nothing that travelled with the player.
/// Walk five metres and there was no longer anything on screen telling you
/// what you were trying to achieve - which made a three-scene game with a
/// two-item progression chain, a three-era gear puzzle and a three-phase boss
/// essentially unreadable.
///
/// This reads live game state and produces one short line plus an optional
/// hint. It owns no state of its own and drives nothing: it is a pure
/// projection of GameState, GearPuzzle and Collector, so it can never
/// disagree with the game or need resetting.
/// </summary>
[DefaultExecutionOrder(-50)]
public sealed class ObjectiveTracker : MonoBehaviour
{
    public static ObjectiveTracker Instance { get; private set; }

    /// <summary>The current goal, one short line.</summary>
    public string Objective { get; private set; } = "";

    /// <summary>How to achieve it. May be empty.</summary>
    public string Hint { get; private set; } = "";

    /// <summary>Raised when either line changes.</summary>
    public event Action Changed;

    private string lastObjective = "";
    private string lastHint = "";

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }

        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    private void Update()
    {
        Evaluate();

        if (Objective != lastObjective || Hint != lastHint)
        {
            lastObjective = Objective;
            lastHint = Hint;
            Changed?.Invoke();
        }
    }

    private void Evaluate()
    {
        GameState state = GameManager.Instance != null ? GameManager.Instance.State : null;

        if (state == null)
        {
            Set("", "");
            return;
        }

        switch (SceneManager.GetActiveScene().name)
        {
            case "MuseumNight":
                EvaluateMuseum(state);
                break;

            case "FrozenCity":
                EvaluateFrozenCity(state);
                break;

            case "ClockCore":
                EvaluateClockCore(state);
                break;

            default:
                Set("", "");
                break;
        }
    }

    // ------------------------------------------------------------------

    // ObjectiveHintText is a single fixed-height line under the main
    // objective (see HUDCanvas/ObjectiveBanner) - it does not grow to fit a
    // paragraph. Every hint here is one short line by design; anything a
    // player needs explained at length belongs in a HudMessageFeed toast or
    // a tutorial plaque, not here. A hint that used to be a full paragraph
    // once overflowed the banner and rendered as unbacked text over the
    // game view - keep these short.
    private void EvaluateMuseum(GameState state)
    {
        if (state.hasTimeLens)
        {
            Set("Reach the portal", "The way forward lies where time first broke.");
            return;
        }

        MuseumTimeSealPuzzle puzzle = MuseumTimeSealPuzzle.Instance;

        if (puzzle != null && puzzle.IsSolved)
        {
            Set("Collect the Time Lens", "Something upstairs has awakened.");
            return;
        }

        int restored = puzzle != null ? puzzle.RestoredCount : 0;

        Set("Find the Time Lens",
            "Three memories guard what the museum has lost. (" + restored + "/3)");
    }

    private void EvaluateFrozenCity(GameState state)
    {
        if (state.hasChronoHourglass)
        {
            Set("Leave for the Clock Core",
                "The tower's mechanism points toward what comes next.");
            return;
        }

        GearPuzzle puzzle = GearPuzzle.Instance;

        if (puzzle == null)
        {
            Set("Reach the clock tower", "The frozen tower has lost a piece of its history.");
            return;
        }

        if (!puzzle.HasGear)
        {
            Set("Find the tower's missing gear",
                "What is gone today may still exist before the city froze.");
            return;
        }

        if (!puzzle.Installed)
        {
            Set("Fit the gear into the tower",
                "Some things can be carried farther than time.");
            return;
        }

        if (!puzzle.Verified)
        {
            Set("Check that the tower still runs",
                "See what became of what you changed.");
            return;
        }

        Set("Take the Chrono Hourglass",
            "The restored rhythm has awakened something that can bend time itself.");
    }

    private void EvaluateClockCore(GameState state)
    {
        var collector = FindAnyObjectByType<Collector>();

        if (collector == null)
        {
            Set("Find the Collector", "");
            return;
        }

        if (collector.IsDefeated)
        {
            Set("The timeline is healed", "");
            return;
        }

        switch (collector.CurrentStage)
        {
            // The objective names the ACTION that advances the fight; the
            // hint points at the idea (which era, what changed) without
            // naming it outright or spelling out the button sequence - the
            // era itself is the puzzle here, not just an input to press.
            case Collector.Stage.Shielded:
                Set("Break the Collector's shield",
                    "The barrier was not always this strong.");
                break;

            case Collector.Stage.Present:
                Set("Press the attack",
                    "The present protects what rules it.");
                break;

            default:
                Set("Finish what you started",
                    "Even the strongest things decay with enough time. " +
                    "You carry something that can make a moment last.");
                break;
        }
    }

    private void Set(string objective, string hint)
    {
        Objective = objective;
        Hint = hint;
    }
}
