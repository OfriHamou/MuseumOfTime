using Unity.Cinemachine;
using UnityEditor.Animations;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// One-off scene setup, driven through Unity's own API so the scene file is
/// always written validly. Run it from the menu, or headlessly with:
///
///   Unity.exe -batchmode -quit -projectPath . ^
///             -executeMethod MuseumSceneSetup.BuildCameraRigFromCommandLine
///
/// It is idempotent: running it twice leaves the scene in the same state.
/// </summary>
public static class MuseumSceneSetup
{
    private const string SceneName = "MuseumNight";
    private const string ScenePath = "Assets/Scenes/MuseumNight.unity";

    [MenuItem("Museum of Time/Build Camera Rig in MuseumNight")]
    public static void BuildCameraRigMenu()
    {
        BuildCameraRig();
    }

    public static void BuildCameraRigFromCommandLine()
    {
        BuildCameraRig();
    }

    private static void BuildCameraRig()
    {
        Scene scene = EditorSceneManager.OpenScene(
            ScenePath,
            OpenSceneMode.Single);

        GameObject player = GameObject.Find("Player");
        if (player == null)
        {
            Debug.LogError("SETUP FAILED: no 'Player' object in " + SceneName);
            return;
        }

        // ---- Camera pivot at head height, parented to the player ----------
        Transform pivot = player.transform.Find("CameraPivot");
        if (pivot == null)
        {
            var pivotObject = new GameObject("CameraPivot");
            pivot = pivotObject.transform;
            pivot.SetParent(player.transform, false);
        }

        pivot.localPosition = new Vector3(0f, 1.6f, 0f);
        pivot.localRotation = Quaternion.identity;

        // ---- Main camera: the real Camera plus the Cinemachine brain ------
        GameObject mainCamera = FindOrCreate("MainCamera", "--- CAMERAS --- ");
        mainCamera.tag = "MainCamera";

        Camera cam = Ensure<Camera>(mainCamera);
        cam.nearClipPlane = 0.05f;
        cam.farClipPlane = 300f;

        Ensure<AudioListener>(mainCamera);
        Ensure<CinemachineBrain>(mainCamera);

        // ---- First person: sits on the pivot, inherits its rotation -------
        GameObject firstPerson = FindOrCreate(
            "FirstPersonCamera",
            "--- CAMERAS --- ");

        firstPerson.transform.SetParent(pivot, false);
        firstPerson.transform.localPosition = Vector3.zero;
        firstPerson.transform.localRotation = Quaternion.identity;

        CinemachineCamera fpCam = Ensure<CinemachineCamera>(firstPerson);
        fpCam.Follow = null;
        fpCam.LookAt = null;
        fpCam.Lens.FieldOfView = 70f;

        // ---- Third person: orbits behind and above the pivot --------------
        GameObject thirdPerson = FindOrCreate(
            "ThirdPersonCamera",
            "--- CAMERAS --- ");

        thirdPerson.transform.SetParent(null);

        CinemachineCamera tpCam = Ensure<CinemachineCamera>(thirdPerson);
        tpCam.Follow = pivot;
        tpCam.LookAt = pivot;
        tpCam.Lens.FieldOfView = 60f;

        CinemachineThirdPersonFollow follow =
            Ensure<CinemachineThirdPersonFollow>(thirdPerson);

        follow.ShoulderOffset = new Vector3(0.5f, 0.2f, 0f);
        follow.VerticalArmLength = 0.2f;
        follow.CameraDistance = 4.5f;

        // ---- The rig component that switches between them -----------------
        PlayerCameraRig rig = Ensure<PlayerCameraRig>(player);

        var so = new SerializedObject(rig);
        so.FindProperty("firstPersonCamera").objectReferenceValue = fpCam;
        so.FindProperty("thirdPersonCamera").objectReferenceValue = tpCam;
        so.FindProperty("cameraPivot").objectReferenceValue = pivot;
        so.ApplyModifiedPropertiesWithoutUndo();

        // ---- Animator: the controller built by NoaAnimatorBuilder ---------
        Animator animator = Ensure<Animator>(player);

        var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(
            "Assets/Animations/Player/NoaController.controller");

        if (controller == null)
        {
            Debug.LogWarning(
                "NoaController not found. Run " +
                "Museum of Time > Build Noa Animator Controller first.");
        }
        else
        {
            animator.runtimeAnimatorController = controller;
            animator.applyRootMotion = false;
        }

        PlayerAnimatorDriver driver = Ensure<PlayerAnimatorDriver>(player);

        var driverSo = new SerializedObject(driver);
        driverSo.FindProperty("animator").objectReferenceValue = animator;
        driverSo.ApplyModifiedPropertiesWithoutUndo();

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);

        Debug.Log(
            "SETUP OK: camera rig built. " +
            "MainCamera(Camera+Brain), FirstPersonCamera(CM), " +
            "ThirdPersonCamera(CM+ThirdPersonFollow), CameraPivot at 1.6m, " +
            "Animator + PlayerAnimatorDriver on Player.");
    }

    /// <summary>Finds a root object by name, creating it under a parent if absent.</summary>
    private static GameObject FindOrCreate(string name, string parentName)
    {
        GameObject existing = GameObject.Find(name);
        if (existing != null)
        {
            return existing;
        }

        var created = new GameObject(name);

        GameObject parent = GameObject.Find(parentName);
        if (parent != null)
        {
            created.transform.SetParent(parent.transform, false);
        }

        return created;
    }

    private static T Ensure<T>(GameObject target) where T : Component
    {
        T component = target.GetComponent<T>();

        if (component == null)
        {
            component = target.AddComponent<T>();
        }

        return component;
    }
}
