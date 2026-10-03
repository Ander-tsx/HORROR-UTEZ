"""Computer-lab (compuaula) dressing kit for CECADEC's ground floor rooms.

Run: blender -b --factory-startup --python Tools/blender/gen_remake_lab.py
Exports Assets/_Project/Art/Remake/Resources/Lab/<Piece>.fbx, loaded at runtime by
RemakeDressing. Unity metres, origin on the floor at the piece's footprint centre,
the side a person uses faces -Z (screens face -Z, chairs are sat on from -Z...+Z).
Screens use the material "Screen_Dead"; the runtime swaps it per instance.
"""
import math
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import importlib  # noqa: E402

import remake_kit as K  # noqa: E402

importlib.reload(K)
from remake_kit import Mesh  # noqa: E402

OUT = os.path.join(K.ART, "Resources", "Lab")
K.TILE.update({"Laminate_White": 0.8, "Laminate_Wood": 0.8, "Metal_Painted": 0.6, "Fabric_Blue": 0.35,
               "Fabric_Black": 0.35, "Plastic_Beige": 0.4, "Plastic_Black": 0.4, "Plastic_White": 0.5,
               "Metal_Brushed": 0.5, "Rubber_Black": 0.3, "Cardboard": 0.5, "Concrete_Dirty": 1.0,
               "Cable_Grey": 0.2, "Rust": 0.6, "Chrome": 0.5, "Plastic_Grey": 0.4, "Wood_Plank": 1.0})


def piece(name, build):
    K.reset()
    m = Mesh(name)
    build(m)
    m.build()
    K.export(name, OUT)


# ---------------------------------------------------------------- furniture

def desk(m, w=1.6, d=0.7, h=0.75):
    top = 0.03
    m.box((0, h - top / 2, 0), (w, top, d), "Laminate_White", 0.004)
    m.box((0, h - top - 0.012, -d / 2 + 0.01), (w - 0.02, 0.025, 0.02), "Plastic_Black")   # edge band
    for sx in (-1, 1):
        x = sx * (w / 2 - 0.04)
        for sz in (-1, 1):
            m.box((x, (h - top) / 2, sz * (d / 2 - 0.04)), (0.04, h - top, 0.04), "Metal_Painted", 0.004)
        m.box((x, 0.1, 0), (0.035, 0.035, d - 0.08), "Metal_Painted")                     # foot rail
        m.box((x, h - top - 0.04, 0), (0.035, 0.05, d - 0.08), "Metal_Painted")
        m.box((x, 0.012, sz * 0), (0.06, 0.024, d - 0.02), "Rubber_Black")
    m.box((0, h - top - 0.04, d / 2 - 0.04), (w - 0.08, 0.05, 0.035), "Metal_Painted")
    m.box((0, 0.45, d / 2 - 0.05), (w - 0.1, 0.42, 0.012), "Metal_Painted")                # modesty panel
    for i in range(14):
        m.box((-w / 2 + 0.12 + i * 0.1, 0.45, d / 2 - 0.057), (0.03, 0.3, 0.004), "Rubber_Black")
    m.box((0, h - top - 0.12, d / 2 - 0.12), (w - 0.2, 0.012, 0.16), "Metal_Painted")      # cable tray
    m.box((0, h - top - 0.08, d / 2 - 0.2), (w - 0.2, 0.08, 0.012), "Metal_Painted")
    for x in (-w / 4, w / 4):
        m.cyl((x, h + 0.001, d / 2 - 0.08), 0.03, 0.006, "Plastic_Black", segs=10)           # grommet


def lab_desk(m):
    desk(m)
    # a bundle of cables dropping through the grommet to the floor
    m.tube([(-0.4, 0.75, 0.27), (-0.42, 0.6, 0.28), (-0.4, 0.3, 0.3), (-0.3, 0.05, 0.25), (0.1, 0.02, 0.32)],
           [0.012, 0.014, 0.015, 0.015, 0.014], "Cable_Grey", 5)


