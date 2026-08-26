# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## What this is

**Museum of Time** — a solo student final project for a Game Development course (פיתוח משחקים), built in Unity
6000.4.8f1 (URP). Noa, a night guard, repairs a broken timeline across three eras (Past/Present/Future) and three
scenes (`MuseumNight` → `FrozenCity` → `ClockCore`), plus `MainMenu` and `Victory`.

The project is graded against a fixed, numbered list of technical requirements (T1–T21), submission rules (S1–S10),
general points (G1–G2) and defense rules (D1–D6). That register — and the full build plan derived from it — lives in
`docs/Implementation_Plan.md`. `docs/Implementation_Overview.md` is the condensed, one-line-per-step progress tracker
(check it first to see what phase is in flight). `docs/Phase{0-4}_Unity_Walkthrough.md` are click-by-click Unity
Editor instructions for work that can't be done by editing files alone (baking navmeshes, wiring Inspector
references, Cinemachine setup, etc.) — point the user there instead of trying to script Editor-only steps.

**Read `docs/Implementation_Plan.md` Part 2 (requirement register) and Part 3 (requirement → scene placement) before
touching AI, input, navmesh, teleport, camera, or era code.** Several design choices below look like arbitrary
constraints but are literal requirement wording that a grader checks:

- **Only the New Input System.** No `Input.GetKey`/`Input.GetAxis`/`Input.mousePosition` anywhere, ever (T12).
  `PlayerInputReader` is the single point of contact with the Input System.
- **LayerMasks for raycasts must be built in code** (`LayerMask.GetMask(...)`), not only assigned in the Inspector
  (T17). See `WardenAI.Awake()` and `PlayerInteractor.Awake()` for the pattern — and the comment in `WardenAI` about
  `GetMask` silently returning an empty mask for a misspelled layer name.
- **Two NavMesh agent types need two separate `NavMeshSurface` bakes and genuinely different routes**, not one bake
  shared by both (T13/T16) — see Architecture below.
- **Hidden teleports (Time Anchors) only exist from `FrozenCity` onward** — never add one to `MuseumNight` (T21).
- **Animators must be hand-authored in the Unity Editor, not imported** (T14) — this can't be done by editing files;
  it's an Editor-only task for the user.
- **Eras are sibling GameObject roots toggled active/inactive**, not separate loaded scenes (keeps the mechanic cheap
  and instant — see `EraManager`).
- **300 MB compressed build cap (S1)** — be deliberate about texture sizes, terrain detail density, and shard/LOD
  counts; this is called out in the brief as a judged axis, not a soft guideline.

## Commands

This is a Unity project — there is no separate CLI build/lint/test toolchain; everything routes through the Unity
Editor or `Unity.exe` in batch mode.

- **Open the project:** open the folder in Unity Hub (Editor version `6000.4.8f1`, see
  `ProjectSettings/ProjectVersion.txt`).
- **Compile check without opening the Editor UI:**
  ```
  "<Unity install path>\Editor\Unity.exe" -batchmode -quit -projectPath . -logFile -
  ```
- **Run Play Mode tests** (in `Assets/Tests/PlayMode/`, assembly `MuseumOfTime.PlayModeTests`) from the CLI:
  ```
  "<Unity install path>\Editor\Unity.exe" -batchmode -projectPath . -runTests -testPlatform PlayMode -testResults results.xml -logFile -
  ```
  Or run them interactively via **Window → General → Test Runner → PlayMode** in the Editor — needed for anything
  that depends on navmesh bakes, Cinemachine, or scene objects.
- There is no EditMode test assembly; `MuseumOfTime.PlayModeTests` is the only test project, and it references
  `MuseumOfTime.Runtime` plus the Input System, Cinemachine and AI Navigation packages.
- **Blender tooling** (`Tools/lod_generate.py`, `Tools/voronoi_fracture.py`) — headless asset generation for the LOD
  (T11) and Voronoi fracture (T10) requirements, run outside Unity:
  ```
  blender --background --python Tools/lod_generate.py
  blender --background --python Tools/voronoi_fracture.py
  ```
  Both print triangle counts to stdout (kept as defense/grading evidence) and export FBX into
  `Assets/Models/LOD/` and `Assets/Models/Fractured/` respectively.

## Architecture

All gameplay code lives in one assembly, `Assets/Scripts/MuseumOfTime.Runtime.asmdef` (references: Input System,
Cinemachine, AI Navigation), organized by feature folder rather than by scene:

- **`Core/`** — `GameManager` is a `DefaultExecutionOrder(-100)` singleton (`DontDestroyOnLoad`) that owns the single
  `GameState` instance and exposes all mutation as methods (`TakeDamage`, `Heal`, `SpendEnergy`, `AddScore`,
  `AddTimeShard`, `AcquireTimeLens`, `SaveCheckpoint`, …), each firing `StateChanged` so UI/HUD code stays purely
  reactive. `GameState` is a plain `[System.Serializable]` data bag with `ToJson()`/`LoadFromJson()`
  (`JsonUtility`) and `ClampValues()`; it's the one place cross-scene progress lives (health/energy/score/shards,
  `hasTimeLens`, `hasChronoHourglass`, checkpoint scene+position+era). `SceneLoader` and `SaveService` handle
  transitions and persistence to `Application.persistentDataPath`.
