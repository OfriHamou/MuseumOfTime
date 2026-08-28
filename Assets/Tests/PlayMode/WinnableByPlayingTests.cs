using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

/// <summary>
/// Wins the game using only the things a player touches.
///
/// FullPlaythroughTests already walks the scene chain, but it takes two
/// shortcuts that hollow out the claim: it calls AcquireTimeLens() and
/// AcquireChronoHourglass() directly instead of picking the items up, and it
/// invokes Collector.RegisterOrbHit by reflection instead of throwing anything.
/// So it proves the scene chain and the boss state machine - not that the items
/// can be obtained or that the boss can be hit.
///
/// Every one of those shortcuts has hidden a real defect at some point:
///
///   - The Time Lens could not be picked up at all, because trigger volumes
///     swallowed the interaction cast and the ray was a zero-width line from a
///     shoulder-offset camera.
///   - The FrozenCity gear socket was buried half a metre inside the clock
///     tower, so the puzzle that grants the Hourglass was unplayable while its
///     own logic tests passed.
///   - The Collector could be reached in principle, but nothing had confirmed
///     a thrown orb physically arrives.
///
/// This test takes none of those shortcuts. It acquires both items through
/// PlayerInteractor - the same look-cast a player aims - solves the three-era
/// puzzle through its real interactables, and breaks every boss phase with
/// real Chrono Orbs thrown by the real launcher under real physics.
///
/// If this passes, the game is winnable. If it fails, it is not.
/// </summary>
public sealed class WinnableByPlayingTests
{
    private const int LoadTimeoutFrames = 300;

    [UnityTearDown]
    public IEnumerator Cleanup()
    {
        Time.timeScale = 1f;
        Time.fixedDeltaTime = 0.02f;
        yield return null;
    }

