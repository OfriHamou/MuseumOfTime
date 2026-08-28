using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

/// <summary>
/// Dying has to be visible.
///
/// It was not. GameManager raised PlayerDied at zero health and the only thing
/// listening was RespawnService, which teleported the player to the last anchor
/// and healed them - silently. From the player's side the world simply jumped:
/// no screen, no message, nothing naming what had happened. Reaching zero
/// health is the most important thing the game has to tell you, and it was the
/// one thing it never said.
/// </summary>
public sealed class DeathFeedbackTests
{
    [UnitySetUp]
    public IEnumerator LoadScene()
    {
        SceneManager.LoadScene("MuseumNight", LoadSceneMode.Single);
        yield return null;
        yield return null;

        GameManager.Instance.ResetGame();
        yield return null;
    }

    [UnityTearDown]
    public IEnumerator Cleanup()
    {
        Time.timeScale = 1f;
        yield return null;
    }

    [UnityTest]
    public IEnumerator EveryGameplaySceneCarriesADeathScreen()
    {
        foreach (string sceneName in new[] { "MuseumNight", "FrozenCity", "ClockCore" })
        {
            SceneManager.LoadScene(sceneName, LoadSceneMode.Single);
            yield return null;
            yield return null;

            Assert.IsNotNull(
                Object.FindFirstObjectByType<DeathOverlay>(FindObjectsInactive.Include),
                sceneName + " has no DeathOverlay, so dying there would happen " +
                "silently - the player would just be teleported with no idea why.");
        }
    }

    [UnityTest]
    public IEnumerator DyingWithNoAnchorIsAGameOverAndReturnsToTheMainMenu()
    {
        var overlay = Object.FindFirstObjectByType<DeathOverlay>(FindObjectsInactive.Include);
        Assert.IsNotNull(overlay, "MuseumNight has no DeathOverlay.");

        // MuseumNight has no Time Anchors at all - T21 puts them in scenes 2
        // and 3 - so there is nowhere earned to go back to. Respawning here
        // just dropped the player at the start with no acknowledgement they
        // had died, and if the spot was unsafe it looped forever.
        Assert.IsFalse(GameManager.Instance.State.hasCheckpoint,
            "This test needs a run with no anchor set.");

        int before = overlay.ShowCount;

        RespawnService.LastCauseOfDeath = "A temporal rift tore through you.";
        GameManager.Instance.TakeDamage(GameManager.Instance.State.maxHealth);

        float deadline = Time.unscaledTime + 3f;

        while (overlay.ShowCount == before && Time.unscaledTime < deadline)
        {
            yield return null;
        }

        Assert.Greater(overlay.ShowCount, before,
            "Health reached zero and no death screen appeared.");

        Assert.AreEqual("GAME OVER", overlay.LastHeadline,
            "With no anchor to return to, a death is the end of the run and " +
            "should say so rather than silently putting the player back.");

        // And it must actually leave for the menu.
        float until = Time.unscaledTime + 12f;

        while (SceneManager.GetActiveScene().name != "MainMenu" &&
               Time.unscaledTime < until)
        {
            yield return null;
        }

        Assert.AreEqual("MainMenu", SceneManager.GetActiveScene().name,
            "A game over never reached the main menu, so the player is left " +
            "in the level with nothing to do.");
    }

    /// <summary>
    /// With an anchor armed, T21's behaviour still holds: the player goes back
    /// to it rather than being thrown out to the menu.
    /// </summary>
    [UnityTest]
    public IEnumerator DyingWithAnAnchorReturnsToItInsteadOfEndingTheRun()
    {
        var overlay = Object.FindFirstObjectByType<DeathOverlay>(FindObjectsInactive.Include);
        var respawn = Object.FindFirstObjectByType<RespawnService>();
        Assert.IsNotNull(respawn, "MuseumNight has no RespawnService.");

        GameManager.Instance.SaveCheckpoint("MuseumNight", new Vector3(2f, 1f, 2f));
        Assert.IsTrue(GameManager.Instance.State.hasCheckpoint);

        int respawnsBefore = respawn.RespawnCount;

        RespawnService.LastCauseOfDeath = "A temporal rift tore through you.";
        GameManager.Instance.TakeDamage(GameManager.Instance.State.maxHealth);

        float deadline = Time.unscaledTime + 12f;

        while (respawn.RespawnCount == respawnsBefore && Time.unscaledTime < deadline)
        {
            yield return null;
        }

        Assert.AreEqual("YOU DIED", overlay.LastHeadline,
            "With an anchor armed the run continues, so the screen should not " +
            "announce a game over.");

        Assert.Greater(respawn.RespawnCount, respawnsBefore,
            "Dying with an anchor armed did not return the player to it (T21).");

        Assert.AreEqual("MuseumNight", SceneManager.GetActiveScene().name,
            "Dying with an anchor armed threw the player out to the menu " +
            "instead of returning them to the anchor.");

        Assert.Greater(GameManager.Instance.State.currentHealth, 0,
            "The player was returned to the anchor with no health.");
    }

    /// <summary>
    /// The loop the player actually hit: die, respawn, die again immediately.
    /// </summary>
    [UnityTest]
    public IEnumerator DyingRepeatedlyEndsTheRunRatherThanLooping()
    {
        var respawn = Object.FindFirstObjectByType<RespawnService>();

        GameManager.Instance.SaveCheckpoint("MuseumNight", new Vector3(2f, 1f, 2f));

        // First death: returns to the anchor.
        GameManager.Instance.TakeDamage(GameManager.Instance.State.maxHealth);

        float deadline = Time.unscaledTime + 12f;

        while (respawn.RespawnCount == 0 && Time.unscaledTime < deadline)
        {
            yield return null;
        }

        Assert.Greater(respawn.RespawnCount, 0, "The first death never respawned.");

        // Second death, straight away - the anchor is evidently not safe.
        GameManager.Instance.TakeDamage(GameManager.Instance.State.maxHealth);

        float until = Time.unscaledTime + 12f;

        while (SceneManager.GetActiveScene().name != "MainMenu" &&
               Time.unscaledTime < until)
        {
            yield return null;
        }

        Assert.AreEqual("MainMenu", SceneManager.GetActiveScene().name,
            "Dying again moments after respawning sent the player back to the " +
            "same unsafe spot. That is the never-ending die-respawn loop.");
    }
}
