using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

/// <summary>
/// One test that plays the whole game, start to finish, through the same
/// objects a player would touch.
///
/// S9 asks for "a coherent logical connection between all the scenes", and the
/// existing suite verified each hop separately - MuseumNight's exit here,
/// FrozenCity's exit there, the Collector somewhere else. Nothing checked that
/// the chain actually holds together in one run, which is precisely where
/// cross-scene state bugs live: the missing GameManager, for instance, made
/// every hop pass in isolation while the HUD silently died at the first scene
/// load.
///
/// Deliberately drives real components (SceneExitTrigger, Collector) rather
/// than setting GameState flags and asserting they are set, which would prove
/// nothing about the game.
/// </summary>
public sealed class FullPlaythroughTests
{
    private const int MaxFramesPerLoad = 240;

    [UnityTearDown]
    public IEnumerator Cleanup()
    {
        Time.timeScale = 1f;
        yield return null;
    }

    [UnityTest]
    public IEnumerator Playthrough_MainMenuToVictoryThroughEveryScene()
    {
        // ---- Main menu -------------------------------------------------
        yield return LoadAndSettle("MainMenu");

        Assert.IsNotNull(
            Object.FindFirstObjectByType<MainMenuController>(),
            "The game does not start at a usable main menu.");

        Assert.IsNotNull(GameManager.Instance,
            "No GameManager exists at the main menu, so nothing can persist.");

        GameManager.Instance.ResetGame();

        // ---- Scene 1: MuseumNight --------------------------------------
        yield return LoadAndSettle("MuseumNight");

        Assert.IsFalse(GameManager.Instance.State.hasTimeLens,
            "A fresh run should not already hold the Time Lens.");

        // The exit must REFUSE to open before the Lens is found. This is the
        // gate that makes the item mean something.
        SceneExitTrigger museumExit = FindExit();
        EnterExit(museumExit);
        yield return null;

        Assert.AreEqual("MuseumNight", SceneManager.GetActiveScene().name,
            "MuseumNight let the player leave without the Time Lens - the " +
            "acquisition chain (T9/S9) is not actually gating anything.");

        // Acquire the Lens the way the scene grants it.
        GameManager.Instance.AcquireTimeLens();
        Assert.IsTrue(GameManager.Instance.State.hasTimeLens);

        EnterExit(museumExit);
        yield return WaitForScene("FrozenCity");

        Assert.AreEqual("FrozenCity", SceneManager.GetActiveScene().name,
            "Holding the Time Lens did not open the way to FrozenCity.");

        // ---- Scene 2: FrozenCity ---------------------------------------
        Assert.IsTrue(GameManager.Instance.State.hasTimeLens,
            "The Time Lens did not survive the scene change (T9).");

        Assert.GreaterOrEqual(
            Object.FindObjectsByType<TimeAnchor>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length,
            2, "FrozenCity must carry at least two hidden teleports (T21).");

        SceneExitTrigger frozenExit = FindExit();
        EnterExit(frozenExit);
        yield return null;

        Assert.AreEqual("FrozenCity", SceneManager.GetActiveScene().name,
            "FrozenCity let the player leave without the Chrono Hourglass.");

        GameManager.Instance.AcquireChronoHourglass();

        EnterExit(frozenExit);
        yield return WaitForScene("ClockCore");

        Assert.AreEqual("ClockCore", SceneManager.GetActiveScene().name,
            "Holding the Chrono Hourglass did not open the way to ClockCore.");

        // ---- Scene 3: ClockCore ----------------------------------------
        Assert.IsTrue(GameManager.Instance.State.hasChronoHourglass,
            "The Hourglass did not survive the scene change (T9).");

        var collector = Object.FindFirstObjectByType<Collector>();
        Assert.IsNotNull(collector, "ClockCore has no Collector to fight.");
        Assert.IsFalse(collector.IsDefeated, "The Collector starts already defeated.");

        // Phase 1 (Past): break the shield with the orb. The shield takes
        // more than one hit, so the count is read from the Collector rather
        // than assumed - a hard-coded 1 here silently left the boss shielded
        // and the run never reached Victory.
        SetEra(TimeEra.Past);
        yield return null;

        int hitsNeeded = GetPrivateInt(collector, "hitsToBreakShield", 2);

        for (int i = 0; i < hitsNeeded; i++)
        {
            RegisterOrbHit(collector);
            yield return null;
        }

        Assert.IsTrue(GetStage(collector) > 0,
            "The Collector's shield did not break after " + hitsNeeded +
            " orb hits in the Past.");

        // Phase 2 (Present): the summoned Warden appears.
        SetEra(TimeEra.Present);
        yield return null;
        RegisterOrbHit(collector);
        yield return null;

        Assert.AreEqual(2, GetStage(collector),
            "The Collector did not advance to its Future phase.");

        // Phase 3 (Future): only a hit while the Hourglass is ACTIVE wins.
        SetEra(TimeEra.Future);
        yield return null;

        // The Hourglass has to be genuinely running, not just carried.
        GameManager.Instance.RestoreFullEnergy();
        SetSlowTimeHeld(true);
        yield return null;

        var hourglass = Object.FindFirstObjectByType<ChronoHourglass>();
        Assert.IsNotNull(hourglass, "The player has no ChronoHourglass in ClockCore.");
        Assert.IsTrue(hourglass.IsSlowing,
            "Holding Ctrl with the Hourglass did not slow time, so the fight " +
            "cannot be won (T9: the item is required in scene 3).");

        RegisterOrbHit(collector);
        yield return WaitForScene("Victory");

        // ---- Victory ----------------------------------------------------
        Assert.AreEqual("Victory", SceneManager.GetActiveScene().name,
            "Defeating the Collector did not reach the Victory screen.");

        Assert.IsNotNull(
            Object.FindFirstObjectByType<VictoryScreenController>(),
            "The Victory scene has no VictoryScreenController.");

        // The run's own numbers must have survived all four scene loads.
        Assert.IsTrue(GameManager.Instance.State.hasTimeLens,
            "The Time Lens was lost somewhere along the run.");
        Assert.IsTrue(GameManager.Instance.State.hasChronoHourglass,
            "The Chrono Hourglass was lost somewhere along the run.");
        Assert.Greater(GameManager.Instance.State.score, 0,
            "The run finished with no score at all.");
    }

