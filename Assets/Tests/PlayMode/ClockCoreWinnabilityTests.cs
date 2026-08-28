using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

/// <summary>
/// "Can the fight actually be won by playing it?"
///
/// ClockCoreSceneTests already covers the Collector's phase LOGIC by calling
/// its transitions directly, and it passed throughout - but logic passing is
/// not the same claim as the fight being winnable. Two things sat between the
/// two, and neither was visible to a logic test:
///
///   1. Whether a real Chrono Orb, thrown by the real launcher along the real
///      camera forward, with real gravity, ever physically reaches the
///      Collector and delivers an OnCollisionEnter. (The gear socket in
///      FrozenCity was buried inside the clock tower for exactly this reason:
///      its state machine was fine and the puzzle was still unplayable.)
///
///   2. Whether the player can still AFFORD to fight. Chrono Energy had no
///      regeneration anywhere in the project - the only thing that ever put
///      any back was dying - while era switching costs 8, an orb costs 5 and
///      the Hourglass drains 18/second. The fight needs all three, so a
///      player who reached the boss having spent their bar was left with a
///      boss they could not touch and no way to recover.
///
/// These drive the real components rather than the state machine.
/// </summary>
public sealed class ClockCoreWinnabilityTests
{
    private GameObject player;
    private Collector collector;
    private ChronoOrbLauncher launcher;

    [UnitySetUp]
    public IEnumerator LoadScene()
    {
        SceneManager.LoadScene("ClockCore", LoadSceneMode.Single);
        yield return null;
        yield return null;

        GameManager.Instance.ResetGame();

        player = GameObject.FindWithTag("Player");
        Assert.IsNotNull(player, "No Player in ClockCore.");

        collector = Object.FindFirstObjectByType<Collector>();
        Assert.IsNotNull(collector, "No Collector in ClockCore.");

        launcher = player.GetComponent<ChronoOrbLauncher>();
        Assert.IsNotNull(launcher, "The player cannot throw Chrono Orbs.");

        yield return null;
    }

    /// <summary>
    /// Stands the player a short throw from the Collector and faces them at
    /// it, the way a player who had walked up to the boss would be.
    /// </summary>
    private IEnumerator TakeAimAt(Component target)
    {
        Collider body = target.GetComponentInChildren<Collider>();
        Vector3 centre = body.bounds.center;

        var controller = player.GetComponent<CharacterController>();
        if (controller != null) { controller.enabled = false; }

        // Six metres out - well inside the flat-throw range of a 28 m/s orb.
        Vector3 stand = centre + ((player.transform.position - centre).normalized * 6f);
        stand.y = player.transform.position.y;

        player.transform.position = stand;
        player.transform.rotation = Quaternion.LookRotation(
            new Vector3(centre.x - stand.x, 0f, centre.z - stand.z).normalized);

        if (controller != null) { controller.enabled = true; }

        // The orb spawns along Camera.main.forward, and the Brain only moves
        // the camera in LateUpdate, so the aim is not valid until it settles.
        for (int i = 0; i < 12; i++) { yield return null; }

        Camera cam = Camera.main;
        Assert.IsNotNull(cam, "No active camera to aim along.");

        cam.transform.rotation = Quaternion.LookRotation(
            (centre - cam.transform.position).normalized);

        yield return null;
    }

    /// <summary>
    /// Throws one orb and waits for it to land on something. Reports whether
    /// the orb registered a collision before it despawned.
    /// </summary>
    private IEnumerator ThrowAndWait(System.Action<bool> report)
    {
        GameManager.Instance.RestoreFullEnergy();

        // The launcher enforces a 0.4 s unscaled cooldown, so back-to-back
        // calls in a loop are refused for a reason that has nothing to do
        // with what this test is asking. Wait it out rather than reaching
        // past it, so the throw under test is one a player could make.
        float waitUntil = Time.unscaledTime + 0.5f;
        while (Time.unscaledTime < waitUntil) { yield return null; }

        Assert.IsNotNull(Camera.main, "No camera tagged MainCamera to throw along.");

        Assert.IsTrue(
            launcher.Throw(),
            "The launcher refused to throw an orb even at full energy and off " +
            "cooldown - the orb prefab reference is probably missing.");

        ChronoOrb orb = launcher.LastOrb;
        Assert.IsNotNull(orb, "No orb was spawned.");

        float deadline = Time.unscaledTime + 4f;

        while (orb != null && orb.Bounces == 0 && Time.unscaledTime < deadline)
        {
            yield return null;
        }

        report(orb != null && orb.Bounces > 0);
    }

