# Museum of Time — Simplification Report

**Branch:** `refactor/simplify-project` · **Nothing committed or pushed** — all changes are in the working tree for
manual review.

## Summary

This was a conservative cleanup pass. The headline finding, stated plainly: **there was almost nothing to remove.**
This project's iterative, headless-builder-driven construction process left behind exactly two genuine dead-code
leftovers across 42 Editor tools, ~70 runtime scripts, 26 test files, and every asset folder — everything else
audited turned out to be load-bearing, either for gameplay, for one of the 21 graded technical requirements, or as
documented recovery infrastructure. Along the way, the pass also surfaced and fixed one real, pre-existing defect
(a corrupted Terrain asset) that was unrelated to the cleanup but was blocking the test-suite verification gate.

## What was removed

| Item | Why safe | How verified |
|---|---|---|
| `Assets/Scripts/Core/GameStateDebugTester.cs` + its component on `Assets/Prefabs/Core/GameManager.prefab` | Self-documented as *"Temporary testing component. Remove it before the final submission."* Zero serialized fields; all methods `[ContextMenu]`-only (inert outside the Editor). | Read the full class; removed the prefab component via `manage_prefabs(modify_contents)` (clean, GUID-safe); forced a full recompile — zero new errors/warnings, zero dangling references. |
| `Assets/Editor/NoaMaterialFixBuilder.cs` | One-time Mixamo texture-extraction/material-remap fix. Not called by `FullSceneRebuild` or anything else. | Positively confirmed its entire output is permanently committed: the `.fbx.meta`'s material remap GUIDs match the committed `.mat` files' own GUIDs exactly, and the `.mat` has a real `_BaseMap` texture wired in. A fresh clone gets the fix without ever running this script. |

Both removals were followed by a forced Unity recompile and console check (zero new issues), and by the full
`Tools/verify.ps1` suite (see Test results below).

## What was simplified

Nothing. No file was found to be partially dead code inside an otherwise-needed script — every Editor builder's
content is either fully in active use or fully one-time-and-already-baked into the saved scenes; the 21-call-site
`Debug.Log` audit found no console-spam candidates; no commented-out code blocks of meaningful size existed anywhere
in `Assets/Scripts/`.

## Duplicate/obsolete systems considered and kept

Several Editor builder names suggested possible supersession chains on first read. Each was checked individually
against `FullSceneRebuild.cs`'s actual call graph and, in every case, turned out to build genuinely different,
non-overlapping content — not a duplicate:

- `MuseumBuilder` (base structure) / `MuseumDressingBuilder` (interior dressing) / `MuseumSceneSetup` (camera rig) — three different jobs, all still called.
- `CinematicLookBuilder` (post-processing/ambient/fog, deliberately run last) / `SurfaceAndVfxLookBuilder` (particle/normal-map materials) / `ScenePolishBuilder` (decorative-only dressing) — three different jobs, all still called.
- `HudBuilder` (functional HUD objects) → `PremiumHudBuilder` (re-skins them, deletes flat originals) — a deliberate two-step "build then reskin," both halves needed.
- `MenuUIBuilder` (shared UI utility library, actively used by three other builders) → `PremiumMenuBuilder` (re-skins MainMenu/Victory) — same pattern; `MenuUIBuilder` cannot be removed without breaking compilation of `HudBuilder`/`MinimapBuilder`/`TutorialTextBuilder`.

Full reasoning for all 42 Editor scripts is in `docs/Simplification_Audit.md`.

## Editor/builders explicitly kept and why

- **`FullSceneRebuild.cs`** and **`NavMeshExternalizer.cs`** — explicitly protected per your instruction; both are
  real, still-load-bearing recovery infrastructure (the latter is the actual fix for the scene-serialization defect
  documented in `docs/Scene_Persistence_Fix.md`, called directly by three content builders).
- The other 38 kept Editor scripts are each either called by `FullSceneRebuild.BuildAll`'s documented pipeline, or —
  for `NoaAnimatorBuilder.cs`/`WardenAnimatorBuilder.cs` specifically — are the actual authorship source for the
  hand-built Animator controllers required by T14 ("not imported"), so they matter for the defense even though
  nothing calls them automatically.
- `SceneAudit.cs` — a standalone read-only diagnostic, kept as a companion to `FullSceneRebuild` in the same
  protected spirit.

## Suspicious items intentionally kept

None remain — the two items originally flagged with caveats (`GameStateDebugTester`, `NoaMaterialFixBuilder`) were
both positively verified and removed rather than left as permanent uncertain-keeps.

## Scene cleanup performed

**None needed.** All five scenes (MainMenu, MuseumNight, FrozenCity, ClockCore, Victory) were individually loaded
and inspected live via Unity MCP — root hierarchies, deep child searches for suspicious names, NavMeshSurface counts
(exactly 2 per gameplay scene, correct), TimeAnchor counts (0 in MuseumNight, 2 each in FrozenCity/ClockCore,
matching T21 exactly), and console state after each load. No duplicate GameObjects, disabled dev-only placeholders,
duplicate cameras/EventSystems/managers, or broken references were found in any scene. Full detail in
`docs/Simplification_Audit.md`'s live-audit section.

## A real defect found and fixed (outside the original cleanup scope)

While running `Tools/verify.ps1` to confirm the cleanup caused no regressions, the suite came back with **46-48
failing tests** — a serious signal that needed investigation before anything could be called done. Root cause,
confirmed step by step:

1. Every failure traced to one repeatable engine error: `Unknown error occurred while loading
   'Assets/Terrain/FrozenCityTerrainData.asset'`, firing on every FrozenCity scene load/unload.
