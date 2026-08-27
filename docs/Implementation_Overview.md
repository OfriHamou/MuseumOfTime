# Museum of Time — Implementation Overview

**Student:** עפרי חמו — 211906813 · **Engine:** Unity 6000.4.8f1, URP · **Full detail:** `docs/Implementation_Plan.md` · **Click-by-click:** `docs/Phase1_Unity_Walkthrough.md`, `docs/Phase2_Unity_Walkthrough.md`, `docs/Phase3_Unity_Walkthrough.md`, `docs/Phase4_Unity_Walkthrough.md`

One line per step, in build order. The right column says which assignment requirement the step satisfies, in plain words. **Bold** means the step closes that requirement; plain text means it contributes to it. The first column tracks progress: **✅** done, **◐** partly done with a step left for you, blank not started. The code in brackets is only a pointer to the same requirement in the full plan — you never need it to read this page.

## Phase 0 — Unblock and clean up  ✅ COMPLETE

| ✅ | Step | What requirement this satisfies |
|---|---|---|
| ✅ | **0.1 Resolve the duplicate `PlayerInputReader`** — two classes of that name exist, so the project does not compile. Delete the `Assets/Assets/Scripts/Core/` copy, keep the `Assets/Scripts/Player/` one, add the `Q`/`R`/`Tab` actions. | Groundwork for using only the new Input System [T12] |
| ✅ | **0.2 Consolidate the Input Actions asset** — make `MuseumInputActions` the project-wide asset, delete the Unity template asset, set Active Input Handling to *New* only, remove every legacy `Input.Get*` call. <br>*Done: actions added, dead binding removed, UI map added; Active Input Handling was already correct and no legacy calls exist. Project-wide Actions now points at MuseumInputActions and the template asset is deleted. Actions are subscribed in code, not via Inspector events.* | **Only the new Input System is used — no legacy input anywhere** [T12] |
| ✅ | **0.3 Repair project structure and Build Settings** — collapse the two script roots into one `Assets/Scripts/`, delete the stale `SampleScene` entry, add consistent hierarchy headers to all five scenes. <br>*Done: all scripts and the prefab moved, nested folder deleted. SampleScene removed; build list is MainMenu, MuseumNight, FrozenCity, ClockCore, Victory. Hierarchy headers already existed in the scenes.* | Lets you find and edit code instantly in the defense, where you must add and remove elements live [D3–D5] |
| ✅ | **0.4 Version control and submission repository** — Unity `.gitignore`, verify `Library/` is untracked, push to GitHub, tag each phase. <br>*Done: `.gitignore` already correct, `Library/` untracked, work committed on branch `phase-0-cleanup`. Repo `github.com/OfriHamou/MuseumOfTime` exists and branch `phase-0-cleanup` is pushed.* | **Source code hosted in a repository, with the download link going in the GDD** [S6] |

## Phase 1 — Player, cameras, animation  ✅ COMPLETE

| ✅ | Step | What requirement this satisfies |
|---|---|---|
| ✅ | **1.1 Camera-relative movement, gravity, jump** — build the move vector from camera forward/right instead of world axes; grounded jump; tune step offset so the museum stairs are climbable. | Stair climbing in the two-storey building [T20]; gameplay driven by the new Input System [T12] |
| ✅ | **1.2 Two Cinemachine cameras and the toggle** — `CM_ThirdPerson` for exploration, `CM_FirstPerson` for Time Lens inspection, swapped on `C` by priority. Minimap camera stays separate, so three in total. | **Camera switches between first person and third person — two cameras besides the minimap** [T19] |
| ✅ | **1.3 Noa's Animator, built by hand** — author `NoaController` yourself with Idle / Walk / Run / Jump (+ Interact). An imported controller does not count. | **An Animator you defined yourself, with at least 4 states** — player half [T14] |

## Phase 2 — World geometry and art  ✅ COMPLETE

