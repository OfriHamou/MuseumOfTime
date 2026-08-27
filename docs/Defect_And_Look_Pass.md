# Defect and Look Pass

What was actually broken, what was fixed, and how each fix is verified. Written after a pass that
started from "the game has many bugs, does not do what the requirements say, and looks horrible."

Everything here was found by **running the game in Unity and looking at it**, or by running the PlayMode
suite — not by reading the plan and assuming.

---

## 1. The one bug behind ten test failures

The PlayMode suite reported **11 failures out of 90**. Ten of them were the same defect.

**`GameManager` existed only in `MainMenu` and `Victory`.** None of the three gameplay scenes contained
one, and nothing created it. Every component that binds to `GameManager.StateChanged` in `Start()` —
the HUD, the shard pickup SFX, the shard sparkle VFX, the acquired-item icons — found
`GameManager.Instance` null, silently skipped the subscription, and never updated again.

It only *appeared* to work when entering through the main menu, because that scene's own instance
survived via `DontDestroyOnLoad`. Pressing Play directly in `MuseumNight` — what the test suite and
every development iteration does — had no manager at all.

**Fix:** a `[RuntimeInitializeOnLoadMethod(AfterSceneLoad)]` bootstrap on `GameManager`. It runs after
the first scene's `Awake`/`OnEnable` and before any `Start()`, so a scene that carries its own manager
still wins and nothing is duplicated.

The duplicate guard was also made safe: it called `Destroy(gameObject)`, and in `MainMenu` that object
also carries `SceneLoader`. It now drops only the duplicate component unless the object exists solely
to host it.

The eleventh failure was a test artifact, not a game bug: `MuseumNightSceneTests` adds its own
`Keyboard` device, so in the Editor (where a real keyboard already exists) every binding resolves once
per device and `move.controls.Count` was 8, not 4. The assertion now checks **which** controls are
bound — that WASD drives Move — which is environment-independent.

---

## 2. Requirement defects

These were present in the hierarchy and absent in play.

### T10 / T11 — the Voronoi and LOD assets were ~1 cm objects lying on their side

`AssetPrefabBuilder` rebuilds the fracture and LOD prefabs from each imported node's `sharedMesh`, onto
brand-new `GameObject`s with identity transforms. The FBX nodes carry **rotation (89.98, 0, 0)** — the
Blender Z-up to Unity Y-up conversion — and **scale 100**, which the importer's `fileScale` of 0.01
cancels back to 1. Copying only the mesh discarded both.

Result: the 4 m stone column arrived as a 4 cm pebble on its side, the marble statue as 2 cm, and the
Voronoi shards at roughly 1 cm. `LODGroup.size` was `0.04`, so the LOD tiers could never switch
sensibly either.

The models were never wrong. The fix is `CopyLocalTransform` in `AssetPrefabBuilder`, plus
`ModelScaleFixBuilder` to hold the import settings the copy depends on. Both Blender exporters were
also switched to `bake_space_transform=True` so a future regeneration bakes the conversion into the
vertex data instead of relying on a root transform.

### T19 — two thirds of the game had no camera toggle

`FrozenCity` and `ClockCore` each had a **single** `Camera` and no `PlayerCameraRig`. T19 requires a
first-person/third-person switch with two cameras besides the minimap, and Part 3 of the plan marks it
present in all three gameplay scenes. Pressing `C` did nothing outside `MuseumNight`.

`CameraRigParityBuilder` gives both scenes the same rig `MuseumSceneSetup` builds for `MuseumNight`.

### T4 — two collision handlers against a required three

Only `ChronoOrb` and `Collector` declared `OnCollisionEnter`. Step 3.3 names four by intent.

`Warden→player` had been quietly dropped rather than written, and for a real reason: Noa moves on a
`CharacterController`, which **never** raises `OnCollisionEnter` — it reports through
`OnControllerColliderHit`. Anything driven off Warden contact would have had to be a trigger, which is
a different requirement.

