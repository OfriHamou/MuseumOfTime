# Museum of Time — Final Project Implementation Plan

**Course:** Game Development (פיתוח משחקים), Year 3 — Final Project 2026  
**Student:** עפרי חמו — 211906813  
**Engine:** Unity 6000.4.8f1, URP 17.4, Input System 1.19, Cinemachine 3.1.7, AI Navigation 2.0.12, ProBuilder 6.0.9  
**Repository:** `C:\Dev\Agent-Agent\MuseumOfTime`

This document replaces the earlier *Final Project Implementation Guide*. That version was written without access to the
assignment brief and used an invented requirement list (R1–R15). This version is built from the two real sources:
the assignment brief (`2026 פיתוח משחקים עבודת סיום.docx`) and the game design document (`MuseumofTime GDD.pdf`).

Every step below states which **numbered assignment requirement** it satisfies and how to prove it. Nothing here is
generic best practice for its own sake — if a step does not serve either the graded requirement list or the game we
actually designed, it is not in this document.

---

## How to use this document

1. Work top to bottom. Phases are ordered by dependency, not by preference.
2. A step is **not done** when the script exists. It is done when its *Verification* test passes.
3. Each time a step closes, tick it in **Part 8 — Compliance Matrix** and capture a screenshot or clip for evidence.
   That evidence becomes the GDD's per-scene requirement pages, which the brief mandates.
4. Requirement codes are used throughout: `T#` = graded technical element, `S#` = submission rule,
   `G#` = general points, `D#` = defense rule. They are defined in Part 2.

---

# Part 1 — What we are building

## 1.1 The concept, as designed

Noa (נועה), a young night guard at the Museum of Time, discovers that every exhibit is a preserved real moment from
history. Someone has stolen **the First Moment** (הרגע הראשון), the anchor that holds the timeline in order. History
begins to come apart: eras bleed into each other, figures from different centuries appear in the wrong places, and
whole exhibits vanish. Noa is the only person who still remembers the original timeline. She enters exhibits, repairs
broken moments, collects **Time Shards** (שברי זמן), and uncovers who is rewriting history — **the Collector** (האספן),
a former museum employee trying to bring back someone he lost.

The player does not win by force. They win by understanding cause and effect — by seeing how a small action in the past
changes an entire future.

## 1.2 Core pillars (these must survive every scope cut)

| Pillar | What it means in the build |
|---|---|
| **Time is the mechanic, not the theme** | Past / Present / Future are playable states, switched at will, and puzzles only solve across them |
| **Understanding over combat** | Noa has no weapon that kills. Her one projectile freezes and rewinds, it does not destroy |
| **Consequence** | Fixing the present can break the future. At least one puzzle must punish a naive solution |
| **The museum remembers** | Objects, doors and NPCs carry state between eras and between scenes |

## 1.3 Scope decisions — GDD as designed vs. GDD as built

The GDD describes **7 scenes and 3 endings**. The assignment mandates **exactly 3 gameplay scenes plus menus**, with a
coherent logical link between all of them. The Unity project has already collapsed to five scene files
(`MainMenu`, `MuseumNight`, `FrozenCity`, `ClockCore`, `Victory`) — that decision is correct and this plan formalises it.

### Scenes we build

| Build scene | GDD source | Role |
|---|---|---|
| `MainMenu` | — | Entry menu (T1) |
| **`MuseumNight`** — *The Last Shift* (המשמרת האחרונה) | GDD Scene 1 | Tutorial, museum interior, two floors, the Clock of Creation shatters |
| **`FrozenCity`** — *The City That Froze Before Sunset* (העיר שקפאה לפני השקיעה) | GDD Scene 2 | Outdoor era, clock tower, stealth, the first Time Anchors |
| **`ClockCore`** — *The Inverted Museum & the Collector* | GDD Scenes 6 + 7, merged | All mechanics combined, boss confrontation across three eras |
| `Victory` | GDD Ending 1 | Victory screen (T1) |

### Cut, and why

| Cut | Reason | Where its idea survives |
|---|---|---|
| GDD Scene 3 — The Library That Did Not Burn | Exceeds the 3-scene limit | Its "let history happen" dilemma moves into ClockCore's final choice |
| GDD Scene 4 — Tomorrow's Laboratory | Exceeds the 3-scene limit | Its accelerated-time pressure becomes the Future state in ClockCore |
| GDD Scene 5 — The Hall of Silent War | Exceeds the 3-scene limit | Its frozen-projectile image becomes a FrozenCity set-piece |
| **Time Shadow** (צל זמן) — the replaying duplicate of Noa | Highest-cost, lowest-grade-yield feature in the GDD; needs recorded playback, a second animated rig and its own puzzle class | **Deferred.** Only build it if Phases 0–6 close early. The `F` binding stays reserved |
| Three endings | Two extra ending scenes buy no requirement points and split testing effort | Ship Ending 1 (*Time Returns to Its Course*) as canonical. Store the player's final choice in `GameState` so Ending 2 can be added later as a text variant |
| Full three-era worlds in every scene | Three complete dressings × three scenes is not achievable | Era coverage is staged — see 1.4 |

### Retained enemy roster

The GDD's four antagonists collapse to the two the requirements actually reward (T13 needs exactly two agent types on
different routes), plus a scripted boss:

- **Time Wardens** (שומרי הזמן) — ground agent. Patrols with pauses, sees in a cone, chases. *Agent type A.*
- **Chronological Shadows** (הצללים הכרונולוגיים) — drifting agent. Ignores walls the Wardens respect, is drawn to Time
  Shards and steals them. *Agent type B, on its own NavMesh bake.*
- **The Collector** (האספן) — scripted boss in ClockCore, not a NavMesh agent.
- **The Restorer** (המשחזר) — *folded into the Collector*. His "undo the player's actions" behaviour becomes the boss's
  phase-two mechanic rather than a separate enemy.

### Era coverage per scene

| Scene | Past | Present | Future |
|---|---|---|---|
| MuseumNight | — | ✅ full | — |
| FrozenCity | ✅ full | ✅ full | ✅ full |
| ClockCore | ✅ arena phase 1 | ✅ arena phase 2 | ✅ arena phase 3 |

MuseumNight is deliberately single-era: it teaches movement, interaction, cameras and stealth without also teaching time
switching. The `Q`/`R` era controls unlock at the end of MuseumNight when the Clock of Creation breaks.

## 1.4 Items and progression

| Item | GDD name | Acquired | Needed for | Requirement |
|---|---|---|---|---|
| **Time Lens** | מצפן זמן (Time Compass) | End of MuseumNight | Reveals hidden Time Anchors and time cracks in FrozenCity | T9 acquired item #1 |
| **Chrono Hourglass** | שעון חול | Mid FrozenCity | Slow-time ability, required to survive ClockCore | T9 acquired item #2 |
| **Time Shards** | שברי זמן | All scenes | Score + gate to the final confrontation | T8, T9 |

Both items persist through `GameState` and are **required** by a later scene — that is what makes the three scenes one
game rather than three demos, and it is what satisfies the brief's "coherent logical connection" rule (S9).

## 1.5 Controls — final binding table

Derived from the GDD control table, extended with what the requirements force us to add.

| Action | Binding | Source |
|---|---|---|
| Move | `WASD` | GDD |
| Run | `Shift` | GDD |
| Jump | `Space` | GDD |
| Look | Mouse | GDD |
| Interact | `E` | GDD |
| Era switch back / forward | `Q` / `R` | GDD |
| Slow time (Chrono Hourglass) | `Ctrl` | GDD |
| Time Journal | `Tab` | GDD |
| Pause | `Escape` | GDD |
| **Throw Chrono Orb** | `Left Mouse` | **Added for T15** — GDD had no projectile |
| **Camera FPS ⇄ third person** | `C` | **Added for T19** — GDD specified third person only |
| Time Shadow | `F` | GDD — *reserved, deferred* |

> **Design note on the two additions.** Neither breaks the fiction. The **Chrono Orb** is a physical glass sphere Noa
> throws to freeze or rewind an object — it is the GDD's "returning small objects backwards in time" made into a
> throwable body, which is exactly what T15 asks for. The **first-person camera** is Noa raising the Time Lens to her
> eye to read an exhibit closely, which is how the tutorial plaques and time cracks are read. Both should be presented
> in the defense as design, not as requirement-chasing.

---

# Part 2 — Assignment requirement register

Extracted directly from the brief. This is the canonical numbering used everywhere else in this document.

## 2.1 Graded technical elements (T)

