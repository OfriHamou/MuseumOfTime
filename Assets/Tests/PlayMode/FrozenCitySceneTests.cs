using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

/// <summary>
/// Play-mode checks for Step 6.2: placing the Phase 3/4 systems that already
/// worked in MuseumNight into FrozenCity, the scene T21/T13/T16 actually
/// assign them to.
///
/// FrozenCity.unity now embeds baked NavMesh tile data (binary byte arrays),
/// which is why `file` reports it as "data" rather than text even under the
/// project's text-serialization setting - the same thing already happened to
/// MuseumNight when Phase 4 baked its navmesh. That makes grepping the raw
/// scene file for component names unreliable, which is the real reason these
/// checks are automated tests rather than a file inspection.
/// </summary>
public sealed class FrozenCitySceneTests
{
    private GameObject player;

    [UnitySetUp]
    public IEnumerator LoadScene()
    {
        SceneManager.LoadScene("FrozenCity", LoadSceneMode.Single);
        yield return null;
        yield return null;

        player = GameObject.Find("Player");
        Assert.IsNotNull(player, "No 'Player' object in FrozenCity - Step 6.2 should have placed one.");
    }

    [Test]
    public void Player_IsFullyControllable()
    {
        Assert.IsNotNull(player.GetComponent<CharacterController>(), "Player has no CharacterController.");
        Assert.IsNotNull(player.GetComponent<PlayerController>(), "Player has no PlayerController.");
        Assert.IsNotNull(player.GetComponent<PlayerCameraRig>(), "Player has no PlayerCameraRig.");
        Assert.IsNotNull(player.GetComponent<PlayerInteractor>(), "Player has no PlayerInteractor.");
        Assert.IsNotNull(player.GetComponent<ChronoHourglass>(), "Player has no ChronoHourglass.");
        Assert.IsNotNull(player.GetComponent<ChronoOrbLauncher>(), "Player has no ChronoOrbLauncher.");
        Assert.IsTrue(player.CompareTag("Player"), "Player is not tagged 'Player' - triggers would not find it.");
    }

    [Test]
    public void EraTravel_StartsUnlocked()
    {
        var era = Object.FindFirstObjectByType<EraManager>();
        Assert.IsNotNull(era, "No EraManager in FrozenCity.");

        Assert.IsTrue(
            era.IsUnlocked,
            "Era travel should already be unlocked in FrozenCity - the Time Lens " +
            "was found in MuseumNight, unlike MuseumNight itself where it starts locked.");
    }

    [Test]
    public void AtLeastTwoTimeAnchors_ExistAndAreArmable()
    {
        TimeAnchor[] anchors = Object.FindObjectsByType<TimeAnchor>(FindObjectsSortMode.None);

        Assert.GreaterOrEqual(
            anchors.Length,
            2,
            "T21 asks for at least two hidden Time Anchors from the second scene onward.");

        foreach (TimeAnchor anchor in anchors)
        {
            Assert.IsNotNull(
                anchor.GetComponent<TimeAnchorTrigger>(),
                anchor.name + " has a TimeAnchor but no TimeAnchorTrigger - it would not count " +
                "toward the trigger tally (T3) the way MuseumNight's own triggers do.");
        }
    }

    [UnityTest]
    public IEnumerator TimeAnchor_IsHiddenWithoutTheLensAndVisibleWithIt()
    {
        TimeAnchor anchor = Object.FindFirstObjectByType<TimeAnchor>();
        Assert.IsNotNull(anchor, "No TimeAnchor in FrozenCity.");

        var lensVisual = (GameObject)typeof(TimeAnchor)
            .GetField("lensVisual", BindingFlags.Instance | BindingFlags.NonPublic)
            .GetValue(anchor);

        Assert.IsNotNull(lensVisual, anchor.name + " has no lensVisual wired up.");

        GameManager.Instance.State.hasTimeLens = false;
        yield return null;
        Assert.IsFalse(lensVisual.activeSelf, "The anchor marker should be hidden without the Time Lens.");

        GameManager.Instance.State.hasTimeLens = true;
        yield return null;
        Assert.IsTrue(lensVisual.activeSelf, "The anchor marker should be visible with the Time Lens.");
    }

