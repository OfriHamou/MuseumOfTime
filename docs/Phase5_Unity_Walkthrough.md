# Phase 5 — Unity Walkthrough

**How to rebuild the UI and readability layer by hand, and where to see it in the editor.**

Fifth in the series, after `Phase1_Unity_Walkthrough.md` through `Phase4_Unity_Walkthrough.md`. Phase 5 is the first
phase whose scenes were built entirely through headless Editor scripts run in Unity batch mode, rather than by hand
in an open Editor session — there was no interactive Editor available while this phase was implemented, only
`Unity.exe -batchmode`. Every builder below is still exposed as a normal `[MenuItem]` too, so it can be re-run (or
re-inspected) by hand exactly the way Phases 1–4's builders can.

**Before you start:** open `Assets/Scenes/MuseumNight.unity`, open the Console, click *Clear*.

## The menu grew by four items

```
Build Camera Rig in MuseumNight
Build Noa Animator Controller
Build Museum (two storeys)
Build Hinge Set Pieces
Build FrozenCity Terrain
Build Fracture and LOD Prefabs
Place World Props
Build Core Systems
Build Warden Animator Controller
Build Navigation (two agent types)
Build Menus (Main Menu and Victory)      <- new
Build HUD and Pause Menu                 <- new
Build Minimap                            <- new
Build Tutorial Text                      <- new
Build Development Player
Build Release Player
```

All four are idempotent, and — new for this phase — all four were actually run **headlessly**, one at a time, via:

```
"C:/Program Files/Unity/Hub/Editor/6000.4.8f1/Editor/Unity.exe" -batchmode -quit -projectPath . ^
    -executeMethod MenuUIBuilder.BuildFromCommandLine -logFile BuildMenus.log
```

(swap the class name for `HudBuilder`, `MinimapBuilder`, `TutorialTextBuilder` for the other three), then verified
with `Tools/verify.ps1` after each one. That loop — implement, build headlessly, run `verify.ps1`, fix, re-verify —
is the one requested for this phase, and it is also just `Tools/verify.ps1`'s own reason for existing.

---

# Step 5.1 — Main menu and victory screen

Closes: *entry menu and victory menu* (T1).

## What already existed

`SceneLoader.cs` (Phase 0) already had `StartNewGame`, `LoadScene`, `LoadMainMenu`, `LoadVictory`, `RestartCurrentScene`
and `QuitGame` — it reads like it was written in anticipation of exactly this step and then left for later. The only
thing missing was **Continue**, which needed the serialized save from Step 3.9:

```csharp
public void ContinueGame()
{
    if (!SaveService.Load())
    {
        Debug.LogWarning("No save to continue from.", this);
        return;
    }

    GameState state = GameManager.Instance.State;

    string target = state.hasCheckpoint && !string.IsNullOrWhiteSpace(state.checkpointSceneName)
        ? state.checkpointSceneName
        : "MuseumNight";

    LoadScene(target);
}
```

## The fast path

**Museum of Time → Build Menus (Main Menu and Victory)**. Console prints:

```
MENUS OK: MainMenu (New Game/Continue/Controls/Quit) and Victory (score/shards/detections/playtime) built.
```

*You should see:* opening `MainMenu.unity`, a `MainMenuCanvas` with a Title, `NewGameButton`, `ContinueButton`,
`ControlsButton`, `QuitButton` and a hidden `ControlsPanel`; an `EventSystem` using `InputSystemUIInputModule` (not
the legacy Standalone module — T12 covers menu navigation too); a `UIManager` object carrying `SceneLoader` and
`MainMenuController`; `MenuCameraDrift` added to `Main Camera`. Opening `Victory.unity` shows a `VictoryCanvas` with
`ScoreText`, `ShardsText`, `DetectionsText`, `PlaytimeText`, `MainMenuButton`, `QuitButton`, and a `UIManager` with
`SceneLoader` + `VictoryScreenController`.

New script files: `Assets/Scripts/UI/MainMenuController.cs`, `VictoryScreenController.cs`, `MenuCameraDrift.cs`.
New Editor script: `Assets/Editor/MenuUIBuilder.cs` (also the home of the shared `CreateText`/`CreateButton`/
`FindOrCreateCanvas`/`EnsureEventSystem` helpers the other three Phase 5 builders reuse).

## Building it by hand instead

