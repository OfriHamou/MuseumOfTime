using UnityEngine;

/// <summary>
/// Swaps an intact mesh for its Voronoi shards when the object breaks.
///
/// The shards are kept hidden and inert until the moment of the break, so the
/// cost of ~30 rigidbodies is only paid once, at the instant it matters. They
/// despawn afterwards: leaving dozens of physics bodies lying in the scene
/// costs frames for something the player has stopped looking at.
/// </summary>
public sealed class FracturedObject : MonoBehaviour
{
    [Header("Pieces")]
    [Tooltip("The whole, unbroken version. Hidden when the object shatters.")]
    [SerializeField] private GameObject intact;

    [Tooltip("Parent of the shards. Hidden until the break.")]
    [SerializeField] private GameObject shards;

    [Header("Break")]
    [SerializeField] private float explosionForce = 260f;
    [SerializeField] private float explosionRadius = 3.5f;
    [SerializeField] private float upwardModifier = 0.4f;

    [Tooltip("Seconds before the shards fade away. Zero leaves them forever.")]
    [SerializeField] private float shardLifetime = 8f;

    private bool broken;

    /// <summary>True once this object has shattered.</summary>
    public bool IsBroken => broken;

    /// <summary>Number of Voronoi shards this object breaks into.</summary>
    public int ShardCount => shards == null ? 0 : shards.transform.childCount;

    private void Awake()
    {
        if (shards != null)
        {
            shards.SetActive(false);
        }

        if (intact != null)
        {
            intact.SetActive(true);
        }
    }

    /// <summary>Shatters the object, pushing the shards out from a point.</summary>
    public void Break(Vector3 origin)
    {
        if (broken || shards == null)
        {
            return;
        }

        broken = true;

        if (intact != null)
        {
            intact.SetActive(false);
        }

        shards.SetActive(true);

        foreach (Rigidbody body in shards.GetComponentsInChildren<Rigidbody>())
        {
            body.isKinematic = false;

            body.AddExplosionForce(
                explosionForce,
                origin,
                explosionRadius,
                upwardModifier);
        }

        if (shardLifetime > 0f)
        {
            Destroy(shards, shardLifetime);
        }
    }

    /// <summary>Shatters from the object's own centre.</summary>
    [ContextMenu("Break")]
    public void Break()
    {
        Break(transform.position);
    }
}
