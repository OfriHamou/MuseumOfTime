using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

/// <summary>
/// Leaving the world must not be a way to lose the game permanently.
///
/// There was no kill plane, no out-of-bounds volume and no height check
/// anywhere in the project. A player who jumped off the mezzanine and over the
/// museum wall fell forever: no damage, no death, no respawn, nothing to do
/// but quit. And it is easy to reach by accident, because the objective at
/// that moment reads "leave the museum" and jumping out of a building is a
/// fair reading of that.
/// </summary>
public sealed class FallGuardTests
{
    private static readonly string[] GameplayScenes =
    {
        "MuseumNight", "FrozenCity", "ClockCore",
    };

    [UnityTearDown]
    public IEnumerator Cleanup()
    {
        Time.timeScale = 1f;
        yield return null;
    }

    [UnityTest]
    public IEnumerator EveryGameplaySceneGuardsAgainstFallingOut()
    {
        foreach (string sceneName in GameplayScenes)
        {
            SceneManager.LoadScene(sceneName, LoadSceneMode.Single);
            yield return null;
            yield return null;

            GameObject player = GameObject.FindWithTag("Player");
            Assert.IsNotNull(player, sceneName + " has no player.");

            Assert.IsNotNull(
                player.GetComponent<FallGuard>(),
                sceneName + " has no FallGuard, so a player who falls out of " +
                "the level there drops forever with no way back.");
        }
    }

    [UnityTest]
    public IEnumerator FallingOutOfTheWorldKillsAndReturnsThePlayer()
    {
        SceneManager.LoadScene("MuseumNight", LoadSceneMode.Single);
        yield return null;
        yield return null;

        GameManager.Instance.ResetGame();
        yield return null;

        GameObject player = GameObject.FindWithTag("Player");
        var guard = player.GetComponent<FallGuard>();
        Assert.IsNotNull(guard, "The player has no FallGuard.");

        var respawn = Object.FindFirstObjectByType<RespawnService>();
        Assert.IsNotNull(respawn, "MuseumNight has no RespawnService.");

        // Arm an anchor, so this test is about the FALL being caught rather
        // than about the game-over path. Without one, a death correctly ends
        // the run and sends the player to the menu instead of respawning.
        GameManager.Instance.SaveCheckpoint("MuseumNight", new Vector3(0f, 1f, 0f));

        int respawnsBefore = respawn.RespawnCount;

        // Drop the player through the floor, the way jumping over the wall does.
        var controller = player.GetComponent<CharacterController>();
        if (controller != null) { controller.enabled = false; }

        player.transform.position = new Vector3(0f, -60f, 0f);

        if (controller != null) { controller.enabled = true; }

        float deadline = Time.unscaledTime + 3f;

        while (guard.CatchCount == 0 && Time.unscaledTime < deadline)
        {
            yield return null;
        }

        Assert.Greater(
            guard.CatchCount, 0,
            "The player fell far below the level and nothing noticed. That is " +
            "an endless fall with no death and no respawn - the run is over " +
            "and the only way out is to quit.");

        // The death screen names it, then hands back to the respawn.
        Assert.AreEqual("You fell out of the world.", RespawnService.LastCauseOfDeath,
            "The death screen would not say why the player died.");

        float until = Time.unscaledTime + 10f;

        while (respawn.RespawnCount == respawnsBefore && Time.unscaledTime < until)
        {
            yield return null;
        }

        Assert.Greater(
            respawn.RespawnCount, respawnsBefore,
            "Falling out of the world never respawned the player.");

        Assert.Greater(
            player.transform.position.y, -20f,
            "The player was 'respawned' but is still below the level.");

        Assert.Greater(
            GameManager.Instance.State.currentHealth, 0,
            "The player came back from the fall with no health.");
    }

    /// <summary>
    /// One fall is one death. Without this the player keeps falling behind the
    /// death screen and racks up a death per frame.
    /// </summary>
    [UnityTest]
    public IEnumerator OneFallCountsOnce()
    {
        SceneManager.LoadScene("MuseumNight", LoadSceneMode.Single);
        yield return null;
        yield return null;

        GameManager.Instance.ResetGame();
        yield return null;

        GameObject player = GameObject.FindWithTag("Player");
        var guard = player.GetComponent<FallGuard>();

        // Keep the CharacterController ENABLED and just write the position.
        //
        // Disabling it makes PlayerController.Update log "Move called on
        // inactive controller" every frame, which the test runner treats as a
        // failure - and the controller is perfectly happy to have its
        // transform written directly; it re-syncs on the next move.
        float until = Time.unscaledTime + 1.5f;

        while (Time.unscaledTime < until)
        {
            player.transform.position = new Vector3(0f, -60f, 0f);
            yield return null;
        }

        Assert.AreEqual(
            1, guard.CatchCount,
            "A single fall registered " + guard.CatchCount + " deaths - the " +
            "guard is re-firing every frame while the player is still down there.");
    }
}
