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

    private void EvaluateMuseum(GameState state)
    {
        if (!state.hasTimeLens)
        {
            // Say WHICH WAY. "Up the stairs" is not a direction in a dark
            // thirty-metre hall with one unlit ramp in a far corner - the
            // ramp is at the west end, and a player told only that stairs
            // exist will wander the room looking for them.
            Set("Find the Time Lens",
                "Take the ramp at the WEST end of the hall, then follow the " +
                "mezzanine east to the curator's office.");
            return;
        }

        Set("Leave the museum",
            "The Time Lens is yours. Q and R now travel between eras. " +
            "Head for the exit on the upper floor.");
    }

    private void EvaluateFrozenCity(GameState state)
    {
        if (state.hasChronoHourglass)
        {
            Set("Leave for the Clock Core",
                "The Chrono Hourglass is yours. Find the way out of the city.");
            return;
        }

        GearPuzzle puzzle = GearPuzzle.Instance;

        if (puzzle == null)
        {
            Set("Reach the clock tower", "Follow the frozen street north.");
            return;
        }

        if (!puzzle.HasGear)
        {
            Set("Find the tower's missing gear",
                "Press Q to reach the PAST, where the gear had not been lost yet.");
            return;
        }

        if (!puzzle.Installed)
        {
            Set("Fit the gear into the tower",
                "Press R to return to the PRESENT, then press E at the socket.");
            return;
        }

        if (!puzzle.Verified)
        {
            Set("Check that the tower still runs",
                "Press R to reach the FUTURE and inspect the mechanism.");
            return;
        }

        Set("Take the Chrono Hourglass", "The tower has given up its reward.");
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
            case Collector.Stage.Shielded:
                Set("Break the Collector's shield",
                    "Press Q for the PAST, then throw the Chrono Orb with the left mouse button.");
                break;

            case Collector.Stage.Present:
                Set("Survive the summoned Warden",
                    "Press R for the PRESENT. Stay out of its cone of vision, or freeze it with the Orb.");
                break;

            default:
                Set("Strike while time is slowed",
                    "Press R for the FUTURE, hold CTRL to slow time, then hit it with the Orb.");
                break;
        }
    }

    private void Set(string objective, string hint)
    {
        Objective = objective;
        Hint = hint;
    }
}
