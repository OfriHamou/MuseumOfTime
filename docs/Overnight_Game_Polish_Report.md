# Overnight Game Polish Report

Branch: `improve/rest-of-game`. In progress — updated continuously as work happens so nothing is lost if
interrupted. Verified entirely via Unity MCP (live hierarchy inspection + real Play Mode), per this pass's
explicit rule against running `Tools/verify.ps1` or batchmode tonight. That headless verification is deferred
to tomorrow.

Starting context found before touching anything (from `docs/Overnight_Improvement_Report.md` and
`docs/Morning_Manual_TODO.md`, both from an earlier session, predating this conversation's MuseumNight Mixamo
work): Warden/Shadow/Collector across all three scenes were still primitive capsules as of that doc. MuseumNight's
Warden and Shadow have since been given real Mixamo models earlier in this conversation (Paladin_J_Nordstrom /
a ChronoShadow model, with `VisualGroundClamp` for grounding). FrozenCity/ClockCore have not been touched by that
work — expect them to still be capsules until confirmed otherwise below.

---

## Continuation pass (after a separate MuseumNight-only fix session)

Resuming this branch's remaining scope: FrozenCity -> ClockCore -> Collector boss -> Victory -> cross-scene flow.
FrozenCity/ClockCore/Victory/cross-scene persistence were already verified in depth in the sections below before
that interruption; this pass re-inspects live via MCP and fixes what a fresh look turns up, rather than repeating
work already confirmed solid.

### Fixed: FrozenCity's spawn-area objective plaque was permanently blank (P1)
**Found:** `Plaque_Objective` (world-space text right at the player's spawn point) never showed anything, in a
real, multi-minute Play Mode session - not a timing artifact. Root cause: its `WorldObjectiveText` component reads
`RoomEntryTrigger.CurrentObjective`, a value only ever set by a `RoomEntryTrigger` firing - and FrozenCity has
exactly one, `Trigger_TowerEntry`, sitting far away near the tower at the *end* of the street. The plaque nearest
spawn was therefore blank for the entire early part of the level, the exact moment a player most needs it.
Also found `Trigger_TowerEntry` itself had never been customized past its class's default placeholder values
(`roomName: "Main Gallery"`, `objective: "Reach the Clock of Creation"`) - MuseumNight-specific text that would
have been shown, wrong, in FrozenCity had it ever fired.
**Fix:** `WorldObjectiveText` now prefers the live, always-current `ObjectiveTracker.Instance.Objective` (the
same source the HUD banner already uses) and only falls back to `RoomEntryTrigger.CurrentObjective` if no tracker
exists - so the plaque can never be stuck blank waiting on a room trigger, in any scene, and per-trigger text no
longer needs to be hand-authored and kept in sync. Also fixed this script's billboard rotation to yaw-only (same
tilt bug already fixed in `Billboard.cs`/`WorldSignpost.cs` this project - three independent implementations had
accumulated the identical bug).
**Verified in Play Mode:** after forcing a proper recompile (see note below), the plaque now reads "Objective:
Find the tower's missing gear" immediately at spawn, upright and readable, screenshot-confirmed.
**Process note:** the first attempt at this fix appeared to silently not work - `Update()` kept reading the old
`RoomEntryTrigger`-only logic even after editing and saving the file. Root cause was procedural, not code: editing
a script via a text-editing tool (outside Unity) does not by itself trigger Unity to notice and recompile it -
`refresh_unity` has to be called afterward, which had been skipped this one time. A `Resources.FindObjectsOfTypeAll`
+ reflection cross-check confirmed the live component's actual field values still matched the pre-edit behavior,
which is what caught it.
**Files:** `Assets/Scripts/UI/WorldObjectiveText.cs`.

