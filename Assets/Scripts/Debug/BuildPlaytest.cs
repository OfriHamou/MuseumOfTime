#if DEVELOPMENT_BUILD || UNITY_EDITOR
using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;

/// <summary>
/// Drives the real, built game with real keyboard input and writes a report.
///
/// This exists so the game can be play-tested from the command line instead of
/// by taking screenshots of the editor. It only runs when the executable is
/// started with -playtest, and the whole file is compiled out of a release
/// build, so it can never affect the submitted game.
///
///   MuseumOfTime.exe -playtest -logFile playtest.log
///
/// Unlike the batch-mode tests, this is a genuine end-to-end check: a real
/// player window, the real Input System, and real key presses going through
/// PlayerInput to PlayerInputReader to PlayerController.
/// </summary>
public sealed class BuildPlaytest : MonoBehaviour
{
    private const string ReportPath = "playtest-report.txt";

    private readonly StringBuilder report = new StringBuilder();

    private Keyboard keyboard;
    private GameObject player;
    private PlayerController controller;
    private PlayerInputReader reader;
    private PlayerCameraRig rig;
    private Animator animator;

    private int passed;
    private int failed;
    private Vector3 spawn;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        bool requested = false;

        foreach (string arg in System.Environment.GetCommandLineArgs())
        {
            if (arg == "-playtest")
            {
                requested = true;
                break;
            }
        }

        if (!requested)
        {
            return;
        }

