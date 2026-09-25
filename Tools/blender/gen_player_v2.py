"""
Player character v2: one skinned low-poly body with a photo-textured head, rigged as a
Unity Humanoid, exported to FBX.

    BLENDER=/Applications/Blender.app/Contents/MacOS/Blender
    "$BLENDER" -b --factory-startup -P Tools/blender/gen_player_v2.py -- [--preview <dir>]

Reads   Tools/character/source/v2_{4,0,3}.jpg          (front, his right, his left)
Writes  Assets/_Project/Art/Characters/Player/PlayerCharacter.fbx
        Assets/_Project/Art/Characters/Player/Source~/PlayerCharacter.blend   (Unity ignores ~)
        Assets/_Project/Art/Textures/Player/T_Player_Head.png  + flat body maps

HOW THE FACE IS MADE (multi-view projection, a small photogrammetry):
  1. The head mesh is built from cross-sections tuned to his measurements (front photo,
     21.7 px/cm from a 6.3 cm interpupillary distance) and unwrapped CYLINDRICALLY:
     u = angle around the vertical axis (front = 0.5), v = height. So the face wraps the
     head evenly instead of stretching sideways like a planar projection.
  2. Each photo gets a camera: its rotation, position and focal length are solved by
     Levenberg-Marquardt so that 3-D landmarks on the mesh (pupils, nose tip, mouth
     corners, chin, ears, crown) land on the pixels measured on that photo.
  3. Every texel of the head map is traced back to its 3-D point and normal, projected
     into each photo, occlusion-tested against the head (the nose hides a cheek), and
     blended by how squarely that photo sees it (cos^5). Side photos are gain-matched to
     the front one first, so the blend has no colour seams.
  4. The map is area-downsampled to 256 x 128 and crushed to 5 bits: a real face, but
     pixelated, as in the reference. The glasses stay painted, from the photos.

Frame: Blender world, character facing -Y, his right = -X, Z up, metres, feet on z = 0.
Head-local measurements below are CENTIMETRES in a "head frame": x = his right,
y = forward, z = up, origin = midpoint of the pupils projected to the ear line.
"""

import math
import os
import sys

import bmesh
import bpy
import numpy as np
from mathutils import Matrix, Vector
from mathutils.bvhtree import BVHTree

_g = globals()
HERE = _g.get("TOOLS_DIR") or os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.dirname(os.path.dirname(HERE))
SOURCE = os.path.join(ROOT, "Tools", "character", "source")
CHAR_DIR = os.path.join(ROOT, "Assets", "_Project", "Art", "Characters", "Player")
TEX_DIR = os.path.join(ROOT, "Assets", "_Project", "Art", "Textures", "Player")
OUT_FBX = os.path.join(CHAR_DIR, "PlayerCharacter.fbx")
OUT_BLEND = os.path.join(CHAR_DIR, "Source~", "PlayerCharacter.blend")

ARGS = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
PREVIEW_DIR = ARGS[ARGS.index("--preview") + 1] if "--preview" in ARGS else None

# ---- Proportions ---------------------------------------------------------------------

HEAD_SCALE = 1.04          # a touch over life size; the reference reads natural
Z_EYE = 1.575              # world height of the pupils (the camera sits here in game)
HEAD_Y0 = 0.012            # world y of the ear line (the face is toward -Y)


def hw(x, y, z):
    """Head-frame centimetres -> world metres."""
    k = HEAD_SCALE / 100.0
    return Vector((-x * k, HEAD_Y0 - y * k, Z_EYE + z * k))


def wh(p):
    """World metres -> head-frame centimetres."""
    k = 100.0 / HEAD_SCALE
    return np.array([-p[0] * k, (HEAD_Y0 - p[1]) * k, (p[2] - Z_EYE) * k])


def cw(x, y, z):
    """Character frame (x = his right, y = forward, z up), metres -> world."""
    return Vector((-x, -y, z))


# ---- Head shape (cm) -----------------------------------------------------------------
# (z, half width, front depth, back depth). His face from the front photo: pupils 6.3 cm
# apart, nose tip 3.6 cm under them, mouth 6.5 cm, chin 10.4 cm; ears 16 cm across the
# lobes, jaw 10.8 cm wide at the mouth, hair top 10.9 cm over the eyes.
HEAD_SECTIONS = [
    (10.3, 2.6, 2.6, 3.4),
    (9.6, 4.4, 4.4, 5.8),
    (8.0, 6.3, 7.0, 8.4),
    (5.5, 7.2, 8.6, 9.5),
    (3.5, 7.4, 9.0, 9.7),
    (2.0, 7.45, 9.2, 9.7),
    (0.5, 7.35, 8.9, 9.6),
    (-1.0, 7.25, 8.85, 9.4),
    (-2.5, 7.1, 8.95, 8.9),
    (-3.7, 6.9, 9.0, 8.2),
    (-5.0, 6.5, 8.95, 7.0),
    (-6.3, 6.0, 8.8, 5.6),
    (-7.5, 5.45, 8.5, 4.4),
    (-8.6, 4.7, 8.1, 3.2),
    (-9.6, 3.6, 7.6, 1.9),
]
HEAD_TOP = (10.8, -0.6)       # crown pole: z, y

# Fitted to his photos by fit_shape(): scales on the sections above, and extra nose.
SHAPE = {"sx": 1.0, "sy": 1.0, "sz": 1.0, "nose": 0.0}
HEAD_BOTTOM = (-10.6, 4.4)    # under-chin pole
HEAD_SEGS = 20                # column 0 is the front centre line


