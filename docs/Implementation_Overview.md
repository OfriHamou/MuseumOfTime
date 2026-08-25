# Museum of Time — Implementation Overview

**Student:** עפרי חמו — 211906813 · **Engine:** Unity 6000.4.8f1, URP · **Full detail:** `docs/Implementation_Plan.md` · **Click-by-click:** `docs/Phase1_Unity_Walkthrough.md`, `docs/Phase2_Unity_Walkthrough.md`

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

## Phase 4 — AI, navigation, stealth

| ✅ | Step | What requirement this satisfies |
|---|---|---|
|  | **4.1 Two agent types, separate bakes** — `WardenAgent` and `ShadowAgent`, two `NavMeshSurface` components baked separately, with a Shadow-only area so the two genuinely travel different routes. Unity's NavMesh bake *is* Recast. | **Pathfinding with two agent types on different routes, each with its own bake** [T13]; **the Recast navmesh half of the stealth requirement** [T16] |
|  | **4.2 Warden patrol with pause** — a waypoint route with a per-point wait; the Warden fully stops and scans for 2–4 seconds. The pause is the graded part. | **Patrol with pause** [T7] |
|  | **4.3 Vision, layer mask, stealth** — range, then cone angle, then a line-of-sight raycast against a mask **written in code**; hide volumes, a detection meter, capture when it fills. | **Stealth working against the navmesh** [T16]; **a LayerMask defined in code rather than the inspector** [T17] |
|  | **4.4 Steering behaviours** — explicit pursue (aims at predicted position), seek and flee, named so they are obvious in a code review. | **A clear AI steering element — seek, flee or pursue** [T13] |
|  | **4.5 Chronological Shadow** — the second agent type as a real character: crosses ledges Wardens cannot, steals Time Shards, recoverable by freezing it. | Makes the two agent types genuinely different [T13]; gives score a real way to be lost [T8] |
|  | **4.6 Enemy Animator** — hand-author `WardenController` with Patrol / Alert / Chase / Attack (+ Frozen), driven by the detection meter. | **An Animator you defined yourself, with at least 4 states** [T14] |

## Phase 5 — UI and readability

| ✅ | Step | What requirement this satisfies |
|---|---|---|
|  | **5.1 Main menu and victory screen** — New Game / Continue / Controls / Quit; victory shows score, shards, detections and playtime. | **Entry menu and victory menu** [T1] |
|  | **5.2 HUD and pause menu** — event-driven bars and counters; pause must restore normal time exactly, not the slow-time value. | Makes health, energy and score visible to the player [T8] |
|  | **5.3 Minimap** — an orthographic camera rendered to a texture, rotating with Noa, live for the **whole** of MuseumNight. Never shows hidden anchors. | **A simple minimap for orientation, present throughout at least one entire scene** [T18] |
|  | **5.4 Dynamic 3D tutorial text** — world-space TextMeshPro exhibit plaques whose text changes with player state. Screen-space UI does not satisfy the "in 3D" clause. | **In-game tutorial with dynamic text and clear instructions, in 3D** [T2] |

## Phase 6 — Scene content

| ✅ | Step | What requirement this satisfies |
|---|---|---|
|  | **6.1 MuseumNight complete** — teach every verb via plaques, force the staircase, shatter the Clock of Creation, introduce one Warden, end by granting the Time Lens. | Delivers in one scene: the two-storey building, the minimap, both cameras, the 3D tutorial, the player Animator and the first fracture |
|  | **6.2 FrozenCity complete** — Terrain approach, era switching unlocks, the bell-never-rang gear puzzle across three eras, patrols and Shadows, two hidden anchors, statue shatters, grants the Hourglass. | Delivers: the Terrain, the hidden teleports, both AI agent types, stealth, patrol with pause, the hinge bell, the projectile, the second fracture and the second acquired item |
|  | **6.3 ClockCore complete** — the inverted museum, then a three-phase boss where the era switch *is* the fight mechanic; unwinnable without the Hourglass. | Proves the acquired items matter, and uses collisions, triggers, stealth and teleports together |
|  | **6.4 Coherence pass** — verify Lens → scene 2 → Hourglass → scene 3 end to end; consistent palette, effects and narrative thread; play the whole game in one sitting. | **Three gameplay scenes with a coherent logical connection between them** [S9]; the brief's demand for precision over an eclectic collection of features [G1] |

## Phase 7 — Polish

| ✅ | Step | What requirement this satisfies |
|---|---|---|
|  | **7.1 Audio** — per-scene ambience, effects on every player action, a mixer snapshot with a low-pass filter that engages during slow-time. Compress hard. | How interesting the game feels [G1] and how well the trailer lands [G2]; keeps the build under 300 MB [S1] |
|  | **7.2 Lighting and visual effects** — bake where possible; per-era colour grading (sepia / neutral / cyan) so the era reads from a still frame; hold framerate on the defense machine. | How interesting the game looks [G1], trailer quality [G2], and the build running smoothly on your own machine in the defense [D2] |

## Phase 8 — Submission and defense

| ✅ | Step | What requirement this satisfies |
|---|---|---|
|  | **8.1 Build, size budget, packaging** — Windows x64; enforce the 300 MB compressed cap using the Editor Log breakdown; zip it, extract to a clean folder, and run it from there. | **Under 300 MB compressed** [S1]; **the EXE uploaded and the packaging verified to work — a −3 penalty if it does not** [S2, S5] |
|  | **8.2 Rebuild the GDD as PowerPoint** — page 1 name and Known Bugs; page 2 onward a per-scene list of which requirements appear in that scene; trailer and repository links. | **The GDD must be a PowerPoint with a YouTube trailer link** [S3]; **page 1 names and known bugs** [S7]; **page 2 onward the per-scene requirement listing** [S8] |
|  | **8.3 The trailer** — max 1:15, target 60 s, 1080p, HUD hidden, cut to music, uploaded to YouTube. Explicitly not a careless screen recording. | **A trailer of at most 1:15** [S4]; **trailer quality, worth up to 5 points** [G2] |
|  | **8.4 Compliance audit and evidence** — fill the compliance matrix; capture a clip per requirement; note the file and line where each is implemented. | Proves every one of the 21 technical requirements is present, and supplies the content for the GDD's per-scene pages |
|  | **8.5 Defense preparation** — run the shipped build on your own machine beforehand; rehearse explaining the AI, era and teleport scripts cold; rehearse adding and removing an element on a timer. | **The game running on your own machine, worth 5 points** [D2]; **a defense on the source code** [D3]; **adding and removing basic elements live** [D5] |

## Critical path and risk

**Never cut.** Phase 0 first, because nothing compiles without it. Then: only the new Input System, the menus, health/energy/score, data carried between scenes, the hidden teleports, the two AI agent types, stealth, the code-written layer mask, and patrol with pause. The teleport and two-agent requirements have the most specific wording in the brief and are the easiest to fail on a technicality.

**Start out of order.** Steps 2.4 and 2.5 — the Blender fracture and polygon reduction. They are the most common cause of a missing requirement, because they depend on an external tool and get left until the end.

**Cheapest points available.** The submission rules and running the build on your own machine. The packaging penalty is −3 and the machine check is worth 5, both for work that is administrative rather than technical.

**Highest-value work.** Step 3.5, the era system, and Step 6.4, the coherence pass. The brief states the game is judged first and foremost on how interesting it is.
