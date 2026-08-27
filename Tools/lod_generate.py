"""Polygon reduction for LOD, using Blender's Decimate modifier.

The requirement is two different assets whose polygon count we reduced
ourselves, integrated as LOD in Unity. This builds two dense meshes, then
produces three tiers of each with Decimate:

    LOD0  100%   the full mesh, seen close up
    LOD1   50%   mid distance
    LOD2   20%   far away

Decimate COLLAPSE merges the least significant edges first, so silhouette is
preserved far better than a uniform simplification would manage. The actual
triangle counts are printed, because those numbers are the evidence that the
reduction really happened.

Run headlessly:

    blender --background --python Tools/lod_generate.py
"""

import bpy
import os

OUT_DIR = os.path.join(os.path.dirname(os.path.dirname(os.path.abspath(__file__))),
                       "Assets", "Models", "LOD")

RATIOS = [1.0, 0.5, 0.2]


def clear_scene():
    bpy.ops.object.select_all(action="SELECT")
    bpy.ops.object.delete(use_global=False)

    for block in (bpy.data.meshes, bpy.data.objects):
        for item in list(block):
            if item.users == 0:
                block.remove(item)


def make_marble_statue():
    """A dense figure: subdivided so there is real detail to remove."""
    bpy.ops.mesh.primitive_uv_sphere_add(radius=0.30, segments=40, ring_count=28,
                                         location=(0, 0, 1.85))
    head = bpy.context.active_object
    head.name = "MarbleStatue"

    bpy.ops.mesh.primitive_cylinder_add(vertices=40, radius=0.38, depth=1.5,
                                        location=(0, 0, 1.0))
    torso = bpy.context.active_object

    bpy.ops.mesh.primitive_cone_add(vertices=40, radius1=0.62, radius2=0.40,
                                    depth=0.5, location=(0, 0, 0.25))
    robe = bpy.context.active_object

    join(head, [torso, robe])

    # Subdivision turns the primitives into something worth decimating.
    # Level 1, not 2. Level 2 gave a 103k-triangle statue and a 14MB export
    # for a prop that is instanced all over the museum.
    modifier = head.modifiers.new(name="sub", type="SUBSURF")
    modifier.levels = 1
    modifier.render_levels = 1
    bpy.context.view_layer.objects.active = head
    bpy.ops.object.modifier_apply(modifier="sub")

    return head


def make_stone_column():
    """A fluted column, dense enough that LOD is worth having."""
    bpy.ops.mesh.primitive_cylinder_add(vertices=96, radius=0.45, depth=4.0,
                                        location=(0, 0, 2.0))
    shaft = bpy.context.active_object
    shaft.name = "StoneColumn"

    bpy.ops.mesh.primitive_cube_add(size=1.0, location=(0, 0, 0.15))
    base = bpy.context.active_object
    base.scale = (1.2, 1.2, 0.3)

    bpy.ops.mesh.primitive_cube_add(size=1.0, location=(0, 0, 3.9))
    capital = bpy.context.active_object
    capital.scale = (1.3, 1.3, 0.35)

    join(shaft, [base, capital])

    modifier = shaft.modifiers.new(name="sub", type="SUBSURF")
    modifier.levels = 2
    modifier.render_levels = 2
    bpy.context.view_layer.objects.active = shaft
    bpy.ops.object.modifier_apply(modifier="sub")

    return shaft


def join(target, others):
    bpy.ops.object.select_all(action="DESELECT")
    for o in others:
        o.select_set(True)
    target.select_set(True)
    bpy.context.view_layer.objects.active = target
    bpy.ops.object.join()


def triangles(obj):
    """Triangle count, since n-gons make face count misleading."""
    total = 0
    for polygon in obj.data.polygons:
        total += len(polygon.vertices) - 2
    return total


def build_tiers(source, label):
    tiers = []

    for index, ratio in enumerate(RATIOS):
        copy = source.copy()
        copy.data = source.data.copy()
        copy.name = "%s_LOD%d" % (label, index)
        bpy.context.collection.objects.link(copy)

        if ratio < 1.0:
            modifier = copy.modifiers.new(name="decimate", type="DECIMATE")
            modifier.decimate_type = "COLLAPSE"
            modifier.ratio = ratio

            bpy.context.view_layer.objects.active = copy
            bpy.ops.object.modifier_apply(modifier="decimate")

        tiers.append((copy, triangles(copy)))

    return tiers


def export(objects, path):
    bpy.ops.object.select_all(action="DESELECT")
    for o in objects:
        o.select_set(True)
    bpy.context.view_layer.objects.active = objects[0]

    # FBX, not OBJ. Unity's OBJ importer merges every group into one mesh,
    # which destroys the whole point here: the shards and the LOD tiers have
    # to arrive as separate objects.
    bpy.ops.export_scene.fbx(
        filepath=path,
        use_selection=True,
        object_types={"MESH"},
        use_mesh_modifiers=True,
        mesh_smooth_type="FACE",
        axis_forward="-Z",
        axis_up="Y",
        global_scale=1.0,
        apply_unit_scale=True,
        # True: bake the Blender Z-up -> Unity Y-up conversion into the
        # vertex data instead of leaving it on a root transform. Unity
        # rebuilds these prefabs from the meshes alone, so a root-level
        # rotation is discarded and the models arrive lying on their side.
        bake_space_transform=True,
    )


def run_one(builder, label):
    clear_scene()

    source = builder()
    base = triangles(source)

    tiers = build_tiers(source, label)
    bpy.data.objects.remove(source, do_unlink=True)

    counts = ", ".join("LOD%d=%d tris (%d%%)" %
                       (i, t, round(100.0 * t / base))
                       for i, (_, t) in enumerate(tiers))

    print("LOD %s: source %d tris -> %s" % (label, base, counts))

    os.makedirs(OUT_DIR, exist_ok=True)
    export([o for o, _ in tiers], os.path.join(OUT_DIR, label + ".fbx"))
    print("LOD %s: exported to %s.fbx" % (label, label))


def main():
    run_one(make_marble_statue, "MarbleStatue")
    run_one(make_stone_column, "StoneColumn")
    print("LOD DONE")


main()
