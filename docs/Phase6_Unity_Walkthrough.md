# Phase 6 — Unity Walkthrough

**How to rebuild the scene content by hand, and where to see it in the editor.**

Sixth in the series, after `Phase1_Unity_Walkthrough.md` through `Phase5_Unity_Walkthrough.md`. Phase 6 is where the
requirement-placement table (`Implementation_Plan.md` Part 3) gets confronted directly: an earlier audit found that
Phase 3/4's systems — the era manager, the two AI agent types, Time Anchors, hinge joints — were real and proven, but
only inside `MuseumNight`, the tutorial sandbox. FrozenCity and ClockCore, the scenes the plan actually assigns most
of them to, were empty. This phase places them where they belong. Like Phase 5, everything here was built through
headless Editor scripts run in Unity batch mode; there was no interactive Editor session.

**Before you start:** open the Console, click *Clear*. There is no single scene to open first this time — this phase
touches `MuseumNight`, `FrozenCity` and `ClockCore` in turn.

**Revision note.** The first pass through this phase deferred two items — the three-era gear puzzle and the
Collector boss fight — reasoning that they were new gameplay systems rather than configuration of existing ones.
That reasoning was overruled on review: the instruction was to implement the simplest system that satisfies the
documented requirement even when the supporting code does not yet exist, not to skip requirements for that reason.
Both are now built, documented below in their final form, and covered by automated tests. Nothing else in this
document changed from that first pass.

## The menu grew by five items

```
Build Player Prefab                         <- new
Build Scene Connections (Phase 6)           <- new
Build FrozenCity Content (Phase 6)          <- new
Build ClockCore Content (Phase 6)           <- new
```

All idempotent. `Build FrozenCity Content` and `Build ClockCore Content` each depend on `Build Player Prefab` and on
`Build Navigation (two agent types)` (Phase 4) having been run at least once each.

---

# Infrastructure: a reusable Player

## The problem

Noa's `Player` object in `MuseumNight` was hand-assembled across Phases 0–1 directly in that scene — `CharacterController`,
`PlayerInput`, `PlayerInputReader`, `PlayerController`, the camera rig, the Animator — and was never turned into a
prefab. FrozenCity and ClockCore had no Player at all. Placing Phase 3/4 systems into scenes with nobody to trigger
them would not have proven anything.

## What was built

`Assets/Editor/PlayerPrefabBuilder.cs` constructs `Assets/Prefabs/Player/Player.prefab` from scratch in code:
`CharacterController`, a `PlayerInput` wired to `MuseumInputActions` with `InvokeCSharpEvents` notification behaviour
(matching Phase 0's fix), `PlayerInputReader`, `PlayerController` (its field defaults already match Step 1.1's tuned
values — nothing to patch), a `CameraPivot` child carrying the first-person `CinemachineCamera`, `Animator` +
`NoaController`, `PlayerAnimatorDriver`, `PlayerInteractor`, `ChronoHourglass`, and `ChronoOrbLauncher` wired to
`ChronoOrb.prefab`.

**The third-person camera is deliberately not part of this prefab.** `CinemachineThirdPersonFollow` orbits a target
from a scene-root object — the same reason `MuseumSceneSetup.cs` builds `ThirdPersonCamera` directly in the scene
rather than as a player child. Each scene's content builder creates `MainCamera` (+`CinemachineBrain`) and
`ThirdPersonCamera` itself and wires the instantiated prefab's `PlayerCameraRig.thirdPersonCamera` to it afterward.

**MuseumNight was not converted to use this prefab.** It already worked, converting it would not close any Phase 6
requirement, and it would have meant touching a scene the instructions said to leave alone unless required.

---

# Step 6.1 — MuseumNight: the one thing it was missing

Closes: the "leaves for the first exhibit" beat, and S9's coherent link between scenes.

## What was already there

Everything else in Step 6.1's beat list was already real from Phases 0–5: plaques teaching every verb, the staircase,
the Warden, the Time Lens pickup. What did **not** exist was a way to actually leave — Step 3.9's item-acquisition
chain was a real flag (`GameState.hasTimeLens`) but nothing read it to open a door.

## What was built

`Assets/Scripts/World/SceneExitTrigger.cs` — a new `PlayerTrigger` subclass, gated on an acquired item:

