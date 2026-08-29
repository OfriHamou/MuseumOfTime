using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.SceneManagement;

/// <summary>
/// Step 7.1's audio system: per-scene ambience, the full SFX cue set, and the
/// slow-time filtering the plan calls "a single mixer snapshot [that] sells
/// the whole ability".
///
/// AUDIOMIXER. The plan asks to route through an AudioMixer with Master /
/// Music / SFX groups and a slow-time snapshot. AudioMixer and
/// AudioMixerGroup have no public constructors, and AssetDatabase cannot
/// build the group/snapshot graph, so the .mixer asset itself cannot be
/// created through supported automation - only the Editor UI or reflection
/// into the internal UnityEditor.Audio.AudioMixerController can, and this
/// project deliberately does not reflect into internal Editor APIs (see the
/// NavMesh discussion in Phase6_Unity_Walkthrough.md). So the mixer is a
/// serialized reference this class is fully wired to USE the moment one is
/// assigned - AudioAndVfxBuilder auto-wires it if the asset exists at
/// Assets/Audio/GameAudioMixer.mixer - and until then the same behaviour is
/// delivered by an AudioLowPassFilter on the SFX source directly. The manual
/// steps to create the asset are in Phase7_Unity_Walkthrough.md.
///
/// Wiring: this is a read-only observer of the existing systems - it
/// subscribes to GameManager.StateChanged and EraManager.EraChanged, and
/// polls public state (ChronoHourglass.IsSlowing, ChronoOrbLauncher.ThrownCount,
/// PlayerController velocity, WardenAI.CurrentState, ShadowAI.CurrentState, FracturedObject.IsBroken,
/// the bell's angular velocity) - the same pattern HUDController uses. No
/// Phase 3/4 source file changed for any of this.
/// </summary>
public sealed class AudioManager : MonoBehaviour
{
    public enum Sfx
    {
        Footstep,
        FootstepStair,
        Interaction,
        ShardPickup,
        OrbThrow,
        OrbImpact,
        Bell,
        Fracture,
        WardenAlert,
        EnemyFrozen,
        Capture,
        EraSwitch,
        SlowTimeEnter,
        SlowTimeExit,
        SealRestored,
        SealRejected,
        PortalActivate,
    }

    [Header("Volumes (used when no AudioMixer is assigned)")]
    [Range(0f, 1f)] [SerializeField] private float masterVolume = 1f;
    [Range(0f, 1f)] [SerializeField] private float musicVolume = 0.5f;
    [Range(0f, 1f)] [SerializeField] private float sfxVolume = 1f;

    [Header("AudioMixer (assigned by AudioAndVfxBuilder once the asset exists)")]
    [SerializeField] private AudioMixer mixer;
    [SerializeField] private AudioMixerGroup musicGroup;
    [SerializeField] private AudioMixerGroup sfxGroup;
    [SerializeField] private AudioMixerSnapshot normalSnapshot;
    [SerializeField] private AudioMixerSnapshot slowTimeSnapshot;

    public static AudioManager Instance { get; private set; }

    /// <summary>The most recently played cue, for wiring tests.</summary>
    public Sfx? LastSfx { get; private set; }

    private AudioSource musicSource;
    private AudioSource sfxSource;
    private AudioLowPassFilter sfxLowPass;

    private System.Collections.Generic.Dictionary<Sfx, AudioClip> clips;

    private ChronoHourglass hourglass;
    private ChronoOrbLauncher launcher;
    private PlayerInteractor interactor;
    private PlayerInputReader inputReader;
    private PlayerController playerController;
    private Rigidbody bellBody;

    private WardenAI[] wardens;
    private ShadowAI[] shadows;
    private FracturedObject[] fractures;
    private bool[] wardenWasAware;
    private bool[] wardenWasFrozen;
    private bool[] shadowWasFrozen;
    private bool[] fractureWasBroken;

    private int lastShardCount;
    private int lastDetectedCount;
    private int lastThrownCount;
    private int lastOrbBounces;
    private bool wasSlowing;
    private bool bellWasRinging;
    private float nextFootstepTime;

    /// <summary>
    /// True when a real AudioMixer is wired. In that mode the slow-time
    /// effect is a snapshot transition and the component AudioLowPassFilter
    /// is intentionally left off; without a mixer, the component filter is
    /// the fallback that delivers the same effect.
    /// </summary>
    public bool UsingMixer => mixer != null && musicGroup != null && sfxGroup != null;

    private void Awake()
    {
        Instance = this;
        BuildSources();
        BuildClips();
        ApplyVolumes();
    }