- **`Player/`** — `PlayerInputReader` is the *only* class that talks to the Input System directly: continuous values
  (`moveInput`, `lookInput`) are plain fields, discrete actions (`JumpPressed`, `InteractPressed`, `ShootPressed`,
  `EraForwardPressed`/`EraBackPressed`, `CameraTogglePressed`, `PausePressed`, `JournalPressed`) are edge-triggered
  flags set in the action callback and cleared in `LateUpdate`. `PlayerController` builds movement from the active
  camera's flattened forward/right (not world axes). `PlayerAnimatorDriver` drives the hand-built Animator from
  `CharacterController.velocity`, not raw input.
- **`Camera/`** — `PlayerCameraRig` owns two `CinemachineCamera`s (`CM_FirstPerson`/`CM_ThirdPerson`) toggled by
  Cinemachine priority (so the Brain blends rather than cuts) on `CameraTogglePressed`. Mouse look is applied here
  (yaw rotates the player, pitch tilts a shared camera pivot) so both views stay consistent. The minimap camera is
  independent of this rig — three cameras total in-scene, per T19's wording.
- **`Time/`** — `EraManager` is a singleton holding `TimeEra CurrentEra` (`Past`/`Present`/`Future`); era switching is
  locked until `Unlock()` is called (end of `MuseumNight`) and each switch costs energy via
  `GameManager.SpendEnergy`. It fires `EraChanged`, which `EraPersistentObject` and era-bound scenery listen to.
  `EraPersistentObject` is the signature-mechanic object: its position is captured per-era and *propagates forward*
  to later eras when moved, which is what makes the cross-era puzzles solvable. `ChronoHourglass` is the slow-time
  ability (`Time.timeScale` + matched `fixedDeltaTime`), gated by `GameState.hasChronoHourglass`, restored to normal
  in `OnDisable` so it can never be left stuck slow.
- **`Interaction/`** — `IInteractable` (`Prompt` + `Interact(GameObject)`) implemented by `DoorInteractable`,
  `ExhibitPlaque`, `ItemPickup`, `ShardPickup`. `PlayerInteractor` raycasts from the active camera against a
  code-built `LayerMask` (`Default` + `Interactable`) and exposes `Current`/`CurrentPrompt` for UI.
- **`World/`** — the trigger set (`RoomEntryTrigger`, `TutorialTrigger`, `EraZoneTrigger`, `HazardTrigger`,
  `TimeAnchorTrigger`) are deliberately five distinct `OnTriggerEnter` components rather than one parameterized
  script. `TimeAnchor` is the hidden-teleport object (invisible without `GameState.hasTimeLens`, arms silently,
  `GameManager.SaveCheckpoint` records scene+position+era). `FracturedObject` swaps an intact mesh for pre-baked,
  hidden Voronoi shards on break, applies an explosion force, then despawns the shards after `shardLifetime`.
  `IFreezable` is implemented by both AI agent types so the Chrono Orb can freeze either one uniformly.
- **`AI/`** — two independent agent types on **separate `NavMeshSurface` bakes**, which must stay separate (not
  merged into one bake) because a custom `ShadowOnly` NavMesh area is what makes their routes actually differ:
  - `WardenAI` (agent type A, `NavMeshAgent`-driven): state machine `Patrol → Pause → Alert → Chase → Search →
    Frozen`. Detection is range → cone angle (`viewAngle`) → `Physics.Raycast` line-of-sight against a
    code-built `visionBlockers` mask, in that order (cheapest check first). Uses `SteeringBehaviours.Pursue` to aim
    at the player's predicted position while chasing, not their current one.
  - `ShadowAI` (agent type B): smaller/more permissive navmesh, states `Drift → SeekShard → Flee → Frozen`, uses
    `SteeringBehaviours.Seek`/`Flee`; steals Time Shards on contact (real score loss) and is recoverable by freezing
    it.
  - `PatrolRoute` holds an ordered, optionally ping-ponging waypoint list with a per-waypoint `waitSeconds` — the
    "pause" is the graded part of patrol, implemented as `agent.isStopped = true` (a real stop) plus a head-sweep
    rotation, not just a speed of zero.
  - `SteeringBehaviours` is a static class with methods named `Seek`/`Flee`/`Pursue` *on purpose*, so the steering
    element required by the brief is unambiguous in a code review — don't rename or fold these into the agent
    classes.

## Repository layout notes

- `Assets/Scripts/` is the single script root (an earlier duplicate `Assets/Assets/Scripts/` tree was collapsed away
  during Phase 0 cleanup — don't recreate a second root).
- `Plans/` holds the original brief and GDD source documents (docx/pdf) that `docs/Implementation_Plan.md` was
  derived from — treat `docs/Implementation_Plan.md` as canonical over anything remembered from those source files.
- Scenes are wired into Build Settings in a fixed order: `MainMenu (0) → MuseumNight (1) → FrozenCity (2) →
  ClockCore (3) → Victory (4)`. Don't add a `SampleScene` or reorder without checking `docs/Implementation_Plan.md`
  Part 3 for what each scene is required to demonstrate.
- `Library/`, `Temp/`, `obj/`, `Builds/`, `Logs/`, `UserSettings/` are Unity-generated/local and git-ignored — never
  hand-edit or commit into them.
