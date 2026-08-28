using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

/// <summary>
/// Regression cover for the bug that made pickups feel broken: standing right
/// in front of a Time Shard or the Time Lens, no prompt appeared and E did
/// nothing.
///
/// Three separate faults stacked up, and any one of them alone was enough:
///
///   1. The look-cast used QueryTriggerInteraction.Collide and took only the
///      single NEAREST hit. The museum is full of trigger volumes - room entry,
///      eight tutorial reveals, era zones - all on the Default layer inside the
///      interact mask. Any one between the camera and a pickup swallowed the
///      cast and returned a collider with no IInteractable. Measured: standing
///      2 m from a shard, the ray was eaten by Trigger_MainGallery 6.6 m away.
///
///   2. It was a zero-width ray from the camera. In third person the camera
///      sits half a metre off Noa's shoulder, so its forward runs PARALLEL to
///      the player-to-target line - it passed 0.54 m to the side of a shard
///      whose collider was 0.28 m across.
///
///   3. Once widened to a spherecast, the first solid thing it met was Noa's
///      own CharacterController, which counted as an occluding wall.
///
/// These tests assert the OUTCOME a player cares about - "standing in front of
/// a pickup, can I take it" - rather than the mechanism, so they stay valid if
/// the casting strategy changes again.
/// </summary>
public sealed class InteractionReachTests
{
    private GameObject player;
    private PlayerInteractor interactor;
    private CharacterController controller;

    [UnitySetUp]
    public IEnumerator LoadScene()
    {
        SceneManager.LoadScene("MuseumNight", LoadSceneMode.Single);
        yield return null;
        yield return null;

        player = GameObject.FindWithTag("Player");
        Assert.IsNotNull(player, "No Player in MuseumNight.");

        interactor = player.GetComponent<PlayerInteractor>();
        Assert.IsNotNull(interactor, "Player has no PlayerInteractor.");

        controller = player.GetComponent<CharacterController>();
    }

    /// <summary>
    /// Drops the pickup two metres along the player's own facing, at chest
    /// height - the position a player would naturally walk up to.
    /// </summary>
    private IEnumerator StandInFrontOf(Component pickup)
    {
        // Let the Cinemachine rig settle before moving anything.
        yield return null;

        Vector3 inFront = player.transform.position
                          + (player.transform.forward * 2f)
                          + new Vector3(0f, 1.1f, 0f);

        pickup.transform.position = inFront;

        // A few frames: the interactor casts in Update, and the camera the
        // cast originates from is driven by the Brain in LateUpdate.
        for (int i = 0; i < 5; i++)
        {
            yield return null;
        }
    }

    [UnityTest]
    public IEnumerator AShardDirectlyAheadCanBeSeenAndTaken()
    {
        var shard = Object.FindFirstObjectByType<ShardPickup>();
        Assert.IsNotNull(shard, "MuseumNight has no Time Shard.");

        yield return StandInFrontOf(shard);

        Assert.IsNotNull(
            interactor.Current,
            "Standing two metres from a Time Shard with it dead ahead, the " +
            "interactor found nothing - so no prompt appears and E does nothing.");

        Assert.AreEqual("Collect the Time Shard", interactor.CurrentPrompt);

        int before = GameManager.Instance.State.timeShards;
        interactor.Current.Interact(player);

        Assert.AreEqual(
            before + 1, GameManager.Instance.State.timeShards,
            "Interacting with a Time Shard did not collect it.");
    }

    [UnityTest]
    public IEnumerator TheTimeLensCanBeSeenAndTaken()
    {
        var lens = Object.FindFirstObjectByType<ItemPickup>();
        Assert.IsNotNull(lens, "MuseumNight has no Time Lens.");

        GameManager.Instance.ResetGame();
        yield return null;

        yield return StandInFrontOf(lens);

        Assert.IsNotNull(
            interactor.Current,
            "The Time Lens could not be targeted. This is the defect the " +
            "player hit: the objective says to find it, and then it cannot " +
            "be picked up.");

        StringAssert.Contains("Time Lens", interactor.CurrentPrompt);

        interactor.Current.Interact(player);

        Assert.IsTrue(
            GameManager.Instance.State.hasTimeLens,
            "Taking the Time Lens did not grant it.");

        Assert.IsTrue(
            EraManager.Instance.IsUnlocked,
            "Taking the Time Lens should unlock era travel - it is what opens " +
            "the game's signature mechanic.");
    }

    /// <summary>
    /// The specific occlusion bug: a trigger volume in the way must not hide
    /// what is behind it, but a solid wall must.
    /// </summary>
    [UnityTest]
    public IEnumerator TriggerVolumesDoNotBlockInteractionButWallsDo()
    {
        var shard = Object.FindFirstObjectByType<ShardPickup>();
        Assert.IsNotNull(shard);

        yield return StandInFrontOf(shard);
        Assert.IsNotNull(interactor.Current, "Baseline: the shard should be reachable.");

        // A trigger volume placed squarely between player and shard.
        var blocker = GameObject.CreatePrimitive(PrimitiveType.Cube);
        blocker.name = "TestTriggerVolume";
        blocker.transform.position =
            Vector3.Lerp(player.transform.position, shard.transform.position, 0.5f);
        blocker.transform.localScale = new Vector3(3f, 3f, 0.4f);
        blocker.GetComponent<Collider>().isTrigger = true;

        for (int i = 0; i < 3; i++) { yield return null; }

        Assert.IsNotNull(
            interactor.Current,
            "A trigger volume between the player and the shard hid it. Trigger " +
            "volumes are invisible and must not occlude interaction.");

        // The same object, made solid, SHOULD block it.
        blocker.GetComponent<Collider>().isTrigger = false;

        for (int i = 0; i < 3; i++) { yield return null; }

        Assert.IsNull(
            interactor.Current,
            "A solid wall between the player and the shard did not block it - " +
            "the player could reach through geometry.");

        Object.Destroy(blocker);
        yield return null;
    }
}
