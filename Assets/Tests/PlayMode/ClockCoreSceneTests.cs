using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

/// <summary>
/// Play-mode checks for Step 6.3: the infrastructure ClockCore now has - a
/// walkable floor, a controllable Player, era travel unlocked, two more Time
/// Anchors, both AI agent types present, and the three-phase Collector boss
/// fight (Assets/Scripts/AI/Collector.cs).
///
/// Collector.OnCollisionEnter is exercised through the internal
/// RegisterOrbHit it delegates to, not through a real physics collision -
/// UnityEngine.Collision has no public constructor, so a test cannot
/// fabricate one. RegisterOrbHit contains the entire phase-transition
/// decision; OnCollisionEnter itself only checks for a ChronoOrb.
/// </summary>
public sealed class ClockCoreSceneTests
{
    private GameObject player;
    private Collector collector;
    private ChronoHourglass hourglass;

    [UnitySetUp]
    public IEnumerator LoadScene()
    {
        SceneManager.LoadScene("ClockCore", LoadSceneMode.Single);
        yield return null;
        yield return null;

        player = GameObject.Find("Player");
        collector = Object.FindFirstObjectByType<Collector>();
        if (player != null)
        {
            hourglass = player.GetComponent<ChronoHourglass>();
        }
    }

    [Test]
    public void Player_IsPresentAndControllable()
    {
        Assert.IsNotNull(player, "No 'Player' object in ClockCore.");
        Assert.IsNotNull(player.GetComponent<CharacterController>(), "Player has no CharacterController.");
        Assert.IsNotNull(player.GetComponent<PlayerController>(), "Player has no PlayerController.");
        Assert.IsTrue(player.CompareTag("Player"), "Player is not tagged 'Player'.");
    }

    [Test]
    public void EraTravel_StartsUnlocked()
    {
        var era = Object.FindFirstObjectByType<EraManager>();
        Assert.IsNotNull(era, "No EraManager in ClockCore.");
        Assert.IsTrue(era.IsUnlocked, "Era travel should be unlocked - both items are held by this scene.");
    }

    [Test]
    public void AtLeastTwoMoreTimeAnchors_Exist()
    {
        TimeAnchor[] anchors = Object.FindObjectsByType<TimeAnchor>(FindObjectsSortMode.None);
        Assert.GreaterOrEqual(anchors.Length, 2, "T21 asks for at least two more hidden Time Anchors here.");
    }

    [Test]
    public void BothAgentTypes_ArePresent()
    {
        // FindObjectsInactive.Include: the Warden is the Collector's "summoned"
        // one and starts inactive until Phase 1 is cleared, by design.
        var warden = Object.FindFirstObjectByType<WardenAI>(FindObjectsInactive.Include);
        var shadow = Object.FindFirstObjectByType<ShadowAI>(FindObjectsInactive.Include);

        Assert.IsNotNull(warden, "No WardenAI in ClockCore.");
        Assert.IsNotNull(shadow, "No ShadowAI in ClockCore.");

        NavMeshAgent wardenAgent = warden.GetComponent<NavMeshAgent>();
        NavMeshAgent shadowAgent = shadow.GetComponent<NavMeshAgent>();

        Assert.AreNotEqual(wardenAgent.agentTypeID, shadowAgent.agentTypeID,
            "Warden and Shadow should be two different agent types.");
        Assert.IsTrue(shadowAgent.isOnNavMesh, "The Shadow is not on its baked navmesh.");
    }

    [Test]
    public void Collector_ExistsAndStartsShieldedWithTheWardenHidden()
    {
        Assert.IsNotNull(collector, "No Collector in ClockCore.");
        Assert.IsFalse(collector.IsDefeated, "The Collector should not start defeated.");

        var warden = Object.FindFirstObjectByType<WardenAI>(FindObjectsInactive.Include);
        Assert.IsNotNull(warden, "No WardenAI to summon.");
        Assert.IsFalse(warden.gameObject.activeSelf, "The Warden should stay hidden until Phase 2 summons it.");
    }

    [Test]
    public void Phase1_OnlyBreaksTheShieldWhileInThePast()
    {
        var era = Object.FindFirstObjectByType<EraManager>();
        era.SetEra(TimeEra.Present);

        RegisterOrbHit(collector);
        RegisterOrbHit(collector);

        Assert.IsFalse(GetShieldBroken(), "A hit landed outside the Past should not have broken the shield.");

        era.SetEra(TimeEra.Past);
        RegisterOrbHit(collector);
        RegisterOrbHit(collector);

        Assert.IsTrue(GetShieldBroken(), "Two orb hits in the Past should break the shield (hitsToBreakShield = 2).");

        var warden = Object.FindFirstObjectByType<WardenAI>(FindObjectsInactive.Include);
        Assert.IsTrue(warden.gameObject.activeSelf, "Breaking the shield should summon the Warden (Phase 2).");
    }

