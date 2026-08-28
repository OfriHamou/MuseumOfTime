# Museum of Time — Final Requirements Audit

**Purpose:** a defense cheat sheet. For every mandatory requirement (T1–T21, S1–S10, G1–G2, D1–D6): which scene(s) it
lives in, which script/GameObject implements it, how it works, how to demonstrate it live, and its verified status.

**Method.** Requirement → script mapping starts from `docs/Implementation_Plan.md` Part 8's compliance matrix, but
every row below was **re-verified against the live project** during this pass (post-cleanup, on
`refactor/simplify-project`), not copied blind:
- Live Unity Editor inspection via MCP — actual scene hierarchies, component lists, NavMeshSurface/TimeAnchor
  counts, build settings — see `docs/Simplification_Audit.md`'s live-audit section for the raw findings.
- `Tools/verify.ps1` — a full headless recompile + the complete `Assets/Tests/PlayMode/` suite (which includes
  `RequirementComplianceTests.cs`, one test per T1–T21/S9/S10, and `FullPlaythroughTests.cs`, a MainMenu→Victory
  run exercising the real trigger/boss/persistence logic) — run **after** this cleanup's two removals, so a PASS
  here reflects the current tree, not history.

Where this audit found a requirement's evidence unchanged from before the cleanup (the overwhelming majority — this
was a conservative pass that removed exactly one dead script and one dead runtime component), that's stated plainly
rather than re-deriving paragraphs that were already correct.

---

## T1 — Entry and victory menus

- **Scene(s):** MainMenu, Victory
- **Implementation:** `MainMenuController.cs`, `VictoryScreenController.cs`; UI built by `MenuUIBuilder`/`PremiumMenuBuilder`
- **GameObject:** `UIManager` (MainMenu/Victory), `MainMenuCanvas`/`VictoryCanvas`
- **How it works:** New Game / Continue (gated on `SaveService.Exists`) / Controls / Quit on MainMenu; Victory reads final score/shards/detections/playtime live from `GameState`.
- **Verified live:** MainMenu hierarchy confirmed: single `UIManager` (`SceneLoader` + `MainMenuController`), single `MainMenuCanvas` (9 children), single `EventSystem`. Victory: single `UIManager` (`VictoryScreenController`), single `VictoryCanvas` (11 children), single `EventSystem`. No duplicates.
- **Demonstrate:** Launch the game, show New Game/Continue, complete a run, show Victory's stat readout.
- **Status:** ✅ PASS

## T2 — In-game 3D tutorial, dynamic text

- **Scene(s):** All three
- **Implementation:** `WorldTutorialText.cs`, `WorldObjectiveText.cs` (world-space `TextMeshPro`, not Canvas UI); built by `TutorialTextBuilder`, `SceneGuidanceBuilder`
- **GameObject:** `TutorialPlaques` root in each gameplay scene
- **How it works:** Exhibit plaques teach each verb; text is dynamic (reads live energy/health/progress via token substitution) and fades in/out on proximity.
- **Verified live:** `TutorialPlaques` root present in MuseumNight (9 children), FrozenCity (5 children), ClockCore (4 children).
- **Demonstrate:** Walk up to a plaque in any scene, show the text is in 3D space (rotate Scene view) and changes with state (e.g. energy percentage).
- **Status:** ✅ PASS

## T3 — At least 4 triggers

- **Scene(s):** All three
- **Implementation:** `RoomEntryTrigger`, `TutorialTrigger`, `EraZoneTrigger`, `HazardTrigger`, `TimeAnchorTrigger`, `SceneExitTrigger` — six distinct `OnTriggerEnter` components
- **GameObject:** `Triggers` root in each scene (MuseumNight 12 children, FrozenCity 7, ClockCore 4)
- **How it works:** Each trigger type does distinct real work (dialogue/camera/objective on room entry, tutorial reveal, era-zone gating, health/energy drain in a hazard, silent anchor arming, scene exit gating on an acquired item).
- **Demonstrate:** Walk into a hazard zone (visible health drain), walk past a hidden Time Anchor location (silent arm), enter/exit the scene-exit volume.
- **Status:** ✅ PASS

