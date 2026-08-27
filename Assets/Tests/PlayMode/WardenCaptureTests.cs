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

        // Neutralise the opening grace period for these tests.
        //
        // MuseumNight's Warden deliberately does not hunt for the first
        // twelve seconds, because it used to spot the player at t=0 from 8.5 m
        // and kill them while the tutorial card was still on screen. That
        // grace is real behaviour worth having and it is asserted separately
        // in TheOpeningGraceKeepsTheWardenBlind - but every test here is about
        // what happens once the Warden IS hunting, and waiting twelve seconds
        // in each of them only makes the suite slower.
        SetPrivate(warden, "huntDelaySeconds", 0f);

        yield return null;
    }

    private static void SetPrivate(object target, string field, float value)
    {
        System.Reflection.FieldInfo info = target.GetType().GetField(
            field,
            System.Reflection.BindingFlags.Instance |
            System.Reflection.BindingFlags.NonPublic);

        Assert.IsNotNull(info, "WardenAI has no field called " + field + ".");
        info.SetValue(target, value);
    }

    /// <summary>
    /// Puts the Warden right on top of the player, looking straight at them,
    /// with nothing between - the situation at the end of a successful chase.
    /// </summary>
    /// <summary>
    /// Moves the Warden onto the player without disturbing its agent.
    ///
    /// An earlier version toggled NavMeshAgent.enabled off and on every frame
    /// to move the transform directly. That jittered the Warden as the agent
    /// re-snapped to the navmesh on each re-enable, which broke line of sight
    /// often enough that detection decayed almost as fast as it accumulated -
    /// the Warden sat at Alert forever and never reached Chase. Warp is the
    /// supported way to relocate an agent and does not fight it.
    /// </summary>
    private void PlaceWardenOnPlayer(NavMeshAgent agent)
    {
        Vector3 spot = player.transform.position + (player.transform.forward * 1.1f);
        spot.y = warden.transform.position.y;

        if (agent != null && agent.enabled && agent.isOnNavMesh)
        {
            agent.Warp(spot);
        }
        else
        {
            warden.transform.position = spot;
        }

        warden.transform.rotation = Quaternion.LookRotation(
            new Vector3(
                player.transform.position.x - warden.transform.position.x,
                0f,
                player.transform.position.z - warden.transform.position.z).normalized);
    }

    private IEnumerator StageACapture()
    {
        yield return PinWardenToPlayer(10f);
    }

    /// <summary>
    /// Holds the Warden on top of the player, facing them, for a while.
    ///
    /// It has to keep re-placing them rather than positioning once, because a
    /// Warden that captures now returns to its patrol route and walks away -
    /// which is the fix for the death loop and is exactly the behaviour these
    /// tests must not accidentally undo. Positioning once and waiting would
    /// measure how fast it leaves, not what happens when it catches someone.
    ///
    /// Re-placing models the worst case the player can actually be in: cornered,
    /// with nowhere to break line of sight.
    /// </summary>
    private IEnumerator PinWardenToPlayer(float seconds)
    {
        var controller = player.GetComponent<CharacterController>();
        var agent = warden.GetComponent<NavMeshAgent>();

        float until = Time.unscaledTime + seconds;

        while (Time.unscaledTime < until)
        {
            PlaceWardenOnPlayer(agent);

            yield return null;

            if (warden.CurrentState == WardenAI.State.Chase &&
                warden.CaptureCount > 0)
            {
                yield break;
            }
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

        float deadline = Time.unscaledTime + 5f;

        while (warden.CaptureCount == 0 && Time.unscaledTime < deadline)
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

        float deadline = Time.unscaledTime + 5f;

        while (warden.CaptureCount == 0 && Time.unscaledTime < deadline)
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
        float firstSeenAt = -1f;

        // Long enough to span two five second cooldowns with margin, while
        // keeping the player cornered so captures actually keep coming.
        float giveUp = Time.unscaledTime + 30f;

        var controller = player.GetComponent<CharacterController>();
        var agent = warden.GetComponent<NavMeshAgent>();

        while (Time.unscaledTime < giveUp)
        {
            PlaceWardenOnPlayer(agent);

            yield return null;

            if (warden.CaptureCount == mark)
            {
                continue;
            }

            mark = warden.CaptureCount;

            if (firstSeenAt < 0f)
            {
                firstSeenAt = Time.time;
                continue;
            }

            float gap = Time.unscaledTime - firstSeenAt;

            Assert.Greater(
                gap, 2.5f,
                "Two consecutive captures were " + gap.ToString("F2") + " s " +
                "apart. The cooldown is not holding, so one bad corner drains " +
                "the player with no chance to act.");

            yield break;
        }

        Assert.Fail("Never observed two captures, so the interval could not be measured.");
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

    /// <summary>
    /// The opening seconds of a scene belong to the player, not the guard.
    /// </summary>
    [UnityTest]
    public IEnumerator TheOpeningGraceKeepsTheWardenBlind()
    {
        SetPrivate(warden, "huntDelaySeconds", 30f);

        yield return StageACapture();   // gives up after its own deadline

        Assert.AreNotEqual(
            WardenAI.State.Chase, warden.CurrentState,
            "The Warden hunted during its opening grace period. MuseumNight " +
            "is the teaching scene: it used to spot the player at t=0 from " +
            "8.5 m away and kill them while the tutorial card was still up.");

        Assert.AreEqual(
            0, warden.CaptureCount,
            "The Warden captured the player during the grace period.");
    }

    /// <summary>
    /// The rule that makes the whole encounter safe to lose.
    /// </summary>
    [UnityTest]
    public IEnumerator RepeatedCapturesCanNeverKillThePlayer()
    {
        GameManager.Instance.ResetGame();

        yield return StageACapture();

        // Long enough for many captures at a five second cooldown.
        float until = Time.unscaledTime + 40f;
        var pin = PinWardenToPlayer(40f);

        while (Time.unscaledTime < until)
        {
            pin.MoveNext();

            Assert.Greater(
                GameManager.Instance.State.currentHealth, 0,
                "A Warden killed the player. Captures must never be able to " +
                "do that: the player respawns in the same room the Warden " +
                "patrols, so a lethal capture loops forever with no way out. " +
                "Measured before the floor existed: twenty-one captures, " +
                "dead, respawned, dead again.");

            Assert.AreEqual(
                0, GameManager.Instance.State.deaths,
                "The player died to a Warden.");

            yield return null;
        }

        Assert.Greater(
            warden.CaptureCount, 1,
            "The test never actually landed repeated captures, so it proved " +
            "nothing. Expected the Warden to keep catching the player.");

        Assert.Less(
            GameManager.Instance.State.currentHealth, 100,
            "Captures cost nothing at all, so being caught is meaningless.");

        Assert.GreaterOrEqual(
            GameManager.Instance.State.currentHealth,
            Mathf.CeilToInt(GameManager.Instance.State.maxHealth * 0.2f),
            "Health fell below the floor a Warden is allowed to take it to.");
    }

    /// <summary>
    /// A chase must end, and the Warden must never stand inside the player.
    ///
    /// Both were broken at once. NavMeshAgent.stoppingDistance was left at
    /// zero, so the agent's destination was the player's exact position, and
    /// the Warden has no collider to stop it arriving there - it stood inside
    /// Noa. And nothing ever ended a chase while it could still see her, with
    /// a chase speed of 4.6 against a walk speed of 4, so no amount of walking
    /// or turning got away from it. It read as being permanently stuck to the
    /// player.
    /// </summary>
    [UnityTest]
    public IEnumerator AChaseEndsAndTheWardenKeepsItsDistance()
    {
        yield return StageACapture();

        Assert.AreEqual(
            WardenAI.State.Chase, warden.CurrentState,
            "The Warden should be chasing at this point.");

        // Stop steering it and let the player stand still in the open. The
        // chase must end on its own within the patience window.
        float giveUp = Time.unscaledTime + 25f;
        float closest = float.MaxValue;

        while (Time.unscaledTime < giveUp &&
               warden.CurrentState == WardenAI.State.Chase)
        {
            Vector3 gap = warden.transform.position - player.transform.position;
            gap.y = 0f;
            closest = Mathf.Min(closest, gap.magnitude);

            yield return null;
        }

        Assert.AreNotEqual(
            WardenAI.State.Chase, warden.CurrentState,
            "The Warden chased for over twenty seconds without ever giving " +
            "up. With no limit on a pursuit it follows the player forever, " +
            "and it is faster than walking, so there is no way to escape it.");

        Assert.Greater(
            closest, 0.6f,
            "The Warden closed to " + closest.ToString("F2") + " m - it is " +
            "standing inside the player. stoppingDistance must keep it out.");
    }
}