    private void BuildSources()
    {
        musicSource = gameObject.AddComponent<AudioSource>();
        musicSource.playOnAwake = false;
        musicSource.loop = true;

        var sfxGo = new GameObject("SfxSource");
        sfxGo.transform.SetParent(transform, false);
        sfxSource = sfxGo.AddComponent<AudioSource>();
        sfxSource.playOnAwake = false;

        sfxLowPass = sfxGo.AddComponent<AudioLowPassFilter>();
        sfxLowPass.cutoffFrequency = 700f;
        // When a mixer is present the low-pass lives in its SlowTime snapshot,
        // so the component-level filter is left off and unused.
        sfxLowPass.enabled = false;

        if (UsingMixer)
        {
            musicSource.outputAudioMixerGroup = musicGroup;
            sfxSource.outputAudioMixerGroup = sfxGroup;

            // UpdateSlowTime only transitions the mixer when IsSlowing changes,
            // so it never runs at all in a scene where the player hasn't
            // touched the Hourglass yet. Whatever snapshot the mixer asset
            // happened to save as current (e.g. SlowTime, with its 700Hz
            // lowpass) would then silently stay active for the whole scene,
            // making all audio through it sound muffled to the point of
            // inaudible. Force the unfiltered snapshot on immediately so
            // playback always starts from a known-good state.
            if (normalSnapshot != null)
            {
                normalSnapshot.TransitionTo(0f);
            }
        }
    }

    private void BuildClips()
    {
        clips = new System.Collections.Generic.Dictionary<Sfx, AudioClip>
        {
            { Sfx.Footstep,      ProceduralAudioClips.Thud("Footstep", 90f, 0.09f, 0.35f) },
            { Sfx.FootstepStair, ProceduralAudioClips.Thud("FootstepStair", 140f, 0.08f, 0.35f) },
            { Sfx.Interaction,   ProceduralAudioClips.Tone("Interaction", 520f, 0.10f) },
            { Sfx.ShardPickup,   ProceduralAudioClips.Chime("ShardPickup") },
            { Sfx.OrbThrow,      ProceduralAudioClips.Noise("OrbThrow", 0.15f, 0.3f, 71, smoothing: 0.3f) },
            { Sfx.OrbImpact,     ProceduralAudioClips.Thud("OrbImpact", 150f, 0.12f) },
            { Sfx.Bell,          ProceduralAudioClips.Tone("Bell", 700f, 0.7f, amplitude: 0.5f) },
            { Sfx.Fracture,      ProceduralAudioClips.Noise("Fracture", 0.4f, 0.5f, 913, smoothing: 0.2f) },
            { Sfx.WardenAlert,   ProceduralAudioClips.Tone("WardenAlert", 660f, 0.25f, 990f) },
            { Sfx.EnemyFrozen,   ProceduralAudioClips.Chime("EnemyFrozen") },
            { Sfx.Capture,       ProceduralAudioClips.Tone("Capture", 300f, 0.4f, 150f) },
            { Sfx.EraSwitch,     ProceduralAudioClips.Tone("EraSwitch", 220f, 0.35f) },
            { Sfx.SlowTimeEnter, ProceduralAudioClips.Tone("SlowTimeEnter", 440f, 0.3f, 330f) },
            { Sfx.SlowTimeExit,  ProceduralAudioClips.Tone("SlowTimeExit", 440f, 0.2f, 660f) },
            { Sfx.SealRestored,  ProceduralAudioClips.Chime("SealRestored") },
            { Sfx.SealRejected,  ProceduralAudioClips.Tone("SealRejected", 220f, 0.22f, 140f) },
            { Sfx.PortalActivate, ProceduralAudioClips.Tone("PortalActivate", 220f, 0.9f, 440f, amplitude: 0.5f) },
        };
    }

    private void Start()
    {
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
        {
            hourglass = player.GetComponent<ChronoHourglass>();
            launcher = player.GetComponent<ChronoOrbLauncher>();
            interactor = player.GetComponent<PlayerInteractor>();
            inputReader = player.GetComponent<PlayerInputReader>();
            playerController = player.GetComponent<PlayerController>();
        }

        wardens = FindObjectsByType<WardenAI>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        wardenWasAware = new bool[wardens.Length];
        wardenWasFrozen = new bool[wardens.Length];

        shadows = FindObjectsByType<ShadowAI>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        shadowWasFrozen = new bool[shadows.Length];

        fractures = FindObjectsByType<FracturedObject>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        fractureWasBroken = new bool[fractures.Length];

        GameObject bell = GameObject.Find("TowerBell");
        if (bell != null)
        {
            bellBody = bell.GetComponent<Rigidbody>();
        }

        if (EraManager.Instance != null)
        {
            EraManager.Instance.EraChanged += OnEraChanged;
        }

        if (GameManager.Instance != null)
        {
            lastShardCount = GameManager.Instance.State.timeShards;
            lastDetectedCount = GameManager.Instance.State.detectedCount;
            GameManager.Instance.StateChanged += OnStateChanged;
        }

        PlayAmbienceForActiveScene();
    }

