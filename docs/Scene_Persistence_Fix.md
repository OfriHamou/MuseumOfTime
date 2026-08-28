# Scene Persistence — Root Cause and Fix

This documents why `MuseumNight`, `FrozenCity` and `ClockCore` kept appearing empty after a fresh
`git pull`/clone, what was actually wrong, and the normal teammate workflow now that it's fixed.

**`docs/error_fix.txt`'s recovery process (running `FullSceneRebuild.BuildAll`) is no longer needed for a normal
pull.** It still exists as an optional recovery/development tool - see the bottom of this document.

---

## The normal workflow (read this first)

```
git pull
git lfs pull        # only if you see LFS pointer stubs instead of real files - see below
open the project in Unity
open MuseumNight / FrozenCity / ClockCore
press Play
```

**You should never need to run `FullSceneRebuild`, a recovery script, or any Claude session to see a populated
scene.** If you do, something has regressed - see "If it happens again" below.

---

## Root cause

Two independent things were wrong. Only the first actually caused scenes to go empty; the second was found and
fixed along the way because it looked identical from the symptoms alone and had to be ruled out properly rather
than assumed.

### The real cause: baked NavMesh data embedded directly in the scene

`MuseumNight`, `FrozenCity` and `ClockCore` each carry two `NavMeshSurface` components (the Warden and the
Chronological Shadow, per T13/T16's "two agent types, two separate bakes" requirement). Baking a NavMeshSurface
produces a `NavMeshData` object, and by default that object is stored **embedded inside the scene** rather than as
its own file.

Unity cannot serialize `NavMeshData` as text - it is fundamentally a binary payload. A scene file has to be one
consistent format, so **any scene holding embedded NavMesh data is forced to binary serialization for the
entire file, regardless of the project's `EditorSettings.serializationMode`.** This was confirmed directly, not
assumed:

1. A brand-new, empty scene saved as proper text under the project's settings.
2. A fresh copy of `MuseumNight`, saved to a brand-new path under the same settings, still came out binary.
3. Diffing `MuseumNight`'s component types against `MainMenu` (which has always saved correctly) left
   `Unity.AI.Navigation.NavMeshSurface` as the standout candidate - a type `MainMenu` doesn't have at all.
4. Checking each `NavMeshSurface.navMeshData`'s asset path directly showed it was empty (embedded) in all six
   cases across the three gameplay scenes, and only those three scenes have any `NavMeshSurface` at all.

That is the complete explanation for **why only these three scenes were affected**: they are the only scenes with
baked, embedded NavMesh data. `MainMenu` and `Victory` have none and were never affected.

Binary scenes are also exactly the case this repository's `.gitattributes` does not handle safely: `*.unity` is
declared `unity-yaml` (text, line-mergeable, `eol=lf`), and no real `unityyamlmerge` driver is configured
(`git config merge.unityyamlmerge.driver` returns nothing on this machine), so git falls back to a naive
line-based **text** merge on what is actually **binary** content the moment two branches touch the same scene -
exactly what a rebase or a PR merge does. That naive merge doesn't have to fail loudly; it can produce a
structurally-invalid binary file that still contains readable fragments of the original object names (which is
why raw byte inspection of a "broken" scene always found real content, and why the file's size looked normal) while
Unity's binary deserializer reads it back as zero objects.

**Fix:** `NavMeshExternalizer.cs` moves each embedded `NavMeshData` out to its own file under `Assets/NavMesh/`
and re-points the surface at it. Once no scene holds any embedded binary sub-asset, the scene file itself is free
to serialize as pure YAML text again. This was wired into the three places that bake NavMesh
(`NavigationBuilder.cs` for MuseumNight, `FrozenCityContentBuilder.cs`, `ClockCoreContentBuilder.cs`) immediately
after the existing `BuildNavMesh()` calls, so a future rebuild can never reintroduce the embedded state - confirmed
by running the full pipeline from a completely clean `Library/` (full reimport) and checking the result stayed
text.

`.gitattributes` gained one new rule, following the project's own existing precedent for `LightingData.asset`:

```
Assets/NavMesh/*.asset binary
```

These six files are genuinely binary NavMesh payloads. Marking them `binary` stops git from ever attempting a
text/line merge on them again. If one is ever lost or conflicted, the fix is a re-bake (`NavigationBuilder` /
the two content builders already do this) - trivial, unlike losing an entire populated scene.

### Ruled out, not the cause: `EditorSettings.serializationMode`

`ProjectSettings/EditorSettings.asset` has always specified `m_SerializationMode: 2` (**ForceText**) - checked
against the full git history of that file, which shows only its original creation. This setting was never the
problem. (A mistaken correction to it was made and reverted during this investigation, once a
`_CheckSerializationMode` diagnostic confirmed the actual enum mapping - `0 = Mixed, 1 = ForceBinary,
2 = ForceText` - the opposite of what was initially assumed. No net change to this file.)

Ruled out before landing on the NavMesh explanation, each with direct evidence:

- **Git LFS** - `.unity` files are not, and should not be, LFS-tracked (`git lfs ls-files` confirms none of the
  five scenes are LFS pointers). LFS was never involved and nothing here changes its configuration.
- **A stale Unity cache** - `Library/` was deleted entirely and the project fully reimported from scratch; the
  three scenes were still saved binary afterward, ruling out any cached artifact.
- **A builder/import hook silently overwriting scenes** - no `[InitializeOnLoad]` or `AssetPostprocessor` exists
  anywhere in the project; `FullSceneRebuild` and every other builder run only when explicitly invoked via
  `-executeMethod` or their menu item.
- **Incomplete serialization / a corrupted commit** - the committed scene files were confirmed byte-identical to
  their git blob objects (via `git cat-file`, bypassing any checkout filter), and raw string extraction from the
  binary content found real object names and component references. The data was never missing; Unity's binary
  deserializer simply could not reconstruct it after whatever merge produced that specific byte layout.

---

## The camera/interaction test (item 7)

`WinnableByPlayingTests.TheGameCanBeWonUsingOnlyThingsAPlayerCanDo` failed reproducibly: standing directly in
front of the FrozenCity gear pickup and looking straight at it, the interaction cast never found it.

**Root cause:** pressing the first/third-person toggle changes `CinemachineBrain`'s active virtual camera
immediately, but the actual `Camera.main` transform only catches up once the brain finishes blending to it. No
blend duration was ever configured anywhere in the project, so Cinemachine used its **stock default: a 2-second
ease-in-out blend** - meant for cinematic cuts, not a perspective-toggle key. Confirmed directly: immediately
after toggling to first person, `CinemachineBrain.ActiveVirtualCamera` correctly reported `FirstPersonCamera`,
but `Camera.main.transform.position` still matched the **third-person** camera's position, by design, because
the blend hadn't finished. The interaction ray was being cast from the wrong camera position for two full
seconds after every single toggle.

This is not just a test artifact - it's a real gameplay inconsistency. A player pressing `C` expects the view
(and therefore where their aim comes from) to change immediately, not drift into place over two seconds.

**Fix:** `CinemachineBrain.DefaultBlend` is now explicitly set to an instant cut
(`CinemachineBlendDefinition.Styles.Cut`, 0 seconds) in both `MuseumSceneSetup.cs` and
`CameraRigParityBuilder.cs`, wherever the brain is created. The third-person camera framing itself
(`ShoulderOffset`, `VerticalArmLength`, `CameraDistance = 2.6`) was not touched - it remains the same reasonable
close third-person exploration camera as before. Confirmed by direct diagnostic: `Camera.main`'s position now
matches `FirstPersonCamera`'s position exactly, in the same frame as the toggle, and the previously-failing test
now passes.

---

## Verification result

`Tools/verify.ps1` → **PASS - 146/152 tests passed, 0 failed** (6 intentionally ignored, unchanged from before).

---

## Manual test to perform yourself

1. Open the project in Unity (no batch script, no menu command run first).
2. Open `MuseumNight`, then `FrozenCity`, then `ClockCore` in turn. Each should show its full populated
   Hierarchy immediately - the museum interior, the frozen city street, the ClockCore arena, all gameplay
   objects, NavMesh agents, triggers, etc.
3. Press Play in `MuseumNight`. Confirm normal movement, the HUD, and that walking works.
4. Press `C` to toggle the camera. It should cut instantly between first- and third-person with no drift or
   delay.
5. `git status` afterward - opening/playing a scene should not, by itself, mark it as modified.

## Fresh-pull test for a teammate

1. `git clone` (or `git pull` on `fix/persistent-scenes`) into a clean folder/machine.
2. `git lfs pull` if any file under version control shows as a small text pointer instead of its real content
   (standard LFS symptom - unrelated to this fix, since no scene file is LFS-tracked).
3. Open the project in Unity Hub. Let the initial import finish (this happens on any fresh clone regardless of
   this fix - it is Unity re-generating its local, git-ignored `Library/` cache, not a sign of anything wrong).
4. Open `MuseumNight`. **The Hierarchy should already show every object** - no builder, no script, no menu
   command.
5. Press Play. The game should run.

If step 4 ever shows an empty Hierarchy again, see "If it happens again" below before assuming this fix
regressed.

---

## If it happens again

This class of bug can resurface if a NavMeshSurface is baked through some path that bypasses
`NavigationBuilder`/`FrozenCityContentBuilder`/`ClockCoreContentBuilder` (for example, baking manually via the
Inspector's "Bake" button without also running `NavMeshExternalizer.SaveExternal` on the result). If a scene ever
goes binary again:

1. Confirm it's actually this cause: `Museum of Time > Externalize Embedded NavMesh Data (recovery)` menu item,
   or `NavMeshExternalizer.Run()` via `-executeMethod`. It only touches NavMesh data references and is safe to
   run at any time.
2. If that doesn't fix it, treat it as a new investigation - don't assume it's the same root cause without
   checking, the way this document had to rule out several plausible-looking explanations before finding the
   real one.

`FullSceneRebuild.BuildAll` remains available as a full rebuild-from-source-code recovery tool
(see `docs/error_fix.txt`) for the case where a scene's content itself needs regenerating, not just
re-serializing - but it is no longer part of the normal workflow.
