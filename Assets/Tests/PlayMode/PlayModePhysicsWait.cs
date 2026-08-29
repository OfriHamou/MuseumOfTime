using System.Collections;
using NUnit.Framework;
using UnityEngine;

/// <summary>
/// Shared PlayMode-test helper: wait for a CharacterController to reach a
/// genuinely grounded state instead of assuming a fixed frame count always
/// settles it.
///
/// CharacterController.isGrounded is only ever updated by a Move() call -
/// PlayerController makes that call from Update(), not FixedUpdate - so a
/// fixed "wait N rendered frames" is at the mercy of how long those frames
/// actually took (scene load spikes, machine load in headless CI). Polling
/// once per physics step instead lets as many Update/Move calls interleave
/// as they need to, and still converges well inside the timeout regardless
/// of frame timing.
/// </summary>
internal static class PlayModePhysicsWait
{
    private const int DefaultTimeoutFixedFrames = 300; // ~5s at the default 50Hz fixed timestep

    /// <summary>
    /// Polls once per FixedUpdate until <paramref name="controller"/> reports
    /// grounded, or fails the test if that never happens within the timeout.
    /// </summary>
    public static IEnumerator UntilGrounded(
        CharacterController controller,
        int timeoutFixedFrames = DefaultTimeoutFixedFrames)
    {
        int frames = 0;

        while (!controller.isGrounded && frames < timeoutFixedFrames)
        {
            yield return new WaitForFixedUpdate();
            frames++;
        }

        Assert.IsTrue(
            controller.isGrounded,
            "CharacterController never reported isGrounded within " +
            timeoutFixedFrames + " physics frames (~" +
            (timeoutFixedFrames * Time.fixedDeltaTime).ToString("0.0") +
            "s) - a real grounding regression, not just slow settling.");
    }
}
