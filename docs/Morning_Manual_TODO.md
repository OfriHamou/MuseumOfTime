# Morning Manual TODO

Everything here needs you to manually download/import/create something — nothing was faked or replaced with
another primitive placeholder. See `docs/Overnight_Improvement_Report.md` for what was fixed/improved without
your input.

**None of these block the game working or passing verification.** They're presentation-only. Do them in
whatever order matters most to you before the defense.

---

### 1. Warden ("Time Guard") model

**Current problem:** visible placeholder capsule (grey, recoloured but still a capsule).
**Why it matters:** the GDD's enemy reference sheet shows a distinct armoured chronological guardian — right now
the Warden reads as unfinished blockout, the single biggest visual gap left in the game.
**Scene/object:** MuseumNight, FrozenCity, ClockCore — the `TimeWarden` GameObject (same prefab-less object,
rebuilt per scene by `NavigationBuilder`/`FrozenCityContentBuilder`/`ClockCoreContentBuilder`).
**Need:** an armoured fantasy humanoid character, roughly human proportions (the NavMeshAgent radius/height are
tuned to 0.5 m / 2.0 m — a wildly different silhouette will need those retuned too).
**Search terms:** "free low poly fantasy knight FBX humanoid", "stylized armored guard rigged", "low poly
sentinel character Humanoid rig".
**Preferred format:** rigged **Humanoid** FBX (matches Noa's own Mixamo rig pipeline — Mixamo works directly if
the model supports auto-rigging).
**Import to:** `Assets/Art/Characters/Warden/`.
**After you add it:** tell me and I'll wire it the same way Noa was integrated — replace the visible capsule
mesh only, keep `WardenAI`, `NavMeshAgent`, `WardenController` (Animator) and the existing detection/patrol logic
completely untouched.

### 2. Chrono Shadow model

**Current problem:** visible placeholder capsule (purple, emissive-tinted but still a capsule).
**Why it matters:** the GDD shows a wispy blue/purple smoke figure — very different from a solid capsule, and
this enemy's whole identity ("crosses ledges Wardens cannot, steals shards") depends on reading as ghostly.
**Scene/object:** MuseumNight, FrozenCity, ClockCore — `ChronologicalShadow`.
**Need:** either (a) a low-poly humanoid/wraith model, or (b) if no model is found, a real VFX-driven approach
(a Shader Graph dissolve/fresnel ghost material on a simple mesh) — either is fine, but a bare capsule is not.
**Search terms:** "free low poly ghost wraith FBX", "stylized shadow creature rigged", "smoke wisp humanoid low
poly".
**Preferred format:** rigged Humanoid FBX if animated; a static low-poly mesh is acceptable if you'd rather drive
it entirely through material/VFX (it already has a particle drift trail).
**Import to:** `Assets/Art/Characters/Shadow/`.
**After you add it:** tell me — I'll swap the visible mesh only, keep `ShadowAI`, its NavMeshAgent and the
existing Seek/Flee steering untouched.

### 3. Collector (final boss) model

**Current problem:** a small red/yellow capsule blob — does not read as a final boss at all.
**Why it matters:** this is the game's climactic fight; right now it looks like a toy, undermining the whole
ClockCore payoff.
**Scene/object:** ClockCore — `Collector`, plus its child `Shield` (the phase-1 shield visual).
**Need:** an ornate robed sorcerer/clockwork-themed humanoid, ideally larger than the player for a "final boss"
silhouette.
**Search terms:** "free fantasy sorcerer boss FBX rigged", "ornate cultist robe low poly humanoid", "clockwork
mage boss model".
**Preferred format:** rigged Humanoid FBX (doesn't need to share Noa's/the Warden's animations — a simple
idle/hit/defeat set is enough, or none at all if it's mostly stationary).
**Import to:** `Assets/Art/Characters/Collector/`.
**After you add it:** tell me — I'll swap the visible mesh, keep `Collector.cs`'s three-phase state machine,
`SceneLoader` and the shield object reference untouched.

### 4. A real skybox (all three scenes)

**Current problem:** default flat-gradient Unity skybox everywhere.
**Why it matters:** a proper night sky (MuseumNight, seen through no windows currently but relevant if that
changes), a cold dusk/aurora sky (FrozenCity), and something otherworldly (ClockCore) would do a lot for mood
with almost no extra work once imported.
**Scene/object:** each scene's Lighting Settings → Skybox Material.
**Need:** a skybox material/cubemap or HDRI.
**Search terms:** "free night sky skybox Unity URP", "stylized aurora skybox HDRI", "free HDRI night starfield".
**Preferred format:** a Unity `.unitypackage`/HDRI compatible with URP (6-sided cubemap or panoramic HDRI both
work).
**Import to:** `Assets/Art/Skyboxes/`.
**After you add it:** tell me which scene(s) it's for and I'll assign it in that scene's Lighting Settings.

### 5. Museum / FrozenCity architecture kits (optional upgrade)

**Current problem:** none — both are now built from primitives with real generated materials (marble/plaster/
wood/brass for the museum; a procedural window-lit facade for FrozenCity) and read reasonably well after tonight's
pass. This is a "further polish" item, not a broken placeholder.
**Why it matters:** a real modular architecture kit (columns, arches, moldings, window frames, doors) would push
both scenes from "good procedural dressing" to genuinely art-directed.
**Scene/object:** MuseumNight's `Museum` root; FrozenCity's `SceneDressing/Building` objects.
**Search terms:** "free modular museum interior kit Unity", "free medieval/european town modular building kit",
"low poly architecture pack URP".
**Preferred format:** FBX/prefab kit with materials, or a `.unitypackage`.
**Import to:** `Assets/Art/Environment/Museum/` and `Assets/Art/Environment/FrozenCity/` respectively.
**After you add it:** tell me and I'll swap specific dressing pieces (walls, columns) for kit pieces where it's a
straightforward drop-in, keeping the NavMesh, triggers and gameplay objects exactly where they are.

### 6. Real PBR stone/brick/marble textures (optional upgrade)

**Current problem:** the museum's marble/plaster/wood/brass materials are all procedurally generated (Perlin
noise), not photographed/authored PBR textures. They read fine now (marble was fixed from a flat checkerboard to
a proper veined pattern tonight) but are not real photographic materials.
**Why it matters:** real PBR textures (with normal/roughness maps) would meaningfully lift material quality.
**Note:** `docs/Museum_of_Time_GDD.pptx` itself embeds three usable seamless textures (stone masonry, red brick,
dirt) that were identified during an earlier session but never imported — worth pulling those in first before
sourcing new ones.
**Search terms:** "free PBR marble texture seamless", "free PBR brass texture seamless", "polyhaven marble stone
brick".
**Preferred format:** seamless tileable PNG/EXR sets (albedo + normal + roughness), ideally 1–2K to stay inside
the 300 MB build budget.
**Import to:** `Assets/Materials/Museum/PBR/`.
**After you add it:** tell me which material(s) to swap and I'll wire the maps into the existing `.mat` assets.

---

## Not on this list (already fine)

Noa's model/materials/Animator, the Clock of Creation and frozen-statue fracture props (Voronoi work), the
marble-statue/stone-column LOD props, and the hinge set pieces (pendulum/gate/signboard) all use real (if
procedural) assets already in the project and were not flagged during tonight's pass.
