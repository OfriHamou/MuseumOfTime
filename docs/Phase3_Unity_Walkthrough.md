# Phase 3 — Unity Walkthrough

**How to rebuild the core systems by hand, and where to see them in the editor.**

Third in the series, after `Phase1_Unity_Walkthrough.md` and `Phase2_Unity_Walkthrough.md`. Phase 3 is
mostly code rather than geometry, so this leans on *what each script is for and why it is written that
way*, followed by exactly where each one is attached in the scene.

**Before you start:** open `Assets/Scenes/MuseumNight.unity`, open the Console, click *Clear*.

## The fast path

**Museum of Time → Build Core Systems.** Console prints:

```
CORE OK: interactor, hourglass, orb launcher, era manager, respawn service,
4 triggers, 4 interactables.
```

That single menu item does everything below. The rest of this document is what it did, and how to do it
yourself.

---

# Step 3.1 — The interaction system

`Assets/Scripts/Interaction/PlayerInteractor.cs`, attached to **Player**.

Casts a ray from the active camera each frame and offers whatever it hits to the player.

**The LayerMask is built in code**, which is what the requirement asks for:

```csharp
interactMask = LayerMask.GetMask("Default", "Interactable");
```

Not set in the Inspector — you can read the file and know which layers count.

**One subtlety worth being able to explain.** The ray is allowed to travel `range + 6f`, but the range
check is then measured **from the player**:

```csharp
if (Vector3.Distance(transform.position, hit.point) > range) { return null; }
```

In third person the camera sits about 4.5 m behind Noa. Measuring from the camera would let her reach
things she is nowhere near; measuring from Noa keeps reach honest in both camera modes.

**Everything usable implements one interface** (`IInteractable`): `Prompt`, `CanInteract`, `Interact`.
The player side never learns what it is looking at.

**By hand:** select **Player** → *Add Component* → **Player Interactor**. Set **Range** to 3.

**To see it:** press Play, walk up to a Time Shard cube, and the interactor's `Current` becomes non-null.
Press **E** to collect it and watch **Score** rise on the GameManager.

---

# Step 3.2 — The trigger set

`Assets/Scripts/World/`, one file per trigger, all under **Triggers** in the Hierarchy.

The requirement is at least four. There are five, and they are **genuinely different components** rather
than one script wearing five hats — four copies of the same script would be hard to defend.

| Trigger | Object in scene | What it does |
|---|---|---|
| `RoomEntryTrigger` | Trigger_MainGallery | Sets the current room and objective |
| `TutorialTrigger` | Trigger_TutorialMove | Reveals the world-space instruction for one verb |
| `EraZoneTrigger` | Trigger_ClockChamber | Marks where era travel matters; can unlock it |
| `HazardTrigger` | Trigger_TemporalRift | Drains health and energy while you stand in it |
| `TimeAnchorTrigger` | *(scenes 2 and 3)* | Silently arms a hidden Time Anchor |

They share an abstract base, `PlayerTrigger`, which owns the "fire once" logic and records each type in
`TriggerLog` so a full playthrough can be shown to have fired all of them.

`HazardTrigger` is the one that sets `onlyOnce = false` in `Awake` — a hazard has to keep hurting.

**By hand:** *GameObject → Create Empty*, add a **Box Collider**, tick **Is Trigger**, set its **Size**,
then *Add Component* → the trigger script you want.

> **The trap:** without **Is Trigger** ticked, `OnTriggerEnter` never fires and the player just bumps
> into an invisible wall. Also, the player must be **tagged `Player`** — the builder sets that, and
> `PlayerTrigger.IsPlayer` falls back to looking for a `PlayerController` in case the tag is missing.

**To see it:** Play, walk into `Trigger_TemporalRift` at roughly (9, 0, −6) and watch **Current Health**
tick down on the GameManager every half second.

---

# Step 3.3 — Collision handling

`Assets/Scripts/World/ChronoOrb.cs`.

Triggers and collisions are different things, and the requirement asks for both. The orb uses real
`OnCollisionEnter` with the contact data:

```csharp
float speed = collision.relativeVelocity.magnitude;
Vector3 point = collision.contacts[0].point;
```

`relativeVelocity` is the point of it: a gentle tap and a hard throw must do different things, or
"impact" means nothing. Three responses, chosen by what was hit:

1. A `FracturedObject` hit **above `breakSpeed`** shatters, with the explosion centred on the contact point.
2. A `HingeJoint` gets woken, so a sleeping rigidbody still swings.
3. An `IFreezable` is frozen for a few seconds.

**To see it:** Play, aim at the Clock of Creation and click. A hard hit shatters it; a glancing one
does not.

---

# Step 3.4 — Health, energy and score

Already in `GameManager` from Phase 0; Phase 3 connects it to actual gameplay.

