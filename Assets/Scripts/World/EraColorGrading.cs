using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Tints the whole screen per era via a URP Volume's Color Adjustments -
/// Past warm sepia, Present neutral, Future cold cyan - so the era reads
/// from a single still frame, which the plan calls out by name as the
/// thing that has to read instantly in the trailer.
/// </summary>
[RequireComponent(typeof(Volume))]
public sealed class EraColorGrading : MonoBehaviour
{
    [Header("Past - warm sepia")]
    [SerializeField] private Color pastFilter = new Color(1f, 0.82f, 0.6f);
    [SerializeField] private float pastHueShift = 15f;

    [Header("Present - neutral")]
    [SerializeField] private Color presentFilter = Color.white;
    [SerializeField] private float presentHueShift = 0f;

    [Header("Future - cold cyan")]
    [SerializeField] private Color futureFilter = new Color(0.6f, 0.85f, 1f);
    [SerializeField] private float futureHueShift = -15f;

    private ColorAdjustments colorAdjustments;

    /// <summary>Exposed for verification and for a defense demo.</summary>
    public ColorAdjustments ColorAdjustments => colorAdjustments;

    private void Awake()
    {
        Volume volume = GetComponent<Volume>();

        if (volume.profile == null)
        {
            volume.profile = ScriptableObject.CreateInstance<VolumeProfile>();
        }

        if (!volume.profile.TryGet(out colorAdjustments))
        {
            // 'false': override NOTHING by default.
            //
            // Add<T>(true) turns on overrideState for EVERY parameter, and
            // this volume sits at priority 10 - above the scene's post-process
            // volume at priority 0. So it was overriding postExposure,
            // contrast and saturation with their zero defaults and wiping out
            // the scene grade entirely. This volume owns the era TINT and
            // nothing else.
            colorAdjustments = volume.profile.Add<ColorAdjustments>(false);
        }

        colorAdjustments.active = true;
        colorAdjustments.colorFilter.overrideState = true;
        colorAdjustments.hueShift.overrideState = true;
        colorAdjustments.postExposure.overrideState = false;
        colorAdjustments.contrast.overrideState = false;
        colorAdjustments.saturation.overrideState = false;
    }

    private void OnEnable()
    {
        if (EraManager.Instance != null)
        {
            EraManager.Instance.EraChanged += Apply;
            Apply(EraManager.Instance.CurrentEra);
        }
    }

    private void OnDisable()
    {
        if (EraManager.Instance != null)
        {
            EraManager.Instance.EraChanged -= Apply;
        }
    }

    public void Apply(TimeEra era)
    {
        if (colorAdjustments == null)
        {
            return;
        }

        switch (era)
        {
            case TimeEra.Past:
                colorAdjustments.colorFilter.value = pastFilter;
                colorAdjustments.hueShift.value = pastHueShift;
                break;

            case TimeEra.Future:
                colorAdjustments.colorFilter.value = futureFilter;
                colorAdjustments.hueShift.value = futureHueShift;
                break;

            default:
                colorAdjustments.colorFilter.value = presentFilter;
                colorAdjustments.hueShift.value = presentHueShift;
                break;
        }
    }
}