| # | Requirement (as written) | Plain reading |
|---|---|---|
| **T1** | תפריטי כניסה וניצחון | Entry menu and victory menu |
| **T2** | הדרכה בתוך המשחק, כתובות דינמיות, והנחיות ברורות (בתלת מימד) | In-game tutorial with dynamic text and clear instructions, rendered in 3D |
| **T3** | לפחות 4 טריגרים | At least 4 triggers |
| **T4** | לפחות זיהוי והפעלה של 3 Collision | At least 3 collisions detected and acted upon |
| **T5** | שימוש בצירים פיזיקליים (Hinge) | Physical hinge joints |
| **T6** | Terrain בבניה עצמית | Self-built Terrain |
| **T7** | פטרול עם השהיה | Patrol **with pause** |
| **T8** | צבירת/איבוד ניקוד או תמריץ של חיים ואנרגיה | Score gain/loss, or a lives/energy incentive |
| **T9** | העברות פרמטרים בין סצנה לסצנה כולל לפחות שני אלמנטים נרכשים (Serialize) | Parameter passing between scenes, including **at least two acquired items**, serialized |
| **T10** | שני Assets שפורק על ידכם בעזרת וורונוי (דוגמה פיצוץ) או בבניה עצמית, שיופיע אינהרנטי במשחק | **Two** assets fractured by you via Voronoi (e.g. an explosion) or self-built, appearing intrinsically in the game |
| **T11** | שני Assets שונים שעבר תהליכי הורדת פוליגונים על ידכם, ושולב כ-LOD ביוניטי | **Two different** assets you reduced polygons on yourself, integrated as LOD in Unity |
| **T12** | יישום רק Input System החדש | **Only** the New Input System |
| **T13** | אלמנט אחד ברור של AI ניהוג (Steering) — חיפוש, בריחה, מרדף ו/או Pathfinding עם לפחות שני סוגי סוכנים שנעים במסלולים שונים (bake נפרד) | One clear Steering element (seek / flee / pursue) and/or Pathfinding, with **two agent types on different routes, separate bake** |
| **T14** | הגדרת Animator (לא מיבוא) עם לפחות 4 מצבים | Animator **you defined** (not imported) with at least 4 states |
| **T15** | ירייה, פגיעה של גוף פיזיקלי (כדור לדוגמה) | Shooting — impact of a physical body (a ball, for example) |
| **T16** | שימוש ב-Recast עם Stealth | Recast navmesh together with a stealth mechanic |
| **T17** | שימוש בהגדרת שכבות בקוד (LayerMask) מול Recast | LayerMask defined **in code**, working against Recast |
| **T18** | מיני מפה פשוטה, אוריינטציה לאורך כל סצנה אחת לפחות | Simple minimap giving orientation, present throughout **at least one whole scene** |
| **T19** | שינוי זווית צילום, מעבר ממצלמה גוף ראשון (FPS) לגוף שלישי, אפשר עם CineMachine (שתי מצלמות מלבד Minimap) | Camera angle change, first person → third person, optionally Cinemachine. **Two cameras** besides the minimap |
| **T20** | בניית בית מעוצב עם שתי קומות בעזרת Texture בבחירה אישית וטיפוס במדרגות | A designed **two-storey** building with your own chosen textures, and stair climbing |
| **T21** | Teleport: החל מהסצנה השניה, לפחות שני Teleport סמויים; שחקן שנכשל חוזר ל-Teleport (ולא לתחילת המשחק) תוך עדכון מחודש לאורך חיים ואולי ניקוד | From **scene 2 onward**, at least **two hidden** teleports. On failure the player returns to the teleport, **not** to the start, with health (and possibly score) refreshed |

## 2.2 Submission rules (S)

