using System.Collections;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

/// <summary>
/// Play-mode checks for the Victory half of Step 5.1: "Victory shows correct
/// stats" (T1), read from the same GameState the rest of the game already
/// writes to.
/// </summary>
public sealed class VictoryScreenTests
{
    [UnitySetUp]
    public IEnumerator LoadScene()
    {
        // Victory has no GameManager of its own - it is only ever reached
        // from a gameplay scene, and relies on the DontDestroyOnLoad
        // singleton already existing, same as production. Load MuseumNight
        // first purely to guarantee that.
        SceneManager.LoadScene("MuseumNight", LoadSceneMode.Single);
        yield return null;

        Assert.IsNotNull(GameManager.Instance, "No GameManager available to seed state from.");
        GameManager.Instance.ResetGame();
        GameManager.Instance.AddScore(1234);
        GameManager.Instance.AddTimeShard(3);
        GameManager.Instance.RegisterDetection();
        GameManager.Instance.State.playTimeSeconds = 125f;

        SceneManager.LoadScene("Victory", LoadSceneMode.Single);
        yield return null;
        yield return null;
    }

    [Test]
    public void Scene_HasAnInputSystemDrivenEventSystem()
    {
        var eventSystem = Object.FindFirstObjectByType<EventSystem>();
        Assert.IsNotNull(eventSystem, "Victory has no EventSystem - buttons cannot be clicked.");

        Assert.IsNotNull(
            eventSystem.GetComponent<InputSystemUIInputModule>(),
            "EventSystem is not using the New Input System's UI module (T12).");
    }

    [Test]
    public void VictoryScreen_ShowsTheRunsRealStats()
    {
        GameObject canvas = GameObject.Find("VictoryCanvas");
        Assert.IsNotNull(canvas, "No VictoryCanvas in the scene.");

        TMP_Text scoreText = canvas.transform.Find("ScoreText").GetComponent<TMP_Text>();
        TMP_Text shardsText = canvas.transform.Find("ShardsText").GetComponent<TMP_Text>();
        TMP_Text detectionsText = canvas.transform.Find("DetectionsText").GetComponent<TMP_Text>();
        TMP_Text playtimeText = canvas.transform.Find("PlaytimeText").GetComponent<TMP_Text>();

        // Read the expected score back from GameState rather than hardcoding
        // the arithmetic: AddTimeShard also awards score (Step 3.4) and
        // RegisterDetection deducts it, so the true total is whatever
        // GameState actually landed on, not just the 1234 added directly.
        GameState state = GameManager.Instance.State;

        StringAssert.Contains(state.score.ToString(), scoreText.text);
        StringAssert.Contains("3", shardsText.text);
        StringAssert.Contains("1", detectionsText.text);
        StringAssert.Contains("02:05", playtimeText.text);
    }
}
