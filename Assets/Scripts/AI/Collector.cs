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
             "is NOT slowed. Lowered from 6 in the same balance pass that cut " +
             "the Hourglass's drain rate - a player still has to spend most " +
             "of the phase slowing time to survive, but a brief unslowed " +
             "moment (releasing Ctrl to let energy recover) no longer costs " +
             "a third of the health bar before they can react to it.")]
    [SerializeField] private float erosionDamagePerSecond = 4f;

    [Tooltip("Seconds of calm after the phase begins before the erosion " +
             "starts, so the phase can be read before it is survived.")]
    [SerializeField] private float erosionGraceSeconds = 4f;

    [Tooltip("Slowed hits needed to end the fight, instead of just one - so " +
             "the finish is a short final stretch rather than a single lucky " +
             "throw.")]
    [SerializeField] private int finalHitsRequired = 3;

    [Tooltip("Real seconds the explosion plays before Victory loads. Real " +
             "time, not game time, so it is not stretched by Slow Time still " +
             "being held when the last hit lands.")]
    [SerializeField] private float defeatDelaySeconds = 1.8f;

    private float erosionBeginsAt;
    private int finalHitsTaken;

    // Accumulates fractional damage between frames. TakeDamage only takes
    // whole ints, and CeilToInt on a single frame's share of a per-second
    // rate rounds any non-zero sliver up to a whole point - at 60fps that
    // turned "4 per second" into 1 point EVERY frame, i.e. 60 per second,
    // independent of the configured rate entirely. A full health bar was
    // gone in under two seconds any time erosion was live, which is what
    // "near-instant death" actually was: not the rate in the Inspector, but
    // this rounding turning it into a framerate multiple of itself.
    private float erosionDamageAccumulator;

    private ChronoHourglass playerHourglass;
    private SceneLoader sceneLoader;
    private Stage stage = Stage.Shielded;
    private int hitsTaken;

    /// <summary>True once all three phases are cleared.</summary>
    public bool IsDefeated => stage == Stage.Defeated;

    /// <summary>Which phase the fight is in right now.</summary>
    public Stage CurrentStage => stage;

    /// <summary>
    /// True whenever the erosion warning should be on screen: Phase 3 has
    /// begun and the Hourglass is not currently holding it back. The HUD is
    /// the only reader of this - nothing here depends on the HUD existing.
    /// </summary>
    public bool ErosionWarningActive => stage == Stage.Future && !TimeIsSlowed();

    /// <summary>True while the grace window is still running, before erosion can deal damage.</summary>
    public bool ErosionInGrace => stage == Stage.Future && Time.time < erosionBeginsAt;

    /// <summary>Seconds left in the grace window, floored at zero once it has elapsed.</summary>
    public float ErosionGraceRemaining => Mathf.Max(0f, erosionBeginsAt - Time.time);

    /// <summary>The grace window remaining, as a 0-1 fraction, for a fill bar.</summary>
    public float ErosionGraceFraction =>
        erosionGraceSeconds <= 0f ? 0f : Mathf.Clamp01(ErosionGraceRemaining / erosionGraceSeconds);

    /// <summary>
    /// One continuous meter spanning the whole fight, for the HUD boss bar.
    /// Every phase's own advance condition (a shield hit, the Present hit,
    /// the final slowed hit) already increments this - nothing here decides
    /// on its own what "counts", so the bar can never disagree with what
    /// actually landed. hitsToBreakShield + 2: one meaningful hit each for
    /// Present and Future, since both need exactly one correct-era strike.
    /// </summary>
    public int BossHitsLanded { get; private set; }

    private int BossHitsTotal => hitsToBreakShield + 1 + finalHitsRequired;

    /// <summary>How many of the final phase's required hits have landed. Read by VFX/SFX for per-hit feedback.</summary>
    public int FinalHitsTaken => finalHitsTaken;

    /// <summary>How many final-phase hits end the fight.</summary>
    public int FinalHitsRequired => finalHitsRequired;

    /// <summary>True while the fight is on and the bar should be visible.</summary>
    public bool BossBarActive => stage != Stage.Defeated;

    /// <summary>"SHIELD" while the barrier still holds, "INTEGRITY" once it is down.</summary>
    public string BossBarLabel => stage == Stage.Shielded ? "SHIELD" : "INTEGRITY";

    /// <summary>Remaining boss "health", as a 0-1 fraction, for a fill bar.</summary>
    public float BossProgressFraction =>
        BossHitsTotal <= 0 ? 0f : Mathf.Clamp01(1f - ((float)BossHitsLanded / BossHitsTotal));

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
        //
        // This names the CAUSE, not the fix - "Hold CTRL to slow time" used
        // to be printed right here, which handed a player who died reading
        // it the exact answer to a puzzle the fight is built around. A death
        // message's job is to explain why you died, not what to do instead.
        if (GameManager.Instance != null)
        {
            erosionDamageAccumulator += erosionDamagePerSecond * Time.deltaTime;
            int wholeDamage = Mathf.FloorToInt(erosionDamageAccumulator);

            if (wholeDamage > 0)
            {
                erosionDamageAccumulator -= wholeDamage;

                RespawnService.LastCauseOfDeath = "Consumed by temporal erosion.";
                GameManager.Instance.TakeDamage(wholeDamage);
            }
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
                BossHitsLanded++;

                if (hitsTaken >= hitsToBreakShield)
                {
                    if (shieldVisual != null) shieldVisual.SetActive(false);
                    if (summonedWarden != null) summonedWarden.SetActive(true);
                    stage = Stage.Present;

                    HudMessageFeed.Post(
                        "The shield shatters. Something else now stirs to defend it.",
                        HudMessageFeed.Tone.Good);
                }
                else
                {
                    int left = Mathf.Max(1, hitsToBreakShield - hitsTaken);

                    HudMessageFeed.Post(
                        "The shield cracks - " + left + " more hit" +
                        (left == 1 ? "" : "s") + " needed",
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
                BossHitsLanded++;
                erosionBeginsAt = Time.time + erosionGraceSeconds;

                HudMessageFeed.Post(
                    "It flees to a moment that has not happened yet.",
                    HudMessageFeed.Tone.Good);

                // The loud, one-time announcement that the phase itself has
                // begun. This used to be the only phase transition with no
                // warning at all: the player read "it flees to a moment that
                // has not happened yet", then started silently losing health
                // a few seconds later with nothing on screen explaining why.
                HudMessageFeed.Post(
                    "THE TIMELINE IS COLLAPSING\nTemporal erosion is consuming you.",
                    HudMessageFeed.Tone.Bad);

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
                        "The orb passes straight through - only a held moment could catch it.",
                        HudMessageFeed.Tone.Bad);

                    return;
                }

                finalHitsTaken++;
                BossHitsLanded++;

                if (finalHitsTaken >= finalHitsRequired)
                {
                    stage = Stage.Defeated;
                    StartCoroutine(DefeatSequence());
                }
                else
                {
                    int left = finalHitsRequired - finalHitsTaken;

                    HudMessageFeed.Post(
                        "The held moment scars it - " + left + " more hit" +
                        (left == 1 ? "" : "s") + " needed",
                        HudMessageFeed.Tone.Good);
                }

                break;
        }
    }

    /// <summary>
    /// Says an orb did nothing without naming the era needed - that is the
    /// puzzle. A wrong-era hit should read as a clear "not this one, try
    /// another" signal, not a printed answer key.
    /// </summary>
    private static void RejectHit(TimeEra needed, TimeEra current)
    {
        HudMessageFeed.Post(
            "The Collector shrugs off the strike - this is not its vulnerable age.",
            HudMessageFeed.Tone.Bad);
    }

    /// <summary>
    /// The short beat between the finishing hit and the scene change: stage
    /// is already Defeated so RegisterOrbHit's guard rejects anything else
    /// that lands, and the erosion in Update stops on its own the same way
    /// (it only runs while stage == Future). GameplayVfx/AudioManager pick up
    /// the explosion from IsDefeated flipping true, the same polling pattern
    /// they already use for a fracture breaking or a Warden freezing.
    /// </summary>
    private System.Collections.IEnumerator DefeatSequence()
    {
        // Landing this hit requires Slow Time, so time may still be scaled
        // down right now - waited in real seconds so the explosion is not
        // dragged out by whatever the player happened to be holding.
        Time.timeScale = 1f;
        Time.fixedDeltaTime = 0.02f;

        HudMessageFeed.Post("The Collector unravels.", HudMessageFeed.Tone.Good);

        yield return new WaitForSecondsRealtime(defeatDelaySeconds);

        Defeat();
    }

    private void Defeat()
    {
        if (sceneLoader != null)
        {
            sceneLoader.LoadScene("Victory");
        }
    }
}
