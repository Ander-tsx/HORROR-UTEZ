"""
Run a landmark generator in a separate, background Blender: build (or rebuild some
units), optionally render the preview views, and print a geometry report. The user's
open Blender session is never touched.

    BLENDER=/Applications/Blender.app/Contents/MacOS/Blender
    "$BLENDER" -b --factory-startup -P Tools/blender/run_landmark.py -- \
        Tools/blender/gen_cecadec_north.py Assets/_Project/Art/Environment/Landmarks/CECADEC_North.blend \
        [--preview <dir>] [--units ground,ramp] [--engine CYCLES]

The report is one line starting with "[landmark_report]" followed by JSON:
  units         objects per unit
  problems      meshes with degenerate faces, duplicate faces or loose vertices
  stray         objects in the file that no unit owns (hand-made or left over)
  dotted_names  names Blender suffixed with .001 etc. (a name clash: fix before Unity,
                whose rules match names)
"""

import json
import os
import sys

import bmesh
import bpy

args = sys.argv[sys.argv.index("--") + 1:]
gen_path, out_path = os.path.abspath(args[0]), os.path.abspath(args[1])
opts = dict(zip(args[2::2], args[3::2]))

g = {"__name__": "landmark_gen", "TOOLS_DIR": os.path.dirname(gen_path), "OUT_BLEND": out_path}
if "--preview" in opts:
    g["PREVIEW_DIR"] = os.path.abspath(opts["--preview"])
if "--units" in opts:
    g["REBUILD_UNITS"] = opts["--units"].split(",")
if "--engine" in opts:
    g["ENGINE"] = opts["--engine"]
exec(compile(open(gen_path).read(), gen_path, "exec"), g)


def report():
    units, problems = {}, {}
    for o in bpy.data.objects:
        unit = o.get("lm_unit")
        if not unit:
            continue
        units[unit] = units.get(unit, 0) + 1
        if o.type != "MESH":
            continue
        bm = bmesh.new()
        bm.from_mesh(o.data)
        degenerate = sum(1 for f in bm.faces if f.calc_area() < 1e-7)
        seen, dup = set(), 0
        for f in bm.faces:
            key = frozenset(v.index for v in f.verts)
            dup += key in seen
            seen.add(key)
        loose = sum(1 for v in bm.verts if not v.link_faces)
        if degenerate or dup or loose:
            problems[o.name] = {"degenerate": degenerate, "dup_faces": dup, "loose": loose}
        bm.free()
    preview = ("Cam_", "Aim_", "Sun", "Preview_")
    return {
        "units": units,
        "problems": problems,
        "stray": [o.name for o in bpy.data.objects if not o.get("lm_unit") and not o.name.startswith(preview)],
        "dotted_names": [o.name for o in bpy.data.objects if "." in o.name],
        "stats": g.get("STATS"),
        "renders": g.get("RESULT"),
    }


print("[landmark_report] " + json.dumps(report()))
