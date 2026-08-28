using UnityEngine;

/// <summary>
/// ClockCore's boss. Three phases, one per era - switching era with Q/R IS
/// the fight mechanic here, not a side ability, exactly as the plan frames
/// it. A Chrono Orb hit only counts while Noa is standing in the era that
/// phase requires, so winning means using the era system, not just aiming.
///
///   Phase 1 (Past)    - shielded; break the shield with the orb (T4).
///   Phase 2 (Present) - the Collector summons a Warden.
///   Phase 3 (Future)  - time is eroding Noa; the Hourglass is mandatory to
///                       survive it, not merely helpful, and only a hit
///                       landed while it is active finishes the fight. This
///                       stands in for the GDD's Restorer "undo" mechanic:
///                       failing to slow time here costs Noa health for as
///                       long as she goes without it, the simplest real
///                       analogue of time being undone around her.
/// </summary>
[RequireComponent(typeof(Collider))]
public sealed class Collector : MonoBehaviour
{
    /// <summary>
    /// Public so the on-screen objective can tell the player which era the
    /// fight currently needs - without that, the three-phase boss is a wall
    /// with no readable rule.
    /// </summary>
    public enum Stage
    {
        Shielded,
        Present,
        Future,
        Defeated,
    }

    [Header("Phase 1 - Past: shielded")]
    [SerializeField] private GameObject shieldVisual;
    [SerializeField] private int hitsToBreakShield = 2;

    [Header("Phase 2 - Present: summons a Warden")]
    [SerializeField] private GameObject summonedWarden;

    [Header("Phase 3 - Future: the Hourglass is mandatory to survive")]
    [Tooltip("Health lost per second while the erasing moment runs and time " +
             "is NOT slowed.")]
    [SerializeField] private float erosionDamagePerSecond = 6f;

    [Tooltip("Seconds of calm after the phase begins before the erosion " +
             "starts, so the phase can be read before it is survived.")]
    [SerializeField] private float erosionGraceSeconds = 4f;

    private float erosionBeginsAt;

    private ChronoHourglass playerHourglass;
    private SceneLoader sceneLoader;
    private Stage stage = Stage.Shielded;
    private int hitsTaken;

    /// <summary>True once all three phases are cleared.</summary>
    public bool IsDefeated => stage == Stage.Defeated;

    /// <summary>Which phase the fight is in right now.</summary>
    public Stage CurrentStage => stage;

    private void Awake()
    {
        sceneLoader = GetComponent<SceneLoader>();
    }

    private void Start()
    {
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
        {
            playerHourglass = player.GetComponent<ChronoHourglass>();
        }

        // Not summoned yet - that happens when the shield breaks, below.
        if (summonedWarden != null)
        {
            summonedWarden.SetActive(false);
        }
    }

    private void Update()
    {
        if (stage != Stage.Future || TimeIsSlowed())
        {
            return;
        }

        // A grace window, and a gentler rate than this started with.
        //
        // Phase 3 used to open at 12 health per second with no grace at all,
        // which gives a player with a full bar about eight seconds to read a
        // new objective, switch era, hold a key they may never have used, aim
        // and land a physics throw - and there is no running away from it,
        // because the erosion is not a place, it is the phase. Played
        // straight, it simply killed me: the phase began and I was dead before
        // I had finished reading what it wanted.
        //
        // Six per second after four seconds of calm is still a clock, and the
        // Hourglass still stops it completely - but it is a fight now rather
        // than a coin flip.
        if (Time.time < erosionBeginsAt)
        {
            return;
        }

        // "Mandatory to survive", not merely helpful: without the Hourglass
        // active, the erasing moment erodes Noa instead of the Collector.
        if (GameManager.Instance != null)
        {
            RespawnService.LastCauseOfDeath =
                "The erasing moment caught up with you. Hold CTRL to slow time.";

            GameManager.Instance.TakeDamage(Mathf.CeilToInt(erosionDamagePerSecond * Time.deltaTime));
        }
    }

    private bool TimeIsSlowed()
    {
        return playerHourglass != null && playerHourglass.IsSlowing;
    }

    private int lastOrbCounted;

    private void OnCollisionEnter(Collision collision)
    {
        var orb = collision.gameObject.GetComponent<ChronoOrb>();

        if (orb != null)
        {
            TakeOrbHit(orb);
        }
    }

