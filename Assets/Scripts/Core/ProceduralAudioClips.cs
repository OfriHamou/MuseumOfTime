using UnityEngine;

/// <summary>
/// Lightweight placeholder sounds, synthesised at runtime rather than
/// imported - there are no recorded ambience/SFX assets in this project yet.
/// Real audio should replace these; AudioManager's own pipeline (the SFX
/// cue set, the ambience-per-scene selection, the slow-time filter/snapshot)
/// does not change either way.
///
/// Everything here is mono at a low sample rate and at most a few seconds
/// long, so the whole synthesised set is a few hundred KB of transient
/// in-memory PCM - "kept lightweight/compressed" in the only sense that
/// applies before real audio files exist (see Phase7_Unity_Walkthrough.md on
/// why AudioClip cannot be written to a compressed project asset from code).
/// </summary>
public static class ProceduralAudioClips
{
    // 22.05 kHz, not 44.1: half the samples for placeholder beeps nobody is
    // listening to closely, which halves the memory the synthesised set uses.
    private const int SampleRate = 22050;

    // ---------------------------------------------------------------
    // Primitives
    // ---------------------------------------------------------------

    /// <summary>A fading sine tone. Optionally glides from one pitch to another.</summary>
    public static AudioClip Tone(string name, float frequency, float duration,
                                 float endFrequency = -1f, float amplitude = 0.4f)
    {
        if (endFrequency < 0f) { endFrequency = frequency; }

        int samples = Mathf.Max(1, Mathf.CeilToInt(SampleRate * duration));
        var data = new float[samples];
        float phase = 0f;

        for (int i = 0; i < samples; i++)
        {
            float t = i / (float)samples;
            float freq = Mathf.Lerp(frequency, endFrequency, t);
            phase += 2f * Mathf.PI * freq / SampleRate;

            // A gentle attack and a longer decay so it does not click.
            float envelope = Mathf.Min(1f, i / (SampleRate * 0.01f)) * (1f - t);
            data[i] = Mathf.Sin(phase) * envelope * amplitude;
        }

        return Make(name, data);
    }

    /// <summary>A short two-note upward chime - used for pickups.</summary>
    public static AudioClip Chime(string name)
    {
        const float duration = 0.25f;
        int samples = Mathf.CeilToInt(SampleRate * duration);
        var data = new float[samples];

        for (int i = 0; i < samples; i++)
        {
            float t = i / (float)SampleRate;
            float frequency = t < 0.12f ? 660f : 990f;
            float envelope = Mathf.Clamp01((duration - t) / 0.1f);
            data[i] = Mathf.Sin(2f * Mathf.PI * frequency * t) * envelope * 0.4f;
        }

        return Make(name, data);
    }

    /// <summary>A short percussive thump - footsteps, impacts, capture.</summary>
    public static AudioClip Thud(string name, float frequency, float duration, float amplitude = 0.5f)
    {
        int samples = Mathf.CeilToInt(SampleRate * duration);
        var data = new float[samples];

        for (int i = 0; i < samples; i++)
        {
            float t = i / (float)samples;
            // Pitch drops as it decays, like a real knock.
            float freq = Mathf.Lerp(frequency, frequency * 0.5f, t);
            float envelope = Mathf.Pow(1f - t, 2f);
            data[i] = Mathf.Sin(2f * Mathf.PI * freq * (i / (float)SampleRate)) * envelope * amplitude;
        }

        return Make(name, data);
    }

    /// <summary>Band-ish noise - whooshes, crashes, wind.</summary>
    public static AudioClip Noise(string name, float duration, float amplitude, int seed,
                                  bool sustain = false, float smoothing = 0.5f)
    {
        int samples = Mathf.CeilToInt(SampleRate * duration);
        var data = new float[samples];
        var rng = new System.Random(seed);
        float last = 0f;

        for (int i = 0; i < samples; i++)
        {
            float t = i / (float)samples;
            float white = (float)(rng.NextDouble() * 2.0 - 1.0);

            // A one-pole low-pass turns hiss into wind/rumble.
            last = Mathf.Lerp(white, last, smoothing);

            float envelope = sustain ? 1f : Mathf.Pow(1f - t, 1.5f);
            data[i] = last * envelope * amplitude;
        }

        return Make(name, data);
    }

    // ---------------------------------------------------------------
    // Per-scene ambience composers
    // ---------------------------------------------------------------

    /// <summary>MuseumNight: a low hum with a slow, echoing tick.</summary>
    public static AudioClip MuseumAmbience(string name)
    {
        const float duration = 4f;
        int samples = Mathf.CeilToInt(SampleRate * duration);
        var data = new float[samples];
        int tickEvery = Mathf.CeilToInt(SampleRate * 1f);   // one tick a second

        for (int i = 0; i < samples; i++)
        {
            float t = i / (float)SampleRate;
            float hum = Mathf.Sin(2f * Mathf.PI * 55f * t) * 0.12f +
                        Mathf.Sin(2f * Mathf.PI * 110f * t) * 0.04f;

            int intoTick = i % tickEvery;
            float tick = intoTick < SampleRate * 0.04f
                ? Mathf.Sin(2f * Mathf.PI * 1200f * t) * (1f - intoTick / (SampleRate * 0.04f)) * 0.12f
                : 0f;

            data[i] = hum + tick;
        }

        return Make(name, data);
    }

    /// <summary>FrozenCity: wind, and one impossibly held note over it.</summary>
    public static AudioClip FrozenAmbience(string name)
    {
        AudioClip wind = Noise(name + "_wind", 4f, 0.18f, 4021, sustain: true, smoothing: 0.9f);
        var data = new float[wind.samples];
        wind.GetData(data, 0);

        for (int i = 0; i < data.Length; i++)
        {
            float t = i / (float)SampleRate;
            data[i] += Mathf.Sin(2f * Mathf.PI * 330f * t) * 0.06f;   // the held note
        }

        return Make(name, data);
    }

    /// <summary>ClockCore: a detuned, dissonant drone - the museum theme, wrong.</summary>
    public static AudioClip ClockCoreAmbience(string name)
    {
        const float duration = 4f;
        int samples = Mathf.CeilToInt(SampleRate * duration);
        var data = new float[samples];

        for (int i = 0; i < samples; i++)
        {
            float t = i / (float)SampleRate;
            // Two close, deliberately out-of-tune drones beating against
            // each other, plus a sub - unsettling rather than musical.
            data[i] = (Mathf.Sin(2f * Mathf.PI * 98f * t) * 0.10f) +
                      (Mathf.Sin(2f * Mathf.PI * 103f * t) * 0.10f) +
                      (Mathf.Sin(2f * Mathf.PI * 49f * t) * 0.06f);
        }

        return Make(name, data);
    }

    private static AudioClip Make(string name, float[] data)
    {
        AudioClip clip = AudioClip.Create(name, data.Length, 1, SampleRate, false);
        clip.SetData(data, 0);
        return clip;
    }
}
