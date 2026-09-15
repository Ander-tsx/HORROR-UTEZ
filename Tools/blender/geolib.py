"""
Geometry helpers for the landmark generators. Blender-native axes: X east, Y north,
Z up, metres. UVs are per metre (1 UV unit = 1 m) unless a primitive sets its own
(cards, decals), matching the kit convention so Unity Tiling = 1 / tile metres.

Rules every builder here keeps, so a piece stays editable by hand in Blender:
  - one MeshBuilder -> one object; nothing from two elements shares a mesh;
  - no overlapping or coplanar faces inside a mesh; welded, no degenerate faces;
  - two-sided surfaces (glass, cards) keep their back face 2 mm behind the front,
    so the two faces never share vertices, and both keep the facing they were authored
    with (normal recalculation would otherwise turn a lone face at random, and a
    back-face-culling shader then hides it);
  - every object is tagged obj["lm_unit"] with the builder unit that made it, so a
    generator can rebuild one unit in a hand-edited file without touching the rest.
"""

import math
import os

import bmesh
import bpy
from mathutils import Matrix, Vector
from mathutils.geometry import tessellate_polygon

BACK_OFFSET = 0.002


# ---- 2-D paths ---------------------------------------------------------------

def arc(cx, cy, r, a0, a1, segs):
    """Points on a circle from angle a0 to a1 (degrees), inclusive."""
    return [(cx + r * math.cos(math.radians(a0 + (a1 - a0) * i / segs)),
             cy + r * math.sin(math.radians(a0 + (a1 - a0) * i / segs))) for i in range(segs + 1)]


def _fillet(p, a, b, r, segs):
    """Arc replacing corner p between neighbours a and b (or p itself if straight)."""
    p = Vector(p); da = (Vector(a) - p).normalized(); db = (Vector(b) - p).normalized()
    half = math.acos(max(-1.0, min(1.0, da.dot(db)))) / 2
    if r <= 0 or half < 1e-3 or half > math.pi / 2 - 1e-3:
        return [tuple(p)]
    t = r / math.tan(half)
    pa = p + da * t; pb = p + db * t
    centre = p + (da + db).normalized() * (r / math.sin(half))
    a0 = math.atan2(pa.y - centre.y, pa.x - centre.x)
    a1 = math.atan2(pb.y - centre.y, pb.x - centre.x)
    d = (a1 - a0 + math.pi) % (2 * math.pi) - math.pi
    return [(centre.x + r * math.cos(a0 + d * k / segs), centre.y + r * math.sin(a0 + d * k / segs))
            for k in range(segs + 1)]


def rounded(points, radius, segs=5):
    """Closed polygon with each corner filleted. radius may be a list per corner."""
    n = len(points)
    radii = radius if isinstance(radius, (list, tuple)) else [radius] * n
    out = []
    for i in range(n):
        out += _fillet(points[i], points[i - 1], points[(i + 1) % n], radii[i], segs)
    return out


def rounded_open(points, radius, segs=5):
    """Open polyline with its inner corners filleted; the end points stay put."""
    n = len(points)
    radii = radius if isinstance(radius, (list, tuple)) else [radius] * n
    out = [tuple(points[0])]
    for i in range(1, n - 1):
        out += _fillet(points[i], points[i - 1], points[i + 1], radii[i], segs)
    return out + [tuple(points[-1])]


def polygon_area(poly):
    return 0.5 * sum(poly[i - 1][0] * poly[i][1] - poly[i][0] * poly[i - 1][1]
                     for i in range(len(poly)))


def rect(x0, y0, x1, y1):
    return [(x0, y0), (x1, y0), (x1, y1), (x0, y1)]


# ---- Mesh builder --------------------------------------------------------------

