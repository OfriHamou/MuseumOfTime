using System;
using UnityEngine;

/// <summary>
/// Owns which era the world is in, and tells everything else when it changes.
///
/// Eras are sibling sets of objects that switch on and off, not three loaded
/// worlds. That is far cheaper and reads identically to the player, who only
/// ever sees one at a time.
/// </summary>
[DefaultExecutionOrder(-90)]
public sealed class EraManager : MonoBehaviour
{
    public static EraManager Instance { get; private set; }

    [SerializeField] private TimeEra startingEra = TimeEra.Present;

    [Tooltip("Locked until the Clock of Creation breaks, so the first scene " +
             "teaches one verb at a time.")]
    [SerializeField] private bool eraTravelUnlocked;

    [SerializeField] private float energyCostPerSwitch = 8f;

    private PlayerInputReader inputReader;

    /// <summary>Raised after the era changes, carrying the new era.</summary>
    public event Action<TimeEra> EraChanged;

    public TimeEra CurrentEra { get; private set; }

    public bool IsUnlocked => eraTravelUnlocked;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }

        Instance = this;
        CurrentEra = startingEra;
    }

    private void Start()
    {
        inputReader = FindFirstObjectByType<PlayerInputReader>();

        // Push the starting era out once, so era-bound objects hide correctly
        // before the player has pressed anything.
        Apply(CurrentEra);
    }

    private void Update()
    {
        // Deliberately NOT gated on eraTravelUnlocked: a player who presses Q
        // before finding the Time Lens needs to be told the key exists and
        // why it is not working yet. TryStep answers that.
        if (inputReader == null)
        {
            return;
        }

        if (inputReader.EraBackPressed)
        {
            Step(-1);
        }
        else if (inputReader.EraForwardPressed)
        {
            Step(1);
        }
    }

    /// <summary>Opens era travel. Called when the Clock of Creation breaks.</summary>
    public void Unlock()
    {
        eraTravelUnlocked = true;
    }

    /// <summary>
    /// One step along Past - Present - Future, paying the energy cost.
    /// Returns false, with a reason on the HUD, when the step is refused.
    ///
    /// Every refusal used to be a silent return. Pressing Q or R and having
    /// absolutely nothing happen - no sound, no message, no flicker - is
    /// indistinguishable from a broken key, and it is the same press whether
    /// the reason is that era travel is still locked, that there is no era
    /// further in that direction, or that the bar is too low. The player has
    /// no way to tell which, so they cannot act on any of them.
    /// </summary>
    public bool TryStep(int direction)
    {
        if (!eraTravelUnlocked)
        {
            HudMessageFeed.Post(
                "Era travel is locked - find the Time Lens first",
                HudMessageFeed.Tone.Bad);

            return false;
        }

        int next = Mathf.Clamp((int)CurrentEra + direction, 0, 2);

        if (next == (int)CurrentEra)
        {
            HudMessageFeed.Post(
                CurrentEra == TimeEra.Past
                    ? "Already in the earliest era"
                    : "Already in the latest era",
                HudMessageFeed.Tone.Neutral);

            return false;
        }

        // Era travel costs energy, so it stays a decision rather than a reflex.
        if (GameManager.Instance != null &&
            !GameManager.Instance.SpendEnergy(energyCostPerSwitch))
        {
            HudMessageFeed.Post(
                "Not enough Chrono Energy - wait for it to recover",
                HudMessageFeed.Tone.Bad);

            return false;
        }

        SetEra((TimeEra)next);
        return true;
    }

    private void Step(int direction)
    {
        TryStep(direction);
    }

    public void SetEra(TimeEra era)
    {
        if (era == CurrentEra)
        {
            return;
        }

        CurrentEra = era;
        Apply(era);
    }

    private void Apply(TimeEra era)
    {
        if (GameManager.Instance != null)
        {
            GameManager.Instance.State.currentEra = era;
        }

        EraChanged?.Invoke(era);
    }
}
