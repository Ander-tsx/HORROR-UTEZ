"""Shared modelling + texture kit for the remake's Blender generators.

Author geometry in UNITY coordinates (metres, x right, y up, z forward). `to_blender` maps
them with a reflection (x, z, y) that cancels the one in FBX import, so Unity gets exactly
the authored coordinates (the old gen_remake_props.py models needed a runtime 180 flip).

Every prop is ONE mesh object with several materials. Material names are the texture
names in Assets/_Project/Art/Remake/Textures; RemakeBuild turns each into a PSX/Lit
material with the house settings, so the look cannot drift between models.

UVs: faces are box-projected in world metres (`TILE[mat]` metres per texture repeat), or
fitted 0..1 on their own plane when a primitive asks for it (screens, labels, posters).
"""
import math
import os
import random
import struct
import zlib

import bmesh
import bpy
import numpy as np
from mathutils import Matrix, Vector

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
ART = os.path.join(ROOT, "Assets", "_Project", "Art", "Remake")
TEX = os.path.join(ART, "Textures")
SRC = os.path.join(ART, "Source~")

# Metres covered by one texture repeat for world-projected UVs.
TILE = {}
DEFAULT_TILE = 1.0


def to_blender(p):
    x, y, z = p
    return Vector((x, z, y))


# ---------------------------------------------------------------- mesh builder

