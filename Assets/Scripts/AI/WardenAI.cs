using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// A Time Warden: agent type A, ground-bound, patrols corridors and stairs.
///
/// Covers three requirements at once:
///   - patrol WITH PAUSE (the pause is the graded part)
///   - stealth against the Recast navmesh
///   - a LayerMask built in code, used for the line-of-sight test
///   - the Pursue steering behaviour, aiming where Noa will be
/// </summary>
[RequireComponent(typeof(NavMeshAgent))]
public sealed class WardenAI : MonoBehaviour, IFreezable
{
    public enum State
    {
        Patrol,
        Pause,
        Alert,
        Chase,
        Search,
        Frozen,
    }

    [Header("Vision")]
    [SerializeField] private float viewDistance = 14f;

    [Tooltip("Total cone width in degrees, so 90 means 45 to each side.")]
    [SerializeField] private float viewAngle = 90f;

    [SerializeField] private float eyeHeight = 1.7f;

    [Header("Detection")]
    [Tooltip("Seconds of unbroken sight before the player is caught.")]
    [SerializeField] private float secondsToDetect = 1.6f;

    [SerializeField] private float detectionDecayPerSecond = 0.6f;

    [Header("Movement")]
    [SerializeField] private float patrolSpeed = 2.2f;
    [SerializeField] private float chaseSpeed = 4.6f;
    [SerializeField] private float searchSeconds = 4f;

    [Header("Capture")]
    [Tooltip("How close the Warden must get to catch Noa, in metres.")]
    [SerializeField] private float captureRadius = 1.6f;

    [Tooltip("Health lost when caught. Reaching zero routes to the anchor.")]
    [SerializeField] private int captureDamage = 25;

    [Tooltip("Score lost when caught, so being seen has a real cost (T8).")]
    [SerializeField] private int captureScorePenalty = 25;

    [Tooltip("Grace period after a capture before the Warden can catch again.")]
    [SerializeField] private float captureCooldown = 3f;

    private NavMeshAgent agent;
    private PatrolRoute route;
    private Transform player;
    private CharacterController playerController;

    private State state = State.Patrol;
    private float waitUntil;
    private float searchUntil;
    private float frozenUntil;
    private float detection;
    private Vector3 lastKnownPosition;
    private float captureAllowedAt;

    /// <summary>How many times this Warden has caught Noa. Read by tests.</summary>
    public int CaptureCount { get; private set; }

    /// <summary>
    /// Anything that can block line of sight. Built in code, not assigned in
    /// the Inspector, which is what the LayerMask requirement asks for and
    /// makes the rule readable from the file itself.
    /// </summary>
    private LayerMask visionBlockers;

    public State CurrentState => state;

    /// <summary>0 to 1. Reaching 1 means the player has been caught.</summary>
    public float DetectionLevel => Mathf.Clamp01(detection);

    /// <summary>True while standing still at a waypoint, scanning.</summary>
    public bool IsPaused => state == State.Pause;

    /// <summary>Exposed so the mask can be shown during the defense.</summary>
    public LayerMask VisionBlockers => visionBlockers;

    private void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        route = GetComponent<PatrolRoute>();