| Source | Effect |
|---|---|
| `HazardTrigger` | −5 health and −4 energy every half second |
| `ShardPickup` | +1 shard, +100 score |
| `ExhibitPlaque` | +25 score the first time it is read |
| Era switch | −8 energy |
| Chrono Orb | −5 energy |
| Slow time | −18 energy per second |
| Respawn | −40 score, health restored |

**Energy is what makes the time powers a decision rather than a reflex.** That is the whole design
purpose of the resource — if the Hourglass were free, there would be no reason ever to stop using it.

**To see it:** Play, select **GameManager** in the Hierarchy, and expand **State** in the Inspector.
Every value updates live as you play.

---

# Step 3.5 — The era system

`Assets/Scripts/Time/`. This is the game's signature mechanic and the plan's highest-value work, since
the brief judges the game first on how interesting it is.

Three scripts:

- **`EraManager`** — owns the current era, raises `EraChanged`, handles `Q` and `R`, charges energy.
  Attached to `--- MANAGERS --- → EraManager`.
- **`EraBoundObject`** — an object that only exists in certain eras. Toggles renderers and colliders
  rather than the GameObject, so the component keeps receiving the next era change.
- **`EraPersistentObject`** — the important one.

**Why `EraPersistentObject` matters.** Without it, era switching is three sets of scenery. With it, a
change made in the past is *already true* later on:

```csharp
// Causality: a change made in the past is already true later on.
for (int later = era + 1; later < eraPositions.Length; later++)
{
    eraPositions[later] = transform.position;
}
```

That is precisely the GDD's own example puzzle — move a cart in the past, and a route opens in the
present but a different exit is blocked in the future.

**Eras are sibling object sets that switch on and off, not three loaded worlds.** Far cheaper, and it
reads identically to a player who only ever sees one at a time.

**Era travel is locked in MuseumNight on purpose** so the first scene teaches one verb at a time. It
unlocks when the Time Lens is picked up.

**By hand:** create an empty **EraManager** object → *Add Component* → **Era Manager**. On any object
that should move through time, add **Era Persistent Object** and fill its three **Era Positions**.

**To see it:** Play, pick up the Time Lens upstairs, then press **Q** and **R**. Watch
`GameManager → State → Current Era` change, and **Current Energy** drop by 8 each switch.

---

# Step 3.6 — The Chrono Hourglass

`Assets/Scripts/Time/ChronoHourglass.cs`, attached to **Player**. Hold **Ctrl**.

Two details that are the actual content of this step:

```csharp
Time.timeScale = slowScale;                  // 0.3
Time.fixedDeltaTime = 0.02f * slowScale;     // physics steps shrink to match
```

**Scaling `fixedDeltaTime` is not optional.** Leave it alone and physics keeps stepping at the old rate
while the world crawls — collisions get sloppy and fast objects tunnel through walls.

```csharp
float cost = energyDrainPerSecond * Time.unscaledDeltaTime;
```

**Drain uses unscaled time.** Scaled time is slowed *by definition*, so draining on it would make the
ability cost less the longer it ran — the opposite of the intent.

`Restore()` always sets `timeScale` back to exactly **1**, never to whatever it was before. It is also
called from `OnDisable`, so the world can never be left in slow motion because a component switched off.

**To see it:** Play, hold **Ctrl**, and watch **Current Energy** fall and everything slow down. Release
and it snaps back.

---

# Step 3.7 — The Chrono Orb

`ChronoOrb.cs` and `ChronoOrbLauncher.cs`. Prefab at `Assets/Prefabs/World/ChronoOrb.prefab`.

This is the physical body the shooting requirement asks for, and it is also how Noa stays a
non-combatant: on impact it freezes or rewinds, it never destroys anything not already meant to break.

The orb spawns **in front of the active camera**, so it goes where you are looking in both camera modes,
and far enough forward that it does not spawn inside Noa's own collider.

Its rigidbody uses `CollisionDetectionMode.ContinuousDynamic` — a small fast sphere on discrete detection
will pass straight through walls.

The cooldown uses `Time.unscaledTime`, so it is not itself slowed by the Hourglass.

**By hand:** make a small Sphere, add **Rigidbody** and **Chrono Orb**, save it as a prefab, then drop it
into the launcher's **Orb Prefab** slot on the Player.

**To see it:** Play, click, and watch the orb arc under gravity and bounce. `Bounces` counts contacts.

---

# Step 3.8 — Time Anchors

`TimeAnchor.cs` and `RespawnService.cs`. The requirement here is the most specific in the brief, so the
implementation matches it clause by clause:

| Clause | Implementation |
|---|---|
| "from the second scene onward" | Anchors belong in FrozenCity and ClockCore. MuseumNight uses a plain respawn |
| "hidden" | No marker, no HUD icon. Arms silently on trigger enter |
| visible only with the Lens | `lensVisual` is shown only while `hasTimeLens` |
| "returns to the teleport, not the start" | `RespawnService` uses `checkpointPosition`, falling back to the scene start only if no anchor has armed |
| "with health refreshed, and possibly score" | Health restored fully, energy partly, score −40 |