    [UnityTest]
    public IEnumerator TheGameCanBeWonUsingOnlyThingsAPlayerCanDo()
    {
        // ================= MuseumNight: take the Time Lens ==============
        yield return Load("MuseumNight");

        GameManager.Instance.ResetGame();
        yield return null;

        Assert.IsFalse(GameManager.Instance.State.hasTimeLens,
            "A fresh run should not already hold the Time Lens.");

        var lens = Object.FindFirstObjectByType<ItemPickup>();
        Assert.IsNotNull(lens, "MuseumNight has no Time Lens to find.");

        yield return TakeByLookingAtIt(lens.transform, "the Time Lens");

        Assert.IsTrue(GameManager.Instance.State.hasTimeLens,
            "The Time Lens could not be picked up by aiming at it and " +
            "interacting - which is the only way a player can get it.");

        // The exit must open now, and only now.
        SceneExitTrigger museumExit = FindExit();
        EnterExit(museumExit);
        yield return WaitForScene("FrozenCity");

        Assert.AreEqual("FrozenCity", SceneManager.GetActiveScene().name,
            "Carrying the Time Lens did not open the way out of the museum.");

        // ============ FrozenCity: solve the three-era puzzle ============
        Assert.IsTrue(GameManager.Instance.State.hasTimeLens,
            "The Time Lens did not survive the scene change.");

        UnlockEras();

        // The gear exists in the PAST, on its bench.
        SetEra(TimeEra.Past);
        yield return null;

        Component gear = FindByTypeName("GearPickup");
        yield return TakeByLookingAtIt(gear.transform, "the gear");

        GearPuzzle puzzle = GearPuzzle.Instance;
        Assert.IsNotNull(puzzle, "FrozenCity has no GearPuzzle.");
        Assert.IsTrue(puzzle.HasGear,
            "Aiming at the gear in the Past and interacting did not pick it up.");

        // It fits the tower in the PRESENT.
        SetEra(TimeEra.Present);
        yield return null;

        Component socket = FindByTypeName("GearSocket");
        yield return TakeByLookingAtIt(socket.transform, "the gear socket");

        Assert.IsTrue(puzzle.Installed,
            "Aiming at the socket in the Present and interacting did not " +
            "install the gear. The socket was once buried inside the tower, " +
            "which is exactly this failure.");

        // And it must still be turning in the FUTURE.
        SetEra(TimeEra.Future);
        yield return null;

        yield return TakeByLookingAtIt(socket.transform, "the mechanism");

        Assert.IsTrue(puzzle.Verified,
            "Checking the mechanism in the Future did not complete the puzzle.");

        // The reward the puzzle gates.
        var reward = Object.FindFirstObjectByType<ItemPickup>();
        Assert.IsNotNull(reward, "Solving the puzzle revealed no reward.");
        Assert.IsTrue(reward.gameObject.activeInHierarchy,
            "The Chrono Hourglass did not appear after the puzzle was solved.");

        yield return TakeByLookingAtIt(reward.transform, "the Chrono Hourglass");

        Assert.IsTrue(GameManager.Instance.State.hasChronoHourglass,
            "The Chrono Hourglass could not be picked up.");

        SceneExitTrigger cityExit = FindExit();
        EnterExit(cityExit);
        yield return WaitForScene("ClockCore");

        Assert.AreEqual("ClockCore", SceneManager.GetActiveScene().name,
            "Carrying the Chrono Hourglass did not open the way to the Clock Core.");

        // ============== ClockCore: beat the Collector ==================
        var collector = Object.FindFirstObjectByType<Collector>();
        Assert.IsNotNull(collector, "ClockCore has no Collector to fight.");

        UnlockEras();

        // Phase 1 - the Past. Real orbs, real physics.
        SetEra(TimeEra.Past);
        yield return StandInFrontOf(collector.transform, 6f);

        yield return ThrowUntilStageChanges(collector, Collector.Stage.Shielded,
            "No number of Chrono Orbs thrown point-blank at the Collector in " +
            "the Past broke its shield, so phase 1 cannot be cleared.");

        // Phase 2 - the Present.
        SetEra(TimeEra.Present);
        yield return null;

        yield return ThrowUntilStageChanges(collector, Collector.Stage.Present,
            "Orbs thrown in the Present did not advance the fight past " +
            "phase 2.");

        // Phase 3 - the Future, and only while time is slowed.
        SetEra(TimeEra.Future);
        GameManager.Instance.RestoreFullEnergy();
        yield return null;

        HoldSlowTime(true);
        yield return null;

        var hourglass = Object.FindFirstObjectByType<ChronoHourglass>();
        Assert.IsNotNull(hourglass, "The player has no ChronoHourglass component.");
        Assert.IsTrue(hourglass.IsSlowing,
            "Holding Ctrl with the Hourglass did not slow time, so the last " +
            "phase is impossible.");

        yield return ThrowUntilStageChanges(collector, Collector.Stage.Future,
            "Orbs thrown in the Future while time was slowed did not finish " +
            "the Collector.");

        HoldSlowTime(false);

        yield return WaitForScene("Victory");

        Assert.AreEqual("Victory", SceneManager.GetActiveScene().name,
            "Defeating the Collector did not reach the Victory screen, so the " +
            "game cannot actually be completed.");

        Assert.IsTrue(GameManager.Instance.State.hasTimeLens,
            "The Time Lens was lost during the run.");
        Assert.IsTrue(GameManager.Instance.State.hasChronoHourglass,
            "The Chrono Hourglass was lost during the run.");
    }

    // ------------------------------------------------------------------
    // Everything below drives the same components a player's input drives.
    // ------------------------------------------------------------------

