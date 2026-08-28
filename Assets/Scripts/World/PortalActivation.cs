using UnityEngine;

/// <summary>
/// Makes the FrozenCity portal visibly change state the instant the Time
/// Lens is acquired, instead of sitting at the same brightness whether or
/// not it is actually usable.
///
/// Before: dim glow, fractured-clock emblem unlit, no particles - it reads
/// as inert/unstable, matching SceneExitTrigger's "Portal unstable" message.
/// After: both lights brighten, the emblem glows, particles begin drifting
/// through the frame, and a one-shot activation cue plays - the player does
/// not need to read a sign to tell the doorway just changed.
/// </summary>
public sealed class PortalActivation : MonoBehaviour
{
    [SerializeField] private Light portalGlow;
    [SerializeField] private Light signGlow;
    [SerializeField] private MeshRenderer clockEmblem;
    [SerializeField] private ParticleSystem particles;

    [SerializeField] private float dimIntensity = 2.5f;
    [SerializeField] private float activeIntensity = 9f;

    private static readonly Color EmblemDim = new Color(0.35f, 0.32f, 0.28f, 1f);
    private static readonly Color EmblemActive = new Color(1.6f, 1.3f, 0.6f, 1f);

    private bool wasActive;

    private void Start()
    {
        ApplyState(IsUnlocked(), instant: true);
    }

    private void Update()
    {
        bool active = IsUnlocked();

        if (active == wasActive)
        {
            return;
        }

        ApplyState(active, instant: false);
    }

    private static bool IsUnlocked()
    {
        return GameManager.Instance != null && GameManager.Instance.State.hasTimeLens;
    }

    private void ApplyState(bool active, bool instant)
    {
        wasActive = active;
        float intensity = active ? activeIntensity : dimIntensity;

        if (portalGlow != null) { portalGlow.intensity = intensity; }
        if (signGlow != null) { signGlow.intensity = intensity; }

        if (clockEmblem != null)
        {
            clockEmblem.material.color = active ? EmblemActive : EmblemDim;
        }

        if (particles != null)
        {
            var emission = particles.emission;
            emission.enabled = active;

            if (active && !particles.isPlaying) { particles.Play(); }
            else if (!active) { particles.Stop(); }
        }

        if (!instant && active && AudioManager.Instance != null)
        {
            AudioManager.Instance.Play(AudioManager.Sfx.PortalActivate);
        }
    }
}
