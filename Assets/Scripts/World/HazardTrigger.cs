using UnityEngine;

/// <summary>Trigger 4: a temporal rift, draining health while stood in.</summary>
public sealed class HazardTrigger : PlayerTrigger
{
    [SerializeField] private int damagePerTick = 5;
    [SerializeField] private float energyDrainPerTick = 4f;
    [SerializeField] private float tickSeconds = 0.5f;

    private float nextTick;

    protected override void Awake()
    {
        base.Awake();

        // A hazard has to keep hurting, so it is the one trigger that repeats.
        onlyOnce = false;
    }

    protected override void OnPlayerEntered(GameObject player)
    {
        nextTick = 0f;
    }

    protected override void OnPlayerStaying(GameObject player)
    {
        if (Time.time < nextTick || GameManager.Instance == null)
        {
            return;
        }

        nextTick = Time.time + tickSeconds;

        GameManager.Instance.TakeDamage(damagePerTick);
        GameManager.Instance.SpendEnergy(energyDrainPerTick);
    }
}
