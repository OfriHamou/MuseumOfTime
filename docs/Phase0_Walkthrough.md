# Phase 0 Walkthrough — Unblock and clean up

**What this is.** A reproduction guide for Phase 0. Everything I did, in the order a person would do it by hand in
Unity, plus the reasoning behind each decision. If you deleted the whole project and started again from commit
`8aa881a`, following this document would get you back to where we are now.

**Branch:** `phase-0-cleanup` · **Commits:** `25bbc57` (safety snapshot) → `986ead4` (the work) → `90a9765` (doc)

---

## Part A — What was wrong before

Four defects, all found by reading the project rather than guessing:

| # | Defect | How you would spot it yourself |
|---|---|---|
| B1 | Two classes named `PlayerInputReader` → the project does not compile | Console shows a red **CS0101** error. Search `PlayerInputReader` in the Project window with scope **In Project** — two results appear |
| B2 | Project-wide input actions pointed at Unity's throwaway template, not your asset | **Edit → Project Settings → Input System Package** — the asset field reads `InputSystem_Actions` |
| B3 | Build list referenced `SampleScene.unity`, a file that no longer exists | **File → Build Profiles → Scene List** — first row, checkbox off, and the file is missing from `Assets/Scenes` |
| B4 | Real scripts lived in a nested `Assets/Assets/Scripts/Core/`, while the correct empty folders sat unused at `Assets/Scripts/` | Project window — you have two script trees |

A fifth, `PlayerController` moving in world space instead of camera-relative, is **deliberately left for Step 1.1** —
it is a gameplay bug, not a blocker.

---

## Part B — Reproducing the work, step by step

### B.0 — Take a rollback point first

```bash
git checkout -b phase-0-cleanup
git add -A
git commit -m "Checkpoint before Phase 0 cleanup"
```

Do this **before** any deletion. The two `PlayerInputReader` files were untracked, so without this commit,
deleting one would have been unrecoverable — there would be no copy anywhere.

---

### B.1 — Remove the duplicate input reader

**Find the collision.** Project window → search box → type `PlayerInputReader` → set the scope dropdown (just under
the search box) to **In Project**. Two results is the bug.

**Decide which to keep — do not guess.** Scripts are referenced by **GUID**, stored in the `.meta` file beside each
script, never by filename or path. So find out which GUID your scenes actually use:

```bash
# GUID of each candidate
grep guid Assets/Assets/Scripts/Core/PlayerInputReader.cs.meta   # 7a5325f7...
grep guid Assets/Scripts/Player/PlayerInputReader.cs.meta        # f20353bc...

# which GUIDs the scene references
grep -o "guid: [a-f0-9]\{32\}" Assets/Scenes/MuseumNight.unity | sort | uniq -c
```

`MuseumNight.unity` referenced **`f20353bc…`** — the `Assets/Scripts/Player/` copy. Then confirm the other one is
referenced by nothing at all:

```bash
grep -rl "7a5325f76befb894597fab1a0ba4af3b" Assets ProjectSettings
# no output = safe to delete
```

**Delete it.** Project window → right-click `Assets/Assets/Scripts/Core/PlayerInputReader.cs` → **Delete**.
Unity removes the `.meta` alongside it.

**Extend the survivor.** Open `Assets/Scripts/Player/PlayerInputReader.cs` and add three actions the GDD control
table needs but the script had no handlers for — `EraBack` (Q), `EraForward` (R), `Journal` (Tab) — each following
the existing edge-triggered pattern, and each cleared in `LateUpdate`. Also add:

```csharp
private void OnDisable()
{
    moveInput = Vector2.zero;   // otherwise a held key sticks when the
    lookInput = Vector2.zero;   // window loses focus and Noa walks forever
    isRunning = false;
    isSlowTimeHeld = false;
}
```

**Where to see it in Unity:** open `MuseumNight`, select the **Player** object in the Hierarchy. The
*Player Input Reader* component in the Inspector shows *Move Input*, *Look Input*, *Is Running*, *Is Slow Time Held*.
Enter Play Mode and hold `W` — *Move Input* changes to roughly `(0, 1)`.

---

### B.2 — Extend the Input Actions asset

Double-click `Assets/Input/MuseumInputActions.inputactions`. The **Input Actions** window opens with three panels:
*Action Maps* (left), *Actions* (middle), *Properties* (right).

**Add the three missing actions.** With the **Player** map selected, click **+** at the top of the *Actions* panel →
rename the new action → select its child binding → in *Properties* click the **Path** dropdown → **Listen** → press
the key → it fills in.