def teacher_desk(m):
    w, d, h = 1.5, 0.75, 0.76
    m.box((0, h - 0.016, 0), (w, 0.032, d), "Laminate_Wood", 0.005)
    m.box((w / 2 - 0.25, (h - 0.03) / 2, 0), (0.46, h - 0.03, d - 0.04), "Laminate_Wood", 0.004)
    for i in range(3):
        y = 0.12 + i * 0.22
        m.box((w / 2 - 0.25, y + 0.09, -d / 2 + 0.015), (0.42, 0.18, 0.02), "Laminate_Wood", 0.003)
        m.box((w / 2 - 0.25, y + 0.14, -d / 2 + 0.002), (0.12, 0.018, 0.02), "Chrome")
    m.box((-w / 2 + 0.03, (h - 0.03) / 2, 0), (0.03, h - 0.03, d - 0.04), "Laminate_Wood")
    m.box((-0.15, 0.4, d / 2 - 0.03), (w - 0.55, 0.55, 0.02), "Laminate_Wood")
    # drawer hanging open, papers spilling
    m.box((w / 2 - 0.25, 0.6, -d / 2 - 0.15), (0.42, 0.14, 0.32), "Laminate_Wood", 0.003)
    for i in range(5):
        m.quad((w / 2 - 0.3 + i * 0.04, 0.68 + i * 0.003, -d / 2 - 0.12 - i * 0.02), (0.21, 0.29), "Paper", "y",
               rot=(0, i * 17, 0))


def chair(m, back=True):
    for i in range(5):
        a = i * 72
        with m.push((0, 0.07, 0), (0, a, 0)):
            m.box((0, 0, 0.17), (0.045, 0.035, 0.32), "Plastic_Black", 0.006, rot=(-6, 0, 0))
            m.cyl((0, -0.035, 0.31), 0.025, 0.03, "Rubber_Black", axis="x", segs=8)
            m.box((0, -0.01, 0.31), (0.035, 0.03, 0.04), "Plastic_Black")
    m.cyl((0, 0.27, 0), 0.025, 0.36, "Chrome", segs=10)
    m.cyl((0, 0.18, 0), 0.04, 0.2, "Plastic_Black", segs=10)
    m.box((0, 0.46, 0), (0.46, 0.07, 0.44), "Fabric_Blue", 0.025)
    m.box((0, 0.41, 0.02), (0.3, 0.04, 0.3), "Plastic_Black")
    if back:
        m.box((0, 0.55, 0.22), (0.05, 0.22, 0.03), "Plastic_Black", rot=(8, 0, 0))
        m.box((0, 0.78, 0.25), (0.42, 0.38, 0.06), "Fabric_Blue", 0.02, rot=(8, 0, 0), taper=(0.85, 1))
        for sx in (-1, 1):
            m.box((sx * 0.25, 0.56, 0.0), (0.03, 0.2, 0.03), "Plastic_Black")
            m.box((sx * 0.25, 0.66, -0.02), (0.06, 0.03, 0.24), "Plastic_Black", 0.008)


def crt(m):
    # bezel + tapered tube housing + stand. Screen faces -Z.
    m.box((0, 0.24, 0), (0.42, 0.37, 0.07), "Plastic_Beige", 0.012)
    m.box((0, 0.24, 0.2), (0.38, 0.33, 0.34), "Plastic_Beige", 0.01, rot=(90, 0, 0), taper=(0.62, 0.62))
    m.box((0, 0.42 * 0.5 + 0.03, -0.037), (0.34, 0.27, 0.006), "Plastic_Black")
    m.quad((0, 0.235, -0.041), (0.32, 0.25), "Screen_Dead", "-z")
    m.box((0.15, 0.08, -0.037), (0.03, 0.015, 0.006), "LED_Amber")
    for i in range(6):
        m.box((0, 0.3 - i * 0.025, 0.36), (0.18, 0.008, 0.01), "Plastic_Black")
    m.box((0, 0.025, 0.08), (0.28, 0.05, 0.26), "Plastic_Beige", 0.01)
    m.cyl((0, 0.055, 0.08), 0.09, 0.03, "Plastic_Beige", segs=12)


