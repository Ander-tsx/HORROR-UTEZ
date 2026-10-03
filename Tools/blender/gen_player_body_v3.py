"""Player body v3: a fully modelled student body on the EXISTING rig and photo head.

The face bake (gen_player_v2.py) needs source photos that are not in the repository, so
this script opens the saved PlayerCharacter.blend, keeps PlayerHead and PlayerRig exactly
as they are, and replaces only PlayerBody with a detailed low-poly body:
shaped polo shirt (white / grey band / navy hem) with collar and short sleeves, defined
arms, hands with four fingers and a thumb, belt, jeans with knees and cuffs, sneakers
with soles and laces, and a school backpack.

Weights are deterministic: every part may only use its own bones (an arm never pulls the
backpack), blended by distance to the two nearest bone segments.

Run: blender -b Assets/_Project/Art/Characters/Player/Source~/PlayerCharacter.blend \
         --python Tools/blender/gen_player_body_v3.py
Character frame: x = his right, y = forward, z = up (metres); Blender = (-x, -y, z).
"""
import math
import os

import bmesh
import bpy
from mathutils import Vector

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.dirname(os.path.dirname(HERE))
CHAR_DIR = os.path.join(ROOT, "Assets", "_Project", "Art", "Characters", "Player")
OUT_FBX = os.path.join(CHAR_DIR, "PlayerCharacter.fbx")
OUT_BLEND = os.path.join(CHAR_DIR, "Source~", "PlayerCharacter.blend")

MATS = ["Player_Skin", "Player_ShirtWhite", "Player_ShirtGrey", "Player_ShirtNavy", "Player_Pants", "Player_Shoes"]


def cw(x, y, z):
    return Vector((-x, -y, z))


class Body:
    def __init__(self):
        self.bm = bmesh.new()
        self.tag = self.bm.verts.layers.int.new("part")
        self.parts = []          # part index -> allowed bones

    def part(self, bones):
        self.parts.append(bones)
        return len(self.parts) - 1

    def loft(self, rings, mat, part, cap0=True, cap1=True):
        n = len(rings[0])
        vs = [[self._v(p, part) for p in ring] for ring in rings]
        for j in range(len(rings) - 1):
            for i in range(n):
                self._f([vs[j][i], vs[j][(i + 1) % n], vs[j + 1][(i + 1) % n], vs[j + 1][i]], mat)
        if cap0:
            self._f(list(reversed(vs[0])), mat)
        if cap1:
            self._f(vs[-1], mat)

    def box(self, c, size, mat, part):
        x, y, z = c
        a, b, h = (s / 2 for s in size)
        ring0 = [(x - a, y - b, z - h), (x + a, y - b, z - h), (x + a, y + b, z - h), (x - a, y + b, z - h)]
        ring1 = [(x - a, y - b, z + h), (x + a, y - b, z + h), (x + a, y + b, z + h), (x - a, y + b, z + h)]
        self.loft([ring0, ring1], mat, part)

    def tube(self, path, radii, mat, part, segs=6):
        pts = [Vector(p) for p in path]
        rings, prev = [], None
        for k, p in enumerate(pts):
            t = (pts[min(k + 1, len(pts) - 1)] - pts[max(k - 1, 0)]).normalized()
            ref = Vector((0, 0, 1)) if abs(t.z) < 0.9 else Vector((1, 0, 0))
            n = prev if prev is not None else t.cross(ref).normalized()
            n = (n - t * n.dot(t)).normalized()
            prev = n
            b = t.cross(n)
            r = radii[k] if isinstance(radii, (list, tuple)) else radii
            rings.append([tuple(p + (n * math.cos(2 * math.pi * i / segs) + b * math.sin(2 * math.pi * i / segs)) * r)
                          for i in range(segs)])
        self.loft(rings, mat, part)

    def _v(self, p, part):
        v = self.bm.verts.new(cw(*p))
        v[self.tag] = part
        return v

    def _f(self, verts, mat):
        try:
            f = self.bm.faces.new(verts)
        except ValueError:
            return
        f.material_index = MATS.index(mat)
        f.smooth = True


