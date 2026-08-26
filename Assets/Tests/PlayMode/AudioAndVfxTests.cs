using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

/// <summary>
/// Play-mode checks for Phase 7. These verify functional wiring only - that a
/// game event actually reaches the audio/VFX system - never how anything
/// sounds or looks, which is manual (see Phase7_Unity_Walkthrough.md). The
/// full ambience content, the AudioMixer asset, the lightmap bake and the
/// D2 framerate check are all documented there as manual/deferred rather
/// than tested here.
/// </summary>
public sealed class AudioAndVfxTests
{
    private GameObject player;
    private AudioManager audioManager;

    [UnitySetUp]
    public IEnumerator LoadScene()
    {
        Time.timeScale = 1f;

        SceneManager.LoadScene("MuseumNight", LoadSceneMode.Single);
        yield return null;
        yield return null;

        player = GameObject.Find("Player");
        Assert.IsNotNull(player, "No 'Player' object in MuseumNight.");

        audioManager = Object.FindFirstObjectByType<AudioManager>();
        Assert.IsNotNull(audioManager, "No AudioManager in MuseumNight.");
    }

    [UnityTearDown]
    public IEnumerator Cleanup()
    {
        Time.timeScale = 1f;
        yield return null;
    }

    // ---------------- audio pipeline ----------------

    [Test]
    public void AudioManager_ExistsWithMusicAndSfxSources()
    {
        AudioSource[] sources = audioManager.GetComponentsInChildren<AudioSource>(true);
        Assert.GreaterOrEqual(sources.Length, 2, "Expected a Music source and a separate SFX source.");

        var lowPass = audioManager.GetComponentInChildren<AudioLowPassFilter>(true);
        Assert.IsNotNull(lowPass, "No AudioLowPassFilter - the slow-time filter has nothing to toggle.");
    }

    [UnityTest]
    public IEnumerator SlowTimeFiltering_EngagesAndReleasesWithTheHourglass()
    {
        var lowPass = audioManager.GetComponentInChildren<AudioLowPassFilter>(true);
        Assert.IsNotNull(lowPass, "No AudioLowPassFilter in the scene.");

        GameManager.Instance.State.hasChronoHourglass = true;
        GameManager.Instance.RestoreFullEnergy();

        var hourglass = player.GetComponent<ChronoHourglass>();
        var reader = player.GetComponent<PlayerInputReader>();
        SetPrivate(reader, "isSlowTimeHeld", true);

        // Wait for slow-time to actually engage (deterministic - no assumed
        // frame count between ChronoHourglass.Update and AudioManager.Update).
        for (int i = 0; i < 20 && !hourglass.IsSlowing; i++) { yield return null; }
        Assert.IsTrue(hourglass.IsSlowing, "Holding Ctrl with the Hourglass should engage slow-time.");

        // AudioManager reacts in both modes: the slow-time ENTER cue fires.
        for (int i = 0; i < 20 && audioManager.LastSfx != AudioManager.Sfx.SlowTimeEnter; i++) { yield return null; }
        Assert.AreEqual(AudioManager.Sfx.SlowTimeEnter, audioManager.LastSfx,
            "AudioManager should react to slow-time engaging.");

        // How the filtering is delivered depends on whether the AudioMixer
        // asset has been created: with a mixer it is a snapshot transition
        // (the component filter stays off by design); without one, the
        // component AudioLowPassFilter is the fallback that engages.
        if (!audioManager.UsingMixer)
        {
            for (int i = 0; i < 20 && !lowPass.enabled; i++) { yield return null; }
            Assert.IsTrue(lowPass.enabled, "Without a mixer, holding the Hourglass should engage the SFX low-pass filter.");
        }

        SetPrivate(reader, "isSlowTimeHeld", false);
        for (int i = 0; i < 20 && hourglass.IsSlowing; i++) { yield return null; }

        for (int i = 0; i < 20 && audioManager.LastSfx != AudioManager.Sfx.SlowTimeExit; i++) { yield return null; }
        Assert.AreEqual(AudioManager.Sfx.SlowTimeExit, audioManager.LastSfx,
            "AudioManager should react to slow-time releasing.");

        if (!audioManager.UsingMixer)
        {
            for (int i = 0; i < 20 && lowPass.enabled; i++) { yield return null; }
            Assert.IsFalse(lowPass.enabled, "Without a mixer, releasing the Hourglass should disengage the SFX low-pass filter.");
        }
    }

    [UnityTest]
    public IEnumerator Sfx_PlaysOnShardPickup()
    {
        GameManager.Instance.AddTimeShard(1);
        yield return null;

        Assert.AreEqual(AudioManager.Sfx.ShardPickup, audioManager.LastSfx,
            "Collecting a shard should play the pickup cue (StateChanged wiring).");
    }