        var host = new GameObject("BuildPlaytest");
        DontDestroyOnLoad(host);
        host.AddComponent<BuildPlaytest>();
    }

    private IEnumerator Start()
    {
        // The harness window is not always focused when launched from a
        // script, and the Input System would otherwise reset devices.
        Application.runInBackground = true;
        InputSystem.settings.backgroundBehavior =
            InputSettings.BackgroundBehavior.IgnoreFocus;

        keyboard = InputSystem.GetDevice<Keyboard>()
                   ?? InputSystem.AddDevice<Keyboard>();

        report.AppendLine("===== MUSEUM OF TIME PLAYTEST =====");
        report.AppendLine("boot scene : " +
            SceneManager.GetActiveScene().name);
        report.AppendLine("focused    : " + Application.isFocused);

        // The build boots into MainMenu, which is correct. Load the gameplay
        // scene the way the menu's New Game button will.
        SceneManager.LoadScene("MuseumNight", LoadSceneMode.Single);
        yield return null;
        yield return new WaitForSeconds(1f);

        report.AppendLine("test scene : " +
            SceneManager.GetActiveScene().name);
        report.AppendLine();

        if (!ResolvePlayer())
        {
            Finish();
            yield break;
        }

        yield return RendersSomething();
        yield return ResetPlayer();
        yield return WalkForward();
        yield return ResetPlayer();
        yield return RunIsFaster();
        yield return ResetPlayer();
        yield return Jumps();
        yield return ResetPlayer();
        yield return CameraToggles();
        yield return ResetPlayer();
        yield return AnimatorReacts();

        Finish();
    }

    private bool ResolvePlayer()
    {
        player = GameObject.Find("Player");

        if (player == null)
        {
            Check("player exists", false, "no GameObject named 'Player'");
            return false;
        }

        controller = player.GetComponent<PlayerController>();
        reader = player.GetComponent<PlayerInputReader>();
        rig = player.GetComponent<PlayerCameraRig>();
        animator = player.GetComponent<Animator>();

        spawn = player.transform.position;
        Check("player exists", true, "found at " + spawn);
        return true;
    }

    // ---------------- individual checks ----------------

    private IEnumerator RendersSomething()
    {
        yield return null;

        Camera cam = Camera.main;

        Check(
            "a camera is rendering",
            cam != null && cam.isActiveAndEnabled,
            cam == null ? "Camera.main is null" : "MainCamera active, fov " +
                cam.fieldOfView.ToString("0.#"));
    }

    private IEnumerator WalkForward()
    {
        Vector3 start = player.transform.position;

        yield return Hold(Key.W, 1.0f);

        Vector3 moved = player.transform.position - start;
        moved.y = 0f;

        Check(
            "W walks forward",
            moved.magnitude > 0.5f,
            "travelled " + moved.magnitude.ToString("0.00") + "m, delta " +
            moved.ToString("0.00"));

        // Compare travel direction against where the camera was looking.
        Vector3 camForward = Camera.main.transform.forward;
        camForward.y = 0f;

        float alignment = Vector3.Dot(
            moved.normalized,
            camForward.normalized);

        Check(
            "movement is camera-relative",
            alignment > 0.8f,
            "dot(travel, camera forward) = " + alignment.ToString("0.00"));

        yield return Release();
    }

    private IEnumerator RunIsFaster()
    {
        Vector3 start = player.transform.position;
        yield return Hold(0.8f, Key.W);
        float walked = Flat(player.transform.position - start);
        yield return Release();

        start = player.transform.position;
        yield return Hold(0.8f, Key.W, Key.LeftShift);
        float ran = Flat(player.transform.position - start);
        yield return Release();

        Check(
            "Shift runs faster than walking",
            ran > walked * 1.2f,
            "walk " + walked.ToString("0.00") + "m vs run " +
            ran.ToString("0.00") + "m");
    }

    private IEnumerator Jumps()
    {
        // Let the player settle onto the floor first.
        yield return new WaitForSeconds(0.5f);

        float ground = player.transform.position.y;
        bool groundedBefore = controller.IsGrounded;

        yield return Hold(Key.Space, 0.1f);
        yield return Release();

        float peak = ground;
        float t = 0f;
        while (t < 1.2f)
        {
            peak = Mathf.Max(peak, player.transform.position.y);
            t += Time.deltaTime;
            yield return null;
        }

        Check(
            "still on the ground after walking",
            player.transform.position.y > spawn.y - 1f,
            "y = " + player.transform.position.y.ToString("0.00") +
            " (spawn y " + spawn.y.ToString("0.00") + ")");

        Check(
            "grounded before jumping",
            groundedBefore,
            "isGrounded = " + groundedBefore);

        Check(
            "Space jumps",
            peak > ground + 0.3f,
            "ground y " + ground.ToString("0.00") + ", peak y " +
            peak.ToString("0.00") + " (rose " +
            (peak - ground).ToString("0.00") + "m)");

        yield return new WaitForSeconds(1f);

        Check(
            "lands again after jumping",
            controller.IsGrounded,
            "isGrounded = " + controller.IsGrounded);
    }

    private IEnumerator CameraToggles()
    {
        bool before = rig.IsFirstPerson;

        yield return Hold(Key.C, 0.1f);
        yield return Release();
        yield return new WaitForSeconds(0.6f);

        bool after = rig.IsFirstPerson;

        Check(
            "C switches camera",
            before != after,
            (before ? "first" : "third") + " person -> " +
            (after ? "first" : "third") + " person");

        yield return Hold(Key.C, 0.1f);
        yield return Release();
        yield return new WaitForSeconds(0.6f);

        Check(
            "C switches back",
            rig.IsFirstPerson == before,
            "back to " + (rig.IsFirstPerson ? "first" : "third") + " person");
    }

    private IEnumerator AnimatorReacts()
    {
        if (animator == null || animator.runtimeAnimatorController == null)
        {
            Check("animator present", false, "no Animator or no controller");
            yield break;
        }

        Check(
            "animator uses NoaController",
            animator.runtimeAnimatorController.name == "NoaController",
            "controller = " + animator.runtimeAnimatorController.name);

        yield return new WaitForSeconds(0.4f);
        float idleSpeed = animator.GetFloat("Speed");

        yield return Hold(Key.W, 0.6f);
        float movingSpeed = animator.GetFloat("Speed");
        string stateName = animator.GetCurrentAnimatorStateInfo(0).IsName("Walk")
            ? "Walk"
            : animator.GetCurrentAnimatorStateInfo(0).IsName("Run")
                ? "Run"
                : "other";
        yield return Release();

        Check(
            "Speed parameter follows movement",
            idleSpeed < 0.1f && movingSpeed > 1f,
            "idle " + idleSpeed.ToString("0.00") + " -> moving " +
            movingSpeed.ToString("0.00"));

        Check(
            "animator leaves Idle while walking",
            stateName == "Walk" || stateName == "Run",
            "state while moving = " + stateName);
    }

    // ---------------- plumbing ----------------

    /// <summary>
    /// Returns the player to the spawn point so each check starts from the
    /// same place. The CharacterController has to be switched off first: it
    /// owns the transform and would otherwise overwrite the teleport.
    /// </summary>
    private IEnumerator ResetPlayer()
    {
        CharacterController cc = player.GetComponent<CharacterController>();
        cc.enabled = false;
        player.transform.position = spawn;
        player.transform.rotation = Quaternion.identity;
        cc.enabled = true;

        yield return Release();
        yield return new WaitForSeconds(0.4f);
    }

    private static float Flat(Vector3 v)
    {
        v.y = 0f;
        return v.magnitude;
    }

    private IEnumerator Hold(Key key, float seconds)
    {
        return Hold(seconds, key);
    }

    /// <summary>
    /// Holds keys down for a period, re-sending the state each frame so the
    /// press survives regardless of how the runtime treats device resets.
    /// </summary>
    private IEnumerator Hold(float seconds, params Key[] keys)
    {
        float t = 0f;

        while (t < seconds)
        {
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(keys));
            t += Time.deltaTime;
            yield return null;
        }
    }

    private IEnumerator Release()
    {
        InputSystem.QueueStateEvent(keyboard, new KeyboardState());
        yield return null;
        yield return null;
    }

    private void Check(string name, bool ok, string detail)
    {
        if (ok)
        {
            passed++;
        }
        else
        {
            failed++;
        }

        report.AppendLine((ok ? "  PASS  " : "  FAIL  ") + name + "   [" +
                          detail + "]");
    }

    private void Finish()
    {
        report.AppendLine();
        report.AppendLine("PASSED " + passed + "   FAILED " + failed);
        report.AppendLine("===================================");

        string text = report.ToString();
        Debug.Log(text);

        try
        {
            System.IO.File.WriteAllText(ReportPath, text);
        }
        catch (System.Exception e)
        {
            Debug.LogWarning("Could not write report: " + e.Message);
        }

        Application.Quit(failed == 0 ? 0 : 1);
    }
}
#endif