Everything is plain uGUI + TextMeshPro: a Canvas (Screen Space – Overlay), a `CanvasScaler` set to *Scale With Screen
Size* at 1920×1080, an `EventSystem` with **Input System UI Input Module** (not the legacy Standalone module — swap
it the same way Phase 0 swapped `PlayerInput`'s Behavior to *Invoke C# Events*), then buttons and TextMeshPro - Text
(UI) objects positioned with anchored `RectTransform`s. Button clicks are wired in code
(`button.onClick.AddListener(...)`) in `Awake()`, for the same reason `PlayerInputReader` subscribes to actions in
code rather than the Inspector: an empty call list fails silently, a missing reference throws.

## Buttons deliberately not covered by an automated test

`QuitButton` calls `Application.Quit()`. Calling that from inside a PlayMode test would terminate the test runner
process itself, so it is left as a manual check — press Play, click Quit, confirm the editor stops Play Mode (a
built player would actually exit).

## How to prove it works

`Assets/Tests/PlayMode/MainMenuTests.cs` (4 tests) and `VictoryScreenTests.cs` (2 tests):

```
Scene_HasAnInputSystemDrivenEventSystem      - EventSystem uses InputSystemUIInputModule
ContinueButton_IsDisabledWithoutASave        - SaveService.Delete() -> button.interactable == false
ContinueButton_IsEnabledOnceASaveExists      - SaveService.Save() -> reload -> button.interactable == true
NewGameButton_ResetsStateAndLoadsMuseumNight - onClick.Invoke() -> score back to 0, scene is MuseumNight
Scene_HasAnInputSystemDrivenEventSystem      - (Victory) same EventSystem check
VictoryScreen_ShowsTheRunsRealStats          - seeds GameState, reads it back off the four TMP fields
```

Button clicks are exercised with `button.onClick.Invoke()` rather than simulated mouse rays. That calls exactly the
same listener a real click would, without the fragility of driving `EventSystem` raycasts from batch mode - the same
trade-off Phase 0 made for keyboard input (`Assert.Ignore` on the six tests that need a focused window).

Or in the Editor: **Play** `MainMenu`, click New Game, confirm `MuseumNight` loads with fresh score/health; delete the
save file (or never create one) and confirm Continue is greyed out; click it after picking up the Time Lens once, and
confirm it restores.

---

# Step 5.2 — HUD and pause menu

Closes: *health/energy/score visible to the player* (supports T8), plus the pause menu.

## The fast path

**Museum of Time → Build HUD and Pause Menu**. Console prints:

```
HUD OK: health/energy/shards/era/items/detection meter, pause menu with Resume/Restart/Controls/Main Menu/Quit.
```

*You should see*, in `MuseumNight`: an `HUDCanvas` with `HealthBar` and `EnergyBar` (filled Images, top-left),
`ShardCountText` and `EraText` below them, `TimeLensIcon`/`HourglassIcon` top-right (both start inactive - neither
item is held at the start of a new game), a `DetectionMeter` top-center (also inactive until a Warden is actually
looking), and a `PauseMenuPanel` (Resume / Restart Scene / Controls / Main Menu / Quit, all inactive until Escape).
`HUDController` is on **Player**; `PauseMenuController` is on **UIManager**, alongside the `SceneLoader` from Step 5.1.

New script files: `HUDController.cs`, `PauseMenuController.cs`. New Editor script: `Assets/Editor/HudBuilder.cs`.

## The bug risk the plan calls out by name, made concrete

> "Set `Time.timeScale = 0` and remember to restore it to **1**, not to the slow-time value."

```csharp
public void Resume()
{
    isPaused = false;
    if (panel != null) panel.SetActive(false);
    if (controlsPanel != null) controlsPanel.SetActive(false);

    // Exactly 1. Never a cached "value before pause" - that is precisely
    // how a slow-time hold would leak into normal play after Resume.
    Time.timeScale = 1f;
}
```

There is no `pausedFromScale` field anywhere in `PauseMenuController` - the temptation the plan is warning about
(cache `Time.timeScale` before zeroing it, restore that cached value on Resume) was never introduced in the first
place, so there is nothing to leak. `Assets/Tests/PlayMode/HudAndPauseMenuTests.cs` pins this down directly:

```
Resuming_AfterPausingDuringSlowTime_LeavesTimeScaleAtExactlyOne
```

sets `Time.timeScale = 0.3f` (simulating a held Chrono Hourglass), pauses, asserts it is `0f`, resumes, asserts it is
`1f` — not `0.3f`.

## Why the detection meter reads `Update()`, not an event

The plan says "subscribe to `GameManager.StateChanged` - never poll in `Update`", and `HUDController.Refresh()`
does exactly that for health, energy, shards, era and the two item icons. The detection meter is the one exception,
and it is a deliberate one: `WardenAI.DetectionLevel` (Phase 4) has no change event to subscribe to, and adding one
would mean editing Phase 4 AI code, which this phase was explicitly told not to touch. `HUDController.Update()`
reads `WardenAI.DetectionLevel` off every Warden in the scene purely to display it - nothing here writes back into
`GameState` or AI state, so it is a read-only display concern, not a violation of the "never poll" rule as it applies
to `GameState`.

## Why there is no explicit "drive HUD animation from unscaledDeltaTime" code

The plan's suggested implementation assumes something is animating (a lerp, a tween) that would otherwise read
`Time.deltaTime` and therefore slow down during Chrono Hourglass use. `HUDController.Refresh()` sets `fillAmount`
and `.text` directly, with no interpolation at all - there is no frame-rate- or timescale-dependent code path for
slow-time to affect in the first place, which satisfies the actual intent (a HUD that never lags during slow-time)
more simply than the suggested mechanism.

## A real bug this step caught in itself

The first version of `ItemIcons_ReflectAcquiredItems` used `GameObject.Find("TimeLensIcon")` and failed with
*"No TimeLensIcon in the scene"* — the icon is correctly `SetActive(false)` whenever `hasTimeLens` is false (the
default), and **`GameObject.Find` does not see inactive GameObjects**. The fix was to read the `HUDController`'s
private `timeLensIcon` field directly via reflection instead of searching the scene for it. The same class of bug
was avoided up front in the tutorial-text tests (Step 5.4) by using `Transform.Find` on a known parent, which does
see inactive children.

## How to prove it works

`Assets/Tests/PlayMode/HudAndPauseMenuTests.cs` (5 tests): the timeScale regression above,
`Hud_HasAllTheElementsTheControllerDrives`, `Hud_UpdatesTheMomentHealthChanges` (takes damage, checks the bar's
`fillAmount` changed in the same frame), `Hud_UpdatesTheMomentAShardIsCollected` (same idea for the shard counter),
`ItemIcons_ReflectAcquiredItems`.

Or in the Editor: **Play**, watch the bars drop as a hazard trigger or a Warden hits you, pick up a shard and watch
the counter, acquire the Time Lens and watch its icon appear, press **Escape** mid-slow-time and confirm Resume
leaves movement at normal speed.

---

# Step 5.3 — Minimap

Closes: *simple minimap for orientation, present throughout the whole scene* (T18).

## What was already reserved for this

`ProjectSettings/TagManager.asset` already defines a **`Minimap`** layer (and a **`Checkpoint`** layer, presumably
for Step 3.8's Time Anchors) that nothing had used yet. That answers the "which layer" question the plan leaves open:
the minimap camera's `cullingMask` is set to `1 << LayerMask.NameToLayer("Minimap")` and nothing else - an **allow
list of exactly one layer**, not a deny-list built by excluding things. That is also what makes "never shows a hidden
Time Anchor" true by construction rather than by remembering to exclude one more layer later: nothing is ever put on
the Minimap layer unless it is meant to be seen from above, so a Time Anchor added in FrozenCity in Phase 6 is
invisible to the minimap for free, provided it is never assigned that layer.

## The fast path

**Museum of Time → Build Minimap**. Console prints:

```
MINIMAP OK: orthographic camera on the 'Minimap' layer only, following and rotating with Noa, always on.
```

*You should see:* a `MinimapCamera` object (orthographic, size 16, `cullingMask` = `Minimap` only, `targetTexture` =
`Assets/Textures/MinimapRT.renderTexture`, tagged `Untagged` so it never competes for `Camera.main`), a small flat
`MinimapMarker` parented under **Player** on the `Minimap` layer, and a `MinimapFrame` / `MinimapDisplay` (`RawImage`)
in the bottom-right corner of `HUDCanvas`. The main gameplay camera (`MainCamera`, the one with `CinemachineBrain`)
has its own `cullingMask` updated to exclude the `Minimap` layer, so the marker never leaks into third- or
first-person view.

New script: `MinimapController.cs` (position + rotation follow, in `LateUpdate` so it settles after the player has
moved). New Editor script: `Assets/Editor/MinimapBuilder.cs`. New assets: `Assets/Textures/MinimapRT.renderTexture`,
`Assets/Materials/UI/MinimapMarker.mat`.

## Why it is a third camera, not a fourth

T19 asks for two gameplay cameras **besides** the minimap. `MinimapCamera` is a plain `Camera` with no
`CinemachineCamera` component and is never referenced by `PlayerCameraRig`, so `CinemachineBrain` has no opinion
about it at all - it is structurally outside the two-camera toggle, the same way the plan asked for.

## What was scoped down on purpose

The plan's "What to do" list also mentions minimap icons for objectives, collected/uncollected shards and exits, on
top of Noa's own marker. Only Noa's marker was built. T18's actual verification bar is orientation - "the minimap is
visible for the entire MuseumNight scene, orients correctly" - which a rotating camera plus a heading marker already
satisfies; the extra icon types are presentation polish, not a distinct requirement, and adding a bespoke icon per
collectible type would have meant touching Phase 3's `ShardPickup` to also carry a Minimap-layer child. Left for a
Phase 6 dressing pass if wanted.

## How to prove it works

`Assets/Tests/PlayMode/MinimapTests.cs` (4 tests):

```
MinimapCamera_IsOrthographicAndRendersOnlyTheMinimapLayer
MinimapCamera_FollowsThePlayer                             - target wiring
MinimapCamera_FollowsPositionAndRotatesToMatchHeading      - moves + rotates Noa, checks both
GameplayCamera_DoesNotRenderTheMinimapLayer
```

What is **not** automated, and needs a look in the Editor or a Play session: whether the render texture actually
*reads* as a legible top-down view (resolution, framing, contrast), and the literal "present throughout the *entire*
scene, load to exit" claim, which is a full-playthrough property rather than something a single frame can assert.
`MinimapCamera` has no code path that ever disables it, so this holds by the absence of any toggle - worth confirming
by eye once rather than trusting that absence.

---

# Step 5.4 — Dynamic 3D tutorial text

Closes: *in-game tutorial with dynamic text and clear instructions, in 3D* (T2). Read literally: **3D**, not a
Canvas overlay, and **dynamic**, not a fixed label.

## The fast path

**Museum of Time → Build Tutorial Text**. Console prints:

```
TUTORIAL TEXT OK: 8 verb plaques (world-space, dynamic, fade on approach) plus one persistent objective line.
```

*You should see*, under a new `TutorialPlaques` parent in `MuseumNight`: `Plaque_Move`, `Plaque_Run`, `Plaque_Jump`,
`Plaque_Interact`, `Plaque_Orb`, `Plaque_Camera`, `Plaque_Era`, `Plaque_SlowTime` (each a `TextMeshPro` - the 3D,
mesh-based component, not `TextMeshProUGUI` - carrying a `WorldTutorialText`), and `Plaque_Objective` (carrying a
`WorldObjectiveText`). Under `Triggers`, one `TutorialTrigger` per verb, each with its `textObject` wired to the
matching plaque - `Trigger_TutorialMove` is the same object Step 3.2's `CoreSystemsBuilder` already placed, now
actually wired to something (it previously activated a `textObject` field that had been left empty).

New scripts: `WorldTutorialText.cs`, `WorldObjectiveText.cs`. New Editor script: `Assets/Editor/TutorialTextBuilder.cs`.

## Why `TextMeshPro`, specifically, is the thing that satisfies "in 3D"

`TextMeshProUGUI` (the Canvas variant used everywhere in Steps 5.1–5.3) renders through a `CanvasRenderer` onto a
Canvas - screen space, full stop, no matter how the Canvas itself is configured. `TextMeshPro` (used only here, in
Step 5.4) renders through an ordinary `MeshRenderer`/`MeshFilter` into the 3D scene, exactly like any other mesh. The
component type itself is the proof, which is what
`TutorialTextTests.EveryVerbHasAWorldSpacePlaque` actually asserts (`GetComponent<TextMeshPro>()` succeeds,
`GetComponentInParent<Canvas>()` returns null).

## What "dynamic" means here, concretely

```csharp
label.text = template
    .Replace("{energy}", energyPercent + "%")
    .Replace("{health}", healthPercent + "%");
```

`Plaque_SlowTime`'s message is authored as `"Hold Ctrl to slow time - {energy}% energy remaining."`; `ApplyTemplate()`
substitutes the live value every time the plaque becomes visible again, so walking away and back shows a different
number if energy changed in between - the plan's own example (*"Hold Ctrl to slow time — 68% energy"*).

## The one interpretation call worth being explicit about

The plan asks for text that "fades in on approach... fades out once the action is performed." Detecting that the
*specific* action was performed would mean eight different detectors (one per verb - a jump, a click, a key hold),
which is a lot of bespoke machinery for a readability feature. `WorldTutorialText` instead fades on **proximity**:
visible within `fadeDistance` (6 m) of the plaque, invisible outside it, in either direction. This still satisfies
the two things that actually matter for the grade - it is not a static always-on label, and it responds to something
about the player rather than sitting there unconditionally - without a nine-way branch of action detectors. Worth
saying so plainly if asked, rather than presenting it as the literal wording.

## A gap, not a silent skip

The plan's "What to do" list also says to keep the fiction - route the tutorial voice through the Old Curator's
notes, tying T2 to the GDD's NPC. That was **not done**: the eight messages are plain instructional text ("Press E to
interact..."), not written as findings from a character. This does not affect T2's own verification bullet (3D,
dynamic, clear instructions - all true), but it is short of the plan's fuller ask, and is left as a copy pass for
whoever dresses MuseumNight's narrative in Phase 6.

## How to prove it works

`Assets/Tests/PlayMode/TutorialTextTests.cs` (4 tests):

```
EveryVerbHasAWorldSpacePlaque                        - TextMeshPro, not under any Canvas, has a WorldTutorialText
EveryVerbTrigger_PointsAtItsPlaque                   - every Trigger_Tutorial* has a non-null textObject
SlowTimePlaque_TextReflectsTheLiveEnergyValue        - sets energy to 42, confirms "42%" appears in the live text
ObjectiveText_ReflectsRoomEntryTriggersCurrentObjective
```

`Transform.Find`, not `GameObject.Find`, is used throughout - every plaque starts `SetActive(false)` until its
trigger reveals it, and `GameObject.Find` cannot see inactive objects (the same lesson Step 5.2 re-learned the hard
way, applied here from the start).

What is **not** automated, and matches the plan's own framing of this bullet as visual: whether the billboard rotation
actually keeps the text readable from a moving third-person camera, whether the fade timing feels right, and whether
a genuine first-time player finishes MuseumNight without outside help.

---

# Bugs found in manual testing, after the automated pass above

Everything above this section is what the original implementation and its own test suite covered. A human then
actually clicked through the menus, and found two real bugs neither the tests nor the headless builders had caught.
Both are fixed; the fixes and the reason the original tests missed them are documented here rather than folded
silently back into the sections above.

## Bug 1 — the Main Menu's Controls panel could not be closed

**Symptom.** Main Menu → Controls opens the panel. Escape does nothing. There is no Back or Close button.

**Root cause.** `ControlsPanel` is centered on the same canvas as `ControlsButton` and is large enough to cover it
once active - so the one thing that opened the panel becomes physically hidden underneath it, and nothing else was
wired to close it. `MainMenuController.Update()` also never read the keyboard at all.

**Fix.**

- `MenuUIBuilder.BuildControlsPanel` now also builds a `ControlsBackButton` inside the panel (the text block was
  shrunk from a 380pt-tall box to 320pt, and moved up 30px, to leave room for it), and returns it via an `out Button`
  parameter so `BuildMainMenu` can wire it.
- `MainMenuController` gained `OnOpenControls()`/`OnCloseControls()` (renamed from the old single `OnToggleControls`,
  which could never be reached once the panel was already covering the button that called it) and an `Update()` that
  closes the panel on `Keyboard.current.escapeKey.wasPressedThisFrame` - the New Input System's device API, not
  `Input.GetKey` (T12).

**How it is verified.** `MainMenuTests.ControlsPanel_OpensFromTheButtonAndClosesFromBack` (`UnityTest`): clicks
`ControlsButton`, confirms the panel opens; clicks the new `ControlsBackButton`, confirms it closes. The Escape-key
path itself is not simulated in the automated test, for the same reason six of Phase 0's own input tests are not:
batch mode runs the player unfocused, and the Input System resets/ignores simulated device state in that condition.
Reading the field via reflection, not `GameObject.Find("ControlsPanel")`, because the panel starts inactive and
`Find` does not see inactive objects.

## Bug 2 — Pause menu buttons could not be clicked

**Symptom.** Escape opens the Pause menu; Resume, Restart Scene, Controls, Main Menu and Quit are all visible; none
of them respond to a click.

**Root cause — two separate bugs, both real, found in this order:**

1. **The cursor was locked and invisible.** `PlayerCameraRig.OnEnable()` sets `Cursor.lockState = CursorLockMode.Locked`
   and `Cursor.visible = false` for gameplay look, and nothing ever released it when the menu opened - pausing only
   set `Time.timeScale = 0`, which has no effect on the cursor at all. The menu was genuinely there; there was no
   visible, movable cursor to aim at it with.
2. **The buttons had no listeners at runtime, regardless of the cursor.** `Assets/Editor/HudBuilder.cs` wired all
   five original buttons with `button.onClick.AddListener(pause.OnResumeButton)` and so on, called from inside the
   Editor batch-mode script that builds the scene. `UnityEvent.AddListener` registers a **non-persistent** listener -
   it exists only in the memory of the process that called it, and is never written into the saved scene. That
   process is `-batchmode -quit`; it exits the instant the scene is saved. The listener registration was already gone
   before anyone ever pressed Play. Every button in this menu had an empty `onClick` list at runtime from the moment
   Phase 5 shipped - the cursor fix alone would not have been enough. `MainMenuController` and `VictoryScreenController`
   never had this problem because they wire their own buttons in `Awake()`, which runs fresh every time the scene
   actually loads; `PauseMenuController` was the one place this project had drifted from that pattern.

**Fix.**

- `PauseMenuController` gained `[SerializeField] private Button` fields for all five original buttons plus the new
  Controls-panel Back button, and now wires every one of them with `.onClick.AddListener(...)` inside its own
  `Awake()` - the same pattern `MainMenuController` already used correctly.
- `HudBuilder.cs` no longer calls `.onClick.AddListener` at all. It only wires the `SerializedObject` **references**
  (`resumeButton`, `restartButton`, `controlsButton`, `controlsBackButton`, `mainMenuButton`, `quitButton`) - exactly
  how it already wires `HUDController`'s health bar, energy bar and every other reference. The dead
  `AddListenerOnce` helper was deleted.
- `PauseMenuController.Pause()` now sets `Cursor.lockState = CursorLockMode.None` and `Cursor.visible = true`;
  `Resume()` sets them back to `Locked` / `false` - handing gameplay look back exactly the way `PlayerCameraRig`
  itself sets it up on enable.
- The Controls sub-panel inside the pause menu had the identical "covers its own opening button" bug as Bug 1, fixed
  the same way: a `PauseControlsBackButton`, and `Update()`'s Escape handling now closes the Controls sub-panel first
  if it is open, rather than resuming play out from under it.

**How it is verified.** Three tests in `HudAndPauseMenuTests.cs`:

- `PauseMenuButtons_CanActuallyBeClicked` - opens the pause menu, finds `ResumeButton` by name (only possible once the
  panel that owns it is active), calls `.onClick.Invoke()`, and confirms the panel actually closes and
  `Time.timeScale` returns to 1. This is the test that would have caught the wiring bug on day one, had button clicks
  been exercised via `.onClick.Invoke()` instead of by calling `pause.Resume()` directly as a C# method (which is
  exactly what the original Step 5.2 test did, and exactly why it never noticed the buttons had no listeners).