| # | Rule |
|---|---|
| **S1** | Compressed deliverable must not exceed **300 MB** |
| **S2** | Build + GDD must be uploaded to Moodle, and the packaging must be verified to work (**−3 penalty** if it does not) |
| **S3** | GDD must be a **PowerPoint**, containing a link to the trailer on **YouTube** |
| **S4** | Trailer: **maximum 1:15**, marketing quality, not a careless screen recording |
| **S5** | The game **EXE** is uploaded together with the GDD |
| **S6** | If source does not fit in Moodle, upload it to a **repository** and put the download link in the GDD |
| **S7** | **GDD page 1** — participant names and **Known Bugs** |
| **S8** | **GDD page 2 onward** — for **each scene**, state which elements and requirements appear in that scene |
| **S9** | Three gameplay scenes plus menus, with a **coherent logical connection** between all scenes |
| **S10** | Scale and realism appropriate to the environment (the brief's example: foxes and zombies must not float above the ground or be the size of a house) |

## 2.3 General points (G)

| # | Worth | Rule |
|---|---|---|
| **G1** | up to 5 pts | How interesting the game is, as a central element — and ambition ("ראש גדול") |
| **G2** | up to 5 pts | An interesting short trailer, up to a minute |

The brief states plainly that the game is judged **first and foremost on how interesting it is**, and that unlike the
homework — which was scored on an eclectic collection of requirements — the final project must be precise and coherent.
Treat G1 as the highest-value line item in the whole rubric.

## 2.4 Defense (D)

| # | Rule |
|---|---|
| **D1** | Defense over Zoom, with both developers if there are two. *(This is a solo project.)* |
| **D2** | The game must run on the examinee's own machine during the defense — **5 points** |
| **D3** | The defense is **on the source code** |
| **D4** | One-on-one; must know every side of the game |
| **D5** | Must be able to **add and remove basic elements live** during the defense |
| **D6** | Self-scheduling sheet published a week before the formal date; defense spread over the 3–4 days before it |

## 2.5 Weight warning from the brief

> "המשחק נשפט על פי משקלו — מאבק מתמיד בין אופטימיזצית מקום, ביצועים, ועניין."

The game is judged on its weight — a constant fight between size optimisation, performance, and interest. The 300 MB
cap is not an afterthought; it is stated as a judging axis. Budget it from day one (see Step 8.1).

---

# Part 3 — Requirement → scene placement

This table is the spine of the project. It is also, almost verbatim, the content the brief demands on GDD page 2 onward (S8).

| Req | MuseumNight | FrozenCity | ClockCore | Menus |
|---|---|---|---|---|
| T1 Menus | | | | ✅ Main + Victory |
| T2 3D tutorial text | ✅ exhibit plaques teach every verb | ✅ era-switch prompts | ✅ boss phase callouts | |
| T3 ≥4 triggers | ✅ hall entry, exhibit proximity, stairwell, blackout | ✅ era zones, tower entry | ✅ arena phase triggers | |
| T4 ≥3 collisions | ✅ orb→display case, player→shard, player→hazard | ✅ orb→bell, shard pickup | ✅ orb→Collector shield | |
| T5 Hinge | ✅ Clock of Creation pendulum, gallery gate | ✅ clock-tower bell + drawbridge | ✅ inverted swinging exhibits | |
| T6 Terrain | | ✅ **self-sculpted city outskirts** | | |
| T7 Patrol + pause | ✅ one Warden, scripted route | ✅ full patrol network | ✅ Wardens in phase 2 | |
| T8 Score / health / energy | ✅ | ✅ | ✅ | ✅ shown on HUD |
| T9 Cross-scene + 2 items | ✅ **grants Time Lens** | ✅ needs Lens, **grants Hourglass** | ✅ needs Hourglass | |
| T10 Voronoi ×2 | ✅ **#1 Clock of Creation shatters** | ✅ **#2 frozen statue shatters** | ✅ reuse both | |
| T11 LOD ×2 | ✅ **#1 marble statue**, **#2 stone column** | ✅ reuse both | ✅ reuse both | |
| T12 New Input System only | ✅ | ✅ | ✅ | ✅ |
| T13 2 agent types, separate bake | ✅ Warden only (teach) | ✅ **Warden + Shadow, separate bakes, different routes** | ✅ both | |
| T14 Animator ≥4 states | ✅ **Noa: Idle/Walk/Run/Jump** | ✅ **Warden: Patrol/Alert/Chase/Attack** | ✅ both | |
| T15 Physical shooting | ✅ taught on a display case | ✅ ring the tower bell | ✅ break the Collector's shield | |
| T16 Recast + stealth | ✅ introduced | ✅ **core loop of the scene** | ✅ phase 2 | |
| T17 LayerMask in code | ✅ vision raycast mask | ✅ hide-volume mask + NavMesh areas | ✅ | |
| T18 Minimap | ✅ **full-scene minimap here** | ✅ (optional) | ✅ (optional) | |
| T19 FPS ⇄ 3rd person | ✅ **both cameras, toggle taught** | ✅ | ✅ | |
| T20 Two-storey + stairs | ✅ **the museum itself is the two-storey building** | | | |
| T21 Hidden teleports | — *(not allowed before scene 2)* | ✅ **≥2 hidden Time Anchors** | ✅ ≥2 more | |
| S9 Coherent link | Lens out → | Lens in, Hourglass out → | Hourglass in | |
| S10 Scale realism | ✅ pass required | ✅ pass required | ✅ pass required | |

**Two placement rules that are easy to get wrong:**

- **T21 explicitly says "from the second scene onward."** Do not put hidden teleports in MuseumNight. MuseumNight uses a
  plain respawn; FrozenCity and ClockCore use Time Anchors.
- **T18 says "throughout at least one whole scene."** Pick MuseumNight and guarantee the minimap is live from the first
  frame to the last. Partial coverage in three scenes does not satisfy it; full coverage in one does.

---

# Part 4 — Current project status (verified)

Established by reading the repository, not assumed.

## 4.1 What exists and works

| Item | State |
|---|---|
| `GameState.cs` | ✅ Solid. Health, energy, score, Time Shards, detection/death counters, `hasTimeLens`, `hasChronoHourglass`, checkpoint scene + position. `[System.Serializable]`, with `ResetToDefaults()` and `ClampValues()` |
| `GameManager.cs` | ✅ Singleton, `DontDestroyOnLoad`, `StateChanged` / `PlayerDied` events, damage/heal/score API, unscaled playtime accumulation |
| `SceneLoader.cs` | ✅ Present |
| `GameStateDebugTester.cs` | ✅ Present |
| `MuseumInputActions.inputactions` | ✅ Player map with Move, Look, Jump, Run, Interact, Shoot, SlowTime, CameraToggle, Pause |
| Scene files | ✅ MainMenu, MuseumNight, FrozenCity, ClockCore, Victory — all in Build Settings and enabled |
| Packages | ✅ Input System, Cinemachine 3.1.7, AI Navigation 2.0.12, ProBuilder 6.0.9, Terrain module, URP 17.4 |

`GameState` already anticipates T9 (`hasTimeLens`, `hasChronoHourglass`), T8 (score/health/energy) and T21
(`hasCheckpoint`, `checkpointSceneName`, `checkpointPosition`). The foundation is genuinely good — do not rewrite it.

## 4.2 Blockers and defects — fix these before anything else

| # | Problem | Evidence |
|---|---|---|
| **B1** | **Two classes named `PlayerInputReader` in the global namespace** → `CS0101`, the project does not compile | `Assets/Assets/Scripts/Core/PlayerInputReader.cs` and `Assets/Scripts/Player/PlayerInputReader.cs` |
| **B2** | Project-wide Input Actions asset is the **Unity template** `InputSystem_Actions` (guid `052faaac…`), not `MuseumInputActions` (guid `95acc8fd…`) | `ProjectSettings/EditorBuildSettings.asset` → `com.unity.input.settings.actions` |
| **B3** | Build Settings references `Assets/Scenes/SampleScene.unity`, which no longer exists | Stale entry, `enabled: 0` |
| **B4** | Scripts live under a nested `Assets/Assets/Scripts/…` while a second tree exists at `Assets/Scripts/…` | Two parallel script roots invite exactly the duplicate-class error in B1 |
| **B5** | `PlayerController.HandleMovement` moves in **world space**, not camera-relative | `new Vector3(input.x, 0f, input.y)` ignores camera yaw — will feel broken the moment mouse look exists |
| **B6** | The `Core` copy of `PlayerInputReader` only `Debug.Log`s Jump / Interact / Shoot / CameraToggle / Pause | The `Player` copy is the better implementation: edge-triggered flags cleared in `LateUpdate` |

## 4.3 Honest completion estimate against the real requirements

| | Count |
|---|---|
| Requirements substantially built | **3** of 21 (T8, T9 partially, T12 partially) |
| Requirements with scaffolding only | 3 (T1, T13, T19) |
| Requirements not started | **15** |
| Submission items not started | 10 of 10 |

The project is at the end of its foundation phase, not the middle of production. Plan accordingly.

---

# Part 5 — The build plan

---

## Phase 0 — Unblock and clean up

> Nothing else in this document can be tested until the project compiles. Do Phase 0 in one sitting.

### Step 0.1 — Resolve the duplicate `PlayerInputReader`

**Goal.** One input reader class, in one place, that the whole project uses.

**What to do**
- Keep `Assets/Scripts/Player/PlayerInputReader.cs` — it is the stronger implementation (edge-triggered
  `JumpPressed` / `InteractPressed` / `ShootPressed` / `CameraTogglePressed` / `PausePressed`, all cleared in `LateUpdate`).
- Delete `Assets/Assets/Scripts/Core/PlayerInputReader.cs` **and its `.meta`**.
- Add `public bool EraForwardPressed`, `EraBackPressed` and `JournalPressed` for the GDD's `R` / `Q` / `Tab` actions.
- Add matching `OnEraForward`, `OnEraBack`, `OnJournal` handlers.
- Confirm the Player GameObject carries exactly one `PlayerInputReader` component.

**Deliverable.** A single `PlayerInputReader` with all eleven actions from the Part 1.5 control table.

**Requirements.** T12.

**Verification.** Console is empty of errors. In Play Mode, `Move Input` reads ≈`(0,1)` on `W`; every button action logs exactly once per press.

---

### Step 0.2 — Consolidate the Input Actions asset

**Goal.** One actions asset, referenced everywhere, with no template leftovers.

**What to do**
- Add `EraForward` (`R`), `EraBack` (`Q`) and `Journal` (`Tab`) to the `Player` map in `MuseumInputActions`.
- Add a `UI` map (Navigate, Submit, Cancel, Point, Click) so menus do not fall back to the old input manager.
- **Project Settings → Input System Package → Project-wide Actions**: point it at `MuseumInputActions`.
- Delete `Assets/InputSystem_Actions.inputactions` and its `.meta`.
- **Project Settings → Player → Active Input Handling** must read **Input System Package (New)** — not *Both*.
  This is the literal wording of T12 ("only the new Input System") and a grader can check it in one click.
- Grep the project for `Input.GetKey`, `Input.GetAxis`, `Input.mousePosition` and remove every hit.

**Deliverable.** `MuseumInputActions` as the single source of input; template asset gone.

**Requirements.** **T12 (fully satisfied by this step).**

**Verification.** Setting *Active Input Handling* to New alone leaves the game fully playable. Project-wide search for `Input.Get` returns zero results.

---

### Step 0.3 — Repair project structure and Build Settings

**Goal.** A layout that a grader — and you, in the defense — can navigate instantly.

**What to do**
- Collapse the two script roots into one: `Assets/Scripts/` with subfolders `Core`, `Player`, `AI`, `Time`, `Interaction`, `UI`, `World`.
  Move files **inside the Unity Editor** so `.meta` GUIDs and scene references survive.
- Delete the now-empty `Assets/Assets/` tree.
- Remove the stale `SampleScene` entry from Build Settings.
- Confirm build order: `MainMenu (0) → MuseumNight (1) → FrozenCity (2) → ClockCore (3) → Victory (4)`.
- Create the remaining folders: `Prefabs`, `Materials`, `Models`, `Audio`, `VFX`, `Terrain`, `Animation`.
- In every scene, create empty parents: `--- MANAGERS ---`, `--- ENVIRONMENT ---`, `--- GAMEPLAY ---`, `--- CHARACTERS ---`, `--- UI ---`, `--- CAMERAS ---`.

**Deliverable.** One script root, clean Build Settings, consistent hierarchy in all five scenes.

**Requirements.** Supports **D3, D4, D5** — you will be asked to find and modify code live.

**Verification.** You can locate any script in under five seconds without the search box. Build Settings lists exactly five scenes, all enabled.

---

### Step 0.4 — Version control and the submission repository

**Goal.** Satisfy S6 early, and give yourself a rollback point before the risky asset work in Phase 2.

**What to do**
- Add a proper Unity `.gitignore` (`Library/`, `Temp/`, `Obj/`, `Build/`, `Logs/`, `UserSettings/`, `*.csproj`, `*.sln`).
- Verify `Library/` is not tracked — it alone can exceed the 300 MB budget.
- Push to a private-then-public GitHub repository.
- Reserve the repo URL for the GDD download link (S6).
- Tag a commit at the end of every phase: `phase-0-clean`, `phase-1-player`, and so on.

**Deliverable.** A clean remote repository with phase tags.

**Requirements.** **S6.**

**Verification.** A fresh `git clone` opens in Unity and compiles without the original `Library` folder.

---

## Phase 1 — Player, cameras, animation

### Step 1.1 — Camera-relative movement, gravity and jump

**Goal.** Noa moves the way the player expects relative to where the camera is looking.

**What to do**
- Fix **B5**: build the move vector from the camera's flattened forward and right, not from world axes.
- Rotate Noa toward her movement direction when in third person; keep her locked to camera yaw in first person.
- Wire `JumpPressed` with a grounded check; use `CharacterController.isGrounded` plus a small coyote-time window.
- Keep gravity at `-20f` and `groundedForce` at `-2f`; these are already tuned reasonably.
- Add a step offset and slope limit tuned so the museum staircase (T20) is climbable — test this now, not in Phase 2.

**Deliverable.** `PlayerController.cs` with camera-relative motion, run, gravity, jump, stair climbing.

**Requirements.** Supports T20 (stair climbing), T12.

**Verification.** `W` always moves Noa away from the camera. Jump only works when grounded. She walks up a 30° ramp and a test staircase without catching.

---

### Step 1.2 — Two Cinemachine cameras and the toggle

**Goal.** Third-person exploration and first-person inspection, switchable.

**What to do**
- Cinemachine 3 uses `CinemachineCamera` (the old `CinemachineVirtualCamera` name is gone in 3.x) and a
  `CinemachineBrain` on the Main Camera.
- **`CM_ThirdPerson`** — the GDD's default view: behind and above Noa, with `CinemachineThirdPersonFollow` or an orbital
  follow. This is the exploration camera.
- **`CM_FirstPerson`** — parented to a head bone / eye anchor. This is the Time Lens view.
- Toggle with `C` via `CameraTogglePressed`, by swapping camera `Priority`. Let the Brain blend — a hard cut looks cheap.
- Deliver the GDD's framing beats: pull back in large halls, push in near puzzles. Use a `CinemachineTargetGroup` or a
  trigger-driven priority change (this doubles as one of the T3 triggers).
- Keep the minimap camera entirely separate — T19 says *two cameras besides the minimap*, so three total.

**Deliverable.** Two Cinemachine cameras, a working toggle, at least one framing beat.

**Requirements.** **T19.**

**Verification.** `C` blends smoothly between views. Both views are playable — movement, interaction and throwing all work in each. The minimap camera is a third, independent camera.

---

### Step 1.3 — Noa's Animator, built by hand

**Goal.** T14, with a controller you can defend line by line.

**What to do**
- **Do not import a controller.** Create `NoaController.controller` yourself. T14 says "not imported" and the defense is
  on your work — an imported controller is an automatic loss on this item.
- Four states minimum: **Idle → Walk → Run → Jump**. Add **Interact** as a fifth for margin.
- Parameters: `Speed` (float), `IsGrounded` (bool), `JumpTrigger` (trigger), `InteractTrigger` (trigger).
- Use a 1D blend tree for Idle→Walk→Run driven by `Speed`; explicit transitions for Jump and Interact.
- Drive `Speed` from the `CharacterController.velocity` magnitude, not from raw input — it stays correct when Noa is blocked by a wall.
- Free Mixamo clips are fine as **animation data**; the *controller and its state machine must be yours*.

**Deliverable.** `NoaController.controller` — hand-built, ≥4 states, wired to `PlayerController`.

**Requirements.** **T14 (player half).**

**Verification.** Animation matches actual movement in both camera modes. You can open the Animator and explain every state, transition and condition unprompted.

---

## Phase 2 — World geometry and art

> This is the phase with external-tool dependencies (Blender) and the highest risk of slipping. Start Step 2.4 early.

### Step 2.1 — The museum: a two-storey building with stairs

**Goal.** T20, delivered as the game's main location rather than as a bolted-on test house.

**What to do**
- Build the museum with **ProBuilder** (already installed). Two floors is the requirement — the museum *is* the building.
- Ground floor: entrance hall, main gallery, the Clock of Creation chamber.
- Upper floor: mezzanine overlooking the hall, the curator's office (a GDD NPC lives here — the Old Curator, האוצר הזקן,
  leaves Noa notes and hints), a storage room.