Added `FallingDebris` and `SwingingHazard`. Both have Rigidbodies, so their `OnCollisionEnter` fires
against Noa's collider for real, and both scale their response from `collision.relativeVelocity` rather
than merely detecting that a hit happened.

### T2 / T3 / T5 — scenes 2 and 3 had no coverage

`FrozenCity` and `ClockCore` had **zero** world-space `TextMeshPro`. A player reaching scene 2 was
never told that `Q`/`R` switch era — the game's signature mechanic and the thing the whole scene is
built on. `SceneGuidanceBuilder` adds era-switch prompts and gear-puzzle guidance to `FrozenCity`,
boss phase callouts to `ClockCore`, an era zone and tower entry trigger, and two hinged pendulums.

### T8 — no HUD outside MuseumNight

`HudBuilder` targeted `MuseumNight` alone, so scenes 2 and 3 had no health/energy/shard readout, no
`EventSystem`, and **no pause menu**. It now runs across all three gameplay scenes.

---

## 3. Why it looked the way it did

### No post-processing ran at all

No camera carried `UniversalAdditionalCameraData`, so URP's `renderPostProcessing` defaulted to
`false`. No tonemapping, no bloom, no vignette — on any camera, in any scene. This is the single
largest reason the game read as flat.

Compounding it: the URP assets were on `LowDynamicRange` grading with MSAA **off**, which throws away
the highlight range bloom and ACES need; ambient was `Flat` at `RGBA(0.06, 0.07, 0.10)` with a single
0.5-intensity light, so interiors rendered essentially black; and fog was off entirely.

`CinematicLookBuilder` owns all of it now — per-scene ambient (Trilight, not Flat), fog, a authored
`VolumeProfile` per scene, and post-processing on every gameplay camera.

### The volume profiles were empty

`VolumeProfile.Add<T>()` creates the component in memory only — it does **not** add it to the profile's
asset file. Without `AssetDatabase.AddObjectToAsset` every effect vanished on the next domain reload
and the profile reloaded with zero components.

### `EraGrading` wiped the scene grade

`profile.Add<ColorAdjustments>(true)` turns on `overrideState` for **every** parameter. That volume sits
at priority 10, above the scene's post-process volume at 0, so it was overriding exposure, contrast and
saturation with their zero defaults. It now overrides only the era tint it actually owns.

### Particles rendered as solid white boxes

The particle material had a URP unlit particle shader and **no texture**. That shader with no `_BaseMap`
draws every particle as a fully opaque white quad, so the Chronological Shadow's drift effect was a
cluster of white boxes around the player.

A soft radial sprite alone was not enough: the material blends **premultiplied alpha**
(`SrcBlend=One, DstBlend=OneMinusSrcAlpha`), so RGB is added directly and white corners at zero alpha
still add full white. The sprite is generated with its RGB premultiplied by alpha.

`GameplayVfx` also built its own textureless material at runtime; it now takes an authored asset, with a
procedural soft-dot fallback so an unwired scene degrades to a dot rather than to boxes.

### Noa was buried to the waist, twice over

`NoaIntegrationBuilder.FitToHuman` measured the bind pose in **world** space and applied the result to
**local** position — equivalent only while the player sits at world y = 0. It does not; the player
spawns at 0.08. Combined with Mixamo mesh bounds being expressed about the root bone, the two errors
compounded to `localPosition = (0, -1, 0)`, putting her feet 0.95 m below a floor whose top is y = 0.

Separately, `AttachNoaModel` removed only the *first* child named `NoaModel`. The scene player is a
prefab instance and the builder updates both prefab and instance, so each run left the buried model and
added a second one beside it.

### Everything was untextured cardboard