class Mesh:
    """Accumulates primitives into one bmesh. `xf` is a Unity-space transform stack."""

    def __init__(self, name):
        self.name = name
        self.bm = bmesh.new()
        self.mats = []
        self.fit = self.bm.faces.layers.int.new("fit")
        self.stack = [Matrix.Identity(4)]
        self.keep = set()  # single-sided cards keep their authored winding

    # transform stack (Unity space)
    @property
    def xf(self):
        return self.stack[-1]

    def push(self, pos=(0, 0, 0), rot=(0, 0, 0), scale=1.0):
        m = Matrix.Translation(Vector(pos)) @ euler(rot) @ Matrix.Scale(scale, 4)
        self.stack.append(self.xf @ m)
        return self

    def pop(self):
        self.stack.pop()
        return self

    def __enter__(self):
        return self

    def __exit__(self, *a):
        self.pop()

    def mat(self, name):
        if name not in self.mats:
            self.mats.append(name)
        return self.mats.index(name)

    def _add(self, verts, faces, mat, fit=False, local=Matrix.Identity(4), smooth=False):
        m = self.xf @ local
        bv = [self.bm.verts.new(to_blender(m @ Vector(v))) for v in verts]
        made = []
        mi = self.mat(mat)
        for f in faces:
            try:
                face = self.bm.faces.new([bv[i] for i in reversed(f)])
            except ValueError:
                continue
            face.material_index = mi
            face[self.fit] = 1 if fit else 0
            face.smooth = smooth
            made.append(face)
        return bv, made

    # primitives -------------------------------------------------------------
    def box(self, c, size, mat, bevel=0.0, rot=(0, 0, 0), fit=False, taper=(1, 1)):
        """Axis box centred on c. taper scales the +y face (x, z) for wedge-like shapes."""
        sx, sy, sz = (s * 0.5 for s in size)
        tx, tz = taper
        v = [(-sx, -sy, -sz), (sx, -sy, -sz), (sx, -sy, sz), (-sx, -sy, sz),
             (-sx * tx, sy, -sz * tz), (sx * tx, sy, -sz * tz), (sx * tx, sy, sz * tz), (-sx * tx, sy, sz * tz)]
        f = [(0, 1, 2, 3), (7, 6, 5, 4), (0, 4, 5, 1), (1, 5, 6, 2), (2, 6, 7, 3), (3, 7, 4, 0)]
        local = Matrix.Translation(Vector(c)) @ euler(rot)
        bv, faces = self._add(v, f, mat, fit, local)
        if bevel > 0:
            edges = list({e for face in faces for e in face.edges})
            bmesh.ops.bevel(self.bm, geom=edges, offset=bevel, segments=1, affect="EDGES", profile=0.5)
        return faces

    def cyl(self, c, r, h, mat, axis="y", segs=12, r2=None, rot=(0, 0, 0), caps=True, smooth=True, fit=False):
        """Cylinder/cone along an axis, centred on c. r2 = radius at the +axis end."""
        r2 = r if r2 is None else r2
        verts = []
        for end, rad in ((-0.5, r), (0.5, r2)):
            for i in range(segs):
                a = 2 * math.pi * i / segs
                verts.append((math.cos(a) * rad, end * h, math.sin(a) * rad))
        faces = [(i, (i + 1) % segs, segs + (i + 1) % segs, segs + i) for i in range(segs)]
        # outward winding check: Unity-space y up, Blender conversion flips handedness once more,
        # so faces are built consistently and recalculated at the end.
        if caps:
            faces.append(tuple(reversed(range(segs))))
            faces.append(tuple(range(segs, 2 * segs)))
        axis_rot = {"y": (0, 0, 0), "x": (0, 0, 90), "z": (90, 0, 0)}[axis]
        local = Matrix.Translation(Vector(c)) @ euler(rot) @ euler(axis_rot)
        self._add(verts, faces, mat, fit, local, smooth)

    def sphere(self, c, radii, mat, segs=10, rings=6, rot=(0, 0, 0), smooth=True):
        rx, ry, rz = radii if isinstance(radii, (tuple, list)) else (radii,) * 3
        verts = [(0, ry, 0)]
        for j in range(1, rings):
            phi = math.pi * j / rings
            for i in range(segs):
                a = 2 * math.pi * i / segs
                verts.append((math.sin(phi) * math.cos(a) * rx, math.cos(phi) * ry, math.sin(phi) * math.sin(a) * rz))
        verts.append((0, -ry, 0))
        faces = []
        for i in range(segs):
            faces.append((0, 1 + (i + 1) % segs, 1 + i))
        for j in range(rings - 2):
            a0, b0 = 1 + j * segs, 1 + (j + 1) * segs
            for i in range(segs):
                faces.append((a0 + i, a0 + (i + 1) % segs, b0 + (i + 1) % segs, b0 + i))
        last = len(verts) - 1
        base = 1 + (rings - 2) * segs
        for i in range(segs):
            faces.append((base + i, base + (i + 1) % segs, last))
        local = Matrix.Translation(Vector(c)) @ euler(rot)
        self._add(verts, faces, mat, False, local, smooth)

    def loft(self, rings, mat, cap0=True, cap1=True, smooth=True, closed=True):
        """rings: list of equal-length point lists (Unity space, already placed)."""
        n = len(rings[0])
        verts = [p for ring in rings for p in ring]
        faces = []
        for j in range(len(rings) - 1):
            for i in range(n if closed else n - 1):
                a, b = j * n + i, j * n + (i + 1) % n
                faces.append((a, b, b + n, a + n))
        if cap0:
            faces.append(tuple(reversed(range(n))))
        if cap1:
            faces.append(tuple(range((len(rings) - 1) * n, len(rings) * n)))
        self._add(verts, faces, mat, False, Matrix.Identity(4), smooth)

    def tube(self, path, radii, mat, segs=6, caps=True, smooth=True):
        """Tube through Unity-space points with per-point radii (cables, pipes, limbs)."""
        if not isinstance(radii, (list, tuple)):
            radii = [radii] * len(path)
        pts = [Vector(p) for p in path]
        rings = []
        prev_n = None
        for k, p in enumerate(pts):
            t = (pts[min(k + 1, len(pts) - 1)] - pts[max(k - 1, 0)]).normalized()
            if prev_n is None:
                ref = Vector((0, 1, 0)) if abs(t.y) < 0.9 else Vector((1, 0, 0))
                prev_n = t.cross(ref).normalized()
            n = (prev_n - t * prev_n.dot(t)).normalized()
            prev_n = n
            b = t.cross(n)
            rings.append([tuple(p + (n * math.cos(2 * math.pi * i / segs) + b * math.sin(2 * math.pi * i / segs)) * radii[k])
                          for i in range(segs)])
        self.loft(rings, mat, caps, caps, smooth)

    def quad(self, c, size, mat, normal="-z", fit=True, rot=(0, 0, 0)):
        """Single-sided card (poster, screen glass, decal)."""
        w, h = size[0] * 0.5, size[1] * 0.5
        v = [(-w, -h, 0), (w, -h, 0), (w, h, 0), (-w, h, 0)]
        face_rot = {"-z": (0, 0, 0), "z": (0, 180, 0), "y": (-90, 0, 0), "-y": (90, 0, 0), "x": (0, -90, 0), "-x": (0, 90, 0)}[normal]
        local = Matrix.Translation(Vector(c)) @ euler(rot) @ euler(face_rot)
        _, faces = self._add(v, [(0, 3, 2, 1)], mat, fit, local)
        self.keep.update(faces)

    # finishing --------------------------------------------------------------
    def build(self, collection=None, origin=(0, 0, 0)):
        bm = self.bm
        bmesh.ops.remove_doubles(bm, verts=bm.verts, dist=0.0002)
        bm.faces.ensure_lookup_table()
        bmesh.ops.recalc_face_normals(bm, faces=[f for f in bm.faces if f not in self.keep])
        uv = bm.loops.layers.uv.new("UVMap")
        for face in bm.faces:
            tile = TILE.get(self.mats[face.material_index], DEFAULT_TILE)
            n = face.normal
            ax = max(range(3), key=lambda i: abs(n[i]))
            if face[self.fit]:
                # fit 0..1 on the face's own plane, worked out in UNITY space (the Blender mesh is its
                # mirror): v follows world up (Unity +Z for floor cards), u is the right of a viewer
                # looking at the face's front, i.e. Unity's Cross(up, forward) with forward = -normal.
                na = Vector((n.x, n.z, n.y))
                up = Vector((0, 1, 0))
                if abs(na.dot(up)) > 0.9:
                    up = Vector((0, 0, 1)) if na.y > 0 else Vector((0, 0, -1))
                t2 = (up - na * up.dot(na)).normalized()
                t1 = t2.cross(-na).normalized()
                pts = [(Vector((l.vert.co.x, l.vert.co.z, l.vert.co.y)).dot(t1),
                        Vector((l.vert.co.x, l.vert.co.z, l.vert.co.y)).dot(t2)) for l in face.loops]
                u0, u1 = min(p[0] for p in pts), max(p[0] for p in pts)
                v0, v1 = min(p[1] for p in pts), max(p[1] for p in pts)
                for l, (a, b) in zip(face.loops, pts):
                    l[uv].uv = ((a - u0) / max(u1 - u0, 1e-6), (b - v0) / max(v1 - v0, 1e-6))
                continue
            for l in face.loops:
                co = l.vert.co
                if ax == 0:
                    l[uv].uv = (co.y / tile * (1 if n.x > 0 else -1), co.z / tile)
                elif ax == 1:
                    l[uv].uv = (co.x / tile * (-1 if n.y > 0 else 1), co.z / tile)
                else:
                    l[uv].uv = (co.x / tile, co.y / tile)
        me = bpy.data.meshes.new(self.name)
        bm.to_mesh(me)
        bm.free()
        for m in self.mats:
            me.materials.append(material(m))
        ob = bpy.data.objects.new(self.name, me)
        (collection or bpy.context.scene.collection).objects.link(ob)
        if origin != (0, 0, 0):
            o = to_blender(origin)
            me.transform(Matrix.Translation(-o))
            ob.location = o
        return ob


