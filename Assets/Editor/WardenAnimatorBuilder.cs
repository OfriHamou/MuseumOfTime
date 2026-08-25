using System.IO;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

/// <summary>
/// Builds the Warden's AnimatorController, the enemy half of the "Animator
/// you defined yourself, at least four states" requirement.
///
/// Same approach as NoaAnimatorBuilder: every state, parameter and transition
/// is authored here rather than imported, so the state machine can be rebuilt
/// exactly and explained line by line.
///
///   Unity.exe -batchmode -quit -projectPath . ^
///             -executeMethod WardenAnimatorBuilder.BuildFromCommandLine
/// </summary>
public static class WardenAnimatorBuilder
{
    private const string Folder = "Assets/Animations/Enemies";
    private const string ControllerPath = Folder + "/WardenController.controller";

    [MenuItem("Museum of Time/Build Warden Animator Controller")]
    public static void BuildMenu() { Build(); }

    public static void BuildFromCommandLine() { Build(); }

    private static void Build()
    {
        Directory.CreateDirectory(Folder);
        AssetDatabase.DeleteAsset(ControllerPath);

        AnimatorController controller =
            AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);

        // AlertLevel is driven by the SAME value as the detection meter, so
        // the animation can never disagree with the mechanic the player is
        // actually being judged by.
        controller.AddParameter("Speed", AnimatorControllerParameterType.Float);
        controller.AddParameter("AlertLevel", AnimatorControllerParameterType.Float);
        controller.AddParameter("IsFrozen", AnimatorControllerParameterType.Bool);
        controller.AddParameter("AttackTrigger", AnimatorControllerParameterType.Trigger);

        AnimatorStateMachine machine = controller.layers[0].stateMachine;

        AnimatorState patrol = AddState(machine, "Patrol", new Vector3(300f, 0f, 0f));
        AnimatorState alert = AddState(machine, "Alert", new Vector3(300f, 90f, 0f));
        AnimatorState chase = AddState(machine, "Chase", new Vector3(300f, 180f, 0f));
        AnimatorState attack = AddState(machine, "Attack", new Vector3(600f, 180f, 0f));
        AnimatorState frozen = AddState(machine, "Frozen", new Vector3(0f, 90f, 0f));

        machine.defaultState = patrol;

        // Patrol -> Alert -> Chase, driven by how much of the detection meter
        // has filled. The thresholds match WardenAI: it flips to Chase at 1.
        Link(patrol, alert, AnimatorConditionMode.Greater, 0.15f, "AlertLevel");
        Link(alert, patrol, AnimatorConditionMode.Less, 0.15f, "AlertLevel");
        Link(alert, chase, AnimatorConditionMode.Greater, 0.95f, "AlertLevel");
        Link(chase, alert, AnimatorConditionMode.Less, 0.95f, "AlertLevel");

        // Attack can start from anywhere, but must not restart itself.
        AnimatorStateTransition anyToAttack = machine.AddAnyStateTransition(attack);
        anyToAttack.AddCondition(AnimatorConditionMode.If, 0f, "AttackTrigger");
        anyToAttack.duration = 0.05f;
        anyToAttack.hasExitTime = false;
        anyToAttack.canTransitionToSelf = false;

        AnimatorStateTransition attackToChase = attack.AddTransition(chase);
        attackToChase.hasExitTime = true;
        attackToChase.exitTime = 0.85f;
        attackToChase.duration = 0.1f;

        // Frozen by a Chrono Orb overrides everything.
        AnimatorStateTransition anyToFrozen = machine.AddAnyStateTransition(frozen);
        anyToFrozen.AddCondition(AnimatorConditionMode.If, 0f, "IsFrozen");
        anyToFrozen.duration = 0.05f;
        anyToFrozen.hasExitTime = false;
        anyToFrozen.canTransitionToSelf = false;

        Link(frozen, patrol, AnimatorConditionMode.IfNot, 0f, "IsFrozen");

        EditorUtility.SetDirty(controller);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log(
            "WARDEN ANIMATOR OK: " + machine.states.Length + " states, " +
            controller.parameters.Length + " parameters at " + ControllerPath);
    }

    private static AnimatorState AddState(
        AnimatorStateMachine machine, string name, Vector3 position)
    {
        AnimatorState state = machine.AddState(name, position);
        state.motion = CreateClip(name);
        return state;
    }

    /// <summary>A named, looping placeholder so no state is empty.</summary>
    private static AnimationClip CreateClip(string name)
    {
        string path = Folder + "/Warden" + name + ".anim";

        var clip = new AnimationClip { name = "Warden" + name };

        AnimationClipSettings settings =
            AnimationUtility.GetAnimationClipSettings(clip);

        settings.loopTime = name != "Attack";
        AnimationUtility.SetAnimationClipSettings(clip, settings);

        AssetDatabase.DeleteAsset(path);
        AssetDatabase.CreateAsset(clip, path);

        return clip;
    }

    private static void Link(
        AnimatorState from, AnimatorState to,
        AnimatorConditionMode mode, float threshold, string parameter)
    {
        AnimatorStateTransition transition = from.AddTransition(to);
        transition.AddCondition(mode, threshold, parameter);

        // Reacts immediately, rather than waiting for the clip to finish.
        transition.hasExitTime = false;
        transition.duration = 0.1f;
    }
}
