# Phase 2 — Unity Walkthrough

**How to rebuild everything in Phase 2 by hand, and where to see it in the editor.**

Companion to `docs/Phase1_Unity_Walkthrough.md`. Every change is listed with the exact menu path, the
exact values, and **what you should see on screen**. Phase 2 is world geometry and art, so most of it is
visible directly in the Scene view rather than hidden in code.

**Before you start:** open `Assets/Scenes/MuseumNight.unity`, open the Console
(**Window → General → Console**), click *Clear*. No red errors.

**All six builders are on one menu.** After the scripts compile, the Unity menu bar has a
**Museum of Time** entry containing:

```
Build Camera Rig in MuseumNight
Build Noa Animator Controller
Build Museum (two storeys)
Build Hinge Set Pieces
Build FrozenCity Terrain
Build Fracture and LOD Prefabs
Place World Props
Build Development Player
Build Release Player
```

Every one is **idempotent** — running it twice leaves the same result, so you can always re-run rather
than undo.

---

# Step 2.1 — The two-storey museum

Closes: *a designed two-storey building with your own textures and stair climbing.*

## The fast path

**Museum of Time → Build Museum (two storeys)**. Console prints:

```
MUSEUM OK: 30x20m, two floors, upper slab at y=5, staircase of 30 steps
at 0.17m rise / 0.3m run.
```

*You should see* a `Museum` object appear in the Hierarchy under `--- ENVIRONMENT --- → Architecture`,
containing `GroundFloorSlab`, six wall pieces, `UpperFloorSlab`, `Staircase`, `MezzanineRailing` and
`InteriorWalls`.

## Building it by hand

The script uses plain cubes, so you can do exactly the same thing with **GameObject → 3D Object → Cube**
and then typing the numbers into the Transform. There is nothing ProBuilder-only here.

| Piece | Position (X, Y, Z) | Scale (X, Y, Z) | Material |
|---|---|---|---|
| GroundFloorSlab | 0, −0.1, 0 | 30, 0.2, 20 | Marble |
| WallNorth | 0, 5, 10 | 30, 10, 0.4 | Plaster |
| WallSouthLeft | −8.5, 5, −10 | 13, 10, 0.4 | Plaster |
| WallSouthRight | 8.5, 5, −10 | 13, 10, 0.4 | Plaster |
| EntranceLintel | 0, 6.05, −10 | 4, 7.9, 0.4 | Plaster |
| WallWest | −15, 5, 0 | 0.4, 10, 20 | Plaster |
| WallEast | 15, 5, 0 | 0.4, 10, 20 | Plaster |
| UpperFloorSlab | 7.5, 5, 0 | 15, 0.2, 20 | Wood |
| RailEdge | 0, 5.55, 0 | 0.1, 1.1, 20 | Brass |

The upper slab covers **only the eastern half** on purpose: the western half stays open so the mezzanine
looks down into the main hall. That is what makes the two floors read as one space rather than two
stacked boxes. The lintel leaves a **2.1 m** doorway, as a real one would.

## The staircase, and the two failures behind it

Thirty steps at **0.17 m rise, 0.30 m run** — the proportions of a real staircase. But the treads you
can see have **no colliders**. What you actually walk on is `StairRamp`, an invisible solid wedge.

That is not laziness; two earlier versions failed:

1. **Colliders on each tread** — Noa jammed against the first one. A CharacterController's step-up is
   unreliable when the same `Move` also carries gravity downward, and ours does, deliberately (see the
   Phase 1 walkthrough on why there is only one `Move` per frame).
2. **A thin sloped plate over the treads** — worse. A thin plate has an *underside*, and she simply
   walked beneath it.

The wedge is 4 m thick and its body reaches below the floor, so there is no underside to get under, and
its top face is a plain **29° slope** — well inside the controller's 50° slope limit, so climbing needs
no step-up logic at all.

> **Worth knowing for the defense:** if an examiner selects a step and finds no Collider, that is the
> intended design. Visible steps plus an invisible ramp is standard practice in shipped games; it also
> stops the camera juddering once per step.

## Textures

Four materials in `Assets/Materials/Museum`, generated in code rather than downloaded:

| Material | Pattern | Tiling | Smoothness |
|---|---|---|---|
| MuseumMarble | checkerboard + Perlin grain | 6 × 4 | 0.65 |
| MuseumWood | planks with grain and dark seams | 4 × 2 | 0.35 |
| MuseumPlaster | fine Perlin noise | 4 × 2 | 0.10 |
| MuseumBrass | noise, metallic | 1 × 1 | 0.85, metallic 0.9 |