## T4 — At least 3 collisions detected and acted upon

- **Scene(s):** All three
- **Implementation:** `ChronoOrb.cs` (orb→object), `FallingDebris.cs`, `SwingingHazard.cs`, `Collector.cs` (orb→boss shield) — four real `OnCollisionEnter` handlers reading `relativeVelocity`/`contacts[0].point`
- **How it works:** Impact force/velocity scales the response (e.g. shatter threshold, damage amount); not `OnTriggerEnter`.
- **Demonstrate:** Throw the orb at a display case or the bell; stand under falling debris; hit the Collector's shield in ClockCore.
- **Status:** ✅ PASS

## T5 — Hinge joints

- **Scene(s):** All three
- **Implementation:** `HingeSetBuilder` output — Clock of Creation pendulum + gallery gate (MuseumNight), clock-tower bell (FrozenCity), swinging exhibits (ClockCore)
- **GameObject:** `Hinges` root — confirmed present in MuseumNight (3 children) and ClockCore (2 children); FrozenCity's bell lives under `ClockTower` (2 children) rather than a separate `Hinges` root — same mechanism, different parenting, not a gap.
- **How it works:** Real `HingeJoint` components with `useLimits` set so nothing spins freely; the tower bell also uses `useMotor`/`useSpring` as documented.
- **Demonstrate:** Hit the bell with the orb and show it swings and rings; show the pendulum's resting swing.
- **Status:** ✅ PASS

## T6 — Self-built Terrain

- **Scene(s):** FrozenCity
- **Implementation:** `TerrainBuilder.cs`
- **GameObject:** `FrozenCityTerrain` (confirmed present live, `Terrain` + `TerrainCollider` components, `isStatic: true`)
- **How it works:** Hand-sculpted heightmap with 3 painted texture layers (cobblestone, frozen dirt, snow), traversable by both AI agent types.
- **Demonstrate:** Open the Terrain inspector and show the sculpt/paint layers; walk from spawn to the clock tower entirely on terrain.
- **Status:** ✅ PASS

## T7 — Patrol with pause

- **Scene(s):** All three
- **Implementation:** `PatrolRoute.cs` (per-waypoint `waitSeconds`), `WardenAI.cs` (`agent.isStopped = true` during pause, head-sweep rotation)
- **How it works:** The Warden fully stops (not just slows) and visibly scans for 2–4s at each waypoint before continuing.
- **Demonstrate:** Watch a Warden patrol; point out the full stop and head sweep, not just a speed change.
- **Status:** ✅ PASS

## T8 — Score / health / energy

- **Scene(s):** All three, shown on HUD
- **Implementation:** `GameManager.cs` + `GameState.cs`; `HUDController.cs`
- **How it works:** Energy drains on time powers, regenerates when idle; score rises on shards/puzzles, falls on capture — both gain and loss are real, per the requirement's explicit wording.
- **Demonstrate:** Collect a shard (score up), get captured or stand in a hazard (health/score down), use slow-time (energy drains and auto-cancels at zero).
- **Status:** ✅ PASS

## T9 — Cross-scene parameters incl. ≥2 acquired items (serialized)

- **Scene(s):** All
- **Implementation:** `GameState.cs` (`[System.Serializable]`, `ToJson()`/`LoadFromJson()` via `JsonUtility`), `SaveService.cs`
- **Items:** Time Lens (granted end of MuseumNight, required in FrozenCity), Chrono Hourglass (granted mid-FrozenCity, required in ClockCore)
- **How it works:** Saved to `Application.persistentDataPath` on scene transition and at Time Anchors; both item flags gate real content, not just cosmetic flags.
- **Demonstrate:** Show the JSON save file on disk; collect the Lens, change scene, show the flag/behavior persisted (FrozenCity is unfinishable without it).
- **Status:** ✅ PASS

## T10 — Two Voronoi-fractured assets, appearing intrinsically

