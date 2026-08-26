# Phase 1 — Unity Walkthrough

**How to rebuild everything in Phase 1 by hand, and where to see it in the editor.**

This is the click-by-click companion to `docs/Implementation_Plan.md`. Every change I made is listed
here with the exact menu path, the exact field names and values, and **what you should see on screen**
after each step. If you can follow this document end to end, you can rebuild Phase 1 from an empty
scene — which is what the defense requires (you must be able to add and remove elements live).

**Before you start:** open the project in Unity, open `Assets/Scenes/MuseumNight.unity`
(Project window → `Assets` → `Scenes` → double-click **MuseumNight**), and open the Console with
**Window → General → Console**. Click *Clear*. There should be **no red errors**.

---

# Step 1.1 — Camera-relative movement, jump, and two CharacterController fixes

## What the code does

File: `Assets/Scripts/Player/PlayerController.cs`

**The bug.** The old code built the movement vector from raw input:

```csharp
Vector3 movement = new Vector3(input.x, 0f, input.y);   // world axes
```

`input.y` was applied to world **Z**, so `W` always walked toward world +Z no matter which way you were
looking. Fine with a fixed camera; wrong the moment mouse look exists.

**The fix** — steer along the camera's own axes, flattened to the ground plane:

```csharp
Vector3 forward = cameraTransform.forward;
Vector3 right   = cameraTransform.right;
forward.y = 0f;  right.y = 0f;      // flatten: looking down must not drive you into the floor
forward.Normalize();  right.Normalize();
Vector3 direction = (forward * input.y) + (right * input.x);
return Vector3.ClampMagnitude(direction, 1f);   // diagonals would otherwise be 1.41x faster
```

**Jump.** Space had been wired to `OnRun`, so jumping had never worked at all. Now:

```csharp
verticalVelocity = Mathf.Sqrt(-2f * gravity * jumpHeight);
```

This is the projectile-motion result solved for launch speed, so the **Jump Height** field is literally
the peak height in metres. Measured in the real build: **1.19 m** for a setting of 1.2.

**Two CharacterController fixes that the tests forced out.**

1. *One `Move()` per frame.* Movement and gravity used to be two separate `Move()` calls.
   `CharacterController.velocity` and `isGrounded` describe **only the last `Move`**, so velocity
   reported pure vertical motion — the Animator's `Speed` stayed at 0 while walking.
2. *`minMoveDistance = 0`.* Unity defaults this to `0.001`, which **silently discards any move under a
   millimetre**. At high framerates the per-frame gravity step falls below that, ground contact is never
   registered, and the character hovers and cannot jump.

## Where to see it in the editor

1. Hierarchy → expand `--- CHARACTERS ---` → select **Player**.
2. In the Inspector you should see, in order: **Character Controller**, **Player Input**,
   **Player Input Reader**, **Player Controller**, **Player Camera Rig**, **Animator**,
   **Player Animator Driver**.
3. Expand **Player Controller**. You should see these fields and defaults:

| Section | Field | Value |
|---|---|---|
| Movement | Walk Speed | 4 |
| Movement | Run Speed | 7 |
| Gravity and jumping | Gravity | −20 |
| Gravity and jumping | Grounded Force | −2 |
| Gravity and jumping | Jump Height | 1.2 |
| Gravity and jumping | Coyote Time | 0.15 |
| Stairs and slopes | Step Offset | 0.35 |
| Stairs and slopes | Slope Limit | 50 |

> **Note.** Step Offset, Slope Limit and Min Move Distance are applied to the **Character Controller**
> in `Awake`, so the values you see on the Character Controller component in edit mode are overwritten at
> runtime. Press Play and look at the Character Controller again: **Min Move Distance becomes 0**.
> That is the fix taking effect, not a bug.

## How to prove it works

- Press **Play**. Hold **W** — Noa walks away from the camera.
- Move the mouse 90° to the right, then hold **W** again — she now walks in the **new** direction.
  Before the fix she would have kept walking the original way.
- Hold **Shift + W** — noticeably faster.
- Press **Space** — she rises and falls back. Watch the Inspector's *Position Y* on the Player.

---

# Step 1.2 — Two Cinemachine cameras and the FPS ⇄ third-person toggle

Closes the requirement: *camera angle change, first person to third person, two cameras besides the minimap.*

## The fastest path (what I ran)

**Menu bar → Museum of Time → Build Camera Rig in MuseumNight**

