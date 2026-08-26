using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Opens every scene in the build list and reports what is actually in it.
/// Written to settle, without opening the editor UI, whether the scenes are
/// genuinely empty or merely appear empty.
///
///   Unity.exe -batchmode -quit -projectPath . ^
///             -executeMethod SceneAudit.Run
/// </summary>
public static class SceneAudit
{
    public static void Run()
    {
        var report = new StringBuilder();
        report.AppendLine("===== SCENE AUDIT =====");

        foreach (EditorBuildSettingsScene entry in EditorBuildSettings.scenes)
        {
            if (!entry.enabled)
            {
                continue;
            }

            Audit(entry.path, report);
        }

        report.AppendLine("=======================");
        Debug.Log(report.ToString());
    }

    private static void Audit(string path, StringBuilder report)
    {
        Scene scene;

        try
        {
            scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
        }
        catch (System.Exception e)
        {
            report.AppendLine("  " + path + "  FAILED TO OPEN: " + e.Message);
            return;
        }

        GameObject[] roots = scene.GetRootGameObjects();

        int total = 0;
        int missingScripts = 0;

        foreach (GameObject root in roots)
        {
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
            {
                total++;

                foreach (Component c in t.GetComponents<Component>())
                {
                    if (c == null)
                    {
                        missingScripts++;
                    }
                }
            }
        }

        report.AppendLine(
            "  " + System.IO.Path.GetFileName(path) +
            "  roots=" + roots.Length +
            "  objects=" + total +
            "  missingScripts=" + missingScripts);

        // Name the roots, so an "empty" scene can be distinguished from one
        // whose contents simply are not visible in the hierarchy window.
        foreach (GameObject root in roots)
        {
            report.AppendLine("      root: " + root.name +
                              " (children=" +
                              (root.GetComponentsInChildren<Transform>(true).Length - 1) +
                              ", active=" + root.activeSelf + ")");
        }
    }
}