```csharp
protected override void OnPlayerEntered(GameObject player)
{
    if (!HasRequiredItem()) { LastExitSucceeded = false; return; }
    LastExitSucceeded = true;
    sceneLoader.LoadScene(targetScene);
}
```

**The bug caught before it shipped:** `PlayerTrigger.onlyOnce` defaults to `true`. Left alone, the very first time the
player touched the exit *without* the item would permanently spend the trigger — walking in later with the Chrono
Hourglass in hand would do nothing, because `OnTriggerEnter` would already consider itself used up. `Awake()` sets
`onlyOnce = false` before calling `base.Awake()`, the same fix `HazardTrigger` uses for a different reason ("a hazard
has to keep hurting"). Here the reason is "the player has to be able to come back once they actually have the item."

`Assets/Editor/SceneConnectionsBuilder.cs` places `Exit_ToFrozenCity` near the Time Lens's own position in
MuseumNight (9, 5.6, 9.5), gated on `TimeLens`, targeting `FrozenCity`.

## How to prove it works

`Assets/Tests/PlayMode/SceneConnectionsTests.cs` (2 tests): walking in without the Lens leaves the player in
MuseumNight; walking in with it loads FrozenCity. Both call `OnPlayerEntered` directly via reflection rather than
simulating a physical walk-through, the same trade-off Step 5.1's button tests made for the same reason (avoiding
batch-mode physics/input timing fragility for a check that does not need it).

---

# Step 6.2 — FrozenCity, made real

Closes: T6 (already done, Phase 2), T21, T13, T16, T17, T7, T5, T15, T10 #2, T9 #2 — the whole right-hand column of
Part 3's placement table for this scene.

## What was already there

The Terrain (Phase 2) and a `ClockTower` placeholder (`Shaft` + `Belfry`), with a comment in `TerrainBuilder.cs`
already pointing at this phase: *"Step 6.2 turns it into the bell puzzle."*

## What was built

`Assets/Editor/FrozenCityContentBuilder.cs`, one builder covering the whole scene:

- **Player + cameras** — the prefab, positioned at `PlayerSpawn` sampled onto the actual terrain height
  (`Terrain.SampleHeight`), plus `MainCamera`/`ThirdPersonCamera` built the same way `MuseumSceneSetup.cs` does.
- **`EraManager`, unlocked from the start.** Unlike MuseumNight, where era travel is locked until the Lens is found,
  FrozenCity's `EraManager` is built with `eraTravelUnlocked = true` — by the time Noa arrives here she already has
  the Lens. `RespawnService` alongside it.