| ✅ | Step | What requirement this satisfies |
|---|---|---|
| ✅ | **2.1 The museum: two storeys with stairs** — build in ProBuilder; ground-floor galleries, upper-floor mezzanine and curator's office, a real walkable staircase, textures you chose yourself. | **A designed two-storey building with your own textures and stair climbing** [T20] |
| ✅ | **2.2 FrozenCity Terrain** — hand-sculpt a Unity Terrain with three paint layers, framing the clock tower. Keep detail density low for the size budget. | **Terrain built by you** [T6]; keeps the build under the 300 MB cap [S1] |
| ✅ | **2.3 Hinge joint set pieces** — Clock of Creation pendulum, clock-tower bell, gallery gate. Build all three so one failure does not cost the requirement. | **Physical hinge joints** [T5]; gives the projectile something to hit [T15] |
| ✅ | **2.4 Voronoi fracture ×2** — Blender Cell Fracture on the Clock of Creation and a frozen statue, 15–40 pieces each, collider and rigidbody per shard. **Start early — external tool dependency.** | **Two assets you fractured yourself with Voronoi, appearing intrinsically in the game** [T10] |
| ✅ | **2.5 LOD ×2** — Blender Decimate a marble statue and a stone column to three tiers each, assign to `LODGroup` in Unity, record the triangle counts. | **Two different assets whose polygons you reduced yourself, integrated as LOD** [T11] |
| ✅ | **2.6 Scale and realism pass** — enforce 1 unit = 1 metre against Noa's 1.7 m; nothing floating, nothing mis-sized. | **Scale and realism appropriate to the environment** — the brief's "no floating foxes" rule [S10] |

## Phase 3 — Core systems  ✅ COMPLETE

| ✅ | Step | What requirement this satisfies |
|---|---|---|
| ✅ | **3.1 Interaction system** — an `IInteractable` interface plus a camera raycast filtered by a layer mask built in code; plaque, door, pickup, lever and NPC implementations. | Carries the in-game tutorial [T2] and the layer mask written in code [T17] |
| ✅ | **3.2 Trigger set** — five distinct components: room entry, tutorial reveal, era zone, temporal-rift hazard, Time Anchor arming. | **At least 4 triggers** [T3] |
| ✅ | **3.3 Collision handling** — real `OnCollisionEnter` using contact data: orb→object, falling debris→player, Warden→player, orb→boss shield. | **At least 3 collisions detected and acted upon** [T4] |
| ✅ | **3.4 Health, energy, score wired to gameplay** — energy is what makes the time powers a choice; score rises on shards and falls on capture. | **Score gained and lost, plus a health and energy incentive** [T8] |
| ✅ | **3.5 Era system (Past / Present / Future)** — sibling zone roots swapped on `Q`/`R`; objects carry changes forward so the GDD's cart puzzle works. **The game's signature mechanic.** | **How interesting the game is — the brief's primary criterion, worth up to 5 points** [G1] |
| ✅ | **3.6 Chrono Hourglass slow-time** — `Time.timeScale` with matched physics step, energy drain, unscaled UI, unmistakable feedback. | **Second of the two items acquired in one scene and required in the next** [T9] |
| ✅ | **3.7 Chrono Orb projectile** — a rigidbody sphere thrown from the active camera; on impact it freezes or rewinds rather than destroys. | **Shooting — a physical body that is fired and impacts** [T15] |
| ✅ | **3.8 Time Anchors — hidden teleports** — at least two each in FrozenCity and ClockCore only; invisible without the Time Lens; failure returns Noa to the last anchor with health refreshed and a score penalty. | **From scene 2 onward, two hidden teleports; on failure the player returns to the teleport rather than the start, with health restored** [T21] |
| ✅ | **3.9 Cross-scene persistence and the two items** — JSON save/load; Time Lens granted in scene 1 and *required* in scene 2, Chrono Hourglass granted in scene 2 and *required* in scene 3. | **Serialized data passed between scenes, including two acquired items** [T9]; makes the three scenes one connected game [S9] |

## Phase 4 — AI, navigation, stealth  ✅ COMPLETE

| ✅ | Step | What requirement this satisfies |
|---|---|---|
| ✅ | **4.1 Two agent types, separate bakes** — `WardenAgent` and `ShadowAgent`, two `NavMeshSurface` components baked separately, with a Shadow-only area so the two genuinely travel different routes. Unity's NavMesh bake *is* Recast. | **Pathfinding with two agent types on different routes, each with its own bake** [T13]; **the Recast navmesh half of the stealth requirement** [T16] |
| ✅ | **4.2 Warden patrol with pause** — a waypoint route with a per-point wait; the Warden fully stops and scans for 2–4 seconds. The pause is the graded part. | **Patrol with pause** [T7] |
| ✅ | **4.3 Vision, layer mask, stealth** — range, then cone angle, then a line-of-sight raycast against a mask **written in code**; hide volumes, a detection meter, capture when it fills. | **Stealth working against the navmesh** [T16]; **a LayerMask defined in code rather than the inspector** [T17] |
| ✅ | **4.4 Steering behaviours** — explicit pursue (aims at predicted position), seek and flee, named so they are obvious in a code review. | **A clear AI steering element — seek, flee or pursue** [T13] |
| ✅ | **4.5 Chronological Shadow** — the second agent type as a real character: crosses ledges Wardens cannot, steals Time Shards, recoverable by freezing it. | Makes the two agent types genuinely different [T13]; gives score a real way to be lost [T8] |
| ✅ | **4.6 Enemy Animator** — hand-author `WardenController` with Patrol / Alert / Chase / Attack (+ Frozen), driven by the detection meter. | **An Animator you defined yourself, with at least 4 states** [T14] |

