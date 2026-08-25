using System.Collections.Generic;

/// <summary>
/// Records which trigger types have fired, so a full playthrough can be shown
/// to have exercised every one of them.
/// </summary>
public static class TriggerLog
{
    private static readonly HashSet<string> Fired = new HashSet<string>();

    public static void Record(string name)
    {
        Fired.Add(name);
    }

    public static int DistinctCount => Fired.Count;

    public static string Summary => string.Join(", ", Fired);

    public static void Clear()
    {
        Fired.Clear();
    }
}
