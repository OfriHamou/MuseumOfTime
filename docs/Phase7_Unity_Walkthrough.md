# Phase 7 — Unity Walkthrough

**How to rebuild the audio, lighting and VFX layer by hand, and where to see it in the editor.**

Seventh in the series, after `Phase1_Unity_Walkthrough.md` through `Phase6_Unity_Walkthrough.md`. Phase 7 is a
polish pass: `Implementation_Plan.md`'s Step 7.1 and 7.2 have no **Verification.** line (every earlier step does) and
only ever *support* the soft scoring axes (G1/G2/S1/S10/D2), never a hard T-coded requirement. That is the plan's own
signal that it is judged by a person looking at and playing the game. So the rule this phase followed: build every
listed item as a real, working, wired system, and where an item genuinely cannot be finished through supported
headless automation (the AudioMixer asset, a lightmap bake, a real-hardware framerate check), prepare everything
around it and hand over the smallest exact manual step - never a functional substitute passed off as the real thing.

Like Phases 5 and 6, everything automatable here was built through headless Editor scripts in Unity batch mode.

**Before you start:** open the Console, click *Clear*.

## The menu grew by one item

```
Build Audio and VFX (Phase 7)
```

One builder (`Assets/Editor/AudioAndVfxBuilder.cs`), run once, applied to `MuseumNight`, `FrozenCity` and `ClockCore`.
Idempotent - and specifically re-runnable *after* the manual AudioMixer step below, at which point it auto-wires the
mixer into all three scenes.

---

# Step 7.1 — Audio

## The full SFX set

`Assets/Scripts/Core/AudioManager.cs` (one per scene) defines all twelve cues the plan lists, as an `Sfx` enum, each
mapped to a synthesised clip, each fired by observing an existing system - **no Phase 3/4 source file was changed for
any of it**:

| Cue | How it fires |
|---|---|
| Footstep + **stair variant** | polls `PlayerController.IsGrounded` + `CurrentSpeed`; a downward raycast whose hit name contains "Stair"/"Step"/"Ramp" picks the stair clip |
| Interaction | `PlayerInputReader.InteractPressed` && `PlayerInteractor.Current != null` |
| Shard pickup | `GameManager.StateChanged`, `timeShards` increased |
| Orb throw | polls `ChronoOrbLauncher.ThrownCount` |
| Orb impact | polls `ChronoOrbLauncher.LastOrb.Bounces` |
| Bell | polls the `TowerBell` rigidbody's angular velocity crossing a threshold |
| Fracture | polls `FracturedObject.IsBroken` transitions |
| Warden alert | polls `WardenAI.CurrentState` entering Alert/Chase |
| Capture | `GameManager.StateChanged`, `detectedCount` increased |
| Era switch | `EraManager.EraChanged` |
| Slow-time enter / exit | polls `ChronoHourglass.IsSlowing` edges |

This is the same read-only-observer pattern `HUDController` (Phase 5) and the previous Phase 7 pass already used - and
the reason it is polling rather than events in most rows is that those Phase 3/4 systems expose public *state* but no
*change events*, and adding events would mean editing Phase 3/4 code.

## Per-scene ambience

`AudioManager.PlayAmbienceForActiveScene()` selects by scene name, matching the plan's own descriptions:

