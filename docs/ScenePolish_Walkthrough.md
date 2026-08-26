# Scene Polish — Presentation Pass Walkthrough

**How the three gameplay scenes were dressed from blockout into intentional-looking places, and where to see it.**

This is a presentation pass, not a numbered phase: the game was already mechanically complete through Phase 7, but a
manual test showed the gameplay scenes still read as greybox. The goal was visual coherence for a full manual
playthrough — **no new gameplay**, and every existing system (NavMesh, triggers, progression, Phase 7 audio/lighting/
grading/VFX) preserved.

Everything here was built through one reproducible headless Editor script, `Assets/Editor/ScenePolishBuilder.cs`:

```
Museum of Time -> Polish Scenes (dressing)      (menu)
Unity.exe -batchmode -quit -projectPath . -executeMethod ScenePolishBuilder.BuildFromCommandLine
```

Idempotent: all added geometry lives under one `SceneDressing` root per scene, cleared and rebuilt each run.

## The one safety rule that shaped everything

**All dressing is decorative — colliders are stripped — so it can never block a NavMeshAgent or the player.** The
single exception is ClockCore's four containment walls, which sit on the arena edge (outside where the agents patrol)
and only stop the player walking off. **No navmesh is re-baked**, and the only touches to existing gameplay objects
are cosmetic (assigning a material to an enemy / pickup / anchor renderer). This is why the full test suite still
passes unchanged — nothing about gameplay logic, pathing or progression moved.

Assets used are all already in the repo: the `MarbleStatue` and `StoneColumn` LOD prefabs (marble, collider-free),
the museum materials (Marble / Wood / Plaster / Brass), plus a handful of small dressing materials created under
`Assets/Materials/Dressing/` (Warden, Shadow, ShardGlow, LensGlow, Collector, Shield, Roof).

---

## MuseumNight — a light readability pass

Already the best-dressed scene (a two-storey ProBuilt museum with real materials, a staircase, six columns, three
marble statues, the hinge set-pieces, the Clock of Creation fracture and Phase 7's warm/cold lighting). Only the
gameplay pieces needed to read more clearly:

- Time Shards → glowing cyan (`ShardGlow`); the Time Lens → warm amber (`LensGlow`); the plaque → brass.
- The Warden → dark grey (`Warden`); the Chronological Shadow → a ghostly emissive purple (`Shadow`).

## FrozenCity — the city that froze before sunset

This was the biggest blockout offender (terrain + a two-cube tower + primitives) and the richest opportunity, so it
got the heaviest pass, matching the GDD's "everyone is motionless … the clock tower visible from spawn":

- **A street to the tower.** Twelve building blocks (plaster, dark roofs) flank a clear central corridor running from
  spawn (z ≈ −20) to the tower (z ≈ 35), giving the player an obvious path. The corridor itself (x within ±4) is left
  clear.
- **Motionless citizens.** Twelve marble-statue figures scattered through the outskirts — the townspeople caught mid-
  life when the city froze. Kept clear of the Warden patrol box and the central path.
- **A lit route.** Ten amber lantern posts down the central path, drawing the eye to the tower.
- **A readable tower landmark.** The placeholder shaft/belfry gained a brass **clock face** with hands on the
  approach side (so it reads as a clock, and as the objective, from spawn), a stepped roof and spire, and four stone
  buttress columns at its base.
- **Gameplay readability.** Warden/Shadow recoloured as in MuseumNight; the Time Anchors' lens-visuals and the Chrono
  Hourglass pickup given the amber `LensGlow` so they read when revealed.

## ClockCore — the inverted museum & the Collector's arena

Previously a bare 40×40 floor. Dressed into an enclosed boss arena that reads as the museum turned inside out:

- **Enclosing marble walls** on the four floor edges (the one colliding dressing — contains the fight; outside the
  agents' central patrol so navmesh is untouched).
- **An inverted museum.** A marble **ceiling**, with three marble statues and two columns **hanging upside-down** from
  it — floor become ceiling.
- **A framed arena.** Eight stone columns ring the space and three statues stand on the floor, framing the fight.
- **A focal boss.** A marble **dais** under the Collector; the Collector recoloured an ominous dark red and its shield
  given a bright emissive material so the "break the shield" beat reads; Warden/Shadow/anchors recoloured as elsewhere.

---

## Verification

`Tools/verify.ps1` — compiles clean, **81/87 PlayMode tests passing, 0 failed** (6 intentionally ignored since Phase
0). No test changed behaviour because the dressing is decorative; the gameplay-content tests
(`FrozenCitySceneTests`, `ClockCoreSceneTests`, etc.) still pass, confirming enemies, anchors, the gear puzzle, the
Collector, NavMesh agents and progression are all intact.

### One test updated (and why)

`AudioAndVfxTests` — the slow-time test previously asserted the component `AudioLowPassFilter` engages. During this
pass the project's `Assets/Audio/GameAudioMixer.mixer` turned out to have been created (the Phase 7 manual AudioMixer
step, now done: Master/Music/SFX groups, Normal/SlowTime snapshots, an SFX Lowpass), so `AudioManager` now delivers
slow-time filtering via a **snapshot transition** and leaves the component filter off by design. The test is now
mixer-aware: it verifies the slow-time enter/exit cues fire in both modes, and only checks the component filter when
no mixer is wired. `AudioManager.UsingMixer` was made public for this, and `AudioManager` gained a small self-heal
(`EnsurePlayerRefs`) so a Start-order miss can't leave its player references null. None of this is gameplay logic.

---

## Still requires manual / visual adjustment

- **A visual look in the Editor or a build.** Composition, spacing and readability were built to plan but not seen;
  confirm each scene reads as intended (the frozen street leads the eye to the tower; the ClockCore inversion reads;
  nothing floats or clips badly).
- **Non-colliding facades.** FrozenCity's buildings and all scattered props are decorative (non-colliding) so they
  never block paths — a player *can* walk through a building facade. The central street is kept clear so a normal
  route never needs to; confirm this looks acceptable, or a later pass can add colliders where they are safely off
  the navmesh.
- **Placeholder art.** Materials are procedural/solid-colour and the "citizens" are marble statues rather than posed
  figures; a real art pass (textured buildings, a skybox, snow cover, posed frozen people) is future polish beyond
  this coherence pass.
- **The Phase 7 manual items** remain as documented there: the optional lightmap bake, and the D2 real-hardware
  framerate check. (The AudioMixer manual item is now **done** — see above.)
