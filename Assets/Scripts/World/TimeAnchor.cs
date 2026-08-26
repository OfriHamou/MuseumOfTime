using UnityEngine;

/// <summary>
/// A hidden teleport. The requirement is specific, so this matches it clause
/// by clause:
///
///   - "from the second scene onward"  : these belong in FrozenCity and
///     ClockCore only. MuseumNight uses a plain respawn.
///   - "hidden"                        : no marker, no HUD icon. It arms
///     silently as Noa walks past. The Time Lens is the only way to see one,
///     which is exactly why the Lens is found in scene one and needed in
///     scene two.
///   - "returns to the teleport, not the start"
///   - "with health refreshed, and possibly score"
/// </summary>
[RequireComponent(typeof(Collider))]
public sealed class TimeAnchor : MonoBehaviour
{
    [Tooltip("Only visible while Noa carries the Time Lens.")]
    [SerializeField] private GameObject lensVisual;

    private void Reset()
    {
        GetComponent<Collider>().isTrigger = true;
    }

    private void Awake()
    {
        GetComponent<Collider>().isTrigger = true;
        RefreshVisibility();
    }

    private void Update()
    {
        RefreshVisibility();
    }

    private void RefreshVisibility()
    {
        if (lensVisual == null)
        {
            return;
        }

        bool hasLens = GameManager.Instance != null &&
                       GameManager.Instance.State.hasTimeLens;

        if (lensVisual.activeSelf != hasLens)
        {
            lensVisual.SetActive(hasLens);
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player"))
        {
            return;
        }

        Arm();
    }

    /// <summary>Silently records this as the place to come back to.</summary>
    public void Arm()
    {
        if (GameManager.Instance == null)
        {
            return;
        }

        GameManager.Instance.SaveCheckpoint(
            UnityEngine.SceneManagement.SceneManager.GetActiveScene().name,
            transform.position);

        if (EraManager.Instance != null)
        {
            GameManager.Instance.State.checkpointEra =
                EraManager.Instance.CurrentEra;
        }
    }
}
