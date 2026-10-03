"""Remake salvage, truck and enemy models (v2, built on remake_kit).

Run: blender -b --factory-startup --python Tools/blender/gen_remake_props.py

Unity coordinates are authored directly (no runtime 180 flip any more):
- loot is centred on its collider centre, its "front" faces -Z;
- the truck uses the gameplay shell coordinates: cab at +Z, cargo opening at -Z;
- characters face +Z and are split into named SEGMENTS (Pelvis, Spine, Chest, Neck, Head,
  UpperArm_L/R, LowerArm_L/R, Hand_L/R, UpperLeg_L/R, LowerLeg_L/R, Foot_L/R). Each
  segment's object origin is its joint; RemakeBody animates them procedurally.
"""
import math
import os
import random
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import importlib  # noqa: E402

import remake_kit as K  # noqa: E402

importlib.reload(K)
from remake_kit import Mesh  # noqa: E402

K.TILE.update({"Plastic_Beige": 0.4, "Plastic_Black": 0.4, "Plastic_Grey": 0.4, "Plastic_White": 0.5,
               "Metal_Brushed": 0.5, "Metal_Painted": 0.6, "Rubber_Black": 0.3, "Truck_Paint": 2.0,
               "Truck_Red": 1.5, "Wood_Plank": 1.2, "Tire": 0.6, "Chrome": 0.6, "Uniform_Oxblood": 0.5,
               "Suit_Torn": 0.6, "Skin_Giant": 0.4, "Skin_Pale": 0.3, "Mop": 0.3, "Rust": 0.8})


def single(name, build, folder=K.ART):
    K.reset()
    m = Mesh(name)
    build(m)
    m.build()
    K.export(name, folder)


# ---------------------------------------------------------------- salvage

def laptop(m):
    # rugged lab laptop, open ~105 degrees, keyboard deck centred
    with m.push((0, -0.2, 0.0)):
        m.box((0, 0.02, 0), (0.6, 0.04, 0.42), "Plastic_Black", 0.01)
        m.quad((0, 0.0405, 0.03), (0.5, 0.2), "Keyboard", "y")
        m.box((0, 0.041, -0.14), (0.16, 0.002, 0.08), "Plastic_Grey")
        for x in (-0.31, 0.31):
            m.box((x, 0.02, -0.18), (0.02, 0.05, 0.05), "Rubber_Black", 0.006)
            m.box((x, 0.02, 0.18), (0.02, 0.05, 0.05), "Rubber_Black", 0.006)
            m.box((x * 0.98, 0.022, 0.0), (0.008, 0.012, 0.03), "Plastic_Grey")
        with m.push((0, 0.04, 0.2), (-15, 0, 0)):
            m.box((0, 0.2, 0.012), (0.6, 0.4, 0.024), "Plastic_Black", 0.008)
            m.quad((0, 0.205, -0.0005), (0.52, 0.32), "Screen_Terminal", "-z")
            m.box((0, 0.388, -0.002), (0.02, 0.008, 0.004), "LED_Green")
        m.box((0.24, 0.042, -0.17), (0.03, 0.004, 0.012), "LED_Amber")
        m.box((0, 0.025, 0.23), (0.25, 0.03, 0.04), "Rubber_Black", 0.008)          # handle


