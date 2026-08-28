using UnityEngine;

/// <summary>
/// A wider, forgiving catch area around the Collector.
///
/// The Collector's own capsule is one metre across. Hitting that with a
/// thrown, arcing, gravity-affected projectile from across the chamber is a
/// genuinely hard throw, and a near miss produces exactly the same result as
/// doing nothing at all - the orb sails past and the fight does not move.
/// Every phase already asks the player to work out the right era and the right
/// key; the aiming does not also need to be the difficult part.
///
/// This is a trigger, not a solid, so it changes nothing about where the
/// player can walk - it only widens what counts as a hit. The Collector
/// itself guards against the same orb being counted twice if it clips both
/// this and the capsule.
/// </summary>
[RequireComponent(typeof(SphereCollider))]
public sealed class CollectorHitVolume : MonoBehaviour
{
    [SerializeField] private Collector collector;

    private void Awake()
    {
        if (collector == null)
        {
            collector = GetComponentInParent<Collector>();
        }

        var sphere = GetComponent<SphereCollider>();
        sphere.isTrigger = true;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (collector == null)
        {
            return;
        }

        var orb = other.GetComponentInParent<ChronoOrb>();

        if (orb != null)
        {
            collector.TakeOrbHit(orb);
        }
    }
}
