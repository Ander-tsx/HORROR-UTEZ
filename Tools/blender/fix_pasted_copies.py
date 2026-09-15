"""
Clean up objects the user copy-pasted by hand in a landmark .blend (for example extending
the hill in CECADEC_North). Geometry and transforms are not touched.

    "$BLENDER" -b --factory-startup -P Tools/blender/fix_pasted_copies.py -- \
        Assets/_Project/Art/Environment/Landmarks/CECADEC_North.blend

1. Material slots Kit_X.00N -> Kit_X: Unity (PsxKitImporter) remaps by exact name, so a
   dotted copy imports without its texture. The dotted materials are then removed.
2. Stacked duplicates (same world geometry and materials, or empties on the same spot,
   i.e. pasted twice) are deleted; the undotted original is kept.
3. Dotted names renamed: base.NNN -> base_C<n>, base_Light.NNN -> base_C<n>_Light so the
   lamp rule still sees the suffix. Their lm_unit gets "_extension" appended, so rebuilding
   the original unit no longer deletes them.
4. Planar per-metre UVs re-projected from world position (realign_uvs.realign) on objects
   matching REALIGN.
"""

import os
import re
import sys

import bpy

HERE = os.path.dirname(os.path.abspath(__file__))
_ru = {}
_src = os.path.join(HERE, "realign_uvs.py")
exec(compile(open(_src).read().replace("\nmain()\n", "\n"), _src, "exec"), _ru)
realign = _ru["realign"]

DOT = re.compile(r"^(.*)\.(\d{3})$")
REALIGN = re.compile(r"^(Garden_Terrace|Garden_Wall|Rock_)")


def base(name):
    m = DOT.match(name)
    return m.group(1) if m else name


def remap_materials():
    remapped = 0
    for o in bpy.data.objects:
        if o.type != "MESH":
            continue
        for i, m in enumerate(o.data.materials):
            mm = m and DOT.match(m.name)
            if mm and bpy.data.materials.get(mm.group(1)):
                o.data.materials[i] = bpy.data.materials[mm.group(1)]
                remapped += 1
    dropped = [m.name for m in list(bpy.data.materials) if DOT.match(m.name) and m.users == 0]
    for n in dropped:
        bpy.data.materials.remove(bpy.data.materials[n])
    return remapped, dropped


def _key(o):
    mw = o.matrix_world
    if o.type == "MESH":
        verts = sorted(tuple(round(c, 3) for c in (mw @ v.co)) for v in o.data.vertices)
        return ("M", tuple(verts), tuple(m.name if m else "" for m in o.data.materials))
    return ("E", tuple(round(c, 3) for c in mw.translation))


def delete_stacked():
    groups = {}
    for o in bpy.data.objects:
        groups.setdefault((base(o.name), _key(o)), []).append(o)
    deleted = []
    for objs in groups.values():
        objs.sort(key=lambda o: (DOT.match(o.name) is not None, o.name))
        for o in objs[1:]:
            deleted.append(o.name)
            bpy.data.objects.remove(o, do_unlink=True)
    return deleted


def _copy_name(b, n):
    return f"{b[:-len('_Light')]}_C{n}_Light" if b.endswith("_Light") else f"{b}_C{n}"


def rename_copies():
    renamed, counters = {}, {}
    for o in sorted((o for o in bpy.data.objects if DOT.match(o.name)), key=lambda o: o.name):
        b = base(o.name)
        n = counters.get(b, 0) + 1
        while bpy.data.objects.get(_copy_name(b, n)):
            n += 1
        counters[b] = n
        renamed[o.name] = o.name = _copy_name(b, n)
        unit = o.get("lm_unit")
        if unit and not unit.endswith("_extension"):
            o["lm_unit"] = unit + "_extension"
        if o.type == "MESH" and o.data.users == 1:
            o.data.name = o.name
    return renamed


def main():
    path = os.path.abspath(sys.argv[sys.argv.index("--") + 1])
    bpy.ops.wm.open_mainfile(filepath=path)
    remapped, dropped = remap_materials()
    deleted = delete_stacked()
    renamed = rename_copies()
    fixed = [o.name for o in bpy.data.objects if o.type == "MESH" and REALIGN.match(o.name)]
    for n in fixed:
        realign(bpy.data.objects[n])
    bpy.ops.wm.save_mainfile()
    print("[fix_pasted] remapped slots:", remapped, "| dropped materials:", len(dropped))
    print("[fix_pasted] deleted stacked duplicates:", deleted)
    print("[fix_pasted] renamed:", renamed)
    print("[fix_pasted] realigned UVs:", len(fixed))
    left = [o.name for o in bpy.data.objects if DOT.match(o.name)]
    left += [m.name for m in bpy.data.materials if DOT.match(m.name)]
    print("[fix_pasted] still dotted:", left)


main()
