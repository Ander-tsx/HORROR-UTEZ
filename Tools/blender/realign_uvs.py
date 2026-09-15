"""
Re-project the per-metre UVs of landmark objects from their WORLD position, after
they were edited by hand in Blender (extrude, move vertices, scale the object).

geolib bakes UVs from the vertex positions at build time, so an edited mesh keeps
UVs that no longer match where its faces are: joint grids go crooked (Ground_Walkway)
and scaled objects stretch their map (TreeBed_Central). This recomputes them with the
same rule geolib uses — dominant axis of each face, 1 UV unit = 1 m, Kit_Aggregate
turned 45 degrees — so an untouched object comes out identical and an edited one
lines up with its neighbours again. Geometry, transforms and materials are not touched.

Only for objects with planar per-metre UVs (ground slabs, planter beds, walls). Not
for sweeps (kerbs), foliage cards or decals: those carry their own UV layout.

    "$BLENDER" -b --factory-startup -P Tools/blender/realign_uvs.py -- \
        Assets/_Project/Art/Environment/Landmarks/CECADEC_North.blend Ground_Walkway TreeBed_Central
"""

import math
import os
import sys

import bmesh
import bpy

# Materials laid diagonally on site; must match the rot= passed in lm_plaza.ground().
ROTATED = {"Kit_Aggregate": 45.0}


def realign(obj):
    world = obj.matrix_world
    turn = world.to_3x3()
    mats = obj.data.materials
    bm = bmesh.new()
    bm.from_mesh(obj.data)
    uv = bm.loops.layers.uv.active or bm.loops.layers.uv.new("UVMap")
    for f in bm.faces:
        n = turn @ f.normal
        mat = mats[f.material_index] if f.material_index < len(mats) else None
        rot = math.radians(ROTATED.get(mat.name if mat else "", 0.0))
        cs, sn = math.cos(rot), math.sin(rot)
        for loop in f.loops:
            c = world @ loop.vert.co
            if abs(n.z) >= max(abs(n.x), abs(n.y)):
                u, v = c.x, c.y
            elif abs(n.x) >= abs(n.y):
                u, v = c.y, c.z
            else:
                u, v = c.x, c.z
            loop[uv].uv = (u * cs - v * sn, u * sn + v * cs)
    bm.to_mesh(obj.data)
    bm.free()
    obj.data.update()


def main():
    args = sys.argv[sys.argv.index("--") + 1:]
    path, names = os.path.abspath(args[0]), args[1:]
    if not names:
        sys.exit("[realign_uvs] name the objects to realign")
    bpy.ops.wm.open_mainfile(filepath=path)
    missing = [n for n in names if bpy.data.objects.get(n) is None]
    if missing:
        sys.exit(f"[realign_uvs] not in {path}: {missing}")
    for n in names:
        realign(bpy.data.objects[n])
    bpy.ops.wm.save_mainfile()
    print(f"[realign_uvs] realigned {names} in {path}")


main()
