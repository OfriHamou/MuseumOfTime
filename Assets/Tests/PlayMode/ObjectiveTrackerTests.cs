using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

/// <summary>
/// Cover for the on-screen objective, added because the game was unreadable:
/// a three-scene progression with a two-item chain, a three-era gear puzzle
/// and a three-phase boss, and nothing on screen saying what to do next.
///
/// These assert that the objective actually TRACKS state rather than being a
/// fixed label - a static "find the lens" line that never changes would look
/// identical in a screenshot and help nobody.
/// </summary>
public sealed class ObjectiveTrackerTests
{
    private static IEnumerator Load(string sceneName)
    {
        SceneManager.LoadScene(sceneName, LoadSceneMode.Single);
        yield return null;
        yield return null;
    }

    private static ObjectiveTracker Tracker()
    {
        var tracker = Object.FindFirstObjectByType<ObjectiveTracker>();
        Assert.IsNotNull(
            tracker,
            SceneManager.GetActiveScene().name + " has no ObjectiveTracker, so " +
            "the player is never told what to do.");
        return tracker;
    }

    [UnityTest]
    public IEnumerator EveryGameplaySceneHasAnObjectiveOnScreen()
    {
        foreach (string sceneName in new[] { "MuseumNight", "FrozenCity", "ClockCore" })
        {
            yield return Load(sceneName);
            yield return null;

            ObjectiveTracker tracker = Tracker();

            Assert.IsNotEmpty(
                tracker.Objective,
                sceneName + " shows no objective.");

            var hud = Object.FindFirstObjectByType<HUDController>();
            Assert.IsNotNull(hud, sceneName + " has no HUDController to show it on.");
        }
    }

    [UnityTest]
    public IEnumerator MuseumObjective_ChangesOnceTheTimeLensIsFound()
    {
        yield return Load("MuseumNight");
        yield return null;

        ObjectiveTracker tracker = Tracker();

        GameManager.Instance.ResetGame();
        yield return null;

        string before = tracker.Objective;
        StringAssert.Contains("Time Lens", before,
            "Before finding it, the objective should point at the Time Lens.");

        GameManager.Instance.AcquireTimeLens();
        yield return null;

        Assert.AreNotEqual(
            before, tracker.Objective,
            "The objective did not change after the Time Lens was acquired - it " +
            "is a fixed label, not a tracker.");
    }

    /// <summary>
    /// Replaces the old NamesTheEraTheBossPhaseNeeds test, which asserted the
    /// exact thing the rest-of-game polish pass deliberately removed: the
    /// hint used to spell out the era and the key ("Press Q until the era
    /// reads PAST..."), which handed the puzzle to the player. The intended
    /// design is objective = overall goal, hint = an idea to chase, era =
    /// something the player works out - so this now asserts the CLUE text
    /// for all three phases, and that phase 1's clue does not name "PAST"
    /// outright (phases 2/3 legitimately use the words "present" and
    /// "future" as ordinary English inside a sentence, e.g. "The present
    /// protects what rules it.", which is the intended wordplay, not a
    /// regression back to a shouted era name).
    /// </summary>
    [UnityTest]
    public IEnumerator ClockCoreObjective_GivesNonSpoilingClueForBossPhase()
    {
        yield return Load("ClockCore");
        yield return null;

        ObjectiveTracker tracker = Tracker();
        var collector = Object.FindFirstObjectByType<Collector>();
        Assert.IsNotNull(collector, "No Collector in ClockCore.");

        Assert.AreEqual(Collector.Stage.Shielded, collector.CurrentStage,
            "The fight should start shielded.");

        yield return null;

        // Phase 1 (Shielded): a clue toward "earlier", never the era itself.
        Assert.AreEqual("Break the Collector's shield", tracker.Objective);
        Assert.AreEqual("The barrier was not always this strong.", tracker.Hint);
        StringAssert.DoesNotContain("PAST", tracker.Hint.ToUpperInvariant(),
            "Phase 1's hint should be a clue, not name the era outright.");

        // Drive to Phase 2 with a real hit in the era phase 1 actually needs,
        // the same as a player landing it would.
        EraManager.Instance.SetEra(TimeEra.Past);
        collector.TakeOrbHit(null);
        collector.TakeOrbHit(null);
        yield return null;

        Assert.AreEqual(Collector.Stage.Present, collector.CurrentStage,
            "Two hits in the Past should break the shield.");
        Assert.AreEqual("Press the attack", tracker.Objective);
        Assert.AreEqual("The present protects what rules it.", tracker.Hint);

        // Drive to Phase 3 with a real hit in the Present.
        EraManager.Instance.SetEra(TimeEra.Present);
        collector.TakeOrbHit(null);
        yield return null;

        Assert.AreEqual(Collector.Stage.Future, collector.CurrentStage,
            "A hit in the Present should push the fight into the Future.");
        Assert.AreEqual("Finish what you started", tracker.Objective);
        Assert.AreEqual(
            "Even the strongest things decay with enough time. " +
            "You carry something that can make a moment last.",
            tracker.Hint);

        string upperHint = tracker.Hint.ToUpperInvariant();
        StringAssert.DoesNotContain("FUTURE", upperHint,
            "Phase 3's hint should be a clue, not name the era outright.");
        StringAssert.DoesNotContain("CTRL", upperHint,
            "Phase 3's hint should not spell out the exact key to press.");
    }
}