    /// <summary>
    /// Stands in front of something, aims at it, and presses interact -
    /// through PlayerInteractor, so the look-cast has to genuinely find it.
    /// </summary>
    private static IEnumerator TakeByLookingAtIt(Transform target, string what)
    {
        GameObject player = GameObject.FindWithTag("Player");
        var interactor = player.GetComponent<PlayerInteractor>();
        Assert.IsNotNull(interactor, "The player has no PlayerInteractor.");

        // Interact in FIRST PERSON, which is what the C key gives a player.
        //
        // In third person the camera sits half a metre off Noa's shoulder and
        // 2.6 m behind her, so the look-cast starts somewhere she is not and
        // runs almost parallel to the line from her to the thing she is
        // standing in front of. First person puts the eye on the pivot, which
        // removes the offset entirely.
        var rig = Object.FindFirstObjectByType<PlayerCameraRig>();

        if (rig != null && !rig.IsFirstPerson)
        {
            rig.ToggleCamera();

            for (int i = 0; i < 20; i++) { yield return null; }
        }

        IInteractable found = null;

        // Walk in until the prompt appears, the way a player does.
        //
        // PlayerInteractor accepts a hit only within 3 m OF THE PLAYER, while
        // the cast starts at the third-person camera about 2.6 m further back.
        // Fixed standing distances kept landing outside that window - the
        // line of sight was clear and the hit was simply too far away to
        // count - so close the gap in steps instead of guessing at it.
        for (float distance = 3f; distance >= 1f && found == null; distance -= 0.4f)
        {
            yield return StandInFrontOf(target, distance);

            for (int i = 0; i < 10 && found == null; i++)
            {
                // Re-aim every frame.
                //
                // The third-person camera trails the pivot with damping, so a
                // single aim before the poll is stale by the time
                // PlayerInteractor casts through the camera - the diagnostic
                // showed the ray hitting the gear while the camera was still
                // 19 degrees off it.
                AimPivotAt(target);

                yield return null;
                found = interactor.Current;
            }
        }

        Assert.IsNotNull(found,
            "Standing in front of " + what + " and looking straight at it " +
            "from four different distances, the interaction cast never found " +
            "it - so no prompt appears and pressing E does nothing. " +
            Diagnose(target));

        found.Interact(player);
        yield return null;
    }

    /// <summary>
    /// Points the camera pivot at a target, measured from where the CAMERA
    /// actually is rather than from the pivot, since the cast starts there.
    /// </summary>
    private static void AimPivotAt(Transform target)
    {
        GameObject player = GameObject.FindWithTag("Player");
        if (player == null) { return; }

        Transform pivot = player.transform.Find("CameraPivot");
        if (pivot == null) { return; }

        Collider body = target.GetComponentInChildren<Collider>();
        Vector3 centre = body != null ? body.bounds.center : target.position;

        Vector3 flatToTarget = centre - player.transform.position;
        flatToTarget.y = 0f;

        if (flatToTarget.sqrMagnitude > 0.01f)
        {
            player.transform.rotation = Quaternion.LookRotation(flatToTarget.normalized);
        }

        // Measured from the PIVOT, never from the camera.
        //
        // Aiming from the camera feeds back on itself: the third-person camera
        // is placed behind the pivot along its rotation, so a steep pitch
        // swings the camera lower, which measures an even steeper pitch next
        // frame. It drove the camera to y = -8, underneath the building,
        // pointing up at a Time Lens fifteen metres away.
        Vector3 fromPivot = centre - pivot.position;
        float flat = new Vector2(fromPivot.x, fromPivot.z).magnitude;
        float pitch = -Mathf.Atan2(fromPivot.y, Mathf.Max(0.01f, flat)) * Mathf.Rad2Deg;

        pivot.localRotation = Quaternion.Euler(Mathf.Clamp(pitch, -70f, 80f), 0f, 0f);
    }

    /// <summary>
    /// Explains what the interaction cast is actually hitting, so a failure
    /// here names the obstruction instead of just saying "nothing found".
    /// </summary>
    private static string Diagnose(Transform target)
    {
        GameObject player = GameObject.FindWithTag("Player");
        Camera cam = Camera.main;

        var sb = new System.Text.StringBuilder();
        sb.AppendLine("  player at " + player.transform.position);
        sb.AppendLine("  target '" + target.name + "' at " + target.position);

        Collider col = target.GetComponentInChildren<Collider>();
        sb.AppendLine("  target collider: " +
            (col == null ? "NONE" : col.GetType().Name + " enabled=" + col.enabled +
             " trigger=" + col.isTrigger + " active=" + col.gameObject.activeInHierarchy +
             " bounds=" + col.bounds.size + " layer=" +
             LayerMask.LayerToName(col.gameObject.layer)));

        if (cam == null) { return sb.ToString(); }

        sb.AppendLine("  camera at " + cam.transform.position +
                      " forward " + cam.transform.forward);

        Vector3 centre = col != null ? col.bounds.center : target.position;
        Vector3 toTarget = centre - cam.transform.position;

        sb.AppendLine("  angle camera-forward to target = " +
            Vector3.Angle(cam.transform.forward, toTarget).ToString("F1") + " deg" +
            ", camera distance " + toTarget.magnitude.ToString("F2") + " m");

        sb.AppendLine("  PLAYER to target = " +
            Vector3.Distance(player.transform.position, centre).ToString("F2") +
            " m (PlayerInteractor only accepts hits within 3 m of the player)");

        var hits = Physics.RaycastAll(cam.transform.position, toTarget.normalized,
                                      toTarget.magnitude + 1f, ~0,
                                      QueryTriggerInteraction.Collide);

        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
        sb.AppendLine("  straight line from the camera to it passes:");

        foreach (RaycastHit hit in hits)
        {
            sb.AppendLine("     " + hit.distance.ToString("F2") + " m  '" +
                hit.collider.gameObject.name + "' trigger=" + hit.collider.isTrigger);
        }

        return sb.ToString();
    }

