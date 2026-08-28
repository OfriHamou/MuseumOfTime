using UnityEngine;

/// <summary>
/// The Chrono Hourglass: hold Ctrl to slow the world down.
///
/// This is the second of the two items carried between scenes. It is found in
/// FrozenCity and required in ClockCore, which is part of what makes the
/// three scenes one game rather than three demos.
/// </summary>
public sealed class ChronoHourglass : MonoBehaviour
{
    [SerializeField] private float slowScale = 0.3f;
    [Tooltip("Energy per second while slowing time. At 18 a full bar bought " +
             "barely five seconds, which is less than one aimed throw takes " +
             "once the slowdown has stretched the orb's flight.")]
    [SerializeField] private float energyDrainPerSecond = 9f;

    [Tooltip("Ignore the item flag. For testing the first scene only.")]
    [SerializeField] private bool alwaysAvailable;

    private PlayerInputReader inputReader;
    private bool active;

    /// <summary>True while time is being slowed.</summary>
    public bool IsSlowing => active;

    /// <summary>True when Noa is carrying the Hourglass.</summary>
    public bool IsAvailable =>
        alwaysAvailable ||
        (GameManager.Instance != null &&
         GameManager.Instance.State.hasChronoHourglass);

    private void Awake()
    {
        inputReader = GetComponent<PlayerInputReader>();
    }

    private void OnDisable()
    {
        // Never leave the world in slow motion because this was switched off.
        Restore();
    }

    private void Update()
    {
        if (inputReader == null)
        {
            return;
        }

        bool wanted = inputReader.IsSlowTimeHeld && IsAvailable;

        if (wanted && !active)
        {
            Begin();
        }
        else if (!wanted && active)
        {
            Restore();
        }

        if (!active)
        {
            return;
        }

        // Drain uses UNSCALED time. Scaled time is slowed by definition, so
        // draining on it would make the ability cost less the longer it ran.
        float cost = energyDrainPerSecond * Time.unscaledDeltaTime;

        if (GameManager.Instance != null &&
            !GameManager.Instance.SpendEnergy(cost))
        {
            Restore();
        }
    }

    private void Begin()
    {
        active = true;
        Time.timeScale = slowScale;

        // Physics steps must shrink with time, or collisions get sloppy and
        // fast objects start tunnelling through walls.
        Time.fixedDeltaTime = 0.02f * slowScale;

        // AudioManager already reacts to IsSlowing (low-pass filter + a
        // pitch sting), but that alone is easy to miss under museum ambience.
        // A HUD toast, the same feedback pattern already used for warden
        // spotting and low-energy throws, makes the moment unambiguous.
        HudMessageFeed.Post("Slow Time engaged", HudMessageFeed.Tone.Good);
    }

    private void Restore()
    {
        if (!active)
        {
            return;
        }

        active = false;

        // Back to exactly 1, never to whatever it happened to be before.
        Time.timeScale = 1f;
        Time.fixedDeltaTime = 0.02f;

        HudMessageFeed.Post("Slow Time ended", HudMessageFeed.Tone.Good);
    }
}
