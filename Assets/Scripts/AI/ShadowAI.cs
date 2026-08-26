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

    private NavMeshAgent agent;
    private Transform player;

    private State state = State.Drift;
    private float frozenUntil;
    private Vector3 home;

    public State CurrentState => state;

    /// <summary>How many shards this Shadow has taken.</summary>
    public int StolenShards { get; private set; }

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

        ShardPickup shard = NearestShard();

        if (shard != null)
        {
            state = State.SeekShard;

            agent.SetDestination(SteeringBehaviours.Seek(
                transform.position, shard.transform.position));

            if (Vector3.Distance(transform.position, shard.transform.position)
                <= stealRange)
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

        if (GameManager.Instance != null)
        {
            GameManager.Instance.RemoveScore(scorePenaltyOnSteal);
        }

        Destroy(shard.gameObject);
    }

    /// <summary>Held still by a Chrono Orb hit, which is how a shard is recovered.</summary>
    public void Freeze(float seconds)
    {
        state = State.Frozen;
        frozenUntil = Time.time + seconds;

        if (agent.isOnNavMesh)
        {
            agent.isStopped = true;
        }
    }
}