The museum materials had albedo but no normal maps, and each carried one fixed tiling applied to
objects of wildly different sizes — a 30 × 20 m floor slab and a 1.5 m stair step share `MuseumMarble`,
which stretched a 256 px tile across five metres. `SurfaceDensityBuilder` measures each renderer and
assigns a shared material variant giving a consistent metres-per-tile, so the scene gains a handful of
extra materials and stays SRP-batchable. The four museum textures were re-authored at 1024² with
matching normal maps.

### The enemies were capsules

Both AI agents were primitive `Capsule`s. The Warden even carried a fully built Animator and
`WardenController` (Patrol/Alert/Chase/Attack/Frozen) with **no avatar, no skinned mesh and no motion on
any state** — so the hand-authored controller T14 grades was invisible in play. `CharacterLookBuilder`
gives both a skinned humanoid using the one imported rig, and fills the controller's motion slots.

### The museum had no ceiling

Structurally the building was complete — two storeys, a real staircase, textured walls — but it was an
**open-topped box**, which reads as an unfinished greybox no matter how well it is lit, and the interior
was otherwise bare.

`MuseumDressingBuilder` adds a coffered ceiling, skirting and cornice, glass display cases on marble
plinths, framed wall art with its own picture lights, and benches. The ceiling is a ring of panels
around three skylight openings rather than one slab, so the moonlight still falls in shafts and the
pooled night lighting survives. Everything sits against the walls and out of the patrol lanes, and the
navmesh is re-baked afterwards so the agents path around the new colliders rather than through them.

The scene ambient was lifted afterwards, because the new roof blocks most of the moonlight that used to
flood in through the missing one.

### The menus were a grey skybox

Both menu scenes held a camera, a light and a canvas of default-styled buttons. `PremiumMenuBuilder`
adds a 3D vignette built from props the game already ships, orbits the camera slowly around it, and
restyles the canvas.

---

### T18 — the minimap was blank

The minimap camera renders an allow-list of exactly the `Minimap` layer, which is the right way to keep
a hidden Time Anchor invisible by construction. But **nothing was ever put on that layer except the
player's own marker**, so the map was one arrow on a flat dark background. The requirement asks for a
minimap that gives *orientation*, and an empty map orients nobody.

`MinimapGeometryBuilder` generates flat plates **from the museum's real geometry**, so the map cannot
drift out of step with the building — change a wall and re-run, and the map changes with it. Walls,
floors and the staircase read as layout; objectives are gold, the exit green.

The player marker itself was 0.6 × 0.9 units against a 32-unit-wide orthographic view — about four
pixels in a 190 px map, indistinguishable from a map plate. It is now sized and shaped to read as a
heading, and raised above every plate so nothing occludes it.

---

## 3b. Two problems found by actually playing it

Both of these came from playing the build rather than from the test suite, and
neither was visible in a screenshot.

### The mouse escaped the window and looking around stopped working

`PlayerCameraRig` set `Cursor.lockState = CursorLockMode.Locked` **once**, in
`OnEnable`, and never again. Unity drops that lock on its own in several
situations and never restores it:

- alt-tabbing away, or clicking outside a windowed player;
- pressing **Escape** in the Editor, which is also the Pause binding;
- requesting the lock before the window has focus — which is exactly when
  `OnEnable` runs on the first frame of Play.

Once released, nothing re-locked it. The cursor ran off the window, mouse-look
stopped responding, and the game became unplayable for the rest of the session.

The fix re-asserts the lock every frame from `LateUpdate` whenever gameplay
should own the mouse, and deliberately does **not** fight the pause menu or an
unfocused window, so Escape still frees the cursor to click Resume and alt-tab
still works. `CursorLockTests` covers all three cases.

The rig exposes `CursorCaptureWanted` and `CursorRecaptureCount` so this is
testable: in the Editor a lock is only honoured while the Game view is the
focused window, so a test runner without focus can never observe `lockState`
becoming `Locked` — but it can observe that the rig keeps asking, which is the
part that was missing.

### Nothing told the player what to do