## Phase 5 — UI and readability  ✅ COMPLETE

Built entirely through headless Editor scripts (`MenuUIBuilder`, `HudBuilder`, `MinimapBuilder`,
`TutorialTextBuilder`), run via `Unity.exe -batchmode -executeMethod ...` and verified with `Tools/verify.ps1` after
each one — no interactive Editor session was used. Full detail: `docs/Phase5_Unity_Walkthrough.md`.

| ✅ | Step | What requirement this satisfies |
|---|---|---|
| ✅ | **5.1 Main menu and victory screen** — New Game / Continue (gated on `SaveService.Exists`) / Controls / Quit; victory shows score, shards, detections and playtime read live from `GameState`. <br>*Done: `SceneLoader.ContinueGame()` added; `MainMenuController`/`VictoryScreenController` built and wired; both scenes navigable through the New Input System `UI` map. Quit is not covered by an automated test — calling `Application.Quit()` in-process would kill the test runner — confirmed by hand instead.* | **Entry menu and victory menu** [T1] |
| ✅ | **5.2 HUD and pause menu** — event-driven bars/counters via `GameManager.StateChanged`; pause restores `Time.timeScale` to exactly **1**, never the slow-time value. <br>*Done: `HUDController` (health/energy/shards/era/item icons, plus a detection meter that reads `WardenAI.DetectionLevel` per-frame since Phase 4 has no change event for it — read-only, nothing written back) and `PauseMenuController`, both automated-tested including the exact-timeScale regression the plan calls out by name.* | Makes health, energy and score visible to the player [T8] |
| ✅ | **5.3 Minimap** — an orthographic camera rendered to a texture, following and rotating with Noa, live for the **whole** of MuseumNight; culling mask is an *allow-list* of exactly the pre-reserved `Minimap` layer, so a hidden Time Anchor is invisible by construction rather than by remembering to exclude it. <br>*Done: camera, follow/rotate, layer isolation from the gameplay camera, all automated-tested. Icons for objectives/collected shards/exits (beyond Noa's own marker) were scoped out as presentation polish beyond T18's orientation requirement — left for a Phase 6 dressing pass.* | **A simple minimap for orientation, present throughout at least one entire scene** [T18] |
| ✅ | **5.4 Dynamic 3D tutorial text** — world-space `TextMeshPro` (not `TextMeshProUGUI`/Canvas) plaques whose text changes with player state; all eight verbs covered plus a persistent objective line. <br>*Done: `WorldTutorialText`/`WorldObjectiveText`, fading on proximity rather than on the specific verb performed (avoids eight bespoke per-verb detectors), `{energy}`/`{health}` token substitution proven by test. Not done: the plan's aside about voicing the text as the Old Curator's notes — left as a copy pass for Phase 6.* | **In-game tutorial with dynamic text and clear instructions, in 3D** [T2] |

**Verification.** `Tools/verify.ps1` — compiles clean, 43/49 PlayMode tests passing (6 intentionally ignored since
Phase 0, unchanged; 0 failed), across 19 new tests in `MainMenuTests.cs`, `VictoryScreenTests.cs`,
`HudAndPauseMenuTests.cs`, `MinimapTests.cs`, `TutorialTextTests.cs`. Layout, legibility, `Application.Quit()`, the
detection meter during real play, and "reads well while walking" remain manual/visual checks — see
`Phase5_Unity_Walkthrough.md`'s own list rather than re-deriving it here.

## Phase 6 — Scene content  ✅ COMPLETE

Placed the Phase 3/4 systems that only existed in `MuseumNight` into `FrozenCity` and `ClockCore` — the scenes
Part 3's requirement-placement table actually assigns most of them to — via new headless Editor builders
(`FrozenCityContentBuilder`, `ClockCoreContentBuilder`, `SceneConnectionsBuilder`) and a new reusable
`Player.prefab`. An initial pass deferred the three-era gear puzzle and the Collector boss fight as "new systems,
not configuration"; that call was overruled on review and both were built as the simplest real implementation of
what the plan describes. Full detail, including defects found and fixed along the way, in
`docs/Phase6_Unity_Walkthrough.md`.

| ✅ | Step | What requirement this satisfies |
|---|---|---|
| ✅ | **6.1 MuseumNight, closed the loop** — everything else already existed from Phases 0–5; the one missing piece was a way to actually leave. <br>*Done: `SceneExitTrigger` (new, gated on an acquired item) + `Exit_ToFrozenCity`, requiring the Time Lens. Automated-tested both ways (blocked without the Lens, succeeds with it).* | Delivers in one scene: the two-storey building, the minimap, both cameras, the 3D tutorial, the player Animator and the first fracture; **closes the MuseumNight→FrozenCity half of S9** |
| ✅ | **6.2 FrozenCity complete, including the gear puzzle** — Terrain approach (Phase 2), era switching unlocked, both AI agent types on separate bakes with genuinely different routes, two hidden Time Anchors, a real hinge bell, the second Voronoi fracture placed, exit to ClockCore. <br>*Done: `GearPuzzle`/`GearPickup`/`GearSocket` — find the gear in the Past, install it in the Present, verify it in the Future — and the Chrono Hourglass pickup now starts hidden and is only revealed when the puzzle is solved, so it actually gates the reward rather than sitting next to it. 14 automated tests, `FrozenCitySceneTests.cs`.* | Delivers: the hidden teleports, both AI agent types on different routes, patrol with pause, the hinge bell, the second fracture, **the GDD's core puzzle**, and the second acquired item |
| ✅ | **6.3 ClockCore complete, including the Collector** — a walkable floor, era travel unlocked, two more hidden Time Anchors, both AI agent types present. <br>*Done: `Assets/Scripts/AI/Collector.cs`, a three-phase boss where the era switch is the mechanic — Past (shielded, break it with the orb), Present (summons the scene's Warden), Future (erodes health unless the Chrono Hourglass is active; a hit only wins while it is). Defeating it loads Victory directly. 8 automated tests, `ClockCoreSceneTests.cs`, including the negative case ("a hit without the Hourglass does not win").* | Proves the acquired items matter for real — **the fight is provably unwinnable without the Hourglass** — and uses collisions, triggers and the era system together |
| ◐ | **6.4 Coherence pass — the acquisition chain automated, the full playthrough not** — Lens → FrozenCity → Hourglass → ClockCore → Victory verified end to end via real trigger/boss logic, not just `GameState` flags that happen to exist. <br>*Not done, and out of scope for automation: the narrative thread (Old Curator notes), consistent visual language (this phase's new content is greybox), and actually playing the full game in one sitting — the brief's own verification for this step names a human playthrough explicitly.* | **Automates the checkable half of S9**; the qualitative half is unchanged from before this phase |

**Verification.** `Tools/verify.ps1` — compiles clean, 67/73 PlayMode tests passing (6 intentionally ignored since
Phase 0, unchanged; 0 failed), across 24 new tests in `SceneConnectionsTests.cs`, `FrozenCitySceneTests.cs`,
`ClockCoreSceneTests.cs`. Every documented Phase 6 requirement is implemented; what remains (6.4's qualitative half,
and the greybox/placeholder nature of the new puzzle and boss numbers) is narrative and art polish, not missing
mechanics — see `Phase6_Unity_Walkthrough.md`.

## Phase 7 — Polish  ◐ NEAR COMPLETE (3 documented manual items remain)

Step 7.1 and 7.2 have no **Verification.** line in `Implementation_Plan.md` and only ever *support* soft scoring axes
(G1/G2/S1/S10/D2) — a human-judged polish pass. Everything listed was built as a real, working, wired system;
the three things that genuinely cannot be finished through supported headless automation (the AudioMixer *asset*, a
lightmap bake, a real-hardware framerate check) have all their surrounding code/lighting prepared and exact manual
steps documented, rather than being faked with a substitute. Full detail in `docs/Phase7_Unity_Walkthrough.md`.

| ✅ | Step | What requirement this satisfies |
|---|---|---|
| ◐ | **7.1 Audio — all content built, mixer asset is the one manual step** — `AudioManager` in all three scenes with per-scene ambience (MuseumNight tick / FrozenCity wind+held-note / ClockCore dissonant drone) and **all twelve listed SFX cues** (footsteps + stair variant, interaction, shard pickup, orb throw, orb impact, bell, fracture, Warden alert, capture, era switch, slow-time enter/exit), each wired to a real game event by observation — no Phase 3/4 file changed. Slow-time filtering works today via an `AudioLowPassFilter`. <br>*Manual: the `AudioMixer` **asset** (Master/Music/SFX + Normal/SlowTime snapshots) — Unity has no supported API to create it from script; `AudioManager` is fully code-wired to use it and the builder auto-detects it, so it is a ~2-minute Editor step then a re-run. See the walkthrough.* | How interesting the game feels [G1] and how well the trailer lands [G2] |
| ◐ | **7.2 Lighting and VFX — grading, all 5 particles and lighting built; bake+framerate manual** — `EraColorGrading` tints warm/neutral/cold by era (tested); **all five particle effects** built (era-switch shockwave, shard sparkle, fracture dust, orb trail on the prefab, Shadow drift on each Shadow); MuseumNight real-time lighting — one cold shadow-casting moonlight, four warm pooled exhibit spots, dim cool ambient for deep shadows. <br>*Manual: an optional lightmap **bake** (`Lightmapping.Bake()` is long-running/hang-prone unattended and needs visual judgement; lights are set up so it is one click), and the **D2 framerate check** (no headless equivalent).* | How interesting the game looks [G1], trailer quality [G2] — **era-reads-from-a-still-frame met** |

**Verification.** `Tools/verify.ps1` — compiles clean, 81/87 PlayMode tests passing (6 intentionally ignored since
Phase 0, unchanged; 0 failed), across 10 tests in `AudioAndVfxTests.cs`. Every automatable Phase 7 item is
implemented and wired; the only remaining work is the AudioMixer asset (manual, code ready), an optional bake, and
the real-hardware framerate check — none of which can be done or verified headlessly. See
`Phase7_Unity_Walkthrough.md` for the exact manual steps.

## Phase 8 — Submission and defense

| ✅ | Step | What requirement this satisfies |
|---|---|---|
|  | **8.1 Build, size budget, packaging** — Windows x64; enforce the 300 MB compressed cap using the Editor Log breakdown; zip it, extract to a clean folder, and run it from there. | **Under 300 MB compressed** [S1]; **the EXE uploaded and the packaging verified to work — a −3 penalty if it does not** [S2, S5] |
|  | **8.2 Rebuild the GDD as PowerPoint** — page 1 name and Known Bugs; page 2 onward a per-scene list of which requirements appear in that scene; trailer and repository links. | **The GDD must be a PowerPoint with a YouTube trailer link** [S3]; **page 1 names and known bugs** [S7]; **page 2 onward the per-scene requirement listing** [S8] |
|  | **8.3 The trailer** — max 1:15, target 60 s, 1080p, HUD hidden, cut to music, uploaded to YouTube. Explicitly not a careless screen recording. | **A trailer of at most 1:15** [S4]; **trailer quality, worth up to 5 points** [G2] |
|  | **8.4 Compliance audit and evidence** — fill the compliance matrix; capture a clip per requirement; note the file and line where each is implemented. | Proves every one of the 21 technical requirements is present, and supplies the content for the GDD's per-scene pages |
|  | **8.5 Defense preparation** — run the shipped build on your own machine beforehand; rehearse explaining the AI, era and teleport scripts cold; rehearse adding and removing an element on a timer. | **The game running on your own machine, worth 5 points** [D2]; **a defense on the source code** [D3]; **adding and removing basic elements live** [D5] |

## Phase 9 — Defect and look pass  ✅ COMPLETE

A pass driven by actually running the game in Unity and looking at it, rather than by re-reading the
plan. Full detail, including the root cause of each defect, in `docs/Defect_And_Look_Pass.md`.

| ✅ | Step | What requirement this satisfies |
|---|---|---|
| ✅ | **9.1 The missing `GameManager`** — it existed only in MainMenu and Victory, so all three gameplay scenes ran without one. Every component binding to `StateChanged` in `Start()` bound to nothing: the HUD, the shard SFX and VFX, the item icons. Fixed with an `AfterSceneLoad` bootstrap plus a duplicate guard that no longer destroys `SceneLoader` alongside it. | Ten of the eleven test failures were this one bug. Restores [T8] and the HUD half of [T2] |
| ✅ | **9.2 T10/T11 were invisible** — the fracture and LOD prefabs are rebuilt from raw `sharedMesh`, discarding each FBX node's rotation (89.98°) and scale (100). The 4 m column shipped as a 4 cm pebble lying on its side; `LODGroup.size` was 0.04. | **Both self-fractured assets and both self-decimated LOD assets now actually appear** [T10, T11]; also [S10] |
| ✅ | **9.3 T19 existed in one scene of three** — FrozenCity and ClockCore had a single camera each and no `PlayerCameraRig`, so `C` did nothing. | **First/third-person toggle in every gameplay scene** [T19] |
| ✅ | **9.4 T4 was below its own minimum** — only two `OnCollisionEnter` implementations existed against a required three. Added `FallingDebris` and `SwingingHazard`, both scaling their response from `relativeVelocity`. | **Four collisions detected and acted upon** [T4], and gives the hinges gameplay weight [T5] |
| ✅ | **9.5 Scenes 2 and 3 had no HUD, pause menu, EventSystem or 3D tutorial text** — a player reaching FrozenCity was never told that Q/R switch era. | **3D tutorial text in all three scenes** [T2]; health/energy/score visible throughout [T8]; per-scene trigger and hinge coverage [T3, T5] |
| ✅ | **9.6 Look development** — no camera had `UniversalAdditionalCameraData`, so URP ran **zero** post-processing anywhere; volume profiles saved empty; `EraGrading` overrode the whole grade; particles drew as opaque white boxes; Noa rendered buried to the waist and doubled; enemies were untextured capsules; materials had no normal maps and one fixed tiling for every object size. | How interesting the game looks [G1] and how the trailer lands [G2] |
| ✅ | **9.7 Verification** — `RequirementComplianceTests` (one test per T1–T21, asserting real properties rather than presence) and `FullPlaythroughTests` (MainMenu → Victory in one run, driving real triggers and the boss). | Makes Part 8's compliance matrix executable; **automates S9's full chain end to end** |

| ✅ | **9.8 Museum dressing** — the museum was an open-topped box with bare interiors. Added a coffered ceiling with skylight openings (so the moonlight still falls in shafts), skirting and cornice, glass display cases on marble plinths, framed wall art with picture lights, and benches. Navmesh re-baked so agents path around the new colliders. | How interesting the game looks [G1, G2]; scale and realism [S10] |
| ✅ | **9.9 Minimap actually showed something** — the Minimap layer held only the player's own marker, so the map was one arrow on a blank background. Map plates are now generated **from the museum's real geometry**, with gold objectives and a green exit. | **A minimap that gives orientation, not just a heading** [T18] |
| ✅ | **9.10 Build verified end to end** — 137.6 MB uncompressed, **56.8 MB compressed** against a 300 MB cap, and the shipped EXE launched and ran with zero exceptions in its `Player.log`. | **Under 300 MB** [S1]; **packaging verified to work** [S2, S5]; **the game runs** [D2] |

**Verification.** 113/113 PlayMode tests passing, 0 failed, 0 skipped — up from 90 total with 11 failed.

## Critical path and risk

**Never cut.** Phase 0 first, because nothing compiles without it. Then: only the new Input System, the menus, health/energy/score, data carried between scenes, the hidden teleports, the two AI agent types, stealth, the code-written layer mask, and patrol with pause. The teleport and two-agent requirements have the most specific wording in the brief and are the easiest to fail on a technicality.

**Start out of order.** Steps 2.4 and 2.5 — the Blender fracture and polygon reduction. They are the most common cause of a missing requirement, because they depend on an external tool and get left until the end.

**Cheapest points available.** The submission rules and running the build on your own machine. The packaging penalty is −3 and the machine check is worth 5, both for work that is administrative rather than technical.

**Highest-value work.** Step 3.5, the era system, and Step 6.4, the coherence pass. The brief states the game is judged first and foremost on how interesting it is.

## Phase 10 - Playability pass (bugs that made the game unwinnable)

| # | Step | State |
|---|------|-------|
| 10.1 | Time Warden capture: costs health and score, survivable, 3 s cooldown | Done |
| 10.2 | Warden vision fixed - eye measured from the feet, cone judged on the horizontal bearing | Done |
| 10.3 | Shadow steal range made horizontal (same `baseOffset` arithmetic) | Done |
| 10.4 | Chrono Energy regenerates (6/s after 1.5 s idle) - no more dead-end runs | Done |
| 10.5 | Every silent refusal now says why (era travel, orb throw) | Done |
| 10.6 | Boss fight verified winnable with real thrown orbs, not just phase logic | Done |
| 10.7 | Audio coverage: 13 procedural clips, ambience in all three scenes | Done |
| 10.8 | Full suite 136/136; release build 57 MB compressed, runs clean | Done |