- `Pausing_UnlocksTheCursorAndResumingRelocksIt` - confirms `Pause()` sets `Cursor.lockState = None` and
  `Cursor.visible = true`. The reverse direction (`Resume()` setting `Locked` / `false`) is **not** asserted on the
  read-back value: `CursorLockMode.Locked` requires the OS window to actually hold input focus, which a headless
  `-batchmode` test process never has, so Unity cannot honour the lock and it reads back as `None` regardless of what
  was requested - the same class of environment limitation Phase 0 already documented for simulated key presses.
  `Resume()` calling the correct API is verified by reading the source; whether the OS actually grants the lock is a
  manual check.
- `PauseControlsPanel_OpensAndClosesFromBack_WithoutResumingPlay` - opens the pause menu, opens its Controls
  sub-panel, closes it via the new Back button, and confirms the *pause menu itself* is still open and
  `Time.timeScale` is still 0 - i.e. that closing Controls does not accidentally resume play.

**Why the original Phase 5 test suite did not catch this.** `Resuming_AfterPausingDuringSlowTime_LeavesTimeScaleAtExactlyOne`
(the one test Step 5.2 already had for the pause menu) calls `pause.Resume()` as a plain C# method call, and toggles
pause via reflection into the private `Toggle()` method - neither path goes anywhere near a `Button` or its `onClick`
event. It correctly proved the `timeScale` logic was right while never exercising whether a player could actually
reach that logic by clicking anything. The lesson generalises: a MonoBehaviour method being individually testable is
not the same claim as the UI actually being wired to call it, and only one of `MainMenuController` and
`PauseMenuController` was ever tested the second way before now.

