using System.Collections.Generic;
using TMPro;
using UnityEngine;

/// <summary>
/// Short-lived messages telling the player what just happened.
///
/// The game had no way to say anything to the player at the moment it
/// mattered. A Chronological Shadow could take a Time Shard and dock 60 score
/// with no sound, no flash and no text - the number in the corner simply went
/// down, and only if you happened to be looking at it. From the player's side
/// that is indistinguishable from a bug.
///
/// Static <see cref="Post"/> so any system can report an event without needing
/// a reference to the HUD; if no feed exists the call is a harmless no-op,
/// which keeps gameplay scripts usable in scenes with no UI (the test suite
/// loads several).
/// </summary>
public sealed class HudMessageFeed : MonoBehaviour
{
    public enum Tone
    {
        Neutral,
        Good,
        Bad,
    }

    private struct Entry
    {
        public string text;
        public Tone tone;
        public float bornAt;
    }

    private static HudMessageFeed instance;

    [SerializeField] private TMP_Text label;

    [Tooltip("Seconds a message stays fully visible.")]
    [SerializeField] private float holdSeconds = 3.2f;

    [SerializeField] private float fadeSeconds = 0.8f;

    [Tooltip("How many recent messages to show at once.")]
    [SerializeField] private int maxVisible = 3;

    private readonly List<Entry> entries = new List<Entry>();

    /// <summary>The most recent message. Exposed for tests.</summary>
    public static string LastMessage { get; private set; } = "";

    private void Awake()
    {
        instance = this;

        if (label == null)
        {
            label = GetComponentInChildren<TMP_Text>(true);
        }

        if (label != null)
        {
            label.text = "";
        }
    }

    private void OnDestroy()
    {
        if (instance == this)
        {
            instance = null;
        }
    }

    /// <summary>Shows a message. Safe to call when no HUD exists.</summary>
    public static void Post(string text, Tone tone = Tone.Neutral)
    {
        if (string.IsNullOrEmpty(text))
        {
            return;
        }

        LastMessage = text;

        if (instance != null)
        {
            instance.Add(text, tone);
        }
    }

    private void Add(string text, Tone tone)
    {
        entries.Add(new Entry
        {
            text = text,
            tone = tone,

            // Unscaled: a message must not linger three times as long just
            // because the Chrono Hourglass is being held.
            bornAt = Time.unscaledTime,
        });

        while (entries.Count > maxVisible)
        {
            entries.RemoveAt(0);
        }

        Render();
    }

    private void Update()
    {
        if (entries.Count == 0)
        {
            return;
        }

        float cutoff = holdSeconds + fadeSeconds;
        bool removed = false;

        for (int i = entries.Count - 1; i >= 0; i--)
        {
            if (Time.unscaledTime - entries[i].bornAt >= cutoff)
            {
                entries.RemoveAt(i);
                removed = true;
            }
        }

        Render();

        if (removed && entries.Count == 0 && label != null)
        {
            label.text = "";
        }
    }

    private void Render()
    {
        if (label == null)
        {
            return;
        }

        var builder = new System.Text.StringBuilder();

        foreach (Entry entry in entries)
        {
            float age = Time.unscaledTime - entry.bornAt;
            float alpha = age <= holdSeconds
                ? 1f
                : Mathf.Clamp01(1f - ((age - holdSeconds) / Mathf.Max(0.01f, fadeSeconds)));

            // Rich text rather than one label per line: the count of visible
            // messages varies, and TMP does the layout for free.
            builder.Append("<alpha=#")
                   .Append(Mathf.RoundToInt(alpha * 255f).ToString("X2"))
                   .Append('>')
                   .Append(ColourOpen(entry.tone))
                   .Append(entry.text)
                   .Append("</color>")
                   .Append('\n');
        }

        label.text = builder.ToString().TrimEnd('\n');
    }

    private static string ColourOpen(Tone tone)
    {
        switch (tone)
        {
            case Tone.Good: return "<color=#8BE29A>";
            case Tone.Bad: return "<color=#FF7A72>";
            default: return "<color=#E6ECF7>";
        }
    }
}
