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

    private void OnCollisionEnter(Collision collision)
    {
        if (collision.gameObject.GetComponent<ChronoOrb>() != null)
        {
            RegisterOrbHit();
        }
    }

    /// <summary>
    /// The actual phase-transition logic, separated from OnCollisionEnter so
    /// it can be exercised directly - Unity's Collision has no public
    /// constructor, so a test cannot fabricate one to drive the message
    /// method itself.
    /// </summary>
    private void RegisterOrbHit()
    {
        if (stage == Stage.Defeated)
        {
            return;
        }

        TimeEra era = EraManager.Instance != null ? EraManager.Instance.CurrentEra : TimeEra.Present;

        switch (stage)
        {
            case Stage.Shielded:
                if (era != TimeEra.Past)
                {
                    return;
                }

                hitsTaken++;

                if (hitsTaken >= hitsToBreakShield)
                {
                    if (shieldVisual != null) shieldVisual.SetActive(false);
                    if (summonedWarden != null) summonedWarden.SetActive(true);
                    stage = Stage.Present;
                }

                break;

            case Stage.Present:
                if (era != TimeEra.Present)
                {
                    return;
                }

                stage = Stage.Future;
                erosionBeginsAt = Time.time + erosionGraceSeconds;

                HudMessageFeed.Post(
                    "The moment is erasing you - hold CTRL to slow time",
                    HudMessageFeed.Tone.Bad);

                break;

            case Stage.Future:
                if (era != TimeEra.Future || !TimeIsSlowed())
                {
                    return;
                }

                stage = Stage.Defeated;
                Defeat();
                break;
        }
    }

    private void Defeat()
    {
        if (sceneLoader != null)
        {
            sceneLoader.LoadScene("Victory");
        }
    }
}