    /// <summary>
    /// Places the player a given distance from a target, facing it, with the
    /// camera aimed at it. Navigation itself is covered elsewhere; what
    /// matters here is that the thing can be aimed at and used.
    /// </summary>
    private static IEnumerator StandInFrontOf(Transform target, float distance)
    {
        GameObject player = GameObject.FindWithTag("Player");
        Assert.IsNotNull(player, "No player in " + SceneManager.GetActiveScene().name);

        Collider body = target.GetComponentInChildren<Collider>();
        Vector3 centre = body != null ? body.bounds.center : target.position;

        var controller = player.GetComponent<CharacterController>();
        if (controller != null) { controller.enabled = false; }

        Vector3 offset = player.transform.position - centre;
        offset.y = 0f;

        if (offset.sqrMagnitude < 0.01f) { offset = Vector3.back; }

        Vector3 stand = centre + (offset.normalized * distance);

        // Stand on the floor the TARGET is on, not the one the player
        // happens to be on.
        //
        // The Time Lens sits on the mezzanine at y=5.6 while the player spawns
        // on the ground floor at y=1. Keeping the player's own height put them
        // underneath it with the mezzanine slab in between, and the look-cast
        // dutifully hit the underside of the floor. That is a broken test, not
        // a broken game - but it fails in exactly the same way a real
        // unreachable item would, so it has to be got right or the whole
        // result is worthless.
        stand.y = FloorHeightUnder(stand, centre.y + 3f);

        player.transform.position = stand;
        player.transform.rotation = Quaternion.LookRotation(
            new Vector3(centre.x - stand.x, 0f, centre.z - stand.z).normalized);

        if (controller != null) { controller.enabled = true; }

        // Aim the RIG, not the camera.
        //
        // Setting Camera.main's rotation looks like it works and does nothing:
        // the Cinemachine Brain re-places the camera from its virtual camera
        // every LateUpdate, so the aim is gone before PlayerInteractor casts
        // through it on the next frame. That is why looking straight at the
        // gear found nothing.
        //
        // PlayerCameraRig only writes the pivot when there is look input, so
        // setting the pivot's pitch here sticks, and the camera follows it -
        // which is exactly what happens when a player moves the mouse.
        Transform pivot = player.transform.Find("CameraPivot");

        if (pivot != null)
        {
            for (int i = 0; i < 4; i++) { yield return null; }

            Vector3 eye = pivot.position;
            Vector3 toTarget = centre - eye;

            float flat = new Vector2(toTarget.x, toTarget.z).magnitude;
            float pitch = -Mathf.Atan2(toTarget.y, Mathf.Max(0.01f, flat)) * Mathf.Rad2Deg;

            pivot.localRotation = Quaternion.Euler(
                Mathf.Clamp(pitch, -70f, 80f), 0f, 0f);
        }

        for (int i = 0; i < 8; i++) { yield return null; }
    }