def lcd(m):
    m.box((0, 0.3, 0), (0.5, 0.31, 0.025), "Plastic_Black", 0.006)
    m.quad((0, 0.305, -0.0131), (0.46, 0.27), "Screen_Dead", "-z")
    m.box((0, 0.15, 0.04), (0.06, 0.3, 0.025), "Plastic_Black", rot=(-10, 0, 0))
    m.box((0, 0.01, 0.03), (0.22, 0.02, 0.17), "Plastic_Black", 0.005)
    m.box((0.2, 0.17, -0.013), (0.012, 0.012, 0.004), "LED_Green")


def tower(m, open_side=False):
    w, h, d = 0.19, 0.42, 0.44
    m.box((0, h / 2, 0), (w, h, d), "Plastic_Beige", 0.008) if not open_side else None
    if open_side:
        m.box((w / 2 - 0.005, h / 2, 0), (0.01, h, d), "Plastic_Beige")           # far side panel
        m.box((0, h - 0.005, 0), (w, 0.01, d), "Plastic_Beige")
        m.box((0, 0.005, 0), (w, 0.01, d), "Plastic_Beige")
        m.box((0, h / 2, -d / 2 + 0.01), (w, h, 0.02), "Plastic_Beige", 0.006)
        m.box((0, h / 2, d / 2 - 0.005), (w, h, 0.01), "Metal_Painted")
        m.quad((w / 2 - 0.012, h / 2, 0), (0.38, 0.36), "PCB", "-x")
        m.box((0.02, 0.3, 0.05), (0.07, 0.07, 0.07), "Metal_Brushed")              # cpu cooler
        for i in range(5):
            m.box((-0.02 + i * 0.012, 0.3, 0.05), (0.004, 0.065, 0.065), "Metal_Brushed")
        m.box((0.03, 0.17, -0.02), (0.012, 0.12, 0.2), "PCB")                        # graphics card
        m.box((0, 0.36, 0.14), (0.15, 0.08, 0.13), "Metal_Painted")                 # PSU
        for path in ([(0, 0.36, 0.07), (-0.03, 0.3, 0.0), (0.0, 0.22, -0.05)],
                     [(0.02, 0.33, 0.08), (-0.05, 0.12, 0.05), (-0.07, 0.015, -0.1), (-0.2, 0.01, -0.3)]):
            m.tube(path, 0.007, "Cable_Grey", 5)
    m.box((0, h - 0.07, -d / 2 - 0.003), (0.15, 0.04, 0.006), "Plastic_Black")
    m.box((0, h - 0.13, -d / 2 - 0.003), (0.15, 0.04, 0.006), "Plastic_Black")
    m.cyl((0.05, 0.08, -d / 2 - 0.003), 0.012, 0.008, "Plastic_Grey", axis="z", segs=8)
    m.box((-0.05, 0.08, -d / 2 - 0.004), (0.008, 0.008, 0.004), "LED_Green")
    for i in range(6):
        m.box((0, 0.17 + i * 0.022, -d / 2 - 0.003), (0.12, 0.006, 0.004), "Plastic_Black")


def keyboard(m):
    m.box((0, 0.012, 0), (0.45, 0.024, 0.15), "Plastic_Beige", 0.006, taper=(1, 0.92))
    m.quad((0, 0.0255, 0.005), (0.42, 0.12), "Keyboard", "y")
    m.tube([(0, 0.015, 0.075), (0.02, 0.012, 0.2), (0.1, 0.01, 0.3)], 0.004, "Cable_Grey", 4)


def mouse(m):
    m.sphere((0, 0.018, 0), (0.03, 0.02, 0.05), "Plastic_Beige", 8, 5)
    m.tube([(0, 0.02, 0.05), (0.01, 0.01, 0.15), (-0.05, 0.005, 0.25)], 0.003, "Cable_Grey", 4)


