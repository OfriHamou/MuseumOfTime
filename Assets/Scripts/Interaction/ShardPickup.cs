using UnityEngine;

/// <summary>
/// A Time Shard: a collectible that raises the player's score and final
/// result. Not a currency or gate - GameManager.AddTimeShard already scores
/// it (100 per shard); this just has to make that connection visible the
/// instant it happens, the same way every other scoring event in the game
/// (freezing an enemy, a Shadow stealing one back) already posts a message.
/// </summary>
public sealed class ShardPickup : MonoBehaviour, IInteractable
{
    [SerializeField] private int shards = 1;

    public string Prompt => "Collect the Time Shard";

    public bool CanInteract => true;

    public void Interact(GameObject interactor)
    {
        if (GameManager.Instance != null)
        {
            int scoreBefore = GameManager.Instance.State.score;
            GameManager.Instance.AddTimeShard(shards);
            int gained = GameManager.Instance.State.score - scoreBefore;

            HudMessageFeed.Post(
                "TIME SHARD RECOVERED  +" + gained,
                HudMessageFeed.Tone.Good);
        }

        Destroy(gameObject);
    }
}
