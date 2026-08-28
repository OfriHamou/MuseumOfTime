using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// A Chronological Shadow: agent type B, smaller and more agile than a
/// Warden, so it reaches places the Wardens cannot.
///
/// It is not a reskinned Warden. Per the GDD it does not speak and endlessly
/// repeats one gesture from its past; mechanically it is drawn to Time Shards
/// and steals them, which makes it a threat to the score rather than to
/// Noa's health, and gives the player a reason to fear something that never
/// attacks.
///
/// It also carries the Seek and Flee steering behaviours, the counterpart to
/// the Warden's Pursue.
/// </summary>
[RequireComponent(typeof(NavMeshAgent))]
public sealed class ShadowAI : MonoBehaviour, IFreezable
{
    public enum State
    {
        Drift,
        SeekShard,
        Flee,
        Frozen,
    }

    [SerializeField] private float seekRadius = 18f;
    [SerializeField] private float fleeDistance = 10f;
    [SerializeField] private float stealRange = 1.2f;
    [SerializeField] private int scorePenaltyOnSteal = 60;

    [Header("Fairness")]
    [Tooltip("Off in MuseumNight, where the Shadow is only there to be met.")]
    [SerializeField] private bool canStealShards = true;

    [Tooltip("Seconds after the scene loads before it will hunt anything.")]
    [SerializeField] private float huntDelaySeconds = 30f;

    [Tooltip("It only goes for a shard the player is near enough to contest.")]
    [SerializeField] private float playerWitnessRadius = 22f;

    private NavMeshAgent agent;
    private Transform player;

    private State state = State.Drift;
    private float frozenUntil;
    private Vector3 home;

    // Held rather than destroyed, so freezing can hand them back.
    private readonly List<ShardPickup> carried = new List<ShardPickup>();

    public State CurrentState => state;

    /// <summary>How many shards this Shadow has taken.</summary>
    public int StolenShards { get; private set; }

    /// <summary>How many stolen shards it is still carrying.</summary>
    public int CarriedShards => carried.Count;

