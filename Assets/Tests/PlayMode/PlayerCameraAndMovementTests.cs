using System.Collections;
using System.Reflection;
using NUnit.Framework;
using Unity.Cinemachine;
using NUnit.Framework.Constraints;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

/// <summary>
/// Play-mode checks for Phase 1: camera-relative movement, jumping, and the
/// first/third person camera switch.
///
/// Like the scene tests, these drive PlayerInputReader's fields directly
/// rather than simulating a keyboard, because batch mode runs unfocused and
/// the Input System resets devices every frame.
/// </summary>
public sealed class PlayerCameraAndMovementTests
{
    private GameObject player;
    private PlayerInputReader reader;
    private PlayerController controller;
    private PlayerCameraRig rig;

    [UnitySetUp]
    public IEnumerator LoadScene()
    {
        SceneManager.LoadScene("MuseumNight", LoadSceneMode.Single);
        yield return null;
        yield return null;

        player = GameObject.Find("Player");
        Assert.IsNotNull(player, "No 'Player' object in MuseumNight.");

        reader = player.GetComponent<PlayerInputReader>();
        controller = player.GetComponent<PlayerController>();
        rig = player.GetComponent<PlayerCameraRig>();

        // Let Cinemachine position the live camera before anything is measured.
        for (int i = 0; i < 5; i++)
        {
            yield return null;
        }
    }

    private static void SetPrivate(object target, string field, object value)
    {
        FieldInfo info = target.GetType().GetField(
            field,
            BindingFlags.Instance | BindingFlags.NonPublic);

        Assert.IsNotNull(info, "Field '" + field + "' not found.");
        info.SetValue(target, value);
    }

    // -----------------------------------------------------------------
    // Camera rig structure  (requirement: FPS/TPS switch, two cameras)
    // -----------------------------------------------------------------

    [Test]
    public void MainCamera_HasACinemachineBrain()
    {
        Assert.IsNotNull(Camera.main, "There is no camera tagged MainCamera.");

        Assert.IsNotNull(
            Camera.main.GetComponent<CinemachineBrain>(),
            "MainCamera has no CinemachineBrain, so Cinemachine cannot " +
            "drive it and the camera switch will do nothing.");
    }

    [Test]
    public void Scene_HasTwoGameplayCinemachineCameras()
    {
        CinemachineCamera[] cams = Object.FindObjectsByType<CinemachineCamera>(
            FindObjectsSortMode.None);

        // The requirement asks for two cameras besides the minimap.
        Assert.GreaterOrEqual(
            cams.Length,
            2,
            "Expected at least two Cinemachine cameras, found " + cams.Length);
    }

    [Test]
    public void Player_HasTheCameraRigWired()
    {
        Assert.IsNotNull(rig, "Player has no PlayerCameraRig component.");

        foreach (string field in
                 new[] { "firstPersonCamera", "thirdPersonCamera", "cameraPivot" })
        {
            FieldInfo info = typeof(PlayerCameraRig).GetField(
                field,
                BindingFlags.Instance | BindingFlags.NonPublic);

            Assert.IsNotNull(info, "Field '" + field + "' not found.");
            Assert.IsNotNull(
                info.GetValue(rig),
                "PlayerCameraRig." + field + " is not assigned. Run " +
                "Museum of Time > Build Camera Rig in MuseumNight.");
        }
    }

    [Test]
    public void CameraPivot_SitsAtHeadHeight()
    {
        Transform pivot = player.transform.Find("CameraPivot");
        Assert.IsNotNull(pivot, "Player has no CameraPivot child.");

        // Noa is about 1.7m, so eye level should be near 1.6m.
        Assert.That(
            pivot.localPosition.y,
            Is.EqualTo(1.6f).Within(0.3f),
            "CameraPivot is not at head height: " + pivot.localPosition);
    }

    [UnityTest]
    public IEnumerator CameraToggle_SwitchesBetweenFirstAndThirdPerson()
    {
        bool startedFirstPerson = rig.IsFirstPerson;

        rig.ToggleCamera();
        yield return null;

        Assert.AreNotEqual(
            startedFirstPerson,
            rig.IsFirstPerson,
            "ToggleCamera did not change which camera is live.");

        rig.ToggleCamera();
        yield return null;

        Assert.AreEqual(
            startedFirstPerson,
            rig.IsFirstPerson,
            "Toggling twice did not return to the original camera.");
    }

    // -----------------------------------------------------------------
    // Camera-relative movement  (the Step 1.1 bug)
    // -----------------------------------------------------------------

