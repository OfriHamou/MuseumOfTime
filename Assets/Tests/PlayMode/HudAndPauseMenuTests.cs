using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

/// <summary>
/// Play-mode checks for Step 5.2: the HUD and pause menu.
///
/// The pause/resume timeScale check is the one the plan calls out by name as
/// a real bug risk (Step 3.6's slow-time work leaves timeScale at 0.3, and a
/// naive "restore what it was" Resume would leak that back into normal play).
/// Everything else here matches the documented verification literally:
/// "every HUD element updates the moment its value changes".
/// </summary>
public sealed class HudAndPauseMenuTests
{
    private GameObject player;

    [UnitySetUp]
    public IEnumerator LoadScene()
    {
        Time.timeScale = 1f;

        SceneManager.LoadScene("MuseumNight", LoadSceneMode.Single);
        yield return null;
        yield return null;

        player = GameObject.Find("Player");
        Assert.IsNotNull(player, "No 'Player' object in MuseumNight.");
    }

    [UnityTearDown]
    public IEnumerator Cleanup()
    {
        Time.timeScale = 1f;
        yield return null;
    }

    [Test]
    public void Resuming_AfterPausingDuringSlowTime_LeavesTimeScaleAtExactlyOne()
    {
        var pause = Object.FindFirstObjectByType<PauseMenuController>();
        Assert.IsNotNull(pause, "No PauseMenuController in the scene.");

        // Simulate the Chrono Hourglass being held when Escape is pressed.
        Time.timeScale = 0.3f;

        SetPrivate(pause, "isPaused", false);
        typeof(PauseMenuController)
            .GetMethod("Toggle", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
            .Invoke(pause, null);

        Assert.AreEqual(0f, Time.timeScale, "Pausing should stop time entirely.");

        pause.Resume();

        Assert.AreEqual(
            1f,
            Time.timeScale,
            "Resume must restore exactly 1, not whatever timeScale held before pausing.");
    }

    private static void SetPrivate(object target, string field, object value)
    {
        var info = target.GetType().GetField(
            field, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);

        Assert.IsNotNull(info, "Field '" + field + "' not found.");
        info.SetValue(target, value);
    }

    private static object GetPrivate(object target, string field)
    {
        var info = target.GetType().GetField(
            field, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);

        Assert.IsNotNull(info, "Field '" + field + "' not found.");
        return info.GetValue(target);
    }

    private static void InvokeToggle(PauseMenuController pause)
    {
        typeof(PauseMenuController)
            .GetMethod("Toggle", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
            .Invoke(pause, null);
    }

    /// <summary>
    /// Bug found in manual testing: PlayerCameraRig locks and hides the OS
    /// cursor for gameplay look and never releases it on its own, so the
    /// pause menu was visible but nothing on it could actually be clicked.
    /// </summary>
    [UnityTest]
    public IEnumerator Pausing_UnlocksTheCursorAndResumingRelocksIt()
    {
        // Not asserted: that the cursor starts locked. Locking depends on the
        // OS window actually having focus, which a headless -batchmode
        // process does not have - PlayerCameraRig.OnEnable's own lock call is
        // a pre-existing, separate concern. What this test owns is the delta
        // Pause()/Resume() themselves cause, regardless of the starting value.
        yield return null;

        var pause = Object.FindFirstObjectByType<PauseMenuController>();
        Assert.IsNotNull(pause, "No PauseMenuController in the scene.");

        SetPrivate(pause, "isPaused", false);
        InvokeToggle(pause);

        Assert.AreEqual(
            CursorLockMode.None,
            Cursor.lockState,
            "Pausing should unlock the cursor - otherwise the menu is visible but unclickable.");
        Assert.IsTrue(Cursor.visible, "Pausing should make the cursor visible.");

        pause.Resume();

        // Not asserted: that Cursor.lockState reads back as Locked.
        // CursorLockMode.Locked requires the OS window to actually have
        // input focus; a headless -batchmode process never has it, so Unity
        // silently cannot honour the lock and it reads back as None
        // regardless of what Resume() requested - the same category of
        // environment limitation Phase 0 already documented for simulated
        // key presses. Resume() calling the correct API is covered by
        // reading the source directly; whether the OS actually grants the
        // lock is a manual check (see Phase5_Unity_Walkthrough.md).
    }

    [UnityTest]
    public IEnumerator PauseMenuButtons_CanActuallyBeClicked()
    {
        var pause = Object.FindFirstObjectByType<PauseMenuController>();
        Assert.IsNotNull(pause, "No PauseMenuController in the scene.");

        SetPrivate(pause, "isPaused", false);
        InvokeToggle(pause);
        yield return null;

        var panel = (GameObject)GetPrivate(pause, "panel");
        Assert.IsTrue(panel.activeSelf, "Pause panel should be open.");

        Button resumeButton = GameObject.Find("ResumeButton")?.GetComponent<Button>();
        Assert.IsNotNull(resumeButton, "No ResumeButton in the scene.");

        resumeButton.onClick.Invoke();
        yield return null;

        Assert.IsFalse(panel.activeSelf, "Clicking Resume should close the pause panel.");
        Assert.AreEqual(1f, Time.timeScale, "Clicking Resume should restore normal time.");
        // Cursor.lockState after Resume is not asserted here - see
        // Pausing_UnlocksTheCursorAndResumingRelocksIt for why.
    }

    /// <summary>
    /// Same "panel covers the button that opened it" bug as the Main Menu's
    /// Controls panel, fixed the same way - a Back button, plus Escape
    /// closing just the sub-panel rather than resuming play out from under it.
    /// </summary>
    [UnityTest]
    public IEnumerator PauseControlsPanel_OpensAndClosesFromBack_WithoutResumingPlay()
    {
        var pause = Object.FindFirstObjectByType<PauseMenuController>();
        Assert.IsNotNull(pause, "No PauseMenuController in the scene.");

        SetPrivate(pause, "isPaused", false);
        InvokeToggle(pause);
        yield return null;

        Button controlsButton = GameObject.Find("PauseControlsButton")?.GetComponent<Button>();
        Assert.IsNotNull(controlsButton, "No PauseControlsButton in the scene.");

        controlsButton.onClick.Invoke();
        yield return null;

        var controlsPanel = (GameObject)GetPrivate(pause, "controlsPanel");
        Assert.IsTrue(controlsPanel.activeSelf, "Controls sub-panel should open.");

        Button backButton = controlsPanel.transform.Find("PauseControlsBackButton")?.GetComponent<Button>();
        Assert.IsNotNull(backButton, "No PauseControlsBackButton - there would be no way back.");

        backButton.onClick.Invoke();
        yield return null;

        Assert.IsFalse(controlsPanel.activeSelf, "Back should close the Controls sub-panel.");

        var panel = (GameObject)GetPrivate(pause, "panel");
        Assert.IsTrue(panel.activeSelf, "Closing Controls should return to the pause menu, not resume gameplay.");
        Assert.AreEqual(0f, Time.timeScale, "Gameplay should still be paused after closing Controls.");
    }

    [Test]
    public void Hud_HasAllTheElementsTheControllerDrives()
    {
        var hud = player.GetComponent<HUDController>();
        Assert.IsNotNull(hud, "Player has no HUDController.");

        Assert.IsNotNull(GameObject.Find("HealthBar"), "No HealthBar in the scene.");
        Assert.IsNotNull(GameObject.Find("EnergyBar"), "No EnergyBar in the scene.");
        Assert.IsNotNull(GameObject.Find("ShardCountText"), "No ShardCountText in the scene.");
        Assert.IsNotNull(GameObject.Find("EraText"), "No EraText in the scene.");
    }

    [UnityTest]
    public IEnumerator Hud_UpdatesTheMomentHealthChanges()
    {
        GameObject bar = GameObject.Find("HealthBar");
        Assert.IsNotNull(bar, "No HealthBar in the scene.");
        Image fill = bar.GetComponent<Image>();

        GameManager.Instance.RestoreFullHealth();
        yield return null;

        Assert.AreEqual(1f, fill.fillAmount, 0.01f, "Full health should read as a full bar.");

        GameManager.Instance.TakeDamage(50);
        yield return null;

        Assert.AreEqual(
            0.5f,
            fill.fillAmount,
            0.01f,
            "The health bar did not update in the same frame TakeDamage fired StateChanged.");
    }

    [UnityTest]
    public IEnumerator Hud_UpdatesTheMomentAShardIsCollected()
    {
        GameObject shardTextGo = GameObject.Find("ShardCountText");
        Assert.IsNotNull(shardTextGo, "No ShardCountText in the scene.");
        var shardText = shardTextGo.GetComponent<TMPro.TMP_Text>();

        int before = GameManager.Instance.State.timeShards;
        GameManager.Instance.AddTimeShard(1);
        yield return null;

        Assert.AreEqual(
            (before + 1).ToString(),
            shardText.text,
            "The shard counter did not update in the same frame the shard was added.");
    }

    [UnityTest]
    public IEnumerator ItemIcons_ReflectAcquiredItems()
    {
        var hud = player.GetComponent<HUDController>();
        Assert.IsNotNull(hud, "Player has no HUDController.");

        // Not GameObject.Find: the icon is legitimately inactive whenever the
        // item has not been acquired, and Find cannot see inactive objects.
        var lensIcon = (GameObject)typeof(HUDController)
            .GetField("timeLensIcon", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
            .GetValue(hud);

        Assert.IsNotNull(lensIcon, "HUDController has no timeLensIcon wired up.");

        // ResetGame both clears hasTimeLens and fires StateChanged, giving a
        // known baseline instead of guessing what an earlier test left behind.
        GameManager.Instance.ResetGame();
        yield return null;

        Assert.IsFalse(lensIcon.activeSelf, "Icon should be hidden before the item is acquired.");

        GameManager.Instance.AcquireTimeLens();
        yield return null;

        Assert.IsTrue(lensIcon.activeSelf, "TimeLensIcon should be shown once the Time Lens is acquired.");
    }
}
