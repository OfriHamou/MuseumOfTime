using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Phase 6, Step 6.1: the one thing MuseumNight was still missing - an
/// actual way to leave for FrozenCity once the Time Lens is held. Without
/// this, Step 3.9's item-acquisition chain was real but there was no door;
/// S9 asks for a coherent link between scenes, not just a flag that gets
/// carried between them.
///
/// FrozenCity's own exit to ClockCore is built by
/// FrozenCityContentBuilder.BuildExit, since that scene did not exist as
/// playable content before this phase. This builder only touches MuseumNight.
///
///   Unity.exe -batchmode -quit -projectPath . ^
///             -executeMethod SceneConnectionsBuilder.BuildFromCommandLine
/// </summary>
public static class SceneConnectionsBuilder
{
    private const string ScenePath = "Assets/Scenes/MuseumNight.unity";

    [MenuItem("Museum of Time/Build Scene Connections (Phase 6)")]
    public static void BuildMenu() { Build(); }

    public static void BuildFromCommandLine() { Build(); }

    private static void Build()
    {
        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

        GameObject triggers = GameObject.Find("Triggers");
        if (triggers == null)
        {
            Debug.LogError("SCENE CONNECTIONS FAILED: no 'Triggers' parent - run Build Core Systems first.");
            return;
        }

        GameObject existing = triggers.transform.Find("Exit_ToFrozenCity")?.gameObject;
        if (existing != null)
        {
            Object.DestroyImmediate(existing);
        }

        var exit = new GameObject("Exit_ToFrozenCity");
        exit.transform.SetParent(triggers.transform, false);

        // Just past where the Time Lens sits (9, 5.6, 6) - leaving is the
        // next thing to do once it is in hand, per the plan's own beat.
        exit.transform.position = new Vector3(9f, 5.6f, 9.5f);

        BoxCollider box = exit.AddComponent<BoxCollider>();
        box.isTrigger = true;
        box.size = new Vector3(3f, 3f, 2f);

        exit.AddComponent<SceneLoader>();
        SceneExitTrigger trigger = exit.AddComponent<SceneExitTrigger>();

        var so = new SerializedObject(trigger);
        so.FindProperty("requiredItem").enumValueIndex = (int)SceneExitTrigger.RequiredItem.TimeLens;
        so.FindProperty("targetScene").stringValue = "FrozenCity";
        so.ApplyModifiedPropertiesWithoutUndo();

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);

        Debug.Log("SCENE CONNECTIONS OK: MuseumNight now exits to FrozenCity once the Time Lens is held.");
    }
}