| Action | Key | Path |
|---|---|---|
| `EraBack` | Q | `<Keyboard>/q` |
| `EraForward` | R | `<Keyboard>/r` |
| `Journal` | Tab | `<Keyboard>/tab` |

⚠️ **Click *Save Asset* (top-left of the window).** This asset does **not** auto-save. Closing without saving loses
everything.

**Remove the dead binding.** `Move` had a binding with an empty path — created by clicking **+** and never assigning
a key. It does nothing, but it is visible clutter in a defense. Select it → **−**.

**Add a UI action map.** Click **+** at the top of the *Action Maps* panel, name it `UI`, and give it
`Navigate`, `Submit`, `Cancel`, `Point`, `Click`. (I copied Unity's stock UI map wholesale from the template asset,
which is faster and known-good.)

**Verify — the Player map should now list exactly 12 actions:**

```
Move, Look, Jump, Run, Interact, Shoot,
SlowTime, CameraToggle, Pause, EraBack, EraForward, Journal
```

That matches the GDD control table one-for-one.

**Two things that were already correct** — worth knowing so you can say so in the defense:

- **Edit → Project Settings → Player → Other Settings → Active Input Handling** already read
  **Input System Package (New)**, not *Both*. This is the literal wording of the "only the new Input System"
  requirement, and an examiner can check it in one click.
- No `Input.GetKey`, `Input.GetAxis` or `Input.mousePosition` anywhere in the project. Verify with
  `grep -rn "Input\.Get" Assets --include=*.cs` — no output.

---

### B.3 — Move the scripts into the right folders

The correct folders already existed and were empty. The real code was one level too deep.

| From | To |
|---|---|
| `Assets/Assets/Scripts/Core/GameManager.cs` | `Assets/Scripts/Core/` |
| `Assets/Assets/Scripts/Core/GameState.cs` | `Assets/Scripts/Core/` |
| `Assets/Assets/Scripts/Core/GameStateDebugTester.cs` | `Assets/Scripts/Core/` |
| `Assets/Assets/Scripts/Core/SceneLoader.cs` | `Assets/Scripts/Core/` |
| `Assets/Assets/Scripts/Core/PlayerController.cs` | `Assets/Scripts/Player/` |
| `Assets/Assets/Prefabs/Core/GameManager.prefab` | `Assets/Prefabs/Core/` |

**The safe way: drag inside Unity's Project window.** Unity moves the `.meta` with the file automatically, so the
GUID is preserved and every reference keeps working.

**The unsafe way: dragging in Windows Explorer** and leaving the `.meta` behind. Unity then treats the file as brand
new, generates a *fresh* GUID, and every scene and prefab that referenced the old GUID shows
**"The associated script can not be loaded"** — a missing-script slot with all its Inspector values gone.

I did the equivalent of the safe way from the command line by moving each file **and its `.meta` together**:

```bash
git mv Assets/Assets/Scripts/Core/GameManager.cs      Assets/Scripts/Core/GameManager.cs
git mv Assets/Assets/Scripts/Core/GameManager.cs.meta Assets/Scripts/Core/GameManager.cs.meta
```

**Proof it worked:** `git status` shows all ten as `R` (rename), not `D` + `A`. And the GUIDs are unchanged:

| Script | GUID |
|---|---|
| `GameManager.cs` | `2625eb673e2886c42941f0ee67b1d043` |
| `PlayerController.cs` | `9830bb017d998314d857f3674e1feff9` |
| `PlayerInputReader.cs` | `f20353bc68551084ca9c304cbadad881` |

Finally, delete the now-empty `Assets/Assets/` tree.

**Where to see it in Unity:** the Project window should show exactly one `Assets/Scripts/` tree with `AI`, `Camera`,
`Core`, `Interaction`, `Player`, `UI`, `World` — and no nested `Assets/Assets`.

---

### B.4 — Version control

`.gitignore` was already the correct Unity template, and `Library/` was already untracked — confirm with
`git ls-files | grep -c "^Library/"` returning `0`. This matters: `Library/` alone can exceed the 300 MB submission
cap.

Work is committed on `phase-0-cleanup` and pushed to `github.com/OfriHamou/MuseumOfTime`. That repository is what
the GDD's source-download link will point at.

---

## Part C — The reasoning, in full

These are the five questions I asked, answered. They are also the kind of question a source-code defense asks.

### 1. Why did two files with the same class name break the build, in different folders?