def ring_point(k, z, w, df, db, inflate=0.0, features=True):
    a = math.pi / 2 + 2 * math.pi * k / HEAD_SEGS         # k = 0 -> straight ahead
    c, s = math.cos(a), math.sin(a)
    front = s > 0
    p = 3.0 if front else 2.2
    x = (w + inflate) * math.copysign(abs(c) ** (2 / p), c)
    y = ((df if front else db) + inflate) * math.copysign(abs(s) ** (2 / p), s)
    if inflate > 0 and z > 5.0:
        # Near the crown the surface faces up: inflate it upward too, or the top rings
        # stay put and the pole alone rises into a cone.
        z += inflate * min(1.0, (z - 5.0) / 5.0)
    if features:
        kk = min(k, HEAD_SEGS - k)
        if kk == 0:
            y += 2.1 * math.exp(-((z + 3.6) / 1.6) ** 2) + 0.35 * math.exp(-(z / 1.0) ** 2)
            if z < -9.0:
                y += 0.5                                  # chin
        if kk == 1:
            y += 0.7 * math.exp(-((z + 3.4) / 1.2) ** 2)  # nose wings
            if abs(z - 0.5) < 0.8:
                y -= 0.5                                  # eye socket, inner
        if kk == 2 and abs(z - 0.5) < 0.8:
            y -= 0.45                                     # eye socket, outer
        if kk == 3 and abs(z + 2.5) < 0.7:
            x += math.copysign(0.3, x)                    # cheekbones
        if kk == 0:
            y += SHAPE["nose"] * math.exp(-((z + 3.6) / 1.6) ** 2)
    return (x * SHAPE["sx"], y * SHAPE["sy"], z * SHAPE["sz"])


def head_rings(inflate_fn=None, features=True, min_z=-99):
    rings = []
    for z, w, df, db in HEAD_SECTIONS:
        if z < min_z:
            continue
        rings.append([ring_point(k, z, w, df, db,
                                 inflate_fn(k, z) if inflate_fn else 0.0, features)
                      for k in range(HEAD_SEGS)])
    return rings


def cyl_uv(p_cm):
    """Cylindrical head UV: u = angle (front 0.5, his right toward u > 0.5), v = height."""
    x, y, z = p_cm
    u = 0.5 + math.atan2(x, y) / (2 * math.pi)
    v = (z + 12.0) / 26.0
    return u, v


def fix_uvs(pts, uvs):
    """Per-face cylindrical UV repair: across the back seam, shift the low side by +1;
    at a pole (on the axis, where the angle means nothing) use the face's other angles."""
    uvs = [list(uv) for uv in uvs]
    on_axis = [math.hypot(p[0], p[1]) < 1.0 for p in pts]
    others = [uv[0] for uv, pole in zip(uvs, on_axis) if not pole]
    us = others or [uv[0] for uv in uvs]
    if max(us) - min(us) > 0.5:
        for uv, pole in zip(uvs, on_axis):
            if not pole and uv[0] < 0.5:
                uv[0] += 1.0
        others = [uv[0] for uv, pole in zip(uvs, on_axis) if not pole]
    for uv, pole in zip(uvs, on_axis):
        if pole and others:
            uv[0] = sum(others) / len(others)
    return [tuple(uv) for uv in uvs]


def build_mesh(name, verts_cm, faces, uv_fn=cyl_uv):
    """Mesh in world space from head-frame cm verts; cylindrical UVs with the back seam
    fixed per face (a face crossing it gets its low u values shifted by +1)."""
    mesh = bpy.data.meshes.new(name)
    mesh.from_pydata([tuple(hw(*v)) for v in verts_cm], [], faces)
    uv_layer = mesh.uv_layers.new(name="UVMap")
    for poly in mesh.polygons:
        pts = [verts_cm[mesh.loops[li].vertex_index] for li in poly.loop_indices]
        uvs = fix_uvs(pts, [uv_fn(p) for p in pts])
        for li, uv in zip(poly.loop_indices, uvs):
            uv_layer.data[li].uv = uv
    mesh.validate()
    # Consistent outward normals per connected piece (skull, hair shell, each ear);
    # flipping a face keeps its loop UVs.
    bm = bmesh.new()
    bm.from_mesh(mesh)
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    bm.to_mesh(mesh)
    bm.free()
    return mesh


def grid_faces(n_rings, segs, top_index=None, bottom_index=None, keep=None):
    faces = []
    for r in range(n_rings - 1):
        for k in range(segs):
            j = (k + 1) % segs
            if keep and not keep(r, k):
                continue
            a, b = r * segs + k, r * segs + j
            c, d = (r + 1) * segs + j, (r + 1) * segs + k
            faces.append((d, c, b, a))
    if top_index is not None:
        for k in range(segs):
            faces.append((top_index, k, (k + 1) % segs))
    if bottom_index is not None:
        base = (n_rings - 1) * segs
        for k in range(segs):
            faces.append((bottom_index, base + (k + 1) % segs, base + k))
    return faces