The game had world-space plaques near each spawn and eight proximity tutorial
plaques, but nothing that travelled with the player. Walk five metres and there
was no longer anything on screen explaining the goal — in a three-scene game
with a two-item progression chain, a three-era gear puzzle and a three-phase
boss whose rule is "use the right era".

Two additions:

- **`ObjectiveTracker`** — a persistent objective line and hint in the HUD,
  computed live from `GameState`, `GearPuzzle` and `Collector`. It owns no
  state and drives nothing, so it can never disagree with the game. It names
  the *key* to press: "Press Q to reach the PAST, where the gear had not been
  lost yet."
- **`ControlsHintCard`** — the control scheme shown once at scene start,
  dismissed on the first real input or after 15 seconds. The bindings past WASD
  (Q/R era travel, CTRL slow time, C camera) are not guessable, and nothing
  showed them unless the player went looking in the pause menu.

`ObjectiveTrackerTests` asserts the objective actually *tracks* state rather
than being a fixed label.

### Looking around hit a wall at the edge of the screen

Reported as "I can't look 360 - it reaches a limit, and my mouse goes off the
screen". Both halves are the same defect. Yaw is genuinely unbounded
(`transform.Rotate` on the player, and `PlayerController` never touches
rotation), so the "limit" was simply the pointer reaching the edge of the
physical display: once there, the mouse produces no more delta and the view
stops turning.

Two causes, fixed together:

1. **The rig fought its own lock.** The first fix re-asserted the lock every
   frame but also *released* it whenever `Application.isFocused` read false.
   That flag reads false during the opening frames of a freshly launched
   player, so the rig was actively handing the cursor back at exactly the
   moment the player was trying to start. Paused is now the only reason
   gameplay gives the mouse up; Unity already drops the OS lock by itself on
   focus loss, so forcing it achieved nothing. `OnApplicationFocus` re-takes it
   the instant focus returns.

2. **The build shipped windowed.** `fullscreenMode` was `3` (Windowed) at a
   native 1920x1080 - a window as large as the screen it sits on. Any moment
   the lock was not held, the pointer was immediately outside it. Now
   `1` (borderless FullScreenWindow), which is what a game of this kind should
   ship as anyway.

### The Chronological Shadows were unreadable, and half-implemented

Reported as "it's not intuitive what those blobs are, why don't they collide
with me, am I supposed to lose health, how do I stop them".

The behaviour was right and completely uncommunicated. A Shadow is agent type
B: it does not attack, it hunts **Time Shards**, and stealing one costs score.
Nothing on screen said so - a shard vanished, the score dropped 60, and there
was no sound, flash or text. From the player's side that is indistinguishable
from a bug.

Worse, half the mechanic was missing. The design calls a stolen shard
*"recoverable by freezing it"*, but `Steal` **destroyed** the shard outright,
so there was nothing left to recover and `Freeze` merely stopped the agent.
Stolen shards are now deactivated and carried, and freezing a Shadow drops
them back at its feet and refunds the score - which is what makes it a threat
you can answer rather than one you can only watch.

Added alongside that:

- **`EnemyNameplate`** - a world-space label over both agent types giving the
  name, the current behaviour and the counter-play: *"CHRONOLOGICAL SHADOW -
  stealing a Time Shard, freeze it with the Orb"*, *"TIME WARDEN - has seen
  you, break line of sight"*. It holds a constant on-screen size by scaling
  with camera distance, so it is legible across a 30 m gallery and does not
  fill the screen when an agent walks into you.
- **`HudMessageFeed`** - transient event lines, so a theft, a freeze and a
  recovery all announce themselves.

Two bugs surfaced while building this, both from the same root: a
`NavMeshAgent` floats its transform `baseOffset` above the navmesh, and
`baseOffset` is `1` on both agents. Grounding each body on the agent's *origin*
therefore left the Warden's feet a full metre in the air and the Shadow's
0.6 m. The nameplate had the same problem. Both now measure from the body's
own renderer bounds.