- A real, walkable **staircase** connecting them — not a ramp, and not a teleport.
- **Textures must be your own choice** (T20 says "בבחירה אישית"): marble floor, wood panelling, plaster, aged brass.
  Author the materials in URP; record where each texture came from for the defense.
- Give the mezzanine a railing with a collider — falling to the ground floor becomes one of the T4 collisions.

**Deliverable.** A two-storey, textured, walkable museum.

**Requirements.** **T20.** Supports S10, G1.

**Verification.** You can walk from the entrance to the upper-floor office using only the stairs. Both floors are used by gameplay, not just present.

---

### Step 2.2 — FrozenCity: self-built Terrain

**Goal.** T6, which cannot be satisfied indoors — this is why FrozenCity exists as an outdoor scene.

**What to do**
- Create a Unity Terrain and sculpt it **yourself**: the city sits in a shallow valley, with raised outskirts framing
  the clock tower so it is visible from spawn.
- Paint at least three texture layers: cobblestone approach, frozen dirt, snow on the heights.
- Add terrain detail and a few trees, but keep the density low — this is the single biggest threat to the 300 MB budget (S1).
- Terrain resolution: **513 or 1025 heightmap**. 2049 is overkill and expensive to ship.
- The terrain must be traversable by the Wardens — bake it into the NavMesh in Phase 4.

**Deliverable.** A hand-sculpted, multi-layer Terrain in FrozenCity.

**Requirements.** **T6.** Supports S1, S10.

**Verification.** The Terrain is demonstrably yours (show the sculpt in the inspector). Noa can walk from spawn to the clock tower entirely on terrain. Scene loads in under five seconds.

---

### Step 2.3 — Hinge joint set pieces

**Goal.** T5, using hinges the story already wanted.

**What to do**
Build at least three, so one failing does not cost the requirement:
1. **The Clock of Creation pendulum** (MuseumNight) — a `HingeJoint` swinging on the Z axis. It stops dead when the
   clock breaks. This is the GDD's opening image and it is literally a hinge.
2. **The clock-tower bell** (FrozenCity) — a hinged bell Noa rings by hitting it with the Chrono Orb. This single object
   satisfies T5 **and** T15 and is the scene's puzzle solution (the GDD's "the tower bell never rang").
3. **The gallery gate / drawbridge** — a hinged gate with a motor and limits, opened by an interaction.

- Set `useLimits` on all three so nothing spins freely and looks broken.
- Use `useMotor` on the gate; use `useSpring` on the pendulum for a believable resting swing.

**Deliverable.** Three working hinge-joint objects across two scenes.

**Requirements.** **T5.** Supports T15, T4.

**Verification.** Each hinge swings on the intended axis, respects its limits, and reacts to physical impact. The bell rings when hit by the orb.

---

### Step 2.4 — Voronoi fracture ×2

**Goal.** T10 — two assets you fractured yourself, appearing **intrinsically** in the game.

> ⚠️ **Start this step early.** It requires Blender and is the most common cause of a missing requirement.

**What to do**
- Install **Blender** and enable the **Cell Fracture** add-on (*Edit → Preferences → Add-ons → "Cell Fracture"*).
  Cell Fracture is a Voronoi implementation — this is exactly the technique T10 names.
- **Fracture #1 — the Clock of Creation** (MuseumNight). It shatters in the opening beat when time breaks. Not a
  side-effect: it is the inciting incident of the entire GDD.
- **Fracture #2 — a frozen statue** (FrozenCity). Noa breaks it with the Chrono Orb to clear a path, or a Shadow
  destroys it as a threat display.
- Fracture into **15–40 pieces**. More is heavier and reads no better on screen.
- Export as FBX → Unity. Each shard: `MeshCollider` (convex) + `Rigidbody`.
- Build a `FracturedObject.cs` that keeps the intact mesh visible until the break, then swaps in the shard hierarchy,
  applies an explosion force, and despawns shards after a few seconds (both for performance and the 300 MB budget).
- Record a short clip of the Blender fracture process for the defense — proof that you did it, not the store.

**Deliverable.** Two fractured prefabs, breaking as story beats in two different scenes.

**Requirements.** **T10.** Supports G1, D3.

**Verification.** Both objects shatter on cue, shards collide with the floor and each other, framerate holds. You can explain the Cell Fracture settings you used.

---

### Step 2.5 — LOD ×2

**Goal.** T11 — two *different* assets you reduced yourself, integrated as LOD.

**What to do**
- **Asset #1 — a marble statue**; **Asset #2 — a stone column**. Both are museum-native and both are duplicated across
  the scene, which is what makes LOD actually pay off.
- In Blender, apply the **Decimate** modifier to make three tiers, e.g.
  `LOD0 100% → LOD1 ~50% → LOD2 ~20%`. Record the actual triangle counts for each tier.
- In Unity, add an `LODGroup` to each prefab and assign the three meshes with sensible screen-relative transitions
  (roughly 60% / 25% / 8%).
- Place many instances so the effect is visible and worth having.
- Keep a before/after triangle-count table for the defense and the GDD.

**Deliverable.** Two prefabs with three-tier hand-decimated LOD groups.

**Requirements.** **T11.** Supports S1.

**Verification.** Moving the Scene camera visibly steps the LODs. The Game view stats window shows the triangle count dropping with distance. You can state the exact reduction percentages.

---

### Step 2.6 — Scale and realism pass

**Goal.** S10, which the brief calls out with an explicit example and which is easy to fail by accident.

**What to do**
- Set a **1 unit = 1 metre** rule and enforce it. Noa ≈ 1.7 m — build everything against her.
- Check every character and prop actually rests on the ground. The brief's example is foxes floating above the ground.
- Doorways ≈ 2.1 m, stair risers ≈ 0.17 m, mezzanine railing ≈ 1.1 m, museum ceilings 4–6 m.
- Enemies must be plausible: Wardens roughly human-scale; Shadows may drift, but drift is a *stated* supernatural
  property, not a physics bug — sell it with VFX so it never reads as an error.
- Walk each scene in first person specifically hunting for scale mistakes; they are far more obvious at eye level.

**Deliverable.** A scene-by-scene scale audit, with fixes applied.

**Requirements.** **S10.** Supports G1.

**Verification.** No object intersects or floats above the floor. Nothing is comically mis-sized relative to Noa.

---

## Phase 3 — Core systems

### Step 3.1 — Interaction system

**Goal.** One reusable mechanism for every "press E on a thing" in the game.

**What to do**
- `IInteractable` with `string Prompt { get; }` and `void Interact(GameObject interactor);`.
- `PlayerInteractor.cs`: a `Physics.Raycast` from the active camera, range ≈ 3 m, **filtered with a `LayerMask` built in
  code** — start banking T17 here.
- Show a world-space prompt when something interactable is targeted; hide it otherwise.
- Concrete implementations: `ExhibitPlaque`, `DoorInteractable`, `ShardPickup`, `ItemPickup`, `LeverInteractable`, `NPCDialogue`.
- Route it through `InteractPressed` from the input reader.

**Deliverable.** Interaction interface, player interactor, and at least five implementations.

**Requirements.** Supports T2, T3, T17, T9.

**Verification.** Looking at an interactable shows its prompt; `E` fires the right response; looking away hides the prompt. Works identically in both camera modes.

---

### Step 3.2 — The trigger set (≥4)

**Goal.** T3, with four triggers that each do real work.

**What to do**
Build these as distinct `OnTriggerEnter` components, not four copies of one script:
1. **`RoomEntryTrigger`** — entering a hall fires dialogue, adjusts camera priority and updates the objective text.
2. **`TutorialTrigger`** — reveals the dynamic 3D instruction for the verb this area teaches (feeds T2).
3. **`EraZoneTrigger`** — entering a time-sensitive zone enables era switching and shows which eras are valid here.
4. **`HazardTrigger`** — a temporal rift that drains health and energy while the player stands in it.
5. **`TimeAnchorTrigger`** — silently arms a hidden teleport (feeds T21).

- Log each trigger's first activation to a debug list, so during the defense you can prove all of them fired.

**Deliverable.** Five distinct trigger components, placed across the three scenes.

**Requirements.** **T3.** Supports T2, T21, T8.

