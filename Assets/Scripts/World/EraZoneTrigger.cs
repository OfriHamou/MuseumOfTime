using UnityEngine;

/// <summary>Trigger 3: marks a zone where era travel is meaningful.</summary>
public sealed class EraZoneTrigger : PlayerTrigger
{
    [SerializeField] private bool unlocksEraTravel;

    public static bool PlayerInEraZone { get; private set; }

    protected override void OnPlayerEntered(GameObject player)
    {
        PlayerInEraZone = true;

        if (unlocksEraTravel && EraManager.Instance != null)
        {
            EraManager.Instance.Unlock();
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (IsPlayer(other))
        {
            PlayerInEraZone = false;
        }
    }
}
