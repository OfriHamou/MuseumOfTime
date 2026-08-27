using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

/// <summary>
/// The Time Warden's consequence.
///
/// Every other part of the Warden was covered and passing: the patrol route,
/// the real stop at each waypoint, the cone, the line-of-sight raycast against
/// a code-built mask, the pursue steering, the freeze. What no test asked was
/// what happens when the chase SUCCEEDS - and the answer was nothing at all.
/// Nothing in the project called TakeDamage from a Warden. It could see Noa,
/// pursue her, reach her, and stand there. Walking into one was
/// indistinguishable from walking into a wall.
///
/// That made the whole stealth layer decorative, and it is why a player could
/// reasonably ask whether the figures following them were enemies at all, or
/// whether they were supposed to lose health when touched.
/// </summary>
public sealed class WardenCaptureTests
{
    private GameObject player;
    private WardenAI warden;

    [UnitySetUp]
    public IEnumerator LoadScene()
    {
        SceneManager.LoadScene("MuseumNight", LoadSceneMode.Single);
        yield return null;
        yield return null;

        GameManager.Instance.ResetGame();

        player = GameObject.FindWithTag("Player");
        Assert.IsNotNull(player, "No Player in MuseumNight.");

        warden = Object.FindFirstObjectByType<WardenAI>();
        Assert.IsNotNull(warden, "No Time Warden in MuseumNight.");

        yield return null;
    }

    /// <summary>
    /// Puts the Warden right on top of the player, looking straight at them,
    /// with nothing between - the situation at the end of a successful chase.
    /// </summary>
    private IEnumerator StageACapture()
    {
        var controller = player.GetComponent<CharacterController>();
        if (controller != null) { controller.enabled = false; }

        var agent = warden.GetComponent<NavMeshAgent>();
        if (agent != null) { agent.enabled = false; }

        Vector3 spot = player.transform.position + (player.transform.forward * 1.0f);
        spot.y = warden.transform.position.y;

        warden.transform.position = spot;
        warden.transform.rotation = Quaternion.LookRotation(
            new Vector3(
                player.transform.position.x - spot.x,
                0f,
                player.transform.position.z - spot.z).normalized);

        if (controller != null) { controller.enabled = true; }
        if (agent != null) { agent.enabled = true; }

        // secondsToDetect is 1.6, and detection only climbs while the player
        // is genuinely in the cone with clear line of sight, so this is real
        // detection rather than a state forced from outside.
        float deadline = Time.time + 6f;

        while (warden.CurrentState != WardenAI.State.Chase && Time.time < deadline)
        {
            yield return null;
        }
    }

    [UnityTest]
    public IEnumerator StandingInFrontOfAWardenIsActuallyDetected()
    {
        yield return StageACapture();

        Assert.AreEqual(
            WardenAI.State.Chase, warden.CurrentState,
            "Standing a metre in front of a Warden, in its cone, in clear " +
            "line of sight, never raised detection to a chase.");
    }

    [UnityTest]
    public IEnumerator BeingCaughtCostsHealthAndScore()
    {
        GameManager.Instance.AddScore(500);

        int healthBefore = GameManager.Instance.State.currentHealth;
        int scoreBefore = GameManager.Instance.State.score;

        yield return StageACapture();

        float deadline = Time.time + 5f;

        while (warden.CaptureCount == 0 && Time.time < deadline)
        {
            yield return null;
        }

        Assert.Greater(
            warden.CaptureCount, 0,
            "A Warden stood on top of the player and never caught them. " +
            "Contact with a Warden has no consequence, so the stealth layer " +
            "is decorative.");

        Assert.Less(
            GameManager.Instance.State.currentHealth, healthBefore,
            "Being caught by a Warden cost no health.");

        Assert.Less(
            GameManager.Instance.State.score, scoreBefore,
            "Being caught by a Warden cost no score - T8 asks for score LOSS " +
            "to be real, not only gain.");
    }

    /// <summary>
    /// A capture must be survivable and must let go, or one contact pins the
    /// player in a corner and drains them without any chance to react.
    /// </summary>
    [UnityTest]
    public IEnumerator ACaptureIsSurvivableAndReleasesThePlayer()
    {
        yield return StageACapture();

        float deadline = Time.time + 5f;

        while (warden.CaptureCount == 0 && Time.time < deadline)
        {
            yield return null;
        }

        Assert.Greater(warden.CaptureCount, 0, "No capture happened.");

        Assert.Greater(
            GameManager.Instance.State.currentHealth, 0,
            "A single Warden capture killed the player outright. A first " +
            "mistake should teach, not end the run.");

        // Note this deliberately does NOT assert the Warden leaves Chase.
        // A capture drops it to Search and zeroes detection, but a player who
        // is still standing in the open a metre away is still plainly
        // visible, so re-acquiring them within a frame or two is the correct
        // behaviour - the grace is time to run, not invisibility.
        //
        // The guarantee that actually stops a Warden pinning the player in a
        // corner and draining them is the interval BETWEEN captures. Timing
        // that needs both ends observed: the first capture lands during the
        // wait for Chase above, so the moment the test notices the count is
        // already non-zero is well after the capture itself, and measuring
        // from there understates the gap by however long the wait took.
        yield return AssertGapBetweenTwoObservedCaptures();
    }

    private IEnumerator AssertGapBetweenTwoObservedCaptures()
    {
        int mark = warden.CaptureCount;
        float giveUp = Time.time + 12f;

        while (warden.CaptureCount == mark && Time.time < giveUp)
        {
            yield return null;
        }

        Assert.Greater(
            warden.CaptureCount, mark,
            "The Warden never landed a second capture, so the interval " +
            "between captures cannot be measured.");

        float firstSeenAt = Time.time;
        mark = warden.CaptureCount;

        while (warden.CaptureCount == mark && Time.time < giveUp)
        {
            yield return null;
        }

        Assert.Greater(
            warden.CaptureCount, mark,
            "No third capture arrived within the window.");

        float gap = Time.time - firstSeenAt;

        Assert.Greater(
            gap, 2.5f,
            "Two consecutive captures were " + gap.ToString("F2") + " s apart. " +
            "The cooldown is not holding, so one bad corner drains the player " +
            "with no chance to act.");
    }

    /// <summary>
    /// The Chrono Orb has to remain a real answer to a Warden, since that is
    /// what the nameplate now tells the player to do.
    /// </summary>
    [UnityTest]
    public IEnumerator AFrozenWardenCannotCapture()
    {
        yield return StageACapture();

        warden.Freeze(3f);
        yield return null;

        int before = warden.CaptureCount;

        for (int i = 0; i < 30; i++) { yield return null; }

        Assert.AreEqual(
            WardenAI.State.Frozen, warden.CurrentState,
            "Freezing the Warden did not put it in the Frozen state.");

        Assert.AreEqual(
            before, warden.CaptureCount,
            "A frozen Warden still caught the player, so the Orb is not " +
            "actually an answer to being chased.");
    }
}