    [Test]
    public void BothAgentTypes_ArePresentWithDifferentDimensions()
    {
        var warden = Object.FindFirstObjectByType<WardenAI>();
        var shadow = Object.FindFirstObjectByType<ShadowAI>();

        Assert.IsNotNull(warden, "No WardenAI in FrozenCity.");
        Assert.IsNotNull(shadow, "No ShadowAI in FrozenCity.");

        NavMeshAgent wardenAgent = warden.GetComponent<NavMeshAgent>();
        NavMeshAgent shadowAgent = shadow.GetComponent<NavMeshAgent>();

        Assert.AreNotEqual(
            wardenAgent.agentTypeID,
            shadowAgent.agentTypeID,
            "Warden and Shadow should be on two different agent types, not one.");

        Assert.IsTrue(wardenAgent.isOnNavMesh, "The Warden is not on its baked navmesh.");
        Assert.IsTrue(shadowAgent.isOnNavMesh, "The Shadow is not on its baked navmesh.");

        Assert.IsNotNull(warden.GetComponent<PatrolRoute>(), "The Warden has no PatrolRoute (T7).");
    }

    [Test]
    public void TheTwoAgentTypes_TakeDifferentRoutesToTheSameDestination()
    {
        var warden = Object.FindFirstObjectByType<WardenAI>();
        var shadow = Object.FindFirstObjectByType<ShadowAI>();
        Assert.IsNotNull(warden, "No WardenAI in FrozenCity.");
        Assert.IsNotNull(shadow, "No ShadowAI in FrozenCity.");

        NavMeshAgent wardenAgent = warden.GetComponent<NavMeshAgent>();
        NavMeshAgent shadowAgent = shadow.GetComponent<NavMeshAgent>();

        // The shortcut FrozenCityContentBuilder built (a 0.8m slot, a 0.7m
        // ledge) sits east of both start points - a destination beyond it is
        // reachable directly by the Shadow and only by going around for the
        // Warden, so the two paths must disagree.
        Vector3 destination = new Vector3(13f, 0f, -18f);

        var wardenPath = new NavMeshPath();
        var shadowPath = new NavMeshPath();

        NavMesh.CalculatePath(wardenAgent.transform.position, destination,
            new NavMeshQueryFilter { agentTypeID = wardenAgent.agentTypeID, areaMask = NavMesh.AllAreas },
            wardenPath);

        NavMesh.CalculatePath(shadowAgent.transform.position, destination,
            new NavMeshQueryFilter { agentTypeID = shadowAgent.agentTypeID, areaMask = NavMesh.AllAreas },
            shadowPath);

        bool differs = wardenPath.status != shadowPath.status ||
                       wardenPath.corners.Length != shadowPath.corners.Length ||
                       PathLength(wardenPath) - PathLength(shadowPath) > 1f;

        Assert.IsTrue(
            differs,
            "Warden (" + wardenPath.status + ", " + PathLength(wardenPath).ToString("0.0") +
            "m) and Shadow (" + shadowPath.status + ", " + PathLength(shadowPath).ToString("0.0") +
            "m) took the same route - the shortcut is not actually agent-specific.");
    }

    private static float PathLength(NavMeshPath path)
    {
        float total = 0f;
        for (int i = 1; i < path.corners.Length; i++)
        {
            total += Vector3.Distance(path.corners[i - 1], path.corners[i]);
        }
        return total;
    }

    [Test]
    public void TheBell_IsARealHingeJoint()
    {
        GameObject bell = GameObject.Find("TowerBell");
        Assert.IsNotNull(bell, "No TowerBell in FrozenCity.");
        Assert.IsNotNull(bell.GetComponent<HingeJoint>(), "TowerBell has no HingeJoint.");
        Assert.IsNotNull(bell.GetComponent<Rigidbody>(), "TowerBell has no Rigidbody - it could not swing.");
    }

    [Test]
    public void TheSecondFracture_IsPlacedAndHasShards()
    {
        var fractured = Object.FindFirstObjectByType<FracturedObject>();
        Assert.IsNotNull(fractured, "No FracturedObject (the frozen statue) in FrozenCity.");
        Assert.GreaterOrEqual(fractured.ShardCount, 15, "Expected the same Voronoi shard count as the source prefab.");
    }

    [Test]
    public void TheChronoHourglass_IsAPickupInTheScene()
    {
        Assert.IsNotNull(FindHourglassPickup(), "No Chrono Hourglass ItemPickup in FrozenCity - T9's second item is not obtainable here.");
    }

    [Test]
    public void HourglassPickup_StartsHiddenUntilTheGearPuzzleIsSolved()
    {
        GameObject hourglass = FindHourglassPickup();
        Assert.IsNotNull(hourglass, "No Chrono Hourglass pickup in FrozenCity.");
        Assert.IsFalse(hourglass.activeSelf, "The Hourglass should stay hidden until the gear puzzle is solved.");
    }

