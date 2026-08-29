using System;
using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

/// <summary>
/// Guarantees the game is not silent.
///
/// Every sound in the project is generated procedurally at runtime by
/// AudioManager.Awake - there are zero AudioClip assets on disk and the
/// AudioSources are created in code. That is a legitimate design (it costs
/// nothing against the 300 MB budget), but it means nothing about the audio is
/// visible in the Editor: opening the scene shows an AudioManager with null
/// sources and no clips, and a single typo in the clip table would ship a
/// completely silent game with no obvious symptom.
///
/// So the guarantee has to be a runtime one.
/// </summary>
public sealed class AudioCoverageTests
{
    private AudioManager audioManager;

    private static IEnumerator Load(string sceneName)
    {
        SceneManager.LoadScene(sceneName, LoadSceneMode.Single);
        yield return null;
        yield return null;
    }

    [UnitySetUp]
    public IEnumerator LoadScene()
    {
        yield return Load("MuseumNight");

        audioManager = UnityEngine.Object.FindFirstObjectByType<AudioManager>();
        Assert.IsNotNull(audioManager, "MuseumNight has no AudioManager.");
    }

    [UnityTest]
    public IEnumerator EverySoundEffectHasARealClip()
    {
        yield return null;

        FieldInfo field = typeof(AudioManager).GetField(
            "clips", BindingFlags.Instance | BindingFlags.NonPublic);

        Assert.IsNotNull(field, "AudioManager has no clips table.");

        var table = field.GetValue(audioManager)
            as System.Collections.Generic.Dictionary<AudioManager.Sfx, AudioClip>;

        Assert.IsNotNull(table, "The clip table was never built.");

        // Every value of the enum must be represented, or some game event
        // fires a cue that does not exist.
        foreach (AudioManager.Sfx id in Enum.GetValues(typeof(AudioManager.Sfx)))
        {
            Assert.IsTrue(
                table.ContainsKey(id),
                "No clip is registered for " + id + ", so that cue is silent.");

            AudioClip clip = table[id];

            Assert.IsNotNull(clip, "The clip for " + id + " is null.");
            Assert.Greater(clip.length, 0f, "The clip for " + id + " has no length.");
            Assert.Greater(clip.samples, 0, "The clip for " + id + " has no samples.");
        }
    }

    [UnityTest]
    public IEnumerator TheAudioSourcesAndListenerExist()
    {
        yield return null;

        foreach (string name in new[] { "musicSource", "sfxSource" })
        {
            FieldInfo field = typeof(AudioManager).GetField(
                name, BindingFlags.Instance | BindingFlags.NonPublic);

            Assert.IsNotNull(field, "AudioManager has no " + name + " field.");
            Assert.IsNotNull(field.GetValue(audioManager), name + " was never created.");
        }

        // Exactly one listener, or Unity warns and the mix behaves oddly.
        AudioListener[] listeners = UnityEngine.Object.FindObjectsByType<AudioListener>(
            FindObjectsInactive.Include, FindObjectsSortMode.None);

        Assert.AreEqual(
            1, listeners.Length,
            "Expected exactly one AudioListener, found " + listeners.Length + ".");
    }

    /// <summary>
    /// FrozenCity intentionally has no background music (see
    /// AudioManager.PlayAmbienceForActiveScene, which stops/clears
    /// musicSource for that scene by design) - only its SFX cue set (bell,
    /// footsteps, orb, shard pickups, Warden/Shadow cues) plays, through the
    /// separate sfxSource. Every other gameplay scene still needs a real,
    /// playing ambience clip, or it silently ships mute with no obvious
    /// symptom - that half of the guarantee is unchanged.
    /// </summary>
    [UnityTest]
    public IEnumerator EveryGameplaySceneMatchesItsIntendedAmbience()
    {
        foreach (string sceneName in new[] { "MuseumNight", "ClockCore" })
        {
            yield return Load(sceneName);

            var manager = UnityEngine.Object.FindFirstObjectByType<AudioManager>();
            Assert.IsNotNull(manager, sceneName + " has no AudioManager.");

            // A few frames: ambience starts from Start(), not Awake().
            for (int i = 0; i < 5; i++) { yield return null; }

            AudioSource music = MusicSource(manager);
            Assert.IsNotNull(music, sceneName + " has no music source.");

            Assert.IsNotNull(
                music.clip,
                sceneName + " has no ambience clip assigned, so the scene is silent.");

            Assert.IsTrue(
                music.isPlaying,
                sceneName + "'s ambience is not playing.");
        }

        // FrozenCity: the opposite assertion, on purpose - no ambience clip,
        // nothing playing, but the AudioManager (and therefore every SFX
        // cue) is still fully present and functioning.
        yield return Load("FrozenCity");

        var frozenCityManager = UnityEngine.Object.FindFirstObjectByType<AudioManager>();
        Assert.IsNotNull(frozenCityManager, "FrozenCity has no AudioManager.");

        for (int i = 0; i < 5; i++) { yield return null; }

        AudioSource frozenCityMusic = MusicSource(frozenCityManager);
        Assert.IsNotNull(frozenCityMusic, "FrozenCity has no music source.");

        Assert.IsNull(
            frozenCityMusic.clip,
            "FrozenCity is intentionally silent - no background music clip should be assigned.");

        Assert.IsFalse(
            frozenCityMusic.isPlaying,
            "FrozenCity is intentionally silent - the music source should not be playing.");
    }

    private static AudioSource MusicSource(AudioManager manager)
    {
        FieldInfo field = typeof(AudioManager).GetField(
            "musicSource", BindingFlags.Instance | BindingFlags.NonPublic);

        return field.GetValue(manager) as AudioSource;
    }

    [UnityTest]
    public IEnumerator ShardPickupFiresItsCue()
    {
        yield return null;

        GameManager.Instance.ResetGame();
        yield return null;

        GameManager.Instance.AddTimeShard(1);

        // AudioManager reacts on StateChanged, then on its next Update.
        for (int i = 0; i < 5; i++) { yield return null; }

        Assert.AreEqual(
            AudioManager.Sfx.ShardPickup, audioManager.LastSfx,
            "Collecting a Time Shard did not fire the pickup cue.");
    }
}