    [UnityTest]
    public IEnumerator Sfx_PlaysOnEraSwitch()
    {
        var era = Object.FindFirstObjectByType<EraManager>();
        era.Unlock();
        era.SetEra(TimeEra.Past);
        yield return null;

        Assert.AreEqual(AudioManager.Sfx.EraSwitch, audioManager.LastSfx,
            "Switching era should play the era-switch cue (EraChanged wiring).");
    }

    [UnityTest]
    public IEnumerator Sfx_PlaysOnOrbThrow()
    {
        GameManager.Instance.RestoreFullEnergy();

        var launcher = player.GetComponent<ChronoOrbLauncher>();
        Assert.IsNotNull(launcher, "Player has no ChronoOrbLauncher.");
        Assert.IsTrue(launcher.Throw(), "The orb should have thrown (energy/cooldown/prefab all present).");
        yield return null;

        Assert.AreEqual(AudioManager.Sfx.OrbThrow, audioManager.LastSfx,
            "Throwing the orb should play the throw cue (ThrownCount-poll wiring).");
    }

    [UnityTest]
    public IEnumerator Sfx_PlaysOnFracture()
    {
        var fractured = Object.FindFirstObjectByType<FracturedObject>();
        Assert.IsNotNull(fractured, "No FracturedObject in MuseumNight.");

        fractured.Break(fractured.transform.position);
        yield return null;

        Assert.AreEqual(AudioManager.Sfx.Fracture, audioManager.LastSfx,
            "Breaking a fractured object should play the fracture cue (IsBroken-poll wiring).");
    }

    // ---------------- particle effects ----------------

    [Test]
    public void EraColorGrading_TintsDifferentlyPerEra()
    {
        var grading = Object.FindFirstObjectByType<EraColorGrading>();
        Assert.IsNotNull(grading, "No EraColorGrading in MuseumNight.");

        var era = Object.FindFirstObjectByType<EraManager>();
        era.Unlock();

        era.SetEra(TimeEra.Past);
        Color past = grading.ColorAdjustments.colorFilter.value;

        era.SetEra(TimeEra.Present);
        Color present = grading.ColorAdjustments.colorFilter.value;

        era.SetEra(TimeEra.Future);
        Color future = grading.ColorAdjustments.colorFilter.value;

        Assert.AreNotEqual(past, present, "Past and Present should not read the same tint.");
        Assert.AreNotEqual(present, future, "Present and Future should not read the same tint.");
        Assert.Greater(past.r, past.b, "Past should read warm (more red than blue).");
        Assert.Greater(future.b, future.r, "Future should read cold (more blue than red).");
    }

    [UnityTest]
    public IEnumerator EraSwitchVfx_PlaysOnEveryEraChange()
    {
        var vfx = Object.FindFirstObjectByType<EraSwitchVfx>();
        Assert.IsNotNull(vfx, "No EraSwitchVfx in MuseumNight.");

        var particles = vfx.GetComponent<ParticleSystem>();
        var era = Object.FindFirstObjectByType<EraManager>();
        era.Unlock();
        era.SetEra(TimeEra.Present);
        particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        era.SetEra(TimeEra.Past);
        yield return null;

        Assert.IsTrue(particles.isPlaying, "The era-switch particle burst should play on every era change.");
    }

    [UnityTest]
    public IEnumerator GameplayVfx_ShardSparklePlaysOnPickup()
    {
        var vfx = Object.FindFirstObjectByType<GameplayVfx>();
        Assert.IsNotNull(vfx, "No GameplayVfx in MuseumNight.");
        vfx.ShardBurst.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        GameManager.Instance.AddTimeShard(1);
        yield return null;

        Assert.IsTrue(vfx.ShardBurst.isPlaying, "Collecting a shard should play the shard-collection sparkle.");
    }

    [UnityTest]
    public IEnumerator GameplayVfx_FractureDustPlaysOnBreak()
    {
        var vfx = Object.FindFirstObjectByType<GameplayVfx>();
        Assert.IsNotNull(vfx, "No GameplayVfx in MuseumNight.");
        vfx.FractureBurst.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var fractured = Object.FindFirstObjectByType<FracturedObject>();
        Assert.IsNotNull(fractured, "No FracturedObject in MuseumNight.");
        fractured.Break(fractured.transform.position);
        yield return null;

        Assert.IsTrue(vfx.FractureBurst.isPlaying, "Breaking a fractured object should play the fracture dust burst.");
    }

    private static void SetPrivate(object target, string field, object value)
    {
        FieldInfo info = target.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.IsNotNull(info, "Field '" + field + "' not found.");
        info.SetValue(target, value);
    }
}