The Shadow's emission was also far too hot - `0.18/0.10/0.42 x 1.6` was
brighter than anything else in a night museum, so with bloom on top it rendered
as a flat neon-violet cutout pasted over the scene rather than a figure
standing in it.

### Pickups could not be picked up

Reported as "I don't know what the Time Lens is or how to pick it up. It does
not pick itself up, so it seems like something does not work there."

Something genuinely did not work. Three faults stacked, and any one alone was
enough to break it:

1. **Trigger volumes swallowed the look-cast.** It used
   `QueryTriggerInteraction.Collide` and took only the single NEAREST hit. The
   museum is full of invisible trigger volumes - room entry, eight tutorial
   reveals, era zones - all on the Default layer inside the interact mask. Any
   one between the camera and a pickup returned a collider with no
   `IInteractable`, so no prompt appeared and E did nothing. Measured: standing
   2 m from a Time Shard, the ray was eaten by `Trigger_MainGallery` **6.6 m
   away**.

2. **A zero-width ray from a shoulder-offset camera.** In third person the
   camera sits half a metre off Noa's shoulder, so `camera.forward` runs
   *parallel* to the player-to-target line. It passed **0.54 m to the side** of
   a shard whose collider was **0.28 m** across.

3. **Noa's own CharacterController counted as a wall** once the cast was
   widened, because it is the first solid thing a camera behind her meets.

