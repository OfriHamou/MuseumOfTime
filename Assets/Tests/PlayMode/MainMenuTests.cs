using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

/// <summary>
/// Play-mode checks for Step 5.1: the main menu.
///
/// Covers the documented verification for T1 that does not need eyes on the
/// screen: New Game actually starts clean, Continue is gated on a real save
/// file, and the EventSystem is driven by the New Input System rather than
/// the legacy Standalone module (T12 extends to menu navigation too).
/// </summary>
public sealed class MainMenuTests
{
    [UnitySetUp]
    public IEnumerator LoadScene()
    {
        SceneManager.LoadScene("MainMenu", LoadSceneMode.Single);
        yield return null;
        yield return null;
    }

    [UnityTearDown]
    public IEnumerator Cleanup()
    {
        SaveService.Delete();
        yield return null;
    }

    [Test]
    public void Scene_HasAnInputSystemDrivenEventSystem()
    {
        var eventSystem = Object.FindFirstObjectByType<EventSystem>();
        Assert.IsNotNull(eventSystem, "MainMenu has no EventSystem - buttons cannot be clicked.");

        Assert.IsNotNull(
            eventSystem.GetComponent<InputSystemUIInputModule>(),
            "EventSystem is not using the New Input System's UI module (T12).");
    }

    [Test]
    public void ContinueButton_IsDisabledWithoutASave()
    {
        SaveService.Delete();

        Assert.IsNotNull(
            Object.FindFirstObjectByType<MainMenuController>(),
            "No MainMenuController in the MainMenu scene.");

        Button continueButton = GameObject.Find("ContinueButton")?.GetComponent<Button>();
        Assert.IsNotNull(continueButton, "No ContinueButton in the scene.");

        // MainMenuController.Start() already ran during LoadScene above,
        // driven by SaveService.Exists as it was at that point (no save).
        Assert.IsFalse(continueButton.interactable, "Continue should be disabled with no save file.");
    }

    [UnityTest]
    public IEnumerator ContinueButton_IsEnabledOnceASaveExists()
    {
        Assert.IsNotNull(GameManager.Instance, "No GameManager in the scene.");
        GameManager.Instance.AcquireTimeLens();
        SaveService.Save();

        SceneManager.LoadScene("MainMenu", LoadSceneMode.Single);
        yield return null;
        yield return null;

        Button continueButton = GameObject.Find("ContinueButton")?.GetComponent<Button>();
        Assert.IsNotNull(continueButton, "No ContinueButton in the scene.");
        Assert.IsTrue(continueButton.interactable, "Continue should be enabled once a save exists.");
    }

    [UnityTest]
    public IEnumerator NewGameButton_ResetsStateAndLoadsMuseumNight()
    {
        Assert.IsNotNull(GameManager.Instance, "No GameManager in the scene.");
        GameManager.Instance.AddScore(500);

        Button newGameButton = GameObject.Find("NewGameButton")?.GetComponent<Button>();
        Assert.IsNotNull(newGameButton, "No NewGameButton in the scene.");

        newGameButton.onClick.Invoke();
        yield return null;
        yield return null;

        Assert.AreEqual(0, GameManager.Instance.State.score, "New Game did not reset the score.");
        Assert.AreEqual(
            "MuseumNight",
            SceneManager.GetActiveScene().name,
            "New Game did not load MuseumNight.");
    }

    /// <summary>
    /// Bug found in manual testing: the Controls panel covers the button that
    /// opened it (both are centered on the same canvas), so there was no way
    /// back - no Back button, and Escape was never read at all.
    /// </summary>
    [UnityTest]
    public IEnumerator ControlsPanel_OpensFromTheButtonAndClosesFromBack()
    {
        var controller = Object.FindFirstObjectByType<MainMenuController>();
        Assert.IsNotNull(controller, "No MainMenuController in the scene.");

        // Reflection, not GameObject.Find: the panel starts inactive
        // (MainMenuController.Start), and Find does not see inactive objects.
        var panel = (GameObject)typeof(MainMenuController)
            .GetField("controlsPanel", BindingFlags.Instance | BindingFlags.NonPublic)
            .GetValue(controller);

        Assert.IsNotNull(panel, "MainMenuController has no controlsPanel wired up.");
        Assert.IsFalse(panel.activeSelf, "Controls panel should start closed.");

        Button controlsButton = GameObject.Find("ControlsButton")?.GetComponent<Button>();
        Assert.IsNotNull(controlsButton, "No ControlsButton in the scene.");

        controlsButton.onClick.Invoke();
        yield return null;

        Assert.IsTrue(panel.activeSelf, "Controls panel should open when ControlsButton is clicked.");

        Button backButton = panel.transform.Find("ControlsBackButton")?.GetComponent<Button>();
        Assert.IsNotNull(backButton, "No ControlsBackButton inside the Controls panel - there would be no way back.");

        backButton.onClick.Invoke();
        yield return null;

        Assert.IsFalse(panel.activeSelf, "The Back button should close the Controls panel.");
    }
}