**Verification.** Each trigger fires once, on the right volume, with the right effect. The debug list shows every trigger type activated in a full playthrough.

---

### Step 3.3 — Collision handling (≥3)

**Goal.** T4 — collisions **detected and acted upon**, which is different from triggers.

**What to do**
Use real `OnCollisionEnter` with physical contact, not `OnTriggerEnter`:
1. **Chrono Orb → object.** The orb's `Rigidbody` strikes a display case, bell or statue; impact force decides whether it shatters (ties to T10 and T5).
2. **Player → falling debris.** Shards from a fracture that hit Noa deal damage using relative velocity.
3. **Player → Warden body block.** Physical contact with a Warden triggers capture and the T21 teleport return.
4. **Orb → Collector's shield** (ClockCore) — the boss's shield only breaks above an impact threshold.

- Use `collision.relativeVelocity.magnitude` so the response scales with impact, and `collision.contacts[0].point` to spawn the VFX in the right place. That detail is very defensible under D3.

**Deliverable.** Four collision responders using contact data.

**Requirements.** **T4.** Supports T10, T15, T21.

**Verification.** Each collision produces the correct effect. A gentle contact and a hard one produce visibly different results.

---

### Step 3.4 — Health, energy and score, wired to gameplay

**Goal.** T8, turning the existing `GameState` fields into a felt system.

**What to do**
- `GameManager.TakeDamage` / `Heal` already exist — call them from the hazard trigger, debris collision and Warden capture.
- **Energy is the resource that makes the time powers a choice.** Slow-time drains it, era switching costs a fixed
  amount, the Chrono Orb costs a small amount. Regenerate slowly while not using powers.
- **Score = Time Shards + efficiency.** Award for shards, exhibits repaired and puzzles solved; deduct on capture.
  T8 explicitly accepts "gain/loss of score", so make loss real.
- Fire `StateChanged` on every change so the HUD (Step 5.2) stays passive.
- Handle `PlayerDied`: in MuseumNight, respawn at scene start; in FrozenCity and ClockCore, route to the Time Anchor system (T21).

**Deliverable.** Health, energy and score all driven by gameplay events, with visible consequences.

**Requirements.** **T8.** Supports T21, G1.

**Verification.** Standing in a rift drains health. Holding slow-time drains energy to zero and force-cancels the ability. Collecting a shard raises score; being captured lowers it.

---

### Step 3.5 — The era system (Past / Present / Future)

**Goal.** The GDD's central mechanic — the thing that makes this game interesting (G1).

**What to do**
- `TimeEra` enum: `Past`, `Present`, `Future`. `EraManager.cs` holds the current era and raises `EraChanged`.
- Represent eras as **sibling root objects** per zone: `Zone_Past`, `Zone_Present`, `Zone_Future`. Switching
  activates one and deactivates the others. This is far cheaper than three loaded worlds and reads identically to the player.
- `EraBoundObject.cs` — marks an object as existing only in listed eras.
- `EraPersistentObject.cs` — an object whose state carries *forward*: move a cart in the Past and it is moved in Present and Future.
  **This is the mechanic the GDD's example puzzle depends on** (moving a cart in the past opens a path in the present
  but blocks a different exit in the future). Build it properly; it is the game's signature.
- Blend a short VFX + audio sting on switch, and keep the camera fixed through the transition — the GDD is specific that
  the camera holds its angle while the world changes around Noa.
- Gate the ability: locked in MuseumNight, unlocked when the Clock of Creation breaks.

**Deliverable.** Working three-era switching with objects that persist changes forward through time.

**Requirements.** **G1 (highest-value item).** Supports T3, T8, T9.

**Verification.** `Q` / `R` switch eras with correct object visibility. Moving the cart in the Past changes both the Present and the Future. At least one puzzle **cannot** be solved without thinking across all three eras.

---

### Step 3.6 — Chrono Hourglass: slow time

**Goal.** The GDD's `Ctrl` ability, and the second acquired cross-scene item.

**What to do**
- Only usable once `GameState.hasChronoHourglass` is true — acquired in FrozenCity, **required** in ClockCore.
- Reduce `Time.timeScale` (≈ 0.3) and set `Time.fixedDeltaTime = 0.02f * Time.timeScale` so physics stays stable.
- Keep Noa near full speed while the world slows — that is the fantasy. Scale her animator and movement by `1/timeScale`.
- Drain energy while held; auto-cancel and restore `timeScale` to 1 at zero.
- The UI must **not** slow: drive HUD animation from `Time.unscaledDeltaTime`.
- Feedback: desaturation, a low audio filter, and particles. The player must never be unsure whether it is active.

**Deliverable.** A working, energy-limited slow-time ability with unmistakable feedback.