def build_head_parts():
    """Skull + face, hair shell and ears, all in head-frame cm. Returns [(name, verts, faces)]."""
    parts = []

    rings = head_rings()
    verts = [p for ring in rings for p in ring]
    top = len(verts)
    verts.append((0.0, HEAD_TOP[1] * SHAPE["sy"], HEAD_TOP[0] * SHAPE["sz"]))
    bottom = len(verts)
    verts.append((0.0, HEAD_BOTTOM[1] * SHAPE["sy"], HEAD_BOTTOM[0] * SHAPE["sz"]))
    parts.append(("Skull", verts, grid_faces(len(rings), HEAD_SEGS, top, bottom)))

    # Hair: short on the sides, volume on top, a fringe swept to his left. Inflation in cm
    # over the skull, cut where his hairline is: fringe 4 cm over the eyes, sideburns down
    # to the eyes in front of the ears, over the ears, down to the nape.
    def cut(k):
        theta = math.degrees(min(2 * math.pi * k / HEAD_SEGS, 2 * math.pi - 2 * math.pi * k / HEAD_SEGS))
        if theta < 40:
            return 5.6                                           # hairline 6.4 cm up; fringe a bit lower
        if theta < 72:
            return 5.6 + (0.3 - 5.6) * (theta - 40) / 32            # temple down to sideburn
        if theta < 100:
            return 2.2                                           # over the ear
        return 2.2 + (-6.3 - 2.2) * (theta - 100) / 80           # behind the ear to the nape

    def inflate(k, z):
        a = 2 * math.pi * k / HEAD_SEGS                       # 0 front, pi back
        front = math.cos(a)
        left = -math.sin(a)                                   # his left is x < 0
        top_volume = max(0.0, (z - 3.0) / 7.0) * 1.3
        fringe = 0.6 * max(0.0, front) * max(0.0, (z - 4.0) / 4.0) * (1.0 + 0.6 * max(0.0, left))
        full = 0.55 + top_volume + fringe + 0.5 * max(0.0, -front)
        # Thin at the cut line, full a couple of cm above it: hair lies down at its edge
        # instead of ending in a cap brim.
        t = min(1.0, max(0.0, (z - min(cut(k), cut((k + 1) % HEAD_SEGS))) / 2.5))
        return 0.15 + (full - 0.15) * t

    hair_rings = head_rings(inflate, features=False, min_z=-6.3)
    hverts = [p for ring in hair_rings for p in ring]
    htop = len(hverts)
    hverts.append((0.0, (HEAD_TOP[1] - 0.3) * SHAPE["sy"], (HEAD_TOP[0] + inflate(0, 10.3)) * SHAPE["sz"]))

    def keep(r, k):
        z_lo = HEAD_SECTIONS[r + 1][0]
        return z_lo >= min(cut(k), cut((k + 1) % HEAD_SEGS)) - 0.01

    parts.append(("Hair", hverts, grid_faces(len(hair_rings), HEAD_SEGS, htop, None, keep)))

    # Ears: small wedges, top at +1.9 cm, lobe at -3.9 cm, standing off the skull.
    for side in (1, -1):
        base_x = side * 7.0
        tip_x = side * 8.2
        ev = [(base_x, 1.1, 1.9), (base_x, -1.9, 1.6), (base_x, 0.6, -3.9), (base_x, -1.2, -3.4),
              (tip_x, 0.7, 1.7), (tip_x, -2.1, 1.3), (tip_x, 0.3, -3.8), (tip_x, -1.3, -3.2)]
        ef = [(0, 1, 5, 4), (2, 6, 7, 3), (0, 4, 6, 2), (1, 3, 7, 5), (4, 5, 7, 6)]
        if side < 0:
            ef = [tuple(reversed(f)) for f in ef]
        parts.append(("Ear_R" if side > 0 else "Ear_L", ev, ef))

    return parts


# Landmarks on the mesh (head cm): where each measured photo point is on the model.
def mesh_landmarks(bvh_cm):
    def hit(origin, direction):
        loc, _, _, _ = bvh_cm.ray_cast(Vector(origin), Vector(direction))
        return np.array(loc) if loc is not None else np.array(origin)

    sx, sy, sz = SHAPE["sx"], SHAPE["sy"], SHAPE["sz"]
    eye = lambda x: hit((x * sx, 30.0, 0.0), (0, -1, 0))
    mouth = lambda x: hit((x * sx, 30.0, -6.5 * sz), (0, -1, 0))
    fixed = lambda x, y, z: np.array([x * sx, y * sy, z * sz])
    return {
        "eyeR": eye(3.15), "eyeL": eye(-3.15),
        "nose": hit((0.0, 30.0, -3.6 * sz), (0, -1, 0)),
        "mouthR": mouth(2.65), "mouthL": mouth(-2.65),
        "chin": hit((0.0, 30.0, -9.9 * sz), (0, -1, 0)),
        "earTopR": fixed(7.9, -0.3, 1.8), "earTopL": fixed(-7.9, -0.3, 1.8),
        "lobeR": fixed(7.6, 0.4, -3.7), "lobeL": fixed(-7.6, 0.4, -3.7),
        "tragusR": fixed(7.1, 1.3, -1.2), "tragusL": fixed(-7.1, 1.3, -1.2),
        "headTop": hit((0.0, 0.3, 40.0), (0, 0, -1)),
    }


# Pixel coordinates read off gridded crops of each photo.
PHOTOS = {
    "front": ("v2_4.jpg", 0.0, 1.0, {
        "eyeR": (505, 690), "eyeL": (642, 689), "nose": (575, 768.5),
        "mouthR": (517.5, 830), "mouthL": (632.5, 830), "chin": (580, 915),
        "earTopR": (395, 658), "earTopL": (735, 656), "lobeR": (400, 764), "lobeL": (730, 761),
        "headTop": (575, 452)}),
    "right": ("v2_0.jpg", 40.0, 0.75, {
        "eyeR": (575, 756), "eyeL": (706, 746), "nose": (675, 824),
        "mouthR": (606, 887), "mouthL": (675, 877.5), "chin": (650, 971),
        "earTopR": (347, 734), "lobeR": (409, 837), "tragusR": (400, 790), "headTop": (562, 490)}),
    "left": ("v2_3.jpg", -40.0, 0.75, {
        "eyeL": (616, 785), "eyeR": (472, 776), "nose": (519, 847.5),
        "mouthL": (579, 925.6), "mouthR": (500.6, 907), "chin": (547.5, 1010),
        "earTopL": (819, 769), "lobeL": (785, 879), "tragusL": (772.5, 829), "headTop": (672, 516)}),
}


# ---- Camera solve --------------------------------------------------------------------

def rodrigues(w):
    th = np.linalg.norm(w)
    if th < 1e-12:
        return np.eye(3)
    k = w / th
    K = np.array([[0, -k[2], k[1]], [k[2], 0, -k[0]], [-k[1], k[0], 0]])
    return np.eye(3) + math.sin(th) * K + (1 - math.cos(th)) * K @ K


def look_from(c):
    zc = -c / np.linalg.norm(c)
    xc = np.cross(zc, [0, 0, 1.0]); xc /= np.linalg.norm(xc)
    yc = np.cross(zc, xc)
    return np.array([xc, yc, zc])


class Camera:
    def __init__(self, R, t, f, cx, cy):
        self.R, self.t, self.f, self.cx, self.cy = R, t, f, cx, cy

    def project(self, P):
        Pc = P @ self.R.T + self.t
        z = Pc[..., 2]
        return np.stack([self.f * Pc[..., 0] / z + self.cx, self.f * Pc[..., 1] / z + self.cy], -1), z

    @property
    def centre(self):
        return -self.R.T @ self.t


