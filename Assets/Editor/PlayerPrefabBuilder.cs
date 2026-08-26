using Unity.Cinemachine;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Builds a reusable Player prefab, so FrozenCity and ClockCore (Phase 6) do
/// not need Noa hand-built a second and third time. MuseumNight's own Player
/// predates this prefab and was never converted to it - Phase 6 does not
/// touch MuseumNight beyond adding the exit trigger in
/// SceneConnectionsBuilder, so that scene is left exactly as it was.
///
/// The third-person Cinemachine camera is deliberately NOT part of this
/// prefab: CinemachineThirdPersonFollow needs a scene-root object to orbit
/// around the player's CameraPivot, the same reason MuseumSceneSetup.cs
/// builds it directly in the scene rather than as a player child. Each
/// scene's content builder wires it up after instantiating this prefab.
///
///   Unity.exe -batchmode -quit -projectPath . ^
///             -executeMethod PlayerPrefabBuilder.BuildFromCommandLine
/// </summary>
public static class PlayerPrefabBuilder
{
    public const string PrefabPath = "Assets/Prefabs/Player/Player.prefab";

    private const string ActionsPath = "Assets/Input/MuseumInputActions.inputactions";
    private const string NoaControllerPath = "Assets/Animations/Player/NoaController.controller";
    private const string OrbPrefabPath = "Assets/Prefabs/World/ChronoOrb.prefab";

    [MenuItem("Museum of Time/Build Player Prefab")]
    public static void BuildMenu() { Build(); }

    public static void BuildFromCommandLine() { Build(); }

    private static void Build()
    {
        InputActionAsset actions = AssetDatabase.LoadAssetAtPath<InputActionAsset>(ActionsPath);
        if (actions == null)
        {
            Debug.LogError("PLAYER PREFAB FAILED: MuseumInputActions not found at " + ActionsPath);
            return;
        }

        var root = new GameObject("Player");
        root.tag = "Player";

        CharacterController cc = root.AddComponent<CharacterController>();
        cc.height = 2f;
        cc.radius = 0.5f;
        cc.center = new Vector3(0f, 1f, 0f);

        var input = root.AddComponent<PlayerInput>();
        input.actions = actions;
        input.defaultActionMap = "Player";
        input.notificationBehavior = PlayerNotifications.InvokeCSharpEvents;

        root.AddComponent<PlayerInputReader>();

        // Defaults on PlayerController already match the tuned values from
        // Step 1.1 (walk 4, run 7, gravity -20, jump height 1.2, step offset
        // 0.35, slope limit 50) - nothing to patch.
        root.AddComponent<PlayerController>();

        var pivotGo = new GameObject("CameraPivot");
        pivotGo.transform.SetParent(root.transform, false);
        pivotGo.transform.localPosition = new Vector3(0f, 1.6f, 0f);

        var firstPersonGo = new GameObject("FirstPersonCamera");
        firstPersonGo.transform.SetParent(pivotGo.transform, false);
        CinemachineCamera firstPersonCam = firstPersonGo.AddComponent<CinemachineCamera>();
        firstPersonCam.Lens.FieldOfView = 70f;

        PlayerCameraRig rig = root.AddComponent<PlayerCameraRig>();
        var rigSo = new SerializedObject(rig);
        rigSo.FindProperty("firstPersonCamera").objectReferenceValue = firstPersonCam;
        rigSo.FindProperty("cameraPivot").objectReferenceValue = pivotGo.transform;
        // thirdPersonCamera is left unset: it lives at scene root, wired by
        // whichever scene's content builder instantiates this prefab.
        rigSo.ApplyModifiedPropertiesWithoutUndo();

        Animator animator = root.AddComponent<Animator>();
        AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(NoaControllerPath);
        if (controller != null)
        {
            animator.runtimeAnimatorController = controller;
        }
        animator.applyRootMotion = false;

        PlayerAnimatorDriver driver = root.AddComponent<PlayerAnimatorDriver>();
        var driverSo = new SerializedObject(driver);
        driverSo.FindProperty("animator").objectReferenceValue = animator;
        driverSo.ApplyModifiedPropertiesWithoutUndo();

        root.AddComponent<PlayerInteractor>();
        root.AddComponent<ChronoHourglass>();

        ChronoOrbLauncher launcher = root.AddComponent<ChronoOrbLauncher>();
        GameObject orbPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(OrbPrefabPath);
        var launcherSo = new SerializedObject(launcher);
        launcherSo.FindProperty("orbPrefab").objectReferenceValue = orbPrefab;
        launcherSo.ApplyModifiedPropertiesWithoutUndo();

        System.IO.Directory.CreateDirectory("Assets/Prefabs/Player");
        PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        Object.DestroyImmediate(root);

        Debug.Log("PLAYER PREFAB OK: " + PrefabPath +
                   " (CharacterController, input, camera pivot + first-person " +
                   "camera, animator, interactor, hourglass, orb launcher).");
    }
}
