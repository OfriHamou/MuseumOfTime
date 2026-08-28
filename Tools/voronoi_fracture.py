"""Voronoi fracture, implemented directly rather than via an add-on.

Blender's Cell Fracture add-on is no longer bundled (it moved to the
extensions platform in 4.x), so this builds the Voronoi decomposition from
first principles, which is also a far better thing to be able to explain:

    A Voronoi cell for a seed point P is the set of points closer to P than
    to any other seed. For each other seed Q, that means staying on P's side
    of the perpendicular bisector plane of PQ. So a cell is just the
    intersection of one half-space per other seed.

The implementation is exactly that. Start each cell as an oversized cube,
bisect it once per other seed with `bmesh.ops.bisect_plane` (discarding the
far half and capping the hole), then boolean-intersect the result with the
source mesh so the pieces add back up to the original shape.

Run headlessly:

    blender --background --python Tools/voronoi_fracture.py

Seeded RNG, so the same shards come out every time.
"""

import bpy
import bmesh
import mathutils
import os
import random
import sys

OUT_DIR = os.path.join(os.path.dirname(os.path.dirname(os.path.abspath(__file__))),
                       "Assets", "Models", "Fractured")

SEED = 20260824
# Seeds are scattered through the bounding box, and any that land in empty
# space outside the shape produce no shard, so this is oversampled to land
# in the 15-40 shard range the plan asks for.
PIECES = 64


def clear_scene():
    bpy.ops.object.select_all(action="SELECT")
    bpy.ops.object.delete(use_global=False)

    for block in (bpy.data.meshes, bpy.data.objects):
        for item in list(block):
            if item.users == 0:
                block.remove(item)


def make_clock_of_creation():
    """A large clock face: the museum's centrepiece, which shatters."""
    bpy.ops.mesh.primitive_cylinder_add(vertices=48, radius=1.5, depth=0.35)
    body = bpy.context.active_object
    body.name = "ClockOfCreation"

    # A raised rim, so the shards are not all identical flat discs.
    bpy.ops.mesh.primitive_torus_add(
        major_radius=1.45, minor_radius=0.12, major_segments=48, minor_segments=8)
    rim = bpy.context.active_object

    ctx_join(body, [rim])
    return body


def make_frozen_statue():
    """A simple standing figure for the frozen city."""
    bpy.ops.mesh.primitive_cylinder_add(vertices=24, radius=0.35, depth=1.6,
                                        location=(0, 0, 0.8))
    torso = bpy.context.active_object
    torso.name = "FrozenStatue"

    bpy.ops.mesh.primitive_uv_sphere_add(radius=0.26, segments=20, ring_count=12,
                                         location=(0, 0, 1.85))
    head = bpy.context.active_object

    bpy.ops.mesh.primitive_cube_add(size=1.0, location=(0, 0, 0.06))
    plinth = bpy.context.active_object
    plinth.scale = (0.55, 0.55, 0.12)

    ctx_join(torso, [head, plinth])
    return torso


def ctx_join(target, others):
    bpy.ops.object.select_all(action="DESELECT")
    for o in others:
        o.select_set(True)
    target.select_set(True)
    bpy.context.view_layer.objects.active = target
    bpy.ops.object.join()


def seed_points(source, count, rng):
    """Random seeds inside the source's bounding box."""
    corners = [source.matrix_world @ mathutils.Vector(c)
               for c in source.bound_box]

    lo = mathutils.Vector((min(c.x for c in corners),
                           min(c.y for c in corners),
                           min(c.z for c in corners)))
    hi = mathutils.Vector((max(c.x for c in corners),
                           max(c.y for c in corners),
                           max(c.z for c in corners)))

    return [mathutils.Vector((rng.uniform(lo.x, hi.x),
                              rng.uniform(lo.y, hi.y),
                              rng.uniform(lo.z, hi.z)))
            for _ in range(count)]


def build_cell(point, others, size):
    """One Voronoi cell: a big cube cut down by every bisector plane."""
    mesh = bpy.data.meshes.new("cell")
    bm = bmesh.new()
    bmesh.ops.create_cube(bm, size=size)

    # Move the cube so it is centred on this seed.
    bmesh.ops.translate(bm, verts=bm.verts, vec=point)

    for other in others:
        if other == point:
            continue

        offset = other - point
        distance = offset.length
        if distance < 1e-6:
            continue

        normal = offset.normalized()
        plane_co = point + (offset * 0.5)   # midpoint: the bisector

        # Keep the half containing `point`, cap the opening it leaves.
        bmesh.ops.bisect_plane(
            bm,
            geom=list(bm.verts) + list(bm.edges) + list(bm.faces),
            plane_co=plane_co,
            plane_no=normal,
            clear_outer=True,
            clear_inner=False,
        )

        bmesh.ops.holes_fill(bm, edges=bm.edges)

    bm.to_mesh(mesh)
    bm.free()

    obj = bpy.data.objects.new("cell", mesh)
    bpy.context.collection.objects.link(obj)
    return obj


def fracture(source, pieces, rng):
    """Split `source` into Voronoi shards, returning the shard objects."""
    dims = source.dimensions
    size = max(dims.x, dims.y, dims.z) * 4.0

    points = seed_points(source, pieces, rng)
    shards = []

    for index, point in enumerate(points):
        cell = build_cell(point, points, size)

        # Intersect the cell with the source: the shard is the overlap.
        boolean = cell.modifiers.new(name="cut", type="BOOLEAN")
        boolean.operation = "INTERSECT"
        boolean.object = source
        boolean.solver = "EXACT"

        bpy.context.view_layer.objects.active = cell
        bpy.ops.object.modifier_apply(modifier="cut")

        # Cells whose seed sat outside the shape come back empty.
        if len(cell.data.vertices) == 0:
            bpy.data.objects.remove(cell, do_unlink=True)
            continue

        cell.name = "%s_Shard_%02d" % (source.name, len(shards))

        # Origin at the shard's own centre, so Unity can spin each piece
        # about itself instead of about the original object's pivot.
        bpy.ops.object.select_all(action="DESELECT")
        cell.select_set(True)
        bpy.context.view_layer.objects.active = cell
        bpy.ops.object.origin_set(type="ORIGIN_GEOMETRY", center="MEDIAN")

        shards.append(cell)

    return shards


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
    rng = random.Random(SEED)

    source = builder()
    bpy.context.view_layer.objects.active = source

    shards = fracture(source, PIECES, rng)

    triangles = sum(len(s.data.polygons) for s in shards)
    print("FRACTURE %s: %d shards, %d faces" % (label, len(shards), triangles))

    if not shards:
        print("FRACTURE %s: FAILED, no shards produced" % label)
        return False

    bpy.data.objects.remove(source, do_unlink=True)

    os.makedirs(OUT_DIR, exist_ok=True)
    export(shards, os.path.join(OUT_DIR, label + ".fbx"))
    print("FRACTURE %s: exported to %s.fbx" % (label, label))
    return True


def main():
    ok = True
    ok &= run_one(make_clock_of_creation, "ClockOfCreation")
    ok &= run_one(make_frozen_statue, "FrozenStatue")
    print("FRACTURE DONE" if ok else "FRACTURE FAILED")


main()