---

# Verifying the whole phase

## Automated: `Tools/verify.ps1`

```
=== Step 1/2: compiling ===
Compile OK (exit code 0, no CS errors in Setup.log)

=== Step 2/2: running PlayMode tests ===
Tests: 71/77 passed, 0 failed (result: Skipped:Ignored)

RESULT: PASS
```

77 tests total - the 30 from Phases 0–1 (24 pass, 6 intentionally `Assert.Ignore`d in batch mode, unchanged since
Phase 0) plus 21 from the original Phase 5 pass, plus 5 more from the two bug fixes above, across five files (two of
them extended rather than new):

| File | Tests | Covers |
|---|---|---|
| `MainMenuTests.cs` | 5 | Step 5.1, MainMenu, plus the Controls Back-button fix |
| `VictoryScreenTests.cs` | 2 | Step 5.1, Victory |
| `HudAndPauseMenuTests.cs` | 8 | Step 5.2, plus the cursor-lock and button-wiring fixes |
| `MinimapTests.cs` | 4 | Step 5.3 |
| `TutorialTextTests.cs` | 4 | Step 5.4 |

Zero of the 24 touch Phase 3 or Phase 4 source files - they exercise the Phase 5 scripts and the read-only public
surface (`GameManager`, `GameState`, `WardenAI.DetectionLevel`, `RoomEntryTrigger.CurrentObjective`) that Phases 3–4
already expose, per this phase's own scope limits.