It also restores `checkpointEra`, so you come back in the era the anchor armed in rather than whichever
one you died in.

> **The teleport trap:** the `CharacterController` owns the transform and will overwrite a plain position
> assignment. It has to be disabled across the move:
> ```csharp
> controller.enabled = false;
> player.transform.position = destination;
> controller.enabled = true;
> ```

**To see it:** Play, then in the Inspector call `RespawnService → Respawn` from the component context
menu. Noa is moved and **Score** drops by 40 while **Health** returns to full.

---

# Step 3.9 — Cross-scene persistence

`Assets/Scripts/Core/SaveService.cs`.

`GameState` was already `[System.Serializable]` and already survived scene loads through the
`DontDestroyOnLoad` singleton. This adds real JSON on disk:

```
C:/Users/<you>/AppData/LocalLow/Ofri and Jonathan/The Museum of Time/museum-of-time-save.json
```

**Why bother, when the singleton already worked?** Because the requirement names *Serialize* explicitly,
and an in-memory singleton cannot be shown to anyone. A file can be opened during the defense.

The two acquired items are what tie the three scenes together:

```
Time Lens        found in MuseumNight   required in FrozenCity
Chrono Hourglass found in FrozenCity    required in ClockCore
```

**To see it:** Play, pick up the Time Lens, then open the path above in Explorer. The JSON has
`"hasTimeLens": true` in readable text.

---

# Verifying the whole phase

```
"C:/Program Files/Unity/Hub/Editor/6000.4.8f1/Editor/Unity.exe" -batchmode -quit -projectPath . -executeMethod BuildScript.BuildDevelopment -logFile Build.log
```

```
cd Build/Playtest && ./MuseumOfTime.exe -playtest -logFile playtest.log && cat playtest-report.txt
```

**46 of 46**, with the Phase 3 lines being:

```
PASS  collecting a shard raises score and count     [shards 0 to 1, score 0 to 100]
PASS  taking damage lowers health                   [100 to 80]
PASS  the world can change era                      [now Past]
PASS  a change in the past carries forward in time  [present x=5.0, future x=5.0]
PASS  Ctrl slows time                               [timeScale while held = 0.30]
PASS  time returns to normal on release             [timeScale = 1.00]
PASS  the Chrono Orb is a real physical body        [thrown 1, bounces 2]
PASS  at least four trigger volumes exist           [4 PlayerTrigger volumes]
PASS  failure returns Noa to the anchor, not start  [landed 0.08m from the anchor]
PASS  respawn restores health and costs score       [health 100, score 100 to 60]
PASS  game state serialises to a real file          [.../museum-of-time-save.json]
PASS  saved state reloads, carrying the item        [hasTimeLens after reload = True]
```

---

# The bug that cost the most time in this phase

**The built game crashed on launch while the editor opened the same scene happily.** The player log said:

```
The file '.../MuseumOfTime_Data/level1' is corrupted!
[Position out of bounds!]
Crash!!!
```

Cause: several `MonoBehaviour` classes were sharing one file (`GameplayTriggers.cs` held five,
`Interactables.cs` held four).

**Unity requires a MonoBehaviour to live in a file named after its class.** The editor tolerates the
violation — play-mode tests passed 24/30 throughout — but the build pipeline writes corrupt scene data
and the player dies on load. Splitting into one class per file fixed it outright.

This is exactly the class of bug that only playing the *built* game catches, which is why the playtest
harness exists.

---

# Things you should be able to answer in the defense

Continuing the list from Phases 1 and 2:

17. **Why is the interaction range measured from the player rather than the camera?** In third person
    the camera is metres behind Noa and would flatter every distance.
18. **Why is the LayerMask built in code?** The requirement asks for it, and it makes the set of
    interactable layers readable rather than buried in a component.
19. **Why does slow-time scale `fixedDeltaTime` too?** Otherwise physics keeps stepping at the old rate
    while the world crawls, and fast objects tunnel through walls.
20. **Why does the energy drain use unscaled time?** Scaled time is slowed by definition, so draining on
    it would make the ability cheaper the longer it ran.
21. **Why does `EraPersistentObject` write forward but not backward?** Causality: a change made in the
    past is already true later, but a change made now cannot rewrite what already happened.
22. **Why disable the CharacterController when teleporting?** It owns the transform and overwrites a
    plain position assignment.
23. **Why is there a JSON save when the singleton already survives scene loads?** The requirement names
    Serialize, and a file can be shown; an in-memory object cannot.
24. **Why must each MonoBehaviour have its own file?** Unity's build pipeline maps scripts by filename;
    sharing a file produced a corrupt scene that crashed the built player on load.