- **Two Time Anchors** (`TimeAnchor_Overlook`, `TimeAnchor_TowerBase`), each carrying both `TimeAnchor` (visibility +
  arming) and `TimeAnchorTrigger` (the component `TriggerLog`/T3's tally actually counts) — mirroring exactly how
  CoreSystemsBuilder already builds `TimeAnchorTrigger` objects, just placed in the scene T21 requires them in.
- **A real hinge bell.** `TerrainBuilder`'s placeholder `Belfry` gets a `TowerBell` child: `Rigidbody` + `HingeJoint`
  with a spring, the same pattern `HingeSetBuilder`'s pendulum uses. No new "ring the bell" script was needed — Step
  3.3's `ChronoOrb` already wakes any `HingeJoint` rigidbody it hits.
- **The second Voronoi fracture, actually placed.** `FrozenStatue.prefab` (built in Phase 2, never instantiated
  anywhere) is now a real object in the scene.
- **The second acquired item.** A `ChronoHourglass`-kind `ItemPickup`, placed near the tower.
- **`Exit_ToClockCore`**, gated on `ChronoHourglass`, the same pattern as Step 6.1's exit.
- **Both AI agent types, on their own bake, on different routes.** A `WardenAgent`/`ShadowAgent` pair (the same
  project-wide types Phase 4 created), each with its own `NavMeshSurface`, baked separately. A wall-with-a-slot and a
  ledge (same trick as `NavigationBuilder.BuildObstacleCourse`, reused rather than reinvented) make the two bakes
  disagree about one route: `1.0m` clearance needed for the Warden, `0.6m` for the Shadow; a `0.7m` ledge inside the
  Shadow's climb, outside the Warden's.

## The three-era gear puzzle

The GDD's "the tower bell never rang, so the moment cannot continue" puzzle, read literally: find a gear in the Past,
install it in the Present, verify it in the Future. Three new scripts, one manager and two interactables:

- **`GearPuzzle.cs`** — a small state holder, the same shape as `EraManager`: `HasGear` / `Installed` / `Verified`,
  each only settable in the era the beat names (`TryInstall` fails outside the Present, `TryVerify` fails outside the
  Future), and a `rewardObject` reference — the Chrono Hourglass pickup — hidden until `Verified` becomes true. This
  is what makes the puzzle load-bearing rather than decorative: **the Hourglass does not exist in the scene until the
  puzzle is solved.**
- **`GearPickup.cs`** — the gear itself, `IInteractable`. Its `Update()` toggles its own renderer and collider based
  on `EraManager.CurrentEra`, so it is only visible and interactable while Noa is in the Past, and does not need to be
  "found" again if she leaves and comes back.
- **`GearSocket.cs`** — one physical object at the tower base serves both remaining steps, since installing the gear
  and later verifying it holds happen at the same place: `Interact()` calls `TryInstall()` if not yet installed,
  otherwise `TryVerify()`.

`FrozenCityContentBuilder.BuildGearPuzzle` places the gear and the socket near `Belfry`, and `BuildHourglassPickup`
now returns the pickup GameObject so the puzzle can wire it as its own reward — the two are built in sequence in
`Build()` specifically so that wiring is possible.

**Why one `GearSocket` handles two steps instead of two objects.** The plan's own beat treats "install" and "verify"
as the same tower mechanism at two different times, not two different places — a second object would have implied a
second location in the world that does not exist in the design.

## Issue found and fixed: NavMesh agent types are not reliably visible from a fresh batch-mode process

`NavigationBuilder.EnsureAgentType` (Phase 4) reads and writes the project's custom NavMesh agent types through
`Unsupported.GetSerializedAssetInterfaceSingleton("NavMeshProjectSettings")`. Calling the same method — and even
`UnityEngine.AI.NavMesh.GetSettingsCount()`/`GetSettingsNameFromID()`, the ostensibly-safe runtime API — from
`FrozenCityContentBuilder` in a **separate, fresh** `-batchmode` process reported **zero** custom agent types, even
though `ProjectSettings/NavMeshAreas.asset` already had `WardenAgent`/`ShadowAgent` correctly written to disk. One
attempt to work around it by calling `EnsureAgentType` a second time produced a genuine bug: both agent types
resolved to the **same** id. Nothing in the batch-mode session had told the runtime navmesh module to load the
settings asset — that normally happens when the Navigation window is opened, which a pure `-batchmode` run never
does.

The fix: `FrozenCityContentBuilder.ReadAgentTypeId` (and the same method in `ClockCoreContentBuilder`) reads
`ProjectSettings/NavMeshAreas.asset`'s plain YAML directly — `m_SettingNames` and `m_Settings` are parallel arrays, so
the name's index in one gives the id's index in the other. This sidesteps the Editor-side caching question entirely.
`NavigationBuilder.cs` itself was not changed; a visibility-only edit to reuse its method was tried first, tested,
found to not fix the underlying problem either, and reverted (confirmed via `git diff` showing zero changes to that
file).

## Issue found and fixed: FrozenCity's Terrain asset would not load

The very first attempt to run PlayMode tests against FrozenCity failed **every single test in the fixture**,
including the simplest one (`Player_IsFullyControllable`). The actual cause, buried in a "SetUp" failure message:

```
Unknown error occurred while loading 'Assets/Terrain/FrozenCityTerrainData.asset'.
```

`git status` confirmed this asset was byte-identical to its Phase 2 commit (`ff70c72`) — nothing in this phase had
touched it. This is a **pre-existing Phase 2 defect**: nobody had ever actually loaded FrozenCity in Play Mode before
(Phases 2–4 only ran batch-mode Editor scripts against it), so the corrupt load was never triggered. Unity's Test
Framework fails any test during which an unhandled error is logged, which is why it took down the whole fixture and
even bled into an unrelated `HudAndPauseMenuTests` case that happened to run around the same time.

The fix: re-ran Phase 2's own idempotent `TerrainBuilder.BuildFromCommandLine()`, which deletes and rebuilds
`FrozenCityTerrainData.asset` from the same sculpting code with identical parameters. The rebuilt asset is the same
size and produces the same terrain; it loads cleanly. This is a fix to Phase 2 content, not Phase 2 code — no `.cs`
file changed — made because it was a direct blocker to verifying this phase's own requirements, not an unrelated
cleanup.

## How to prove it works

`Assets/Tests/PlayMode/FrozenCitySceneTests.cs` (14 tests): Player fully equipped and tagged; era travel starts
unlocked; at least two Time Anchors exist and each carries `TimeAnchorTrigger`; a Time Anchor's marker is hidden
without the Lens and visible with it; both agent types present with different dimensions and both `isOnNavMesh`; the
two agent types' paths to the same destination actually differ; the bell is a real `HingeJoint` + `Rigidbody`; the
frozen statue is a placed `FracturedObject` with its shards; the exit refuses to leave without the Hourglass and
succeeds with it. For the gear puzzle specifically: the Hourglass pickup exists but starts hidden; `TryInstall`
fails without the gear and outside the Present, succeeds in the Present with the gear in hand; `TryVerify` fails
outside the Future, succeeds in the Future once installed, and revealing the Hourglass is the observable proof it
worked; the gear itself is only interactable while `EraManager.CurrentEra == Past`.

These are automated tests, not a raw scene-file inspection, for a specific reason: baking real NavMesh data into
FrozenCity embeds binary tile-data byte arrays directly in the scene file, so `file` now reports
`Assets/Scenes/FrozenCity.unity` as `data` rather than `ASCII text` — the same thing that already happened to
MuseumNight in Phase 4. Grepping the raw file for component names is no longer reliable there, which is exactly why
this phase leaned on `Assets/Tests/PlayMode/` instead.

---

# Step 6.3 — ClockCore: infrastructure and the Collector

Closes: the "both agent types present" cell of Part 3's placement table for this scene, the Time Anchors T21 asks
for ("×2 more"), and the three-phase Collector confrontation.

## What was already there

Nothing. Not even a floor. `ClockCore.unity` before this phase was pure Phase 0.3 scaffolding — empty named parents
(`Architecture`, `Enemies`, `Triggers`, …) and nothing else.

## What was built

`Assets/Editor/ClockCoreContentBuilder.cs`:

- **A greybox floor** — a single flat 40×40m slab. ClockCore had no walkable surface at all before this.
- **Player + cameras**, same pattern as FrozenCity, minus terrain height sampling (the floor is flat).
- **`EraManager`, unlocked** (both items already held by this point), **`RespawnService`**.
- **Two more Time Anchors** (`TimeAnchor_EastWing`, `TimeAnchor_WestWing`).
- **Both AI agent types**, each on its own `NavMeshSurface` bake over the flat floor, with a simple patrol loop for
  the Warden — the Warden is the one the Collector summons in Phase 2, below, so `BuildCollector` finds and reuses
  the same `TimeWarden` object `BuildNavigationAndAgents` already placed, rather than baking a second one.

## The Collector: three phases, one per era

`Assets/Scripts/AI/Collector.cs`. The era switch is the fight's own mechanic, read directly from the plan:

```csharp
private void OnCollisionEnter(Collision collision)
{
    if (collision.gameObject.GetComponent<ChronoOrb>() != null)
    {
        RegisterOrbHit();
    }
}
```

`RegisterOrbHit()` holds the actual phase logic, and only advances if Noa is standing in the era that phase names:

- **Phase 1 (Past) — shielded.** A `Shield` sphere sits over the Collector. Two orb hits **landed while in the Past**
  (hits outside the Past do not count at all) break it; breaking it also activates the summoned `TimeWarden`, which
  starts inactive.
- **Phase 2 (Present) — summons a Warden.** One orb hit **landed while in the Present** advances to the final phase.
  The Warden is already active and patrolling by this point, which is the "summons Wardens" beat.
- **Phase 3 (Future) — the Hourglass is mandatory, not optional.** `Update()` deals continuous damage
  (`erosionDamagePerSecond`) whenever the current phase is Future and `ChronoHourglass.IsSlowing` is false — this is
  the plan's own line, "time is nearly erased," made into an actual mechanic rather than flavour text. A hit only
  defeats the Collector if it lands **while in the Future and while the Hourglass is active**; a hit without it is
  simply ignored, so there is no way to end the fight without engaging the ability. This stands in for the GDD's
  Restorer "undo" mechanic in the simplest form that is still real: going without the Hourglass here does not stall
  the fight, it actively costs Noa health for as long as she does.

On the third condition being met, `Collector.Defeat()` calls `SceneLoader.LoadScene("Victory")` directly — the same
`SceneLoader` class every other scene transition in this project uses, not a new mechanism.

**Why `RegisterOrbHit` is a separate method from `OnCollisionEnter`.** `UnityEngine.Collision` has no public
constructor, so nothing outside the physics engine can construct one to drive `OnCollisionEnter` directly in a test.
Splitting the actual decision logic out into a plain method that a test can call by reflection was the difference
between being able to test the fight at all and not.

## What is simpler than the full GDD beat, and why that is still honest

- **No separate Restorer character.** The plan frames the Restorer's undo mechanic as living *inside* Phase 3, not as
  a second boss - the erosion-damage-without-the-Hourglass mechanic is that beat's simplest real form: failing to
  engage the ability costs Noa health for as long as she goes without it, rather than a bespoke system that reverts
  specific past actions.
- **No separate "return the Time Shards" gate.** `GameState.timeShards` already accumulates from Steps 3.4/3.9;
  Victory's own screen already displays the total (Step 5.1). Adding an arbitrary shard-count threshold before the
  Collector can be damaged would be a new invented rule the plan does not actually specify a number for.
- **The shield/summon/erosion numbers (2 hits, one Warden, 12 damage/second) are placeholders**, the same way
  `hitsToBreakShield` on the pendulum-style hinges in Phase 2 used round numbers rather than playtested ones - this
  is a greybox implementation of the mechanic, not a tuned encounter.

## Verifying it does not defeat itself by accident

The property that matters most to prove is the one the plan states as its own verification bullet - "the fight
cannot be won without the Hourglass" - so `Phase3_CannotBeWonWithoutTheHourglass` checks the negative case explicitly
(a Future-phase hit while not slowing time leaves `IsDefeated` false) immediately before checking the positive one,
rather than only checking that winning is *possible*.

---

# Step 6.4 — Coherence pass

Closes: S9, as far as it can be closed without a human playing the game.

## What was verified end to end

The acquisition chain now has real doors on both ends: `SceneConnectionsTests.cs` (2 tests, MuseumNight) covers
"Lens required to leave for FrozenCity"; `FrozenCitySceneTests.Exit_*` (2 tests, already listed under Step 6.2)
covers "Hourglass required to leave for ClockCore". Together these are the automated half of S9's chain —
**Lens → FrozenCity → Hourglass → ClockCore** — each link a real trigger a test actually walks through (via
`OnPlayerEntered`, not just a flag check), not merely `GameState` fields that happen to exist.

## What was not attempted

- **The narrative thread** — the plan's aside about the Old Curator's notes appearing in all three scenes,
  progressively revealing the Collector. Phase 5 already left this out of MuseumNight's tutorial text for the same
  reason: it is copy-writing and voice, not a structural requirement. FrozenCity and ClockCore currently have no
  narrative text at all — no plaques, no NPC dialogue. The Collector fight is now real but silent.
- **Consistent visual language** — palette, era-switch VFX, lighting. FrozenCity and ClockCore's new content uses
  plain primitives and the existing Museum materials; this is greybox work, not an art pass. Phase 7 territory.
- **Playing the whole game start to finish in one sitting.** This phase's own verification note for this step says
  exactly that is how S9 gets checked. It has not been done — there is no way to automate "does this feel like one
  game" any more than Phase 5 could automate "does the HUD read well".

---

# Verifying the whole phase

## Automated: `Tools/verify.ps1`

```
=== Step 1/2: compiling ===
Compile OK (exit code 0, no CS errors in Setup.log)

=== Step 2/2: running PlayMode tests ===
Tests: 67/73 passed, 0 failed (result: Skipped:Ignored)

RESULT: PASS
```

73 tests total — the 49 from Phases 0–5 (43 real passes + 6 intentionally ignored, unchanged) plus 24 new ones,
across three files:

| File | Tests | Covers |
|---|---|---|
| `SceneConnectionsTests.cs` | 2 | Step 6.1 / 6.4 |
| `FrozenCitySceneTests.cs` | 14 | Step 6.2, including the gear puzzle |
| `ClockCoreSceneTests.cs` | 8 | Step 6.3, including the Collector's three phases |

## Manual/visual — could not be automated, and were not faked

- **Actually playing FrozenCity and ClockCore** — walking the terrain, finding the gear, fighting the Collector from
  a first-person view, ringing the bell, watching the statue shatter. Structural presence, gating logic and phase
  transitions are tested; how any of it *feels* to play is not.
- **Balance and pacing of the gear puzzle and the Collector fight** — the shield's hit count, the erosion damage
  rate, the gear/socket placement are placeholder numbers, the same way Phase 2's hinge joints used round numbers
  before anyone play-tested them. A greybox pass, not a tuned encounter.
- **Scale and dressing of the new greybox content** — the FrozenCity obstacle course, the Collector's capsule-and-
  sphere shield, and ClockCore's floor are functional, not art-directed.
- **The full S9 playthrough** — the acquisition chain is tested end to end programmatically; a human has not yet
  played MuseumNight → FrozenCity → ClockCore → Victory back to back.

---

# Things you should be able to answer in the defense

Continuing the list from Phases 1–5:

41. **Why was a Player prefab built now, and not earlier?** MuseumNight's Player was hand-assembled directly in that
    scene across Phases 0–1 and never needed to exist anywhere else until FrozenCity and ClockCore needed their own.
42. **Why is the third-person camera not part of the Player prefab?** `CinemachineThirdPersonFollow` needs a
    scene-root object to orbit the player's pivot from; baking it into the prefab would make it move with the player
    instead of around them.
43. **Why does `SceneExitTrigger` set `onlyOnce = false`?** Walking into an exit without the required item must not
    permanently spend the trigger - the player has to be able to come back once they actually have it. Same fix
    class as `HazardTrigger`, different reason.
44. **Why does FrozenCity's `EraManager` start unlocked when MuseumNight's starts locked?** By the time Noa reaches
    FrozenCity she already found the Time Lens in MuseumNight; the lock in MuseumNight exists specifically to teach
    one verb at a time before era travel is introduced.
45. **Why did resolving a NavMesh agent type id by name fail from a fresh batch-mode process?** Nothing in that
    process had told the runtime navmesh module to load `ProjectSettings/NavMeshAreas.asset` - that normally happens
    when the Navigation window opens, which never happens in pure `-batchmode`. Reading the settings file's own YAML
    directly sidesteps the question.
46. **Why was `FrozenCityTerrainData.asset` failing to load, and how do you know it was not this phase's fault?**
    `git status` showed it byte-identical to its Phase 2 commit before the fix; nobody had loaded FrozenCity in Play
    Mode before, since Phases 2-4 only ran batch-mode Editor scripts against it, so the defect was real but latent.
47. **How does ClockCore reach Victory if there is no exit trigger there?** It does not need one - `Collector.Defeat()`
    calls `SceneLoader.LoadScene("Victory")` directly once all three phases clear. The transition is the boss fight's
    own resolution, not a door in the level.
48. **Why does a Chrono Orb hit only count in the Collector fight sometimes?** Each phase only accepts a hit landed
    while Noa is in the specific era that phase names - hitting the Collector in the wrong era does nothing at all,
    which is what makes switching era the actual mechanic rather than a precondition checked once.
49. **Why is `RegisterOrbHit` a separate method from `OnCollisionEnter` on `Collector`?** `UnityEngine.Collision` has
    no public constructor, so a test cannot fabricate one to invoke the Unity message method directly. The phase logic
    lives in a plain method a test can call by reflection instead.
50. **What actually makes the Hourglass "mandatory" rather than "helpful" in Phase 3?** Two separate effects: standing
    in the Future without it active costs health every frame via `Collector.Update()`, and a hit landed without it
    active is simply ignored rather than counted - there is no way to end the fight without it, not just a harder way.
51. **Why does the gear puzzle use one `GearSocket` object for both installing and verifying, instead of two?** The
    plan's own beat treats them as the same tower mechanism at two different times, not two different places.
52. **Why does the Chrono Hourglass pickup not exist in the scene until the gear puzzle is solved?** So the puzzle
    actually gates the reward instead of sitting decoratively next to it - `GearPuzzle.TryVerify()` is what calls
    `SetActive(true)` on it, not the scene's initial state.