    private void OnDestroy()
    {
        if (EraManager.Instance != null)
        {
            EraManager.Instance.EraChanged -= OnEraChanged;
        }

        if (GameManager.Instance != null)
        {
            GameManager.Instance.StateChanged -= OnStateChanged;
        }
    }

    private void Update()
    {
        EnsurePlayerRefs();
        UpdateSlowTime();
        UpdateFootsteps();
        UpdateOrb();
        UpdateInteraction();
        UpdateWardens();
        UpdateShadows();
        UpdateFractures();
        UpdateBell();
    }

    /// <summary>
    /// Re-resolves the player-derived references if they are still null.
    /// Start caches them once, but if AudioManager's Start happened to run
    /// before the player was findable by tag, they would stay null forever
    /// and the slow-time filter / footstep / orb cues would silently never
    /// fire. Cheap self-heal: the Find only runs while a ref is still missing.
    /// </summary>
    private void EnsurePlayerRefs()
    {
        if (hourglass != null)
        {
            return;
        }

        GameObject p = GameObject.FindGameObjectWithTag("Player");
        if (p == null)
        {
            return;
        }

        hourglass = p.GetComponent<ChronoHourglass>();
        launcher = launcher != null ? launcher : p.GetComponent<ChronoOrbLauncher>();
        interactor = interactor != null ? interactor : p.GetComponent<PlayerInteractor>();
        inputReader = inputReader != null ? inputReader : p.GetComponent<PlayerInputReader>();
        playerController = playerController != null ? playerController : p.GetComponent<PlayerController>();
    }

    // ---------------- slow-time: the filter/snapshot ----------------

    private void UpdateSlowTime()
    {
        bool slowing = hourglass != null && hourglass.IsSlowing;

        if (slowing != wasSlowing)
        {
            Play(slowing ? Sfx.SlowTimeEnter : Sfx.SlowTimeExit);

            if (UsingMixer)
            {
                AudioMixerSnapshot snapshot = slowing ? slowTimeSnapshot : normalSnapshot;
                if (snapshot != null)
                {
                    snapshot.TransitionTo(0.15f);
                }
            }

            wasSlowing = slowing;
        }

        if (!UsingMixer && sfxLowPass != null)
        {
            sfxLowPass.enabled = slowing;
        }
    }

    // ---------------- footsteps, with a stair variant ----------------

    private void UpdateFootsteps()
    {
        if (playerController == null || !playerController.IsGrounded)
        {
            return;
        }

        float speed = playerController.CurrentSpeed;
        if (speed < 0.5f)
        {
            return;
        }

        if (Time.time < nextFootstepTime)
        {
            return;
        }

        // Faster movement, quicker steps.
        nextFootstepTime = Time.time + Mathf.Clamp(3.5f / speed, 0.28f, 0.6f);
        Play(OnStairs() ? Sfx.FootstepStair : Sfx.Footstep);
    }

    private bool OnStairs()
    {
        if (playerController == null)
        {
            return false;
        }

        Vector3 origin = playerController.transform.position + Vector3.up;

        if (!Physics.Raycast(origin, Vector3.down, out RaycastHit hit, 3f,
                             ~0, QueryTriggerInteraction.Ignore))
        {
            return false;
        }

        string n = hit.collider.transform.root.name + "/" + hit.collider.name;
        return n.Contains("Stair") || n.Contains("Step") || n.Contains("Ramp");
    }

    // ---------------- orb throw and impact ----------------

    private void UpdateOrb()
    {
        if (launcher == null)
        {
            return;
        }

        if (launcher.ThrownCount > lastThrownCount)
        {
            Play(Sfx.OrbThrow);
            lastThrownCount = launcher.ThrownCount;
            lastOrbBounces = 0;
        }

        if (launcher.LastOrb != null)
        {
            if (launcher.LastOrb.Bounces > lastOrbBounces)
            {
                Play(Sfx.OrbImpact);
            }

            lastOrbBounces = launcher.LastOrb.Bounces;
        }
    }

    // ---------------- interaction ----------------

