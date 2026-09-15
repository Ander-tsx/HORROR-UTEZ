"""
Bootstrap the building-kit pieces as .blend files.

Each piece is a plain box at its real-metre size, with the origin (pivot) placed where
UtezBuildingBuilder / UtezTerrainBuilder expect it, and per-metre UVs (1 UV unit = 1 m)
so a texture at Tiling (1,1) repeats once per metre.

Unity imports these with bakeAxisConversion on, which passes Blender's axes straight
through to the mesh. So the geometry here is authored in UNITY convention already:
X = width / tiling axis, Y = up, Z = depth. That reads a little unusual inside Blender
(models look like they are lying on their side relative to Blender's Z-up), which is
expected and correct — it lands upright in Unity.

RUN ONCE. It OVERWRITES Kit_*.blend. After this the .blend files are yours to edit in
Blender — Unity re-imports them on focus and every placed instance updates. Re-run only to
reset a piece to the box default.

Usage:
    BLENDER=/Applications/Blender.app/Contents/MacOS/Blender
    "$BLENDER" -b -P Tools/blender/gen_kit_pieces.py -- "$PWD/Assets/_Project/Art/Environment/Kit"
"""

import bpy
import bmesh
import sys
import os

# Unity axes: X = width / tiling axis, Y = height (up), Z = depth.
# pivot: where the object origin sits — 'base' (bottom face), 'top' (top face), 'center'.
PIECES = [
    ("Kit_Wall_4m",  (4.0, 4.0,  0.4),  "base"),
    ("Kit_Floor_4m", (4.0, 0.35, 4.0),  "top"),
    ("Kit_Pilaster", (1.5, 4.0,  0.75), "base"),
    ("Kit_Window",   (4.0, 1.6,  0.2),  "center"),
    ("Kit_Roof",     (4.0, 0.5,  4.0),  "base"),
    ("Kit_Pillar",   (0.5, 4.0,  0.5),  "base"),
    ("Kit_Kerb",     (4.0, 0.3,  0.3),  "base"),
    ("Kit_Pad",      (4.0, 0.05, 4.0),  "base"),
]


def build(name, dims, pivot, out_dir):
    width, height, depth = dims

    bpy.ops.wm.read_factory_settings(use_empty=True)

    mesh = bpy.data.meshes.new(name)
    obj = bpy.data.objects.new(name, mesh)
    bpy.context.scene.collection.objects.link(obj)

    bm = bmesh.new()
    bmesh.ops.create_cube(bm, size=1.0)  # unit cube, centred on the origin

    for v in bm.verts:
        v.co.x *= width
        v.co.y *= height
        v.co.z *= depth

    if pivot == "base":
        shift_y = height * 0.5      # bottom face down to Y = 0
    elif pivot == "top":
        shift_y = -height * 0.5     # top face up to Y = 0
    else:
        shift_y = 0.0
    for v in bm.verts:
        v.co.y += shift_y

    bm.normal_update()

    uv = bm.loops.layers.uv.new("UVMap")
    for face in bm.faces:
        n = face.normal
        for loop in face.loops:
            c = loop.vert.co
            if abs(n.x) > 0.5:       # end faces: depth x height
                loop[uv].uv = (c.z, c.y)
            elif abs(n.y) > 0.5:     # top / bottom: width x depth
                loop[uv].uv = (c.x, c.z)
            else:                    # main faces: width x height
                loop[uv].uv = (c.x, c.y)

    bm.to_mesh(mesh)
    bm.free()
    mesh.update()

    path = os.path.join(out_dir, name + ".blend")
    bpy.ops.wm.save_as_mainfile(filepath=path)
    print("[gen_kit] wrote %s  (%.2f w x %.2f h x %.2f d m, pivot=%s)"
          % (path, width, height, depth, pivot))


def main():
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    if not argv:
        print("[gen_kit] ERROR: pass the output directory after '--'")
        sys.exit(1)

    out_dir = argv[0]
    os.makedirs(out_dir, exist_ok=True)

    for name, dims, pivot in PIECES:
        build(name, dims, pivot, out_dir)

    print("[gen_kit] done — %d pieces" % len(PIECES))


main()