def whiteboard(m):
    w, h = 2.4, 1.2
    m.box((0, h / 2, 0.01), (w + 0.04, h + 0.04, 0.02), "Metal_Brushed", 0.005)
    m.quad((0, h / 2, -0.0005), (w, h), "Whiteboard", "-z")
    m.box((0, -0.02, -0.04), (w * 0.7, 0.025, 0.07), "Metal_Brushed")
    for i, x in enumerate((-0.3, -0.18, 0.4)):
        m.cyl((x, 0.0, -0.05), 0.009, 0.12, ("Plastic_Red", "Plastic_Black", "Plastic_Grey")[i], axis="x", segs=6)


def projector_mount(m):
    m.cyl((0, -0.3, 0), 0.025, 0.6, "Metal_Painted", segs=8)
    m.box((0, -0.62, 0), (0.18, 0.04, 0.18), "Metal_Painted")
    m.box((0, -0.7, 0), (0.32, 0.11, 0.26), "Plastic_White", 0.02)
    m.cyl((-0.08, -0.7, -0.13), 0.04, 0.03, "Glass_Dirty", axis="z", segs=10)
    m.tube([(0.1, -0.68, 0.12), (0.12, -0.5, 0.15), (0.1, 0.0, 0.2)], 0.008, "Cable_Grey", 4)


def minisplit(m):
    m.box((0, 0, 0), (0.86, 0.29, 0.21), "Plastic_White", 0.03)
    m.box((0, -0.1, -0.09), (0.76, 0.07, 0.04), "Plastic_Grey", 0.01, rot=(25, 0, 0))
    for i in range(10):
        m.box((-0.34 + i * 0.075, 0.08, -0.106), (0.05, 0.004, 0.004), "Plastic_Grey")
    m.box((0.33, -0.02, -0.106), (0.03, 0.01, 0.003), "LED_Green")
    m.tube([(0.4, -0.05, 0.08), (0.48, -0.25, 0.1), (0.48, -1.6, 0.1)], 0.02, "Plastic_White", 6)
    m.tube([(0.35, -0.13, 0.0), (0.36, -0.3, 0.02)], 0.006, "Rubber_Black", 4)


def shelf(m):
    w, h, d = 0.95, 1.85, 0.4
    for sx in (-1, 1):
        for sz in (-1, 1):
            m.box((sx * (w / 2 - 0.02), h / 2, sz * (d / 2 - 0.02)), (0.035, h, 0.035), "Metal_Painted")
    for i in range(5):
        y = 0.08 + i * 0.42
        m.box((0, y, 0), (w, 0.02, d), "Metal_Painted", 0.003)
    m.box((-0.2, 0.25, 0), (0.42, 0.32, 0.33), "Cardboard", 0.01)
    m.box((0.22, 0.2, 0.02), (0.36, 0.22, 0.3), "Cardboard", 0.01, rot=(0, 8, 0))
    m.box((0.1, 0.62, 0), (0.6, 0.18, 0.3), "Cardboard", 0.01)
    with m.push((-0.15, 0.93, 0.02), (0, -12, 0)):
        crt(m)
    for i in range(6):
        m.box((0.15 + i * 0.045, 1.45, 0), (0.035, 0.28, 0.3), "Plastic_Black" if i % 2 else "Plastic_Grey")
    m.tube([(0.4, 1.7, 0.1), (0.3, 1.4, 0.18), (0.42, 0.95, 0.15)], 0.008, "Cable_Grey", 4)


def rack(m):
    w, h, d = 0.6, 1.9, 0.75
    m.box((0, h / 2, 0.02), (w, h, d - 0.04), "Plastic_Black", 0.01)
    m.box((0, h / 2, -d / 2 + 0.02), (w - 0.04, h - 0.06, 0.01), "Glass_Dirty")
    for i in range(9):
        y = 0.35 + i * 0.15
        m.box((0, y, -d / 2 + 0.04), (0.48, 0.042, 0.02), "Metal_Brushed" if i % 3 else "Plastic_Grey")
        for p in range(8):
            mat = "LED_Green" if (i + p) % 3 else ("LED_Amber" if (i * p) % 5 else "LED_Red")
            m.box((-0.2 + p * 0.03, y + 0.008, -d / 2 + 0.028), (0.008, 0.006, 0.004), mat)
    for k in range(5):
        m.tube([(-0.2 + k * 0.08, 0.4 + k * 0.15, -d / 2 + 0.03), (-0.25 + k * 0.05, 0.3 + k * 0.1, -d / 2 - 0.06),
                (-0.3 + k * 0.1, 0.02, -d / 2 - 0.2)], 0.006, ("Cable_Grey", "Plastic_Red", "LED_Green")[k % 3] if k == 99 else "Cable_Grey", 4)


