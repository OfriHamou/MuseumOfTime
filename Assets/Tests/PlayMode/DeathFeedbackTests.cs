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
    public IEnumerator RunningOutOfHealthShowsTheDeathScreenAndNamesTheCause()
    {
        var overlay = Object.FindFirstObjectByType<DeathOverlay>(FindObjectsInactive.Include);
        Assert.IsNotNull(overlay, "MuseumNight has no DeathOverlay.");

        int before = overlay.ShowCount;

        RespawnService.LastCauseOfDeath = "A temporal rift tore through you.";
        GameManager.Instance.TakeDamage(GameManager.Instance.State.maxHealth);

        // The overlay fades in over a fraction of a second.
        float deadline = Time.unscaledTime + 3f;

        while (overlay.ShowCount == before && Time.unscaledTime < deadline)
        {
            yield return null;
        }

        Assert.Greater(
            overlay.ShowCount, before,
            "Health reached zero and no death screen appeared. Dying was " +
            "invisible: the player was teleported and healed with nothing on " +
            "screen to say so.");

        Assert.AreEqual("YOU DIED", overlay.LastHeadline);

        Assert.IsTrue(overlay.IsShowing,
            "The death screen should still be held on screen at this point, " +
            "long enough to be read.");
    }

    /// <summary>
    /// The respawn must wait for the screen, or the player is moved behind a
    /// fade and never sees the death that caused it.
    /// </summary>
    [UnityTest]
    public IEnumerator TheRespawnWaitsForTheDeathScreenToFinish()
    {
        var respawn = Object.FindFirstObjectByType<RespawnService>();
        Assert.IsNotNull(respawn, "MuseumNight has no RespawnService.");

        var overlay = Object.FindFirstObjectByType<DeathOverlay>(FindObjectsInactive.Include);
        int respawnsBefore = respawn.RespawnCount;

        GameManager.Instance.TakeDamage(GameManager.Instance.State.maxHealth);

        // While the screen is up, the respawn must NOT have happened yet.
        float deadline = Time.unscaledTime + 2f;
        bool sawScreenBeforeRespawn = false;

        while (Time.unscaledTime < deadline)
        {
            if (overlay.IsShowing && respawn.RespawnCount == respawnsBefore)
            {
                sawScreenBeforeRespawn = true;
                break;
            }

            yield return null;
        }

        Assert.IsTrue(
            sawScreenBeforeRespawn,
            "The player was respawned before the death screen was shown, so " +
            "the death is invisible again.");

        // And it must eventually finish and respawn them.
        float until = Time.unscaledTime + 8f;

        while (respawn.RespawnCount == respawnsBefore && Time.unscaledTime < until)
        {
            yield return null;
        }

        Assert.Greater(
            respawn.RespawnCount, respawnsBefore,
            "The death screen never handed back to the respawn, so the run " +
            "would be stuck on a black screen forever.");

        Assert.Greater(
            GameManager.Instance.State.currentHealth, 0,
            "The player was respawned without any health.");
    }
}
