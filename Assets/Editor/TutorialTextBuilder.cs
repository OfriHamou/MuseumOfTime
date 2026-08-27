using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Phase 5, Step 5.4: dynamic, world-space tutorial text in MuseumNight.
///
///   Unity.exe -batchmode -quit -projectPath . ^
///             -executeMethod TutorialTextBuilder.BuildFromCommandLine
///
/// Reuses the TutorialTrigger placed by Step 3.2's CoreSystemsBuilder for
/// Move, and adds one more trigger + plaque pair per remaining verb, plus a
/// persistent objective line. Idempotent - re-running it edits the existing
/// triggers/plaques in place rather than duplicating them.
/// </summary>
public static class TutorialTextBuilder
{
    private const string ScenePath = "Assets/Scenes/MuseumNight.unity";

    private struct VerbTutorial
    {
        public string TriggerName;
        public string PlaqueName;
        public Vector3 Position;
        public Vector3 TriggerSize;
        public string Message;

        public VerbTutorial(string trigger, string plaque, Vector3 position, Vector3 size, string message)
        {
            TriggerName = trigger;
            PlaqueName = plaque;
            Position = position;
            TriggerSize = size;
            Message = message;
        }
    }

    [MenuItem("Museum of Time/Build Tutorial Text")]
    public static void BuildMenu() { Build(); }

    public static void BuildFromCommandLine() { Build(); }

    private static void Build()
    {
        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

        GameObject triggers = GameObject.Find("Triggers");
        if (triggers == null)
        {
            Debug.LogError("TUTORIAL TEXT FAILED: no 'Triggers' parent - run Build Core Systems first.");
            return;
        }

        GameObject plaques = MenuUIBuilder.FindOrCreate("TutorialPlaques", null);

        var verbs = new[]
        {
            // Move reuses the trigger CoreSystemsBuilder already placed.
            new VerbTutorial(
                "Trigger_TutorialMove", "Plaque_Move",
                new Vector3(0f, 1.6f, -7f), new Vector3(8f, 3f, 4f),
                "Hold W to walk toward where you are looking."),

            new VerbTutorial(
                "Trigger_TutorialRun", "Plaque_Run",
                new Vector3(-9f, 1.6f, -6f), new Vector3(6f, 3f, 4f),
                "Hold Shift while moving to run."),

            new VerbTutorial(
                "Trigger_TutorialJump", "Plaque_Jump",
                new Vector3(5f, 1.6f, -2f), new Vector3(5f, 3f, 4f),
                "Press Space to jump."),

            new VerbTutorial(
                "Trigger_TutorialInteract", "Plaque_Interact",
                new Vector3(0f, 1.6f, 5f), new Vector3(6f, 3f, 4f),
                "Press E to interact with whatever you are facing."),

            new VerbTutorial(
                "Trigger_TutorialOrb", "Plaque_Orb",
                new Vector3(-5f, 1.6f, 3f), new Vector3(5f, 3f, 4f),
                "Left-click to throw the Chrono Orb. It freezes or rewinds - it never destroys."),

            new VerbTutorial(
                "Trigger_TutorialCamera", "Plaque_Camera",
                new Vector3(12f, 1.6f, 2f), new Vector3(5f, 3f, 4f),
                "Press C to switch between third person and the Time Lens view."),

            new VerbTutorial(
                "Trigger_TutorialEra", "Plaque_Era",
                new Vector3(-9f, 1.6f, 6f), new Vector3(6f, 3f, 4f),
                "Once era travel unlocks, press Q or R to step between Past, Present and Future."),

            new VerbTutorial(
                "Trigger_TutorialSlowTime", "Plaque_SlowTime",
                new Vector3(9f, 5.6f, 3f), new Vector3(4f, 3f, 4f),
                "Hold Ctrl to slow time - {energy}% energy remaining."),
        };

        foreach (VerbTutorial verb in verbs)
        {
            GameObject plaque = CreatePlaque(plaques, verb.PlaqueName, verb.Position, verb.Message);
            WireTrigger(triggers, verb.TriggerName, verb.Position, verb.TriggerSize, plaque);
        }

        // The persistent objective line, at the entrance looking into the
        // main gallery - the player's "next goal" from the moment they enter.
        GameObject objective = MenuUIBuilder.FindOrCreate("Plaque_Objective", plaques);
        objective.transform.position = new Vector3(0f, 2.2f, -9f);
        SetupTextMesh(objective, "Objective: Reach the Clock of Creation");
        MenuUIBuilder.Ensure<WorldObjectiveText>(objective);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);

        Debug.Log("TUTORIAL TEXT OK: " + verbs.Length +
                   " verb plaques (world-space, dynamic, fade on approach) " +
                   "plus one persistent objective line.");
    }

    private static GameObject CreatePlaque(GameObject parent, string name, Vector3 position, string message)
    {
        GameObject go = MenuUIBuilder.FindOrCreate(name, parent);
        go.transform.position = position;

        SetupTextMesh(go, message);

        WorldTutorialText tutorial = MenuUIBuilder.Ensure<WorldTutorialText>(go);
        var so = new SerializedObject(tutorial);
        so.FindProperty("fadeDistance").floatValue = 6f;
        so.FindProperty("fadeSpeed").floatValue = 4f;
        so.ApplyModifiedPropertiesWithoutUndo();

        // TutorialTrigger.OnPlayerEntered reveals this with SetActive(true);
        // WorldTutorialText then owns its own fade for the rest of its life.
        go.SetActive(false);

        return go;
    }

    private static void SetupTextMesh(GameObject go, string message)
    {
        TextMeshPro tmp = MenuUIBuilder.Ensure<TextMeshPro>(go);
        tmp.text = message;
        // fontSize=2.5 in a 1.2m-tall box was ~2x the box height per line,
        // overflowing/clipping badly - auto-size keeps any message length
        // readable without another magic constant to get wrong.
        tmp.enableAutoSizing = true;
        tmp.fontSizeMin = 0.3f;
        tmp.fontSizeMax = 0.9f;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = Color.white;
        tmp.enableWordWrapping = true;

        RectTransform rect = go.GetComponent<RectTransform>();
        if (rect != null)
        {
            rect.sizeDelta = new Vector2(3.2f, 1.2f);
        }
    }

    private static void WireTrigger(
        GameObject parent, string name, Vector3 position, Vector3 size, GameObject plaque)
    {
        GameObject triggerGo = MenuUIBuilder.FindOrCreate(name, parent);
        triggerGo.transform.position = position;

        BoxCollider box = MenuUIBuilder.Ensure<BoxCollider>(triggerGo);
        box.isTrigger = true;
        box.size = size;

        TutorialTrigger trigger = MenuUIBuilder.Ensure<TutorialTrigger>(triggerGo);
        var so = new SerializedObject(trigger);
        so.FindProperty("textObject").objectReferenceValue = plaque;
        // "message" is TutorialTrigger's own field (Step 3.2); the plaque
        // carries the text now, but keep it in sync for anyone reading the
        // trigger alone.
        so.FindProperty("message").stringValue = plaque.GetComponent<TextMeshPro>().text;
        so.ApplyModifiedPropertiesWithoutUndo();
    }
}