    /// <summary>
    /// Finds the walkable surface under a point, starting the search above the
    /// target so an upper floor is found rather than the ground beneath it.
    /// </summary>
    private static float FloorHeightUnder(Vector3 where, float startHeight)
    {
        var from = new Vector3(where.x, startHeight, where.z);

        RaycastHit[] hits = Physics.RaycastAll(
            from, Vector3.down, startHeight + 5f, ~0, QueryTriggerInteraction.Ignore);

        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

        foreach (RaycastHit hit in hits)
        {
            // Skip the player's own collider and anything paper-thin sideways.
            if (hit.collider.CompareTag("Player")) { continue; }

            return hit.point.y + 0.05f;
        }

        return where.y;
    }

    /// <summary>
    /// Throws real orbs until the Collector leaves the given stage.
    /// </summary>
    private static IEnumerator ThrowUntilStageChanges(
        Collector collector, Collector.Stage from, string failure)
    {
        GameObject player = GameObject.FindWithTag("Player");
        var launcher = player.GetComponent<ChronoOrbLauncher>();
        Assert.IsNotNull(launcher, "The player cannot throw Chrono Orbs.");

        for (int attempt = 0; attempt < 12; attempt++)
        {
            if (collector.CurrentStage != from)
            {
                yield break;
            }

            // Re-aim each time: the summoned Warden can shove the player.
            yield return StandInFrontOf(collector.transform, 6f);

            GameManager.Instance.RestoreFullEnergy();

            // The launcher enforces an unscaled cooldown.
            float ready = Time.unscaledTime + 0.5f;
            while (Time.unscaledTime < ready) { yield return null; }

            launcher.Throw();

            // Let the orb fly. Time may be slowed in phase 3, so allow for it.
            float until = Time.unscaledTime + 2.5f;
            while (Time.unscaledTime < until && collector.CurrentStage == from)
            {
                yield return null;
            }
        }

        Assert.AreNotEqual(from, collector.CurrentStage, failure);
    }

    private static IEnumerator Load(string sceneName)
    {
        SceneManager.LoadScene(sceneName, LoadSceneMode.Single);
        yield return null;
        yield return null;
    }

    private static IEnumerator WaitForScene(string sceneName)
    {
        for (int i = 0; i < LoadTimeoutFrames; i++)
        {
            if (SceneManager.GetActiveScene().name == sceneName)
            {
                yield return null;
                yield return null;
                yield break;
            }

            yield return null;
        }
    }

    private static Component FindByTypeName(string typeName)
    {
        System.Type type = typeof(GearPuzzle).Assembly.GetType(typeName);
        Assert.IsNotNull(type, "No type called " + typeName + ".");

        Object[] found = Object.FindObjectsByType(
            type, FindObjectsInactive.Include, FindObjectsSortMode.None);

        Assert.Greater(found.Length, 0,
            "No " + typeName + " in " + SceneManager.GetActiveScene().name + ".");

        return (Component)found[0];
    }

    private static SceneExitTrigger FindExit()
    {
        var exit = Object.FindFirstObjectByType<SceneExitTrigger>();

        Assert.IsNotNull(exit,
            SceneManager.GetActiveScene().name + " has no way out of it.");

        return exit;
    }

    private static void EnterExit(SceneExitTrigger exit)
    {
        typeof(SceneExitTrigger)
            .GetMethod("OnPlayerEntered", BindingFlags.Instance | BindingFlags.NonPublic)
            .Invoke(exit, new object[] { null });
    }

    private static void UnlockEras()
    {
        Assert.IsNotNull(EraManager.Instance, "No EraManager in this scene.");
        EraManager.Instance.Unlock();
    }

    private static void SetEra(TimeEra era)
    {
        EraManager.Instance.SetEra(era);
    }

    /// <summary>
    /// Holds Ctrl through the real input reader. Writing ChronoHourglass's
    /// own flag does not survive: its Update recomputes what the player is
    /// holding every frame and cancels the moment the input is not there.
    /// </summary>
    private static void HoldSlowTime(bool held)
    {
        GameObject player = GameObject.FindWithTag("Player");
        var reader = player.GetComponent<PlayerInputReader>();

        typeof(PlayerInputReader)
            .GetField("isSlowTimeHeld", BindingFlags.Instance | BindingFlags.NonPublic)
            .SetValue(reader, held);
    }
}
