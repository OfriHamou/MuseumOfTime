using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

/// <summary>
/// Automated play-mode checks for the MuseumNight scene.
///
/// These load the real scene and drive a virtual keyboard through the Input
/// System, so they verify the same path the player uses. Run headlessly with:
///
///   Unity.exe -batchmode -runTests -projectPath . \
///             -testPlatform PlayMode -testResults TestResults.xml -quit
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

    /// <summary>Holds a key down for a number of frames.</summary>
    private IEnumerator HoldKey(Key key, int frames)
    {
        InputSystem.QueueStateEvent(keyboard, new KeyboardState(key));
        InputSystem.Update();

        for (int i = 0; i < frames; i++)
        {
            yield return null;
        }
    }

    private IEnumerator ReleaseAllKeys()
    {
        InputSystem.QueueStateEvent(keyboard, new KeyboardState());
        InputSystem.Update();
        yield return null;
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
    // Input reaches the reader
    // -----------------------------------------------------------------

    [UnityTest]
    public IEnumerator PressingW_SetsForwardMoveInput()
    {
        PlayerInputReader reader = FindPlayer()
            .GetComponent<PlayerInputReader>();

        yield return HoldKey(Key.W, 3);

        Assert.Greater(
            reader.MoveInput.y,
            0.5f,
            "Holding W did not set MoveInput.y. Actual: " + reader.MoveInput);

        yield return ReleaseAllKeys();
    }

    [UnityTest]
    public IEnumerator ReleasingW_ReturnsMoveInputToZero()
    {
        PlayerInputReader reader = FindPlayer()
            .GetComponent<PlayerInputReader>();

        yield return HoldKey(Key.W, 3);
        yield return ReleaseAllKeys();
        yield return null;

        Assert.Less(
            reader.MoveInput.magnitude,
            0.01f,
            "MoveInput did not return to zero on key release. Actual: " +
            reader.MoveInput);
    }

    [UnityTest]
    public IEnumerator PressingShift_SetsRunning()
    {
        PlayerInputReader reader = FindPlayer()
            .GetComponent<PlayerInputReader>();

        yield return HoldKey(Key.LeftShift, 3);

        Assert.IsTrue(reader.IsRunning, "Left Shift did not set IsRunning.");

        yield return ReleaseAllKeys();
    }

    // -----------------------------------------------------------------
    // Input actually moves the player
    // -----------------------------------------------------------------

    [UnityTest]
    public IEnumerator HoldingW_MovesThePlayer()
    {
        GameObject player = FindPlayer();
        Vector3 start = player.transform.position;

        yield return HoldKey(Key.W, 30);

        float travelled = Vector3.Distance(start, player.transform.position);

        Assert.Greater(
            travelled,
            0.1f,
            "Player did not move while W was held. Start " + start +
            ", end " + player.transform.position);

        yield return ReleaseAllKeys();
    }

    [UnityTest]
    public IEnumerator HoldingD_MovesThePlayerSideways()
    {
        GameObject player = FindPlayer();
        Vector3 start = player.transform.position;

        yield return HoldKey(Key.D, 30);

        Assert.Greater(
            Vector3.Distance(start, player.transform.position),
            0.1f,
            "Player did not move while D was held.");

        yield return ReleaseAllKeys();
    }
}
