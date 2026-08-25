using UnityEngine;

/// <summary>
/// Base class for the trigger volumes: fires once when the player walks in.
///
/// The requirement asks for at least four triggers, and the subclasses are
/// genuinely different components rather than one script wearing four hats,
/// because four copies of the same thing would be hard to defend.
/// </summary>
[RequireComponent(typeof(Collider))]
public abstract class PlayerTrigger : MonoBehaviour
{
    [SerializeField] protected bool onlyOnce = true;

    private bool spent;

    /// <summary>True once this trigger has fired at least once.</summary>
    public bool HasFired { get; private set; }

    protected virtual void Awake()
    {
        GetComponent<Collider>().isTrigger = true;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (spent || !IsPlayer(other))
        {
            return;
        }

        if (onlyOnce)
        {
            spent = true;
        }

        HasFired = true;
        TriggerLog.Record(GetType().Name);
        OnPlayerEntered(other.gameObject);
    }

    private void OnTriggerStay(Collider other)
    {
        if (IsPlayer(other))
        {
            OnPlayerStaying(other.gameObject);
        }
    }

    protected static bool IsPlayer(Collider other)
    {
        return other.CompareTag("Player") ||
               other.GetComponentInParent<PlayerController>() != null;
    }

    protected abstract void OnPlayerEntered(GameObject player);

    protected virtual void OnPlayerStaying(GameObject player)
    {
    }
}