    /// <summary>
    /// The physical question: does a thrown orb actually arrive?
    /// </summary>
    [UnityTest]
    public IEnumerator AThrownOrbPhysicallyReachesTheCollector()
    {
        EraManager.Instance.SetEra(TimeEra.Past);

        yield return TakeAimAt(collector);

        Assert.AreEqual(
            Collector.Stage.Shielded, collector.CurrentStage,
            "The fight should start in its first phase.");

        // Two hits break the shield. Throw generously so one bad bounce does
        // not fail the test for the wrong reason.
        for (int i = 0; i < 6 && collector.CurrentStage == Collector.Stage.Shielded; i++)
        {
            bool landed = false;
            yield return ThrowAndWait(hit => landed = hit);

            Assert.IsTrue(
                landed,
                "Orb " + (i + 1) + " never hit anything before it despawned - " +
                "it is not reaching the boss at all.");

            yield return null;
        }

        Assert.AreNotEqual(
            Collector.Stage.Shielded, collector.CurrentStage,
            "Six Chrono Orbs thrown point-blank at the Collector in the Past " +
            "did not break its shield. The phase logic is fine, so the orb is " +
            "not delivering a collision to the Collector, and the fight cannot " +
            "be won by playing it.");
    }

    /// <summary>
    /// The economy question. Nothing in the project regenerated energy, so
    /// this asserts the recovery exists at all.
    /// </summary>
    [UnityTest]
    public IEnumerator EnergyRecoversSoTheRunCanNeverDeadEnd()
    {
        EraManager.Instance.Unlock();
        EraManager.Instance.SetEra(TimeEra.Present);
        GameManager.Instance.State.currentEnergy = 0f;
        yield return null;

        // TryStep is the path the Q/R keys actually take, cost included.
        Assert.IsFalse(
            EraManager.Instance.TryStep(1),
            "Baseline: an era switch should be refused at zero energy.");

        float deadline = Time.unscaledTime + 8f;

        while (GameManager.Instance.State.currentEnergy < 20f &&
               Time.unscaledTime < deadline)
        {
            yield return null;
        }

        Assert.Greater(
            GameManager.Instance.State.currentEnergy, 20f,
            "Energy did not recover. With no regeneration anywhere, a player " +
            "who spends their bar before reaching the Collector can never " +
            "switch era or throw an orb again, and the run is unwinnable with " +
            "nothing on screen explaining why.");

        Assert.IsTrue(
            EraManager.Instance.TryStep(1),
            "After recovering, an era switch should be affordable again.");

        Assert.AreEqual(TimeEra.Future, EraManager.Instance.CurrentEra);
    }

    /// <summary>
    /// Recovery must not outpace the Hourglass, or slowing time becomes free
    /// and phase 3 stops carrying any cost at all.
    /// </summary>
    [UnityTest]
    public IEnumerator TheHourglassStillCostsMoreThanItRecovers()
    {
        var hourglass = player.GetComponent<ChronoHourglass>();
        Assert.IsNotNull(hourglass, "The player has no Chrono Hourglass component.");

        GameManager.Instance.State.hasChronoHourglass = true;
        GameManager.Instance.RestoreFullEnergy();

        float start = GameManager.Instance.State.currentEnergy;

        // Drive the drain directly: input cannot be synthesised here, and the
        // question is the arithmetic, not the binding.
        float drained = 0f;
        float until = Time.unscaledTime + 1f;

        while (Time.unscaledTime < until)
        {
            float cost = 18f * Time.unscaledDeltaTime;
            GameManager.Instance.SpendEnergy(cost);
            drained += cost;
            yield return null;
        }

        float spentNet = start - GameManager.Instance.State.currentEnergy;

        Assert.Greater(
            spentNet, drained * 0.9f,
            "Regeneration is cancelling out the Hourglass drain, so slowing " +
            "time would be free and phase 3 would carry no cost.");

        Assert.IsTrue(
            Mathf.Approximately(Time.timeScale, 1f),
            "Time scale was left altered.");
    }
}