def euler(rot):
    from mathutils import Euler
    rx, ry, rz = (math.radians(a) for a in rot)
    return Euler((rx, ry, rz), "YXZ").to_matrix().to_4x4()


def material(name):
    mat = bpy.data.materials.get(name)
    if mat is None:
        mat = bpy.data.materials.new(name)
        path = os.path.join(TEX, name + ".png")
        if os.path.exists(path):
            mat.use_nodes = True
            nodes = mat.node_tree.nodes
            img = bpy.data.images.load(path, check_existing=True)
            tex = nodes.new("ShaderNodeTexImage")
            tex.image = img
            tex.interpolation = "Closest"
            bsdf = nodes.get("Principled BSDF")
            if bsdf:
                mat.node_tree.links.new(tex.outputs["Color"], bsdf.inputs["Base Color"])
    return mat


def reset():
    for ob in list(bpy.data.objects):
        bpy.data.objects.remove(ob, do_unlink=True)
    for me in list(bpy.data.meshes):
        bpy.data.meshes.remove(me)


def export(name, folder=ART, objects=None):
    """Save the editable source and export an FBX that Unity imports in authored coordinates."""
    os.makedirs(SRC, exist_ok=True)
    os.makedirs(folder, exist_ok=True)
    bpy.ops.wm.save_as_mainfile(filepath=os.path.join(SRC, name + ".blend"))
    bpy.ops.object.select_all(action="DESELECT")
    for ob in (objects or bpy.context.scene.objects):
        ob.select_set(True)
    bpy.ops.export_scene.fbx(filepath=os.path.join(folder, name + ".fbx"), use_selection=True,
                             object_types={"MESH", "ARMATURE", "EMPTY"}, add_leaf_bones=False, bake_anim=False,
                             axis_forward="-Z", axis_up="Y", apply_scale_options="FBX_SCALE_ALL",
                             bake_space_transform=True, mesh_smooth_type="FACE", path_mode="STRIP")
    print("[remake_asset]", name)


# ---------------------------------------------------------------- textures

def write_png(path, img):
    """img: HxWx3 or HxWx4 float 0..1 array, row 0 = TOP of the image."""
    img = np.clip(img, 0, 1)
    if img.shape[2] == 3:
        img = np.concatenate([img, np.ones(img.shape[:2] + (1,))], axis=2)
    data = (img * 255 + 0.5).astype(np.uint8)
    h, w = data.shape[:2]
    raw = b"".join(b"\x00" + data[y].tobytes() for y in range(h))

    def chunk(tag, payload):
        c = struct.pack(">I", len(payload)) + tag + payload
        return c + struct.pack(">I", zlib.crc32(tag + payload) & 0xFFFFFFFF)

    png = b"\x89PNG\r\n\x1a\n" + chunk(b"IHDR", struct.pack(">IIBBBBB", w, h, 8, 6, 0, 0, 0))
    png += chunk(b"IDAT", zlib.compress(raw, 9)) + chunk(b"IEND", b"")
    os.makedirs(os.path.dirname(path), exist_ok=True)
    with open(path, "wb") as f:
        f.write(png)


