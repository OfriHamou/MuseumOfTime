# Phase 4 — Unity Walkthrough

**How to rebuild the AI and navigation by hand, and where to see it in the editor.**

Fourth in the series. Phase 4 carries the three requirements the plan flags as easiest to fail on a
technicality, so this document is as much about *what the wording actually demands* as about clicks.

**Before you start:** open `Assets/Scenes/MuseumNight.unity`, open the Console, click *Clear*.

## The fast path

Two menu items, in this order:

1. **Museum of Time → Build Warden Animator Controller** → `WARDEN ANIMATOR OK: 5 states, 4 parameters`
2. **Museum of Time → Build Navigation (two agent types)** →
   `NAV OK: WardenAgent(id … r=0.50 h=2.0 climb=0.40) | ShadowAgent(id … r=0.30 h=1.2 climb=0.90) | two NavMeshSurfaces baked separately.`

**Read that second line carefully.** It deliberately prints the dimensions *read back from the project
settings*, not the values that were requested, because an earlier version reported ids that did not exist.

---

# Step 4.1 — Two agent types, separate bakes, different routes

The requirement:

> AI steering and/or pathfinding, with **at least two agent types moving on different routes (separate
> bake)**

Three clauses, and only the first is obvious. Each is handled separately below.

## Terminology first

**Unity's NavMesh baking *is* Recast.** `com.unity.ai.navigation` wraps the Recast/Detour library, which
is what the brief means by that word. Say so in the defense — it shows you know what the tool is rather
than which button to press.

## Clause 1: two agent types

| Agent | Radius | Height | Climb | Slope |
|---|---|---|---|---|
| `WardenAgent` | 0.50 | 2.0 | **0.40** | 45° |
| `ShadowAgent` | 0.30 | 1.2 | **0.90** | 60° |

**By hand:** **Window → AI → Navigation → Agents** tab → **+** to add a type, then fill in the fields and
rename it. You should end up with three entries: Humanoid, WardenAgent, ShadowAgent.

> **A trap worth knowing about.** Doing this from script is harder than it looks. Unity exposes
> `NavMesh.CreateSettings()` and `NavMesh.RemoveSettings()` but **no setter** for an existing type's
> dimensions. My first attempt removed and recreated the settings to "update" them, which mints a fresh
> `agentTypeID` every run, orphans the old entry, and leaves agents in the scene pointing at ids that no
> longer exist. The Console happily reported success while the project settings contained two identically
> sized types called `New Agent` and `New Agent 1`. The fix is to edit the entries **in place** through
> `SerializedObject`, which is what `NavigationBuilder.EnsureAgentType` now does.

## Clause 2: separate bake

Two `NavMeshSurface` components, each with its own **Agent Type**, each baked on its own:

```csharp
wardenSurface.BuildNavMesh();
shadowSurface.BuildNavMesh();
```

**By hand:** *GameObject → Create Empty* named `NavMesh_Warden` → *Add Component* → **NavMesh Surface** →
set **Agent Type** to `WardenAgent` → **Bake**. Repeat for `NavMesh_Shadow` with `ShadowAgent`.

> **This is where people lose the mark.** One surface with two agent types assigned looks similar in the
> Scene view but is *not* a separate bake. Two surfaces, two bakes, and you should be able to select each
> one and show its own baked mesh.

## Clause 3: different routes — the part most people miss

Two agent types that walk identical paths do not satisfy "different routes".

Rather than hand-painting NavMesh areas, the geometry does the work. `BuildObstacleCourse()` places, on
the east side:

- a wall with a **0.8 m slot** — a 0.5 m-radius Warden needs 1.0 m of clearance to fit, a 0.3 m-radius
  Shadow needs 0.6 m
- behind it a **0.7 m ledge** — inside the Shadow's 0.9 m climb, outside the Warden's 0.4 m

So the two bakes genuinely disagree about where walking is possible, and the difference is driven purely
by the agent dimensions. That is easier to defend than "I painted a special area", because the reason is
visible in the numbers.

