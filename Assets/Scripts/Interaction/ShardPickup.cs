using UnityEngine;

/// <summary>A Time Shard: score, and the gate to the finale.</summary>
public sealed class ShardPickup : MonoBehaviour, IInteractable
{
    [SerializeField] private int shards = 1;

    public string Prompt => "Collect the Time Shard";

    public bool CanInteract => true;

    public void Interact(GameObject interactor)
    {
        if (GameManager.Instance != null)
        {
            GameManager.Instance.AddTimeShard(shards);
        }

        Destroy(gameObject);
    }
}