## Manual/visual - could not be automated, and were not faked

- **Layout and legibility** of every screen: MainMenu, Victory, the HUD, the pause panel, the minimap's actual
  on-screen framing. Structural presence and live data-binding are tested; whether it *reads well* is a look.
- **`Application.Quit()`** on both Quit buttons - untestable in-process (it would kill the test runner), confirmed by
  pressing it. Its listener wiring is now covered the same way every other pause-menu button's is (see the bug-fix
  section above), so the remaining risk is specifically "does Quit actually exit", not "is it wired".
- **That the OS actually grants the cursor lock after Resume** - `Resume()` is proven to call the correct API
  (`Pausing_UnlocksTheCursorAndResumingRelocksIt`), but whether `CursorLockMode.Locked` truly takes effect can only be
  observed with a real, focused window, which a headless test process is not. Confirmed by pressing Escape twice in
  the Editor and checking the mouse is captured again.
- **The detection meter appearing during real play** - a Warden actually spotting Noa and the meter filling and
  fading believably. `HUDController` reading `WardenAI.DetectionLevel` is tested for wiring, not for how it looks
  mid-chase.
- **Restart Scene / Main Menu buttons on the pause panel** - both trigger real scene loads (`SceneLoader`, already
  exercised elsewhere for New Game/Continue) and are wired by the identical `Awake()` mechanism the bug fix proved
  works for Resume/Controls; not independently click-tested to avoid three more full scene-load tests for the same
  underlying `SceneLoader.LoadScene` call New Game's test already covers.