class MeshBuilder:
    """Faces for ONE object. `transform`, when set, maps every point on its way in
    (used to extrude a 2-D outline in a vertical plane: see frame_panel)."""

    def __init__(self, materials):
        self.materials = list(materials)
        self.bm = bmesh.new()
        self.uv = self.bm.loops.layers.uv.new("UVMap")
        self.explicit = set()
        self.fill = set()
        self.keep = set()
        self.mat_rot = {}
        self.transform = None

    def slot(self, name):
        if name not in self.materials:
            self.materials.append(name)
        return self.materials.index(name)

    def _p(self, p):
        return Vector(self.transform(p) if self.transform else p)

    def face(self, pts, mat, uvs=None, keep=False):
        """One face. keep=True: its winding (counter-clockwise seen from the front) is
        final, finish() will not recalculate its normal."""
        verts = [self.bm.verts.new(self._p(p)) for p in pts]
        f = self.bm.faces.new(verts)
        f.material_index = self.slot(mat)
        if uvs is not None:
            for loop, uv in zip(f.loops, uvs):
                loop[self.uv].uv = uv
            self.explicit.add(f)
        if keep:
            self.keep.add(f)
        return f

    def pane(self, pts, mat, uvs=None):
        """Two-sided quad: front face, and a back face BACK_OFFSET behind it."""
        front = self.face(pts, mat, uvs, keep=True)
        # A new BMFace has no normal until asked for one; without this the back face
        # landed on the front, remove_doubles fused them and the pane went one-sided.
        front.normal_update()
        n = front.normal.copy()
        back = [tuple(Vector(v.co) - n * BACK_OFFSET) for v in reversed(front.verts)]
        saved, self.transform = self.transform, None
        self.face(back, mat, uvs[::-1] if uvs else None, keep=True)
        self.transform = saved

    def box(self, lo, hi, mat, cap=None):
        """Axis-aligned closed box. cap(x, y) -> z reshapes the top face."""
        (x0, y0, z0), (x1, y1, z1) = lo, hi
        def z(x, y, top):
            return (cap(x, y) if cap else z1) if top else z0
        c = {(i, j, k): (xs, ys, z(xs, ys, k)) for i, xs in enumerate((x0, x1))
             for j, ys in enumerate((y0, y1)) for k in (0, 1)}
        quads = [((0, 0, 0), (0, 1, 0), (1, 1, 0), (1, 0, 0)),
                 ((0, 0, 1), (1, 0, 1), (1, 1, 1), (0, 1, 1)),
                 ((0, 0, 0), (1, 0, 0), (1, 0, 1), (0, 0, 1)),
                 ((0, 1, 0), (0, 1, 1), (1, 1, 1), (1, 1, 0)),
                 ((0, 0, 0), (0, 0, 1), (0, 1, 1), (0, 1, 0)),
                 ((1, 0, 0), (1, 1, 0), (1, 1, 1), (1, 0, 1))]
        for q in quads:
            self.face([c[k] for k in q], mat)

    def _cap(self, outer, holes, z, mat, up):
        """Flat face at height z over outer minus holes (triangulated, merged later)."""
        if not holes:
            pts = [(x, y, z) for x, y in (outer if up else reversed(outer))]
            f = self.face(pts, mat)
            return
        loops = [[Vector((x, y, z)) for x, y in outer]] + [[Vector((x, y, z)) for x, y in h] for h in holes]
        flat = [p for loop in loops for p in loop]
        for tri in tessellate_polygon(loops):
            a, b, c = (flat[i] for i in tri)
            cross = (b - a).cross(c - a)
            if cross.length < 1e-9:          # collinear sliver from the tessellator
                continue
            if (cross.z > 0) != up:
                b, c = c, b
            self.fill.add(self.face([a, b, c], mat))

    def prism(self, poly, z0, z1, mat, side_mat=None, rot=0.0, bottom=False, holes=()):
        """Extruded polygon: top, sides, optional bottom. Holes cut through it (their
        walls face into the hole). rot turns the per-metre UVs of `mat`'s flat faces."""
        outer = poly if polygon_area(poly) > 0 else poly[::-1]
        inner = [h if polygon_area(h) < 0 else h[::-1] for h in holes]
        if rot:
            self.mat_rot[self.slot(mat)] = rot
        self._cap(outer, inner, z1, mat, up=True)
        if bottom:
            self._cap(outer, inner, z0, mat, up=False)
        sm = side_mat or mat
        for loop in [outer] + inner:
            for i in range(len(loop)):
                (ax, ay), (bx, by) = loop[i], loop[(i + 1) % len(loop)]
                self.face([(ax, ay, z0), (bx, by, z0), (bx, by, z1), (ax, ay, z1)], sm)

    def frame_panel(self, outer, holes, y0, y1, mat):
        """Flat frame in the vertical XZ plane (outline and holes given as (x, z)),
        `y0..y1` thick: a window or door frame as one welded, hole-cut mesh."""
        self.transform = lambda p: (p[0], -p[2], p[1])      # 2-D y -> Z, extrusion -> -Y
        self.prism(outer, -y1, -y0, mat, bottom=True, holes=holes)
        self.transform = None

    def sweep(self, path, width, z0, z1, mat, closed=False, cap_ends=True):
        """Low wall of `width` centred on a 2-D polyline, UVs along its length."""
        n = len(path)
        pts = [Vector(p) for p in path]
        normals = []
        for i in range(n):
            prev = pts[i - 1] if (closed or i > 0) else None
            nxt = pts[(i + 1) % n] if (closed or i < n - 1) else None
            d = Vector((0.0, 0.0))
            if prev is not None:
                d += (pts[i] - prev).normalized()
            if nxt is not None:
                d += (nxt - pts[i]).normalized()
            d.normalize()
            nrm = Vector((-d.y, d.x))
            seg = (nxt - pts[i]) if nxt is not None else (pts[i] - prev)
            miter = 1.0 / max(0.4, abs(nrm.dot(Vector((-seg.normalized().y, seg.normalized().x)))))
            normals.append(nrm * (width / 2) * miter)
        dist = [0.0]
        for i in range(1, n + (1 if closed else 0)):
            dist.append(dist[-1] + (pts[i % n] - pts[i - 1]).length)
        h = z1 - z0
        for i in range(n if closed else n - 1):
            j = (i + 1) % n
            u0, u1 = dist[i], dist[i + 1]
            L0, L1 = pts[i] + normals[i], pts[j] + normals[j]
            R0, R1 = pts[i] - normals[i], pts[j] - normals[j]
            self.face([(*R0, z0), (*R1, z0), (*R1, z1), (*R0, z1)], mat,
                      uvs=[(u0, 0), (u1, 0), (u1, h), (u0, h)])
            self.face([(*L1, z0), (*L0, z0), (*L0, z1), (*L1, z1)], mat,
                      uvs=[(u1, 0), (u0, 0), (u0, h), (u1, h)])
            self.face([(*R0, z1), (*R1, z1), (*L1, z1), (*L0, z1)], mat,
                      uvs=[(u0, h), (u1, h), (u1, h + width), (u0, h + width)])
        if cap_ends and not closed:
            for i, first in ((0, True), (n - 1, False)):
                L, R = pts[i] + normals[i], pts[i] - normals[i]
                q = [(*L, z0), (*R, z0), (*R, z1), (*L, z1)]
                self.face(q if first else q[::-1], mat,
                          uvs=[(0, 0), (width, 0), (width, h), (0, h)])

    def tube(self, points, radii, mat, segs=6, cap_end=True):
        """One welded tube along a 3-D polyline (branches, poles). Rings follow the
        path by parallel transport, so consecutive segments share their ring."""
        pts = [Vector(p) for p in points]
        n = len(pts)
        tangents = []
        for i in range(n):
            a = pts[max(i - 1, 0)]; b = pts[min(i + 1, n - 1)]
            tangents.append((b - a).normalized())
        ref = Vector((0, 0, 1)) if abs(tangents[0].z) < 0.9 else Vector((1, 0, 0))
        u = tangents[0].cross(ref).normalized()
        rings, dist = [], [0.0]
        for i in range(n):
            t = tangents[i]
            u = (u - t * u.dot(t)).normalized()
            v = t.cross(u)
            rings.append([pts[i] + (u * math.cos(2 * math.pi * k / segs) + v * math.sin(2 * math.pi * k / segs)) * radii[i]
                          for k in range(segs)])
            if i:
                dist.append(dist[-1] + (pts[i] - pts[i - 1]).length)
        circ = 2 * math.pi * max(radii[0], 0.01)
        for i in range(n - 1):
            for k in range(segs):
                j = (k + 1) % segs
                self.face([rings[i][k], rings[i][j], rings[i + 1][j], rings[i + 1][k]], mat,
                          uvs=[(circ * k / segs, dist[i]), (circ * (k + 1) / segs, dist[i]),
                               (circ * (k + 1) / segs, dist[i + 1]), (circ * k / segs, dist[i + 1])])
        if cap_end:
            self.face(rings[-1], mat)
        return pts[-1]

    def cylinder(self, base, r0, r1, height, mat, segs=8, top=True, tilt=None):
        """Straight tapered tube standing on `base` along `tilt` (default up)."""
        axis = Vector(tilt).normalized() if tilt else Vector((0, 0, 1))
        end = Vector(base) + axis * height
        self.tube([base, end], [r0, r1], mat, segs=segs, cap_end=top)
        return end

    def sphere(self, centre, r, mat, segs=8, rings=5, squash=1.0):
        """UV sphere with triangle fans at the poles (no degenerate quads)."""
        c = Vector(centre)
        bottom = c + Vector((0, 0, -r * squash)); top = c + Vector((0, 0, r * squash))
        rows = []
        for k in range(1, rings):
            t = math.pi * k / rings
            rows.append([c + Vector((r * math.sin(t) * math.cos(2 * math.pi * i / segs),
                                     r * math.sin(t) * math.sin(2 * math.pi * i / segs),
                                     -r * squash * math.cos(t))) for i in range(segs)])
        for i in range(segs):
            j = (i + 1) % segs
            self.face([bottom, rows[0][j], rows[0][i]], mat)
            self.face([top, rows[-1][i], rows[-1][j]], mat)
        for k in range(len(rows) - 1):
            for i in range(segs):
                j = (i + 1) % segs
                self.face([rows[k][i], rows[k][j], rows[k + 1][j], rows[k + 1][i]], mat)

    def blob(self, centre, size, mat, seed, segs=7, rings=5, flatten=0.55):
        """Lumpy boulder: a sphere displaced by a smooth function of direction, so
        shared vertices move together and the mesh stays welded."""
        cx, cy, cz = centre
        def p(t, a):
            d = 1.0 + 0.22 * math.sin(a * 2 + seed) * math.sin(t * 3 + seed * 0.7) + 0.1 * math.cos(a * 3 - seed)
            return (cx + size * d * math.sin(t) * math.cos(a), cy + size * 0.8 * d * math.sin(t) * math.sin(a),
                    cz - size * flatten * d * math.cos(t))
        bottom = (cx, cy, cz - size * flatten); top = (cx, cy, cz + size * flatten)
        rows = [[p(math.pi * k / rings, 2 * math.pi * i / segs) for i in range(segs)] for k in range(1, rings)]
        for i in range(segs):
            j = (i + 1) % segs
            self.face([bottom, rows[0][j], rows[0][i]], mat)
            self.face([top, rows[-1][i], rows[-1][j]], mat)
        for k in range(len(rows) - 1):
            for i in range(segs):
                j = (i + 1) % segs
                self.face([rows[k][i], rows[k][j], rows[k + 1][j], rows[k + 1][i]], mat)

    def card(self, centre, size, yaw, pitch, uv_rect, mat, double=True, lift=0.0):
        """Alpha-tested quad. uv_rect = (u0, v0, u1, v1) in the atlas."""
        w, h = size
        m = Matrix.Rotation(math.radians(yaw), 3, "Z") @ Matrix.Rotation(math.radians(pitch), 3, "X")
        c = Vector(centre)
        corners = [tuple(c + m @ Vector(v)) for v in ((-w / 2, 0, -h / 2 + lift), (w / 2, 0, -h / 2 + lift),
                                                       (w / 2, 0, h / 2 + lift), (-w / 2, 0, h / 2 + lift))]
        u0, v0, u1, v1 = uv_rect
        uvs = [(u0, v0), (u1, v0), (u1, v1), (u0, v1)]
        if double:
            self.pane(corners, mat, uvs)
        else:
            self.face(corners, mat, uvs=uvs)

    def verts_bounds(self):
        co = [v.co for v in self.bm.verts]
        lo = Vector((min(p.x for p in co), min(p.y for p in co), min(p.z for p in co)))
        hi = Vector((max(p.x for p in co), max(p.y for p in co), max(p.z for p in co)))
        return lo, hi

    def finish(self, name, pivot=(0.0, 0.0, 0.0)):
        bm = self.bm
        bmesh.ops.remove_doubles(bm, verts=bm.verts, dist=1e-5)
        # Holed caps keep the tessellator's triangles. Merging them back (the old
        # dissolve_limit) made n-gons whose outline ran round the hole along a seam:
        # Blender draws those, Unity's triangulator fills straight across the hole.
        bmesh.ops.dissolve_degenerate(bm, dist=1e-5, edges=bm.edges[:])
        # Any n-gon left (concave caps: stair profiles, notched outlines) is triangulated
        # here, by Blender, so the importer never has to guess.
        big = [f for f in bm.faces if len(f.verts) > 4]
        if big:
            bmesh.ops.triangulate(bm, faces=big, quad_method="BEAUTY", ngon_method="BEAUTY")
        explicit = {f for f in self.explicit if f.is_valid}
        for f in bm.faces:
            if f in explicit:
                continue
            n = f.normal
            rot = self.mat_rot.get(f.material_index, 0.0)
            cs, sn = math.cos(math.radians(rot)), math.sin(math.radians(rot))
            for loop in f.loops:
                c = loop.vert.co
                if abs(n.z) >= max(abs(n.x), abs(n.y)):
                    u, v = c.x, c.y
                elif abs(n.x) >= abs(n.y):
                    u, v = c.y, c.z
                else:
                    u, v = c.x, c.z
                loop[self.uv].uv = (u * cs - v * sn, u * sn + v * cs)
        keep = {f for f in self.keep if f.is_valid}
        bmesh.ops.recalc_face_normals(bm, faces=[f for f in bm.faces if f not in keep])
        if tuple(pivot) != (0.0, 0.0, 0.0):
            bmesh.ops.translate(bm, verts=bm.verts, vec=-Vector(pivot))
        mesh = bpy.data.meshes.new(name)
        bm.to_mesh(mesh)
        bm.free()
        return mesh