- **MuseumNight** - `MuseumAmbience`: a low hum with a slow, echoing tick ("a ticking, echoing museum at night").
- **FrozenCity** - `FrozenAmbience`: filtered wind noise with one sustained held note over it ("a frozen city with
  wind and one impossibly held note").
- **ClockCore** - `ClockCoreAmbience`: two detuned drones beating against each other plus a sub ("a distorted...
  version of the museum theme" - dissonant rather than musical).

All three are synthesised in `Assets/Scripts/Core/ProceduralAudioClips.cs` at 22.05 kHz mono, a few seconds each and
looping - the whole synthesised set is a few hundred KB of transient in-memory PCM, which is "kept lightweight" in
the only sense available before real recorded audio exists.

## The AudioMixer — prepared in code, one manual step to finish

**This is the one Step 7.1 item that cannot be completed through supported headless automation, and it is not
faked.** `AudioMixer` and `AudioMixerGroup` have no public constructors, and `AssetDatabase.CreateAsset` cannot build
the group/snapshot graph - the *only* programmatic path is reflection into the internal, undocumented
`UnityEditor.Audio.AudioMixerController`, which this project deliberately does not do (the same principled reason
Phase 6 moved *off* an unreliable internal Editor API for NavMesh).

**What is already done in code:** `AudioManager` has serialized `AudioMixer` / `AudioMixerGroup` (Music, SFX) /
`AudioMixerSnapshot` (Normal, SlowTime) fields and is fully wired to use them - it routes both `AudioSource`s to the
groups and, on slow-time, calls `slowTimeSnapshot.TransitionTo(...)` instead of the component low-pass. The builder
auto-loads a mixer at `Assets/Audio/GameAudioMixer.mixer` and assigns the groups/snapshots by name. **Until that asset
exists, the identical audible result - the SFX bus filtered during slow-time - is delivered by an
`AudioLowPassFilter` on the SFX source**, so the game is fully functional today; the mixer is a drop-in upgrade, not a
prerequisite.

**The exact manual steps** (do these once, then re-run the builder) are in the "Manual actions still required"
section at the end of this document.

## How it is verified

`Assets/Tests/PlayMode/AudioAndVfxTests.cs` - functional wiring only, never sound quality:

```
AudioManager_ExistsWithMusicAndSfxSources     - two AudioSources + an AudioLowPassFilter
SfxLowPassFilter_EngagesOnlyWhileTimeIsSlowed - holds Ctrl -> filter on; releases -> off
Sfx_PlaysOnShardPickup                        - AddTimeShard -> LastSfx == ShardPickup   (event wiring)
Sfx_PlaysOnEraSwitch                          - SetEra -> LastSfx == EraSwitch           (event wiring)
Sfx_PlaysOnOrbThrow                           - launcher.Throw() -> LastSfx == OrbThrow  (counter-poll wiring)
Sfx_PlaysOnFracture                           - fractured.Break() -> LastSfx == Fracture (state-poll wiring)
```

`AudioManager.LastSfx` exists purely so a test can assert "this game event reached the audio system" without a
microphone. The four `Sfx_Plays*` tests deliberately cover all three wiring mechanisms (a subscribed event, a polled
counter, a polled state transition) rather than every one of the twelve cues - proving each mechanism works is what
catches a real regression; footsteps/warden-alert/capture/bell use those same three mechanisms and are left to manual
play rather than flaky setup (a warden actually seeing Noa, a bell actually rung).

---

# Step 7.2 — Lighting and VFX

## Era color grading (kept from the previous pass)

`Assets/Scripts/World/EraColorGrading.cs` - a global URP `Volume` whose `ColorAdjustments` is built in code and
driven by `EraManager.EraChanged`: warm sepia Past, neutral Present, cold cyan Future, so the era reads from a single
still frame (the plan's "reads instantly in the trailer"). Tested: `EraColorGrading_TintsDifferentlyPerEra`.

## All five particle effects

| Effect | Implementation |
|---|---|
| **Era-switch shockwave** | `EraSwitchVfx` - a burst at Noa on every `EraManager.EraChanged` |
| **Shard collection** | `GameplayVfx` - a sparkle burst at the player on `GameManager.StateChanged` (shard up) |
| **Fracture dust** | `GameplayVfx` - a dust burst at the object on `FracturedObject.IsBroken` transition |
| **Orb trail** | a world-space `ParticleSystem` child added to `ChronoOrb.prefab`, so every thrown orb leaves a trail |
| **Chronological Shadow drift** | a looping world-space `ParticleSystem` child added to every `ShadowAI`, so particles linger where the Shadow was |

`Assets/Scripts/World/GameplayVfx.cs` is a new observer (shard sparkle + fracture dust); the orb trail and Shadow
drift ride their own objects and are attached by the builder (to the prefab once; to each Shadow per scene). Again,
**no Phase 3/4 source file changed** - the orb trail is added to the *prefab asset*, the drift to the Shadow
*GameObjects*, both from the Editor builder.

Tested: `EraSwitchVfx_PlaysOnEveryEraChange`, `GameplayVfx_ShardSparklePlaysOnPickup`,
`GameplayVfx_FractureDustPlaysOnBreak` - each asserts the `ParticleSystem` actually plays on its event. The orb trail
and Shadow drift are `playOnAwake` particle systems riding their objects, verified structurally (present on the
prefab / on each Shadow) rather than by a play-mode assertion - there is no discrete event to hang a wiring test on.

## MuseumNight lighting

`AudioAndVfxBuilder.BuildMuseumLighting()` builds, into a `MuseumLighting` root, the look the plan describes:

- **Cold moonlight** - one shadow-casting directional light, cool blue, angled down. *One* shadow-caster on purpose:
  the plan names real-time shadow count as the museum's main performance risk.
- **Warm pooled exhibit spots** - four warm spot lights pointing straight down over the Clock of Creation, the two
  Time Shards and the curator's office, shadows off (cheap), so each pools warm light on its exhibit.
- **Deep shadows** - the scene's ambient is set to a dim, cool `Flat` value so the pools read against darkness.

This is real-time lighting that already delivers the documented look. **A lightmap bake was not run** - see below.

## What is manual, and why it is not faked

- **The lightmap bake.** `Lightmapping.Bake()` is long-running and lightmapper-dependent, with a real chance of
  hanging in an unattended headless batch process with no way to watch or cancel it, for a "bake **where possible**"
  result that needs a real Editor session to judge by eye regardless. The lights above are set up so a bake is a
  one-click improvement, not a prerequisite - exact steps below.
- **The D2 framerate / performance check.** A real-hardware measurement with no headless equivalent; the shadow count
  was kept deliberately low (one caster) to help it, but "does it hold 60fps on the defense machine" can only be
  measured there.

---

# Verifying the whole phase

## Automated: `Tools/verify.ps1`

```
=== Step 1/2: compiling ===
Compile OK (exit code 0, no CS errors in Setup.log)

=== Step 2/2: running PlayMode tests ===
Tests: 81/87 passed, 0 failed (result: Skipped:Ignored)

RESULT: PASS
```

87 tests total - the 77 from Phases 0–6 (71 pass, 6 intentionally `Assert.Ignore`d in batch mode, unchanged) plus 10
in `AudioAndVfxTests.cs`. None touch any Phase 3/4 source file.

## Automatically completed

- Full twelve-cue SFX set, wired to real game events (three distinct wiring mechanisms, all tested).
- Per-scene ambience for all three scenes (MuseumNight tick, FrozenCity wind+note, ClockCore dissonant drone).
- The slow-time SFX filtering, working today via an `AudioLowPassFilter`, and code-ready to switch to an AudioMixer
  snapshot the moment the asset exists.
- All five particle effects (era-switch, shard sparkle, fracture dust, orb trail, Shadow drift).
- Per-era color grading (warm/neutral/cold), tested to differ per era.
- MuseumNight real-time lighting: cold moonlight (one shadow caster), four warm pooled exhibit spots, dim cool
  ambient for deep shadows.

## Manual actions still required

> **Update:** step 1 below (the AudioMixer) has since been **completed** — `Assets/Audio/GameAudioMixer.mixer` now
> exists with the Master/Music/SFX groups, the Normal/SlowTime snapshots and the SFX Lowpass, and the builder has
> wired it into every scene's `AudioManager`. Slow-time filtering is now delivered by the snapshot transition rather
> than the fallback component filter. The instructions are kept here for reproducibility. (See also
> `ScenePolish_Walkthrough.md`, which adapted the slow-time test to be mixer-aware once the asset was present.)

**1. Create the AudioMixer asset (~2 minutes), then re-run the builder.** This finishes the one Step 7.1 item Unity
cannot create through supported automation. Exact steps:

1. In the Project window, right-click the **`Assets/Audio`** folder → **Create → Audio Mixer**. Name it exactly
   **`GameAudioMixer`** (giving `Assets/Audio/GameAudioMixer.mixer`).
2. Double-click it to open the **Audio Mixer** window. It starts with one **Master** group.
3. In the **Groups** panel, select **Master**, click **+**, name the new child group exactly **`Music`**. Select
   **Master** again, click **+**, name the second child exactly **`SFX`**.
4. Select the **`SFX`** group. In its Inspector, **Add Effect → Lowpass**.
5. In the **Snapshots** panel, rename the existing snapshot to exactly **`Normal`**. Click **+** to add a second
   snapshot named exactly **`SlowTime`**.
6. With the **`SlowTime`** snapshot selected (highlighted), select the **`SFX`** group and set its **Lowpass →
   Cutoff freq** to about **700**. With the **`Normal`** snapshot selected, set the same **Cutoff freq** to **22000**
   (fully open).
7. Re-run **Museum of Time → Build Audio and VFX (Phase 7)** (menu, or the `-executeMethod` batch command). The log
   will now read `AudioManager (mixer wired ...)`; every scene's `AudioManager` now routes through the mixer and uses
   the `Normal`/`SlowTime` snapshot transition instead of the component filter.

*(The exact names Music / SFX / Normal / SlowTime matter - the builder looks them up by name.)*

**2. Bake MuseumNight lighting (optional polish).** Open `MuseumNight`, **Window → Rendering → Lighting**, and click
**Generate Lighting**. Static geometry should be marked *Contribute GI / Static* first; verify the pooled look still
reads afterward. Left manual because it is long-running, can hang unattended, and needs a visual judgement.

**3. Visual / audio / performance checks (a person, in the Editor or a build):**
- Play each scene and confirm the ambience suits it and the SFX fire on the right actions (footsteps change on the
  stairs; the bell rings when the orb hits it; a warden alert sounds when spotted; capture sounds on detection).
- Confirm each of the five particle effects reads on screen (orb trail follows a thrown orb; Shadow drift trails the
  Chronological Shadow; sparkle/dust/shockwave fire on their events).
- Confirm the per-era color grading reads warm/neutral/cold and MuseumNight's pooled lighting reads as intended.
- **Run the URP performance profile on the defense machine and confirm a stable framerate (D2)** - the one check
  with no automated equivalent at all.

---

# Things you should be able to answer in the defense

Continuing the list from Phases 1–6:

53. **Why is there no `.mixer` asset created by the builder?** `AudioMixer`/`AudioMixerGroup` have no public
    constructors and `AssetDatabase` cannot build the group graph; the only programmatic path is reflection into the
    internal `AudioMixerController`, which this project refuses to do. The asset is a 2-minute manual step; the code
    is fully wired to use it the moment it exists, and an `AudioLowPassFilter` delivers the same effect until then.
54. **Why are most SFX cues polled instead of event-driven?** Those Phase 3/4 systems expose public *state* but no
    *change events*, and adding events would mean editing Phase 3/4 code - the same reason HUDController polls the
    detection meter. Only shard-pickup, capture and era-switch had real events to subscribe to.
55. **How does the stair footstep variant know it is on the stairs?** A downward raycast under the player; if the hit
    collider's name (or its root's) contains "Stair"/"Step"/"Ramp", the stair clip plays instead - the same string
    the Phase 2 staircase objects are named with.
56. **Why only one shadow-casting light in MuseumNight?** The plan names real-time shadow count as the museum's main
    performance risk (D2); the moonlight casts, the four warm exhibit spots do not.
57. **How does the orb leave a trail without editing the orb code?** A world-space `ParticleSystem` child was added
    to the `ChronoOrb` *prefab* by the builder; it emits over time and, being world-simulated, leaves particles
    behind as the orb flies - no change to `ChronoOrb.cs`.
58. **Why was the lightmap bake left manual?** It is long-running and can hang unattended in headless batch mode, and
    the result needs a visual judgement in a real Editor session regardless - the lights are set up so it is a
    one-click improvement, not a blocker.
59. **What in Phase 7 is genuinely still outstanding?** Three things, all documented, none faked: the AudioMixer
    asset (2-minute manual step, code ready), the optional lightmap bake, and the D2 real-hardware framerate check.