def ring_xy(cx, cy, z, w, yf, yb, segs=16, power=2.4):
    """Horizontal superellipse ring: half-width w, front depth yf, back depth yb."""
    pts = []
    for i in range(segs):
        a = 2 * math.pi * i / segs
        c, s = math.cos(a), math.sin(a)
        x = cx + w * math.copysign(abs(c) ** (2 / power), c)
        d = yf if s > 0 else yb
        y = cy + d * math.copysign(abs(s) ** (2 / power), s)
        pts.append((x, y, z))
    return pts


def ring_x(x, cy, cz, ry, rz, segs=10):
    """Ring perpendicular to the X axis (arms in T-pose)."""
    return [(x, cy + math.cos(2 * math.pi * i / segs) * ry, cz + math.sin(2 * math.pi * i / segs) * rz) for i in range(segs)]


def build(body):
    torso = body.part(["Hips", "Spine", "Chest", "UpperChest", "Neck", "RightShoulder", "LeftShoulder"])
    # Shirt torso. Bands are separate lofts so the material edges are clean.
    sections = [(0.955, 0.158, 0.098, 0.098), (1.07, 0.152, 0.098, 0.092), (1.13, 0.16, 0.106, 0.095),
                (1.22, 0.172, 0.116, 0.1), (1.3, 0.18, 0.112, 0.1), (1.36, 0.182, 0.1, 0.096),
                (1.405, 0.15, 0.075, 0.078), (1.44, 0.075, 0.058, 0.058)]
    bands = [(0, 1, "Player_ShirtNavy"), (1, 2, "Player_ShirtGrey"), (2, 7, "Player_ShirtWhite")]
    for a, b, mat in bands:
        rings = [ring_xy(0, 0, z, w, yf, yb) for z, w, yf, yb in sections[a:b + 1]]
        body.loft(rings, mat, torso, cap0=(a == 0), cap1=(b == 7))
    # polo collar and placket
    neck = body.part(["Neck", "UpperChest"])
    body.loft([ring_xy(0, 0.0, 1.425, 0.078, 0.064, 0.062, 12), ring_xy(0, 0.0, 1.465, 0.07, 0.06, 0.058, 12),
               ring_xy(0, -0.004, 1.475, 0.085, 0.07, 0.066, 12)], "Player_ShirtWhite", neck, False, False)
    body.box((0, 0.113, 1.33), (0.035, 0.012, 0.1), "Player_ShirtGrey", torso)
    for z in (1.3, 1.34):
        body.box((0, 0.121, z), (0.012, 0.006, 0.012), "Player_ShirtWhite", torso)
    body.box((-0.085, 0.117, 1.28), (0.07, 0.008, 0.07), "Player_ShirtGrey", torso)            # chest pocket
    body.tube([(0, 0, 1.43), (0, -0.004, 1.5), (0, -0.004, 1.56)], [0.05, 0.047, 0.045], "Player_Skin", neck, 10)

    for side, sx in (("Right", 1), ("Left", -1)):
        arm = body.part([side + "Shoulder", side + "UpperArm", side + "LowerArm"])
        y0 = -0.014
        sleeve = [(0.13, 0.072, 0.07), (0.19, 0.07, 0.068), (0.26, 0.064, 0.06), (0.31, 0.062, 0.058)]
        body.loft([ring_x(sx * x, y0, 1.395, ry, rz) for x, ry, rz in sleeve], "Player_ShirtWhite", arm, False, False)
        body.loft([ring_x(sx * x, y0, 1.395, ry, rz) for x, ry, rz in ((0.305, 0.064, 0.06), (0.325, 0.064, 0.06))],
                  "Player_ShirtGrey", arm, False, False)
        skin = [(0.3, 0.05, 0.048), (0.38, 0.046, 0.045), (0.45, 0.04, 0.039), (0.52, 0.043, 0.04),
                (0.6, 0.036, 0.032), (0.69, 0.03, 0.022)]
        body.loft([ring_x(sx * x, y0 - 0.006 * (x > 0.45), 1.395, ry, rz) for x, ry, rz in skin], "Player_Skin", arm, True, False)
        hand = body.part([side + "LowerArm", side + "Hand"])
        palm = [(0.685, 0.03, 0.022), (0.72, 0.045, 0.02), (0.77, 0.047, 0.016)]
        body.loft([ring_x(sx * x, -0.02, 1.393, ry, rz, 8) for x, ry, rz in palm], "Player_Skin", hand, True, True)
        for f in range(4):
            y = -0.02 - 0.033 + f * 0.022
            ln = 0.085 if f in (1, 2) else 0.07
            body.tube([(sx * 0.765, y, 1.392), (sx * (0.77 + ln * 0.5), y, 1.389), (sx * (0.77 + ln), y, 1.384)],
                      [0.0105, 0.0095, 0.008], "Player_Skin", hand, 6)
        body.tube([(sx * 0.715, 0.02, 1.39), (sx * 0.745, 0.045, 1.386), (sx * 0.775, 0.058, 1.382)],
                  [0.012, 0.011, 0.009], "Player_Skin", hand, 6)
        # Watch on the left wrist.
        if side == "Left":
            body.loft([ring_x(sx * x, -0.02, 1.395, 0.034, 0.027, 8) for x in (0.655, 0.675)], "Player_Shoes", hand, False, False)

        # Jeans legs, sneakers.
        leg = body.part([side + "UpperLeg", side + "LowerLeg", "Hips"])
        hx = sx * 0.092
        legs = [(0.93, 0.09, 0.0), (0.8, 0.083, 0.004), (0.62, 0.068, 0.01), (0.5, 0.06, 0.016), (0.4, 0.058, 0.006),
                (0.27, 0.054, -0.002), (0.13, 0.052, -0.008), (0.095, 0.056, -0.01)]
        body.loft([ring_xy(hx, y, z, r, r * 1.02, r, 12, 2.0) for z, r, y in legs], "Player_Pants", leg, True, True)
        for z in (0.5, 0.88):
            body.box((hx + sx * 0.0, 0.055 if z < 0.6 else 0.07, z), (0.06, 0.012, 0.03), "Player_Pants", leg)
        shoe = body.part([side + "Foot", side + "Toes", side + "LowerLeg"])
        sole = [(-0.07, 0.04), (0.02, 0.05), (0.12, 0.048), (0.19, 0.036)]
        body.loft([[(hx - w, y, 0.0), (hx + w, y, 0.0), (hx + w, y, 0.03), (hx - w, y, 0.03)] for y, w in sole],
                  "Player_ShirtWhite", shoe)
        upper = [(-0.065, 0.034, 0.03, 0.11), (0.0, 0.042, 0.03, 0.115), (0.07, 0.045, 0.03, 0.09),
                 (0.14, 0.04, 0.03, 0.065), (0.185, 0.03, 0.03, 0.045)]
        rings = []
        for y, w, z0, z1 in upper:
            rings.append([(hx - w, y, z0), (hx + w, y, z0), (hx + w * 0.85, y, z1), (hx, y, z1 + 0.008), (hx - w * 0.85, y, z1)])
        body.loft(rings, "Player_Shoes", shoe)
        for i in range(3):
            body.box((hx, 0.06 + i * 0.03, 0.098 - i * 0.012), (0.05, 0.012, 0.006), "Player_ShirtWhite", shoe)
        body.box((hx, -0.07, 0.07), (0.05, 0.01, 0.06), "Player_ShirtWhite", shoe)

    hips = body.part(["Hips", "RightUpperLeg", "LeftUpperLeg", "Spine"])
    pel = [(0.85, 0.15, 0.09, 0.1), (0.92, 0.165, 0.1, 0.112), (0.965, 0.162, 0.1, 0.105)]
    body.loft([ring_xy(0, 0, z, w, yf, yb) for z, w, yf, yb in pel], "Player_Pants", hips)
    body.loft([ring_xy(0, 0, z, 0.164, 0.102, 0.104) for z in (0.94, 0.975)], "Player_Shoes", hips, False, False)  # belt
    body.box((0, 0.105, 0.957), (0.05, 0.01, 0.034), "Player_ShirtGrey", hips)
    for sx in (-1, 1):
        body.box((sx * 0.11, -0.105, 0.88), (0.09, 0.008, 0.1), "Player_Pants", hips)             # back pockets

    pack = body.part(["UpperChest"])
    body.box((0, -0.175, 1.2), (0.29, 0.13, 0.37), "Player_ShirtNavy", pack)
    body.box((0, -0.25, 1.13), (0.22, 0.04, 0.17), "Player_ShirtNavy", pack)
    body.box((0, -0.272, 1.16), (0.18, 0.006, 0.01), "Player_ShirtGrey", pack)
    body.box((0, -0.18, 1.395), (0.1, 0.03, 0.04), "Player_ShirtGrey", pack)
    straps = body.part(["UpperChest", "Chest"])
    for sx in (-1, 1):
        body.tube([(sx * 0.085, -0.15, 1.36), (sx * 0.1, -0.06, 1.425), (sx * 0.1, 0.06, 1.41), (sx * 0.115, 0.118, 1.3),
                   (sx * 0.125, 0.112, 1.12), (sx * 0.12, -0.11, 1.04)], 0.014, "Player_ShirtGrey", straps, 5)