It is idempotent — running it twice changes nothing. The Console prints:

```
SETUP OK: camera rig built. MainCamera(Camera+Brain), FirstPersonCamera(CM),
ThirdPersonCamera(CM+ThirdPersonFollow), CameraPivot at 1.6m,
Animator + PlayerAnimatorDriver on Player.
```

The script is `Assets/Editor/MuseumSceneSetup.cs`. Reading it is the fastest way to see exactly which
components go where — it is the same list as the manual steps below.

## Building it by hand instead

Your four camera objects existed but were **empty Transforms with no components** — that is why the Game
view said *No cameras rendering*.

**1. The camera pivot.**
Right-click **Player** in the Hierarchy → **Create Empty**. Rename it `CameraPivot` (F2, or slow
double-click). In the Inspector set **Position** to `X 0, Y 1.6, Z 0`.
*You should see:* `CameraPivot` indented under `Player`, sitting at head height in the Scene view.

**2. The real camera.**
Select **MainCamera** (under `--- CAMERAS ---`). Inspector → **Add Component** → type `Camera` → Enter.
**Add Component** → `Audio Listener`. **Add Component** → type `Cinemachine Brain` → Enter.
At the top of the Inspector, set the **Tag** dropdown to **MainCamera**.
*You should see:* the Game view stops saying *No cameras rendering* and shows the scene.

**3. First-person camera.**
Drag **FirstPersonCamera** in the Hierarchy and drop it **onto `CameraPivot`** to parent it.
Set its Position to `0, 0, 0`. **Add Component** → `Cinemachine Camera`.
In that component set **Lens → Field Of View** to `70`.

**4. Third-person camera.**
Select **ThirdPersonCamera** (leave it at the scene root). **Add Component** → `Cinemachine Camera`.
Drag `CameraPivot` from the Hierarchy into both its **Follow** and **Look At** fields.
Set **Lens → Field Of View** to `60`.
**Add Component** → `Cinemachine Third Person Follow`. Set:

| Field | Value |
|---|---|
| Shoulder Offset | `X 0.5, Y 0.2, Z 0` |
| Vertical Arm Length | 0.2 |
| Camera Distance | 4.5 |

*You should see:* in the Scene view, a camera gizmo floating behind and slightly right of the player.

**5. The rig that switches them.**
Select **Player** → **Add Component** → `Player Camera Rig`. Then drag into its slots:

- **First Person Camera** ← the `FirstPersonCamera` object
- **Third Person Camera** ← the `ThirdPersonCamera` object
- **Camera Pivot** ← the `CameraPivot` object

*You should see:* three filled object slots, no "None (…)" left.

## How it works

Switching is by **priority**, not by enabling and disabling:

```csharp
firstPersonCamera.Priority = isFirstPerson ? 20 : 0;
thirdPersonCamera.Priority = isFirstPerson ? 0  : 20;
```

Cinemachine makes whichever camera has the highest priority live, and the **Brain blends** between them —
a hard enable/disable would cut, which looks cheap.

Mouse look lives in the rig, not in Cinemachine: **yaw turns the Player, pitch tilts the CameraPivot**.
Both cameras follow that pivot, so the two views agree with each other and with movement.

> **The detail worth knowing for your defense.** Mouse delta is **not** multiplied by `Time.deltaTime`.
> It is already a per-frame delta; scaling it again makes sensitivity change with framerate. This is one
> of the most common mistakes in mouse-look code.

> **Why `PlayerController` no longer rotates the player.** I originally had the controller turn Noa toward
> her movement direction *and* the rig turn her with mouse yaw. Two scripts rotating one transform in the
> same frame fight each other, and which wins depends on script execution order — a genuinely nasty bug.
> Rotation is now owned by `PlayerCameraRig` alone, and the comment at the top of `PlayerController`
> says so.

## How to prove it works

Press **Play**, then press **C**. The view blends between over-the-shoulder and first person, and back
again on a second press. In the Hierarchy during Play, select each Cinemachine camera and watch its
**Priority** field swap between 20 and 0.

---

# Step 1.3 — Noa's Animator

Closes the requirement: *an Animator you defined yourself (not imported) with at least four states.*
Ours has **six states and four parameters**.

## The fastest path (what I ran)

**Menu bar → Museum of Time → Build Noa Animator Controller**

Console prints:

```
ANIMATOR OK: NoaController built with 6 states and 4 parameters
at Assets/Animations/Player/NoaController.controller
```