    private void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        home = transform.position;
    }

    private void Start()
    {
        GameObject found = GameObject.FindGameObjectWithTag("Player");

        if (found != null)
        {
            player = found.transform;
        }
    }

    private void Update()
    {
        if (state == State.Frozen)
        {
            if (Time.time >= frozenUntil)
            {
                state = State.Drift;

                if (agent.isOnNavMesh)
                {
                    agent.isStopped = false;
                }
            }

            return;
        }

        if (!agent.isOnNavMesh)
        {
            return;
        }

        // Flee takes priority: a raised orb is the one thing it avoids.
        if (PlayerIsThreatening())
        {
            state = State.Flee;

            agent.SetDestination(SteeringBehaviours.Flee(
                transform.position, player.position, fleeDistance));

            return;
        }

        ShardPickup shard = CanHunt() ? NearestShard() : null;

        if (shard != null)
        {
            state = State.SeekShard;

            agent.SetDestination(SteeringBehaviours.Seek(
                transform.position, shard.transform.position));

            // Horizontal only. The agent's transform rides a baseOffset and
            // the shards float, so a straight distance carried well over a
            // metre of pure vertical error against a 1.2 m range - the Shadow
            // could stand directly on a shard, measure itself out of range and
            // circle it indefinitely.
            Vector3 gap = shard.transform.position - transform.position;
            gap.y = 0f;

            if (gap.magnitude <= stealRange)
            {
                Steal(shard);
            }

            return;
        }

        // Nothing to chase: drift around where it was placed.
        state = State.Drift;

        if (!agent.pathPending &&
            agent.remainingDistance <= agent.stoppingDistance + 0.2f)
        {
            Vector2 offset = Random.insideUnitCircle * 5f;
            agent.SetDestination(home + new Vector3(offset.x, 0f, offset.y));
        }
    }

    private bool PlayerIsThreatening()
    {
        if (player == null)
        {
            return false;
        }

        var launcher = player.GetComponent<ChronoOrbLauncher>();

        return launcher != null &&
               launcher.ThrownCount > 0 &&
               Vector3.Distance(transform.position, player.position) < 6f;
    }

    /// <summary>
    /// Whether it is allowed to go after a shard at all yet.
    ///
    /// Without this the Shadow simply took every Time Shard in the scene
    /// within the first few seconds - before the player had walked ten metres
    /// - so the score dropped for reasons the player never saw and there was
    /// nothing left to collect. A theft the player cannot witness or contest
    /// is not a mechanic, it is just an unexplained penalty.
    ///
    /// So: a grace period at the start, and it only hunts while the player is
    /// close enough to see it happen and do something about it.
    /// </summary>
    private bool CanHunt()
    {
        // MuseumNight is the teaching scene - Part 3 of the plan puts the
        // Shadow's threat in FrozenCity onward. It still appears here, on its
        // own navmesh bake, so the player meets one and learns what it is
        // before it can cost them anything; with stealing on it simply took
        // both of the scene's two Time Shards within the first half minute,
        // before the player had been taught the Orb that answers it.
        if (!canStealShards)
        {
            return false;
        }

        if (Time.timeSinceLevelLoad < huntDelaySeconds)
        {
            return false;
        }

        if (player == null)
        {
            return false;
        }

        return Vector3.Distance(transform.position, player.position) <= playerWitnessRadius;
    }

    private ShardPickup NearestShard()
    {
        ShardPickup[] shards =
            Object.FindObjectsByType<ShardPickup>(FindObjectsSortMode.None);

        ShardPickup best = null;
        float bestDistance = seekRadius;

        foreach (ShardPickup shard in shards)
        {
            float distance =
                Vector3.Distance(transform.position, shard.transform.position);

            if (distance < bestDistance)
            {
                bestDistance = distance;
                best = shard;
            }
        }

        return best;
    }

    private void Steal(ShardPickup shard)
    {
        StolenShards++;
        carried.Add(shard);

        if (GameManager.Instance != null)
        {
            GameManager.Instance.RemoveScore(scorePenaltyOnSteal);
        }

        // Deactivated, NOT destroyed. The design calls a stolen shard
        // "recoverable by freezing it", which is impossible once the object
        // is gone - the previous version destroyed it outright, so the
        // recovery half of the mechanic did not exist.
        shard.gameObject.SetActive(false);

        HudMessageFeed.Post(
            "A Shadow stole a Time Shard!  -" + scorePenaltyOnSteal + " score",
            HudMessageFeed.Tone.Bad);

        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.Play(AudioManager.Sfx.Capture);
        }
    }

    /// <summary>
    /// Held still by a Chrono Orb hit - and made to give back what it took.
    /// This is the whole reason the Shadow is a threat you can answer rather
    /// than one you can only avoid.
    /// </summary>
    public void Freeze(float seconds)
    {
        state = State.Frozen;
        frozenUntil = Time.time + seconds;

        if (agent.isOnNavMesh)
        {
            agent.isStopped = true;
        }

        DropCarriedShards();
    }

    private void DropCarriedShards()
    {
        if (carried.Count == 0)
        {
            HudMessageFeed.Post("Shadow frozen", HudMessageFeed.Tone.Good);
            return;
        }

        int recovered = 0;

        foreach (ShardPickup shard in carried)
        {
            if (shard == null)
            {
                continue;
            }

            // Scattered slightly so several shards do not stack into one.
            Vector2 offset = Random.insideUnitCircle * 1.2f;
            shard.transform.position =
                transform.position + new Vector3(offset.x, 0.5f, offset.y);

            shard.gameObject.SetActive(true);
            recovered++;

            if (GameManager.Instance != null)
            {
                GameManager.Instance.AddScore(scorePenaltyOnSteal);
            }
        }

        carried.Clear();

        HudMessageFeed.Post(
            "Shadow frozen - " + recovered + " Time Shard" +
            (recovered == 1 ? "" : "s") + " recovered",
            HudMessageFeed.Tone.Good);
    }
}
