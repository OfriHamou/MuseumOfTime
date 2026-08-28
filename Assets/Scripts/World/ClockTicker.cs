using UnityEngine;

/// <summary>
/// A localized, spatial clock tick - the museum's ambience used to bake a
/// tick into the scene-wide music loop, so it played loudly everywhere for
/// the whole level. This puts ticking back where it actually belongs: on the
/// clock itself, falling off with distance like a real sound in the room,
/// growing noticeable only as the player approaches. Placed on the Clock of
/// Creation and any other clock-themed exhibit.
/// </summary>
[RequireComponent(typeof(AudioSource))]
public sealed class ClockTicker : MonoBehaviour
{
    [Tooltip("Full volume at this distance or closer.")]
    [SerializeField] private float minDistance = 1.5f;

    [Tooltip("Inaudible beyond this distance.")]
    [SerializeField] private float maxDistance = 9f;

    [Range(0f, 1f)]
    [SerializeField] private float volume = 0.6f;

    private void Awake()
    {
        var source = GetComponent<AudioSource>();
        source.clip = ProceduralAudioClips.ClockTick(gameObject.name + "_Tick");
        source.loop = true;
        source.playOnAwake = true;
        source.spatialBlend = 1f;
        source.rolloffMode = AudioRolloffMode.Linear;
        source.minDistance = minDistance;
        source.maxDistance = maxDistance;
        source.volume = volume;
        source.Play();
    }
}
