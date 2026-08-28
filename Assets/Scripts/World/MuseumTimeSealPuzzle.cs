using System;
using UnityEngine;

/// <summary>
/// MuseumNight's introductory puzzle: three Temporal Seals, one per era,
/// scattered around the museum so finding them means actually exploring it
/// rather than walking straight from spawn to the exit. Restoring all three
/// unlocks the Time Lens display. Mirrors GearPuzzle's shape - a small
/// singleton owning puzzle state - rather than a generic puzzle framework:
/// three seals is the whole puzzle, so three seals is all this needs to know.
/// </summary>
public sealed class MuseumTimeSealPuzzle : MonoBehaviour
{
    private const int TotalSeals = 3;

    public static MuseumTimeSealPuzzle Instance { get; private set; }

    [Tooltip("The sealed display cover shown until all three seals are restored.")]
    [SerializeField] private GameObject lockedDisplay;

    [Tooltip("The Time Lens pickup itself - hidden and non-interactable until solved.")]
    [SerializeField] private GameObject timeLens;

    public int RestoredCount { get; private set; }
    public bool IsSolved => RestoredCount >= TotalSeals;

    /// <summary>Raised each time a seal is restored, carrying the new count.</summary>
    public event Action<int> SealRestored;

    private void Awake()
    {
        Instance = this;
    }

    private void Start()
    {
        if (timeLens != null) { timeLens.SetActive(false); }
        if (lockedDisplay != null) { lockedDisplay.SetActive(true); }
    }

    /// <summary>
    /// Called by a TemporalSeal the instant it restores. Deliberately does
    /// not take the era that solved it - naming the era here would let a
    /// player solve the other two seals by elimination instead of by reading
    /// the riddle images, which defeats the point of hiding the answer.
    /// </summary>
    public void RegisterRestored()
    {
        if (IsSolved)
        {
            return;
        }

        RestoredCount++;
        SealRestored?.Invoke(RestoredCount);

        HudMessageFeed.Post(
            "Temporal Seal restored - " + RestoredCount + "/" + TotalSeals,
            HudMessageFeed.Tone.Good);

        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.Play(AudioManager.Sfx.SealRestored);
        }

        if (!IsSolved)
        {
            return;
        }

        if (lockedDisplay != null) { lockedDisplay.SetActive(false); }
        if (timeLens != null) { timeLens.SetActive(true); }

        HudMessageFeed.Post("Time Lens display unlocked", HudMessageFeed.Tone.Good);
    }
}