2. `git diff` confirmed this asset was **byte-identical to the committed HEAD** — nothing in the cleanup touched it.
3. A forced reimport failed at Unity's native format parser, meaning the *committed source bytes themselves* were
   unparseable — not just a stale Library cache.
4. `.gitattributes` showed the actual cause: `Assets/NavMesh/*.asset` and `LightingData.asset` both have a `binary`
   exception (added specifically because baked binary Unity assets get silently corrupted by git's text/line-ending
   normalization — see `docs/Scene_Persistence_Fix.md`), but `FrozenCityTerrainData.asset` — which is *also*
   genuinely binary (it embeds heightmap/alphamap arrays) — was **missed** and left under the generic `*.asset
   unity-yaml` (text) rule. Same bug class, one file never covered by the fix.

**This predates the cleanup entirely** and was not caused by removing `GameStateDebugTester` or
`NoaMaterialFixBuilder` — those two changes have no relationship to Terrain code, and the Terrain asset's own git
history shows it was never touched.

**Fix applied**, scoped as narrowly as possible:
- Did **not** run the full `TerrainBuilder.BuildFromCommandLine` recovery tool, because it also destroys and
  rebuilds the `ClockTower` GameObject with a crude two-cube placeholder (per its own code comments) — that would
  have wiped out the real, polished, hinge-bell-equipped tower already living in the saved scene, a much bigger
  change than the actual problem warranted.
- Instead, wrote a temporary, narrowly-scoped repair script that regenerated **only** the corrupted `TerrainData`
  payload (same sculpt algorithm as `TerrainBuilder.cs`, reusing the three existing, unaffected `.terrainlayer`
  assets rather than recreating them), preserving the asset's **exact original GUID** by deleting only the raw file
  and keeping its `.meta` — so the FrozenCity scene's existing Terrain/TerrainCollider references needed **zero**
  edits. Confirmed via the regenerated relief value (0.849, correctly non-flat) and the diff showing the asset as
  modified-in-place, not recreated. The temporary script was deleted immediately after use.
- Added the missing `Assets/Terrain/FrozenCityTerrainData.asset binary` rule to `.gitattributes` (mirroring the
  existing NavMesh/LightingData precedent) so this cannot recur on a future checkout or merge.

This was surfaced to you mid-task and approved before executing, since it went beyond the original "remove dead
code" scope, touching real (if corrupted) game content.

## Test results

| Run | Result |
|---|---|
| Before this pass's two removals were touched (baseline, per `docs/Scene_Persistence_Fix.md`) | 146/152 passed, 0 failed, 6 intentionally ignored |
| After removing `GameStateDebugTester` + `NoaMaterialFixBuilder`, before the Terrain fix | **46-48 failed** (reproduced twice, both times the identical Terrain-load defect — unrelated to the removals, as established above) |
| **Final, after the Terrain repair** | **146/152 passed, 0 failed, 6 intentionally ignored — `RESULT: PASS`** |

The final run exactly matches the project's own recorded historical baseline — the cleanup introduced zero
regressions, and the pre-existing Terrain defect is now also fixed.

## Requirement audit result

Every mandatory requirement (T1–T21, S9, S10) is ✅ PASS per `docs/Final_Requirements_Audit.md`, cross-checked
against both live Unity MCP inspection and the passing test suite above. Submission-process items (S1–S8 packaging,
G2 trailer, D2 defense-machine check) are marked NEEDS ATTENTION only because they are administrative/manual steps
outside a code cleanup's scope, not because anything regressed — see that document for detail.

## Remaining concerns

- **None from this cleanup.** The diff is small and fully explained: two deletions, one prefab component removal,
  one binary-asset repair, one `.gitattributes` addition, plus these three new docs.
- **Not addressed, out of scope:** the pre-existing `CS0618` obsolete-API warnings (`FindObjectsSortMode`,
  `GetInstanceID`) seen in the compile step across ~10 files — these are Unity 6 API deprecation notices, not
  errors, unrelated to this cleanup's brief, and fixing them would touch working code purely for style/currency
  reasons against the "don't rewrite working code" instruction. Flagging for awareness only.
- **Worth a human look before submission:** the same class of git text-normalization bug that hit
  `FrozenCityTerrainData.asset` could in principle affect other native-binary `.asset` files if any exist elsewhere
  in the project under the generic `*.asset unity-yaml` rule — this pass only found and fixed the one that was
  actively failing tests (Terrain). No others surfaced as broken during the live scene audit or the test run, but a
  targeted look at any other Terrain/large-binary `.asset` files during defense prep would be reasonable due
  diligence, not urgent.
- **300 MB build cap:** unaffected by this pass (only removed 2 small files and fixed one binary asset in place);
  no rebuild was performed to re-verify the actual number, per your instruction not to force unnecessary work.

## Final verification checklist

1. ✅ All five scenes open normally (build settings confirmed correct order; each scene individually loaded and inspected live via MCP).
2. ✅ MainMenu → MuseumNight → FrozenCity → ClockCore → Victory chain intact (`S9`, `FullPlaythroughTests`).
3. ✅ All three gameplay scenes remain persistent, populated YAML scenes (no rebuild was performed on any scene).
4. ✅ External NavMesh assets/references remain valid (NavMeshSurface count confirmed exactly 2 per gameplay scene; `NavMeshExternalizer.cs` untouched).
5. ✅ Unity Console has no important errors (only pre-existing `CS0618` obsolete-API warnings, unrelated to this pass).
6. ✅ `Tools/verify.ps1` → **0 failed** (146/152 passed, 6 intentionally ignored).
7. ✅ `docs/Final_Requirements_Audit.md` — every mandatory T/S9/S10 requirement is PASS.
8. ✅ 300 MB cap: not put at risk by this pass (net effect is a size *decrease* — two files removed, nothing added except doc/text files).
