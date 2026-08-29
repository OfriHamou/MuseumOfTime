using System.Collections;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

/// <summary>
/// Play-mode checks for Step 5.4: dynamic 3D tutorial text.
///
/// The documented verification is largely visual ("rotate the Scene camera
/// and see it in the world" / "a first-time player completes MuseumNight
/// without help") and is left as a manual check in
/// Phase5_Unity_Walkthrough.md. What is cheaply and meaningfully automatable
/// is the structural claim underneath T2's "in 3D" clause - TextMeshPro
/// (world-space) rather than TextMeshProUGUI on a Canvas - and that the text
/// is actually dynamic rather than a fixed label.
/// </summary>
public sealed class TutorialTextTests
{
    private static readonly string[] PlaqueNames =
    {
        "Plaque_Move", "Plaque_Run", "Plaque_Jump", "Plaque_Interact",
        "Plaque_Orb", "Plaque_Camera", "Plaque_Era", "Plaque_SlowTime",
    };

    private GameObject player;

    [UnitySetUp]
    public IEnumerator LoadScene()
    {
        SceneManager.LoadScene("MuseumNight", LoadSceneMode.Single);
        yield return null;
        yield return null;

        player = GameObject.Find("Player");
        Assert.IsNotNull(player, "No 'Player' object in MuseumNight.");
    }

    [Test]
    public void EveryVerbHasAWorldSpacePlaque()
    {
        GameObject parent = GameObject.Find("TutorialPlaques");
        Assert.IsNotNull(parent, "No TutorialPlaques parent in the scene.");

        foreach (string name in PlaqueNames)
        {
            // Transform.Find, not GameObject.Find: the plaques start inactive
            // until their trigger reveals them, and Find only sees active
            // objects.
            Transform plaque = parent.transform.Find(name);
            Assert.IsNotNull(plaque, "Missing tutorial plaque: " + name);

            Assert.IsNotNull(
                plaque.GetComponent<TextMeshPro>(),
                name + " is not a world-space TextMeshPro.");

            Assert.IsNull(
                plaque.GetComponentInParent<Canvas>(),
                name + " is parented under a Canvas, so it is screen space, not 3D.");

            Assert.IsNotNull(
                plaque.GetComponent<WorldTutorialText>(),
                name + " has no WorldTutorialText driving its content.");
        }
    }

    [Test]
    public void EveryVerbTrigger_PointsAtItsPlaque()
    {
        GameObject triggers = GameObject.Find("Triggers");
        Assert.IsNotNull(triggers, "No Triggers parent in the scene.");

        string[] triggerNames =
        {
            "Trigger_TutorialMove", "Trigger_TutorialRun", "Trigger_TutorialJump",
            "Trigger_TutorialInteract", "Trigger_TutorialOrb", "Trigger_TutorialCamera",
            "Trigger_TutorialEra", "Trigger_TutorialSlowTime",
        };

        foreach (string name in triggerNames)
        {
            Transform triggerT = triggers.transform.Find(name);
            Assert.IsNotNull(triggerT, "Missing trigger: " + name);

            var trigger = triggerT.GetComponent<TutorialTrigger>();
            Assert.IsNotNull(trigger, name + " has no TutorialTrigger.");

            var textObject = (GameObject)typeof(TutorialTrigger)
                .GetField("textObject", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                .GetValue(trigger);

            Assert.IsNotNull(textObject, name + " has no textObject wired up - it would reveal nothing.");
        }
    }

    [UnityTest]
    public IEnumerator SlowTimePlaque_TextReflectsTheLiveEnergyValue()
    {
        GameObject plaque = GameObject.Find("TutorialPlaques")
            .transform.Find("Plaque_SlowTime").gameObject;

        GameManager.Instance.State.maxEnergy = 100f;
        GameManager.Instance.State.currentEnergy = 42f;

        CharacterController cc = player.GetComponent<CharacterController>();
        cc.enabled = false;
        player.transform.position = plaque.transform.position;
        cc.enabled = true;

        // TutorialTrigger normally does this; jump straight to "revealed" so
        // the test exercises WorldTutorialText's own dynamic-text behaviour.
        plaque.SetActive(true);
        yield return null;
        yield return null;

        TMP_Text label = plaque.GetComponent<TMP_Text>();

        StringAssert.Contains(
            "42%",
            label.text,
            "The tutorial text did not substitute the player's live energy value.");
    }

    /// <summary>
    /// WorldObjectiveText's intended architecture (Step 5.4 follow-up, the
    /// FrozenCity spawn-plaque fix): prefer the live, always-current
    /// <see cref="ObjectiveTracker"/> - the same source the HUD banner
    /// reads - and only fall back to <see cref="RoomEntryTrigger"/>'s static
    /// value if no tracker exists. This replaces the old
    /// RoomEntryTrigger-first test, which asserted the behaviour this fix
    /// deliberately changed.
    /// </summary>
    [UnityTest]
    public IEnumerator ObjectiveText_PrefersLiveObjectiveTrackerObjective()
    {
        GameObject objective = GameObject.Find("TutorialPlaques")
            .transform.Find("Plaque_Objective")?.gameObject;

        Assert.IsNotNull(objective, "No Plaque_Objective in the scene.");
        Assert.IsNotNull(objective.GetComponent<WorldObjectiveText>(), "Plaque_Objective has no WorldObjectiveText.");

        TMP_Text label = objective.GetComponent<TMP_Text>();

        // A: with a live ObjectiveTracker present (MuseumNight always has
        // one), the plaque reflects ITS objective, not a hand-set trigger
        // value - drive the tracker into a known state via the real
        // GameManager API rather than asserting an exact hardcoded string.
        Assert.IsNotNull(ObjectiveTracker.Instance, "No live ObjectiveTracker in the scene.");
        GameManager.Instance.AcquireTimeLens();
        yield return null;

        StringAssert.Contains(ObjectiveTracker.Instance.Objective, label.text);

        // B: with no ObjectiveTracker available, RoomEntryTrigger's static
        // value remains a valid fallback so the plaque never goes silent.
        typeof(ObjectiveTracker).GetProperty("Instance").SetValue(null, null);
        typeof(RoomEntryTrigger).GetProperty("CurrentObjective").SetValue(null, "Test Objective Text");
        yield return null;

        StringAssert.Contains("Test Objective Text", label.text);
    }
}
