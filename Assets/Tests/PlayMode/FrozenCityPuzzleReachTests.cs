using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

/// <summary>
/// The FrozenCity gear puzzle, checked for REACHABILITY rather than logic.
///
/// GearPuzzle's own state machine was already covered, and it passed - but the
/// puzzle was still impossible to play, because the socket the player has to
/// press E on was buried inside the clock tower. It was placed 2.5 m in front
/// of the tower's centre, and the tower's Shaft is a 6 m cube, so its front
/// face is 3 m out: the socket sat half a metre INSIDE solid geometry and the
/// look-cast hit the wall every time.
///
/// A logic test cannot catch that. These assert the physical situation: that
/// nothing solid stands between the player and each puzzle piece.
/// </summary>
public sealed class FrozenCityPuzzleReachTests
{
    private const string Scene = "FrozenCity";

    [UnitySetUp]
    public IEnumerator LoadScene()
    {
        SceneManager.LoadScene(Scene, LoadSceneMode.Single);
        yield return null;
        yield return null;

        GameManager.Instance.ResetGame();
        GameManager.Instance.State.hasTimeLens = true;
        yield return null;
    }

    private static Component FindByTypeName(string typeName)
    {
        System.Type type = typeof(GearPuzzle).Assembly.GetType(typeName);
        Assert.IsNotNull(type, "No type called " + typeName + ".");

        Object[] found = Object.FindObjectsByType(
            type, FindObjectsInactive.Include, FindObjectsSortMode.None);

        Assert.Greater(found.Length, 0, "No " + typeName + " in " + Scene + ".");
        return (Component)found[0];
    }

    /// <summary>
    /// Is anything solid sitting between a point just outside the piece and the
    /// piece itself? That is what "buried in a wall" looks like to a raycast.
    /// </summary>
    private static void AssertNotEmbeddedInGeometry(Component piece, string label)
    {
        Collider own = piece.GetComponent<Collider>();
        Assert.IsNotNull(own, label + " has no collider at all.");

        Vector3 centre = own.bounds.center;
        int mask = LayerMask.GetMask("Default", "Interactable");

        // Approach from each horizontal side; at least one must be clear, or
        // the player can never look at it from anywhere.
        Vector3[] approaches =
        {
            Vector3.forward, Vector3.back, Vector3.left, Vector3.right,
        };

        bool reachableFromSomewhere = false;
        var blockedBy = new System.Collections.Generic.List<string>();

        foreach (Vector3 approach in approaches)
        {
            Vector3 from = centre + (approach * 2.5f);
            Vector3 direction = (centre - from).normalized;

            var hits = Physics.RaycastAll(
                from, direction, 2.5f, mask, QueryTriggerInteraction.Ignore);

            System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

            bool clear = true;

            foreach (RaycastHit hit in hits)
            {
                if (hit.collider == own)
                {
                    break;
                }

                // Something solid in the way that is not the piece itself.
                clear = false;
                blockedBy.Add(approach + " blocked by " + hit.collider.gameObject.name);
                break;
            }

            if (clear) { reachableFromSomewhere = true; }
        }

        Assert.IsTrue(
            reachableFromSomewhere,
            label + " is walled in on every side - it cannot be interacted with " +
            "from anywhere. " + string.Join("; ", blockedBy));
    }

    [UnityTest]
    public IEnumerator TheGearSocketIsNotBuriedInsideTheClockTower()
    {
        yield return null;

        Component socket = FindByTypeName("GearSocket");
        AssertNotEmbeddedInGeometry(socket, "GearSocket");
    }

    [UnityTest]
    public IEnumerator TheGearIsReachableInThePast()
    {
        EraManager.Instance.SetEra(TimeEra.Past);
        yield return null;

        Component gear = FindByTypeName("GearPickup");

        Assert.IsTrue(
            gear.GetComponent<Collider>().enabled,
            "The gear has no active collider in the Past, so it cannot be taken " +
            "- and the Past is the only era the objective sends the player to.");

        AssertNotEmbeddedInGeometry(gear, "Gear");
    }

    [UnityTest]
    public IEnumerator SolvingThePuzzleRevealsTheChronoHourglass()
    {
        var reward = Object.FindObjectsByType<ItemPickup>(
            FindObjectsInactive.Include, FindObjectsSortMode.None);

        Assert.Greater(reward.Length, 0, "FrozenCity has no ItemPickup reward.");

        Assert.IsFalse(
            reward[0].gameObject.activeSelf,
            "The Chrono Hourglass should start hidden - the puzzle is supposed " +
            "to gate it, not sit next to it.");

        GearPuzzle puzzle = GearPuzzle.Instance;
        Assert.IsNotNull(puzzle, "No GearPuzzle in FrozenCity.");

        EraManager.Instance.SetEra(TimeEra.Past);
        puzzle.CollectGear();
        yield return null;

        EraManager.Instance.SetEra(TimeEra.Present);
        Assert.IsTrue(puzzle.TryInstall(), "The gear could not be installed in the Present.");
        yield return null;

        EraManager.Instance.SetEra(TimeEra.Future);
        Assert.IsTrue(puzzle.TryVerify(), "The gear could not be verified in the Future.");
        yield return null;

        Assert.IsTrue(
            reward[0].gameObject.activeSelf,
            "Solving the gear puzzle did not reveal the Chrono Hourglass, so the " +
            "run cannot continue to ClockCore.");
    }
}