def projector(m):
    m.box((0, 0, 0), (0.58, 0.22, 0.46), "Plastic_White", 0.035)
    m.box((0, -0.105, 0), (0.54, 0.02, 0.42), "Plastic_Grey", 0.01)
    m.cyl((-0.16, 0.0, -0.24), 0.075, 0.05, "Plastic_Black", axis="z", segs=14)
    m.cyl((-0.16, 0.0, -0.268), 0.055, 0.01, "Glass_Dirty", axis="z", segs=14)
    m.cyl((-0.16, 0.112, -0.12), 0.03, 0.012, "Plastic_Grey", segs=10)
    for i in range(9):
        m.box((0.06 + i * 0.024, 0.0, -0.232), (0.012, 0.13, 0.008), "Plastic_Black")
    for i in range(4):
        m.box((0.15 + (i % 2) * 0.05, 0.112, 0.08 + (i // 2) * 0.05), (0.03, 0.006, 0.03), "Plastic_Grey")
    m.box((0.2, 0.112, -0.05), (0.02, 0.004, 0.02), "LED_Red")
    for x in (-0.22, 0.22):
        m.cyl((x, -0.125, -0.16), 0.02, 0.02, "Rubber_Black", segs=8)
    m.box((0.18, 0.0, 0.232), (0.2, 0.12, 0.01), "Plastic_Black")
    m.tube([(0.1, -0.02, 0.24), (0.12, -0.08, 0.32), (0.3, -0.11, 0.38)], 0.008, "Rubber_Black", 5)


def microscope(m):
    with m.push((0, -0.425, 0)):
        m.box((0, 0.05, 0.02), (0.3, 0.1, 0.36), "Plastic_White", 0.02)
        m.cyl((0, 0.11, -0.04), 0.07, 0.04, "Glass_Dirty", segs=12)
        m.box((0, 0.36, 0.14), (0.08, 0.5, 0.1), "Plastic_White", 0.02, rot=(-8, 0, 0))
        m.box((0, 0.3, -0.04), (0.2, 0.02, 0.18), "Metal_Painted", 0.004)
        m.box((0, 0.315, -0.04), (0.12, 0.004, 0.05), "Glass_Dirty")
        for sx in (-1, 1):
            m.cyl((sx * 0.07, 0.3, 0.12), 0.035, 0.03, "Plastic_Black", axis="x", segs=12)
            m.cyl((sx * 0.1, 0.3, 0.12), 0.022, 0.03, "Plastic_Grey", axis="x", segs=10)
        m.box((0, 0.62, 0.05), (0.09, 0.08, 0.2), "Plastic_White", 0.02)
        m.cyl((0, 0.56, -0.04), 0.045, 0.04, "Metal_Brushed", segs=10)
        for i, a in enumerate((-30, 0, 30)):
            with m.push((0, 0.53, -0.04), (0, a, 0)):
                m.cyl((0, -0.04, -0.03), 0.012 + i * 0.002, 0.07, "Chrome", segs=8)
        for sx in (-1, 1):
            with m.push((sx * 0.035, 0.7, 0.0), (-40, 0, 0)):
                m.cyl((0, 0.06, 0), 0.02, 0.14, "Plastic_Black", segs=10)
                m.cyl((0, 0.14, 0), 0.024, 0.03, "Rubber_Black", segs=10)
        m.box((0.13, 0.08, 0.15), (0.02, 0.012, 0.03), "LED_Green")


def workstation(m):
    w, h, d = 0.49, 0.91, 0.59
    with m.push((0, -h / 2, 0)):
        m.box((0, h / 2, 0), (w, h, d), "Plastic_Black", 0.02)
        m.box((0, h / 2, -d / 2 - 0.006), (w - 0.04, h - 0.06, 0.012), "Metal_Brushed")
        for i in range(4):
            m.box((0, h - 0.12 - i * 0.07, -d / 2 - 0.014), (w - 0.12, 0.05, 0.006), "Plastic_Black")
            m.box((w / 2 - 0.09, h - 0.12 - i * 0.07, -d / 2 - 0.018), (0.02, 0.01, 0.003), "LED_Green" if i % 2 else "LED_Amber")
        for r in range(8):
            for c in range(10):
                m.box((-0.18 + c * 0.04, 0.12 + r * 0.04, -d / 2 - 0.013), (0.025, 0.025, 0.004), "Plastic_Black")
        m.box((0, h - 0.035, -d / 2 + 0.02), (0.16, 0.025, 0.02), "LED_Amber")
        m.box((0, h + 0.012, 0.0), (0.3, 0.025, 0.04), "Plastic_Grey", 0.008)       # carry handle
        for x in (-0.17, 0.17):
            m.box((x, h, 0.0), (0.03, 0.03, 0.06), "Plastic_Grey")
        for x in (-0.2, 0.2):
            for z in (-0.25, 0.25):
                m.cyl((x, -0.012, z), 0.025, 0.024, "Rubber_Black", segs=8)
        m.quad((w / 2 + 0.0005, h * 0.55, 0.05), (0.34, 0.2), "Paper", "x")          # inventory sticker


def ups(m):
    w, h, d = 0.53, 0.66, 0.61
    with m.push((0, -h / 2, 0)):
        m.box((0, h / 2, 0), (w, h, d), "Plastic_Black", 0.018)
        m.box((0, h / 2 + 0.05, -d / 2 - 0.006), (w - 0.06, h - 0.2, 0.012), "Plastic_Grey", 0.006)
        m.box((0, h - 0.18, -d / 2 - 0.014), (0.22, 0.09, 0.006), "Screen_Terminal")
        for i, mat in enumerate(("LED_Green", "LED_Amber", "LED_Red")):
            m.box((-0.12 + i * 0.12, h - 0.28, -d / 2 - 0.014), (0.03, 0.015, 0.004), mat)
        m.cyl((0.0, 0.18, -d / 2 - 0.015), 0.03, 0.012, "Plastic_Red", axis="z", segs=12)
        m.quad((0, 0.36, -d / 2 - 0.0135), (0.3, 0.07), "Caution_Tape", "-z")
        for sx in (-1, 1):
            m.box((sx * (w / 2 + 0.02), h * 0.6, 0), (0.03, 0.06, 0.3), "Plastic_Grey", 0.01)
        for r in range(6):
            m.box((0, 0.08 + r * 0.06, d / 2 + 0.004), (w - 0.1, 0.02, 0.008), "Plastic_Grey")
        for x in (-0.2, 0.2):
            for z in (-0.22, 0.22):
                m.cyl((x, -0.01, z), 0.03, 0.02, "Rubber_Black", segs=8)


def printer(m):
    w, h, d = 0.77, 0.67, 0.78
    with m.push((0, -h / 2, 0)):
        m.box((0, h * 0.42, 0), (w, h * 0.84, d), "Plastic_Beige", 0.03)
        m.box((0, h - 0.06, 0.02), (w - 0.02, 0.1, d - 0.04), "Plastic_Grey", 0.015)
        m.box((0, h + 0.002, 0.02), (w - 0.14, 0.006, d - 0.2), "Glass_Dirty")
        m.box((w / 2 - 0.12, h - 0.02, -d / 2 + 0.08), (0.2, 0.06, 0.12), "Plastic_Grey", 0.01, rot=(-20, 0, 0))
        m.box((w / 2 - 0.12, h - 0.01, -d / 2 + 0.03), (0.14, 0.05, 0.004), "Screen_BSOD", rot=(-20, 0, 0))
        for i in range(3):
            m.box((0, 0.08 + i * 0.13, -d / 2 - 0.006), (w - 0.06, 0.1, 0.012), "Plastic_Beige", 0.006)
            m.box((0, 0.11 + i * 0.13, -d / 2 - 0.014), (0.22, 0.02, 0.008), "Plastic_Black")
        m.box((0, 0.48, -d / 2 - 0.12), (0.46, 0.012, 0.24), "Plastic_Grey")
        m.quad((0, 0.487, -d / 2 - 0.12), (0.4, 0.22), "Paper", "y")
        m.quad((0.05, 0.45, -d / 2 - 0.3), (0.21, 0.29), "Paper", "y", rot=(30, 10, 0))
        m.box((-w / 2 + 0.03, 0.3, 0.1), (0.01, 0.2, 0.3), "Plastic_Grey")


def cart(m):
    # maintenance cart: deck at origin height (collider 1.3 x .12 x 1.65), handle at +Z
    m.box((0, 0, 0), (1.3, 0.1, 1.65), "Plastic_Red", 0.02)
    m.box((0, 0.055, 0), (1.18, 0.01, 1.52), "Rubber_Black")
    for sx in (-1, 1):
        m.box((sx * 0.63, 0.2, 0), (0.05, 0.36, 1.6), "Metal_Painted", 0.006)
        for i in range(5):
            m.box((sx * 0.632, 0.08 + i * 0.07, 0), (0.052, 0.012, 1.5), "Rust" if i == 2 else "Metal_Painted")
        for sz in (-1, 1):
            z = sz * 0.59
            m.box((sx * 0.55, -0.1, z), (0.06, 0.1, 0.06), "Metal_Painted")
            m.cyl((sx * 0.55, -0.2, z), 0.11, 0.06, "Rubber_Black", axis="x", segs=12)
            m.cyl((sx * 0.55, -0.2, z), 0.05, 0.07, "Metal_Brushed", axis="x", segs=8)
        m.tube([(sx * 0.6, 0.0, 0.8), (sx * 0.6, 0.6, 0.82), (sx * 0.6, 1.0, 0.86)], 0.022, "Metal_Brushed", 8, smooth=True)
    m.tube([(-0.6, 1.0, 0.86), (0.6, 1.0, 0.86)], 0.025, "Rubber_Black", 8)
    m.box((0.3, 0.12, -0.3), (0.4, 0.15, 0.35), "Cardboard", 0.01, rot=(0, 12, 0))
    m.quad((-0.2, 0.065, 0.3), (0.3, 0.4), "Paper", "y", rot=(0, 30, 0))


def oscilloscope(m):
    w, h, d = 0.42, 0.24, 0.36
    m.box((0, 0, 0), (w, h, d), "Plastic_Grey", 0.015)
    m.box((0, 0, -d / 2 - 0.004), (w - 0.02, h - 0.02, 0.008), "Plastic_Black")
    m.quad((-0.07, 0.02, -d / 2 - 0.0085), (0.2, 0.15), "Screen_Static", "-z")
    for r in range(3):
        for c in range(3):
            m.cyl((0.08 + c * 0.04, 0.06 - r * 0.045, -d / 2 - 0.015), 0.012, 0.015, "Plastic_Black", axis="z", segs=8)
    for c in range(4):
        m.cyl((-0.15 + c * 0.06, -0.09, -d / 2 - 0.012), 0.01, 0.012, "Chrome", axis="z", segs=8)
    m.box((0, h / 2 + 0.012, 0), (0.24, 0.02, 0.03), "Plastic_Black", 0.006)
    m.tube([(-0.15, -0.09, -d / 2 - 0.02), (-0.2, -0.11, -d / 2 - 0.12), (-0.05, -0.12, -d / 2 - 0.2)], 0.004, "Plastic_Red", 4)


def router(m):
    w, h, d = 0.44, 0.07, 0.32
    m.box((0, 0, 0), (w, h, d), "Metal_Painted", 0.008)
    m.box((0, 0, -d / 2 - 0.003), (w - 0.02, h - 0.016, 0.006), "Plastic_Black")
    for p in range(12):
        x = -0.18 + p * 0.03
        m.box((x, -0.005, -d / 2 - 0.007), (0.02, 0.02, 0.004), "Plastic_Grey")
        m.box((x, 0.017, -d / 2 - 0.007), (0.006, 0.006, 0.003), "LED_Green" if p % 4 else "LED_Amber")
    for x in (-0.2, 0.2):
        m.box((x * 1.12, 0, -d / 2 + 0.01), (0.03, h, 0.02), "Metal_Brushed")
    m.quad((0.1, h / 2 + 0.0005, 0.05), (0.15, 0.08), "Paper", "y")


# ---------------------------------------------------------------- truck

def truck(m):
    # chassis
    for x in (-0.55, 0.55):
        m.box((x, 0.5, 0.2), (0.12, 0.2, 7.2), "Metal_Painted")
    for z in (-2.8, -1.2, 0.4, 2.0):
        m.box((0, 0.5, z), (1.2, 0.12, 0.1), "Metal_Painted")
    # cargo box
    m.box((0, 0.75, -0.8), (2.7, 0.18, 4.7), "Metal_Painted", 0.01)
    m.box((0, 0.851, -0.8), (2.62, 0.02, 4.6), "Wood_Plank")
    for sx in (-1, 1):
        m.box((sx * 1.42, 1.91, -0.8), (0.16, 2.42, 4.7), "Truck_Paint", 0.02)
        m.box((sx * 1.505, 1.33, -0.8), (0.012, 0.22, 4.72), "Truck_Red")
        m.box((sx * 1.505, 2.95, -0.8), (0.012, 0.08, 4.72), "Truck_Red")
        for z in range(9):
            m.box((sx * 1.508, 1.91, -2.95 + z * 0.54), (0.02, 2.3, 0.04), "Truck_Paint")
        for y in (1.25, 1.75, 2.3):
            m.box((sx * 1.33, y, -0.8), (0.035, 0.06, 4.4), "Metal_Brushed")
        m.box((sx * 1.3, 0.62, -0.8), (0.08, 0.1, 4.6), "Rust")
        m.box((sx * 1.505, 2.9, -3.05), (0.03, 0.1, 0.1), "LED_Amber")
        m.box((sx * 1.505, 2.9, 1.4), (0.03, 0.1, 0.1), "LED_Amber")
        # rear frame + roll-up door rolled at the top
        m.box((sx * 1.36, 1.91, -3.1), (0.1, 2.42, 0.1), "Metal_Brushed")
        m.box((sx * 1.2, 0.95, -3.18), (0.3, 0.2, 0.08), "LED_Red")
    m.box((0, 3.14, -0.8), (3.0, 0.15, 4.7), "Truck_Paint", 0.01)
    m.cyl((0, 2.95, -3.0), 0.14, 2.6, "Metal_Painted", axis="x", segs=10)
    m.box((0, 3.02, -3.12), (2.8, 0.11, 0.1), "Metal_Brushed")
    m.box((0, 0.73, -3.18), (2.7, 0.08, 0.1), "Metal_Brushed")
    m.box((0, 1.9, 1.53), (2.7, 2.4, 0.15), "Truck_Paint")
    m.box((0, 0.42, -3.25), (2.2, 0.08, 0.12), "Metal_Painted")                       # rear bumper / step
    m.box((0, 0.6, -3.25), (0.6, 0.03, 0.2), "Metal_Brushed")
    # wheels with rims and arches
    for sx in (-1, 1):
        for z in (-1.95, 2.35):
            m.cyl((sx * 1.2, 0.45, z), 0.45, 0.32, "Tire", axis="x", segs=16)
            m.cyl((sx * 1.37, 0.45, z), 0.26, 0.02, "Metal_Brushed", axis="x", segs=12)
            m.cyl((sx * 1.385, 0.45, z), 0.08, 0.02, "Rust", axis="x", segs=8)
            for k in range(6):
                a = k * math.pi / 3
                m.cyl((sx * 1.39, 0.45 + math.sin(a) * 0.17, z + math.cos(a) * 0.17), 0.018, 0.02, "Chrome", axis="x", segs=6)
            m.box((sx * 1.27, 0.98, z), (0.45, 0.06, 1.15), "Metal_Painted")
        m.cyl((sx * 0.9, 0.62, 0.6), 0.22, 0.9, "Metal_Brushed", axis="z", segs=12)      # fuel tank / toolbox
        m.box((sx * 1.25, 0.5, 1.6), (0.3, 0.05, 0.4), "Metal_Brushed")                 # cab step
    # cab
    m.box((0, 1.15, 2.72), (2.64, 1.5, 2.05), "Truck_Paint", 0.06)
    m.box((0, 2.12, 2.55), (2.5, 0.6, 1.7), "Truck_Paint", 0.06, taper=(0.98, 0.85))
    m.box((0, 1.9, 3.56), (2.2, 0.62, 0.05), "Glass_Dirty", rot=(-12, 0, 0))
    m.box((0, 1.9, 3.6), (2.3, 0.7, 0.03), "Plastic_Black", rot=(-12, 0, 0))
    for sx in (-1, 1):
        m.box((sx * 1.325, 1.95, 2.55), (0.02, 0.55, 1.1), "Glass_Dirty")
        m.box((sx * 1.33, 1.25, 2.3), (0.02, 0.9, 1.0), "Truck_Paint")
        m.box((sx * 1.345, 1.32, 2.0), (0.04, 0.05, 0.18), "Chrome")
        m.tube([(sx * 1.3, 2.1, 3.3), (sx * 1.55, 2.1, 3.35)], 0.015, "Metal_Painted", 5)
        m.box((sx * 1.6, 1.95, 3.35), (0.08, 0.32, 0.18), "Plastic_Black", 0.02)
        m.box((sx * 0.95, 1.0, 3.76), (0.4, 0.24, 0.06), "Plastic_White", 0.01)
        m.quad((sx * 0.95, 1.0, 3.7905), (0.34, 0.18), "Eye_Glow", "z")
        m.box((sx * 1.2, 0.72, 3.8), (0.15, 0.08, 0.05), "LED_Amber")
    m.box((0, 0.95, 3.77), (1.3, 0.35, 0.03), "Plastic_Black")
    for i in range(8):
        m.box((-0.56 + i * 0.16, 0.95, 3.79), (0.03, 0.3, 0.015), "Chrome")
    m.box((0, 0.56, 3.83), (2.8, 0.18, 0.2), "Metal_Brushed", 0.02)
    m.box((0, 0.62, 3.935), (0.45, 0.13, 0.01), "Paper")
    m.tube([(0.85, 0.35, -0.2), (0.85, 0.3, -1.5), (0.95, 0.3, -2.6)], 0.045, "Rust", 6)    # exhaust
    m.box((0, 2.45, 2.6), (1.4, 0.06, 0.25), "LED_Amber")                                    # roof marker


# ---------------------------------------------------------------- segmented characters

def ellipse(cx, y, cz, rx, rz, segs=10, rot=0.0):
    pts = []
    for i in range(segs):
        a = 2 * math.pi * i / segs + rot
        pts.append((cx + math.cos(a) * rx, y, cz + math.sin(a) * rz))
    return pts


def limb(name, joint, length, r0, r1, mat, segs=8, extra=None, bend=0.0):
    """Limb hanging down (-Y) from its joint. extra(m) adds details in joint-local space."""
    m = Mesh(name)
    with m.push(joint):
        rings = []
        for k in range(5):
            t = k / 4
            r = r0 + (r1 - r0) * t
            bulge = math.sin(t * math.pi) * 0.12 * r0
            rings.append(ellipse(0, -length * t, bend * math.sin(t * math.pi), r + bulge, (r + bulge) * 0.9, segs))
        m.loft([[p for p in ring] for ring in rings], mat)
        if extra:
            extra(m)
    return m.build(origin=joint)


def hand(name, joint, scale, skin, finger_len, side):
    m = Mesh(name)
    with m.push(joint, (0, 0, 0), scale):
        m.box((0, -0.05, 0), (0.06, 0.1, 0.09), skin, 0.015)
        for f in range(4):
            z = -0.03 + f * 0.02
            ln = finger_len * (1.0 if f in (1, 2) else 0.82)
            m.tube([(0, -0.09, z), (0.004, -0.09 - ln * 0.5, z - 0.004), (0.015 * side * 0, -0.09 - ln, z - 0.012)],
                   [0.009, 0.008, 0.006], skin, 5)
        m.tube([(0, -0.04, -0.045), (0.0, -0.08, -0.07), (0.0, -0.08 - finger_len * 0.45, -0.075)], [0.01, 0.009, 0.007], skin, 5)
    return m.build(origin=joint)


def character(name, p):
    """Segment set for a humanoid. p: dict of proportions/materials. Faces +Z."""
    K.reset()
    hip = p["hip"]
    sh_y = p["shoulder_y"]
    parts = []
    # pelvis + torso
    m = Mesh("Pelvis")
    with m.push((0, hip, 0)):
        rings = [ellipse(0, -0.08, 0, p["hip_w"] * 0.95, p["depth"] * 0.85), ellipse(0, 0.05, 0, p["hip_w"], p["depth"]),
                 ellipse(0, 0.14, 0, p["waist_w"], p["depth"] * 0.9)]
        m.loft([[q for q in r] for r in rings], p["pants"])
        if p.get("pelvis_extra"):
            p["pelvis_extra"](m)
    parts.append(m.build(origin=(0, hip, 0)))
    spine_y = hip + 0.14
    m = Mesh("Spine")
    with m.push((0, spine_y, 0)):
        h = (sh_y - spine_y) * 0.45
        rings = [ellipse(0, 0, 0, p["waist_w"], p["depth"] * 0.9), ellipse(0, h, 0.0, p["waist_w"] * 1.05, p["depth"])]
        m.loft([[q for q in r] for r in rings], p["torso"])
        if p.get("belt"):
            m.loft([[q for q in ellipse(0, y, 0, p["waist_w"] * 1.04, p["depth"] * 0.94)] for y in (0.0, 0.05)], p["belt"])
    parts.append(m.build(origin=(0, spine_y, 0)))
    chest_y = spine_y + (sh_y - spine_y) * 0.45
    m = Mesh("Chest")
    with m.push((0, chest_y, 0)):
        top = sh_y - chest_y
        rings = [ellipse(0, 0, 0, p["waist_w"] * 1.05, p["depth"]), ellipse(0, top * 0.55, 0.01, p["chest_w"], p["depth"] * 1.1),
                 ellipse(0, top, 0, p["chest_w"] * 1.02, p["depth"] * 0.9), ellipse(0, top + 0.06, 0, p["neck_r"] * 1.6, p["neck_r"] * 1.4)]
        m.loft([[q for q in r] for r in rings], p["torso"])
        if p.get("chest_extra"):
            p["chest_extra"](m, top)
    parts.append(m.build(origin=(0, chest_y, 0)))
    neck_y = sh_y + 0.05
    parts.append(limb("Neck", (0, neck_y, 0), -p["neck_len"], p["neck_r"], p["neck_r"] * 0.9, p["skin"]))
    head_y = neck_y + p["neck_len"]
    m = Mesh("Head")
    with m.push((0, head_y, 0)):
        p["head"](m)
    parts.append(m.build(origin=(0, head_y, 0)))
    for side, sx in (("L", -1), ("R", 1)):
        sxw = sx * p["chest_w"] * 1.02
        parts.append(limb("UpperArm_" + side, (sxw, sh_y, 0), p["upper_arm"], p["arm_r"], p["arm_r"] * 0.8, p["sleeve"]))
        parts.append(limb("LowerArm_" + side, (sxw, sh_y - p["upper_arm"], 0), p["lower_arm"], p["arm_r"] * 0.8,
                          p["arm_r"] * 0.6, p.get("forearm", p["sleeve"])))
        parts.append(hand("Hand_" + side, (sxw, sh_y - p["upper_arm"] - p["lower_arm"], 0), p["hand_scale"], p["skin"],
                          p["finger"], sx))
        hx = sx * p["hip_w"] * 0.55
        parts.append(limb("UpperLeg_" + side, (hx, hip - 0.04, 0), p["upper_leg"], p["leg_r"], p["leg_r"] * 0.75, p["pants"]))
        parts.append(limb("LowerLeg_" + side, (hx, hip - 0.04 - p["upper_leg"], 0), p["lower_leg"], p["leg_r"] * 0.75,
                          p["leg_r"] * 0.5, p["pants"]))
        ank = (hx, hip - 0.04 - p["upper_leg"] - p["lower_leg"], 0)
        m = Mesh("Foot_" + side)
        with m.push(ank):
            fl = p["foot"]
            g = -ank[1]
            m.box((0, g + 0.06, fl * 0.22), (p["leg_r"] * 1.7, 0.1, fl * 0.85), p["shoe"], 0.02, taper=(0.9, 0.8))
            m.sphere((0, g + 0.045, fl * 0.62), (p["leg_r"] * 0.85, 0.045, fl * 0.2), p["shoe"], 8, 4)
            m.box((0, g + 0.012, fl * 0.3), (p["leg_r"] * 1.8, 0.024, fl * 1.05), "Rubber_Black", 0.006)
            m.cyl((0, g + 0.09, 0), p["leg_r"] * 0.62, 0.08, p["shoe"], segs=8)
        parts.append(m.build(origin=ank))
    if p.get("props"):
        p["props"](parts)
    K.export(name)


def caretaker_head(m):
    m.loft([[q for q in ellipse(0, y, 0.01, rx, rz, 12)] for y, rx, rz in
            ((0.0, 0.07, 0.07), (0.06, 0.1, 0.11), (0.14, 0.105, 0.12), (0.22, 0.095, 0.11), (0.27, 0.06, 0.07))], "Skin_Pale")
    for sx in (-1, 1):
        m.box((sx * 0.04, 0.15, 0.11), (0.035, 0.02, 0.012), "LED_Red")
        m.box((sx * 0.04, 0.15, 0.105), (0.05, 0.035, 0.012), "Plastic_Black")
        m.box((sx * 0.105, 0.14, 0.0), (0.015, 0.05, 0.035), "Skin_Pale")
    m.box((0, 0.11, 0.125), (0.025, 0.04, 0.03), "Skin_Pale")
    m.box((0, 0.065, 0.11), (0.07, 0.012, 0.012), "Plastic_Black")
    # cap
    m.cyl((0, 0.27, 0.0), 0.11, 0.07, "Uniform_Navy", segs=12, r2=0.1)
    m.box((0, 0.24, 0.13), (0.18, 0.012, 0.1), "Uniform_Navy", rot=(-10, 0, 0))
    m.box((0, 0.29, 0.11), (0.05, 0.03, 0.005), "LED_Amber")


def caretaker_chest(m, top):
    m.box((0.08, top * 0.6, 0.15), (0.06, 0.08, 0.012), "Paper")                         # id badge
    m.box((-0.09, top * 0.55, 0.15), (0.09, 0.09, 0.012), "Uniform_Navy")
    m.box((0.12, top * 0.85, 0.07), (0.05, 0.08, 0.04), "Plastic_Black")                 # radio
    m.cyl((0.12, top * 0.85 + 0.08, 0.07), 0.006, 0.08, "Plastic_Black", segs=5)


def caretaker_props(parts):
    m = Mesh("Mop")
    joint = (0.25, 0.68, 0.0)
    with m.push(joint, (12, 0, 0)):
        m.cyl((0, -0.1, 0), 0.016, 1.6, "Laminate_Wood", segs=6)
        m.box((0, -0.92, 0), (0.32, 0.05, 0.08), "Plastic_Grey")
        for i in range(10):
            m.tube([(-0.14 + i * 0.03, -0.94, 0), (-0.15 + i * 0.033, -1.1, 0.02), (-0.16 + i * 0.036, -1.18, 0.06)],
                   0.012, "Mop", 4)
    parts.append(m.build(origin=joint))


def giant_head(m):
    # elongated, bald, hollow-eyed; eyes are pinpricks of light
    m.loft([[q for q in ellipse(0, y, z, rx, rz, 14)] for y, rx, rz, z in
            ((0.0, 0.06, 0.06, 0.0), (0.08, 0.1, 0.11, 0.02), (0.2, 0.115, 0.13, 0.02), (0.34, 0.11, 0.13, 0.0),
             (0.44, 0.08, 0.1, -0.02), (0.48, 0.03, 0.04, -0.03))], "Skin_Giant")
    for sx in (-1, 1):
        m.sphere((sx * 0.045, 0.27, 0.115), (0.03, 0.022, 0.015), "Plastic_Black", 8, 5)
        m.sphere((sx * 0.045, 0.27, 0.124), 0.005, "Eye_Glow", 6, 4)
        m.box((sx * 0.11, 0.25, 0.0), (0.012, 0.06, 0.03), "Skin_Giant")
    m.box((0, 0.21, 0.135), (0.02, 0.05, 0.02), "Skin_Giant")
    m.box((0, 0.12, 0.125), (0.13, 0.03, 0.02), "Plastic_Black")                          # stretched mouth
    for i in range(9):
        m.box((-0.056 + i * 0.014, 0.128, 0.134), (0.008, 0.012, 0.006), "Plastic_White")
    m.box((0, 0.105, 0.12), (0.11, 0.008, 0.02), "Blood_Dry")


def giant_tails(m):
    r = random.Random(5)
    for i in range(7):
        a = math.radians(-150 + i * 20)
        x, z = math.sin(a) * 0.24, math.cos(a) * 0.17
        ln = r.uniform(0.45, 0.9)
        m.box((x, 0.1 - ln / 2, z), (0.09, ln, 0.012), "Suit_Torn", rot=(r.uniform(-8, 8), math.degrees(a), r.uniform(-6, 6)))


def giant_chest(m, top):
    m.box((0, top * 0.75, 0.2), (0.07, 0.4, 0.012), "Plastic_Red", rot=(4, 0, 0))       # tie
    m.box((0, top * 0.95, 0.16), (0.22, 0.12, 0.05), "Shirt_White", rot=(25, 0, 0))     # collar
    m.tube([(-0.08, top + 0.02, 0.1), (-0.05, top * 0.6, 0.22), (0.0, top * 0.35, 0.23)], 0.004, "Plastic_Black", 4)
    m.box((0.0, top * 0.32, 0.235), (0.08, 0.11, 0.008), "Paper")                        # ID card on lanyard
    for i in range(3):
        m.cyl((0.0, top * 0.1 + i * 0.12, 0.215), 0.012, 0.008, "Plastic_Black", axis="z", segs=6)


character("Caretaker", dict(
    hip=0.95, shoulder_y=1.45, hip_w=0.17, waist_w=0.15, chest_w=0.2, depth=0.12, neck_r=0.055, neck_len=0.1,
    upper_arm=0.3, lower_arm=0.27, arm_r=0.05, upper_leg=0.44, lower_leg=0.42, leg_r=0.075, foot=0.26,
    hand_scale=1.0, finger=0.07, skin="Skin_Pale", torso="Uniform_Oxblood", sleeve="Uniform_Oxblood", pants="Uniform_Navy",
    shoe="Rubber_Black", belt="Plastic_Black", head=caretaker_head, chest_extra=caretaker_chest, props=caretaker_props))

character("Giant", dict(
    hip=1.8, shoulder_y=2.95, hip_w=0.22, waist_w=0.17, chest_w=0.27, depth=0.15, neck_r=0.06, neck_len=0.32,
    upper_arm=0.98, lower_arm=0.95, arm_r=0.06, forearm="Skin_Giant", upper_leg=0.92, lower_leg=0.86, leg_r=0.085, foot=0.36,
    hand_scale=1.6, finger=0.17, skin="Skin_Giant", torso="Suit_Torn", sleeve="Suit_Torn", pants="Suit_Torn",
    shoe="Plastic_Black", belt="Plastic_Black", head=giant_head, chest_extra=giant_chest, pelvis_extra=giant_tails))

for name, fn in [("Laptop", laptop), ("Projector", projector), ("Microscope", microscope), ("Workstation", workstation),
                 ("UPS", ups), ("Printer", printer), ("Cart", cart), ("Oscilloscope", oscilloscope), ("Router", router),
                 ("Truck", truck)]:
    single(name, fn)
