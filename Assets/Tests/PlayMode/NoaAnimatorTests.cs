using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

/// <summary>
/// Play-mode checks for Step 1.3: Noa's Animator.
///
/// The requirement is an Animator defined by us, not imported, with at least
/// four states. These verify the controller's shape and that it is actually
/// driven by movement rather than sitting inert on the object.
/// </summary>
public sealed class NoaAnimatorTests
{
    private GameObject player;
    private Animator animator;
    private PlayerInputReader reader;

    [UnitySetUp]
    public IEnumerator LoadScene()
    {
        SceneManager.LoadScene("MuseumNight", LoadSceneMode.Single);
        yield return null;
        yield return null;

        player = GameObject.Find("Player");
        Assert.IsNotNull(player, "No 'Player' object in MuseumNight.");

        animator = player.GetComponent<Animator>();
        reader = player.GetComponent<PlayerInputReader>();
    }

    private static void SetPrivate(object target, string field, object value)
    {
        FieldInfo info = target.GetType().GetField(
            field,
            BindingFlags.Instance | BindingFlags.NonPublic);

        Assert.IsNotNull(info, "Field '" + field + "' not found.");
        info.SetValue(target, value);
    }

    [Test]
    public void Player_HasAnAnimatorWithOurController()
    {
        Assert.IsNotNull(animator, "Player has no Animator component.");

        Assert.IsNotNull(
            animator.runtimeAnimatorController,
            "Animator has no controller assigned. Run " +
            "Museum of Time > Build Noa Animator Controller.");

        Assert.AreEqual(
            "NoaController",
            animator.runtimeAnimatorController.name,
            "The Animator is not using the controller we authored.");
    }

    [Test]
    public void Controller_HasAtLeastFourStates()
    {
        // The requirement is four; the controller ships six so that losing
        // one during later edits still leaves the requirement satisfied.
        var controller = animator.runtimeAnimatorController;

        Assert.GreaterOrEqual(
            controller.animationClips.Length,
            4,
            "Expected at least four states with motion, found " +
            controller.animationClips.Length);
    }

    [Test]
    public void Controller_HasTheParametersTheDriverSets()
    {
        string[] expected =
        {
            "Speed", "IsGrounded", "JumpTrigger", "InteractTrigger",
        };

        foreach (string name in expected)
        {
            bool found = false;

            foreach (AnimatorControllerParameter p in animator.parameters)
            {
                if (p.name == name)
                {
                    found = true;
                    break;
                }
            }

            Assert.IsTrue(found, "Animator parameter '" + name + "' missing.");
        }
    }

    [Test]
    public void Player_HasTheAnimatorDriver()
    {
        Assert.IsNotNull(
            player.GetComponent<PlayerAnimatorDriver>(),
            "Player has no PlayerAnimatorDriver, so the Animator would " +
            "never receive any parameter values.");
    }

    [UnityTest]
    public IEnumerator Animator_StartsInIdle()
    {
        yield return null;

        AnimatorStateInfo state = animator.GetCurrentAnimatorStateInfo(0);

        Assert.IsTrue(
            state.IsName("Idle"),
            "The default state should be Idle.");
    }

    [UnityTest]
    public IEnumerator Moving_RaisesTheSpeedParameter()
    {
        Assert.AreEqual(
            0f,
            animator.GetFloat("Speed"),
            0.01f,
            "Speed should start at zero.");

        // Sample the PEAK while running, not the value at the end.
        //
        // Reading it only after 120 frames quietly assumed the player would
        // still be in open floor by then, which depends entirely on frame
        // time: at ~4 m/s, 120 frames covers anywhere from 6 to 12 metres,
        // and the museum is only about 10 m deep from the spawn point. Once
        // the scene got heavier (ceiling, display cases, picture lights) the
        // frames lengthened, the player reliably reached the north wall, and
        // the test read the Speed of someone standing still against it.
        //
        // The peak is the honest measurement of "did movement drive the
        // Animator", and it still fails outright if the player never moves.
        float peakSpeed = 0f;

        for (int i = 0; i < 120; i++)
        {
            SetPrivate(reader, "moveInput", Vector2.up);
            yield return null;

            peakSpeed = Mathf.Max(peakSpeed, animator.GetFloat("Speed"));
        }

        Assert.Greater(
            peakSpeed,
            0.1f,
            "Speed never rose above zero while the player was moving, so the " +
            "Animator is not being driven by actual movement.");
    }

    [UnityTest]
    public IEnumerator Grounded_IsReportedToTheAnimator()
    {
        // Wait for the CharacterController itself to genuinely settle,
        // rather than assuming a fixed frame count is always enough - see
        // PlayModePhysicsWait for why a fixed wait is the wrong tool here.
        CharacterController cc = player.GetComponent<CharacterController>();
        yield return PlayModePhysicsWait.UntilGrounded(cc);

        // One more frame: PlayerAnimatorDriver.Update() mirrors isGrounded
        // onto the Animator the same frame, but execution order between it
        // and PlayerController's own Update is not guaranteed, so the
        // Animator parameter can still be one frame behind the controller.
        yield return null;

        Assert.IsTrue(
            animator.GetBool("IsGrounded"),
            "IsGrounded was false on the Animator even though the " +
            "CharacterController itself reports grounded - the driver is " +
            "not forwarding the real state.");
    }
}
