using UnityEngine;

/// <summary>
/// Ceiling debris that drops when Noa walks underneath and hurts on impact.
///
/// This is one of the graded collisions (T4), and deliberately a real
/// <see cref="OnCollisionEnter"/> that reads the contact data rather than a
/// trigger volume that just fires: the damage is scaled by how fast the debris
/// was actually travelling when it connected, so a piece that has already
/// bounced and slowed does less than one that lands square on.
///
/// Note on why the collision lives HERE and not on the player: Noa moves with
/// a CharacterController, and a CharacterController never raises
/// OnCollisionEnter - it reports through OnControllerColliderHit instead. A
/// Rigidbody striking it, however, does get OnCollisionEnter, so the hazard is
/// the side that owns the response.
/// </summary>
[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(Collider))]
public sealed class FallingDebris : MonoBehaviour
{
    [Tooltip("How close Noa must get, in metres, before this lets go.")]
    [SerializeField] private float triggerRadius = 3.5f;

    [Tooltip("Damage at the reference impact speed below.")]
    [SerializeField] private int damageAtReferenceSpeed = 22;

    [Tooltip("Impact speed, in m/s, that deals the full damage above.")]
    [SerializeField] private float referenceSpeed = 9f;

    [Tooltip("Impact speed below which the hit is ignored entirely.")]
    [SerializeField] private float minimumSpeed = 2.5f;

    [SerializeField] private float despawnSeconds = 6f;

    private Rigidbody body;
    private Transform player;
    private Vector3 restPosition;
    private Quaternion restRotation;
    private bool released;
    private bool hasHitPlayer;

    /// <summary>True once this piece has actually struck Noa.</summary>
    public bool HasHitPlayer => hasHitPlayer;

    /// <summary>The impact speed of the last registered hit, in m/s.</summary>
    public float LastImpactSpeed { get; private set; }

    private void Awake()
    {
        body = GetComponent<Rigidbody>();

        // Held in place until released, so it does not simply fall at load.
        body.isKinematic = true;
        body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

        restPosition = transform.position;
        restRotation = transform.rotation;
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
        if (released || player == null)
        {
            return;
        }

        Vector3 flat = player.position - transform.position;
        flat.y = 0f;

        if (flat.sqrMagnitude <= triggerRadius * triggerRadius)
        {
            Release();
        }
    }

    /// <summary>Lets go of the debris. Public so a trigger can drop it too.</summary>
    public void Release()
    {
        if (released)
        {
            return;
        }

        released = true;
        body.isKinematic = false;

        // A small sideways nudge so it does not fall in a perfectly straight
        // line, which reads as scripted rather than structural.
        body.AddForce(new Vector3(Random.Range(-0.6f, 0.6f), 0f,
                                  Random.Range(-0.6f, 0.6f)),
                      ForceMode.VelocityChange);

        Invoke(nameof(Restore), despawnSeconds);
    }

    private void OnCollisionEnter(Collision collision)
    {
        // relativeVelocity is the reason this is a collision and not a
        // trigger: it is the only thing that distinguishes a lethal drop from
        // a piece rolling gently into an ankle.
        float speed = collision.relativeVelocity.magnitude;
        LastImpactSpeed = speed;

        if (!collision.gameObject.CompareTag("Player") || speed < minimumSpeed)
        {
            return;
        }

        if (hasHitPlayer)
        {
            return;
        }

        hasHitPlayer = true;

        int damage = Mathf.Max(
            1,
            Mathf.RoundToInt(damageAtReferenceSpeed * (speed / referenceSpeed)));

        if (GameManager.Instance != null)
        {
            GameManager.Instance.TakeDamage(damage);
        }

        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.Play(AudioManager.Sfx.OrbImpact);
        }
    }

    /// <summary>Puts the piece back so the hazard can be met again.</summary>
    private void Restore()
    {
        body.isKinematic = true;
        body.linearVelocity = Vector3.zero;
        body.angularVelocity = Vector3.zero;

        transform.SetPositionAndRotation(restPosition, restRotation);

        released = false;
        hasHitPlayer = false;
    }
}