    // ------------------------------------------------------------------

    private static IEnumerator LoadAndSettle(string sceneName)
    {
        SceneManager.LoadScene(sceneName, LoadSceneMode.Single);
        yield return null;
        yield return null;
    }

    private static IEnumerator WaitForScene(string sceneName)
    {
        for (int i = 0; i < MaxFramesPerLoad; i++)
        {
            if (SceneManager.GetActiveScene().name == sceneName)
            {
                // A couple more frames so Awake/Start of the new scene run.
                yield return null;
                yield return null;
                yield break;
            }

            yield return null;
        }
    }

    private static int GetPrivateInt(object target, string field, int fallback)
    {
        FieldInfo info = target.GetType().GetField(
            field, BindingFlags.Instance | BindingFlags.NonPublic);

        return info != null ? (int)info.GetValue(target) : fallback;
    }

    /// <summary>Stage as an int: 0 Shielded, 1 Present, 2 Future, 3 Defeated.</summary>
    private static int GetStage(Collector collector)
    {
        object stage = typeof(Collector)
            .GetField("stage", BindingFlags.Instance | BindingFlags.NonPublic)
            .GetValue(collector);

        return (int)stage;
    }

    private static SceneExitTrigger FindExit()
    {
        var exit = Object.FindFirstObjectByType<SceneExitTrigger>();
        Assert.IsNotNull(exit,
            SceneManager.GetActiveScene().name + " has no SceneExitTrigger, so " +
            "there is no way out of it (S9).");
        return exit;
    }

    private static void EnterExit(SceneExitTrigger exit)
    {
        MethodInfo method = typeof(SceneExitTrigger).GetMethod(
            "OnPlayerEntered", BindingFlags.Instance | BindingFlags.NonPublic);

        method.Invoke(exit, new object[] { null });
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

    private static void SetEra(TimeEra era)
    {
        Assert.IsNotNull(EraManager.Instance, "No EraManager in ClockCore.");

        FieldInfo unlocked = typeof(EraManager).GetField(
            "eraTravelUnlocked", BindingFlags.Instance | BindingFlags.NonPublic);

        if (unlocked != null) { unlocked.SetValue(EraManager.Instance, true); }

        EraManager.Instance.SetEra(era);
    }

    /// <summary>
    /// Holds Ctrl through the real input path rather than writing
    /// ChronoHourglass.active directly.
    ///
    /// Setting the field straight does not survive: ChronoHourglass.Update
    /// recomputes "wanted" from PlayerInputReader every frame and calls
    /// Restore() the moment it finds the flag set without the input held, so
    /// the shortcut was reverted before the orb hit was ever processed.
    /// </summary>
    private static void SetSlowTimeHeld(bool held)
    {
        var player = GameObject.FindWithTag("Player");
        Assert.IsNotNull(player, "No player in ClockCore.");

        var reader = player.GetComponent<PlayerInputReader>();
        Assert.IsNotNull(reader, "The player has no PlayerInputReader.");

        FieldInfo field = typeof(PlayerInputReader).GetField(
            "isSlowTimeHeld", BindingFlags.Instance | BindingFlags.NonPublic);

        Assert.IsNotNull(field, "PlayerInputReader has no isSlowTimeHeld field.");
        field.SetValue(reader, held);
    }
}
