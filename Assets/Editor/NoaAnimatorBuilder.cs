using System.IO;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

/// <summary>
/// Builds Noa's AnimatorController from scratch.
///
/// The assignment requires an Animator defined by us rather than imported,
/// with at least four states. This builds one explicitly: every state,
/// parameter and transition below is authored here, so the whole state
/// machine can be explained line by line. Motion clips are created as empty
/// placeholders and can be replaced with real animation later without
/// touching the state machine.
///
/// Run from the menu, or headlessly with:
///   Unity.exe -batchmode -quit -projectPath . ^
///             -executeMethod NoaAnimatorBuilder.BuildFromCommandLine
///
/// Idempotent: it rebuilds the controller from nothing each time.
/// </summary>
public static class NoaAnimatorBuilder
{
    private const string Folder = "Assets/Animations/Player";
    private const string ControllerPath = Folder + "/NoaController.controller";

    // Tuning: below WalkThreshold Noa is idle, above RunThreshold she runs.
    private const float WalkThreshold = 0.1f;
    private const float RunThreshold = 5.5f;

    [MenuItem("Museum of Time/Build Noa Animator Controller")]
    public static void BuildMenu()
    {
        Build();
    }

    public static void BuildFromCommandLine()
    {
        Build();
    }

    private static void Build()
    {
        Directory.CreateDirectory(Folder);

        AssetDatabase.DeleteAsset(ControllerPath);

        AnimatorController controller =
            AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);

        // ---- Parameters -------------------------------------------------
        // Speed is metres per second, read from CharacterController.velocity
        // rather than raw input so it stays correct when walking into a wall.
        controller.AddParameter("Speed", AnimatorControllerParameterType.Float);
        controller.AddParameter("IsGrounded", AnimatorControllerParameterType.Bool);
        controller.AddParameter("JumpTrigger", AnimatorControllerParameterType.Trigger);
        controller.AddParameter("InteractTrigger", AnimatorControllerParameterType.Trigger);

        AnimatorStateMachine machine = controller.layers[0].stateMachine;

        // ---- States -----------------------------------------------------
        AnimatorState idle = AddState(machine, "Idle", new Vector3(300f, 0f, 0f));
        AnimatorState walk = AddState(machine, "Walk", new Vector3(300f, 80f, 0f));
        AnimatorState run = AddState(machine, "Run", new Vector3(300f, 160f, 0f));
        AnimatorState jump = AddState(machine, "Jump", new Vector3(600f, 40f, 0f));
        AnimatorState fall = AddState(machine, "Fall", new Vector3(600f, 120f, 0f));
        AnimatorState interact = AddState(machine, "Interact", new Vector3(0f, 80f, 0f));

        machine.defaultState = idle;

        // ---- Locomotion transitions, driven by Speed --------------------
        Link(idle, walk, AnimatorConditionMode.Greater, WalkThreshold, "Speed");
        Link(walk, idle, AnimatorConditionMode.Less, WalkThreshold, "Speed");
        Link(walk, run, AnimatorConditionMode.Greater, RunThreshold, "Speed");
        Link(run, walk, AnimatorConditionMode.Less, RunThreshold, "Speed");

        // ---- Jump and fall ----------------------------------------------
        // Any state can jump, so the trigger is wired from the AnyState node.
        AnimatorStateTransition anyToJump = machine.AddAnyStateTransition(jump);
        anyToJump.AddCondition(AnimatorConditionMode.If, 0f, "JumpTrigger");
        anyToJump.duration = 0.05f;
        anyToJump.hasExitTime = false;
        // Without this a new jump would cancel and restart the current one.
        anyToJump.canTransitionToSelf = false;

        // Rising becomes falling once the upward part of the arc is over.
        AnimatorStateTransition jumpToFall = jump.AddTransition(fall);
        jumpToFall.hasExitTime = true;
        jumpToFall.exitTime = 0.5f;
        jumpToFall.duration = 0.1f;

        // Landing returns to Idle; the Speed conditions above take over again.
        Link(fall, idle, AnimatorConditionMode.If, 0f, "IsGrounded");

        // Walking off a ledge should fall without a jump being pressed.
        Link(idle, fall, AnimatorConditionMode.IfNot, 0f, "IsGrounded");
        Link(walk, fall, AnimatorConditionMode.IfNot, 0f, "IsGrounded");
        Link(run, fall, AnimatorConditionMode.IfNot, 0f, "IsGrounded");

        // ---- Interact ----------------------------------------------------
        AnimatorStateTransition anyToInteract =
            machine.AddAnyStateTransition(interact);
        anyToInteract.AddCondition(AnimatorConditionMode.If, 0f, "InteractTrigger");
        anyToInteract.duration = 0.1f;
        anyToInteract.hasExitTime = false;
        anyToInteract.canTransitionToSelf = false;

        AnimatorStateTransition interactToIdle = interact.AddTransition(idle);
        interactToIdle.hasExitTime = true;
        interactToIdle.exitTime = 0.9f;
        interactToIdle.duration = 0.1f;

        EditorUtility.SetDirty(controller);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log(
            "ANIMATOR OK: NoaController built with " +
            machine.states.Length + " states and " +
            controller.parameters.Length + " parameters at " + ControllerPath);
    }

    /// <summary>Adds a state with a placeholder clip so it is never empty.</summary>
    private static AnimatorState AddState(
        AnimatorStateMachine machine,
        string name,
        Vector3 position)
    {
        AnimatorState state = machine.AddState(name, position);
        state.motion = CreatePlaceholderClip(name);
        return state;
    }

    /// <summary>
    /// Creates a named, looping placeholder clip. Real animation (Mixamo or
    /// otherwise) can be dropped onto the state later; only the controller
    /// itself has to be our own work.
    /// </summary>
    private static AnimationClip CreatePlaceholderClip(string name)
    {
        string path = Folder + "/" + name + ".anim";

        var clip = new AnimationClip { name = name };

        AnimationClipSettings settings =
            AnimationUtility.GetAnimationClipSettings(clip);

        settings.loopTime = name != "Jump" && name != "Interact";
        AnimationUtility.SetAnimationClipSettings(clip, settings);

        AssetDatabase.DeleteAsset(path);
        AssetDatabase.CreateAsset(clip, path);

        return clip;
    }

    private static void Link(
        AnimatorState from,
        AnimatorState to,
        AnimatorConditionMode mode,
        float threshold,
        string parameter)
    {
        AnimatorStateTransition transition = from.AddTransition(to);
        transition.AddCondition(mode, threshold, parameter);

        // Locomotion must react immediately, so it never waits for the clip
        // to finish playing.
        transition.hasExitTime = false;
        transition.duration = 0.1f;
    }
}