**The proof, from the playtest.** Same destination, one path query per agent type:

```
warden PathPartial  12.1m in 4 corners
shadow PathComplete  1.4m in 2 corners
```

`PathPartial` means the Warden **cannot get there at all**. That is the requirement demonstrated rather
than asserted.

**To see it yourself:** select `NavMesh_Shadow`, and in the Navigation window enable **Show NavMesh**.
The blue mesh threads through the slot on the east side. Select `NavMesh_Warden` and it stops short.

---

# Step 4.2 — Patrol with pause

`PatrolRoute.cs` holds waypoints, each with its own `waitSeconds`. `WardenAI` walks them.

**The pause is the graded part**, not the patrol. A guard that stops and scans is what makes stealth
playable, because it gives the player a window to move in.

```csharp
state = State.Pause;
agent.isStopped = true;                            // a real stop
waitUntil = Time.time + route.Current.waitSeconds;
...
transform.Rotate(Vector3.up, 40f * Time.deltaTime); // sweeping the head
```

`agent.isStopped = true` rather than setting speed to zero: the agent stays planted, and the head sweep
makes the pause read as a *scan* rather than an idle.

**By hand:** select `TimeWarden` → **Patrol Route** component → set **Waypoints**, each with a position
and a **Wait Seconds** of 2–4. Select the object and the route draws as cyan spheres in the Scene view.

**To see it:** Play and watch `TimeWarden`. It walks to a waypoint, **fully stops and turns on the spot**
for a few seconds, then moves on.

> **A test that proved nothing, and how it was caught.** My first version of this check stopped watching
> the moment it had seen one pause and one movement. That took 1.5 seconds and measured **0.0 m of
> travel** — a completely stuck agent would have passed it. It now watches for a fixed 16 seconds and
> requires more than 2 m covered. The Warden covers 12 m. Worth remembering that a green test is not the
> same as a meaningful one.

---

# Step 4.3 — Vision, the LayerMask, and stealth

Detection is three tests, deliberately in this order because each costs more than the one before:

1. **Range** — a cheap distance check
2. **Cone** — `Vector3.Angle` against half the view angle
3. **Line of sight** — a raycast, only if the first two passed

The mask, built in code as the requirement asks:

```csharp
visionBlockers = LayerMask.GetMask("Default", "Obstacle", "StealthCover");
```

> **The bug this shipped with first, and it is a nasty one.** I originally asked for a layer called
> `HideVolume`. This project does not define that layer — it calls it `StealthCover`.
> **`LayerMask.GetMask` silently ignores names that do not exist.** The mask quietly collapsed to
> `Default` alone, so every piece of stealth cover stopped blocking sight, and *nothing warned about it*:
> no error, no exception, and the code reads perfectly. The playtest only caught it because the mask
> value printed as `1` instead of covering several layers.
>
> The check now asserts the mask spans **at least two layers**, which would have caught it immediately.

**To see it:** select `TimeWarden` in the Scene view — the vision cone draws as two orange rays.
In Play mode, watch `DetectionLevel` climb while you stand in the cone and fall when you break sight
behind a pillar.

---

# Step 4.4 — Steering behaviours

`SteeringBehaviours.cs`, named after the textbook so they are obvious in a code review. The requirement
asks for a clear steering element, and pathfinding alone is arguably not steering.

**`Pursue` is the one to talk about.** It aims at where the target is *going* to be:

```csharp
float lookAhead = Mathf.Min(distance / selfSpeed, 2f);
return targetPosition + (targetVelocity * lookAhead);
```

That is what separates pursue from a naive chase: the pursuer cuts the corner and intercepts, instead of
trailing along behind and never catching anything moving at its own speed. The look-ahead scales with
distance but is capped, or the prediction becomes nonsense at long range.

`Seek` and `Flee` are used by the Shadow. `Flee` guards the degenerate case of standing exactly on the
threat, where normalising a zero vector would produce `NaN`.

---

# Step 4.5 — The Chronological Shadow

`ShadowAI.cs`. Deliberately **not** a reskinned Warden.

