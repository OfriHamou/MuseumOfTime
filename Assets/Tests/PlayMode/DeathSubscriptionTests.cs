using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

/// <summary>
/// Dying must work on the FIRST scene load, not only on later ones.
///
/// This is the bug the existing death tests could not see. No scene contains a
/// GameManager - it is created by a RuntimeInitializeOnLoadMethod(AfterSceneLoad)
/// bootstrap, which runs after every scene object's Awake and OnEnable. So
/// RespawnService.OnEnable found GameManager.Instance null, never subscribed to
/// PlayerDied, and nothing at all was listening when health reached zero.
///
/// The earlier tests passed anyway because by the time they ran, a GameManager
/// from a previous scene was already alive and carried across by
/// DontDestroyOnLoad - so the subscription happened to succeed. The player
/// hitting it fresh got a health bar reading 0% and no death, no screen, no
/// respawn. And because TakeDamage returns early once health is zero, they were
/// then stuck alive at zero forever: unable to die, unable to be sent back.
/// </summary>
public sealed class DeathSubscriptionTests
{
    [UnityTearDown]
    public IEnumerator Cleanup()
    {
        Time.timeScale = 1f;
        yield return null;
    }

    [UnityTest]
    public IEnumerator ZeroHealthAlwaysProducesADeathInEveryScene()
    {
        foreach (string sceneName in new[] { "MuseumNight", "FrozenCity", "ClockCore" })
        {
            SceneManager.LoadScene(sceneName, LoadSceneMode.Single);
            yield return null;
            yield return null;

            GameManager.Instance.ResetGame();

            // Arm an anchor so this measures the death firing at all, rather
            // than the game-over-to-menu path.
            GameManager.Instance.SaveCheckpoint(sceneName, new Vector3(0f, 2f, 0f));
            yield return null;

            var respawn = Object.FindFirstObjectByType<RespawnService>();
            Assert.IsNotNull(respawn, sceneName + " has no RespawnService.");

            int before = respawn.RespawnCount;

            GameManager.Instance.TakeDamage(GameManager.Instance.State.maxHealth);

            float deadline = Time.unscaledTime + 12f;

            while (respawn.RespawnCount == before && Time.unscaledTime < deadline)
            {
                yield return null;
            }

            Assert.Greater(
                respawn.RespawnCount, before,
                sceneName + ": health reached zero and nothing happened. " +
                "Nothing was listening for PlayerDied, so the player is stuck " +
                "alive at zero health with no death, no screen and no respawn.");

            Assert.Greater(
                GameManager.Instance.State.currentHealth, 0,
                sceneName + ": the player was left at zero health.");
        }
    }

    /// <summary>
    /// The stuck-at-zero state itself: even if health is emptied by some path
    /// that never raises the event, the watchdog must still notice.
    /// </summary>
    [UnityTest]
    public IEnumerator HealthAtZeroWithNoEventStillTriggersADeath()
    {
        SceneManager.LoadScene("ClockCore", LoadSceneMode.Single);
        yield return null;
        yield return null;

        GameManager.Instance.ResetGame();
        GameManager.Instance.SaveCheckpoint("ClockCore", new Vector3(0f, 2f, -10f));
        yield return null;

        var respawn = Object.FindFirstObjectByType<RespawnService>();
        int before = respawn.RespawnCount;

        // Empty health WITHOUT going through TakeDamage, so no event is raised
        // - a loaded save, a direct write, an event lost to script ordering.
        GameManager.Instance.State.currentHealth = 0;

        float deadline = Time.unscaledTime + 12f;

        while (respawn.RespawnCount == before && Time.unscaledTime < deadline)
        {
            yield return null;
        }

        Assert.Greater(
            respawn.RespawnCount, before,
            "Health sat at zero and no death ever happened. The player can " +
            "neither die nor recover - the run is stuck with a 0% health bar " +
            "and nothing occurring.");
    }
}