Now: triggers pass through while solid geometry still blocks, the cast is a
sphere with 0.35 m of aim tolerance, self-hits are skipped, and pickup hit
boxes are sized in metres rather than in local units (they were being shrunk by
the object's own scale). `InteractionReachTests` covers all three.

### Nothing looked like a pickup

Every collectible was the same 0.4 x 0.4 x 0.1 untextured plate, unlit, in a
deliberately dim museum. `CollectibleLookBuilder` gives each one a distinct
floating, spinning, emissive shape, its own point light, and a world-space
label naming it *and the key*: "Take the Time Lens [E]".

### The gear socket was buried inside the clock tower

FrozenCity's puzzle was unplayable. The socket was placed 2.5 m in front of the
tower's centre, but the tower `Shaft` is a 6 m cube - its front face is 3 m out,
so the socket sat **half a metre inside solid stone**. The look-cast hit the
wall every time. `GearPuzzle`'s own logic tests passed throughout, because
logic was never the problem.

Moved to 3.6 m out. `FrozenCityPuzzleReachTests` now asserts *reachability* -
that nothing solid stands between the player and each puzzle piece - which is a
different question from whether the state machine works.

### The Shadow emptied the tutorial scene before the player arrived

MuseumNight has exactly two Time Shards. A Chronological Shadow took both
within the first half minute, before the player had walked far enough to reach
either - so the score dropped for reasons they never saw and there was nothing
left to collect.

Part 3 of the plan places the Shadow's threat in FrozenCity onward and marks
MuseumNight "Warden only (teach)". It still appears there, on its own navmesh
bake, so the player meets one and learns what it is - but it cannot steal.
Elsewhere it waits 30 seconds and only hunts shards the player is near enough
to witness and contest.

### The scenes were too dark to navigate

Ambient raised roughly 45%, moonlight 1.15 to 1.6, exhibit spots 14 to 20,
interior fill 3.4 to 6, ClockCore's key and practicals raised, and fog thinned
in both interiors.

One hypothesis discarded along the way: URP's 8-additional-lights-per-object
cap would have explained patchy lighting on a single 30 x 20 m floor mesh, but
the PC renderer is **already Forward+**, which culls per tile and has no such
cap. The scene was simply under-lit.

### The Time Warden had no consequence at all

The question was put plainly: *are those enemies? why don't they collide with
me? am I supposed to lose health if they touch me?*

The answer was that nothing happened, and that was a bug. Nothing anywhere in
the project called `TakeDamage` from a Warden. It could see Noa, pursue her
with the predicted-intercept steering, reach her - and stand there. Walking
into one was indistinguishable from walking into a wall, which made the entire
stealth layer decorative.

Every *other* part of the Warden was covered and passing: the patrol route, the
real stop at each waypoint, the vision cone, the line-of-sight raycast against
a code-built mask, the pursue steering, the freeze. No test had ever asked what
happens when the chase succeeds.

A capture now costs health and score (T8 asks for score *loss* to be real),
posts a message naming both the escape and the counter, drops the Warden to
Search, and holds a three-second cooldown so one bad corner cannot drain the
run. It is survivable, so a first mistake teaches.

### The Warden went blind at point-blank range

Found while writing the capture test, and much worse than the missing capture.

`eyeHeight` is measured from the feet, but the Warden's transform rides a
metre above the floor on `NavMeshAgent.baseOffset`, so the eye was being placed
at **3.0 m**, hunting for a target at 1.28 m. The cone was then judged on the
full 3D angle, so the closer the player got the further *down* the sightline
tilted. Measured: standing one metre in front of a Warden, in the open, the
bearing was **59.8 deg** against a 45 deg half-cone - outside it.

So the Warden could see the player across the room and went blind the moment
they were on top of it, then wandered off. Detection stalled at 0.18 and
decayed to nothing.

The eye is now measured from the feet, and the cone is judged on the
**horizontal bearing** only. A ground patrol should not be able to lose
somebody by standing next to them.

The Chronological Shadow had the same arithmetic in a different place: its
1.2 m steal range was a straight distance, while its own transform rode the
same offset and the shards float. It carried well over a metre of pure vertical
error - the Shadow could stand directly on a shard, measure itself out of range
and circle it indefinitely. Also horizontal now.

### Chrono Energy could strand a run permanently

The plan asks for energy to "regenerate slowly while not using powers". Nothing
implemented it, and nothing else put any back either - the only source in the
whole project was the partial refill on respawn. Energy was a one-way resource.

Era travel costs 8, an orb costs 5, the Hourglass drains 18/second, and the
ClockCore fight needs all three: roughly fifty with no misses, on top of
whatever FrozenCity's three-era puzzle had already spent. A player who reached
the Collector with an empty bar could not switch era, could not throw, and
could not win - and phase 3 erodes health without the Hourglass, so the only
exit was to die on purpose, because dying was the one thing that refilled it.

Now 6/second after 1.5 seconds without spending. That keeps the moment-to-moment
choice intact - the Hourglass still costs a net 18/second, so slowing time is
never free - while making a dead end impossible. `ClockCoreWinnabilityTests`
asserts both halves: that energy recovers, and that it does not outpace the
Hourglass.

### Every refusal was silent

Pressing Q with too little energy, pressing Q before finding the Time Lens,
pressing Q at the end of the era range, and clicking with an empty bar all did
exactly the same thing: nothing. No sound, no message, no flicker. That is
indistinguishable from a broken key, and the four causes need four different
responses from the player.

`EraManager.TryStep` and the orb launcher now say which it is.

### Is the boss fight actually winnable?

`ClockCoreSceneTests` covered the Collector's phase logic by calling its
transitions directly, and passed throughout - but that is not the same claim.
The gear socket in FrozenCity had passing logic tests while being buried inside
a wall.

`ClockCoreWinnabilityTests` throws real orbs from the real launcher along the
real camera forward, under real gravity, and requires the shield to break from
the hits landing. It does.

### Two graded props shipped bright magenta

`AssetPrefabBuilder` loaded `MuseumMarble.mat` by path to skin the LOD tiers
(T11) and the fracture shards (T10). But `MuseumBuilder` is what *creates* that
material, and it runs four steps later in `FullSceneRebuild`. On a clean
rebuild the load returned null, every tier was written with an empty material
slot, and Unity drew all of them in the error shader - twenty-one renderers of
bright pink stone at the foot of the clock tower.

A null material is not an error to Unity. Nothing logged, nothing threw, and
every other test passed; the only symptom was visual. `MaterialIntegrityTests`
now walks all five scenes and fails on any renderer with a missing material or
the error shader, and the builder creates a stand-in rather than depending on
builder order.

### The terrain was flat, but only on the second rebuild

T6 asks for a sculpted terrain. `SculptHeights` produced a proper relief of
0.85 and `SetHeights` wrote it correctly - and then `PaintLayers` threw it
away.

`CreateTerrainData` deletes and recreates the `.asset`, so the object it
returns is a fresh, *flat* TerrainData and the sculpt exists only in memory.
`PaintLayers` then writes three PNGs and calls
`TextureImporter.SaveAndReimport`, and that import reloads the terrain asset
from its still-flat on-disk state.

It survived the first rebuild because the previous run's asset was still
there, which is why this only appeared on the second rebuild in a row - and a
flat terrain is indistinguishable from a plane in a screenshot. The sculpt is
now committed to disk before anything can reimport it, and the builder logs an
error if the finished heightmap has no relief.

### Pickup labels were squashed

`PickupBeacon` set the label's `localScale` for constant apparent size, which
is not enough on its own: the label hangs off the pickup, and the pickups carry
their own non-uniform scale. The Chrono Hourglass is `(0.3, 0.5, 0.3)`, so its
prompt rendered at a third of its width. The parent's contribution is divided
back out now.

### The look limit, researched rather than reasoned about

Reported three times, so worth recording what the answer turned out to be
rather than only what changed.

The bindings were checked first and were correct: the Look action reads
`<Mouse>/delta`, not `<Mouse>/position`, so it was not the classic
position-clamps-at-the-screen-edge mistake. Yaw is
`transform.Rotate(Vector3.up, ...)` and is genuinely unbounded; only pitch is
clamped, at -70/+80, which is standard. The cursor handling already followed
the canonical pattern - re-assert `CursorLockMode.Locked` every frame,
re-assert on `OnApplicationFocus`, and release only for the pause menu.

The Unity Input System issue tracker names the actual failure: at the window
edge the OS stops producing movement, **delta goes to zero on that axis, and
the lock is reported as no longer held**. Which means `lockState` is a request,
not a reading of what the OS is doing.

That matters because the recentring fallback bailed out early whenever
`lockState == Locked` - on the reasoning that a held lock already pins the
pointer, so a warp would be redundant. That reasoning skipped precisely the
case the fallback was written for. It never ran when it was needed.

It now runs regardless of the reported lock state. The dangerous half of that
change is guarded: what Unity reports for the pointer position while locked is
not guaranteed to be the window centre, and a stale off-centre value would make
it warp and suppress look on every frame, disabling looking entirely rather
than merely limiting it. So a warp additionally requires the pointer to have
actually *moved* since the previous frame - a still pointer needs no rescue.
`CursorLockTests.AStillPointerIsNeverWarpedSoLookIsNeverSuppressed` pins that
down.

One caveat that no code can remove: in the Unity **Editor**, a lock is only
honoured while the Game view is the focused pane, and pressing Escape releases
it unconditionally. Judge the look in the built player, not in the Editor.

### A latent obstruction the dressing pass introduced

Adding display cases put one plinth directly in the player's path from the
MuseumNight spawn — they walked into it within two seconds of gaining control.
`NoaAnimatorTests` caught it as the Animator's `Speed` collapsing to zero while
movement was still held. The cases are now placed clear of the spawn corridor.

That test also had a latent framerate dependency of its own: it read `Speed`
only after 120 frames, which assumed the player would still be in open floor by
then. At ~4 m/s that is anywhere from 6 to 12 metres, and the museum is about
10 m deep from the spawn — so once the scene got heavier the player reliably
reached the north wall and the test measured someone standing still against it.
It now samples the peak while running, which is the honest measurement and
still fails outright if the player never moves.

---

## 4. A latent bug the scale fix exposed

With the LOD models correctly sized, a `StoneColumn` at `(0, 0, -16)` in `ClockCore` turned out to sit
one metre in front of the player spawn, directly between the third-person camera and Noa. It had been
harmless for as long as it was a 4 cm pebble. Moved to flank the entrance.

---

## 5. Size and the shipped build (S1, S2, D2)

| | |
|---|---|
| Uncompressed player build | **137.6 MB** (5 scenes) |
| Compressed `.zip` — the figure S1 caps | **56.8 MB** |
| S1 limit | 300 MB |
| Headroom | **243 MB** |

The brief states outright that the game is judged on its weight, so `BuildSizeBuilder` still caps the
character texture imports and enables optimal animation-clip compression even though the cap was never
at risk — the imported character's source maps are 4096² for a character a few hundred pixels tall for
almost the whole game, and runtime texture memory and load time matter for D2 even when archive size
does not. That pass took the uncompressed build from 145.4 MB to 137.6 MB.

**The shipped EXE was launched and verified**, not just built: it ran for 25 seconds, initialised D3D11,
PhysX and the Windows input backend, and its `Player.log` contains **zero exceptions, errors or missing
references**. Build artefacts are at `Build/Release/` and `Build/MuseumOfTime_v1.zip`.

---

## 6. Verification

`Assets/Tests/PlayMode/` — **113 tests, all passing**, up from 90 with 11 failures.

Two new suites:

- **`RequirementComplianceTests`** — one test per graded requirement T1–T21, asserted against the scenes
  as they ship. This is Part 8's compliance matrix made executable, so "the requirement is present"
  fails loudly the moment it stops being true. It asserts real properties, not presence: that the
  fractured assets are larger than 0.5 units, that LOD tiers actually decrease in triangle count, that
  the terrain is not flat, that a patrol waypoint has a non-zero wait, that `MuseumNight` has **no**
  Time Anchor (T21 forbids one before scene 2), and that the anchors elsewhere are invisible without
  the Lens.

- **`FullPlaythroughTests`** — one test that plays MainMenu → MuseumNight → FrozenCity → ClockCore →
  Victory in a single run, driving `SceneExitTrigger` and `Collector` rather than setting `GameState`
  flags. It checks both directions of each gate: that each exit **refuses** to open without its item,
  and opens with it.

---

## 7. Builder order

`FullSceneRebuild.BuildAll` now includes every pass above, and the order is load-bearing:

1. `ModelScaleFixBuilder` — before any prefab is built from those models.
2. …existing scene builders…
3. `SceneGuidanceBuilder` — before `HazardCollisionBuilder`, which attaches to the pendulums it creates.
4. `CameraRigParityBuilder` — before the HUD passes, which need the second camera.
5. `PremiumHudBuilder` — after `HudBuilder`; it re-skins what that builds and deletes the flat originals.
6. `SurfaceDensityBuilder` — after all geometry exists, since it measures renderers.
7. **`CinematicLookBuilder`** — then `MinimapGeometryBuilder` (which reads the museum geometry the
   look pass has finished with) and `BuildSizeBuilder`. The look pass It owns ambient, fog and post-processing, and several earlier
   builders touch lighting. `AudioAndVfxBuilder` used to force a flat 0.06 ambient *after* the look pass
   and silently revert the whole thing.

---

## Known remaining work

- **Audio mixer asset** — still the documented manual step; Unity has no supported API to create one.
- **Lightmap bake** — lighting is real-time. A bake is optional and needs visual judgement.
- **Framerate check on the defense machine (D2)** — no headless equivalent.
- **Trailer, GDD PowerPoint, Moodle upload (S2–S5, S7, S8, G2)** — submission work, not code.