- It uses the `ShadowAgent` navmesh, so it reaches places the Wardens cannot.
- Per the GDD it does not speak and repeats one gesture from its past.
- **It steals Time Shards.** That makes it a threat to your *score* rather than your health, which gives
  the player a reason to fear something that never attacks.
- Freezing it with the Chrono Orb is how a stolen shard is recovered.

Both enemies implement `IFreezable`, so the orb creates a stealth opening against either.

---

# Step 4.6 — The Warden Animator

Same approach as Noa's: authored in code by `WardenAnimatorBuilder`, never imported.

**5 states** — Patrol, Alert, Chase, Attack, Frozen. **4 parameters** — `Speed`, `AlertLevel`,
`IsFrozen`, `AttackTrigger`.

The important detail: **`AlertLevel` is fed the same value as the detection meter**, so the animation can
never disagree with the mechanic the player is actually being judged by:

```csharp
animator.SetFloat(AlertId, warden.DetectionLevel);
```

`Speed` comes from `agent.velocity`, not the intended destination, so an agent stuck against a wall reads
as standing still.

Attack and Frozen come from **AnyState** with `canTransitionToSelf = false`, so a repeat trigger cannot
restart an in-progress animation.

**To see it:** open `Assets/Animations/Enemies/WardenController`, press Play, select `TimeWarden`, and
watch the state box move from Patrol to Alert as `AlertLevel` climbs.

---

# Verifying the whole phase

```
"C:/Program Files/Unity/Hub/Editor/6000.4.8f1/Editor/Unity.exe" -batchmode -quit -projectPath . -executeMethod BuildScript.BuildDevelopment -logFile Build.log
```

```
cd Build/Playtest && ./MuseumOfTime.exe -playtest -logFile playtest.log && cat playtest-report.txt
```

**56 of 56**, with the Phase 4 lines being:

```
PASS  both enemy types are in the scene            [TimeWarden and ChronologicalShadow]
PASS  they use two different agent types           [warden id -902729914, shadow id 287145453]
PASS  the agent types have different dimensions    [warden r=0.50 h=2.0, shadow r=0.30 h=1.2]
PASS  both agents are actually on their navmesh    [both onNavMesh=True]
PASS  the two agent types take different routes    [warden PathPartial 12.1m; shadow PathComplete 1.4m]
PASS  the warden has a patrol route                [4 waypoints]
PASS  the warden patrols AND pauses                [paused=True walked=True, covered 12.0m in 16.0s]
PASS  vision uses a LayerMask built from real layers [mask value 385 covering 3 layers]
PASS  a Chrono Orb hit freezes a warden            [state = Frozen]
PASS  the warden has our own Animator controller   [WardenController, 5 clips]
```

---

# Things you should be able to answer in the defense

Continuing from Phases 1–3:

25. **Is Unity's NavMesh the same as Recast?** Yes — `com.unity.ai.navigation` wraps Recast/Detour.
26. **What makes a bake "separate"?** Two `NavMeshSurface` components, each with its own agent type,
    each baked independently. One surface serving two agents is not.
27. **How do the two agents end up on different routes?** A 0.8 m slot and a 0.7 m ledge: passable by a
    0.3 m-radius agent that can climb 0.9 m, impassable to a 0.5 m-radius agent that can climb 0.4 m. The
    Warden's path query returns `PathPartial`.
28. **Why `agent.isStopped` rather than setting speed to zero for the pause?** It plants the agent
    properly; zero speed leaves it still trying to path.
29. **What happens if `LayerMask.GetMask` is given a layer name that does not exist?** It is silently
    ignored — the mask loses that layer with no warning at all.
30. **What makes Pursue different from a chase?** It targets the predicted position, so the pursuer
    intercepts rather than trailing behind.
31. **Why does the Animator read `agent.velocity` rather than the destination?** So an agent pressed
    against a wall correctly reads as standing still.
32. **Why is `canTransitionToSelf` unticked on the AnyState transitions?** Otherwise a repeated trigger
    restarts the animation that is already playing.