def weights(obj, arm, body):
    tag = obj.data.attributes.get("part")
    bones = {b.name: (b.head_local.copy(), b.tail_local.copy()) for b in arm.data.bones}
    groups = {name: obj.vertex_groups.new(name=name) for name in bones}
    for v in obj.data.vertices:
        allowed = body.parts[tag.data[v.index].value]
        scored = []
        for name in allowed:
            a, b = bones[name]
            ab = b - a
            t = max(0.0, min(1.0, (v.co - a).dot(ab) / max(ab.length_squared, 1e-9)))
            d = (v.co - (a + ab * t)).length
            scored.append((d, name))
        scored.sort()
        top = scored[:2]
        ws = [1.0 / (d ** 4 + 1e-6) for d, _ in top]
        total = sum(ws)
        for (d, name), w in zip(top, ws):
            if w / total > 0.02:
                groups[name].add([v.index], w / total, "REPLACE")


def main():
    arm = bpy.data.objects["PlayerRig"]
    head = bpy.data.objects["PlayerHead"]
    old = bpy.data.objects.get("PlayerBody")
    if old:
        bpy.data.objects.remove(old, do_unlink=True)
    body = Body()
    build(body)
    bmesh.ops.recalc_face_normals(body.bm, faces=body.bm.faces)
    mesh = bpy.data.meshes.new("Body")
    body.bm.to_mesh(mesh)
    body.bm.free()
    for m in MATS:
        mesh.materials.append(bpy.data.materials.get(m) or bpy.data.materials.new(m))
    uv = mesh.uv_layers.new(name="UVMap")
    for poly in mesh.polygons:
        for li in poly.loop_indices:
            co = mesh.vertices[mesh.loops[li].vertex_index].co
            uv.data[li].uv = (co.x * 4 + co.y * 4, co.z * 4)
    obj = bpy.data.objects.new("PlayerBody", mesh)
    bpy.context.scene.collection.objects.link(obj)
    weights(obj, arm, body)
    obj.parent = arm
    mod = obj.modifiers.new("Armature", "ARMATURE")
    mod.object = arm
    unweighted = sum(1 for v in mesh.vertices if not v.groups)
    tris = sum(len(p.vertices) - 2 for o in (head, obj) for p in o.data.polygons)
    print(f"[player_v3] body verts {len(mesh.vertices)}, unweighted {unweighted}, total tris {tris}")
    bpy.ops.wm.save_as_mainfile(filepath=OUT_BLEND)
    for o in bpy.context.view_layer.objects:
        o.select_set(o in (arm, obj, head))
    bpy.ops.export_scene.fbx(
        filepath=OUT_FBX, use_selection=True, object_types={"ARMATURE", "MESH"},
        add_leaf_bones=False, bake_anim=False, use_armature_deform_only=True,
        axis_forward="-Z", axis_up="Y", apply_scale_options="FBX_SCALE_ALL",
        bake_space_transform=True, mesh_smooth_type="FACE", path_mode="STRIP")
    print(f"[player_v3] exported {OUT_FBX}")


main()
