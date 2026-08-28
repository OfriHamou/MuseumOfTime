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

    /// <summary>
    /// MuseumNight: a quiet, mysterious low hum, with no ticking baked in.
    ///
    /// This used to embed a clock tick once a second directly into the loop,
    /// which meant a loud tick played everywhere in the scene for the entire
    /// level - not "a museum with a clock in it somewhere" but "a metronome
    /// with a museum around it". Ticking now belongs to ClockTicker, a
    /// spatial 3D source placed on actual clock exhibits, so it is only
    /// audible near them and grows as the player approaches - this clip is
    /// only the ambient bed.
    ///
    /// 9 seconds rather than 4, and a slow amplitude swell, so the loop point
    /// is far less noticeable than a short clip repeating verbatim.
    /// </summary>
    public static AudioClip MuseumAmbience(string name)
    {
        const float duration = 9f;
        int samples = Mathf.CeilToInt(SampleRate * duration);
        var data = new float[samples];

        for (int i = 0; i < samples; i++)
        {
            float t = i / (float)SampleRate;

            // A slow breathing swell (roughly one cycle per loop) keeps a
            // static drone from feeling mechanical, without adding a
            // repeating event a player can count along to.
            float swell = 0.75f + 0.25f * Mathf.Sin(2f * Mathf.PI * (1f / duration) * t);

            // Original amplitudes here (0.07/0.025) were roughly -23dB versus
            // the 0.4-0.5 typical SFX cue - technically playing, but far too
            // quiet to notice under anything else, which is what "I can't
            // hear the music" turned out to be. A touch of 220Hz is added
            // too, since 55/110Hz alone is easy to lose entirely on small
            // speakers with weak bass response.
            float hum = (Mathf.Sin(2f * Mathf.PI * 55f * t) * 0.22f +
                         Mathf.Sin(2f * Mathf.PI * 110f * t) * 0.10f +
                         Mathf.Sin(2f * Mathf.PI * 220f * t) * 0.05f) * swell;

            data[i] = hum;
        }

        return Make(name, data);
    }

    /// <summary>
    /// A single clock tick, meant to be looped by a spatial AudioSource
    /// (ClockTicker) rather than baked into the scene-wide ambience - so it
    /// is only heard near an actual clock, at a volume that falls off with
    /// distance like a real sound in the room would.
    /// </summary>
    public static AudioClip ClockTick(string name)
    {
        const float duration = 1f;
        int samples = Mathf.CeilToInt(SampleRate * duration);
        var data = new float[samples];

        for (int i = 0; i < samples; i++)
        {
            float t = i / (float)SampleRate;
            float intoTick = t;

            data[i] = intoTick < 0.04f
                ? Mathf.Sin(2f * Mathf.PI * 1200f * t) * (1f - intoTick / 0.04f) * 0.5f
                : 0f;
        }

        return Make(name, data);
    }

    /// <summary>
    /// MainMenu: a calm, cinematic pad with a slow bell motif drifting over
    /// it - meant to feel distinctly warmer and more hopeful than
    /// MuseumNight's tense hum, since this is the "welcome to the museum"
    /// moment rather than the "you are alone in it at night" one.
    ///
    /// 16 seconds and the two bell hits land at irregular points in the loop
    /// (not on a fixed beat), so it does not read as a ticking metronome -
    /// the exact failure mode this project already fixed once for the
    /// in-scene clock ambience.
    /// </summary>
    public static AudioClip MainMenuTheme(string name)
    {
        const float duration = 16f;
        int samples = Mathf.CeilToInt(SampleRate * duration);
        var data = new float[samples];

        // A slow, gentle major-leaning triad (A3-C#4-E4), breathing in and
        // out over the whole loop rather than sitting at a fixed volume.
        for (int i = 0; i < samples; i++)
        {
            float t = i / (float)SampleRate;
            float swell = 0.7f + 0.3f * Mathf.Sin(2f * Mathf.PI * (1f / duration) * t);

            // Same fix as MuseumAmbience: these were far too quiet (-26dB
            // range) to actually be heard against anything else in the mix.
            float pad = (Mathf.Sin(2f * Mathf.PI * 220.00f * t) * 0.18f +
                         Mathf.Sin(2f * Mathf.PI * 277.18f * t) * 0.13f +
                         Mathf.Sin(2f * Mathf.PI * 329.63f * t) * 0.11f) * swell;

            data[i] = pad;
        }

        AddBell(data, 3.2f, 660f);
        AddBell(data, 9.7f, 880f);

        return Make(name, data);
    }

    /// <summary>Adds one soft, decaying bell tone into an existing buffer at a given time.</summary>
    private static void AddBell(float[] data, float atSeconds, float frequency)
    {
        int start = Mathf.FloorToInt(atSeconds * SampleRate);
        const float bellDuration = 2.2f;
        int bellSamples = Mathf.Min(data.Length - start, Mathf.CeilToInt(SampleRate * bellDuration));

        for (int i = 0; i < bellSamples; i++)
        {
            float t = i / (float)SampleRate;
            float envelope = Mathf.Exp(-t * 1.4f);
            data[start + i] += Mathf.Sin(2f * Mathf.PI * frequency * t) * envelope * 0.35f;
        }
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
