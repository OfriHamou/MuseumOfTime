using UnityEngine;

/// <summary>
/// The thrown glass sphere. This is the physical body the shooting
/// requirement asks for, and it is also how Noa stays a non-combatant: on
/// impact it freezes or rewinds what it hits, it never destroys anything that
/// was not already meant to break.
///
/// Impact is handled in OnCollisionEnter, using the contact data, so the
/// response scales with how hard the orb actually hit.
/// </summary>
[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(SphereCollider))]
public sealed class ChronoOrb : MonoBehaviour
{
    [SerializeField] private float freezeSeconds = 4f;

    [Tooltip("Impact speed below which nothing shatters, in metres/second.")]
    [SerializeField] private float breakSpeed = 6f;

    [SerializeField] private float lifetime = 8f;

    private Rigidbody body;
    private int bounces;

    /// <summary>How many surfaces this orb has struck.</summary>
    public int Bounces => bounces;

    /// <summary>Set when the orb has shattered something.</summary>
    public bool CausedBreak { get; private set; }

    /// <summary>
    /// Whether the Chrono Hourglass was running at the moment this orb was
    /// thrown.
    ///
    /// The Collector's last phase asks the player to "strike while time is
    /// slowed", and it used to test that at the moment of IMPACT. Slowing time
    /// also stretches the orb's flight by more than three times in real
    /// seconds, so satisfying it meant holding the key through a much longer
    /// flight - at 18 energy a second, frequently longer than the bar lasts.
    /// The throw would land a fraction after the Hourglass ran dry and simply
    /// not count, with the player having done everything right.
    ///
    /// Recording it at launch keeps the intent - you have to be slowing time
    /// to land the blow - without making it a stopwatch problem.
    /// </summary>
    public bool ThrownWhileTimeSlowed { get; set; }

    private void Awake()
    {
        body = GetComponent<Rigidbody>();
        Destroy(gameObject, lifetime);
    }

    private void OnCollisionEnter(Collision collision)
    {
        bounces++;

        // relativeVelocity is the whole point: a gentle tap and a hard throw
        // have to do different things, or "impact" means nothing.
        float speed = collision.relativeVelocity.magnitude;
        Vector3 point = collision.contacts[0].point;

        // 1. Something breakable, hit hard enough.
        var fractured = collision.gameObject.GetComponentInParent<FracturedObject>();

        if (fractured != null && speed >= breakSpeed)
        {
            fractured.Break(point);
            CausedBreak = true;
            return;
        }

        // 2. A hinge: let the physics push it, and wake it so a sleeping
        //    body still reacts.
        var hinged = collision.gameObject.GetComponentInParent<HingeJoint>();

        if (hinged != null)
        {
            hinged.GetComponent<Rigidbody>().WakeUp();
            return;
        }

        // 3. A living thing: freeze it rather than hurt it.
        var freezable = collision.gameObject.GetComponentInParent<IFreezable>();
        freezable?.Freeze(freezeSeconds);
    }

    /// <summary>Throws the orb along a direction.</summary>
    public void Launch(Vector3 direction, float force)
    {
        body.AddForce(direction.normalized * force, ForceMode.Impulse);
    }
}