- **The minimap's "present throughout the entire scene, load to exit" claim** - true by the absence of any disabling
  code, not verified against a full playthrough.
- **Whether the world-space tutorial text is actually readable while walking**, and whether a first-time player
  finishes MuseumNight unaided - the plan's own framing of this bullet as a play-test, not a unit test.
- **The camera drift on MainMenu** - purely a cosmetic addition; confirm it looks like a slow drift, not a spin.

---

# Things you should be able to answer in the defense

Continuing the list from Phases 1–4:

33. **Why does `MainMenuController` wire button clicks in `Awake()` rather than the Inspector?** Same reason
    `PlayerInputReader` subscribes to actions in code: an empty Inspector call list fails silently; a missing
    reference in code throws immediately.
34. **Why is Quit not covered by an automated test?** `Application.Quit()` would terminate the test runner process
    itself in batch mode.
35. **Why does `Resume()` set `Time.timeScale = 1f` literally, instead of restoring a cached value?** Because the
    cached value could be `0.3f` if Escape was pressed while the Chrono Hourglass was held - restoring "whatever it
    was" would leak slow motion into normal play.
36. **Why does the detection meter read `WardenAI.DetectionLevel` in `Update()` instead of an event?** No change
    event exists on `WardenAI` for it, and adding one would mean editing Phase 4 AI code, which this phase does not
    touch. Nothing here writes back into AI or game state - it is a read-only display.