**Requirements.** **T9 (item #2).** Supports T8, G1.

**Verification.** Time slows, energy drains, the ability cancels at zero and `timeScale` returns to exactly 1. Physics does not jitter. UI stays responsive throughout.

---

### Step 3.7 — The Chrono Orb: physical projectile

**Goal.** T15 — a physical body that is fired and impacts.

**What to do**
- A glass sphere prefab: `Rigidbody`, `SphereCollider`, trail renderer.
- `ChronoOrbLauncher.cs` on Noa, fired from `ShootPressed`. Spawn at a muzzle point in front of the active camera and
  apply `AddForce(cam.forward * power, ForceMode.Impulse)`.
- The orb is not a weapon: on impact it **freezes or rewinds** what it hits — a hinged bell rings, a fractured object
  shatters, a moving Warden is frozen for a few seconds. That keeps Noa a non-combatant, exactly as the GDD insists.
- Small energy cost; a short cooldown; despawn after ~8 s.
- Orbs must physically bounce off geometry before expiring — this is the visible proof that it is a real physical body.

**Deliverable.** A throwable rigidbody orb with three distinct impact behaviours.

**Requirements.** **T15.** Supports T4, T5, T10.

**Verification.** The orb arcs under gravity, bounces off walls, rings the hinged bell, shatters a fractured object, and briefly freezes a Warden.

---

### Step 3.8 — Time Anchors: the hidden teleports

**Goal.** T21, read literally. This requirement has the most specific wording in the brief — match it clause by clause.

**What to do**
- **Placement.** At least two per scene in **FrozenCity and ClockCore only**. T21 says *from the second scene onward*;
  MuseumNight must not have them.
- **Hidden.** No marker, no icon, no prompt on the HUD. They arm silently as Noa walks past. The **Time Lens** (item #1)
  is the *only* way to see them, which is precisely why the Lens is acquired in scene 1 and needed in scene 2 —
  it converts a requirement into the game's cross-scene spine (S9).
- **On failure return to the anchor, not the start.** Capture by a Warden, death by hazard, or a fatal fall all
  respawn Noa at the **last armed anchor**, in the era she was in.
- **Refresh on return.** T21 says health is restored and "possibly score". Restore health to full, restore energy
  partially, and deduct a small score penalty so failure still costs something (this also serves T8's "loss" clause).
- Store the anchor in the existing `GameState.hasCheckpoint` / `checkpointSceneName` / `checkpointPosition` fields — they
  are already there and unused.
- Add `checkpointEra` to `GameState` so the return restores the correct era.

**Deliverable.** A hidden anchor system, ≥2 per scene in scenes 2 and 3, with correct failure-return semantics.

**Requirements.** **T21.** Supports T3, T8, T9, S9.

**Verification.** Anchors are invisible without the Lens and visible with it. Dying returns Noa to the last anchor, not to scene start, with health refreshed and score reduced. The era is restored correctly.

---

### Step 3.9 — Cross-scene persistence and the two acquired items

**Goal.** T9, explicitly and demonstrably.

**What to do**
- `GameState` is already `[System.Serializable]`. Add `SaveToJson()` / `LoadFromJson()` using `JsonUtility` and write to
  `Application.persistentDataPath`. T9 names *Serialize* — having an actual serialization path, not only an in-memory
  singleton, is the safest reading of the requirement, and it is trivially demonstrable in the defense.
- Save on every scene transition and on reaching a Time Anchor.
- **Item #1 — Time Lens.** Granted at the end of MuseumNight. **Required** in FrozenCity to find the anchors and read
  time cracks. Without it, FrozenCity is unfinishable.
- **Item #2 — Chrono Hourglass.** Granted mid-FrozenCity. **Required** in ClockCore, where the boss arena is unsurvivable
  at normal speed.
- Carry across scenes: health, energy, score, Time Shard count, both item flags, detection count, deaths, playtime, and the anchor.
- Build a small on-screen debug overlay (toggleable) showing the live `GameState` — invaluable for D3/D5.

**Deliverable.** Serialized, verifiable persistence with two gating acquired items.

**Requirements.** **T9.** Supports S9, T8, T21.

**Verification.** Collect the Lens, change scene, confirm the flag survives. Delete the save file and confirm a clean new game. Show the JSON file on disk during the defense.

---

## Phase 4 — AI, navigation and stealth

### Step 4.1 — Two NavMesh agent types with separate bakes

**Goal.** T13's hardest clause and the foundation for T16 and T17.

> **Terminology, for the defense.** Unity's NavMesh baking *is* Recast — `com.unity.ai.navigation` wraps the
> Recast/Detour library. When the brief says "Recast", it means `NavMeshSurface` baking plus navigation agents. Say
> this explicitly in the defense; it shows you know what the tool is, not just which button to press.

**What to do**
- **Navigation window → Agents**: define two agent types.
  - **`WardenAgent`** — radius 0.5, height 2.0, step height 0.4, max slope 45°. Human-scale, ground-bound.
  - **`ShadowAgent`** — radius 0.3, height 1.2, step height 0.8, max slope 60°. Smaller and more permissive; it reaches
    places the Wardens cannot.
- **Two separate `NavMeshSurface` components**, each with its own Agent Type, each baked separately.
  T13 says *bake נפרד* — two surfaces, two bakes, and you must be able to show both in the inspector.
- **Different routes.** This is the part most people miss: the two agent types must actually *travel differently*.
  - Mark ledges, rooftops and rubble with a custom NavMesh Area (`ShadowOnly`) included in the Shadow bake and excluded from the Warden bake.
  - Mark the main floor and stairs as walkable for both.
  - Result: Wardens patrol corridors and stairs; Shadows cut across broken ground and elevated ledges. Visibly different paths.
- In FrozenCity, bake the Terrain into both surfaces.

**Deliverable.** Two agent types, two `NavMeshSurface` bakes, two genuinely different route networks.

**Requirements.** **T13 (pathfinding half), T16 (Recast half).**

**Verification.** Toggle each surface's gizmo and see two visibly different navmeshes. Spawn one of each agent with the same destination and watch them take different paths.

---

### Step 4.2 — Warden patrol with pause

**Goal.** T7, read literally — *patrol **with pause***, not patrol.

**What to do**
- `PatrolRoute.cs` holding an ordered waypoint list, with per-waypoint `waitSeconds`.
- `WardenAI.cs` state machine: **Patrol → Pause → Alert → Chase → Search → Return**.
- At each waypoint the Warden **stops for 2–4 seconds and sweeps its head/vision cone** while paused. The pause must be
  visible and purposeful — a stationary enemy that scans is the thing being graded, and it is also what makes stealth playable.
- Use `NavMeshAgent.isStopped` during pause; do not just zero the speed.
- Optional loop vs. ping-pong per route.

**Deliverable.** `WardenAI.cs` + `PatrolRoute.cs` with visible, timed pauses.

**Requirements.** **T7.** Supports T13, T16.

**Verification.** The Warden walks to a waypoint, fully stops, scans for the configured duration, then moves on. The pause is obvious to a watching grader.

---

### Step 4.3 — Vision, LayerMask and stealth

**Goal.** T16 and T17 together — they are one system.

**What to do**
- **Detection is three tests, in order:** range → angle (`Vector3.Angle` against a ~90° cone) → line of sight.
- The line-of-sight test is `Physics.Raycast` against a **`LayerMask` built in code**:
  ```csharp
  // T17 — LayerMask constructed in code, used against the Recast-navigated agents
  private LayerMask visionBlockers;
  private void Awake()
  {
      visionBlockers = LayerMask.GetMask("Default", "Environment", "HideVolume");
  }
  ```
  Do **not** configure this in the inspector only. T17 says *בקוד* — in code. Keep it a named, commented field so it is
  findable in five seconds during the defense.
- **Stealth mechanics:**
  - Crouch (or slow walk) reduces the detection radius.
  - `HideVolume` objects — display cases, pillars, statues — break line of sight via the mask.
  - A **detection meter** fills while Noa is in the cone and drains when she breaks sight. Full meter = capture.
  - Slow-time and the Chrono Orb both create stealth openings: freeze a Warden, cross behind it.
- Draw the vision cone with `OnDrawGizmosSelected` — for tuning and for the defense.
- On capture: increment `GameState.detectedCount`, apply the score penalty, and return Noa to the last Time Anchor (T21).

**Deliverable.** A vision + stealth system driven by a code-built LayerMask.

**Requirements.** **T16, T17.** Supports T13, T21, T8.

**Verification.** Standing behind a `HideVolume` prevents detection at any range. Stepping out starts the detection meter. Show the `LayerMask.GetMask` call on request. Capture returns Noa to an anchor.

---

### Step 4.4 — Steering behaviours

**Goal.** T13's first clause — *a clear steering element*: seek, flee, or pursue.

**What to do**
- Pathfinding alone is arguably not steering. Implement at least two classic behaviours explicitly, and **name the
  methods after them** so they are obvious in a code review:
  - **`Pursue()`** — the Warden aims at Noa's *predicted* position (`target.position + target.velocity * lookAhead`),
    not her current one. Visibly smarter than naive chasing and unmistakably "pursue".
  - **`Seek()`** — the Shadow steers toward the nearest Time Shard.
  - **`Flee()`** — the Shadow retreats when Noa raises the Chrono Orb, and Wardens back off briefly when frozen.
- Blend steering with `NavMeshAgent` by feeding the steering target into `SetDestination` each interval, or by driving
  `agent.velocity` directly for the Shadow so its drift reads as floating rather than walking.

**Deliverable.** Named `Seek`, `Flee` and `Pursue` behaviours in use.

**Requirements.** **T13 (steering half).**

**Verification.** The Warden cuts corners to intercept rather than trailing behind. The Shadow moves toward shards unprompted and retreats from a raised orb.

---

### Step 4.5 — The Chronological Shadow: second agent type

**Goal.** Make agent type B a real character, not a reskinned Warden.

**What to do**
- Uses the `ShadowAgent` navmesh — crosses ledges and rubble the Wardens cannot.
- Per the GDD it does not speak, and endlessly repeats one action from its past. Loop a single gesture animation.
- **Drawn to Time Shards and steals them** — it can take a shard from Noa on contact, which is a real score loss (T8)
  and a genuine reason to fear it.
- Recover a stolen shard by freezing the Shadow with the Chrono Orb.
- Visual: translucent, desaturated, drifting slightly above the ground. Because the drift is *stated fiction*, sell it
  with trailing VFX so it never reads as the S10 floating-object failure.

**Deliverable.** A distinct second enemy with its own navmesh, route and threat model.

**Requirements.** **T13.** Supports T8, T15, S10, G1.

**Verification.** Wardens and Shadows demonstrably take different routes to the same target. A Shadow steals a shard, score drops, and freezing it recovers the shard.

---

### Step 4.6 — Enemy Animator

**Goal.** The second half of T14, and margin in case the player controller is questioned.

**What to do**
- Hand-build `WardenController.controller` with **Patrol / Alert / Chase / Attack**, plus **Frozen** for the orb hit.
- Parameters: `Speed`, `AlertLevel` (float, driven by the detection meter), `IsFrozen` (bool), `AttackTrigger`.
- Drive `AlertLevel` from the same value the detection meter uses, so the animation and the mechanic never disagree.

**Deliverable.** A hand-built enemy Animator with ≥4 states.

**Requirements.** **T14.**

**Verification.** The Warden visibly changes posture as the detection meter fills, freezes when hit, and resumes correctly.

---

## Phase 5 — UI and readability

### Step 5.1 — Main menu and victory screen

**Goal.** T1.

**What to do**
- **MainMenu**: title treatment, New Game, Continue (enabled only when a save exists), Controls, Quit. Add a slow
  camera drift over a museum vignette — cheap, and it sells G1 in the trailer.
- **Continue** loads the serialized `GameState` from Step 3.9.
- **Victory**: the GDD's Ending 1 — time returns to its course, nobody remembers but Noa. Show final score, shards
  collected, times detected, and total playtime. All four values already exist in `GameState`.
- All menu navigation through the New Input System `UI` map (T12).

**Deliverable.** A functional main menu and a victory screen with a run summary.

**Requirements.** **T1.** Supports T9, T12, G1.

**Verification.** New Game starts clean. Continue restores a run. Victory shows correct stats. Everything is navigable by mouse and keyboard.

---

### Step 5.2 — HUD and pause menu

**Goal.** Make health, energy, score and the current era legible at a glance.

**What to do**
- HUD: health bar, energy bar, Time Shard count, current era indicator, item icons (Lens / Hourglass), and a subtle
  detection meter that only appears when a Warden is looking.
- Subscribe to `GameManager.StateChanged` — never poll in `Update`.
- Pause menu on `Escape`: Resume, Restart Scene, Controls, Main Menu, Quit. Set `Time.timeScale = 0` and remember to
  restore it to **1**, not to the slow-time value (a real bug risk given Step 3.6).
- Drive all HUD animation from `Time.unscaledDeltaTime` so slow-time does not slow the interface.

**Deliverable.** A live HUD and a working pause menu.

**Requirements.** Supports T8, T2, T12.

**Verification.** Every HUD element updates the moment its value changes. Pausing during slow-time and resuming leaves `timeScale` at exactly 1.

---

### Step 5.3 — Minimap

**Goal.** T18 — orientation, present throughout an entire scene.

**What to do**
- A second (third overall) orthographic camera above Noa, rendering a `Minimap` layer only, into a `RenderTexture` shown
  in a corner of the HUD.
- Follow Noa's position; rotate with her heading (rotating reads better for orientation, which is what T18 asks for).
- Minimap icons on a dedicated layer: Noa, objectives, collected/uncollected shards, exits. **Do not show hidden Time
  Anchors** — that would break T21's "hidden" clause.
- Mask it to a circle or a framed rectangle. The GDD's **Time Compass** is the in-fiction name for it, which ties it to
  the item table.
- **Guarantee full coverage in MuseumNight** — active from load to exit. That single scene is what satisfies T18.
- Exclude the minimap camera from the Cinemachine Brain so it never counts against T19's "two cameras besides the minimap".

**Deliverable.** A working, always-on minimap in MuseumNight.

**Requirements.** **T18.** Supports T19, G1.

**Verification.** The minimap is visible for the entire MuseumNight scene, orients correctly, and never reveals a hidden anchor.

---

### Step 5.4 — Dynamic 3D tutorial text

**Goal.** T2 — read carefully: *tutorial, dynamic text, clear instructions, **in 3D***. Screen-space UI does not satisfy the 3D clause.

**What to do**
- Use **world-space TextMeshPro** placed in the scene — text that lives in 3D space, not on the canvas overlay.
- The museum makes this natural: **exhibit plaques** are the tutorial. Each plaque teaches one verb, and its text is
  **dynamic** — it reads the player's current binding and progress, e.g. *"Press E to read the plaque"* → after reading,
  *"Hold Ctrl to slow time — 68% energy"*.
- Fade in on approach (via `TutorialTrigger` from Step 3.2), fade out once the action is performed.
- Billboard the text toward the camera so it stays readable in both camera modes.
- Cover every verb: move, run, jump, interact, throw the orb, switch camera, switch era, slow time.
- Add a persistent **objective line** in world space at key doorways ("Reach the Clock of Creation chamber") so the
  player always has a next goal.
- Keep the fiction: the Old Curator's notes are the voice of the tutorial. That satisfies T2 *and* the GDD's NPC.

**Deliverable.** World-space, dynamic, progress-aware tutorial text covering every mechanic.

**Requirements.** **T2.** Supports T3, G1.

**Verification.** Text exists in 3D space (rotate the Scene camera and see it in the world). Content changes based on player state. A first-time player completes MuseumNight without external instruction.

---

## Phase 6 — Scene content

### Step 6.1 — MuseumNight, complete

**Goal.** A polished tutorial scene that also carries five requirements on its own.

**Beats**
1. Noa closes down the museum. Move / run / look are taught by plaques.
2. Interaction and the camera toggle are taught upstairs — which forces the player up the staircase (T20).
3. The Chrono Orb is found; the player learns to throw it at a display case (T15, T4, T10 setup).
4. In the central hall every clock stops at the same second, the lights fail, and **the Clock of Creation shatters** (T10 #1).
5. A single Warden appears. Stealth is introduced in one short, forgiving corridor (T16, T7).
6. Noa finds the **Time Lens** in the curator's office and leaves for the first exhibit (T9 #1).

**Requirements closed here.** T20, T18, T19, T2, T14 (player), T10 #1, T11 both.

**Verification.** A new player finishes in 5–8 minutes without help and leaves holding the Time Lens.

---

### Step 6.2 — FrozenCity, complete

**Goal.** The scene that carries the most requirements — build it after the systems are proven.

**Beats**
1. Noa enters the painting. The city is frozen just before sunset; everyone is motionless (GDD Scene 2, verbatim).
2. Open Terrain approach to the clock tower (T6).
3. Era switching unlocks. The core puzzle is the GDD's own: **the tower bell never rang, so the moment cannot continue.**
   Return a **gear** to the tower across eras — find it in the Past, install it in the Present, verify in the Future.
4. Wardens patrol the streets; Shadows drift across rooftops on their own navmesh (T13, T7, T16, T17).
5. **≥2 hidden Time Anchors**, visible only through the Time Lens (T21, T9 #1 paying off).
6. Ring the bell with the Chrono Orb — the hinged bell (T5, T15, T4).
7. A frozen statue shatters (T10 #2).
8. The **Chrono Hourglass** is the reward for freeing the city (T9 #2).

**Requirements closed here.** T6, T21, T13, T16, T17, T7, T5, T15, T10 #2, T9 #2.

**Verification.** The scene is unfinishable without the Lens, and finishing it grants the Hourglass. Both agent types are visibly using different routes.

---

### Step 6.3 — ClockCore, complete

**Goal.** The convergence scene and the confrontation.

**Beats**
1. Noa returns to a museum that has turned itself inside out — floor became ceiling, doors lead to wrong halls, figures
   from different centuries walk together (GDD Scene 6).
2. Wardens *and* Shadows are both present, on both navmeshes.
3. Reaching the Clock of Creation chamber, Noa meets **the Collector** (GDD Scene 7).
4. **Three-phase boss across three eras** — the museum as it was built (Past), as Noa knows it (Present), as it nearly
   ceases to exist (Future). The era switch is the boss mechanic, not a side ability.
   - *Phase 1 (Past)* — the Collector is shielded; break the shield with the Chrono Orb (T4).
   - *Phase 2 (Present)* — he summons Wardens; use stealth and slow-time (T16, T9 #2).
   - *Phase 3 (Future)* — time is nearly erased; the Hourglass is mandatory to survive. This is where the Restorer's
     undo mechanic lives: he reverts Noa's placements and she must beat him to the last shard.
5. Return the Time Shards, prevent the erasure of the First Moment, → `Victory`.

**Requirements closed here.** All previously introduced mechanics used together; T21 anchors ×2; T3 phase triggers.

**Verification.** The fight cannot be won without the Hourglass. Each phase changes the era and the required tactic. Victory loads the Victory scene with correct stats.

---

### Step 6.4 — Coherence pass

**Goal.** S9 — "a coherent logical connection between all the scenes." The brief is explicit that the final project is
judged on precision and coherence, unlike the homework.

**What to do**
- Verify the chain end to end: **Lens (scene 1) → required in scene 2 → Hourglass (scene 2) → required in scene 3.**
- Time Shards accumulate across all three scenes and gate the finale.
- Carry the narrative thread: the Old Curator's notes appear in all three scenes and progressively reveal the Collector.
- Keep the visual language constant: the same brass/glass/marble palette, the same era-switch VFX everywhere.
- Play the whole game start to finish in one sitting and write down every moment that feels disconnected. Fix those.

**Deliverable.** One continuous game, not three demos.

**Requirements.** **S9.** Supports G1.

**Verification.** A player who has never seen the GDD can explain, after playing, why scene 2 follows scene 1 and what carried over.

---

## Phase 7 — Audio, lighting and polish

### Step 7.1 — Audio

**What to do**
- Ambience per scene: a ticking, echoing museum at night; a frozen city with wind and one impossibly held note; a
  distorted, reversed version of the museum theme in ClockCore.
- Effects on: footsteps (with a stair variant), interaction, shard pickup, orb throw and impact, bell, fracture,
  Warden alert, capture, era switch, slow-time enter/exit.
- Route through an `AudioMixer` with Master / Music / SFX groups and a low-pass filter on the SFX group that engages
  during slow-time — a single mixer snapshot sells the whole ability.
- Keep audio compressed (Vorbis, mono for 3D sources). Audio is one of the top three contributors to build size (S1).

**Requirements.** Supports G1, G2, S1.

---

### Step 7.2 — Lighting and VFX

**What to do**
- MuseumNight is the identity shot: warm pooled spotlights on exhibits, cold moonlight from skylights, deep shadows.
- Bake lighting where possible. Real-time shadow count is the main performance risk on the museum's two floors.
- Era colour grading via URP volumes: Past = warm sepia, Present = neutral, Future = cold cyan. The player should be
  able to tell the era from a still frame — that reads instantly in the trailer.
- Particles: shard collection, fracture dust, orb trail, Shadow drift, era-switch shockwave.
- Run the URP performance profile and hold a stable framerate on your defense machine (D2).

**Requirements.** Supports G1, G2, S10, D2.

---

## Phase 8 — Submission and defense

### Step 8.1 — Build, size budget and packaging

**Goal.** S1, S2, S5 — including the −3 penalty clause.

**What to do**
- Build a Windows x64 standalone. Test it on a machine that has never opened Unity.
- **Enforce the 300 MB compressed budget from now on, not at the end.** Suggested allocation:

  | Category | Budget |
  |---|---|
  | Textures | ≤ 120 MB |
  | Audio | ≤ 40 MB |
  | Meshes | ≤ 60 MB |
  | Terrain data | ≤ 20 MB |
  | Engine + code | ≈ 40 MB |
  | Headroom | ≈ 20 MB |

- Levers if over budget: cap texture import sizes at 1024 (2048 only for hero surfaces), enable crunch compression,
  set audio to Vorbis / mono, reduce terrain heightmap and detail resolution, and delete unused imported packages
  (`com.unity.multiplayer.center` and `com.unity.visualscripting` are currently in the manifest and unused).
- Check the **Editor Log** after building — it prints a full size breakdown by asset. Use it, do not guess.
- Zip the build, extract it into a clean folder, and run it from there. That extract-and-run test is exactly what the
  −3 packaging penalty is checking.

**Verification.** The zip is under 300 MB. Extracted to a clean folder on another machine, it launches and is completable.

---

### Step 8.2 — Rebuild the GDD as the required PowerPoint

**Goal.** S3, S7, S8. **The current GDD is a Word/PDF document and does not meet the brief's format.**

**What to do**
- Convert to **PowerPoint** — the brief says PowerPoint explicitly.
- **Slide 1 (S7):** participant name and ID (עפרי חמו, 211906813) **and a Known Bugs list.** Be honest here; an accurate
  known-bugs list reads as professionalism, and hiding a bug the examiner then finds reads far worse.
- **Slide 2 onward (S8):** one section per scene — MuseumNight, FrozenCity, ClockCore — each listing **which of the 21
  requirements appear in that scene**. Part 3 of this document is that content; copy it across.
- Include the **YouTube trailer link** (S3) on slide 1 or 2.
- Include the **repository download link** (S6).
- Update the design content to match what was actually built: three scenes, not seven; one ending, not three; the two
  added mechanics (Chrono Orb, first-person camera) documented as design.
- Keep the strong material from the existing GDD — concept, story, characters, items, the era table, the control table.
  It is good writing and it directly serves G1.

**Verification.** Slide 1 has names + known bugs. Every scene has its own requirement listing. Both links open.

---

### Step 8.3 — The trailer

**Goal.** S4 and G2 — up to 5 points, for at most 75 seconds of video.

**What to do**
- **Maximum 1:15.** Shorter is allowed and usually better. Target 60 seconds.
- Structure: museum at night and clocks stopping (0–10s) → the Clock of Creation shatters (10–18s) → era switching, the
  same place in three colours (18–35s) → stealth past a Warden, slow-time, the orb ringing the bell (35–55s) →
  the Collector, title card (55–65s).
- Record at 1080p60 with the HUD hidden, using a clean camera path. The brief explicitly warns against a careless
  screen recording (*"לא הקלטה רשלנית"*) — this is a scored deliverable, not documentation.
- Cut to music with real beat alignment. The era-switch VFX is your best cut point.
- Upload to YouTube (unlisted is fine), and put the link in the GDD.

**Verification.** Under 1:15, 1080p, cut to music, shows every headline mechanic, and would make a stranger want to play it.

---

### Step 8.4 — Compliance audit and evidence capture

**Goal.** Walk into the defense knowing every one of the 21 items is provable.

**What to do**
- Fill in Part 8 below completely.
- For each requirement, capture a screenshot or a 5–10 second clip, named `T07_patrol_pause.mp4` and so on.
- For each, write the **file and line** where it is implemented — D3 is a source-code defense and being able to jump
  straight to `WardenAI.cs:112` is worth real points.
- Verify the two most-missed items specifically: T21 anchors exist **only** from scene 2, and T18's minimap covers a
  **whole** scene.

**Verification.** Every row in Part 8 reads *Done*, with an evidence file and a code reference.

---

### Step 8.5 — Defense preparation

**Goal.** D1–D6, including the 5 points for the game running on your own machine.

**What to do**
- **D2 — 5 points.** Run the *shipped build* on your defense machine, from the extracted zip, more than once, on the day.
  Have it already open before the call starts.
- **D3 — source-code defense.** Be able to open and explain, cold: `PlayerController`, `WardenAI`, `EraManager`,
  `TimeAnchor`, `ChronoOrb`, `GameManager`/`GameState`. Know why the `LayerMask` is built in code and why the two
  NavMesh surfaces are baked separately.
- **D5 — live edit.** This is the clause people are least ready for. Rehearse, on a timer:
  - *Add:* drop a new Time Shard into the scene and make it score; add a waypoint to a patrol route; add a new
    interactable plaque; add a third LOD instance.
  - *Remove:* delete an enemy and show the scene still completes; disable the minimap; disable slow-time.
  - Keep the code structured so these are five-second edits — that is what Step 0.3's folder work was for.
- **D6 —** book your slot the moment the scheduling sheet is published; the defense runs in the 3–4 days before the formal date.
- Prepare a two-minute spoken walkthrough of the game, and open with the trailer — the brief says the defense **starts
  from the trailer**.

**Verification.** A rehearsal where you run the build, explain three scripts unprompted, and add and remove an element in under two minutes each.

---

# Part 6 — Priority order if time runs short

If the schedule compresses, this is the order to protect. Higher items lose more points if dropped.

| Tier | Items | Rationale |
|---|---|---|
| **1 — never cut** | Phase 0 (compiles at all), T12, T1, T8, T9, T21, T13, T16, T17, T7 | Non-negotiable graded items; T21 and T13 have the most specific wording and are the easiest to fail on a technicality |
| **2 — high value** | T20, T6, T10, T11, T5, T15, T14, T18, T19, T2, T3, T4 | Each is a discrete graded element; each is individually achievable in a day or less |
| **3 — points multiplier** | G1 (era system + coherence), G2 (trailer), S9 | 10 points combined, plus the brief's stated primary criterion. Do not let these be what gets cut |
| **4 — mandatory admin** | S1–S8, D2 | Cheap in effort, expensive to miss. The −3 packaging penalty and D2's 5 points are pure avoidable loss |
| **5 — cut first** | Time Shadow, endings 2 and 3, GDD scenes 3–5, extra NPC dialogue, minimaps in scenes 2 and 3 | Already cut in Part 1.3. Do not un-cut them |

---

# Part 7 — Risk register

| Risk | Impact | Mitigation |
|---|---|---|
| Blender fracture (T10) and decimation (T11) left to the end | Two requirements lost outright | Do Steps 2.4 and 2.5 in the first half of the schedule, before the scenes are dressed |
| Build exceeds 300 MB | S1 failure, and the brief judges on weight | Budget per Step 8.1 from the start; check the Editor Log after every milestone build |
| T21 anchors placed in scene 1 | Requirement read as unmet | Part 3 flags this; verify in Step 8.4 |
| "Separate bake" (T13) done as one surface with two agents | Requirement unmet | Two `NavMeshSurface` components, verified visually in Step 4.1 |
| Animator imported instead of authored (T14) | Requirement explicitly excludes imports | Build both controllers by hand; be ready to open them in the defense |
| Trailer over 1:15 | S4 failure and G2 points | Target 60s; check the exported duration |
| `Time.timeScale` left below 1 after a pause during slow-time | Game-breaking bug found live in the defense | Explicit restore-to-1 in Step 5.2; add it to the regression pass |
| Scope creep back toward the GDD's 7 scenes | Everything slips | The cut list in Part 1.3 is final |

---

# Part 8 — Compliance matrix

Fill this in as you go. It is both your own tracker and the raw material for GDD slides 2+ (S8).

| Req | Requirement | Scene(s) | Implementation / script | Evidence file | Status |
|---|---|---|---|---|---|
| T1 | Entry + victory menus | MainMenu, Victory | | | ☐ |
| T2 | Dynamic 3D tutorial text | All | `TutorialText.cs`, world-space TMP | | ☐ |
| T3 | ≥4 triggers | All | `RoomEntry`, `Tutorial`, `EraZone`, `Hazard`, `TimeAnchor` | | ☐ |
| T4 | ≥3 collisions | All | `ChronoOrb`, debris, Warden contact, boss shield | | ☐ |
| T5 | Hinge joints | Museum, FrozenCity | Pendulum, bell, gate | | ☐ |
| T6 | Self-built Terrain | FrozenCity | Unity Terrain, 3 layers | | ☐ |
| T7 | Patrol with pause | FrozenCity, ClockCore | `PatrolRoute.cs`, `WardenAI.cs` | | ☐ |
| T8 | Score / health / energy | All | `GameManager`, `GameState` | | ☐ |
| T9 | Cross-scene + 2 items | All | `GameState` JSON, Lens + Hourglass | | ☐ |
| T10 | Voronoi fracture ×2 | Museum, FrozenCity | Blender Cell Fracture; `FracturedObject.cs` | | ☐ |
| T11 | LOD ×2 | Museum + reused | Blender Decimate; `LODGroup` | | ☐ |
| T12 | New Input System only | All | `MuseumInputActions`, `PlayerInputReader` | | ☐ |
| T13 | 2 agent types, separate bake | FrozenCity, ClockCore | 2× `NavMeshSurface`; `Seek`/`Flee`/`Pursue` | | ☐ |
| T14 | Animator ≥4 states, authored | All | `NoaController`, `WardenController` | | ☐ |
| T15 | Physical projectile | All | `ChronoOrbLauncher.cs` | | ☐ |
| T16 | Recast + stealth | FrozenCity, ClockCore | NavMeshSurface + detection meter | | ☐ |
| T17 | LayerMask in code | All | `LayerMask.GetMask(...)` in `WardenAI` | | ☐ |
| T18 | Minimap, whole scene | MuseumNight | Ortho camera → RenderTexture | | ☐ |
| T19 | FPS ⇄ 3rd person | All | `CM_FirstPerson`, `CM_ThirdPerson` | | ☐ |
| T20 | Two storeys + stairs | MuseumNight | ProBuilder museum | | ☐ |
| T21 | ≥2 hidden teleports, scene 2+ | FrozenCity, ClockCore | `TimeAnchor.cs` | | ☐ |
| S1 | ≤300 MB compressed | — | | | ☐ |
| S2 | Build + GDD uploaded, packaging verified | — | | | ☐ |
| S3 | GDD as PowerPoint + YouTube link | — | | | ☐ |
| S4 | Trailer ≤1:15 | — | | | ☐ |
| S5 | EXE uploaded with GDD | — | | | ☐ |
| S6 | Repository + link in GDD | — | | | ☐ |
| S7 | GDD p.1 names + known bugs | — | | | ☐ |
| S8 | GDD p.2+ per-scene requirement map | — | | | ☐ |
| S9 | Coherent link across scenes | All | Lens → scene 2 → Hourglass → scene 3 | | ☐ |
| S10 | Scale / realism | All | Scale audit | | ☐ |
| G1 | Interest and ambition | All | Era system, consequence puzzles | | ☐ |
| G2 | Trailer quality | — | | | ☐ |
| D2 | Build runs on defense machine | — | | | ☐ |
| D5 | Live add/remove rehearsed | — | | | ☐ |

---

## Immediate next action

**Step 0.1.** Delete `Assets/Assets/Scripts/Core/PlayerInputReader.cs` and its `.meta`, keeping
`Assets/Scripts/Player/PlayerInputReader.cs`. Nothing else in this plan can be verified until the project compiles.