def locker(m):
    w, h, d = 0.9, 1.8, 0.45
    m.box((0, h / 2, 0), (w, h, d), "Metal_Painted", 0.006)
    for i in range(3):
        x = -w / 3 + i * w / 3
        m.box((x, h / 2, -d / 2 - 0.004), (w / 3 - 0.02, h - 0.06, 0.008), "Metal_Painted")
        for v in range(5):
            m.box((x, h - 0.2 - v * 0.03, -d / 2 - 0.009), (0.15, 0.012, 0.004), "Plastic_Black")
        m.box((x + 0.09, h / 2, -d / 2 - 0.012), (0.02, 0.08, 0.012), "Chrome")
    # one door bent open
    m.box((w / 3 + 0.12, h / 2, -d / 2 - 0.13), (0.008, h - 0.06, w / 3 - 0.02), "Metal_Painted", rot=(0, 25, 0))


def trash(m):
    m.cyl((0, 0.18, 0), 0.15, 0.36, "Plastic_Grey", segs=10, r2=0.18, caps=False)
    m.cyl((0, 0.005, 0), 0.15, 0.01, "Plastic_Grey", segs=10)
    for i, (x, z) in enumerate(((0.05, 0.03), (-0.06, -0.02), (0.0, -0.08), (0.2, 0.15), (-0.25, 0.1))):
        m.sphere((x, 0.33 if abs(x) < 0.1 else 0.04, z), 0.045 + 0.01 * (i % 2), "Paper", 6, 4)


def papers(m):
    import random
    r = random.Random(3)
    for i in range(14):
        m.quad((r.uniform(-0.7, 0.7), 0.002 + i * 0.0004, r.uniform(-0.5, 0.5)), (0.21, 0.29), "Paper", "y",
               rot=(0, r.uniform(0, 360), 0))


def ceiling_tile(m):
    m.box((0, 0.012, 0), (0.6, 0.012, 0.6), "Plastic_White", rot=(6, 20, 3))
    m.box((0.38, 0.01, 0.2), (0.3, 0.012, 0.25), "Plastic_White", rot=(-8, 50, 0))
    import random
    r = random.Random(9)
    for i in range(10):
        m.box((r.uniform(-0.5, 0.6), 0.02, r.uniform(-0.4, 0.5)), (r.uniform(0.03, 0.09),) * 3, "Concrete_Dirty",
              rot=(r.uniform(0, 90), r.uniform(0, 90), 0))


def cable_mess(m):
    import random
    r = random.Random(4)
    for k in range(7):
        pts = [(r.uniform(-0.8, 0.8), 0.01, r.uniform(-0.6, 0.6))]
        for j in range(5):
            x, y, z = pts[-1]
            pts.append((x + r.uniform(-0.3, 0.3), 0.01 + 0.004 * (j % 2), z + r.uniform(-0.3, 0.3)))
        m.tube(pts, 0.008, "Cable_Grey" if k % 3 else "Rubber_Black", 4)


def fluoro_hanging(m):
    # fixture hanging from one chain: origin at the ceiling attachment.
    m.tube([(0, 0, 0), (0.02, -0.4, 0)], 0.006, "Metal_Brushed", 4)
    with m.push((0.25, -0.62, 0), (0, 0, -28)):
        m.box((0, 0, 0), (1.22, 0.07, 0.3), "Plastic_White", 0.01)
        m.cyl((0.05, -0.045, -0.06), 0.016, 1.1, "Plastic_White", axis="x", segs=6)
        m.cyl((-0.2, -0.045, 0.06), 0.016, 0.6, "Glass_Dirty", axis="x", segs=6)
    m.tube([(-0.5, -0.9, 0.1), (-0.48, -0.5, 0.12), (-0.3, 0, 0.1)], 0.006, "Cable_Grey", 4)


