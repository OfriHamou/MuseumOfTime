using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

/// <summary>
/// Automated play-mode checks for the MuseumNight scene. Run headlessly with:
///
///   Unity.exe -batchmode -runTests -projectPath . ^
///             -testPlatform PlayMode -testResults TestResults.xml
///
/// Two different techniques are used on purpose, because batch mode runs the
/// player unfocused and the Input System's default backgroundBehavior
/// (ResetAndDisableNonBackgroundDevices) wipes device state at the start of
/// every frame. A simulated key therefore cannot stay held across frames
/// without changing a project setting that would also change how the shipped
/// game behaves.
///
///  - Input path (device -> action -> reader) is asserted within a single
///    input update, which is unaffected by the reset.
///  - Movement path (reader -> controller -> transform) sets the reader's
///    value directly and then lets real frames run, so it needs no device.
///
/// Together they cover the same ground as holding a key down would.
/// </summary>
public sealed class MuseumNightSceneTests
{
    private const string SceneName = "MuseumNight";

    private Keyboard keyboard;

    [UnitySetUp]
    public IEnumerator LoadScene()
    {
        keyboard = InputSystem.AddDevice<Keyboard>();

        SceneManager.LoadScene(SceneName, LoadSceneMode.Single);

        // One frame to load, one for Awake/OnEnable/Start to have all run.
        yield return null;
        yield return null;
    }

    [UnityTearDown]
    public IEnumerator Cleanup()
    {
        if (keyboard != null && keyboard.added)
        {
            InputSystem.RemoveDevice(keyboard);
        }

        yield return null;
    }

    private static GameObject FindPlayer()
    {
        GameObject player = GameObject.Find("Player");
        Assert.IsNotNull(player, "No GameObject named 'Player' in the scene.");
        return player;
    }

    /// <summary>
    /// Presses keys and flushes them through the Input System immediately.
    /// Callbacks fire inside this call, so read the result straight after.
    /// </summary>
    /// <summary>
    /// Simulated key presses only survive when the player has focus. Batch
    /// mode runs unfocused and the Input System's default backgroundBehavior
    /// (ResetAndDisableNonBackgroundDevices) wipes device state every frame,
    /// so these are skipped there and run from the Editor's Test Runner.
    /// </summary>
    private static void RequireFocusForDeviceInput()
    {
        if (!Application.isFocused)
        {
            Assert.Ignore(
                "Needs a focused player: batch mode resets input devices " +
                "every frame. Run this from the Editor Test Runner window.");
        }
    }

    private void PressAndFlush(params Key[] keys)
    {
        // Batch mode runs unfocused, and the default backgroundBehavior
        // (ResetAndDisableNonBackgroundDevices) resets and disables the
        // keyboard once a frame boundary passes. Replacing the device
        // immediately before injecting keeps the press and the read inside
        // the same input update, which the reset cannot interfere with.
        if (!keyboard.enabled)
        {
            InputSystem.EnableDevice(keyboard);
        }

        InputSystem.QueueStateEvent(keyboard, new KeyboardState(keys));
        InputSystem.Update();
    }

    /// <summary>
    /// Writes straight to the reader's backing field, standing in for a key
    /// that is being held. Used by the movement tests so they do not depend
    /// on device state surviving a frame boundary.
    /// </summary>
    private static void ForceMoveInput(PlayerInputReader reader, Vector2 value)
    {
        FieldInfo field = typeof(PlayerInputReader).GetField(
            "moveInput",
            BindingFlags.Instance | BindingFlags.NonPublic);

        Assert.IsNotNull(field, "PlayerInputReader.moveInput field not found.");
        field.SetValue(reader, value);
    }

    // -----------------------------------------------------------------
    // Scene wiring
    // -----------------------------------------------------------------

    [Test]
    public void Player_HasTheRequiredComponents()
    {
        GameObject player = FindPlayer();

        Assert.IsNotNull(
            player.GetComponent<CharacterController>(),
            "Player has no CharacterController, so it cannot move.");

        Assert.IsNotNull(
            player.GetComponent<PlayerInputReader>(),
            "Player has no PlayerInputReader.");

        Assert.IsNotNull(
            player.GetComponent<PlayerController>(),
            "Player has no PlayerController.");

        Assert.IsNotNull(
            player.GetComponent<PlayerInput>(),
            "Player has no PlayerInput.");
    }