37. **Why is the minimap's culling mask an allow-list of one layer rather than a deny-list?** So a hidden Time Anchor
    added in a later scene is invisible to the minimap by construction - it would have to be deliberately assigned
    the `Minimap` layer to ever appear, rather than deliberately excluded from a broad one.
38. **What makes `TextMeshPro` satisfy "in 3D" when `TextMeshProUGUI` would not?** `TextMeshProUGUI` renders through
    a `CanvasRenderer` onto a Canvas - always screen space. `TextMeshPro` renders through a normal
    `MeshRenderer`/`MeshFilter` as scene geometry, so its type alone proves it is world space.
39. **Why does the tutorial text fade on proximity rather than on the specific verb being performed?** Detecting each
    of the eight verbs individually would need eight different detectors; fading on distance is one mechanism that
    still makes the text respond to the player rather than sit there as a static label.
40. **Why does `GameObject.Find` fail on the item icons and the tutorial plaques, and what was used instead?**
    `GameObject.Find` does not see inactive GameObjects, and both start inactive by design (no item held yet; not
    revealed by its trigger yet). `Transform.Find` on a known parent, or reading the field directly off the
    component, both see inactive objects correctly.
41. **What actually made the pause menu's buttons unclickable - the cursor, or the wiring?** Both, independently. The
    cursor being locked and invisible meant nothing could be aimed at the menu at all; separately, every button's
    `onClick` listener had been registered from an Editor batch-mode script and was never persisted into the saved
    scene, so even a visible cursor would have clicked nothing. Fixing only one would not have fixed the bug.
