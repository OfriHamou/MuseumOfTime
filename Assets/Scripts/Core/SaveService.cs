using System.IO;
using UnityEngine;

/// <summary>
/// Writes GameState to disk as JSON.
///
/// The requirement names "Serialize" explicitly. An in-memory singleton that
/// survives LoadScene would arguably satisfy passing data between scenes, but
/// it cannot be shown to anyone. A real file can be opened during the defense,
/// which is why this exists.
/// </summary>
public static class SaveService
{
    private const string FileName = "museum-of-time-save.json";

    /// <summary>Full path of the save file, for showing in the defense.</summary>
    public static string Path =>
        System.IO.Path.Combine(Application.persistentDataPath, FileName);

    public static bool Exists => File.Exists(Path);

    public static void Save()
    {
        if (GameManager.Instance == null)
        {
            return;
        }

        try
        {
            File.WriteAllText(Path, GameManager.Instance.State.ToJson());
        }
        catch (System.Exception e)
        {
            Debug.LogWarning("Could not write save: " + e.Message);
        }
    }

    public static bool Load()
    {
        if (GameManager.Instance == null || !Exists)
        {
            return false;
        }

        try
        {
            GameManager.Instance.State.LoadFromJson(File.ReadAllText(Path));
            return true;
        }
        catch (System.Exception e)
        {
            Debug.LogWarning("Could not read save: " + e.Message);
            return false;
        }
    }

    public static void Delete()
    {
        if (Exists)
        {
            File.Delete(Path);
        }
    }
}