def solve_camera(points3d, points2d, yaw_deg, size):
    cx, cy = size[0] / 2.0, size[1] / 2.0
    yaw = math.radians(yaw_deg)
    c0 = np.array([math.sin(yaw), math.cos(yaw), 0.0]) * 45.0        # cm; his right = +x
    R0 = look_from(c0)
    params = np.concatenate([np.zeros(3), -R0 @ c0, [math.log(1150.0)]])

    def residual(p):
        cam = Camera(rodrigues(p[:3]) @ R0, p[3:6], math.exp(p[6]), cx, cy)
        uv, _ = cam.project(points3d)
        return (uv - points2d).ravel()

    lam = 1e-2
    r = residual(params)
    for _ in range(200):
        J = np.zeros((r.size, params.size))
        for i in range(params.size):
            d = np.zeros_like(params); d[i] = 1e-5
            J[:, i] = (residual(params + d) - r) / 1e-5
        A = J.T @ J
        g = J.T @ r
        step = np.linalg.solve(A + lam * np.diag(np.diag(A) + 1e-9), -g)
        r_new = residual(params + step)
        if r_new @ r_new < r @ r:
            params, r, lam = params + step, r_new, lam * 0.4
            if np.linalg.norm(step) < 1e-7:
                break
        else:
            lam *= 5.0
    cam = Camera(rodrigues(params[:3]) @ R0, params[3:6], math.exp(params[6]), cx, cy)
    rms = math.sqrt((r @ r) / (r.size / 2))
    return cam, rms



def fit_shape(marks, views):
    """Joint Levenberg-Marquardt over every photo's camera AND four head-shape numbers
    (width, depth, height scale, extra nose), so one head explains all three photos.
    A soft prior keeps the shape within a few percent of the average head unless the
    photos really disagree with it."""
    cams0 = []
    for names, pts2d, yaw, size in views:
        cam, _ = solve_camera(np.array([marks[n] for n in names]), pts2d, yaw, size)
        w = np.zeros(3)
        cams0.append((cam, size))

    def unpack(p):
        shape = p[:4]
        cams = []
        for i, (cam, size) in enumerate(cams0):
            q = p[4 + 7 * i: 11 + 7 * i]
            cams.append(Camera(rodrigues(q[:3]) @ cam.R, q[3:6], math.exp(q[6]), size[0] / 2, size[1] / 2))
        return shape, cams

    def model(name, shape):
        pt = marks[name] * np.array([shape[0], shape[1], shape[2]])
        if name == "nose":
            pt[1] += shape[3]
        return pt

    def residual(p):
        shape, cams = unpack(p)
        out = []
        for (names, pts2d, _, _), cam in zip(views, cams):
            P = np.array([model(n, shape) for n in names])
            uv, _ = cam.project(P)
            out.append((uv - pts2d).ravel())
        prior = np.array([(shape[0] - 1) * 150, (shape[1] - 1) * 150, (shape[2] - 1) * 150, shape[3] * 8])
        out.append(prior)
        return np.concatenate(out)

    p = [1.0, 1.0, 1.0, 0.0]
    for cam, _ in cams0:
        p += [0, 0, 0, *cam.t, math.log(cam.f)]
    p = np.array(p, float)
    r = residual(p)
    lam = 1e-2
    for _ in range(300):
        J = np.zeros((r.size, p.size))
        for i in range(p.size):
            d = np.zeros_like(p); d[i] = 1e-5
            J[:, i] = (residual(p + d) - r) / 1e-5
        A = J.T @ J
        step = np.linalg.solve(A + lam * np.diag(np.diag(A) + 1e-9), -(J.T @ r))
        r_new = residual(p + step)
        if r_new @ r_new < r @ r:
            p, r, lam = p + step, r_new, lam * 0.4
            if np.linalg.norm(step) < 1e-7:
                break
        else:
            lam *= 5.0
    shape, _ = unpack(p)
    n_pts = sum(len(v[0]) for v in views)
    rms = math.sqrt(np.sum(r[:-4] ** 2) / n_pts)
    return {"sx": float(np.clip(shape[0], 0.85, 1.2)), "sy": float(np.clip(shape[1], 0.85, 1.2)),
            "sz": float(np.clip(shape[2], 0.85, 1.2)), "nose": float(np.clip(shape[3], -1.0, 1.5))}, rms


# ---- Photo projection bake -----------------------------------------------------------

BAKE_W, BAKE_H = 512, 256
OUT_W, OUT_H = 256, 128


def load_image(path):
    img = bpy.data.images.load(path, check_existing=False)
    w, h = img.size
    px = np.empty(w * h * 4, np.float32)
    img.pixels.foreach_get(px)
    bpy.data.images.remove(img)
    return px.reshape(h, w, 4)[::-1, :, :3].copy(), (w, h)      # row 0 = top


def sample(img, uv):
    h, w, _ = img.shape
    x = np.clip(uv[:, 0], 0, w - 1.001); y = np.clip(uv[:, 1], 0, h - 1.001)
    x0, y0 = np.floor(x).astype(int), np.floor(y).astype(int)
    fx, fy = (x - x0)[:, None], (y - y0)[:, None]
    return (img[y0, x0] * (1 - fx) * (1 - fy) + img[y0, x0 + 1] * fx * (1 - fy) +
            img[y0 + 1, x0] * (1 - fx) * fy + img[y0 + 1, x0 + 1] * fx * fy)