**By hand:** right-click in the Project window → **Create → Material**, set **Shader** to
*Universal Render Pipeline/Lit*, drop a texture into **Base Map**, and set **Tiling** underneath it.
Select the material and look at the preview sphere to check it before assigning.

*You should see:* click any material in `Assets/Materials/Museum` and the Inspector preview shows the
pattern; the `.png` beside it is the generated texture.

---

# Step 2.2 — FrozenCity terrain

Closes: *Terrain built by you.*

## The fast path

**Museum of Time → Build FrozenCity Terrain**. Console prints:

```
TERRAIN OK: 200x200m, 513 heightmap, max height 40m, 3 paint layers.
```

This one **opens the FrozenCity scene**, not MuseumNight. *You should see* the scene switch, and a
`FrozenCityTerrain` object appear with a bowl-shaped landscape and a `ClockTower` standing in it.

## Building it by hand

1. **GameObject → 3D Object → Terrain.**
2. In the Inspector, click the **gear icon** (Terrain Settings, the rightmost of the five tabs).
   Set **Mesh Resolution → Terrain Width** and **Length** to `200`, **Terrain Height** to `40`.
   Set **Texture Resolutions → Heightmap Resolution** to `513`.
3. **Sculpt**: click the **first tab** (Paint Terrain), choose **Raise or Lower Terrain** from the
   dropdown, pick a large soft brush, and raise the outer edges while leaving the middle flat.
   *You should see* a shallow bowl: flat where the city sits, rising towards the rim.
4. **Paint layers**: same tab, choose **Paint Texture** from the dropdown → **Edit Terrain Layers → Create
   Layer** → pick a texture. Do this three times for cobbles, dirt and snow, then paint each by hand.

> **Why 513 and not 2049.** Heightmap data grows with the *square* of that number, so 2049 is sixteen
> times the data of 513 for detail nobody will see on a valley floor. The brief states the game is judged
> on its weight, so this is a scored decision, not a detail. The whole terrain is 1.9 MB.

The script paints by **height** rather than by hand: cobbles below 12% of max height, snow above 30%,
dirt blended between. That is why the snow sits on the outskirts and the streets stay clear.

---

# Step 2.3 — Hinge joints

Closes: *physical hinge joints.*

## The fast path

**Museum of Time → Build Hinge Set Pieces**. Console prints `HINGES OK`.

*You should see* three objects under `Props → Hinges`: `ClockOfCreationPendulum`, `GalleryGate`,
`ExhibitSignboard`.

## Building them by hand

Every one is: a Cube (or Cylinder) → **Add Component → Rigidbody** → **Add Component → Hinge Joint**,
then set the fields below.

### ClockOfCreationPendulum — position (−9, 4.2, 8)

| Field | Value |
|---|---|
| Rigidbody → Mass | 12 |
| Rigidbody → Angular Damping | 0.05 |
| Hinge → Anchor | 0, 0, 0 |
| Hinge → Axis | 0, 0, 1 |
| Hinge → Use Limits | ✔, Min −35, Max 35 |
| Hinge → Use Spring | ✔, Spring 30, Damper 1, Target 0 |
| Rotation | Z = 25° so it starts already swinging |

The rod and bob hang **below** the pivot, so the centre of mass is low and it swings like a real pendulum
rather than spinning.

### GalleryGate — position (2.5, 1.25, −2)