# ---- Objects and preview materials -------------------------------------------------

def make_object(name, builder, collection, pivot="base", unit=None, materials=None):
    """Object from a builder. pivot: a point, "base" (centre of the footprint at the
    lowest point — the default, so a piece can be dropped and rotated in place) or
    "centre"."""
    if isinstance(pivot, str):
        lo, hi = builder.verts_bounds()
        mid = (lo + hi) / 2
        pivot = (mid.x, mid.y, lo.z) if pivot == "base" else tuple(mid)
    mesh = builder.finish(name, pivot)
    for m in builder.materials:
        mesh.materials.append(materials[m] if materials else bpy.data.materials.get(m))
    obj = bpy.data.objects.new(name, mesh)
    obj.location = pivot
    collection.objects.link(obj)
    if unit:
        obj["lm_unit"] = unit
    return obj


def make_empty(name, location, collection, size=0.3, unit=None):
    e = bpy.data.objects.new(name, None)
    e.location = location
    e.empty_display_size = size
    collection.objects.link(e)
    if unit:
        e["lm_unit"] = unit
    return e


def preview_material(name, texture_dir, file_name=None, tiling=1.0, flat=None,
                     alpha=None, clip=False, emission=0.0):
    """Blender-side look of a Kit material, so the .blend previews textured."""
    mat = bpy.data.materials.get(name) or bpy.data.materials.new(name)
    mat.use_nodes = True
    nt = mat.node_tree
    nt.nodes.clear()
    out = nt.nodes.new("ShaderNodeOutputMaterial")
    bsdf = nt.nodes.new("ShaderNodeBsdfPrincipled")
    bsdf.inputs["Roughness"].default_value = 0.9
    nt.links.new(bsdf.outputs["BSDF"], out.inputs["Surface"])
    if flat is not None:
        bsdf.inputs["Base Color"].default_value = (*flat, 1.0)
    path = os.path.join(texture_dir, file_name) if file_name else None
    if path and os.path.exists(path):
        tex = nt.nodes.new("ShaderNodeTexImage")
        tex.image = bpy.data.images.load(path, check_existing=True)
        tex.image.reload()
        mapping = nt.nodes.new("ShaderNodeMapping")
        tx, ty = tiling if isinstance(tiling, (tuple, list)) else (tiling, tiling)
        mapping.inputs["Scale"].default_value = (tx, ty, 1.0)
        coords = nt.nodes.new("ShaderNodeTexCoord")
        nt.links.new(coords.outputs["UV"], mapping.inputs["Vector"])
        nt.links.new(mapping.outputs["Vector"], tex.inputs["Vector"])
        nt.links.new(tex.outputs["Color"], bsdf.inputs["Base Color"])
        if clip:
            nt.links.new(tex.outputs["Alpha"], bsdf.inputs["Alpha"])
    if emission:
        bsdf.inputs["Emission Color"].default_value = bsdf.inputs["Base Color"].default_value
        bsdf.inputs["Emission Strength"].default_value = emission
    if alpha is not None:
        bsdf.inputs["Alpha"].default_value = alpha
        bsdf.inputs["Roughness"].default_value = 0.1
    if alpha is not None or clip:
        mat.blend_method = "BLEND" if alpha is not None else "CLIP"
        if hasattr(mat, "surface_render_method"):
            mat.surface_render_method = "BLENDED" if alpha is not None else "DITHERED"
    mat.use_backface_culling = not clip
    return mat