Then **Museum of Time → Build Camera Rig in MuseumNight** puts the Animator on the Player and assigns it.

The builder is `Assets/Editor/NoaAnimatorBuilder.cs`. Building the controller from a script is still
*authoring* it — the opposite of importing someone else's — and it means the state machine can be rebuilt
exactly and explained line by line, which is what the defense asks for.

## Building it by hand instead

**1. Create the controller.**
Project window → navigate to `Assets/Animations/Player` (create the folders with right-click → **Create →
Folder** if missing). Right-click in the empty area → **Create → Animator Controller**. Name it
**`NoaController`**.

**2. Open it.** Double-click `NoaController`. The **Animator** window opens (if it does not:
**Window → Animation → Animator**).
*You should see:* three built-in nodes — a green **Entry**, a red **Exit**, and an orange **Any State**.

**3. Add the parameters.**
In the Animator window, click the **Parameters** tab (top-left, next to *Layers*). Click **+** for each:

| Type | Name |
|---|---|
| Float | `Speed` |
| Bool | `IsGrounded` |
| Trigger | `JumpTrigger` |
| Trigger | `InteractTrigger` |

*You should see:* four rows. `Speed` shows `0.0`, `IsGrounded` an unticked checkbox, the two triggers a
radio-button circle.

**4. Add the six states.**
Right-click the empty grid → **Create State → Empty**, six times. Select each and rename it in the
Inspector's top field:

`Idle` · `Walk` · `Run` · `Jump` · `Fall` · `Interact`

*You should see:* the first state you created is **orange** — that is the default state. It must be
**Idle**. If it is not, right-click `Idle` → **Set as Layer Default State**.

**5. Wire the locomotion transitions.**
Right-click a state → **Make Transition** → click the target state. Then **select the white arrow** and
edit it in the Inspector.

| From → To | Condition | Settings |
|---|---|---|
| Idle → Walk | `Speed` **Greater** `0.1` | Has Exit Time **off**, Duration `0.1` |
| Walk → Idle | `Speed` **Less** `0.1` | Has Exit Time **off**, Duration `0.1` |
| Walk → Run | `Speed` **Greater** `5.5` | Has Exit Time **off**, Duration `0.1` |
| Run → Walk | `Speed` **Less** `5.5` | Has Exit Time **off**, Duration `0.1` |

> **Untick `Has Exit Time` on all of these.** Left on, the transition waits for the clip to finish
> playing before it fires, so the animation lags behind what the character is actually doing. This is the
> single most common Animator mistake.

**6. Wire jumping and falling.**

- Right-click **Any State** → *Make Transition* → click **Jump**. Condition: `JumpTrigger`.
  Untick *Has Exit Time*. **Untick `Can Transition To Self`** — otherwise a second jump cancels and
  restarts the first one.
- `Jump → Fall`: leave *Has Exit Time* **on**, set **Exit Time** `0.5` — the rise becomes a fall halfway
  through the arc.
- `Fall → Idle`: condition `IsGrounded` **true**. Untick *Has Exit Time*. This is the landing.
- `Idle → Fall`, `Walk → Fall`, `Run → Fall`: condition `IsGrounded` **false**. Untick *Has Exit Time*.
  This is walking off a ledge without pressing jump.

**7. Wire Interact.**
**Any State → Interact** on `InteractTrigger`, *Has Exit Time* off, *Can Transition To Self* off.
Then `Interact → Idle` with *Has Exit Time* **on** and **Exit Time** `0.9`, so the gesture plays out.

**8. Put it on the Player.**
Select **Player** → **Add Component** → `Animator`. Drag `NoaController` into its **Controller** field.
Untick **Apply Root Motion** (the script moves Noa, not the animation).
**Add Component** → `Player Animator Driver`, and drag the Animator into its **Animator** slot.

## What drives it

`Assets/Scripts/Player/PlayerAnimatorDriver.cs`:

```csharp
animator.SetFloat(SpeedId, controller.CurrentSpeed);
animator.SetBool(GroundedId, controller.IsGrounded);
if (inputReader.JumpPressed)     { animator.SetTrigger(JumpId); }
if (inputReader.InteractPressed) { animator.SetTrigger(InteractId); }
```

`CurrentSpeed` reads **`CharacterController.velocity`**, not raw input — so walking into a wall correctly
reads as standing still, and the animation can never disagree with what the character is really doing.
The Animator holds no game logic; it only reacts.