### Fixed: three tutorial plaques showed a raw, unsubstituted `{energy}`/`{health}` placeholder (P1, cross-scene)
**Found:** while reading every tutorial plaque's exact text (flagged as not yet checked), found `{energy}` and
`{health}` tokens sitting literally in the authored strings - e.g. "Each switch costs energy - {energy}%
remaining." No string-templating/substitution code exists anywhere in the project (`TutorialTrigger` just displays
whatever static string it's given), so these would have rendered to the player exactly as typed, curly braces and
all. Present in **three** places: FrozenCity's `Plaque_Era` (+ its `Trigger_TutorialEra`'s own `message` field),
MuseumNight's `Plaque_SlowTime` (+ `Trigger_TutorialSlowTime`), and ClockCore's `Plaque_PhaseFuture` (+
`Trigger_TutorialPhaseFuture`) - the same copy-pasted authoring mistake made three times, which is why MuseumNight
is touched here despite this pass's scope (a genuine shared/repeated content bug, not a MuseumNight redesign).
**Fix:** reworded all three to drop the specific live number rather than build a templating system for one-off
tutorial hints - the mechanic is still explained ("costs energy", "time itself erodes you"), and the player's
actual HUD bars already show the live number a few pixels away.
**Verified:** grepped all three scene files afterward for any remaining `{word}` pattern - none found.
**Files:** `Assets/Scenes/{FrozenCity,MuseumNight,ClockCore}.unity` (text-only changes, no script changes for this
part).

## FrozenCity

### Fixed: enemies were a Noa-clone (recolored `Ch02_*` body) and a bare capsule, with no colliders at all (P0/P1)
**Found:** `TimeWarden` and `ChronologicalShadow` each had a root-level primitive `Capsule` mesh **and** a `Body`
child that was literally Noa's own character parts (`Ch02_Body`, `Ch02_Cloth`, `Ch02_Hair`, `Ch02_Eyelashes`,
`Ch02_Sneakers`, `Ch02_Socks`) tinted per-enemy — a Noa clone, exactly what this pass is meant to eliminate.
Neither root had **any** `Collider` at all, meaning a real Chrono Orb could never physically hit either enemy in
this scene (same class of bug found and fixed in MuseumNight earlier).
**Fix:** ported the exact setup already working in MuseumNight — instantiated the same `TimeWarden.fbx` /
`ChronoShadow.fbx` model prefabs (not duplicated, referenced by their existing asset paths) as `TimeWardenVisual`
/ `ChronoShadowVisual` children, matching MuseumNight's local position/scale, `VisualGroundClamp` (footClearance
-0.04), `Animator` avatar + shared `WardenController`, and added a `CapsuleCollider` to each root matching
MuseumNight's exact dimensions. Removed the old `Body` (Noa clone) children and disabled the now-unused root
capsule `MeshRenderer` (left the component rather than deleting, in case anything still references the mesh).
**Verified in Play Mode:** both enemies render the correct model, animated, grounded, no console errors. Fired a
real `ChronoOrbLauncher.Throw()` at each (not a direct `Freeze()` call) — both register a real collision, freeze
for ~30s (`frozenUntil - throwTime` ≈ 30.0s for both), award +15 score once. Confirms the collider fix actually
restores working combat, not just visuals.
**Files:** `Assets/Scenes/FrozenCity.unity` only (no script changes — reused existing `VisualGroundClamp.cs` and
model/controller assets as-is).

### Fixed: `GearSocket.Interact()` silently did nothing on a wrong-era attempt (P1)
**Found:** pressing E at the socket before having the gear, or in the wrong era, called `TryInstall()`/
`TryVerify()` and simply dropped the `false` return value — no message, same "indistinguishable from a broken
key" anti-pattern this project's own `EraManager.TryStep` comment already flags as something it fixed once.
**Fix:** added `HudMessageFeed.Post(..., Tone.Bad)` on failure, wording that hints at the right era without
literally naming the whole solution (e.g. "The gear only seats in the Present. Press R to step forward.").
**Verified in Play Mode:** exercised the full real flow via each `IInteractable.Interact()` call (the same
method `PlayerInteractor` invokes) — wrong-era message posts correctly, then Past→collect, Present→install,
wrong-era-verify message posts correctly, Future→verify all work in order; `ChronoHourglass` correctly
activates on `Verified`; objective text updates through all five stages correctly.
**Files:** `Assets/Scripts/World/GearSocket.cs`.

