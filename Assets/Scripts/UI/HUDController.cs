using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The gameplay HUD: health, energy, shards, era and the two acquired items.
/// Subscribes to GameManager.StateChanged rather than polling in Update, so
/// the health/energy/score-driven elements only touch the UI when something
/// actually changed.
///
/// The one exception is the detection meter: WardenAI has no "detection
/// changed" event to subscribe to (Phase 4 code, out of scope to extend
/// here), so that one value is read each frame purely for display - nothing
/// here writes back into game or AI state.
/// </summary>
public sealed class HUDController : MonoBehaviour
{
    [SerializeField] private Image healthFill;
    [SerializeField] private Image energyFill;
    [SerializeField] private TMP_Text shardText;
    [SerializeField] private TMP_Text eraText;
    [SerializeField] private GameObject timeLensIcon;
    [SerializeField] private GameObject hourglassIcon;
    [SerializeField] private GameObject detectionMeterRoot;
    [SerializeField] private Image detectionFill;

    private WardenAI[] wardens;

    private void Start()
    {
        wardens = FindObjectsByType<WardenAI>(FindObjectsSortMode.None);

        if (detectionMeterRoot != null)
        {
            detectionMeterRoot.SetActive(false);
        }

        if (GameManager.Instance != null)
        {
            GameManager.Instance.StateChanged += Refresh;
            Refresh();
        }
    }

    private void OnDestroy()
    {
        if (GameManager.Instance != null)
        {
            GameManager.Instance.StateChanged -= Refresh;
        }
    }

    private void Refresh()
    {
        GameState state = GameManager.Instance.State;

        if (healthFill != null) healthFill.fillAmount = (float)state.currentHealth / state.maxHealth;
        if (energyFill != null) energyFill.fillAmount = state.currentEnergy / state.maxEnergy;
        if (shardText != null) shardText.text = state.timeShards.ToString();
        if (eraText != null) eraText.text = state.currentEra.ToString();
        if (timeLensIcon != null) timeLensIcon.SetActive(state.hasTimeLens);
        if (hourglassIcon != null) hourglassIcon.SetActive(state.hasChronoHourglass);
    }

    private void Update()
    {
        if (detectionMeterRoot == null || wardens == null || wardens.Length == 0)
        {
            return;
        }

        float highest = 0f;

        foreach (WardenAI warden in wardens)
        {
            if (warden != null)
            {
                highest = Mathf.Max(highest, warden.DetectionLevel);
            }
        }

        detectionMeterRoot.SetActive(highest > 0.01f);

        if (detectionFill != null)
        {
            detectionFill.fillAmount = highest;
        }
    }
}
