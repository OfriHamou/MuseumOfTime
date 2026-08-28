# Museum of Time — Simplification Audit

**Branch:** `refactor/simplify-project` · **Purpose:** conservative cleanup/simplification pass, done without breaking
any current functionality or any graded requirement (see `docs/Implementation_Plan.md` Part 2 for the T1–T21/S1–S10/G1–G2/D1–D6
register and Part 8 for the requirement → script compliance matrix this audit was cross-checked against).

**Method.** Every `Assets/Editor/*.cs` builder was read and checked against `FullSceneRebuild.cs`'s actual call graph
(via grep for cross-references), not assumed unused from its name alone. Runtime scripts were checked for dead
classes/methods by grepping project-wide across **both code and scene/prefab YAML**, since a `MonoBehaviour` can be
referenced only from a `.unity`/`.prefab` file. Nothing below was classified SAFE TO REMOVE without a concrete
"nothing calls this, and here is the grep that proves it" check.

**Headline result:** this project is close to unusually clean for its size. Of 42 Editor builder scripts, **41 are
still load-bearing** — either invoked by `FullSceneRebuild.BuildAll` (the documented recovery orchestrator, which
must stay per the project owner's explicit instruction) or are the actual authorship source for a graded requirement
(the hand-built Animator controllers). What looked from naming like duplicate/superseded builders
(`MuseumBuilder`/`MuseumDressingBuilder`/`MuseumSceneSetup`, `CinematicLookBuilder`/`SurfaceAndVfxLookBuilder`/`ScenePolishBuilder`)
turned out on inspection to each own genuinely different, non-overlapping content, all still called in the documented
order. `HudBuilder`→`PremiumHudBuilder` and `MenuUIBuilder`→`PremiumMenuBuilder` are a deliberate "build the
functional objects, then re-skin them" two-step, not a superseded-by relationship.

---

## REMOVED (verified safe, executed)

| Item | Why | Verification performed |
|---|---|---|
| **`Assets/Scripts/Core/GameStateDebugTester.cs`** + its component on `Assets/Prefabs/Core/GameManager.prefab` | The class's own doc comment reads: *"Temporary testing component. Remove it before the final submission."* It had zero serialized fields, its methods are all `[ContextMenu]`-only (inert outside the Editor — never runs automatically), and it is not referenced by any test or requirement. | Read the full class body — confirmed no `Update`/`Awake`/event subscriptions, nothing but manually-invoked `[ContextMenu]` debug actions. Read the prefab YAML to confirm the component had no serialized state to lose. Removed the component from the live prefab via `manage_prefabs(modify_contents, components_to_remove)` (clean, GUID-safe removal — not hand-edited YAML), confirmed via `read_console` that this produced **zero** new errors/warnings and zero "missing script" references. Then deleted the `.cs`/`.meta` files and forced a full Unity recompile — confirmed clean (only pre-existing, unrelated `CS0618` obsolete-API warnings, no new errors, no reference to `GameStateDebugTester` anywhere in the console). |
| **`Assets/Editor/NoaMaterialFixBuilder.cs`** | Not called by `FullSceneRebuild` or any other builder (zero cross-references). One-time fix that extracted embedded Mixamo textures and remapped materials on `Idle.fbx`'s importer. | Positively verified — not just assumed — that its entire output is permanently baked into committed, git-tracked assets: `git ls-files` confirms `Ch02_body.mat`/`Ch02_hair.mat` and all 6 extracted textures are tracked; the `.fbx.meta`'s `externalObjects` remap block (the exact output of the script's `AddRemap` call) is committed and its two material GUIDs (`45773f4c…`, `e89d010f…`) match the committed `.mat` files' own GUIDs exactly; the `Ch02_body.mat` file has a real `_BaseMap` texture GUID wired in. A fresh clone gets Noa's fixed rendering without ever running this script. Deleted the `.cs`/`.meta` files; confirmed clean recompile afterward. |

Both removals were followed by a forced Unity recompile (`refresh_unity(compile: request, mode: force)`) and a
console check — zero new errors or warnings, zero dangling references. `Tools/verify.ps1`'s full compile + PlayMode
test run (see `docs/Final_Requirements_Audit.md` and `docs/Simplification_Report.md` for the result) is the final
confirmation that nothing regressed.

---

## SAFE TO SIMPLIFY

**None found.** Every Editor builder's content is either fully in active use (called by `FullSceneRebuild` or a
requirement's authorship source) or fully one-time-and-already-baked into the saved scenes — there is no file that
is *partially* dead code sitting inside an otherwise-needed script. The runtime dead-code scan (21 `Debug.Log` call
sites, all one-shot event logs, none in `Update`/`FixedUpdate`; no commented-out code blocks of any size; no
TODO/FIXME/HACK markers beyond ordinary design-decision comments) found nothing worth trimming without risk. The
26-file PlayMode test suite has no redundant coverage — every test pair that looks superficially similar on name
alone (`DeathFeedbackTests`/`DeathSubscriptionTests`, `ClockCoreSceneTests`/`ClockCoreWinnabilityTests`,
`WinnableByPlayingTests`/`FullPlaythroughTests`, `FrozenCityPuzzleReachTests`/`InteractionReachTests`) is documented
in its own header comment as covering a distinct, previously-real bug the other test doesn't catch.

---

## KEEP

### All Editor builders except the two candidates above

| Script | Reason to keep |
|---|---|
| AssetPrefabBuilder | Called by `FullSceneRebuild` step 2 — rebuilds Voronoi (T10)/LOD (T11) prefabs from Blender exports; its transform-copy fix is what makes T10/T11 render at real scale. |
| AudioAndVfxBuilder | Called by `FullSceneRebuild` — places the full Phase 7 audio/lighting/VFX pipeline in all 3 gameplay scenes. |
| BuildScript | Not part of scene rebuild — it's the actual player-build pipeline (S1/S2/S5, the EXE). Different purpose entirely. |
| BuildSizeBuilder | Called last in `FullSceneRebuild` — import-side texture caps for the 300 MB cap (S1), an explicit judging axis. |
| CameraRigParityBuilder | Called by `FullSceneRebuild` — gives FrozenCity/ClockCore the same two-camera rig MuseumNight has (T19); fixed a real defect. |
| CharacterLookBuilder | Called by `FullSceneRebuild` — gives Warden/Shadow skinned bodies so the hand-built Animators (T14) have something to drive. |
| CinematicLookBuilder | Called last among the look-dev builders by design (owns post-processing/ambient/fog exclusively — running earlier would let other builders overwrite it). Complementary to, not superseded by, ScenePolishBuilder/SurfaceAndVfxLookBuilder. |
| ClockCoreContentBuilder | Called by `FullSceneRebuild` — builds ClockCore's Player, both AI agents, 2 Time Anchors, the 3-phase Collector boss. Also calls `NavMeshExternalizer.SaveExternal` directly. |
| CollectibleLookBuilder | Called by `FullSceneRebuild` — fixed a real "can't tell what a pickup is" defect. |
| CoreSystemsBuilder | Called by `FullSceneRebuild` — wires Phase 3 systems into MuseumNight. |
| FrozenCityContentBuilder | Called by `FullSceneRebuild` — builds the 3-era gear puzzle, both AI agents, hidden anchors; also calls `NavMeshExternalizer.SaveExternal`. |
| **FullSceneRebuild** | **Explicitly protected** — the recovery orchestrator itself, per the project owner's hard rule. |
| HazardCollisionBuilder | Called by `FullSceneRebuild` after SceneGuidanceBuilder by design; supplies 2 of T4's 4 required collisions. |
| HingeSetBuilder | Called by `FullSceneRebuild` — builds the 3 hinge set-pieces for T5. |
| HudBuilder | Called by `FullSceneRebuild` — builds the functional HUD objects/logic bindings that `PremiumHudBuilder` later re-skins. Both halves needed. |
| InteractableObjectBuilder | Called by `FullSceneRebuild`, deliberately after CollectibleLookBuilder — gives exits/pickups real renderers. |
| MenuUIBuilder | Not itself called by `FullSceneRebuild`, but is a shared utility library (`Ensure<T>`, `CreateButton`, `EnsureEventSystem`, etc.) actively used by `HudBuilder`, `MinimapBuilder`, `TutorialTextBuilder` — **deleting it breaks compilation of those three.** Note: its `BuildFromCommandLine` base-menu-build step has fallen out of the automated recovery chain (a real gap — `PremiumMenuBuilder` reskins but doesn't call it, and nothing else does either), worth the project owner knowing about but out of scope for a conservative cleanup to fix uninvited. |
| MinimapBuilder | Called by `FullSceneRebuild` — builds the minimap camera/layer/marker (T18). |
| MinimapGeometryBuilder | Called by `FullSceneRebuild` after museum geometry exists — fixed T18 being an empty map. |
| ModelScaleFixBuilder | Called first in `FullSceneRebuild` by design — holds import settings `AssetPrefabBuilder` depends on. |
| MuseumBuilder | Called by `FullSceneRebuild` — base two-storey museum structure (T20). Complementary to MuseumSceneSetup/MuseumDressingBuilder, not superseded. |
| MuseumDressingBuilder | Called by `FullSceneRebuild`, before the navmesh bake by design — fixed the "open-topped box" defect. |
| MuseumSceneSetup | Called by `FullSceneRebuild` — builds MuseumNight's camera rig; distinct job from the other two Museum builders. |
| **NavMeshExternalizer** | **Explicitly protected** — this is the actual fix documented in `docs/Scene_Persistence_Fix.md` for the root cause of scenes going empty (embedded binary NavMesh data forcing whole-scene binary serialization). Called directly by `NavigationBuilder`, `FrozenCityContentBuilder`, `ClockCoreContentBuilder`. Has its own recovery menu item. |
| NavigationBuilder | Called by `FullSceneRebuild` — builds the two-agent-type, two-separate-bake navmesh setup for T13/T16. |
| NoaAnimatorBuilder | Not called by `FullSceneRebuild`, but is the actual construction script for `NoaController.controller` — the authorship mechanism for T14 ("an Animator you defined, not imported"). Needed if the controller asset is ever lost; directly relevant to defending T14 and to D5 (live add/remove). |
| NoaIntegrationBuilder | Called by `FullSceneRebuild` — wires the imported Mixamo model onto the player/Animator. |
| PlayerPrefabBuilder | Called by `FullSceneRebuild` step 2 — builds the reusable Player prefab used by FrozenCity/ClockCore. |
| PremiumHudBuilder | Called by `FullSceneRebuild`, explicitly after HudBuilder — re-skins what HudBuilder builds, keeps `HUDController`-bound object names intact so tests still pass. |
| PremiumMenuBuilder | Called by `FullSceneRebuild` — re-skins MainMenu/Victory. |
| SceneAudit | Standalone read-only diagnostic, not called by any builder, but a genuine recovery/diagnostic companion to the explicitly-protected FullSceneRebuild — same spirit, same protection. |
| SceneConnectionsBuilder | Called by `FullSceneRebuild` — adds the MuseumNight→FrozenCity exit trigger (S9). |
| SceneGuidanceBuilder | Called by `FullSceneRebuild` — fixed FrozenCity/ClockCore having zero T2/T3/T5 coverage. |
| ScenePolishBuilder | Called by `FullSceneRebuild` — decorative-only dressing pass, distinct concern from the other two look-pass builders. |
| SurfaceAndVfxLookBuilder | Called by `FullSceneRebuild` — fixes particle materials and adds normal maps; distinct from the other look-pass builders. |
| SurfaceDensityBuilder | Called by `FullSceneRebuild`, after all geometry exists — fixes texel density/tiling stretching. |
| TerrainBuilder | Called by `FullSceneRebuild` — sculpts FrozenCity's terrain (T6). |
| ThirdPersonCameraFixBuilder | Called by `FullSceneRebuild` — tunes the third-person camera framing across all 3 scenes. |
| TutorialTextBuilder | Called by `FullSceneRebuild` — builds MuseumNight's world-space tutorial plaques (T2). |
| WardenAnimatorBuilder | Same reasoning as NoaAnimatorBuilder — construction script for `WardenController.controller` (T14's enemy half). |
| WorldPropsPlacer | Called by `FullSceneRebuild` — places the fracture/LOD prefabs (repeated instances, since LOD needs repetition to matter). |

### Runtime scripts

- **`Assets/Scripts/Debug/BuildPlaytest.cs`** — looked like scaffolding from its folder name, but it's a real,
  actively-designed CLI playtest harness gated behind `#if DEVELOPMENT_BUILD || UNITY_EDITOR` and a `-playtest`
  launch flag, self-bootstrapping via `[RuntimeInitializeOnLoadMethod]` and driving genuine Input System input.
  Referenced by `BuildScript.cs`'s own doc comment as part of the intended dev-build workflow. Not dead, not
  temporary in the way `GameStateDebugTester` is.
- All other runtime classes checked as candidates (`FallGuard`, `WorldSignpost`, `ObjectiveWaypoint`,
  `CollectorHitVolume`, `MenuCameraDrift`, `HudMessageFeed`, `DeathOverlay`, `EraSwitchVfx`, `GameplayVfx`,
  `PickupBeacon`, `ProceduralAudioClips`, `EnemyNameplate`, `CollectorPhaseLabel`, `ControlsHintCard`) — all 14 are
  referenced in scene YAML and/or builder code. None dead.

### Tests

All 26 `Assets/Tests/PlayMode/*.cs` files kept — no redundant coverage found (see SAFE TO SIMPLIFY section for the
specific pairs checked and why each pair is non-overlapping). No test references a symbol that was never built
(`EraBoundObject`, `LeverInteractable`, `NPCDialogue` — named in the plan but never implemented — zero references
anywhere, confirming nothing depends on them).

### Assets

No orphaned/duplicate-named assets found (`_old`, `_backup`, `_v2`, `temp`, `test`, `unused`, `copy` — none matched
except `SurfaceDensityBuilder`'s deliberate `MaterialName_u<N>_v<N>.mat` tiling-variant naming scheme, which is a
systematic generated-asset convention, not versioning cruft).

---

## Live scene audit (Step 5) — completed via Unity MCP

All five scenes were loaded individually in the live Unity Editor and inspected (root hierarchy, deep child
searches, and console state after each load):

| Scene | Root count | Findings |
|---|---|---|
| MainMenu | 8 | Single `GameManager`, single `EventSystem`, single `UIManager`, single Canvas. No duplicates. |
| MuseumNight | 31 | Populated with `Hinges`, `LODObjects`, `Destructibles`, `MinimapCamera` roots as expected. NavMeshSurface count: **2** (correct). TimeAnchor count: **0** (correct — T21 forbids anchors in scene 1). No "test"/"old"/"debug"-named objects. |
| FrozenCity | 30 | NavMeshSurface count: **2** (correct). TimeAnchor count: **2** (correct). No suspicious names. |
| ClockCore | 26 | NavMeshSurface count: **2** (correct). TimeAnchor count: **2** (correct). Spot-checked `Collector`'s serialized references (`shieldVisual`, `summonedWarden`) — both populated, no missing refs. No suspicious names. |
| Victory | 7 | Single `GameManager`, single `EventSystem`, single `UIManager`, single Canvas. No duplicates. |

**Console:** every scene load produced only the known-benign `MCP-FOR-UNITY: WebSocket is not initialised` bridge
warning (an artifact of the MCP connection itself, not project code) — zero project errors or warnings in any scene.

**Conclusion: no scene-side cleanup needed.** No duplicate GameObjects, no disabled dev-only placeholders, no broken
references, no duplicate cameras/EventSystems/managers/lighting/VFX objects were found in any of the five scenes.
This matches the Editor-builder audit's overall picture: this project's iterative build process left behind almost
no debris — the two items removed above were the only two genuine leftovers found across the entire codebase and
scene set.

*Scope note: the very large decorative "dressing" subtrees (e.g. `SceneDressing`'s 26–67 children per scene,
`MenuDiorama`'s 27 children) were not walked node-by-node beyond the checks above, since they are generated,
non-gameplay decoration and a full manual walk would have been effort disproportionate to the risk for a
conservative pass. If desired, this could be a follow-up spot-check, not a blocker for this cleanup.*

---

## A pre-existing defect found and fixed (not a cleanup item)

Running `Tools/verify.ps1` to confirm this cleanup caused no regressions surfaced a real, pre-existing bug
unrelated to anything above: `Assets/Terrain/FrozenCityTerrainData.asset` was corrupted in git history (missing
from `.gitattributes`' binary-exception list, same root-cause class as the already-documented NavMeshData bug).
This was repaired — GUID preserved, no scene edits needed — and `.gitattributes` was updated to prevent recurrence.
Full detail in `docs/Simplification_Report.md`; this is called out there rather than duplicated here since it isn't
a dead-code/duplication finding, just a fix that happened to be necessary to reach a clean test run.