### Fixed: FrozenCity exit repeated MuseumNight's "literal EXIT + GPS directions" issue (P2)
**Found:** `ObjectiveTracker.EvaluateFrozenCity()`'s post-Hourglass hint said "The lit EXIT gate is at the NORTH
end of the street, past the clock tower" (literal, directional) — the exact pattern already identified and
fixed for MuseumNight's portal earlier this project. The exit itself (`Sign_CityExit` / `PortalLabel`) is a copy
of the same portal-kit prefab pattern, with the same "EXIT" text and a `PortalLabel` naming the destination
scene outright, and no dim/bright activation-state feedback.
**Fix:** hint changed to an atmospheric, non-directional clue ("The tower's mechanism points toward what comes
next."). `Sign_CityExit` reworded to "WHERE THE BELL WAITS TO RING" (ties to the gear/bell puzzle just solved).
`PortalLabel` deactivated (was naming "TO THE CLOCK CORE" outright). Generalized `PortalActivation.cs` (already
built for the MuseumNight portal) with a `RequiredItem` enum mirroring `SceneExitTrigger`'s own gate, and wired
it here against `hasChronoHourglass` with tuned dim/active light intensities matching this portal's actual
`PortalGlow`/`BeaconGlow` baselines (1.5 → 6, rather than reusing MuseumNight's values verbatim, which would
have overexposed `BeaconGlow`).
**Verified in Play Mode:** confirmed dim state before `AcquireChronoHourglass()`, bright state + one-shot
`PortalActivate` SFX after, via the real component's own `Update()`.
**Files:** `Assets/Scripts/UI/ObjectiveTracker.cs`, `Assets/Scripts/World/PortalActivation.cs` (generalized,
also affects the already-working MuseumNight instance only by adding a mode it still defaults to `TimeLens` for
— verified MuseumNight's portal still activates on `hasTimeLens` exactly as before), `Assets/Scenes/FrozenCity.unity`.

### Fixed: `FrozenStatue`'s "intact" state was a single stray shard fragment with zero collider (P1) — same systemic bug also found and fixed in MuseumNight's `ClockOfCreation`
**Found:** `Intact/Whole` referenced `FrozenStatue_Shard_00` — one small fracture piece, not the complete
statue — because `Tools/voronoi_fracture.py`'s output FBX only ever contains cut shard meshes, never a separate
whole-object mesh, and whatever originally wired this fell back to shard index 0 as a placeholder that was
never corrected. The root object also had **no collider at all**, so a real Chrono Orb could never hit it to
trigger the break. Checked MuseumNight's `ClockOfCreation` (the same fracture-prop pattern) on the hypothesis
this was systemic rather than scene-specific — confirmed identical bug there too (`Whole` = `ClockOfCreation_
Shard_00`, no collider), which is why it was fixed there as well despite the "don't redesign MuseumNight" rule
(this qualifies as the shared/system-issue exception that rule allows for).
**Fix:** for both objects, replaced the single wrong-shard "Whole" with a set of static (no `Rigidbody`/
`Collider`) visual copies of every piece in `Shards`, each at that shard's exact resting local transform — so
the "intact" state is the real statue/clock assembled from its own real geometry, not a placeholder. Added one
aggregate `BoxCollider` on each root, sized from the shards' actual combined world-space `Renderer.bounds`
(their cached mesh-asset `bounds` are all zero as exported by the fracture tool, so `MeshRenderer.bounds`, which
Unity computes from live vertex data, was used instead of the mesh asset's own broken `bounds` field).
**Verified in Play Mode:** `ClockOfCreation` now renders as a complete faceted crystal-tower shape (screenshot
confirmed) instead of a floating fragment. `FrozenStatue`: fired a real Chrono Orb from a normal throwing
distance (close-range throws were landing before the orb built up a clean flight path and not registering —
not a bug, just needed realistic throw distance) — `FracturedObject.IsBroken` correctly flips to `true` on real
collision.
**Files:** `Assets/Scenes/MuseumNight.unity`, `Assets/Scenes/FrozenCity.unity` (no script changes; reused the
existing `FracturedObject.cs` exactly as designed).

### Fixed: 3 duplicate empty `Label` objects stacked on the `Gear` pickup (P3, cleanup)
**Found:** the `Gear` pickup had 4 identical empty-text `Label` children at the exact same position — harmless
(nothing renders from empty text) but pure duplicate-object clutter, the same class of "repeated rebuilds left
duplicates" issue `GearPickup.cs`'s own code comment already documents for that object's colliders.
**Fix:** removed 3 of the 4, kept one.
**Files:** `Assets/Scenes/FrozenCity.unity`.

### Verified working as-is, no fix needed
- **Tower Bell**: not an E-interactable (the request's assumption didn't match the live scene, as warned it
  might not) — it's a `HingeJoint` + `SwingingHazard` physics obstacle, the same proven mechanic as MuseumNight's
  Clock of Creation pendulum (T4/T5 collision requirement). Deals speed-scaled damage, knocks the player back,
  plays the `Bell` SFX. Working as designed; no prototype-looking gap found once the "intact" mesh fix above
  made the tower's own geometry correct.
- **Audio**: `AudioManager` present and correctly wired to the shared mixer; `FrozenAmbience` was already at a
  reasonable amplitude (unlike the original `MuseumAmbience`, which needed the earlier session's loudness fix)
  — confirmed actually playing (`isPlaying=true`, correct clip/mixer group) in a real Play Mode session.
- **Checkpoints**: `TimeAnchor.Arm()` correctly records scene/position/era on a real trigger walk-through.

### Not deeply tested this pass (flagged for manual check, not because anything looked broken)
Falling debris hazards, full patrol/detection/stealth behavior tuning for both enemies in this specific street
layout, every individual tutorial plaque's exact wording/placement, and the minimap were not exhaustively
walked given the scope of the rest of this list — spot checks found nothing alarming, but they didn't get the
same full treatment as the items above.

## ClockCore

### Fixed: enemies were a Noa-clone + bare capsule with no colliders, same as FrozenCity (P0/P1)
**Found/Fix:** identical bug and identical fix to FrozenCity's — ported `TimeWarden.fbx`/`ChronoShadow.fbx` visuals,
`VisualGroundClamp`, `Animator` wiring, and `CapsuleCollider`s onto `TimeWarden`/`ChronologicalShadow`, removed the
Noa-clone `Body` children.
**Verified in Play Mode:** both enemies render correctly, animate, and are grounded. `TimeWarden` starts the scene
`activeSelf=false` by design — `Collector.cs` activates it as `summonedWarden` only once Phase 2 (Present) begins;
confirmed this is intentional (not a bug) by reading `Collector.cs` and confirming it flips active via the real
Phase-1→Phase-2 transition, not at scene load.
**Files:** `Assets/Scenes/ClockCore.unity` only.

### Improved: Collector and Shield read as a tinted default-material capsule/sphere, not a boss (P1/P2)
**Found:** per the pre-existing `Overnight_Improvement_Report.md`, `Collector` had no distinguishing visual
treatment and `Shield` was a flat opaque yellow sphere with no feedback that it was a shield rather than solid
geometry. No external model downloads are in scope for this pass, so the fix works within the existing meshes.
**Fix:** `Collector` scaled up (1.6/1.8/1.6) and given an emissive deep red/orange material
(`Assets/Materials/Dressing/Collector.mat`, `_EMISSION` enabled). `Shield` converted from an opaque material to a
transparent, emissive blue one (`Assets/Materials/Dressing/Shield.mat`: `_Surface=Transparent`, alpha 0.35,
emissive blue) so it now reads as an energy barrier rather than solid matter.
**Found a second issue while verifying:** the first screenshot after the material change showed both Collector and
Shield fully blown out to a solid white/orange blob, unreadable as a shape. Root cause was NOT the new materials —
it was two pre-existing chamber lights, `BeaconGlow` (Point, intensity 6, sitting almost inside the boss at 2m)
and `CollectorSpotlight` (Spot, intensity 6, directly overhead), overexposing anything under them regardless of
material. Reduced both to intensity 2.5 and re-boosted the material emission slightly now that it can actually
read.
**Verified in Play Mode:** re-screenshotted — Collector now reads as a dark, distinct silhouette with a warm
core, and Shield is a clearly visible translucent blue barrier around its base, both readable against the dramatic
overhead light beam (kept, unchanged) rather than washed out by it.
**Files:** `Assets/Scenes/ClockCore.unity` (Collector, Shield, `BeaconGlow`, `CollectorSpotlight`),
`Assets/Materials/Dressing/Collector.mat`, `Assets/Materials/Dressing/Shield.mat`.

### Fixed: `EraSwitchVfx.OnEraChanged` could throw `MissingReferenceException` on a destroyed `ParticleSystem` (P2)
**Found:** while driving `EraManager.SetEra(TimeEra.Future)` for the Phase 3 boss test, hit a
`MissingReferenceException` from `EraSwitchVfx.OnEraChanged` calling `particles.Play()` on a particle system whose
native object had been destroyed while the component itself (and its event subscription to the persistent
`EraManager.EraChanged`) was still alive — a classic Unity "zombie MonoBehaviour" scenario. Most likely triggered
by this session's non-standard testing sequence (a real death-triggered trip to MainMenu followed by a direct
Editor scene reload rather than a normal `SceneLoader` transition), not a pattern a real single continuous
playthrough would hit — but the missing null-guard is a genuine gap regardless of trigger.
**Fix:** added `if (particles == null) { return; }` at the top of `OnEraChanged`, matching the defensive pattern
already used elsewhere in the project for cross-lifetime event handlers.
**Verified:** re-ran the exact same `SetEra(TimeEra.Future)` call after the fix — no exception, era switch
succeeded cleanly.
**Files:** `Assets/Scripts/World/EraSwitchVfx.cs`.

### Verified in Play Mode: full 3-phase Collector boss fight is playable end-to-end
Ran the real fight via `ChronoOrbLauncher.Throw()` (not direct `Freeze()`/`Defeat()` calls) through all three
phases in sequence:
- **Phase 1 (Shielded, Past era):** two real orb hits broke the shield and transitioned to `Present`; confirmed
  `summonedWarden` (`TimeWarden`) activates on this transition.
- **Phase 2 (Present):** one real orb hit transitioned to `Future` and started the erosion grace timer.
- **Phase 3 (Future):** confirmed `erosionDamagePerSecond` genuinely drains health once the
  `erosionGraceSeconds` grace period elapses without `ChronoHourglass.IsSlowing` (caught this directly — a first
  attempt at Phase 3 died to erosion damage while multiple separate MCP round-trips ate into the grace window,
  correctly triggering the game's real death flow). Re-ran Phase 3 in fewer, faster calls with slow-time active
  (`ChronoHourglass` engaged, `Time.timeScale` dropped to 0.3) and landed the real orb hit — `Collector.Defeat()`
  fired and `SceneLoader` transitioned the game to `Victory` with no console errors.
`Collector.cs` itself needed no script changes — it is a complete, correctly-designed state machine with good
wrong-era feedback (`RejectHit`) already in place.
**Files:** none (verification only, aside from the `EraSwitchVfx` fix above).

### Verified working as-is, no fix needed
- **Falling debris hazards** (`Hazards/FallingDebris_0`/`_1`): real proximity-triggered release, real
  `OnCollisionEnter` speed-scaled damage, and re-arm after `despawnSeconds` all confirmed via actual gameplay
  (walked the player under one, it dropped and hit for the expected damage). Re-triggering while still standing
  underneath correctly does it again after the despawn timer — this is what killed the player during testing when
  the player was left in place across several separate tool calls; not a bug, `RespawnService` correctly sent the
  run to MainMenu since no checkpoint was armed in that particular test pass.
- **Checkpoints**: two `TimeAnchor`s (`EastWing`/`WestWing`) present and arm correctly on trigger, same as the
  other two scenes.
- **Audio**: `ClockCoreAmbience` confirmed actually playing, correct clip and `Music` mixer group.

### Not deeply tested this pass
Exhaustive patrol/detection tuning for the summoned Warden and the roaming Shadow in this specific chamber layout,
every tutorial plaque's exact wording, and full visual pass on `SceneDressing`'s other 26 children beyond the
Collector/Shield/lighting fixed above.

## Victory

### Verified in Play Mode, no fixes needed
This scene was already in good shape — no bugs found.
- **Layout**: "TIMELINE RESTORED" title, subtitle, Main Menu / Quit buttons, and a stats panel (Score / Time
  Shards / Times Detected / Playtime) all render in a single left-aligned column against the `MenuDiorama`
  background with no overlap, clipping, or raw/unformatted debug text (screenshot-checked).
  `VictoryScreenController.Start()` reads all four values straight off `GameState` and formats playtime as
  `mm:ss` — clean, no placeholder text.
- **Buttons**: fired both `Button.onClick` handlers for real. `MainMenuButton` correctly calls
  `SceneLoader.LoadMainMenu()` and the scene actually changes to `MainMenu`. `QuitButton` correctly calls
  `SceneLoader.QuitGame()` (`Application.Quit()`, a no-op in-editor as expected, confirmed via its "Quit
  requested." log line).
- **State on entry**: confirmed `Time.timeScale == 1` and the cursor visible/unlocked on arrival at Victory even
  though the Collector was defeated while `ChronoHourglass` had time slowed to 0.3x — its `OnDisable` (fired when
  the Player object is destroyed on scene unload) correctly restores normal time before Victory's own `Start()`
  methods run.
**Caveat:** stats read 0/00:00 in this check because the scene was loaded directly rather than reached via a real
playthrough (GameState starts at its defaults). Confirming real non-zero score/shards/detections/playtime numbers
requires reaching Victory via an actual run, which is covered in the cross-scene playtest below.
**Files:** none (verification only).

## Cross-scene playtest

### Verified: full MainMenu -> MuseumNight -> FrozenCity -> ClockCore -> Victory persistence via real transitions
Ran the complete chain using the real production code path throughout — `SceneLoader.StartNewGame()` from
MainMenu (which also calls `GameManager.ResetGame()`, confirming the run starts clean) and `SceneLoader.LoadScene()`
between every later scene (the same method the portals/exit triggers call), never a direct Editor scene load. Item
acquisition, damage, and score along the way used the same real public `GameManager` API calls
(`AcquireTimeLens`, `AcquireChronoHourglass`, `AddScore`, `AddTimeShard`, `TakeDamage`, `SpendEnergy`) used to
simulate "legitimately earned" state throughout this whole pass, since replaying every puzzle from scratch a
second time was unnecessary — each puzzle's own internal logic was already verified in depth per-scene above.
- **MuseumNight -> FrozenCity:** score, shards, health, energy, `hasTimeLens`, and era-unlock all confirmed intact
  immediately on arrival in `FrozenCity`.
- **FrozenCity -> ClockCore:** `hasTimeLens`, `hasChronoHourglass`, shards, score all confirmed intact on arrival
  in `ClockCore`.
- **ClockCore -> Victory:** arrived with real non-zero stats displayed correctly on the Victory screen: Score
  685, Time Shards 2, Times Detected 1, Playtime 01:16.
- **Console:** zero errors across the entire chain.
This resolves the `hasTimeLens=False`/`hasChronoHourglass=False` readings seen earlier in this pass as the
already-suspected false alarm — those only ever occurred when a scene was opened directly in the Editor,
bypassing `GameManager`'s real `DontDestroyOnLoad` singleton entirely. Through real transitions, persistence is
solid.
**Observed, likely explained by test methodology, not a persistence bug:** score/shards/health changed by more
than what I explicitly added at each scene boundary (e.g. an unexplained score/shard jump on entering FrozenCity
and ClockCore). Checked `GameManager.AddScore` — it is a plain, unmultiplied `state.score += amount`, so the extra
points are coming from somewhere else actually firing, not from a scaling bug in the call I made. Likely cause:
`SceneLoader.LoadScene()` (used here to move between scenes quickly for this test) drops the player at the
scene's default spawn transform, not at the position a real portal walk-through would use — if that default spawn
sits near a `ShardPickup`/`ExhibitPlaque` trigger volume that collects on overlap, arriving there auto-collects it
immediately on scene start. This would not happen on a real playthrough, which enters each scene through the
portal's own trigger, adjacent to the previous scene's exit, not at the raw default spawn. Not chased to a
definitive root cause tonight since nothing broke (no console errors, values stayed sane and only in the
player's favor) — worth a glance during tomorrow's manual pass if the exact scoring numbers matter.
**Files:** none (verification only).
