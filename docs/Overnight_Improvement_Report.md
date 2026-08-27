# Overnight Improvement Report

**Scope:** autonomous overnight pass across the whole game (bug fixes first, then visual quality), using
`docs/` as the requirement source of truth and `docs/Museum_of_Time_GDD.pptx` only as mood/theme inspiration.
Everything below was built through the existing headless Editor builders — no gameplay system was rewritten.

## How this was verified

Recon used a temporary PlayMode test that rendered each scene's `Main Camera` to a PNG (deleted afterward, not
part of the permanent suite) so real bugs could be found and re-checked visually rather than guessed at. Every
code change was re-verified with `Tools/verify.ps1`.

**Final verification result: PASS — 84/90 tests passed, 0 failed** (the same 6 tests intentionally ignored since
Phase 0 remain ignored). See "Files changed" below for the full diff surface.

---

## Priority 1 — Bugs found and fixed

### 1. Player spawn placed the third-person camera behind the entrance wall (MuseumNight)
**Symptom:** recon screenshot showed a solid grey wall filling most of the frame instead of the museum interior.
**Cause:** the rebuilt scene's Player spawn (z = −9) left only ~1 m of clearance from the entrance wall (z = −10)
— not enough for the third-person camera's 2.6 m pull-back, so the camera ended up positioned behind/inside the
wall.
**Fix:** moved the spawn to z = −6 (still just inside the entrance, still ahead of the "Move" tutorial trigger at
z = −7).

### 2. Every particle effect in the game rendered solid magenta/pink
**Symptom:** the era-switch shockwave, shard sparkle, fracture dust, orb trail and Shadow drift all showed as
flat magenta blobs/rectangles instead of their intended colours — visible in every scene from the moment it
loads (the era-switch burst fires once automatically at scene start so era-bound objects initialise correctly,
which is also why it was visible without the player doing anything).
**Cause:** none of the five particle systems ever had a material assigned to their `ParticleSystemRenderer`.
Unity's default fallback particle material uses a Built-in-Render-Pipeline shader, which URP renders as solid
magenta — the universal "broken material" tell.
**Fix:** added a shared `GetParticleMaterial()` helper (in both `AudioAndVfxBuilder.cs` for the edit-time
particles and `GameplayVfx.cs` for the two created at runtime) that assigns URP's own
`Universal Render Pipeline/Particles/Unlit` shader. All five effects now show their configured colour.

### 3. `RespawnService.sceneStart` was never wired in any scene
**Symptom:** dying before arming a Time Anchor would respawn the player at `Vector3.up` (world origin, roughly
(0, 1, 0)) — not necessarily inside the level, and **always** the case in MuseumNight, which can never have a
Time Anchor (T21 forbids one there).
**Cause:** `CoreSystemsBuilder`/`FrozenCityContentBuilder`/`ClockCoreContentBuilder` all call
`Ensure<RespawnService>()` but none of them ever assigned its `sceneStart` field.
**Fix:** wired `sceneStart` to a dedicated `SceneStart` marker at the player's spawn point (MuseumNight) or to
the existing `PlayerSpawn` marker (FrozenCity/ClockCore) in each scene's manager-building code.

### 4. Tutorial plaque text was roughly 2× too large for its own box, badly clipped
**Symptom:** "Hold W to walk toward where you are looking." rendered gigantic, overlapping the 3D scene and
partly running off the visible plaque area.
**Cause:** `TutorialTextBuilder.SetupTextMesh` set a fixed `fontSize = 2.5` inside a 1.2 m-tall world-space box —
roughly double the box height per line.
**Fix:** switched to TextMeshPro auto-sizing (`fontSizeMin 0.3` / `fontSizeMax 0.9`), so any message length stays
readable inside the box without another magic constant to get wrong. Applies to all 8 verb plaques and the
persistent objective line (they share the same setup method).