def value_noise(size, cells, seed):
    """Tileable smooth value noise in 0..1."""
    rng = np.random.default_rng(seed)
    g = rng.random((cells, cells))
    y, x = np.mgrid[0:size, 0:size] / size * cells
    x0, y0 = np.floor(x).astype(int), np.floor(y).astype(int)
    fx, fy = x - x0, y - y0
    fx, fy = fx * fx * (3 - 2 * fx), fy * fy * (3 - 2 * fy)
    x1, y1 = (x0 + 1) % cells, (y0 + 1) % cells
    x0, y0 = x0 % cells, y0 % cells
    a = g[y0, x0] * (1 - fx) + g[y0, x1] * fx
    b = g[y1, x0] * (1 - fx) + g[y1, x1] * fx
    return a * (1 - fy) + b * fy


def fbm(size, seed, octaves=4, base=4):
    out = np.zeros((size, size))
    amp, total = 1.0, 0.0
    for o in range(octaves):
        out += value_noise(size, base * 2 ** o, seed + o) * amp
        total += amp
        amp *= 0.5
    return out / total


def tint(mask, color):
    return mask[..., None] * np.array(color)[None, None, :]


def colorize(n, dark, light):
    d, l = np.array(dark), np.array(light)
    return d[None, None, :] + n[..., None] * (l - d)[None, None, :]


# 5x7 bitmap font for lettering on boards, posters and screens.
FONT = {
    "A": "01110100011000111111100011000110001", "B": "11110100011000111110100011000111110",
    "C": "01110100011000010000100001000101110", "D": "11110100011000110001100011000111110",
    "E": "11111100001000011110100001000011111", "F": "11111100001000011110100001000010000",
    "G": "01110100011000010111100011000101111", "H": "10001100011000111111100011000110001",
    "I": "01110001000010000100001000010001110", "J": "00111000100001000010000101001001100",
    "K": "10001100101010011000101001001010001", "L": "10000100001000010000100001000011111",
    "M": "10001110111010110101100011000110001", "N": "10001100011100110101100111000110001",
    "O": "01110100011000110001100011000101110", "P": "11110100011000111110100001000010000",
    "Q": "01110100011000110001101011001001101", "R": "11110100011000111110101001001010001",
    "S": "01111100001000001110000010000111110", "T": "11111001000010000100001000010000100",
    "U": "10001100011000110001100011000101110", "V": "10001100011000110001100010101000100",
    "W": "10001100011000110101101011010101010", "X": "10001100010101000100010101000110001",
    "Y": "10001100010101000100001000010000100", "Z": "11111000010001000100010001000011111",
    "0": "01110100011001110101110011000101110", "1": "00100011000010000100001000010001110",
    "2": "01110100010000100010001000100011111", "3": "11111000100010000010000011000101110",
    "4": "00010001100101010010111110001000010", "5": "11111100001111000001000011000101110",
    "6": "00110010001000011110100011000101110", "7": "11111000010001000100010000100001000",
    "8": "01110100011000101110100011000101110", "9": "01110100011000101111000010001001100",
    " ": "00000000000000000000000000000000000", ".": "00000000000000000000000000110001100",
    ":": "00000011000110000000011000110000000", "-": "00000000000000011111000000000000000",
    "!": "00100001000010000100001000000000100", "?": "01110100010000100010001000000000100",
    "/": "00001000010001000100010001000010000", "$": "00100011111010001110001011111000100",
    "%": "11000110010001000100010001001100011", ">": "10000010000010000010001000100010000",
    "_": "00000000000000000000000000000011111", "(": "00010001000100001000010000010000010",
    ")": "01000001000001000010000100010001000", ",": "00000000000000000000001100010001000", "#": "01010010101111101010111110101001010",
}


def draw_text(img, text, x, y, color, scale=1, jitter=0.0, seed=0):
    """Stamp text into img (row 0 = top). Returns the x after the text."""
    rng = random.Random(seed)
    h, w = img.shape[:2]
    for ch in text.upper():
        glyph = FONT.get(ch, FONT[" "])
        dy = int(round(rng.uniform(-jitter, jitter))) if jitter else 0
        for gy in range(7):
            for gx in range(5):
                if glyph[gy * 5 + gx] == "1":
                    for sy in range(scale):
                        for sx in range(scale):
                            px, py = x + gx * scale + sx, y + (gy + dy) * scale + sy
                            if 0 <= px < w and 0 <= py < h:
                                img[py, px, :3] = color[:3]
        x += 6 * scale
    return x
