using Unity.Cinemachine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Gives FrozenCity and ClockCore the same two-camera rig MuseumNight has.
///
///   Unity.exe -batchmode -quit -projectPath . ^
///             -executeMethod CameraRigParityBuilder.BuildFromCommandLine
///
/// T19 requires a first-person/third-person switch with two cameras besides
/// the minimap, and Part 3 of the plan marks that requirement present in all
/// three gameplay scenes. It was not: FrozenCity and ClockCore each had a
/// single Camera and no PlayerCameraRig, so pressing C did nothing and there
/// was no first-person view to switch to for two thirds of the game.
///
/// Idempotent, and deliberately mirrors MuseumSceneSetup.BuildCameraRig so the
/// three scenes cannot drift apart.
/// </summary>
public static class CameraRigParityBuilder
{
    private static readonly string[] ScenePaths =
    {
        "Assets/Scenes/FrozenCity.unity",
        "Assets/Scenes/ClockCore.unity",
    };

    [MenuItem("Museum of Time/Build Camera Rigs (FrozenCity + ClockCore)")]
    public static void BuildMenu() { Build(); }

    public static void BuildFromCommandLine() { Build(); }

    private static void Build()
    {
        foreach (string scenePath in ScenePaths)
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(scenePath) == null) { continue; }

            Scene scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);

            if (BuildRig())
            {
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
                Debug.Log("RIG OK: " + scene.name);
            }
        }

        Debug.Log("=== CAMERA RIG PARITY COMPLETE ===");
    }

    private static bool BuildRig()
    {
        GameObject player = GameObject.FindWithTag("Player");
        if (player == null) { player = GameObject.Find("Player"); }

        if (player == null)
        {
            Debug.LogError("RIG FAILED: no Player in " +
                           SceneManager.GetActiveScene().name);
            return false;
        }

        // ---- Pivot at head height ----------------------------------------
        Transform pivot = player.transform.Find("CameraPivot");
        if (pivot == null)
        {
            var go = new GameObject("CameraPivot");
            pivot = go.transform;
            pivot.SetParent(player.transform, false);
        }

        pivot.localPosition = new Vector3(0f, 1.6f, 0f);
        pivot.localRotation = Quaternion.identity;

        // ---- Main camera + brain -----------------------------------------
        GameObject mainCamera = FindMainCamera();

        mainCamera.tag = "MainCamera";
        Camera cam = Ensure<Camera>(mainCamera);
        cam.nearClipPlane = 0.05f;
        cam.farClipPlane = 300f;

        Ensure<AudioListener>(mainCamera);
        Ensure<CinemachineBrain>(mainCamera);

        // A brain-driven camera must not also be parented to the player, or
        // the two fight over its transform every frame.
        if (mainCamera.transform.parent != null)
        {
            mainCamera.transform.SetParent(null, true);
        }

        // ---- First person -------------------------------------------------
        GameObject firstPerson = FindOrCreate("FirstPersonCamera");
        firstPerson.transform.SetParent(pivot, false);
        firstPerson.transform.localPosition = Vector3.zero;
        firstPerson.transform.localRotation = Quaternion.identity;

        CinemachineCamera fpCam = Ensure<CinemachineCamera>(firstPerson);
        fpCam.Follow = null;
        fpCam.LookAt = null;
        fpCam.Lens.FieldOfView = 70f;

        // ---- Third person -------------------------------------------------
        GameObject thirdPerson = FindOrCreate("ThirdPersonCamera");
        thirdPerson.transform.SetParent(null);

        CinemachineCamera tpCam = Ensure<CinemachineCamera>(thirdPerson);
        tpCam.Follow = pivot;
        tpCam.LookAt = pivot;
        tpCam.Lens.FieldOfView = 60f;

        CinemachineThirdPersonFollow follow = Ensure<CinemachineThirdPersonFollow>(thirdPerson);

        // These players are the prefab-based ones, whose root sits at the
        // capsule bottom - so no vertical compensation is wanted here. See the
        // note in MuseumSceneSetup for why MuseumNight's differs.
        follow.ShoulderOffset = new Vector3(0.5f, 0f, 0f);
        follow.VerticalArmLength = 0.15f;
        follow.CameraDistance = 2.6f;

        // ---- The switcher --------------------------------------------------
        PlayerCameraRig rig = Ensure<PlayerCameraRig>(player);

        var so = new SerializedObject(rig);
        so.FindProperty("firstPersonCamera").objectReferenceValue = fpCam;
        so.FindProperty("thirdPersonCamera").objectReferenceValue = tpCam;
        so.FindProperty("cameraPivot").objectReferenceValue = pivot;
        so.ApplyModifiedPropertiesWithoutUndo();

        EditorUtility.SetDirty(rig);
        return true;
    }

    /// <summary>
    /// Reuses whatever real Camera the scene already has rather than adding a
    /// second one - two enabled cameras at the same depth render on top of
    /// each other.
    /// </summary>
    private static GameObject FindMainCamera()
    {
        GameObject tagged = GameObject.FindWithTag("MainCamera");
        if (tagged != null) { return tagged; }

        foreach (Camera c in Object.FindObjectsByType<Camera>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (!c.name.ToLowerInvariant().Contains("minimap"))
            {
                return c.gameObject;
            }
        }

        return new GameObject("MainCamera");
    }

    private static GameObject FindOrCreate(string name)
    {
        GameObject go = GameObject.Find(name);
        return go != null ? go : new GameObject(name);
    }

    private static T Ensure<T>(GameObject go) where T : Component
    {
        T c = go.GetComponent<T>();
        return c != null ? c : go.AddComponent<T>();
    }
}