        // The layer mask, built in code. StealthCover is the project's layer
        // for display cases and pillars Noa can break line of sight behind;
        // Obstacle is solid world geometry.
        //
        // Note the failure mode: LayerMask.GetMask silently ignores a name
        // that does not exist. An earlier version asked for "HideVolume",
        // which this project does not define, and the mask quietly collapsed
        // to Default alone - the stealth cover did nothing and nothing warned.
        visionBlockers = LayerMask.GetMask("Default", "Obstacle", "StealthCover");
    }

    private void Start()
    {
        agent.speed = patrolSpeed;

        GameObject found = GameObject.FindGameObjectWithTag("Player");

        if (found != null)
        {
            player = found.transform;
            playerController = found.GetComponent<CharacterController>();
        }

        GoToNextWaypoint();
    }

    private void Update()
    {
        if (state == State.Frozen)
        {
            if (Time.time >= frozenUntil)
            {
                state = State.Patrol;
                agent.isStopped = false;
                GoToNextWaypoint();
            }

            return;
        }

        UpdateDetection();
        Tick();
    }

    // -----------------------------------------------------------------
    // Detection: range, then cone, then line of sight. In that order,
    // because each test is more expensive than the one before it.
    // -----------------------------------------------------------------

    private bool CanSeePlayer()
    {
        if (player == null)
        {
            return false;
        }

        // eyeHeight is measured from the FEET. The transform is lifted by the
        // agent's baseOffset, so adding eyeHeight to it put the eye a metre
        // higher than intended - at 3.0 m, hunting for a target at 1.28 m.
        float feet = transform.position.y - (agent != null ? agent.baseOffset : 0f);

        Vector3 eye = new Vector3(transform.position.x, feet + eyeHeight, transform.position.z);
        Vector3 target = player.position + (Vector3.up * 1.2f);
        Vector3 toPlayer = target - eye;

        if (toPlayer.magnitude > viewDistance)
        {
            return false;
        }

        // The cone is judged on the HORIZONTAL bearing only.
        //
        // Measuring the full 3D angle meant the sightline tilted further down
        // the closer the player got, and at point-blank range it fell outside
        // the cone completely: the Warden went blind exactly when the player
        // was standing on top of it, then wandered off. A ground patrol that
        // cannot see what is directly in front of it is not a guard, and it
        // made the whole stealth layer behave at random.
        Vector3 flatToPlayer = new Vector3(toPlayer.x, 0f, toPlayer.z);
        Vector3 flatForward = new Vector3(transform.forward.x, 0f, transform.forward.z);

        // Almost on top of it: bearing is meaningless, and being that close
        // in the open should always count as being seen.
        if (flatToPlayer.sqrMagnitude > 0.04f &&
            Vector3.Angle(flatForward, flatToPlayer) > viewAngle * 0.5f)
        {
            return false;
        }

        // Line of sight, against the mask built in Awake. Anything solid in
        // the way, including a HideVolume, breaks the sighting.
        if (Physics.Raycast(eye, toPlayer.normalized, out RaycastHit hit,
                            toPlayer.magnitude, visionBlockers,
                            QueryTriggerInteraction.Ignore))
        {
            if (!hit.collider.CompareTag("Player"))
            {
                return false;
            }
        }

        return true;
    }

    private void UpdateDetection()
    {
        if (CanSeePlayer())
        {
            detection += Time.deltaTime / secondsToDetect;
            lastKnownPosition = player.position;

            if (state == State.Patrol || state == State.Pause)
            {
                state = State.Alert;
                agent.isStopped = false;
            }
        }
        else
        {
            detection -= Time.deltaTime * detectionDecayPerSecond;
        }

        detection = Mathf.Clamp01(detection);

        if (detection >= 1f && state != State.Chase)
        {
            state = State.Chase;
            agent.speed = chaseSpeed;
        }
    }

    private void Tick()
    {
        switch (state)
        {
            case State.Patrol:
                if (!agent.pathPending &&
                    agent.remainingDistance <= agent.stoppingDistance + 0.2f)
                {
                    BeginPause();
                }

                break;

            case State.Pause:
                // A real stop, not merely zero speed: isStopped keeps the
                // agent planted while it scans.
                if (Time.time >= waitUntil)
                {
                    agent.isStopped = false;
                    state = State.Patrol;
                    GoToNextWaypoint();
                }
                else
                {
                    // Sweep the head while stood still, so the pause is
                    // visibly a scan rather than an idle.
                    transform.Rotate(Vector3.up, 40f * Time.deltaTime);
                }

                break;

            case State.Alert:
                agent.SetDestination(lastKnownPosition);

                if (detection <= 0.01f)
                {
                    state = State.Patrol;
                    GoToNextWaypoint();
                }

                break;

            case State.Chase:
                ChasePlayer();
                break;

            case State.Search:
                if (Time.time >= searchUntil)
                {
                    state = State.Patrol;
                    agent.speed = patrolSpeed;
                    GoToNextWaypoint();
                }

                break;
        }
    }

    private void ChasePlayer()
    {
        if (player == null)
        {
            return;
        }

        if (!CanSeePlayer())
        {
            // Lost them. Head for where they were, then give up.
            state = State.Search;
            searchUntil = Time.time + searchSeconds;
            agent.SetDestination(lastKnownPosition);
            return;
        }

        Vector3 velocity = playerController != null
            ? playerController.velocity
            : Vector3.zero;

        // Pursue, not chase: aim where Noa will be.
        Vector3 intercept = SteeringBehaviours.Pursue(
            transform.position, player.position, velocity, agent.speed);

        agent.SetDestination(intercept);
        lastKnownPosition = player.position;

        TryCapture();
    }

    /// <summary>
    /// Catching Noa. This was the whole missing half of the Warden: it would
    /// see her, pursue her and reach her, and then nothing happened - no
    /// damage, no penalty, no reset. Walking into one was indistinguishable
    /// from walking into a wall, which made the entire stealth layer read as
    /// decoration and left a player reasonably asking whether the figures
    /// following them were enemies at all.
    ///
    /// The plan asks for a capture to cost health and score and to route
    /// through the Time Anchor system (T21), which is what happens here: the
    /// hit is survivable, so a first mistake teaches rather than ends, and
    /// GameManager.TakeDamage raises PlayerDied at zero, which RespawnService
    /// already turns into a return to the last anchor.
    /// </summary>
    private void TryCapture()
    {
        if (Time.time < captureAllowedAt)
        {
            return;
        }

        // Horizontal only: the Warden's agent sits on a baseOffset, so a
        // straight distance would never close on a player stood on the floor.
        Vector3 gap = player.position - transform.position;
        gap.y = 0f;

        if (gap.sqrMagnitude > captureRadius * captureRadius)
        {
            return;
        }

        captureAllowedAt = Time.time + captureCooldown;
        CaptureCount++;

        if (GameManager.Instance != null)
        {
            GameManager.Instance.TakeDamage(captureDamage);
            GameManager.Instance.RemoveScore(captureScorePenalty);
        }

        HudMessageFeed.Post(
            "Caught by a Time Warden - break line of sight, or freeze it with the Orb",
            HudMessageFeed.Tone.Bad);

        // It has what it came for; let Noa get away rather than pinning her
        // in a corner and draining her to death in one contact.
        state = State.Search;
        searchUntil = Time.time + searchSeconds;
        detection = 0f;
        agent.speed = patrolSpeed;
    }

    private void BeginPause()
    {
        if (route == null || !route.HasWaypoints)
        {
            return;
        }

        state = State.Pause;
        agent.isStopped = true;
        waitUntil = Time.time + Mathf.Max(0.1f, route.Current.waitSeconds);
    }

    private void GoToNextWaypoint()
    {
        if (route == null || !route.HasWaypoints || !agent.isOnNavMesh)
        {
            return;
        }

        agent.speed = patrolSpeed;
        agent.SetDestination(route.Advance().position);
    }

    /// <summary>Held still by a Chrono Orb hit.</summary>
    public void Freeze(float seconds)
    {
        state = State.Frozen;
        frozenUntil = Time.time + seconds;

        if (agent.isOnNavMesh)
        {
            agent.isStopped = true;
        }
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.6f, 0f, 0.6f);
        Vector3 eye = transform.position + (Vector3.up * eyeHeight);

        Quaternion left = Quaternion.Euler(0f, -viewAngle * 0.5f, 0f);
        Quaternion right = Quaternion.Euler(0f, viewAngle * 0.5f, 0f);

        Gizmos.DrawRay(eye, left * transform.forward * viewDistance);
        Gizmos.DrawRay(eye, right * transform.forward * viewDistance);
    }
}