    [UnityTest]
    public IEnumerator ForwardInput_MovesAlongCameraForwardNotWorldZ()
    {
        // Turn the player a quarter turn. The camera follows, so "forward"
        // is now world +X. The old code moved along world +Z regardless.
        player.transform.rotation = Quaternion.Euler(0f, 90f, 0f);

        for (int i = 0; i < 10; i++)
        {
            yield return null;
        }

        Vector3 start = player.transform.position;

        for (int i = 0; i < 150; i++)
        {
            SetPrivate(reader, "moveInput", Vector2.up);
            yield return null;
        }

        Vector3 delta = player.transform.position - start;
        delta.y = 0f;

        Assert.Greater(
            delta.magnitude,
            0.005f,
            "Player did not move at all.");

        Assert.Greater(
            Mathf.Abs(delta.x),
            Mathf.Abs(delta.z),
            "Movement was not camera-relative. After turning 90 degrees, " +
            "forward should be mostly along X but the delta was " + delta);
    }

    [UnityTest]
    public IEnumerator MovementDirection_FollowsTheCameraAfterTurning()
    {
        Vector3 forwardBefore = MeasureMoveDirection();
        yield return null;

        player.transform.rotation = Quaternion.Euler(0f, 180f, 0f);
        for (int i = 0; i < 10; i++)
        {
            yield return null;
        }

        Vector3 startB = player.transform.position;
        for (int i = 0; i < 150; i++)
        {
            SetPrivate(reader, "moveInput", Vector2.up);
            yield return null;
        }

        Vector3 forwardAfter = player.transform.position - startB;
        forwardAfter.y = 0f;

        // Turning 180 degrees must reverse the direction of travel.
        float dot = Vector3.Dot(
            forwardBefore.normalized,
            forwardAfter.normalized);

        Assert.Less(
            dot,
            0f,
            "After turning 180 degrees the player moved the same way as " +
            "before, so movement is not following the camera. dot=" + dot);
    }

    private Vector3 MeasureMoveDirection()
    {
        Vector3 start = player.transform.position;

        for (int i = 0; i < 60; i++)
        {
            SetPrivate(reader, "moveInput", Vector2.up);
            controller.SendMessage(
                "Update",
                SendMessageOptions.DontRequireReceiver);
        }

        Vector3 delta = player.transform.position - start;
        delta.y = 0f;
        return delta;
    }

    // -----------------------------------------------------------------
    // Jumping  (never worked before: Space was wired to OnRun)
    // -----------------------------------------------------------------

    [UnityTest]
    public IEnumerator Jump_RaisesThePlayerOffTheGround()
    {
        // Wait for a genuine grounded state rather than assuming a fixed
        // frame count always settles the CharacterController. Triggering
        // the jump before isGrounded is actually true makes
        // PlayerController.CanJump() reject it (grounded is false and the
        // coyote-time window has already expired since spawn), so the test
        // was measuring a jump that silently never fired - "ground and
        // peak Y were identical" is exactly that, not a height problem.
        yield return PlayModePhysicsWait.UntilGrounded(player.GetComponent<CharacterController>());

        float groundY = player.transform.position.y;

        // Unity's order each frame is Update -> coroutine resume -> LateUpdate.
        // A coroutine therefore sets jumpPressed AFTER Update has run, and the
        // reader's LateUpdate clears it again before the next Update sees it.
        // Disabling the reader stops LateUpdate from firing while leaving the
        // JumpPressed property readable by PlayerController.
        reader.enabled = false;
        SetPrivate(reader, "jumpPressed", true);

        yield return null;

        SetPrivate(reader, "jumpPressed", false);
        reader.enabled = true;

        float peak = groundY;
        for (int i = 0; i < 120; i++)
        {
            peak = Mathf.Max(peak, player.transform.position.y);
            yield return null;
        }

        Assert.Greater(
            peak,
            groundY + 0.05f,
            "Jump did not lift the player. Ground " + groundY +
            ", peak " + peak);
    }

    [UnityTest]
    public IEnumerator Gravity_PullsThePlayerBackDown()
    {
        for (int i = 0; i < 60; i++)
        {
            yield return null;
        }

        float groundY = player.transform.position.y;

        // Drop the player from above and check it comes back down.
        player.transform.position += Vector3.up * 3f;

        for (int i = 0; i < 400; i++)
        {
            yield return null;
        }

        Assert.Less(
            player.transform.position.y,
            groundY + 3f,
            "Player did not fall back down under gravity.");
    }
}