Parameter names are cached with `Animator.StringToHash` once, rather than passing strings every frame.

## How to prove it works

1. Double-click `NoaController` so the **Animator window is open**.
2. Press **Play**, and select **Player** in the Hierarchy so the Animator window shows the live machine.
3. *You should see:* a blue progress bar on **Idle**, and at the bottom-left the parameter values live.
4. Hold **W** — the blue bar moves to **Walk**, and `Speed` climbs to about **4**.
5. Add **Shift** — it moves to **Run**, `Speed` climbs to about **7**.
6. Release — it returns to **Idle**, `Speed` falls to **0**.
7. Press **Space** — it jumps to **Jump**, then **Fall**, then back to **Idle** on landing, and
   `IsGrounded` unticks and re-ticks.

## A note on the animation clips

The builder creates six **empty placeholder clips** next to the controller so no state is motionless-but-
undefined. Noa will not visibly animate yet — the states are real, the motion is not.

To add real animation later: download clips from Mixamo (free), drop the `.fbx` into
`Assets/Animations/Player`, set **Rig → Animation Type: Humanoid** in the import settings, then drag each
clip onto the matching state's **Motion** field. **The clips may be imported; only the controller has to
be your own work** — which it is.

---

# Verifying the whole phase

## Option A — the automated test suite

**Window → General → Test Runner → PlayMode tab → Run All.**
*You should see:* **30 tests, all green.** (From the command line 6 of them skip, because simulating key
presses needs a focused player and batch mode resets input devices every frame.)

Headless equivalent, with Unity closed:

```
Unity.exe -batchmode -runTests -projectPath . -testPlatform PlayMode -testResults TestResults.xml
```

## Option B — play the real built game

**Museum of Time → Build Development Player**, then from a shell:

```
cd Build/Playtest
./MuseumOfTime.exe -playtest -logFile playtest.log
cat playtest-report.txt
```

This presses real keys in the real executable. Current result — **14 of 14**:

```
PASS  a camera is rendering              [MainCamera active, fov 60]
PASS  W walks forward                    [travelled 4.00m]
PASS  movement is camera-relative        [dot(travel, camera fwd) = 1.00]
PASS  Shift runs faster than walking     [walk 3.20m vs run 5.60m]
PASS  still on the ground after walking  [y = 1.08 (spawn y 1.08)]
PASS  grounded before jumping            [isGrounded = True]
PASS  Space jumps                        [ground y 1.08, peak y 2.27 (rose 1.19m)]
PASS  lands again after jumping          [isGrounded = True]
PASS  C switches camera                  [third person -> first person]
PASS  C switches back                    [back to third person]
PASS  animator uses NoaController        [controller = NoaController]
PASS  Speed parameter follows movement   [idle 0.00 -> moving 4.00]
PASS  animator leaves Idle while walking [state while moving = Walk]
```

`dot = 1.00` proves the Step 1.1 fix exactly. `rose 1.19m` against a **Jump Height** of 1.2 proves the
`v = sqrt(-2gh)` formula.

## Option C — just play it

Press **Play** in the editor and use `WASD`, `Shift`, `Space`, `C`, and the mouse.

---

# Things you should be able to answer in the defense

These all come from real bugs found and fixed during Phase 1:

1. **Why must movement be camera-relative?** Raw input maps to world axes, so `W` walks toward world +Z
   regardless of facing.
2. **Why only one `Move()` call per frame?** `velocity` and `isGrounded` describe only the last `Move`,
   so splitting movement and gravity makes velocity report pure vertical motion.
3. **What does `minMoveDistance` do?** Unity's default `0.001` discards sub-millimetre moves, so at high
   framerates gravity is thrown away and the character hovers and cannot jump.
4. **Why does only one script rotate the player?** Two scripts rotating one transform in a frame fight,
   and the winner depends on script execution order.
5. **Why is mouse delta not scaled by `deltaTime`?** It is already per-frame; scaling it makes
   sensitivity depend on framerate.
6. **Why `Has Exit Time` off on locomotion transitions?** Otherwise the transition waits for the clip to
   finish and the animation lags behind the character.
7. **Why does `Speed` come from `velocity` and not from input?** So walking into a wall reads as standing
   still.
8. **Why is the ground 100×100 m?** A default Unity plane is 10×10 m; the player walked off the edge
   after five metres and fell to y = −34. It is a placeholder until Step 2.1 builds the museum.