| Field | Value |
|---|---|
| Rigidbody → Mass | 20, **Use Gravity off** |
| Hinge → Anchor | −0.5, 0, 0 (the gate's own left edge, where the pins would be) |
| Hinge → Axis | 0, 1, 0 (swings about vertical) |
| Hinge → Use Limits | ✔, Min −95, Max 95 |
| Hinge → Use Motor | ✔, Target Velocity 45, Force 600 |

**Two mistakes I made here, both worth avoiding:**

- I first placed it **intersecting a wall**. A joint jammed inside static geometry cannot move at all,
  and there is no warning — it just sits there.
- I first set limits **Min 0, Max 95**. The gate starts at angle 0, which *is* the lower limit, so it was
  pinned against its own stop and the motor had nowhere to drive it. Symmetric limits fixed it.
- I first left it resting **exactly on the floor** (`y = 1.05`, bottom at 0). It ground against the slab.
  Lifting it to 1.25 and turning gravity off hangs it on its pins instead.

### ExhibitSignboard — position (4, 2.6, 6)

Anchor `0, 0.5, 0` so it hangs from its **top edge**, axis `1, 0, 0`, limits ±60, mass 3, started at 18°
so it swings and settles.

## How to see them work

Press **Play**, select `ClockOfCreationPendulum` in the Hierarchy, and watch the Transform's rotation Z
oscillate in the Inspector. The gate drives itself open to 95° in the first couple of seconds and stops
at its limit.

---

# Step 2.4 — Voronoi fracture

Closes: *two assets you fractured yourself using Voronoi, appearing intrinsically in the game.*

## What Voronoi actually is

Worth being able to say plainly, because this is the single most likely thing to be probed:

> A Voronoi cell for a seed point **P** is every point in space closer to **P** than to any other seed.
> For each rival seed **Q**, "closer to P than to Q" means staying on P's side of the perpendicular
> bisector plane of the segment PQ. So a Voronoi cell is simply the **intersection of one half-space per
> rival seed** — and a convex solid.

**Blender no longer ships the Cell Fracture add-on** (it moved to the extensions platform in 4.x), so
`Tools/voronoi_fracture.py` implements the definition above directly:

1. Scatter N seed points through the object's bounding box.
2. For each seed, start with an oversized cube.
3. For every other seed, `bmesh.ops.bisect_plane` at the midpoint with the connecting vector as the
   normal, discarding the far half and capping the hole.
4. Boolean-**intersect** the resulting cell with the source mesh, so the shards add back up to the
   original shape.

## Running it

```
"C:/Program Files/Blender Foundation/Blender 5.2/blender.exe" --background --python Tools/voronoi_fracture.py
```

Prints:

```
FRACTURE ClockOfCreation: 29 shards, 334 faces
FRACTURE FrozenStatue: 48 shards, 971 faces
```

The RNG is seeded, so the same shards come out every run.

> **Seeds versus shards.** The script scatters **64** seeds but yields 29 and 48 shards. Seeds landing in
> empty bounding-box space — outside the actual mesh — produce nothing. That is expected; it is
> oversampled on purpose.

## Doing it by hand in Blender

If you would rather do it interactively: **Edit → Preferences → Get Extensions**, search *Cell Fracture*,
install and enable it. Then select the object, **Object → Quick Effects → Cell Fracture**, set
**Source Limit** to about 30, and press OK. Same result, less control, and you cannot re-run it
reproducibly.

## Bringing it into Unity

**Museum of Time → Build Fracture and LOD Prefabs**. Console prints:

```
FRACTURE PREFAB OK: ClockOfCreation with 29 shards
FRACTURE PREFAB OK: FrozenStatue with 48 shards
```

*You should see* `Assets/Prefabs/World/ClockOfCreation.prefab`. Double-click it: it has an `Intact` child
and a `Shards` child holding 29 pieces, each with a **MeshCollider (Convex)** and a **kinematic
Rigidbody**.

> **Convex is not optional.** A non-convex MeshCollider cannot have a Rigidbody, and every shard needs
> one to be thrown by the explosion.

> **FBX, not OBJ.** I exported OBJ first and Unity's importer **merged all 29 shards into a single
> mesh** — the prefab came out with "1 shard". Same for the LOD tiers. If you export by hand, use FBX.

`FracturedObject.Break()` hides the intact mesh, enables the shards, clears `isKinematic`, and applies
`AddExplosionForce`. The shards despawn after 8 seconds, because leaving 29 rigidbodies lying around
costs frames for something the player has stopped looking at.

**To see it:** enter Play, select `ClockOfCreation` under `Destructibles`, then in the Inspector click the
**⋮** on the *Fractured Object* component → **Break**. The clock bursts apart.

---

# Step 2.5 — LOD

Closes: *two different assets whose polygons you reduced yourself, integrated as LOD in Unity.*

## Running it

```
"C:/Program Files/Blender Foundation/Blender 5.2/blender.exe" --background --python Tools/lod_generate.py
```

```
LOD MarbleStatue: source 9760 tris -> LOD0=9760 (100%), LOD1=4880 (50%), LOD2=1952 (20%)
LOD StoneColumn:  source 4992 tris -> LOD0=4992 (100%), LOD1=2496 (50%), LOD2=998  (20%)
```

**Keep these numbers.** They are the evidence that the reduction really happened, and they belong in the
GDD.

## Doing it by hand in Blender

Select the object → **Modifier Properties** (the wrench icon) → **Add Modifier → Generate → Decimate** →
set **Ratio** to `0.5` → **Ctrl+A → Apply**. The modifier panel shows the resulting **Face Count** live,
which is where those numbers come from. Repeat at `0.2` on a fresh copy for LOD2.

Leave **Decimate Type** on **Collapse**: it merges the least significant edges first, so the silhouette
survives far better than *Un-Subdivide* or *Planar* would manage.

> **A mistake worth avoiding.** I first built the statue at subdivision level 2 — **103,424 triangles**
> and a **14 MB** export, for a prop instanced all over the museum. Level 1 gives 9,760, which is what a
> greybox prop should cost.

## Wiring the LODGroup in Unity

The prefab builder does it, but by hand:

1. Create an empty GameObject named `StoneColumn`.
2. Add three children, each with a MeshFilter + MeshRenderer, one per tier.
3. Select the parent → **Add Component → LOD Group**.
4. In the LOD Group bar, right-click → **Insert Before** until there are three bands, then drag each
   tier's renderer into the matching band.
5. Set the transition percentages to **60 / 25 / 2**.

*You should see* a horizontal coloured bar (LOD 0 green, LOD 1 yellow, LOD 2 blue, Culled grey) with a
little camera icon. **Drag the camera icon along the bar** — the Scene view swaps meshes as you cross
each boundary. That is the fastest way to prove LOD works in a defense.

**In the scene**, six columns line the main hall and three statues stand along the east wall. LOD only
earns its keep when a mesh repeats at varying distances, which is why they are instanced rather than
placed once.

---

# Step 2.6 — Scale

Closes: *scale and realism appropriate to the environment.*

Done **by construction** rather than corrected afterwards — every dimension was chosen in metres against
Noa's 1.7 m:

| Thing | Value | Why |
|---|---|---|
| Step rise / run | 0.17 / 0.30 m | Real staircase proportions |
| Doorway | 2.1 m | Standard door height |
| Mezzanine railing | 1.1 m | Standard guard height |
| Ground floor ceiling | 5.0 m | Gallery, not a house |
| Building | 30 × 20 m | A small museum |
| CharacterController | 2.0 m, radius 0.5 | Noa at human scale |

The playtest measures three of these every run, so a bad edit gets caught rather than noticed later.

---

# Verifying the whole phase

## Play the real built game

```
"C:/Program Files/Unity/Hub/Editor/6000.4.8f1/Editor/Unity.exe" -batchmode -quit -projectPath . -executeMethod BuildScript.BuildDevelopment -logFile Build.log
```

```
cd Build/Playtest && ./MuseumOfTime.exe -playtest -logFile playtest.log && cat playtest-report.txt
```

Current result — **29 of 29**, with the Phase 2 lines being:

```
PASS  Noa can climb the staircase          [rose 4.51m to a peak of 5.59]
PASS  reached the upper floor              [peak y = 5.59, slab at y = 5]
PASS  stays on the upper floor             [y after walking on = 5.59]
PASS  ClockOfCreationPendulum swings       [26.2 degrees after an impulse]
PASS  GalleryGate swings                   [36.0 degrees, limits -95 to 95]
PASS  ExhibitSignboard swings              [35.1 degrees, limits -60 to 60]
PASS  step rise is human-sized             [0.170m per step]
PASS  mezzanine railing is a safe height   [1.10m above the floor]
PASS  fractured object has Voronoi shards  [ClockOfCreation has 29 shards]
PASS  shards fly apart when it breaks      [first shard moved 6.86m]
PASS  LOD groups are in the scene          [9 LODGroup instances]
PASS  two different assets use LOD         [StoneColumn, MarbleStatue]
PASS  tiers get simpler with distance      [verts 9202 / 6079 / 2606]
```

## Just walk around

Press **Play**. Walk to the west wall, up the stairs, and out onto the mezzanine — you can look down into
the main hall. The columns and statues are the LOD props; the pendulum is swinging in the Clock chamber.

---

# Things you should be able to answer in the defense

Added to the Phase 1 list. All from real problems in this phase:

9. **What is a Voronoi cell?** Every point closer to this seed than to any other — the intersection of
   one half-space per rival seed, bounded by perpendicular bisector planes.
10. **Why do the visible stairs have no colliders?** CharacterController step-up is unreliable when the
    same `Move` also carries gravity; the treads are decoration over an invisible 29° wedge.
11. **Why did a thin sloped plate not work?** It has an underside, and the player walks beneath it.
12. **Why FBX and not OBJ?** Unity's OBJ importer merges groups into one mesh, destroying both the shard
    separation and the LOD tiers.
13. **Why must shard colliders be convex?** A non-convex MeshCollider cannot have a Rigidbody.
14. **Why is the heightmap 513?** Heightmap data grows with the square of the resolution, and the brief
    judges the build on its weight.
15. **Why did the motorised gate not move at first?** It started pinned against its own `min = 0` limit,
    so the motor had nowhere to drive it.
16. **Why read `vertexCount` and not `triangles.Length` at runtime?** Imported meshes ship with
    Read/Write disabled; enabling it keeps a second CPU-side copy of every mesh in memory and in the build.