    /// <summary>
    /// One entry point for an orb striking the Collector, however it arrived.
    ///
    /// The physical capsule is only a metre across, which is a hard thing to
    /// hit with a thrown, arcing projectile from across a chamber. A wider
    /// trigger volume forwards to this as well, so a throw that passes close
    /// counts - and this guards against the same orb being counted twice when
    /// it clips both.
    /// </summary>
    public void TakeOrbHit(ChronoOrb orb)
    {
        if (orb != null)
        {
            int id = orb.GetInstanceID();

            if (id == lastOrbCounted)
            {
                return;
            }

            lastOrbCounted = id;
        }

        RegisterOrbHit(orb);
    }

    /// <summary>
    /// The actual phase-transition logic, separated from OnCollisionEnter so
    /// it can be exercised directly - Unity's Collision has no public
    /// constructor, so a test cannot fabricate one to drive the message
    /// method itself.
    /// </summary>
    private void RegisterOrbHit(ChronoOrb orb = null)
    {
        if (stage == Stage.Defeated)
        {
            return;
        }

        TimeEra era = EraManager.Instance != null ? EraManager.Instance.CurrentEra : TimeEra.Present;

        // EVERY hit says something now.
        //
        // Each of these used to be a bare return: the player threw an orb, it
        // struck the Collector, and nothing happened at all - no sound, no
        // message, no flicker. There was no way to learn the rule from playing,
        // because the game's answer to a wrong-era hit and its answer to a
        // miss were identical, and the answer to a correct-but-not-final hit
        // was identical too. "Throwing does nothing to the boss" is the only
        // conclusion available.
        switch (stage)
        {
            case Stage.Shielded:
                if (era != TimeEra.Past)
                {
                    RejectHit(TimeEra.Past, era);
                    return;
                }

                hitsTaken++;

                if (hitsTaken >= hitsToBreakShield)
                {
                    if (shieldVisual != null) shieldVisual.SetActive(false);
                    if (summonedWarden != null) summonedWarden.SetActive(true);
                    stage = Stage.Present;

                    HudMessageFeed.Post(
                        "Shield broken! It has summoned a Warden - now hit it " +
                        "in the PRESENT (press R)",
                        HudMessageFeed.Tone.Good);
                }
                else
                {
                    int left = Mathf.Max(1, hitsToBreakShield - hitsTaken);

                    HudMessageFeed.Post(
                        "The shield cracks - " + left + " more hit" +
                        (left == 1 ? "" : "s") + " in the PAST",
                        HudMessageFeed.Tone.Good);
                }

                break;

            case Stage.Present:
                if (era != TimeEra.Present)
                {
                    RejectHit(TimeEra.Present, era);
                    return;
                }

                stage = Stage.Future;
                erosionBeginsAt = Time.time + erosionGraceSeconds;

                HudMessageFeed.Post(
                    "It retreats into the FUTURE. Press R, then HOLD CTRL to " +
                    "slow time and hit it while you hold it",
                    HudMessageFeed.Tone.Good);

                break;

            case Stage.Future:
                if (era != TimeEra.Future)
                {
                    RejectHit(TimeEra.Future, era);
                    return;
                }

                // Slowed now, OR slowed when the throw was made.
                //
                // Requiring it at impact alone meant holding the key through a
                // flight the slowdown had itself stretched by over three
                // times - often longer than a full energy bar lasts, so a
                // correct throw would land a moment late and silently not
                // count.
                bool slowed = TimeIsSlowed() ||
                              (orb != null && orb.ThrownWhileTimeSlowed);

                if (!slowed)
                {
                    HudMessageFeed.Post(
                        "The orb passes straight through - HOLD CTRL to slow " +
                        "time, and throw while you are holding it",
                        HudMessageFeed.Tone.Bad);

                    return;
                }

                stage = Stage.Defeated;
                Defeat();
                break;
        }
    }

    /// <summary>
    /// Says why an orb did nothing, naming the era needed and the key for it.
    /// </summary>
    private static void RejectHit(TimeEra needed, TimeEra current)
    {
        string key = needed == TimeEra.Past ? "Q"
            : needed == TimeEra.Future ? "R" : "Q or R";

        HudMessageFeed.Post(
            "The Collector is untouchable in the " + current.ToString().ToUpperInvariant() +
            " - it can only be hurt in the " + needed.ToString().ToUpperInvariant() +
            ". Press " + key + ".",
            HudMessageFeed.Tone.Bad);
    }

    private void Defeat()
    {
        if (sceneLoader != null)
        {
            sceneLoader.LoadScene("Victory");
        }
    }
}