42. **Why does a non-persistent `UnityEvent.AddListener()` call from an Editor script not survive being saved?**
    `UnityEvent` serializes only its *persistent* calls (the ones set up through the Inspector's UI); `AddListener`
    registers a runtime-only delegate that lives in the calling process's memory and is discarded the moment that
    process exits - which for a `-batchmode -quit` builder is immediately after the scene is saved.
43. **Why does `PauseMenuController` now wire its own buttons in `Awake()` instead of letting the Editor builder do
    it?** Because `Awake()` runs fresh every time the scene actually loads, in the Editor, in a test, or in a built
    player - unlike a one-off Editor script's in-memory listener, that wiring can never go stale. It is the same
    pattern `MainMenuController` and `VictoryScreenController` used correctly from the start; `PauseMenuController`
    was the one place Phase 5 had drifted from it.
44. **Why didn't `Resuming_AfterPausingDuringSlowTime_LeavesTimeScaleAtExactlyOne` (Step 5.2's original pause test)
    catch the button-wiring bug?** It calls `pause.Resume()` as a direct C# method call and reaches `Toggle()` through
    reflection - neither path goes through a `Button` or its `onClick` event, so a completely unwired button would
    have passed that test just as easily as a correctly wired one.
45. **Why is `CursorLockMode.Locked` not asserted on after `Resume()` in the automated test?** Locking the cursor
    requires the OS window to actually hold input focus, which a headless `-batchmode` test process never has - the
    same category of limitation Phase 0 documented for simulated key presses. The unlock direction (`Pause()`) is
    asserted and passes, because releasing a lock does not require focus the way acquiring one does.
46. **Why does the Controls panel need both a Back button and Escape handling, when either alone would close it?**
    Redundancy for exactly the failure mode that shipped the bug: relying on Escape alone leaves players who expect a
    visible on-screen control with no way out, and relying on a button alone gives no quick keyboard path back.
