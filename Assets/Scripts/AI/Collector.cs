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
    private enum Stage
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
    [SerializeField] private float erosionDamagePerSecond = 12f;

    private ChronoHourglass playerHourglass;
    private SceneLoader sceneLoader;
    private Stage stage = Stage.Shielded;
    private int hitsTaken;

    /// <summary>True once all three phases are cleared.</summary>
    public bool IsDefeated => stage == Stage.Defeated;

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

        // "Mandatory to survive", not merely helpful: without the Hourglass
        // active, the erasing moment erodes Noa instead of the Collector.
        if (GameManager.Instance != null)
        {
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
