using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

/// <summary>
/// Regression cover for the bug that made the game unplayable: the mouse
/// escaping the window during play, after which looking around stopped working
/// for the rest of the session.
///
/// PlayerCameraRig set Cursor.lockState once, in OnEnable, and never again.
/// Unity drops that lock on its own - on alt-tab, on clicking outside a
/// windowed player, on Escape in the Editor, and when the lock is requested
/// before the window has focus (which is exactly when OnEnable runs on the
/// first frame of Play). Nothing ever re-applied it.
///
/// These tests assert the rig's DECISION rather than Cursor.lockState itself.
/// In the Editor a lock is only honoured while the Game view is the focused
/// window, so a test runner that does not have focus can never observe
/// lockState becoming Locked - but it can observe that the rig keeps asking,
/// which is the part that was actually missing.
/// </summary>
public sealed class CursorLockTests
{
    private PlayerCameraRig rig;
    private PauseMenuController pause;

    [UnitySetUp]
    public IEnumerator LoadScene()
    {
        Time.timeScale = 1f;

        SceneManager.LoadScene("MuseumNight", LoadSceneMode.Single);
        yield return null;
        yield return null;

        rig = Object.FindFirstObjectByType<PlayerCameraRig>();
        Assert.IsNotNull(rig, "No PlayerCameraRig in MuseumNight.");

        pause = Object.FindFirstObjectByType<PauseMenuController>();
        Assert.IsNotNull(pause, "No PauseMenuController in MuseumNight.");
    }

    [UnityTearDown]
    public IEnumerator Cleanup()
    {
        if (pause != null && pause.IsPaused)
        {
            pause.Resume();
        }

        Time.timeScale = 1f;
        yield return null;
    }

    [UnityTest]
    public IEnumerator Gameplay_WantsTheCursorCaptured()
    {
        yield return null;

        Assert.IsTrue(
            rig.CursorCaptureWanted,
            "During normal play the rig should want the cursor captured, or " +
            "the mouse is free to leave the window and looking around breaks.");
    }

    /// <summary>
    /// The actual regression: something else releases the lock mid-play and
    /// the rig has to take it back on its own.
    /// </summary>
    [UnityTest]
    public IEnumerator CursorLock_IsReclaimedAfterSomethingElseReleasesIt()
    {
        yield return null;

        int before = rig.CursorRecaptureCount;

        // Exactly what alt-tab, or Escape in the Editor, does to the lock.
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        yield return null;
        yield return null;

        Assert.Greater(
            rig.CursorRecaptureCount, before,
            "The rig did not re-take the cursor after it was released. This is " +
            "the bug that let the mouse wander off the window permanently.");
    }

    /// <summary>
    /// The other half: the rig must NOT fight the pause menu, or the menu is
    /// visible with no cursor to click it.
    /// </summary>
    [UnityTest]
    public IEnumerator Pausing_ReleasesTheCursorAndResumingTakesItBack()
    {
        yield return null;

        typeof(PauseMenuController)
            .GetMethod("Pause", System.Reflection.BindingFlags.Instance |
                                System.Reflection.BindingFlags.NonPublic)
            .Invoke(pause, null);

        yield return null;

        Assert.IsFalse(
            rig.CursorCaptureWanted,
            "The rig kept claiming the cursor while paused, so the pause menu " +
            "cannot be clicked.");

        Assert.AreEqual(
            CursorLockMode.None, Cursor.lockState,
            "The cursor should be free while paused.");

        pause.Resume();
        yield return null;

        Assert.IsTrue(
            rig.CursorCaptureWanted,
            "Resuming should hand the cursor back to gameplay.");
    }

    /// <summary>
    /// The edge-recentring fallback must stay inert while the pointer is
    /// still, and this is the guard on a genuinely dangerous change.
    ///
    /// The fallback exists because a pointer that reaches the window edge
    /// stops producing movement - Mouse/delta goes to zero and the view stops
    /// turning part-way round, which is the reported symptom. It originally
    /// refused to run whenever lockState said Locked, which skipped the very
    /// case it was written for, since lockState is a request rather than a
    /// reading of what the OS is doing.
    ///
    /// Letting it run regardless introduces the opposite risk: what Unity
    /// reports for the pointer position while locked is not guaranteed to be
    /// the window centre, and a stale off-centre value would make it warp and
    /// suppress look on EVERY frame - which would not limit looking, it would
    /// disable it. Requiring the pointer to have actually moved is what makes
    /// that impossible, so it is worth a test rather than a comment.
    /// </summary>
    [UnityTest]
    public IEnumerator AStillPointerIsNeverWarpedSoLookIsNeverSuppressed()
    {
        var reader = Object.FindFirstObjectByType<PlayerInputReader>();
        Assert.IsNotNull(reader, "No PlayerInputReader in MuseumNight.");

        // Nothing is moving the mouse during a test run, so this is exactly
        // the "pointer held still" case.
        for (int i = 0; i < 30; i++)
        {
            yield return null;

            Assert.IsTrue(
                rig.CursorCaptureWanted,
                "Gameplay stopped wanting the cursor for no reason.");
        }

        // Feed a look value and confirm it survives a frame - if the fallback
        // were warping every frame it would be suppressed away to zero.
        reader.SetLookForTesting(new Vector2(3f, 0f));

        Assert.AreNotEqual(
            Vector2.zero, reader.LookInput,
            "Look input was suppressed while the pointer was completely " +
            "still, so the recentring fallback is firing when it should be " +
            "inert - the player would be unable to look at all.");

        yield return null;
    }
}