    [UnityTest]
    public IEnumerator Phase3_CannotBeWonWithoutTheHourglass()
    {
        var era = Object.FindFirstObjectByType<EraManager>();

        // Clear phases 1 and 2 first.
        era.SetEra(TimeEra.Past);
        RegisterOrbHit(collector);
        RegisterOrbHit(collector);
        era.SetEra(TimeEra.Present);
        RegisterOrbHit(collector);

        era.SetEra(TimeEra.Future);

        // A hit while NOT slowing time must not win the fight - this is the
        // documented verification bullet, made literal: "the fight cannot be
        // won without the Hourglass."
        RegisterOrbHit(collector);
        yield return null;

        Assert.IsFalse(collector.IsDefeated, "Hitting the Collector in the Future without the Hourglass should not defeat it.");

        GameManager.Instance.RestoreFullHealth();
        GameManager.Instance.RestoreFullEnergy();
        // ChronoHourglass.IsAvailable also requires the item flag - ClockCore
        // assumes both items are already held, but a test must not rely on
        // whichever earlier test happened to set this on the shared,
        // DontDestroyOnLoad GameManager.
        GameManager.Instance.State.hasChronoHourglass = true;

        // Hold Ctrl via the same input reader path ChronoHourglass reads.
        var reader = player.GetComponent<PlayerInputReader>();
        SetPrivate(reader, "isSlowTimeHeld", true);
        yield return null;

        RegisterOrbHit(collector);
        yield return null;
        yield return null;

        Assert.IsTrue(collector.IsDefeated, "Hitting the Collector in the Future while the Hourglass is active should defeat it.");
        Assert.AreEqual("Victory", SceneManager.GetActiveScene().name, "Defeating the Collector should load Victory.");
    }

    [UnityTest]
    public IEnumerator Phase3_ErodesHealthWithoutTheHourglassActive()
    {
        var era = Object.FindFirstObjectByType<EraManager>();
        era.SetEra(TimeEra.Past);
        RegisterOrbHit(collector);
        RegisterOrbHit(collector);
        era.SetEra(TimeEra.Present);
        RegisterOrbHit(collector);
        era.SetEra(TimeEra.Future);

        GameManager.Instance.RestoreFullHealth();
        int before = GameManager.Instance.State.currentHealth;

        // Phase 3 opens with a few seconds of calm before the erosion starts.
        //
        // That grace is deliberate. Without it the phase began at 12 health a
        // second with no warning, which gives a player about eight seconds to
        // read a brand new objective, switch era, hold a key they may never
        // have pressed, and land a physics throw - and there is nowhere to run
        // to, because the erosion is the phase rather than a place. Played
        // straight it simply killed me before I had finished reading it.
        //
        // So wait past the grace, then assert the erosion really does bite.
        float deadline = Time.time + 12f;

        while (GameManager.Instance.State.currentHealth >= before &&
               Time.time < deadline)
        {
            yield return null;
        }

        Assert.Less(
            GameManager.Instance.State.currentHealth,
            before,
            "Time should be eroding Noa's health in the Future phase while " +
            "the Hourglass is inactive, once the opening grace has passed.");
    }

    private static void RegisterOrbHit(Collector target)
    {
        MethodInfo method = typeof(Collector).GetMethod(
            "RegisterOrbHit", BindingFlags.Instance | BindingFlags.NonPublic);

        // One explicit null argument: RegisterOrbHit now takes the orb that
        // struck (so the last phase can accept a throw MADE while time was
        // slowed, not only one that lands while it still is). Optional
        // parameters are a compiler convenience - reflection still requires
        // the argument to be supplied.
        method.Invoke(target, new object[] { null });
    }

    private bool GetShieldBroken()
    {
        var stage = typeof(Collector)
            .GetField("stage", BindingFlags.Instance | BindingFlags.NonPublic)
            .GetValue(collector);

        // Stage.Shielded is 0 - broken means we have moved past it.
        return (int)stage != 0;
    }

    private static void SetPrivate(object target, string field, object value)
    {
        FieldInfo info = target.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic);
        info.SetValue(target, value);
    }

    /// <summary>
    /// The grace at the start of phase 3 - the difference between a climax
    /// and a coin flip.
    /// </summary>
    [UnityTest]
    public IEnumerator Phase3_GivesAMomentBeforeTheErosionStarts()
    {
        var era = Object.FindFirstObjectByType<EraManager>();
        era.SetEra(TimeEra.Past);
        RegisterOrbHit(collector);
        RegisterOrbHit(collector);
        era.SetEra(TimeEra.Present);
        RegisterOrbHit(collector);
        era.SetEra(TimeEra.Future);

        GameManager.Instance.RestoreFullHealth();
        int before = GameManager.Instance.State.currentHealth;

        // Well inside the grace window.
        float until = Time.time + 1.5f;

        while (Time.time < until)
        {
            yield return null;
        }

        Assert.AreEqual(
            before, GameManager.Instance.State.currentHealth,
            "The erosion started immediately. The player needs a moment to " +
            "read what the phase is asking before it starts killing them.");
    }
}