Because **Unity ignores folders when compiling.** Every `.cs` file under `Assets/` is compiled into a single
assembly, `Assembly-CSharp.dll`, unless you explicitly create Assembly Definition files. Neither script declared a
`namespace`, so both landed in the global namespace of that one assembly — a straight redefinition, error
**CS0101**. Folder structure in Unity is for humans; it means nothing to the compiler.

### 2. What is a `.meta` file for, and what breaks without it?

Every asset gets a `.meta` file holding a permanent **GUID** plus its import settings. Scenes and prefabs store
references as that GUID — never as a file path. That is why you can rename and move assets freely and nothing
breaks.

Move a script in Explorer without its `.meta`, and Unity sees an unknown file, mints a **new** GUID, and every
reference to the old one dangles. In the Inspector that appears as a component whose script slot says
**"Missing (Mono Script)"**, with every value you had set on it gone. Re-assigning the script gives you an empty
component — the serialized data is not recoverable.

### 3. Why add a UI action map before making the asset project-wide?

Unity's `InputSystemUIInputModule` — the component on the EventSystem that makes buttons clickable — reads its
`Navigate`, `Submit`, `Cancel`, `Point` and `Click` actions from the **project-wide** actions asset. The template
asset we are replacing has a UI map; ours did not. Switch the pointer over first and menus go dead: buttons stop
highlighting and stop responding to clicks, with no error in the Console to explain why.

### 4. Why clear `jumpPressed` in `LateUpdate` rather than `Update`?

Unity runs **every** `Update` on every object, and only then runs the `LateUpdate` pass. If the flag were cleared in
`Update`, whether a given script saw the jump would depend on script execution order — `PlayerController` might run
before the reader (sees it) or after (misses it), and that ordering can change silently.

Clearing in `LateUpdate` guarantees the flag stays true for the whole `Update` pass, so **every** script polling it
that frame sees the same value, exactly once per key press. This is the standard way to expose one-shot input.

### 5. What did I check before deleting, and what would have broken if I got it wrong?

I compared the GUID of each candidate against the GUIDs actually referenced in `MuseumNight.unity`, then searched
the entire project for the loser's GUID to confirm it appeared nowhere.

Had I deleted the referenced one instead, the **Player** object in `MuseumNight` would have lost its
*Player Input Reader* component to a missing-script slot. Worse, that also breaks the **PlayerInput** component's
event wiring: its Unity Events point at methods on a component that no longer resolves, so every binding
(`OnMove`, `OnJump`, …) would silently stop firing — and Unity does not always warn about it.

---

## Part D — Verification checklist

Work through this in Unity. Every line should be true before Phase 1.

- [ ] **Console is clear of red errors.** *Window → General → Console*, click *Clear*, then let it recompile.
- [ ] **One `PlayerInputReader` in the project.** Search it with scope *In Project* — exactly one result.
- [ ] **Player map has 12 actions.** Double-click `MuseumInputActions`, select the *Player* map.
- [ ] **Both action maps exist** — `Player` and `UI`.
- [ ] **`Assets/Assets/` is gone.** The Project window shows one `Scripts` tree.
- [ ] **No missing scripts.** Open `MuseumNight`, select **Player** — every component resolves, none says
      *Missing (Mono Script)*.
- [ ] **Play Mode input works.** Press Play, select Player, hold `W` — *Move Input* reads about `(0, 1)`.
      Press `Q`, `R`, `Tab` — the Console logs *Era Back*, *Era Forward*, *Journal*.
- [ ] **Active Input Handling** is *Input System Package (New)* — *Project Settings → Player → Other Settings*.

### Still outstanding — your three clicks

- [ ] **Project-wide Actions → `MuseumInputActions`.** *Edit → Project Settings → Input System Package*.
      Currently still points at the template (`guid: 052faaac…`); it should read `95acc8fd…`.
- [ ] **Delete `Assets/InputSystem_Actions.inputactions`** — after the step above, not before.
- [ ] **Remove `SampleScene`** from *File → Build Profiles → Scene List*. The list should end as
      `MainMenu, MuseumNight, FrozenCity, ClockCore, Victory`.

---

## Part E — What Phase 0 did and did not close

**Closed:** nothing outright. Phase 0 is groundwork.

**Advanced:** the "only the new Input System" requirement is now all but complete — the actions asset is correct and
no legacy input calls exist. It closes the moment you assign the project-wide asset.

**Set up for later:** the folder layout exists so that Phase 4's AI scripts, Phase 3's systems and Phase 5's UI each
have a home, and so you can find any script in seconds during the defense — where you must add and remove elements
live.

**Deliberately not touched:** `PlayerController`'s world-space movement bug. That is Step 1.1, the first thing in
Phase 1.