    private void UpdateInteraction()
    {
        // InteractPressed is a one-frame flag cleared in LateUpdate, so reading
        // it in Update sees the same value PlayerInteractor did this frame.
        if (interactor != null && inputReader != null &&
            inputReader.InteractPressed && interactor.Current != null)
        {
            Play(Sfx.Interaction);
        }
    }

    // ---------------- warden alert ----------------

    private void UpdateWardens()
    {
        if (wardens == null)
        {
            return;
        }

        for (int i = 0; i < wardens.Length; i++)
        {
            if (wardens[i] == null)
            {
                continue;
            }

            bool aware = wardens[i].CurrentState == WardenAI.State.Alert ||
                         wardens[i].CurrentState == WardenAI.State.Chase;

            if (aware && !wardenWasAware[i])
            {
                Play(Sfx.WardenAlert);
            }

            wardenWasAware[i] = aware;

            bool frozen = wardens[i].CurrentState == WardenAI.State.Frozen;

            if (frozen && !wardenWasFrozen[i])
            {
                Play(Sfx.EnemyFrozen);
            }

            wardenWasFrozen[i] = frozen;
        }
    }

    // ---------------- shadow frozen ----------------

    private void UpdateShadows()
    {
        if (shadows == null)
        {
            return;
        }

        for (int i = 0; i < shadows.Length; i++)
        {
            if (shadows[i] == null)
            {
                continue;
            }

            bool frozen = shadows[i].CurrentState == ShadowAI.State.Frozen;

            if (frozen && !shadowWasFrozen[i])
            {
                Play(Sfx.EnemyFrozen);
            }

            shadowWasFrozen[i] = frozen;
        }
    }

    // ---------------- fracture ----------------

    private void UpdateFractures()
    {
        if (fractures == null)
        {
            return;
        }

        for (int i = 0; i < fractures.Length; i++)
        {
            if (fractures[i] == null)
            {
                continue;
            }

            if (fractures[i].IsBroken && !fractureWasBroken[i])
            {
                Play(Sfx.Fracture);
            }

            fractureWasBroken[i] = fractures[i].IsBroken;
        }
    }

    // ---------------- bell ----------------

    private void UpdateBell()
    {
        if (bellBody == null)
        {
            return;
        }

        bool ringing = bellBody.angularVelocity.magnitude > 0.8f;

        if (ringing && !bellWasRinging)
        {
            Play(Sfx.Bell);
        }

        bellWasRinging = ringing;
    }

    // ---------------- event handlers ----------------

    private void OnStateChanged()
    {
        GameState state = GameManager.Instance.State;

        if (state.timeShards > lastShardCount)
        {
            Play(Sfx.ShardPickup);
        }

        if (state.detectedCount > lastDetectedCount)
        {
            Play(Sfx.Capture);
        }

        lastShardCount = state.timeShards;
        lastDetectedCount = state.detectedCount;
    }

    private void OnEraChanged(TimeEra _)
    {
        Play(Sfx.EraSwitch);
    }

    // ---------------- playback ----------------

    public void Play(Sfx id)
    {
        LastSfx = id;

        if (sfxSource != null && clips != null && clips.TryGetValue(id, out AudioClip clip))
        {
            sfxSource.PlayOneShot(clip, UsingMixer ? 1f : masterVolume * sfxVolume);
        }
    }

    public void PlayAmbienceForActiveScene()
    {
        if (musicSource == null)
        {
            return;
        }

        string scene = SceneManager.GetActiveScene().name;

        AudioClip ambience = scene switch
        {
            "MainMenu" => ProceduralAudioClips.MainMenuTheme("MainMenuTheme"),
            "FrozenCity" => ProceduralAudioClips.FrozenAmbience("FrozenAmbience"),
            "ClockCore" => ProceduralAudioClips.ClockCoreAmbience("ClockCoreAmbience"),
            // Victory had no AudioManager at all - the screen was completely
            // silent. Reusing the calm main theme rather than writing a new
            // clip generator for a single scene that just needs SOME music.
            "Victory" => ProceduralAudioClips.MainMenuTheme("VictoryTheme"),
            _ => ProceduralAudioClips.MuseumAmbience("MuseumAmbience"),
        };

        musicSource.clip = ambience;
        musicSource.loop = true;
        ApplyVolumes();
        musicSource.Play();
    }

    private void ApplyVolumes()
    {
        if (musicSource != null && !UsingMixer)
        {
            musicSource.volume = masterVolume * musicVolume;
        }
        else if (musicSource != null)
        {
            musicSource.volume = 1f;
        }
    }
}
