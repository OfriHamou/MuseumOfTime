using UnityEngine;

/// <summary>
/// A hinged pendulum that hurts and shoves Noa when it connects.
///
/// Third of the graded collisions (T4), and the one that makes the hinge
/// joints (T5) matter for gameplay rather than being scenery: the bob's speed
/// comes entirely from the HingeJoint's own physics, so how hard it hits
/// depends on where in its arc it catches you.
///
/// Only counts a hit while the pendulum is genuinely moving, so standing
/// against a resting bob is harmless.
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public sealed class SwingingHazard : MonoBehaviour
{
    [Tooltip("Damage dealt at the reference impact speed below.")]
    [SerializeField] private int damageAtReferenceSpeed = 15;

    [Tooltip("Impact speed, in m/s, that deals the full damage above.")]
    [SerializeField] private float referenceSpeed = 5f;

    [Tooltip("Impact speed below which the pendulum is treated as at rest.")]
    [SerializeField] private float minimumSpeed = 1.6f;

    [Tooltip("How far a hit shoves Noa away, in metres.")]
    [SerializeField] private float knockbackDistance = 0.6f;

    [Tooltip("Seconds before the same pendulum can hit again.")]
    [SerializeField] private float hitCooldown = 1.25f;

    private float nextHitTime;

    /// <summary>The impact speed of the last registered hit, in m/s.</summary>
    public float LastImpactSpeed { get; private set; }

    /// <summary>How many times this pendulum has connected with Noa.</summary>
    public int HitCount { get; private set; }

    private void OnCollisionEnter(Collision collision)
    {
        if (!collision.gameObject.CompareTag("Player"))
        {
            return;
        }

        float speed = collision.relativeVelocity.magnitude;
        LastImpactSpeed = speed;

        if (speed < minimumSpeed || Time.time < nextHitTime)
        {
            return;
        }

        nextHitTime = Time.time + hitCooldown;
        HitCount++;

        int damage = Mathf.Max(
            1,
            Mathf.RoundToInt(damageAtReferenceSpeed * (speed / referenceSpeed)));

        if (GameManager.Instance != null)
        {
            GameManager.Instance.TakeDamage(damage);
        }

        // Shove Noa along the contact normal. A CharacterController ignores
        // physics forces entirely, so the push has to be applied as an
        // explicit Move on the controller itself.
        var controller = collision.gameObject.GetComponent<CharacterController>();

        if (controller != null)
        {
            Vector3 away = -collision.contacts[0].normal;
            away.y = 0f;

            if (away.sqrMagnitude > 0.0001f)
            {
                // A single displacement, not a per-second velocity: a
                // CharacterController ignores physics forces, so the shove has
                // to be one explicit Move of a known distance.
                controller.Move(away.normalized * knockbackDistance);
            }
        }

        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.Play(AudioManager.Sfx.Bell);
        }
    }
}