- **Scene(s):** MuseumNight (Clock of Creation, #1), FrozenCity (frozen statue, #2), reused in ClockCore
- **Implementation:** Blender Cell Fracture (`Tools/voronoi_fracture.py`) → `AssetPrefabBuilder` rebuilds prefabs from the FBX exports → `FracturedObject.cs` at runtime
- **GameObject:** `Destructibles` root — confirmed present in MuseumNight and FrozenCity
- **How it works:** Intact mesh swaps to pre-baked shard hierarchy on break, explosion force applied, shards despawn after `shardLifetime`.
- **Demonstrate:** Trigger the Clock of Creation shatter in MuseumNight's opening beat; shatter the frozen statue in FrozenCity.
- **Status:** ✅ PASS

## T11 — Two different hand-decimated LOD assets

- **Scene(s):** MuseumNight (marble statue, stone column), reused elsewhere
- **Implementation:** Blender Decimate (`Tools/lod_generate.py`) → `AssetPrefabBuilder`; real `LODGroup` per prefab
- **GameObject:** `LODObjects` root — confirmed present in MuseumNight (9 children)
- **How it works:** 3-tier LODGroup (100%/~50%/~20% triangle counts), placed as multiple instances so the effect is visible.
- **Demonstrate:** Move the Scene camera away from an instance and show the Game view stats' triangle count step down.
- **Status:** ✅ PASS

## T12 — Only the New Input System

- **Scene(s):** All
- **Implementation:** `MuseumInputActions.inputactions`, `PlayerInputReader.cs` (the single point of contact with the Input System)
- **How it works:** No `Input.GetKey`/`GetAxis`/`mousePosition` anywhere; Active Input Handling is New-only.
- **Demonstrate:** Show `PlayerInputReader` is the only class touching `UnityEngine.InputSystem` directly; Project Settings → Active Input Handling.
- **Status:** ✅ PASS (unchanged by this cleanup — no input code was touched)

## T13 — Steering (seek/flee/pursue) and/or pathfinding, 2 agent types on separate bakes

- **Scene(s):** All three (FrozenCity/ClockCore are where both agents actually appear together)
- **Implementation:** `WardenAI.cs` (agent type A), `ShadowAI.cs` (agent type B), `SteeringBehaviours.cs` (`Seek`/`Flee`/`Pursue`, named explicitly), `NavigationBuilder.cs`
- **How it works:** Two separate `NavMeshSurface` components, two separate bakes, a custom `ShadowOnly` NavMesh area so the two agent types genuinely travel different routes.
- **Verified live:** NavMeshSurface count confirmed **exactly 2** in MuseumNight, FrozenCity, and ClockCore (no accidental duplicate bakes).
- **Demonstrate:** Toggle each NavMeshSurface's gizmo to show two different navmeshes; spawn one of each agent type toward the same destination and watch the different paths.
- **Status:** ✅ PASS

## T14 — Hand-authored Animator, ≥4 states

- **Scene(s):** All (player), enemy scenes for Warden
- **Implementation:** `NoaController.controller` (built by `NoaAnimatorBuilder.cs`: Idle/Walk/Run/Jump/Interact), `WardenController.controller` (built by `WardenAnimatorBuilder.cs`: Patrol/Alert/Chase/Attack/Frozen)
- **How it works:** Both controllers are constructed programmatically from scratch (states/parameters/transitions), not imported — the authorship scripts (`NoaAnimatorBuilder`/`WardenAnimatorBuilder`) are the actual proof of "not imported" and were explicitly kept in this cleanup for exactly that reason.
- **Verified live:** Player GameObject in all three gameplay scenes carries an `Animator` + `PlayerAnimatorDriver`.
- **Demonstrate:** Open both Animator windows and narrate every state/transition/condition live — this is D5-relevant (live add/remove).
- **Status:** ✅ PASS

## T15 — Physical projectile, shooting and impact

- **Scene(s):** All three
- **Implementation:** `ChronoOrb.cs` (Rigidbody, SphereCollider, trail), `ChronoOrbLauncher.cs`
- **How it works:** Spawned at a muzzle point, `AddForce(ForceMode.Impulse)`; on impact freezes/rewinds rather than destroys (keeps Noa a non-combatant per the GDD).
- **Verified live:** `ChronoOrbLauncher`/`ChronoHourglass` confirmed present on the Player in all three gameplay scenes.
- **Demonstrate:** Throw the orb — arcs under gravity, bounces, rings the bell, shatters a fractured object, freezes a Warden.
- **Status:** ✅ PASS

## T16 — Recast navmesh with stealth

- **Scene(s):** All three, core loop in FrozenCity
- **Implementation:** The two baked `NavMeshSurface`s (T13) + `WardenAI`'s detection meter, cone, and hide-volume mechanic
- **How it works:** Range → cone angle → line-of-sight raycast, in that order; crouch/hide volumes reduce detection.
- **Demonstrate:** Stand behind a `HideVolume` object and show no detection regardless of range; step out and show the meter fill.
- **Status:** ✅ PASS

## T17 — LayerMask built in code

- **Scene(s):** All
- **Implementation:** `WardenAI.Awake()` and `PlayerInteractor.Awake()` — `LayerMask.GetMask(...)`, per `CLAUDE.md`'s own documented pattern
- **How it works:** Vision-blocker mask and interaction mask are constructed in code, not only assigned in the Inspector — the exact literal wording of T17.
- **Demonstrate:** Open `WardenAI.cs`, show the `GetMask` call and the comment explaining the misspelled-layer-name pitfall.
- **Status:** ✅ PASS (unchanged by this cleanup)

## T18 — Minimap, live for a whole scene

- **Scene(s):** MuseumNight (full coverage), optional elsewhere
- **Implementation:** `MinimapController.cs`, `MinimapBuilder.cs`, `MinimapGeometryBuilder.cs`
- **GameObject:** `MinimapCamera` — confirmed present in MuseumNight; **not** present as a root in FrozenCity/ClockCore, matching the plan's explicit choice to guarantee full coverage in exactly one scene rather than partial coverage in three.
- **How it works:** Orthographic camera on an allow-listed `Minimap` layer, rendering real museum geometry (objectives gold, exit green); hidden Time Anchors are excluded by construction (layer allow-list), not by remembering to exclude them.
- **Demonstrate:** Play through MuseumNight start to finish showing the minimap never drops out.
- **Status:** ✅ PASS

## T19 — First/third-person camera switch, 2 cameras besides minimap

- **Scene(s):** All three
- **Implementation:** `PlayerCameraRig.cs`, two `CinemachineCamera`s (`CM_ThirdPerson`/`CM_FirstPerson`) toggled by priority
- **Verified live:** `MainCamera` (CinemachineBrain) + `ThirdPersonCamera` (CinemachineCamera) confirmed present as scene roots in MuseumNight, FrozenCity, and ClockCore — a real defect from before this project's Phase 9 pass (FrozenCity/ClockCore originally had only one camera each) that stayed fixed.
- **How it works:** `CinemachineBrain.DefaultBlend` is an instant cut (0s), not the Cinemachine default 2s ease — fixed after a real bug where `Camera.main`'s position lagged the toggle by up to 2 seconds (see `docs/Scene_Persistence_Fix.md` item 7).
- **Demonstrate:** Press the camera toggle in any scene and show the instant cut; note the minimap camera is a third, independent camera.
- **Status:** ✅ PASS

## T20 — Two-storey building, own textures, stairs

- **Scene(s):** MuseumNight
- **Implementation:** `MuseumBuilder.cs` (structure), `MuseumDressingBuilder.cs` (interior dressing), `MuseumSceneSetup.cs` (camera rig)
- **GameObject:** `Museum` root (11 children), `MuseumStructure`, `MuseumDressing`
- **How it works:** Ground floor (entrance/galleries/Clock of Creation chamber) + upper mezzanine/office, connected by a real walkable staircase with a step offset tuned to clear the rise; personally-chosen materials (marble/wood/plaster/brass) with normal maps.
- **Demonstrate:** Walk the staircase; open the Materials inspector and describe each texture's origin.
- **Status:** ✅ PASS

## T21 — ≥2 hidden teleports from scene 2 onward, failure returns to anchor with refreshed state

- **Scene(s):** FrozenCity, ClockCore only (explicitly **not** MuseumNight)
- **Implementation:** `TimeAnchor.cs`, `TimeAnchorTrigger.cs`, `RespawnService.cs`; state in `GameState.hasCheckpoint`/`checkpointSceneName`/`checkpointPosition`/`checkpointEra`
- **Verified live:** TimeAnchor count confirmed **exactly 0** in MuseumNight, **exactly 2** in FrozenCity, **exactly 2** in ClockCore — the two placement rules the plan calls out as "easy to get wrong" both hold exactly.
- **How it works:** Anchors are invisible without the Time Lens, arm silently; on failure the player returns to the last armed anchor (not scene start) with health restored, energy partially restored, and a score penalty (satisfying T8's "loss" clause too).
- **Demonstrate:** Get captured/killed in FrozenCity or ClockCore and show respawn at the last anchor with correct era, not at scene start.
- **Status:** ✅ PASS

---

## S9 — Coherent logical connection across all scenes

- **Implementation:** `SceneConnectionsBuilder.cs`, `SceneExitTrigger.cs`; the Lens→FrozenCity→Hourglass→ClockCore→Victory chain
- **Verified live:** Build Settings confirmed exact order MainMenu(0)→MuseumNight(1)→FrozenCity(2)→ClockCore(3)→Victory(4), all enabled.
- **Status:** ✅ PASS (`FullPlaythroughTests` exercises the full chain with real trigger/boss logic, not just flag presence)

## S10 — Scale and realism

- **Implementation:** 1 unit = 1 metre convention; model import scale fixed (see `docs/Defect_And_Look_Pass.md` — the "4m column shipped as a 4cm pebble" defect, now fixed)
- **Status:** ✅ PASS

## S1 — ≤300 MB compressed build

- Last recorded build size (`docs/Implementation_Overview.md` Phase 9/11): **56–57 MB compressed**, well under budget. This cleanup only *removed* two files and one prefab component — it cannot have increased build size.
- **Status:** ✅ PASS (no rebuild performed as part of this cleanup; the 300 MB check should be re-run at actual submission-build time, but nothing in this pass works against it)

## S2, S5 — Packaging/EXE upload verified to work

- **Status:** ⚠️ NEEDS ATTENTION — this is a submission-time action (build, zip, extract-to-clean-folder, run), not something a code cleanup pass performs or can verify. Not regressed by this pass; simply not yet re-executed since the cleanup.

## S3, S4, S6, S7, S8 — GDD format, trailer, repo link, known bugs, per-scene requirement listing

- **Status:** ⚠️ NEEDS ATTENTION (administrative/submission-content items, outside a code cleanup's scope — see `docs/Implementation_Plan.md` Phase 8 for the full checklist). Not evaluated by this audit.

## G1, G2 — How interesting the game is; trailer quality

- Qualitative/judged criteria. The era-switching mechanic, coherence chain, and boss fight are all intact and unmodified by this cleanup.
- **Status:** Unaffected by this cleanup (no gameplay, art, or narrative content was touched)

## D1–D6 — Defense rules

- D2 (runs on your machine), D3 (source-code defense), D5 (live add/remove) are all **helped** by this cleanup: fewer
  files to search through, one less dead script to explain away, and the removed `GameStateDebugTester` no longer
  needs an awkward "yes I know this is supposed to be gone" explanation if a grader opens the `GameManager` prefab.
- **Status:** Unaffected structurally; D2/D5 depend on rehearsal, not code.

---

## Test-suite verification

`Tools/verify.ps1` was run after both removals (compile + full `Assets/Tests/PlayMode/` suite). See
`docs/Simplification_Report.md` for the final pass/fail counts from this run — that section is the authoritative,
current-tree confirmation for every ✅ PASS row above (`RequirementComplianceTests.cs` asserts one test per
T1–T21/S9/S10 against real runtime properties, not just presence).