    [Test]
    public void Scene_HasExactlyOnePlayerInput()
    {
        PlayerInput[] all = Object.FindObjectsByType<PlayerInput>(
            FindObjectsSortMode.None);

        // Unity treats every PlayerInput as a separate joined player. Two of
        // them fight over the keyboard and one silently receives nothing.
        Assert.AreEqual(
            1,
            all.Length,
            "Expected one PlayerInput in the scene, found " + all.Length +
            ". Extra test objects must be deleted.");
    }

    [Test]
    public void ActionsAsset_ContainsEveryActionTheDesignNeeds()
    {
        PlayerInput playerInput = FindPlayer().GetComponent<PlayerInput>();

        Assert.IsNotNull(
            playerInput.actions,
            "PlayerInput has no Actions asset assigned.");

        InputActionMap map = playerInput.actions.FindActionMap("Player");
        Assert.IsNotNull(map, "MuseumInputActions has no 'Player' action map.");

        string[] expected =
        {
            "Move", "Look", "Jump", "Run", "Interact", "Shoot",
            "SlowTime", "CameraToggle", "Pause",
            "EraBack", "EraForward", "Journal",
        };

        foreach (string name in expected)
        {
            Assert.IsNotNull(
                map.FindAction(name),
                "Action '" + name + "' is missing from the Player map.");
        }

        Assert.IsNotNull(
            playerInput.actions.FindActionMap("UI"),
            "MuseumInputActions has no 'UI' map, so menus will not respond.");
    }

    [Test]
    public void PlayerMap_IsEnabledAndMoveIsBoundToWASD()
    {
        PlayerInput playerInput = FindPlayer().GetComponent<PlayerInput>();
        InputActionMap map = playerInput.actions.FindActionMap("Player");

        Assert.IsTrue(map.enabled, "The Player action map is not enabled.");

        InputAction move = map.FindAction("Move");

        // Assert on WHICH controls are bound, not how many. The count is not
        // environment-independent: this fixture adds its own Keyboard, so in
        // the Editor (where a real keyboard is already present) every binding
        // resolves once per device and the count doubles. The requirement is
        // that WASD drives Move - that is what is checked.
        var boundKeys = new System.Collections.Generic.HashSet<string>();
        foreach (var control in move.controls)
        {
            boundKeys.Add(control.name.ToLowerInvariant());
        }

        foreach (string key in new[] { "w", "a", "s", "d" })
        {
            Assert.IsTrue(
                boundKeys.Contains(key),
                "Move should be driven by WASD, but '" + key + "' is not bound. " +
                "Bound controls: " + string.Join(", ", boundKeys) + ".");
        }
    }

    [Test]
    public void Scene_HasACameraThatRenders()
    {
        Camera[] cameras = Object.FindObjectsByType<Camera>(
            FindObjectsSortMode.None);

        int enabled = 0;
        foreach (Camera cam in cameras)
        {
            if (cam.isActiveAndEnabled)
            {
                enabled++;
            }
        }

        Assert.Greater(
            enabled,
            0,
            "No enabled Camera in the scene - the Game view shows " +
            "'No cameras rendering' and the player sees nothing.");
    }

    // -----------------------------------------------------------------
    // Input path: device -> action -> reader
    // -----------------------------------------------------------------

    [Test]
    public void PressingW_SetsForwardMoveInput()
    {
        RequireFocusForDeviceInput();
        PlayerInputReader reader = FindPlayer()
            .GetComponent<PlayerInputReader>();

        PressAndFlush(Key.W);

        Assert.Greater(
            reader.MoveInput.y,
            0.5f,
            "Pressing W did not set MoveInput.y. Actual: " + reader.MoveInput);
    }

    [Test]
    public void PressingD_SetsSidewaysMoveInput()
    {
        RequireFocusForDeviceInput();
        PlayerInputReader reader = FindPlayer()
            .GetComponent<PlayerInputReader>();

        PressAndFlush(Key.D);

        Assert.Greater(
            reader.MoveInput.x,
            0.5f,
            "Pressing D did not set MoveInput.x. Actual: " + reader.MoveInput);
    }