def rasterize(parts):
    """Texel -> (3-D point, normal) in head cm, over all parts' cylindrical UVs. Where
    parts overlap in UV (hair over scalp), the point farthest from the axis wins."""
    P = np.zeros((BAKE_H, BAKE_W, 3)); N = np.zeros((BAKE_H, BAKE_W, 3))
    R = np.full((BAKE_H, BAKE_W), -1.0)
    for _, verts, faces in parts:
        V = np.array(verts, dtype=float)
        vn = np.zeros_like(V)
        tris = []
        for f in faces:
            for i in range(1, len(f) - 1):
                tris.append((f[0], f[i], f[i + 1]))
        for a, b, c in tris:
            n = np.cross(V[b] - V[a], V[c] - V[a])
            vn[[a, b, c]] += n
        vn /= np.maximum(np.linalg.norm(vn, axis=1, keepdims=True), 1e-9)
        for a, b, c in tris:
            uvs = np.array(fix_uvs([V[i] for i in (a, b, c)], [cyl_uv(V[i]) for i in (a, b, c)]))
            for shift in (0.0, -1.0):
                tu = (uvs[:, 0] + shift) * BAKE_W
                tv = (1.0 - uvs[:, 1]) * BAKE_H
                x0, x1 = int(max(0, math.floor(tu.min()))), int(min(BAKE_W - 1, math.ceil(tu.max())))
                y0, y1 = int(max(0, math.floor(tv.min()))), int(min(BAKE_H - 1, math.ceil(tv.max())))
                if x0 > x1 or y0 > y1:
                    continue
                xs, ys = np.meshgrid(np.arange(x0, x1 + 1) + 0.5, np.arange(y0, y1 + 1) + 0.5)
                d = (tu[1] - tu[0]) * (tv[2] - tv[0]) - (tu[2] - tu[0]) * (tv[1] - tv[0])
                if abs(d) < 1e-12:
                    continue
                w1 = ((xs - tu[0]) * (tv[2] - tv[0]) - (tu[2] - tu[0]) * (ys - tv[0])) / d
                w2 = ((tu[1] - tu[0]) * (ys - tv[0]) - (xs - tu[0]) * (tv[1] - tv[0])) / d
                w0 = 1 - w1 - w2
                inside = (w0 >= -1e-3) & (w1 >= -1e-3) & (w2 >= -1e-3)
                if not inside.any():
                    continue
                pts = w0[..., None] * V[a] + w1[..., None] * V[b] + w2[..., None] * V[c]
                nrm = w0[..., None] * vn[a] + w1[..., None] * vn[b] + w2[..., None] * vn[c]
                rad = np.hypot(pts[..., 0], pts[..., 1])
                yy, xx = np.nonzero(inside)
                ty, tx = yy + y0, xx + x0
                better = rad[yy, xx] > R[ty, tx]
                ty, tx, yy, xx = ty[better], tx[better], yy[better], xx[better]
                P[ty, tx] = pts[yy, xx]; N[ty, tx] = nrm[yy, xx]; R[ty, tx] = rad[yy, xx]
    covered = R >= 0
    N /= np.maximum(np.linalg.norm(N, axis=2, keepdims=True), 1e-9)
    return P, N, covered


def bake_head(parts, bvh_cm, cams, images):
    P, N, covered = rasterize(parts)
    ys, xs = np.nonzero(covered)
    pts, nrm = P[ys, xs], N[ys, xs]

    colours, weights = {}, {}
    for key, cam in cams.items():
        img, bias = images[key]
        uv, depth = cam.project(pts)
        to_cam = cam.centre[None, :] - pts
        dist = np.linalg.norm(to_cam, axis=1)
        to_cam /= dist[:, None]
        facing = np.clip(np.sum(nrm * to_cam, axis=1), 0, 1)
        h, w, _ = img.shape
        ok = (depth > 0) & (facing > 0.05) & (uv[:, 0] >= 0) & (uv[:, 0] < w - 1) & (uv[:, 1] >= 0) & (uv[:, 1] < h - 1)
        # Occlusion: anything of the head between the texel and the lens.
        for i in np.nonzero(ok)[0]:
            o = Vector(pts[i] + to_cam[i] * 1.0)
            hit = bvh_cm.ray_cast(o, Vector(to_cam[i]), dist[i])
            # Only real occluders (the nose over a cheek), not the rim of the socket the
            # texel sits in: start 1 cm out along the ray.
            if hit[0] is not None:
                ok[i] = False
        wgt = np.where(ok, facing ** 5 * bias, 0.0)
        if key != "front":
            # The glasses float in front of the face: every photo paints them at a
            # different spot. Around the eyes, trust the front photo.
            eyes = (np.abs(pts[:, 0]) < 7.0) & (pts[:, 2] > -3.0) & (pts[:, 2] < 2.6) & (pts[:, 1] > 4.0)
            wgt[eyes] *= 0.15
        colours[key] = sample(img, uv)
        weights[key] = wgt

    # Match each side photo's exposure to the front one where both see the skin well.
    f = weights["front"]
    for key in cams:
        if key == "front":
            continue
        both = (f > 0.15) & (weights[key] > 0.15)
        if both.sum() > 50:
            gain = colours["front"][both].mean(0) / np.maximum(colours[key][both].mean(0), 1e-4)
            colours[key] = colours[key] * np.clip(gain, 0.6, 1.6)

    total = sum(weights.values())
    acc = sum(colours[k] * weights[k][:, None] for k in cams)
    rgb = np.zeros((BAKE_H, BAKE_W, 3))
    known = np.zeros((BAKE_H, BAKE_W), bool)
    good = total > 0.01
    rgb[ys[good], xs[good]] = acc[good] / total[good][:, None]
    known[ys[good], xs[good]] = True

    # Fill what no photo saw (the back of the head, under the chin): hair above the
    # ears, skin below, from the photos' own medians so it matches.
    hair_region = pts[:, 2] > 3.0
    face_region = (np.abs(pts[:, 0]) < 4.5) & (pts[:, 2] < -4.5) & (pts[:, 2] > -8.0) & (pts[:, 1] > 5)
    kn = known[ys, xs]
    sel = hair_region & kn & (pts[:, 1] < 3)
    hair_rgb = np.median(rgb[ys, xs][sel], axis=0) if sel.sum() > 20 else np.array([0.08, 0.07, 0.07])
    sel = face_region & kn
    skin_rgb = np.median(rgb[ys, xs][sel], axis=0) if sel.sum() > 20 else np.array([0.72, 0.5, 0.38])
    fill = np.where((P[..., 2] > 2.0)[..., None], hair_rgb, skin_rgb)
    rgb = np.where(known[..., None], rgb, fill)

    # Dilate the baked colour into every texel no triangle covers, so mip levels and
    # filtering never pull the fill colour across a seam (the crown fan, the back seam).
    cov = covered.copy()
    for _ in range(12):
        s_ = np.zeros_like(rgb); n_ = np.zeros(cov.shape)
        for dy, dx in ((1, 0), (-1, 0), (0, 1), (0, -1)):
            m = np.roll(cov, (dy, dx), (0, 1))
            s_ += np.roll(rgb, (dy, dx), (0, 1)) * m[..., None]
            n_ += m
        grow = ~cov & (n_ > 0)
        rgb[grow] = s_[grow] / n_[grow][:, None]
        cov |= grow

    # Soften the known/unknown boundary so no hard edge shows at the back.
    blur = rgb.copy()
    for _ in range(3):
        blur = (np.roll(blur, 1, 0) + np.roll(blur, -1, 0) + np.roll(blur, 1, 1) + np.roll(blur, -1, 1) + blur) / 5
    edge = known & ~(np.roll(known, 2, 0) & np.roll(known, -2, 0) & np.roll(known, 2, 1) & np.roll(known, -2, 1))
    rgb = np.where(edge[..., None], blur, rgb)

    # Pixelate: 2 x 2 area average down to 256 x 128, then 5 bits per channel.
    small = rgb.reshape(OUT_H, 2, OUT_W, 2, 3).mean(axis=(1, 3))
    small = np.clip(small, 0, 1)
    small = np.round(small * 31) / 31
    return small, skin_rgb, hair_rgb


