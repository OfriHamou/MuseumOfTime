using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

/// <summary>
/// Command-line builds. Run headlessly with:
///
///   Unity.exe -batchmode -quit -projectPath . ^
///             -executeMethod BuildScript.BuildDevelopment
///
/// The development build carries BuildPlaytest, so the resulting executable
/// can be play-tested from a shell. BuildRelease is the one that ships: it has
/// no development flag, so the playtest harness is compiled out entirely.
/// </summary>
public static class BuildScript
{
    private const string DevPath = "Build/Playtest/MuseumOfTime.exe";
    private const string ReleasePath = "Build/Release/MuseumOfTime.exe";

    [MenuItem("Museum of Time/Build Development Player")]
    public static void BuildDevelopment()
    {
        Build(DevPath, BuildOptions.Development);
    }

    [MenuItem("Museum of Time/Build Release Player")]
    public static void BuildRelease()
    {
        Build(ReleasePath, BuildOptions.None);
    }

    private static void Build(string path, BuildOptions options)
    {
        string[] scenes = EditorBuildSettings.scenes
            .Where(s => s.enabled)
            .Select(s => s.path)
            .ToArray();

        if (scenes.Length == 0)
        {
            Debug.LogError("BUILD FAILED: no enabled scenes in Build Settings.");
            return;
        }

        var playerOptions = new BuildPlayerOptions
        {
            scenes = scenes,
            locationPathName = path,
            target = BuildTarget.StandaloneWindows64,
            options = options,
        };

        BuildReport report = BuildPipeline.BuildPlayer(playerOptions);
        BuildSummary summary = report.summary;

        if (summary.result == BuildResult.Succeeded)
        {
            // The 300 MB cap in the brief applies to the compressed upload,
            // so this raw figure is an early-warning number, not the verdict.
            double megabytes = summary.totalSize / (1024.0 * 1024.0);

            Debug.Log(
                "BUILD OK: " + path + " (" + megabytes.ToString("0.0") +
                " MB uncompressed, " + scenes.Length + " scenes)");
        }
        else
        {
            Debug.LogError(
                "BUILD FAILED: " + summary.result +
                " with " + summary.totalErrors + " errors.");
        }
    }
}