    [Test]
    public void ReleasingW_ReturnsMoveInputToZero()
    {
        RequireFocusForDeviceInput();
        PlayerInputReader reader = FindPlayer()
            .GetComponent<PlayerInputReader>();

        PressAndFlush(Key.W);
        Assert.Greater(reader.MoveInput.y, 0.5f, "W was not registered.");

        PressAndFlush();

        Assert.Less(
            reader.MoveInput.magnitude,
            0.01f,
            "MoveInput did not return to zero on release. Actual: " +
            reader.MoveInput);
    }

    [Test]
    public void PressingShift_SetsRunning()
    {
        RequireFocusForDeviceInput();
        PlayerInputReader reader = FindPlayer()
            .GetComponent<PlayerInputReader>();

        PressAndFlush(Key.LeftShift);

        Assert.IsTrue(reader.IsRunning, "Left Shift did not set IsRunning.");
    }

    [Test]
    public void PressingQRTab_FireTheActionsAddedInPhase0()
    {
        RequireFocusForDeviceInput();
        PlayerInputReader reader = FindPlayer()
            .GetComponent<PlayerInputReader>();

        // These three had no Inspector wiring at all, so they only work
        // because the reader subscribes in code.
        PressAndFlush(Key.Q);
        Assert.IsTrue(reader.EraBackPressed, "Q did not fire EraBack.");

        PressAndFlush();
        PressAndFlush(Key.R);
        Assert.IsTrue(reader.EraForwardPressed, "R did not fire EraForward.");

        PressAndFlush();
        PressAndFlush(Key.Tab);
        Assert.IsTrue(reader.JournalPressed, "Tab did not fire Journal.");
    }

    [UnityTest]
    public IEnumerator OneShotFlags_ClearAfterASingleFrame()
    {
        RequireFocusForDeviceInput();
        PlayerInputReader reader = FindPlayer()
            .GetComponent<PlayerInputReader>();

        PressAndFlush(Key.Q);
        Assert.IsTrue(reader.EraBackPressed, "Q did not fire EraBack.");

        // LateUpdate must clear it, so one press is never read twice.
        yield return null;

        Assert.IsFalse(
            reader.EraBackPressed,
            "EraBackPressed stayed true for more than one frame.");
    }

    // -----------------------------------------------------------------
    // Movement path: reader -> controller -> transform
    // -----------------------------------------------------------------

    [UnityTest]
    public IEnumerator ForwardInput_MovesThePlayer()
    {
        GameObject player = FindPlayer();
        PlayerInputReader reader = player.GetComponent<PlayerInputReader>();

        Vector3 start = player.transform.position;

        for (int i = 0; i < 150; i++)
        {
            ForceMoveInput(reader, Vector2.up);
            yield return null;
        }

        Assert.Greater(
            Vector3.Distance(start, player.transform.position),
            0.005f,
            "Player did not move with forward input. Start " + start +
            ", end " + player.transform.position);
    }

    [UnityTest]
    public IEnumerator SidewaysInput_MovesThePlayer()
    {
        GameObject player = FindPlayer();
        PlayerInputReader reader = player.GetComponent<PlayerInputReader>();

        Vector3 start = player.transform.position;

        for (int i = 0; i < 150; i++)
        {
            ForceMoveInput(reader, Vector2.right);
            yield return null;
        }

        Assert.Greater(
            Vector3.Distance(start, player.transform.position),
            0.005f,
            "Player did not move with sideways input.");
    }

    [UnityTest]
    public IEnumerator NoInput_LeavesThePlayerStill()
    {
        GameObject player = FindPlayer();
        PlayerInputReader reader = player.GetComponent<PlayerInputReader>();

        // Let gravity settle first, then measure only horizontal drift.
        for (int i = 0; i < 10; i++)
        {
            yield return null;
        }

        Vector3 start = player.transform.position;

        for (int i = 0; i < 150; i++)
        {
            ForceMoveInput(reader, Vector2.zero);
            yield return null;
        }

        Vector3 end = player.transform.position;
        float horizontal = Vector2.Distance(
            new Vector2(start.x, start.z),
            new Vector2(end.x, end.z));

        Assert.Less(
            horizontal,
            0.001f,
            "Player drifted horizontally with no input. Moved " + horizontal);
    }
}
