using Unity.Cinemachine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Brings the already-built third-person camera closer in all three
/// gameplay scenes without a full scene rebuild: opens each scene, finds the
/// existing "ThirdPersonCamera" object and tightens its
/// CinemachineThirdPersonFollow framing. The three content-builder scripts
/// (MuseumSceneSetup, FrozenCityContentBuilder, ClockCoreContentBuilder) were
/// updated with the matching baseline values so a future full rebuild won't
/// regress this.
///
/// A prior pass raised VerticalArmLength while shrinking CameraDistance,
/// which steepened the height-to-distance ratio (0.2/4.5=0.044 ->
/// 0.3/2.2=0.136) and made the view read as top-down. This pass drops the
/// ratio well below the original (0.15/2.6=0.058) and removes the shoulder's
/// extra vertical lift, for a shallow, behind-the-player exploration angle.
///
/// MuseumNight's hand-built Player root sits at world Y ~= 1 (its
/// CharacterController.center convention), while FrozenCity/ClockCore's
/// prefab-based players spawn at Y ~= 0.1. CameraPivot is a child at the same
/// local (0, 1.6, 0) in both, so the whole third-person rig otherwise ends up
/// ~0.9m higher in MuseumNight than the other two scenes for identical
/// component values. That is compensated here purely via ShoulderOffset.y
/// (a CinemachineThirdPersonFollow-only field) so first-person, which reads
/// CameraPivot directly, is completely unaffected.
///
///   Unity.exe -batchmode -quit -projectPath . ^
///             -executeMethod ThirdPersonCameraFixBuilder.BuildFromCommandLine
/// </summary>
public static class ThirdPersonCameraFixBuilder
{
    private const float BaseShoulderX = 0.5f;
    private const float VerticalArmLength = 0.15f;
    private const float CameraDistance = 2.6f;

    private const string ReferenceScene = "Assets/Scenes/FrozenCity.unity";

    [MenuItem("Museum of Time/Fix Third Person Camera Distance")]
    public static void BuildMenu() { Build(); }

    public static void BuildFromCommandLine() { Build(); }

    private static void Build()
    {
        float referenceY = ReadPlayerWorldY(ReferenceScene);

        FixScene("Assets/Scenes/FrozenCity.unity", 0f);
        FixScene("Assets/Scenes/ClockCore.unity", 0f);

        float museumY = ReadPlayerWorldY("Assets/Scenes/MuseumNight.unity");
        float museumCompensation = referenceY - museumY;
        FixScene("Assets/Scenes/MuseumNight.unity", museumCompensation);

        Debug.Log("NOACAM OK: CameraDistance=" + CameraDistance + ", VerticalArmLength=" + VerticalArmLength +
                   ", MuseumNight ShoulderOffset.y compensation=" + museumCompensation +
                   " (reference root Y=" + referenceY + ", MuseumNight root Y=" + museumY + ").");
    }

    private static float ReadPlayerWorldY(string scenePath)
    {
        EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
        GameObject player = GameObject.Find("Player");
        return player != null ? player.transform.position.y : 0f;
    }

    private static void FixScene(string scenePath, float shoulderYCompensation)
    {
        Scene scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);

        GameObject cam = GameObject.Find("ThirdPersonCamera");
        if (cam == null)
        {
            Debug.LogWarning("NOACAM: no 'ThirdPersonCamera' in " + scenePath + " - skipping.");
            return;
        }

        var follow = cam.GetComponent<CinemachineThirdPersonFollow>();
        if (follow == null)
        {
            Debug.LogWarning("NOACAM: 'ThirdPersonCamera' in " + scenePath + " has no CinemachineThirdPersonFollow.");
            return;
        }

        follow.ShoulderOffset = new Vector3(BaseShoulderX, shoulderYCompensation, 0f);
        follow.VerticalArmLength = VerticalArmLength;
        follow.CameraDistance = CameraDistance;

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
    }
}