def lab_bench(m):
    w, d, h = 1.8, 0.85, 0.88
    m.box((0, h - 0.025, 0), (w, 0.05, d), "Metal_Brushed", 0.004)
    m.box((0, 0.4, 0.05), (w - 0.06, 0.78, d - 0.12), "Metal_Painted", 0.006)
    for i in range(4):
        m.box((-w / 2 + 0.25 + i * 0.43, 0.6, -d / 2 + 0.054), (0.38, 0.24, 0.01), "Metal_Painted")
        m.box((-w / 2 + 0.25 + i * 0.43, 0.68, -d / 2 + 0.045), (0.12, 0.02, 0.02), "Chrome")
    m.box((0.55, h + 0.06, 0.1), (0.2, 0.07, 0.14), "Metal_Painted", 0.005)            # vise
    m.box((0.55, h + 0.08, -0.04), (0.18, 0.05, 0.03), "Metal_Painted")
    m.cyl((0.55, h + 0.08, -0.12), 0.01, 0.16, "Chrome", axis="z", segs=6)
    m.box((-0.5, h + 0.15, d / 2 - 0.1), (0.7, 0.3, 0.12), "Metal_Painted")               # gas rail
    for i in range(3):
        m.cyl((-0.75 + i * 0.25, h + 0.15, d / 2 - 0.18), 0.012, 0.06, "Plastic_Red", axis="z", segs=6)
    m.tube([(0.8, h + 0.3, d / 2), (0.8, h + 0.02, d / 2 - 0.1), (0.2, h + 0.02, 0.0)], 0.01, "Rubber_Black", 5)


def tape(m):
    m.quad((0, 1.2, 0), (1.4, 0.08), "Caution_Tape", "-z", rot=(0, 0, 32))
    m.quad((0, 1.2, 0), (1.4, 0.08), "Caution_Tape", "z", rot=(0, 0, 32))
    m.quad((0, 1.2, 0.002), (1.4, 0.08), "Caution_Tape", "-z", rot=(0, 0, -32))
    m.quad((0, 1.2, 0.002), (1.4, 0.08), "Caution_Tape", "z", rot=(0, 0, -32))


def poster(m):
    m.quad((0, 0, 0), (0.6, 0.6), "Poster_Rules", "-z")


def exit_sign(m):
    m.box((0, 0, 0.03), (0.36, 0.18, 0.06), "Plastic_White", 0.006)
    m.quad((0, 0, -0.0005), (0.32, 0.15), "Sign_Exit", "-z")


def blood(m):
    m.quad((0, 0.003, 0), (0.9, 0.6), "Blood_Dry", "y", rot=(0, 23, 0))
    m.quad((0.5, 0.0035, 0.3), (0.3, 0.22), "Blood_Dry", "y", rot=(0, 70, 0))


for name, fn in [
    ("Lab_Desk", lab_desk), ("Lab_TeacherDesk", teacher_desk), ("Lab_Chair", chair),
    ("Lab_Stool", lambda m: chair(m, back=False)), ("Lab_CRT", crt), ("Lab_LCD", lcd),
    ("Lab_Tower", tower), ("Lab_TowerOpen", lambda m: tower(m, True)), ("Lab_Keyboard", keyboard),
    ("Lab_Mouse", mouse), ("Lab_Whiteboard", whiteboard), ("Lab_ProjectorMount", projector_mount),
    ("Lab_AC", minisplit), ("Lab_Shelf", shelf), ("Lab_Rack", rack), ("Lab_Locker", locker),
    ("Lab_Trash", trash), ("Lab_Papers", papers), ("Lab_CeilingTile", ceiling_tile),
    ("Lab_CableMess", cable_mess), ("Lab_FluoroHanging", fluoro_hanging), ("Lab_Bench", lab_bench),
    ("Lab_Tape", tape), ("Lab_Poster", poster), ("Lab_ExitSign", exit_sign), ("Lab_Blood", blood),
]:
    piece(name, fn)
