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
        yield return ResetPlayer();
        yield return ClimbsTheStaircase();
        yield return HingesSwing();
        yield return ScaleIsRealistic();

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

    /// <summary>
    /// Walks up the museum staircase. This is the real proof for the
    /// two-storey requirement: not that stairs exist, but that the
    /// CharacterController's step offset actually lets Noa climb them.
    /// </summary>
    private IEnumerator ClimbsTheStaircase()
    {
        GameObject stairs = GameObject.Find("Staircase");

        if (stairs == null)
        {
            Check("staircase exists", false, "no 'Staircase' object");
            yield break;
        }

        Check("staircase exists", true,
            stairs.transform.childCount + " steps and landings");

        // Put Noa at the foot of the stairs, facing along them (+Z).
        Transform firstStep = stairs.transform.Find("Step00");
        CharacterController cc = player.GetComponent<CharacterController>();

        cc.enabled = false;
        player.transform.position = firstStep.position + new Vector3(0f, 1.2f, -1.5f);
        player.transform.rotation = Quaternion.identity;
        cc.enabled = true;

        // Give Cinemachine time to catch up after the teleport: W is
        // camera-relative, so a lagging camera sends Noa the wrong way.
        yield return new WaitForSeconds(1.5f);

        Vector3 startPos = player.transform.position;
        float startY = startPos.y;
        Vector3 camFwd = Camera.main.transform.forward;

        Collider stepCollider = firstStep.GetComponent<Collider>();
        report.AppendLine("        (Step00 collider: " +
            (stepCollider == null ? "NONE" : stepCollider.GetType().Name +
             " enabled=" + stepCollider.enabled +
             " bounds=" + stepCollider.bounds) + ")");

        CharacterController ccInfo = player.GetComponent<CharacterController>();
        report.AppendLine("        (CC height=" + ccInfo.height +
            " radius=" + ccInfo.radius + " center=" + ccInfo.center +
            " stepOffset=" + ccInfo.stepOffset.ToString("0.00") +
            " feetY=" + (player.transform.position.y + ccInfo.center.y -
                         (ccInfo.height / 2f)).ToString("0.00") + ")");

        // Is anything actually solid where the stairs are?
        Vector3 probe = new Vector3(firstStep.position.x, 6f, -3.5f);
        if (Physics.Raycast(probe, Vector3.down, out RaycastHit hit, 20f))
        {
            report.AppendLine("        (raycast down over the stairs hit '" +
                hit.collider.name + "' on layer " + hit.collider.gameObject.layer +
                " at y=" + hit.point.y.ToString("0.00") + ")");
        }
        else
        {
            report.AppendLine("        (raycast down over the stairs hit NOTHING)");
        }

        report.AppendLine("        (player layer " + player.layer +
            ", collides with Default? " +
            !Physics.GetIgnoreLayerCollision(player.layer, 0) + ")");

        report.AppendLine("        (Step00 at " + firstStep.position +
                          ", player at " + startPos +
                          ", camera forward " + camFwd.ToString("0.00") + ")");

        // Drop Noa directly onto the middle of the ramp. If she rests there
        // the surface collides and the problem is step-up; if she falls to
        // floor level the surface is not colliding with the controller.
        cc.enabled = false;
        player.transform.position = new Vector3(firstStep.position.x, 4.5f, -3.5f);
        cc.enabled = true;
        yield return new WaitForSeconds(1.5f);

        report.AppendLine("        (dropped onto ramp mid-point, settled at y=" +
            player.transform.position.y.ToString("0.00") +
            ", grounded=" + cc.isGrounded + ")");

        // Back to the foot of the stairs for the real climb attempt.
        cc.enabled = false;
        player.transform.position = startPos;
        player.transform.rotation = Quaternion.identity;
        cc.enabled = true;
        yield return new WaitForSeconds(1.0f);

        // Track the highest point reached. Measuring only the final position
        // is wrong: she can climb correctly and then walk on past the top.
        float peakY = startY;

        // Sample the walk itself: where is she, and what is under her feet?
        for (int sample = 0; sample < 4; sample++)
        {
            yield return Hold(Key.W, 1f);

            Vector3 pos = player.transform.position;
            peakY = Mathf.Max(peakY, pos.y);
            string under = "nothing";

            if (Physics.Raycast(pos + Vector3.up, Vector3.down, out RaycastHit floor, 6f))
            {
                under = floor.collider.name + " @y=" + floor.point.y.ToString("0.00");
            }

            report.AppendLine("        (t=" + (sample + 1) + "s pos=" +
                pos.ToString("0.00") + " grounded=" + cc.isGrounded +
                " under=" + under + ")");
        }

        yield return Release();

        float climbed = peakY - startY;
        report.AppendLine("        (ended at " + player.transform.position +
                          ", moved " + (player.transform.position - startPos).ToString("0.00") + ")");

        Check(
            "Noa can climb the staircase",
            climbed > 2f,
            "rose " + climbed.ToString("0.00") + "m (from y " +
            startY.ToString("0.00") + " to a peak of " +
            peakY.ToString("0.00") + ")");

        Check(
            "reached the upper floor",
            peakY > 4.5f,
            "peak y = " + peakY.ToString("0.00") +
            ", upper slab is at y = 5");

        Check(
            "stays on the upper floor instead of falling off",
            player.transform.position.y > 4f,
            "y after walking on = " +
            player.transform.position.y.ToString("0.00"));
    }

    /// <summary>
    /// Confirms the hinge joints are real physics joints that move, rather
    /// than static props that merely have the component attached.
    /// </summary>
    private IEnumerator HingesSwing()
    {
        string[] names =
        {
            "ClockOfCreationPendulum", "GalleryGate", "ExhibitSignboard",
        };

        foreach (string name in names)
        {
            GameObject go = GameObject.Find(name);

            if (go == null)
            {
                Check(name + " exists", false, "not found in scene");
                continue;
            }

            HingeJoint joint = go.GetComponent<HingeJoint>();

            if (joint == null)
            {
                Check(name + " has a HingeJoint", false, "component missing");
                continue;
            }

            // Nudge it, rather than hoping it is still swinging on its own.
            // By this point in the run any starting motion has damped out, so
            // measuring passively would test nothing.
            var body = go.GetComponent<Rigidbody>();
            body.WakeUp();

            if (joint.useMotor)
            {
                // A motorised joint has usually already driven itself to its
                // limit and gone to sleep by now, so reverse it and watch it
                // travel back. That proves the motor and joint both work.
                JointMotor motor = joint.motor;
                motor.targetVelocity = -motor.targetVelocity;
                joint.motor = motor;
            }
            else
            {
                body.AddTorque(joint.axis.normalized * 40f, ForceMode.Impulse);
            }

            Quaternion before = go.transform.rotation;
            yield return new WaitForSeconds(0.8f);

            if (name == "GalleryGate")
            {
                report.AppendLine("        (gate angle=" +
                    joint.angle.ToString("0.0") +
                    " angVel=" + body.angularVelocity.ToString("0.00") +
                    " kinematic=" + body.isKinematic +
                    " sleeping=" + body.IsSleeping() +
                    " useMotor=" + joint.useMotor +
                    " mass=" + body.mass + ")");
            }
            float moved = Quaternion.Angle(before, go.transform.rotation);

            Check(
                name + " swings on its hinge",
                moved > 1f,
                "rotated " + moved.ToString("0.0") + " degrees after an " +
                "impulse, axis " + joint.axis + ", limits " +
                joint.limits.min + " to " + joint.limits.max);
        }
    }

    /// <summary>
    /// The brief judges scale explicitly, so the key dimensions are measured
    /// rather than eyeballed.
    /// </summary>
    private IEnumerator ScaleIsRealistic()
    {
        yield return null;

        GameObject stairs = GameObject.Find("Staircase");

        if (stairs != null)
        {
            Transform a = stairs.transform.Find("Step00");
            Transform b = stairs.transform.Find("Step01");

            if (a != null && b != null)
            {
                float rise = b.position.y - a.position.y;

                // Real staircases sit around 0.15-0.20m per step.
                Check(
                    "step rise is human-sized",
                    rise > 0.10f && rise < 0.22f,
                    rise.ToString("0.000") + "m per step");
            }
        }

        CharacterController cc = player.GetComponent<CharacterController>();

        Check(
            "Noa is roughly human height",
            cc.height > 1.4f && cc.height < 2.1f,
            "CharacterController height = " + cc.height.ToString("0.00") + "m");

        GameObject railing = GameObject.Find("RailEdge");
        if (railing != null)
        {
            float top = railing.transform.position.y +
                        (railing.transform.localScale.y / 2f);
            float deck = 5f;

            Check(
                "mezzanine railing is a safe height",
                top - deck > 0.9f && top - deck < 1.3f,
                (top - deck).ToString("0.00") + "m above the floor");
        }
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