    [UnityTest]
    public IEnumerator GearPuzzle_IsSolvedByFindingInstallingAndVerifyingAcrossTheThreeEras()
    {
        GearPuzzle puzzle = GearPuzzle.Instance;
        Assert.IsNotNull(puzzle, "No GearPuzzle in FrozenCity.");

        var era = Object.FindFirstObjectByType<EraManager>();
        Assert.IsNotNull(era, "No EraManager in FrozenCity.");

        era.SetEra(TimeEra.Present);
        Assert.IsFalse(puzzle.TryInstall(), "Installing without having found the gear first should fail.");

        puzzle.CollectGear();
        era.SetEra(TimeEra.Past);
        Assert.IsFalse(puzzle.TryInstall(), "Installing outside the Present should fail, even with the gear in hand.");

        era.SetEra(TimeEra.Present);
        Assert.IsTrue(puzzle.TryInstall(), "Installing the gear in the Present should succeed.");

        Assert.IsFalse(puzzle.TryVerify(), "Verifying outside the Future should fail.");

        era.SetEra(TimeEra.Future);
        Assert.IsTrue(puzzle.TryVerify(), "Verifying the installed gear in the Future should succeed.");
        yield return null;

        GameObject hourglass = FindHourglassPickup();
        Assert.IsTrue(hourglass.activeSelf, "Solving the gear puzzle should reveal the Chrono Hourglass - " +
            "\"the moment cannot continue\" until it is.");
    }

    [Test]
    public void GearPickup_IsOnlyVisibleAndInteractableInThePast()
    {
        var gear = Object.FindFirstObjectByType<GearPickup>(FindObjectsInactive.Include);
        Assert.IsNotNull(gear, "No GearPickup in FrozenCity.");

        var era = Object.FindFirstObjectByType<EraManager>();

        era.SetEra(TimeEra.Present);
        gear.SendMessage("Update");
        Assert.IsFalse(gear.GetComponent<Collider>().enabled, "The gear should not be interactable outside the Past.");

        era.SetEra(TimeEra.Past);
        gear.SendMessage("Update");
        Assert.IsTrue(gear.GetComponent<Collider>().enabled, "The gear should be interactable while in the Past.");
    }

    private static GameObject FindHourglassPickup()
    {
        // Include inactive: the pickup starts hidden until the gear puzzle is
        // solved (GearPuzzle.rewardObject), so a default search that skips
        // inactive objects would not find it before that.
        var pickups = Object.FindObjectsByType<ItemPickup>(FindObjectsInactive.Include, FindObjectsSortMode.None);

        foreach (ItemPickup pickup in pickups)
        {
            var kind = (ItemPickup.Kind)typeof(ItemPickup)
                .GetField("item", BindingFlags.Instance | BindingFlags.NonPublic)
                .GetValue(pickup);

            if (kind == ItemPickup.Kind.ChronoHourglass)
            {
                return pickup.gameObject;
            }
        }

        return null;
    }

    [Test]
    public void Exit_RequiresTheHourglassBeforeItWillLeaveForClockCore()
    {
        var exit = Object.FindFirstObjectByType<SceneExitTrigger>();
        Assert.IsNotNull(exit, "No SceneExitTrigger in FrozenCity.");

        GameManager.Instance.State.hasChronoHourglass = false;

        InvokeOnPlayerEntered(exit);

        Assert.AreEqual(
            "FrozenCity",
            SceneManager.GetActiveScene().name,
            "The exit let the player through to ClockCore without the Chrono Hourglass.");
        Assert.IsFalse(SceneExitTrigger.LastExitSucceeded);
    }

    [UnityTest]
    public IEnumerator Exit_LeavesForClockCoreOnceTheHourglassIsHeld()
    {
        var exit = Object.FindFirstObjectByType<SceneExitTrigger>();
        Assert.IsNotNull(exit, "No SceneExitTrigger in FrozenCity.");

        GameManager.Instance.State.hasChronoHourglass = true;

        InvokeOnPlayerEntered(exit);
        yield return null;
        yield return null;

        Assert.IsTrue(SceneExitTrigger.LastExitSucceeded);
        Assert.AreEqual(
            "ClockCore",
            SceneManager.GetActiveScene().name,
            "Holding the Chrono Hourglass should let the player leave FrozenCity for ClockCore.");
    }

    private static void InvokeOnPlayerEntered(SceneExitTrigger trigger)
    {
        MethodInfo method = typeof(SceneExitTrigger).GetMethod(
            "OnPlayerEntered", BindingFlags.Instance | BindingFlags.NonPublic);

        method.Invoke(trigger, new object[] { null });
    }
}
