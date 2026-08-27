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

    [UnityTest]
    public IEnumerator ClockCoreObjective_NamesTheEraTheBossPhaseNeeds()
    {
        yield return Load("ClockCore");
        yield return null;

        ObjectiveTracker tracker = Tracker();
        var collector = Object.FindFirstObjectByType<Collector>();
        Assert.IsNotNull(collector, "No Collector in ClockCore.");

        Assert.AreEqual(Collector.Stage.Shielded, collector.CurrentStage,
            "The fight should start shielded.");

        yield return null;

        // The whole point of the hint: the boss is unreadable without being
        // told which era each phase needs.
        StringAssert.Contains("PAST", tracker.Hint,
            "Phase 1's hint should tell the player to reach the Past.");
    }
}