### 5. A stray null-material lookup could silently render Phase 4 geometry magenta
**Symptom:** a large solid magenta rectangle appeared near MuseumNight's entrance in recon.
**Cause:** `NavigationBuilder.BuildObstacleCourse()` (the narrow-gap/ledge geometry that makes the Warden's and
Shadow's navmesh routes genuinely differ, per T13/T16) loads `MuseumPlaster.mat` via
`AssetDatabase.LoadAssetAtPath` and silently skips the material assignment if that returns null — which it did,
apparently from a stale AssetDatabase index earlier in the same batch run.
**Fix:** added one retry via `AssetDatabase.Refresh()` if the first load returns null, **and** a safe grey
fallback material if it's still null after that, so this can never render as Unity's magenta error colour again
regardless of root cause.

### 6. The "marble" material was a flat 2-tone checkerboard, not marble
Not a functional bug, but flagged here because it read exactly like Unity's universal "no texture" placeholder
look and was reported as a "broken material" during recon. See Visual Improvements below.

---

## Priority 2 — Visual improvements

### MuseumNight
- **Marble material rebuilt.** `MuseumBuilder.Marble()` replaces the old flat checkerboard (`Checker()`) with a
  proper turbulence-vein algorithm (layered Perlin noise warping a sine-wave band pattern) — used throughout the
  museum's floors, stairs and columns, and inherited by ClockCore's walls/ceiling/dais/statues since they share
  the same material asset.
- Player spawn/camera-clipping bug fixed (see above) — the museum interior, Clock of Creation area and lighting
  are now actually visible from spawn instead of a wall fill.

### FrozenCity
- **Building facades.** Replaced flat plaster walls with a new procedural `BuildingFacade` material: a stone-block
  texture with a grid of windows, roughly 2/3 of them warmly lit (emissive) and the rest dark, so the street reads
  as a once-lived-in city rather than a row of blank boxes.
- **Dedicated lighting.** FrozenCity previously had no scene-specific lighting at all (only MuseumNight had one).
  Added a cool, low dusk-blue directional key light with a cold ambient floor, so the warm lanterns along the
  central path read as the one remaining point of warmth/life in a frozen city.
- **Lanterns now actually light the street.** The lantern posts had a glowing (emissive) lamp mesh but no `Light`
  component — they looked lit but cast nothing. Added a real point light to each.

### ClockCore
- Inherits the fixed veined-marble material (floor/walls/ceiling/dais/statues) instead of the checkerboard.
- **Dedicated lighting.** Added a moody violet ambient (the "final area" should feel otherworldly) with one
  focused warm spotlight over the Collector's dais, so the boss reads as the room's deliberate focal point instead
  of blending into flat ambient light.
- **Procedural brass clockwork gears.** Three wall-mounted gear props (a flat brass disc, radial teeth, and a
  small emissive time-energy hub) built from primitives + the existing brass/lens-glow materials — the same
  "primitives dressed with real materials and lighting" technique already used throughout this project's
  architecture (museum walls, hinge props, columns), giving the arena an actual clockwork visual language instead
  of bare walls.

### All three scenes
- The particle-material fix (Priority 1 #2) directly improves every scene's presentation quality — no more
  magenta flashes on era switch, shard pickup, fracture, or near the Shadow enemy.

---

## Remaining known issues

- **Warden, Chrono Shadow and Collector remain primitive capsules.** No real character/creature model assets
  exist anywhere in the project (confirmed by inspection — `Assets/Models/Characters` etc. contain no such
  assets). Per instructions, these were **not** dressed up with more primitives pretending to be finished art —
  see `docs/Morning_Manual_TODO.md`.
- **ClockCore's ceiling shows sky through it at extreme camera angles** — the dressing ceiling is a finite 40×40
  plane; looking sharply upward near the room's edge can catch the skybox beyond it. Minor, viewing-angle-only,
  not fixed given the time budget.
- **The era-switch particle burst and its SFX cue fire once automatically on scene load** (by design — the same
  `EraManager.EraChanged` event that fires this also correctly initializes era-bound object visibility on load,
  and every other listener needs that same initial call). This means a soft blue flash + chime plays a moment
  after any scene loads, before the player has touched Q/R. Left as-is: fixing it would require either changing
  `EraManager`'s public event contract (used by several other listeners) or adding a fragile "first call" flag
  to two individual VFX/audio listeners for a very minor, easy-to-miss cosmetic effect.
- **MuseumNight's museum interior lighting is still fairly dim/flat** in the recon screenshot compared to
  FrozenCity/ClockCore's new dedicated lighting — it already had a lighting pass from Phase 7
  (`BuildMuseumLighting`: cold moonlight + 4 warm exhibit spots) which was left untouched since it wasn't
  demonstrably broken, but a further contrast pass here would likely help.

See `docs/Morning_Manual_TODO.md` for everything that requires manually sourcing/importing a real asset.

---

## Files changed

**Editor builders** (all existing tools, extended — none rewritten):
- `Assets/Editor/AudioAndVfxBuilder.cs` — particle material fix; FrozenCity/ClockCore lighting.
- `Assets/Editor/MuseumBuilder.cs` — `Marble()` replaces `Checker()`.
- `Assets/Editor/ScenePolishBuilder.cs` — `BuildingFacade` material + generator; `BuildGear()`; lantern point lights.
- `Assets/Editor/NavigationBuilder.cs` — material-load retry + safe fallback.
- `Assets/Editor/TutorialTextBuilder.cs` — auto-sizing text.
- `Assets/Editor/CoreSystemsBuilder.cs`, `FrozenCityContentBuilder.cs`, `ClockCoreContentBuilder.cs` —
  `RespawnService.sceneStart` wiring.

**Runtime scripts:**
- `Assets/Scripts/World/GameplayVfx.cs` — particle material fix (runtime-created shard/fracture bursts).

**Generated assets** (regenerated/new, tracked in git):
- `Assets/Materials/Museum/MuseumMarble.{mat,png}` — regenerated (veined, not checkerboard).
- `Assets/Materials/Museum/{MuseumBrass,MuseumPlaster,MuseumWood}.mat` — regenerated alongside Marble (same
  `CreateMaterials()` call; content unchanged, timestamps only).
- `Assets/Materials/Dressing/BuildingFacade.{mat,png}` and `BuildingFacadeEmission.png` — new.
- `Assets/Prefabs/Player/Player.prefab`, `Assets/Prefabs/World/ChronoOrb.prefab` — re-saved by the rebuild
  (Noa/camera/orb-trail wiring re-verified, no content change).

**Scenes** (rebuilt via the existing builder chain — content, not hand-edited):
- `Assets/Scenes/MuseumNight.unity`, `FrozenCity.unity`, `ClockCore.unity`.

**Untouched, confirmed:**
- `MuseumOfTime.slnx` — still the pre-existing pending deletion from before this session; not touched.
- No Phase 3/4 core gameplay script was modified (era system, AI, interaction, triggers, health/energy/score,
  save/load, NavMesh agent types, LayerMask code, steering behaviours all untouched).
- Scene serialization mode unchanged for all three scenes.

## Reference
See `docs/Morning_Manual_TODO.md` for the manual asset-import work that needs a human — none of it was faked or
worked around with more primitives.