def save_png(path, rgb):
    h, w = rgb.shape[:2]
    img = bpy.data.images.new(os.path.basename(path), w, h, alpha=False)
    rgba = np.concatenate([np.clip(rgb[::-1], 0, 1), np.ones((h, w, 1))], axis=2)
    img.pixels.foreach_set(rgba.astype(np.float32).ravel())
    img.filepath_raw = path
    img.file_format = "PNG"
    img.save()
    return img


def flat_map(rgb, size=16, jitter=0.04, seed=1):
    rng = np.random.default_rng(seed)
    block = rng.uniform(-jitter, jitter, (size // 2, size // 2, 1)).repeat(2, 0).repeat(2, 1)
    return np.clip(np.array(rgb)[None, None, :] * (1 + block), 0, 1)


# ---- Body (Skin modifier) ------------------------------------------------------------

# Skeleton nodes in the character frame (metres): name -> (position, (radius x, radius y)).
BODY_NODES = {
    "pelvis": ((0, 0.0, 0.93), (0.155, 0.105)),
    "spine": ((0, 0.0, 1.05), (0.142, 0.094)),
    "chest": ((0, 0.006, 1.20), (0.158, 0.1)),
    "upperchest": ((0, 0.0, 1.32), (0.165, 0.098)),
    "neckbase": ((0, -0.008, 1.42), (0.05, 0.048)),
    "necktop": ((0, -0.004, 1.53), (0.045, 0.046)),
}
for side, sx in (("R", 1), ("L", -1)):
    BODY_NODES.update({
        f"clav{side}": ((sx * 0.075, -0.005, 1.395), (0.062, 0.058)),
        f"shoulder{side}": ((sx * 0.19, -0.012, 1.395), (0.058, 0.056)),
        f"elbow{side}": ((sx * 0.45, -0.02, 1.395), (0.04, 0.04)),
        f"wrist{side}": ((sx * 0.69, -0.02, 1.395), (0.028, 0.022)),
        f"palm{side}": ((sx * 0.77, -0.02, 1.395), (0.042, 0.016)),
        f"fingers{side}": ((sx * 0.86, -0.02, 1.393), (0.036, 0.011)),
        f"thumb{side}": ((sx * 0.745, 0.035, 1.39), (0.013, 0.011)),
        f"hip{side}": ((sx * 0.09, 0.0, 0.9), (0.1, 0.1)),
        f"knee{side}": ((sx * 0.095, 0.012, 0.5), (0.066, 0.068)),
        f"ankle{side}": ((sx * 0.095, -0.012, 0.095), (0.046, 0.05)),
        f"toes{side}": ((sx * 0.097, 0.1, 0.045), (0.046, 0.034)),
        f"toe_end{side}": ((sx * 0.097, 0.175, 0.038), (0.038, 0.024)),
    })

BODY_EDGES = [("pelvis", "spine"), ("spine", "chest"), ("chest", "upperchest"),
              ("upperchest", "neckbase"), ("neckbase", "necktop")]
for s in ("R", "L"):
    BODY_EDGES += [("upperchest", f"clav{s}"), (f"clav{s}", f"shoulder{s}"), (f"shoulder{s}", f"elbow{s}"),
                   (f"elbow{s}", f"wrist{s}"), (f"wrist{s}", f"palm{s}"), (f"palm{s}", f"fingers{s}"),
                   (f"wrist{s}", f"thumb{s}"),
                   ("pelvis", f"hip{s}"), (f"hip{s}", f"knee{s}"), (f"knee{s}", f"ankle{s}"),
                   (f"ankle{s}", f"toes{s}"), (f"toes{s}", f"toe_end{s}")]

BODY_MATERIALS = ["Player_Skin", "Player_ShirtWhite", "Player_ShirtGrey", "Player_ShirtNavy",
                  "Player_Pants", "Player_Shoes"]


def build_body():
    names = list(BODY_NODES)
    mesh = bpy.data.meshes.new("BodySkeleton")
    mesh.from_pydata([tuple(cw(*BODY_NODES[n][0])) for n in names], [(names.index(a), names.index(b)) for a, b in BODY_EDGES], [])
    obj = bpy.data.objects.new("BodySkeleton", mesh)
    bpy.context.scene.collection.objects.link(obj)
    skin = obj.modifiers.new("Skin", "SKIN")
    if not mesh.skin_vertices:
        mesh.skin_vertices.new()
    skin.branch_smoothing = 0.6
    skin.use_smooth_shade = True
    for i, n in enumerate(names):
        sv = mesh.skin_vertices[0].data[i]
        # The subdivision after the Skin modifier pulls every section in by ~25 %.
        rx, ry = BODY_NODES[n][1]
        sv.radius = (rx * 1.28, ry * 1.28)
        sv.use_root = n == "pelvis"
    sub = obj.modifiers.new("Subdivision", "SUBSURF")
    sub.levels = 1
    sub.render_levels = 1

    deps = bpy.context.evaluated_depsgraph_get()
    body_mesh = bpy.data.meshes.new_from_object(obj.evaluated_get(deps))
    body_mesh.name = "Body"
    bpy.data.objects.remove(obj, do_unlink=True)

    for m in BODY_MATERIALS:
        body_mesh.materials.append(bpy.data.materials.get(m))

    # Clothing by region, per face, in the character frame.
    for poly in body_mesh.polygons:
        c = poly.center
        x, y, z = -c.x, -c.y, c.z          # character frame
        ax = abs(x)
        mat = "Player_Skin"
        if z < 0.115:
            mat = "Player_Shoes"
        elif z < 0.99 and ax < 0.25:
            mat = "Player_Pants"
        elif z < 1.45 and (ax < 0.29 or (ax < 0.34 and z > 1.33)):
            # Short sleeves end a third down the upper arm; the V-neck opens on the chest.
            v_neck = y > 0.03 and z > 1.35 + 1.3 * ax and ax < 0.08
            if v_neck or z > 1.46:
                mat = "Player_Skin"
            elif z < 1.07:
                mat = "Player_ShirtNavy"
            elif z < 1.13 + 0.05 * (x / 0.17):
                mat = "Player_ShirtGrey"
            else:
                mat = "Player_ShirtWhite"
        poly.material_index = BODY_MATERIALS.index(mat)
        poly.use_smooth = True

    # Per-metre planar UVs: the body maps are near-flat colour, any projection works.
    uv = body_mesh.uv_layers.new(name="UVMap")
    for poly in body_mesh.polygons:
        for li in poly.loop_indices:
            co = body_mesh.vertices[body_mesh.loops[li].vertex_index].co
            uv.data[li].uv = (co.x * 4 + co.y * 4, co.z * 4)
    return body_mesh


# ---- Armature ------------------------------------------------------------------------

def bones():
    B = {}
    def b(name, head, tail, parent):
        B[name] = (cw(*head), cw(*tail), parent)
    b("Hips", (0, 0, 0.93), (0, 0, 1.03), None)
    b("Spine", (0, 0, 1.03), (0, 0, 1.16), "Hips")
    b("Chest", (0, 0, 1.16), (0, 0, 1.28), "Spine")
    b("UpperChest", (0, 0, 1.28), (0, 0, 1.4), "Chest")
    b("Neck", (0, -0.005, 1.4), (0, -0.005, 1.5), "UpperChest")
    b("Head", (0, -0.005, 1.5), (0, -0.005, 1.72), "Neck")
    for side, sx in (("Right", 1), ("Left", -1)):
        b(f"{side}Shoulder", (sx * 0.03, -0.005, 1.385), (sx * 0.185, -0.012, 1.395), "UpperChest")
        b(f"{side}UpperArm", (sx * 0.185, -0.012, 1.395), (sx * 0.45, -0.02, 1.395), f"{side}Shoulder")
        b(f"{side}LowerArm", (sx * 0.45, -0.02, 1.395), (sx * 0.69, -0.02, 1.395), f"{side}LowerArm".replace("Lower", "Upper"))
        b(f"{side}Hand", (sx * 0.69, -0.02, 1.395), (sx * 0.86, -0.02, 1.393), f"{side}LowerArm")
        b(f"{side}UpperLeg", (sx * 0.09, 0.0, 0.92), (sx * 0.095, 0.012, 0.5), "Hips")
        b(f"{side}LowerLeg", (sx * 0.095, 0.012, 0.5), (sx * 0.095, -0.012, 0.095), f"{side}UpperLeg")
        b(f"{side}Foot", (sx * 0.095, -0.012, 0.095), (sx * 0.097, 0.1, 0.045), f"{side}LowerLeg")
        b(f"{side}Toes", (sx * 0.097, 0.1, 0.045), (sx * 0.097, 0.18, 0.038), f"{side}Foot")
    return B


def build_armature():
    data = bpy.data.armatures.new("PlayerRig")
    arm = bpy.data.objects.new("PlayerRig", data)
    bpy.context.scene.collection.objects.link(arm)
    bpy.context.view_layer.objects.active = arm
    bpy.ops.object.mode_set(mode="EDIT")
    for name, (head, tail, parent) in bones().items():
        eb = data.edit_bones.new(name)
        eb.head, eb.tail = head, tail
        eb.roll = 0.0
    for name, (_, _, parent) in bones().items():
        if parent:
            data.edit_bones[name].parent = data.edit_bones[parent]
            data.edit_bones[name].use_connect = False
    bpy.ops.object.mode_set(mode="OBJECT")
    return arm


def skin_to_rig(obj, arm, auto=True):
    obj.parent = arm
    mod = obj.modifiers.new("Armature", "ARMATURE")
    mod.object = arm
    if not auto:
        return
    for o in bpy.context.view_layer.objects:
        o.select_set(False)
    obj.select_set(True)
    arm.select_set(True)
    bpy.context.view_layer.objects.active = arm
    obj.modifiers.remove(mod)
    bpy.ops.object.parent_set(type="ARMATURE_AUTO")


# ---- Materials, preview, export ------------------------------------------------------

def material(name, image=None, colour=None):
    mat = bpy.data.materials.get(name) or bpy.data.materials.new(name)
    mat.use_nodes = True
    nt = mat.node_tree
    bsdf = next(n for n in nt.nodes if n.type == "BSDF_PRINCIPLED")
    bsdf.inputs["Roughness"].default_value = 0.8
    if image is not None:
        tex = nt.nodes.new("ShaderNodeTexImage")
        tex.image = image
        tex.interpolation = "Closest"
        nt.links.new(tex.outputs["Color"], bsdf.inputs["Base Color"])
    elif colour is not None:
        bsdf.inputs["Base Color"].default_value = (*colour, 1.0)
    return mat


def render_previews(out_dir):
    scene = bpy.context.scene
    scene.render.engine = "BLENDER_EEVEE"
    scene.render.resolution_x, scene.render.resolution_y = 720, 960
    scene.view_settings.view_transform = "Standard"
    world = scene.world or bpy.data.worlds.new("World")
    scene.world = world
    world.use_nodes = True
    world.node_tree.nodes["Background"].inputs["Color"].default_value = (0.55, 0.56, 0.6, 1)
    sun = bpy.data.objects.new("Sun", bpy.data.lights.new("Sun", "SUN"))
    sun.data.energy = 3.0
    sun.rotation_euler = (math.radians(55), 0, math.radians(-25))
    scene.collection.objects.link(sun)
    views = {
        "front": ((0, -2.7, 1.05), (0, 0, 0.92), 50),
        "three_quarter": ((-1.7, -2.0, 1.3), (0, 0, 0.92), 50),
        "side": ((-2.7, 0, 1.1), (0, 0, 0.92), 50),
        "back": ((0, 2.7, 1.2), (0, 0, 0.92), 50),
        "face": ((0, -0.62, 1.6), (0, 0, 1.575), 50),
        "face_right": ((-0.45, -0.45, 1.6), (0, 0, 1.575), 50),
        "face_left": ((0.45, -0.45, 1.6), (0, 0, 1.575), 50),
    }
    out = []
    for name, (loc, target, lens) in views.items():
        cam = bpy.data.objects.new("Cam_" + name, bpy.data.cameras.new("Cam_" + name))
        cam.data.lens = lens
        cam.data.clip_start = 0.02
        cam.location = loc
        scene.collection.objects.link(cam)
        aim = bpy.data.objects.new("Aim_" + name, None)
        aim.location = target
        scene.collection.objects.link(aim)
        cam.constraints.new("TRACK_TO").target = aim
        scene.camera = cam
        scene.render.filepath = os.path.join(out_dir, f"pc_{name}.png")
        bpy.ops.render.render(write_still=True)
        out.append(scene.render.filepath)
    return out


def main():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    os.makedirs(TEX_DIR, exist_ok=True)
    os.makedirs(os.path.dirname(OUT_BLEND), exist_ok=True)

    def head_bvh(parts):
        all_v, all_f = [], []
        for _, verts, faces in parts:
            base = len(all_v)
            all_v += [Vector(v) for v in verts]
            all_f += [tuple(i + base for i in f) for f in faces]
        return BVHTree.FromPolygons(all_v, all_f)

    loaded = {key: load_image(os.path.join(SOURCE, f)) for key, (f, _, _, _) in PHOTOS.items()}

    # Fit his head shape to all photos at once, then rebuild the head with it.
    parts = build_head_parts()
    marks = mesh_landmarks(head_bvh(parts))
    views = []
    for key, (_, yaw, _, pix) in PHOTOS.items():
        names = [n for n in pix if n in marks]
        views.append((names, np.array([pix[n] for n in names], float), yaw, loaded[key][1]))
    fitted, joint_rms = fit_shape(marks, views)
    SHAPE.update(fitted)
    print(f"[player_v2] head shape {({k: round(v, 3) for k, v in SHAPE.items()})}, joint rms {joint_rms:.1f} px")

    parts = build_head_parts()
    bvh = head_bvh(parts)
    marks = mesh_landmarks(bvh)

    cams, images = {}, {}
    for key, (file_name, yaw, bias, pix) in PHOTOS.items():
        img, size = loaded[key]
        names = [n for n in pix if n in marks]
        cam, rms = solve_camera(np.array([marks[n] for n in names]), np.array([pix[n] for n in names], float),
                                yaw, size)
        print(f"[player_v2] camera {key}: rms {rms:.1f} px, f {cam.f:.0f}, "
              f"centre {np.round(cam.centre, 1)} cm")
        cams[key], images[key] = cam, (img, bias)

    head_rgb, skin_rgb, hair_rgb = bake_head(parts, bvh, cams, images)
    head_img = save_png(os.path.join(TEX_DIR, "T_Player_Head.png"), head_rgb)
    print(f"[player_v2] skin {np.round(skin_rgb * 255)}, hair {np.round(hair_rgb * 255)}")

    body_colours = {
        "Player_Skin": skin_rgb,
        "Player_ShirtWhite": (0.86, 0.86, 0.84),
        "Player_ShirtGrey": (0.6, 0.61, 0.63),
        "Player_ShirtNavy": (0.1, 0.12, 0.2),
        "Player_Pants": (0.07, 0.07, 0.085),
        "Player_Shoes": (0.1, 0.1, 0.11),
    }
    for i, (name, rgb) in enumerate(body_colours.items()):
        tex_name = "T_" + name + ".png"
        img = save_png(os.path.join(TEX_DIR, tex_name), flat_map(rgb, seed=i + 1))
        material(name, img)
    material("Player_Head", head_img)

    # Head object: skull + hair + ears, one mesh, one material.
    head_parts_v, head_parts_f = [], []
    for _, verts, faces in parts:
        base = len(head_parts_v)
        head_parts_v += verts
        head_parts_f += [tuple(i + base for i in f) for f in faces]
    head_mesh = build_mesh("Head", head_parts_v, head_parts_f)
    head_mesh.materials.append(bpy.data.materials["Player_Head"])
    for poly in head_mesh.polygons:
        poly.use_smooth = True
    head = bpy.data.objects.new("PlayerHead", head_mesh)
    bpy.context.scene.collection.objects.link(head)

    body = bpy.data.objects.new("PlayerBody", build_body())
    bpy.context.scene.collection.objects.link(body)

    arm = build_armature()
    skin_to_rig(body, arm, auto=True)
    # The head is rigid: all of it follows the Head bone.
    group = head.vertex_groups.new(name="Head")
    group.add(list(range(len(head_mesh.vertices))), 1.0, "REPLACE")
    skin_to_rig(head, arm, auto=False)

    unweighted = sum(1 for v in body.data.vertices if not v.groups)
    tris = sum(len(p.vertices) - 2 for o in (head, body) for p in o.data.polygons)
    print(f"[player_v2] body verts {len(body.data.vertices)}, unweighted {unweighted}, total tris {tris}")

    bpy.ops.wm.save_as_mainfile(filepath=OUT_BLEND)

    for o in bpy.context.view_layer.objects:
        o.select_set(o in (arm, body, head))
    bpy.ops.export_scene.fbx(
        filepath=OUT_FBX, use_selection=True, object_types={"ARMATURE", "MESH"},
        add_leaf_bones=False, bake_anim=False, use_armature_deform_only=True,
        axis_forward="-Z", axis_up="Y", apply_scale_options="FBX_SCALE_ALL",
        bake_space_transform=True, mesh_smooth_type="FACE", path_mode="STRIP")
    print(f"[player_v2] exported {OUT_FBX}")

    if PREVIEW_DIR:
        os.makedirs(PREVIEW_DIR, exist_ok=True)
        print("[player_v2] previews", render_previews(PREVIEW_DIR))


main()
