"""Pixel textures for the remake (64-128 px, point sampled in Unity through PSX/Lit).

Run: blender -b --factory-startup --python Tools/blender/gen_remake_textures.py
Writes Assets/_Project/Art/Remake/Textures/<Name>.png. Names double as material names.
"""
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import importlib  # noqa: E402

import numpy as np  # noqa: E402

import remake_kit as K  # noqa: E402

importlib.reload(K)

S = 64
rng = np.random.default_rng(7)


def save(name, img):
    K.write_png(os.path.join(K.TEX, name + ".png"), img)
    print("[texture]", name)


def grime(img, seed, amount=0.25, cells=6):
    n = K.fbm(img.shape[0], seed, 4, cells)
    return img * (1 - amount * np.clip(n * 1.6 - 0.5, 0, 1)[..., None])


def speckle(img, seed, amount=0.06):
    r = np.random.default_rng(seed).random(img.shape[:2])
    return img * (1 - amount + amount * 2 * r[..., None])


def scratches(img, seed, count=14, color=(1, 1, 1), alpha=0.25):
    r = np.random.default_rng(seed)
    h, w = img.shape[:2]
    for _ in range(count):
        x, y = r.integers(0, w), r.integers(0, h)
        dx, dy = r.uniform(-1, 1), r.uniform(-0.4, 0.4)
        for t in range(r.integers(4, 16)):
            px, py = int(x + dx * t) % w, int(y + dy * t) % h
            img[py, px] = img[py, px] * (1 - alpha) + np.array(color) * alpha
    return img


def solid(color, seed, var=0.08, grime_amt=0.25):
    n = K.fbm(S, seed, 4, 4)
    img = K.colorize(n, [c * (1 - var) for c in color], [min(1, c * (1 + var)) for c in color])
    return speckle(grime(img, seed + 50, grime_amt), seed + 9, 0.05)


# ---------------------------------------------------------------- plastics / metals / wood
save("Plastic_Beige", scratches(solid((0.74, 0.68, 0.55), 1, 0.07, 0.35), 2, 10, (0.5, 0.45, 0.35), 0.3))
save("Plastic_Black", solid((0.07, 0.075, 0.08), 3, 0.25, 0.1))
save("Plastic_Grey", solid((0.42, 0.43, 0.44), 4, 0.08, 0.25))
save("Plastic_White", solid((0.78, 0.79, 0.77), 5, 0.05, 0.3))
save("Plastic_Red", solid((0.55, 0.08, 0.06), 6, 0.1, 0.2))

brushed = np.repeat(K.value_noise(S, 32, 11)[:, :1], S, axis=1) * 0.5 + K.fbm(S, 12, 3, 8) * 0.5
save("Metal_Brushed", speckle(K.colorize(brushed, (0.35, 0.37, 0.39), (0.62, 0.64, 0.66)), 13, 0.04))
paint = solid((0.36, 0.38, 0.4), 14, 0.06, 0.3)
chips = K.fbm(S, 15, 4, 8) > 0.68
paint[chips] = paint[chips] * 0.45 + np.array([0.24, 0.13, 0.07]) * 0.55
save("Metal_Painted", paint)
rust = K.colorize(K.fbm(S, 16, 5, 4), (0.18, 0.07, 0.03), (0.55, 0.27, 0.1))
save("Rust", speckle(rust, 17, 0.15))
save("Chrome", K.colorize(np.repeat(np.linspace(0, 1, S)[:, None], S, 1) ** 0.6 * 0.6 + K.fbm(S, 18, 2, 2) * 0.4,
                          (0.25, 0.26, 0.28), (0.85, 0.86, 0.88)))

y, x = np.mgrid[0:S, 0:S]
grain = np.sin(y * 0.55 + K.fbm(S, 19, 3, 4) * 9) * 0.5 + 0.5
save("Laminate_Wood", scratches(grime(K.colorize(grain * 0.4 + K.fbm(S, 20, 3, 8) * 0.6, (0.42, 0.29, 0.17), (0.66, 0.48, 0.3)), 21, 0.3), 22, 12))
save("Laminate_White", scratches(grime(solid((0.82, 0.81, 0.77), 23, 0.03, 0.0), 24, 0.12, 3), 25, 10, (0.55, 0.55, 0.52), 0.25))
plank = K.colorize(np.sin(x * 0.3 + K.fbm(S, 26, 3, 4) * 6) * 0.25 + 0.5 + K.fbm(S, 27, 3, 8) * 0.25, (0.25, 0.15, 0.08), (0.52, 0.36, 0.2))
plank[(y % 16) == 0] *= 0.35
save("Wood_Plank", grime(plank, 28, 0.35))
save("Cardboard", speckle(K.colorize(K.fbm(S, 29, 3, 6), (0.45, 0.33, 0.2), (0.62, 0.48, 0.3)), 30, 0.08))

weave = ((x + y) % 4 < 2) * 0.25 + ((x - y) % 4 < 2) * 0.25 + K.fbm(S, 31, 3, 8) * 0.5
save("Fabric_Blue", grime(K.colorize(weave, (0.06, 0.1, 0.2), (0.17, 0.25, 0.42)), 32, 0.3))
save("Fabric_Black", grime(K.colorize(weave, (0.03, 0.03, 0.035), (0.12, 0.12, 0.13)), 33, 0.2))
save("Rubber_Black", solid((0.05, 0.05, 0.05), 34, 0.3, 0.05))
save("Cable_Grey", solid((0.32, 0.33, 0.34), 35, 0.1, 0.1))
concrete = K.colorize(K.fbm(S, 36, 5, 4), (0.32, 0.31, 0.29), (0.55, 0.54, 0.5))
save("Concrete_Dirty", speckle(grime(concrete, 37, 0.45, 3), 38, 0.12))
save("Glass_Dirty", grime(K.colorize(K.fbm(S, 39, 3, 3), (0.07, 0.13, 0.15), (0.16, 0.25, 0.27)), 40, 0.4))

# ---------------------------------------------------------------- electronics
keys = np.full((S, S, 3), 0.18)
for ky in range(5):
    for kx in range(8):
        keys[3 + ky * 12:12 + ky * 12, 2 + kx * 8:8 + kx * 8] = (0.76, 0.72, 0.62)
        keys[4 + ky * 12, 3 + kx * 8:7 + kx * 8] = (0.86, 0.83, 0.74)
save("Keyboard", grime(keys, 41, 0.35))

pcb = K.colorize(K.fbm(S, 42, 3, 8), (0.02, 0.22, 0.08), (0.05, 0.36, 0.14))
for i in range(18):
    yy = rng.integers(0, S)
    pcb[yy, rng.integers(0, 20):rng.integers(30, S)] = (0.75, 0.62, 0.25)
    xx = rng.integers(0, S)
    pcb[rng.integers(0, 20):rng.integers(30, S), xx] = (0.75, 0.62, 0.25)
for i in range(6):
    cx, cy = rng.integers(4, S - 16), rng.integers(4, S - 12)
    pcb[cy:cy + 8, cx:cx + 12] = (0.05, 0.05, 0.06)
    pcb[cy - 1, cx:cx + 12:2] = (0.8, 0.8, 0.8)
save("PCB", pcb)

dead = K.colorize(K.fbm(S, 43, 2, 2), (0.015, 0.025, 0.035), (0.05, 0.07, 0.09))
streak = np.clip(1 - np.abs((x - y * 0.8) - 20) / 6, 0, 1) * 0.12
save("Screen_Dead", dead + streak[..., None])
cracked = (dead + streak[..., None]).copy()
cx, cy = 40, 22
for k in range(14):
    ang = rng.uniform(0, 2 * np.pi)
    px, py = float(cx), float(cy)
    for t in range(rng.integers(12, 40)):
        ang += rng.uniform(-0.35, 0.35)
        px += np.cos(ang); py += np.sin(ang)
        if 0 <= int(px) < S and 0 <= int(py) < S:
            cracked[int(py), int(px)] = (0.65, 0.72, 0.75)
for r in (4, 9):
    for a in np.linspace(0, 2 * np.pi, 40):
        px, py = int(cx + np.cos(a) * r), int(cy + np.sin(a) * r * 0.8)
        if 0 <= px < S and 0 <= py < S:
            cracked[py, px] = (0.5, 0.56, 0.6)
save("Screen_Cracked", cracked)

bsod = np.zeros((S, S, 3)); bsod[:] = (0.02, 0.12, 0.62)
K.draw_text(bsod, ":(", 4, 5, (0.9, 0.92, 1), 1)
for i, line in enumerate(["ERROR", "FATAL", "0X0000", "UTEZ", "REINICIE"]):
    K.draw_text(bsod, line, 4, 16 + i * 9, (0.85, 0.88, 1), 1)
save("Screen_BSOD", bsod)

term = np.zeros((S, S, 3)); term[:] = (0.0, 0.02, 0.0)
lines = ["C:\\>DIR", "NO HAY", "SALIDA", "> AYUDA", "> ?????", "CORRE", "_"]
for i, line in enumerate(lines):
    K.draw_text(term, line.replace("\\", "/"), 2, 2 + i * 9, (0.25, 0.95, 0.35), 1, 0.3, i)
term[::2] *= 0.75
save("Screen_Terminal", term)
static = np.random.default_rng(44).random((S, S))[..., None] * np.array([0.8, 0.85, 0.9])
static[::2] *= 0.6
save("Screen_Static", static)

for name, col in (("LED_Green", (0.2, 1, 0.35)), ("LED_Red", (1, 0.12, 0.08)), ("LED_Amber", (1, 0.6, 0.1)), ("Eye_Glow", (1, 0.85, 0.6))):
    save(name, np.ones((8, 8, 3)) * np.array(col))

# ---------------------------------------------------------------- boards, paper, signage
W = 128
board = K.colorize(K.fbm(W, 45, 3, 4), (0.82, 0.84, 0.82), (0.93, 0.94, 0.92))
board = grime(board, 46, 0.25, 5)
smear = K.fbm(W, 47, 3, 3)
board[smear > 0.62] = board[smear > 0.62] * 0.85 + np.array([0.55, 0.6, 0.62]) * 0.15
K.draw_text(board, "EXAMEN FINAL", 6, 8, (0.08, 0.12, 0.45), 1, 0.3, 1)
K.draw_text(board, "LUNES 7:00", 6, 18, (0.08, 0.12, 0.45), 1, 0.3, 5)
K.draw_text(board, "NO ESTAN", 14, 36, (0.6, 0.05, 0.04), 2, 0.8, 2)
K.draw_text(board, "SOLOS", 34, 54, (0.6, 0.05, 0.04), 2, 0.8, 6)
K.draw_text(board, "CC9 > 22:00", 50, 80, (0.1, 0.35, 0.12), 1, 0.4, 3)
K.draw_text(board, "SALGAN", 10, 96, (0.6, 0.05, 0.04), 3, 1.2, 4)
save("Whiteboard", board)

paper = K.colorize(K.fbm(S, 48, 2, 4), (0.78, 0.76, 0.68), (0.9, 0.88, 0.8))
for i in range(9):
    paper[6 + i * 6, 6:6 + rng.integers(30, 52)] = (0.35, 0.35, 0.38)
save("Paper", grime(paper, 49, 0.3))

poster = np.zeros((W, W, 3)); poster[:] = (0.86, 0.85, 0.8)
poster[:22] = (0.1, 0.28, 0.2)
K.draw_text(poster, "UTEZ", 40, 4, (0.95, 0.95, 0.9), 2)
K.draw_text(poster, "REGLAMENTO", 8, 28, (0.1, 0.1, 0.12), 2)
for i, line in enumerate(["1 NO COMER", "2 NO BEBER", "3 APAGAR PC", "4 NO CORRER", "5 NO MIRAR", "  ATRAS"]):
    K.draw_text(poster, line, 8, 50 + i * 12, (0.15, 0.15, 0.18), 1, 0.2, i)
poster = grime(poster, 50, 0.4, 4)
save("Poster_Rules", poster)

evac = np.zeros((S, S, 3)); evac[:] = (0.05, 0.5, 0.2)
K.draw_text(evac, "SALIDA", 14, 10, (0.95, 0.98, 0.95), 1)
evac[30:44, 14:50] = (0.95, 0.98, 0.95)
evac[26:48, 40:44] = (0.95, 0.98, 0.95)
save("Sign_Exit", evac)

tape = np.zeros((16, S, 3)); tape[:] = (0.95, 0.78, 0.05)
for i in range(0, S, 12):
    for yy in range(16):
        tape[yy, (i + yy) % S:(i + yy + 5) % S or S] = (0.05, 0.05, 0.05)
save("Caution_Tape", np.concatenate([tape] * 4, axis=0))

# ---------------------------------------------------------------- characters and vehicle
save("Uniform_Oxblood", grime(K.colorize(weave, (0.22, 0.03, 0.025), (0.4, 0.06, 0.04)), 51, 0.35))
save("Uniform_Navy", grime(K.colorize(weave, (0.03, 0.05, 0.11), (0.08, 0.11, 0.22)), 52, 0.3))
save("Skin_Pale", grime(K.colorize(K.fbm(S, 53, 4, 4), (0.55, 0.5, 0.46), (0.74, 0.7, 0.64)), 54, 0.3))
veins = K.fbm(S, 55, 4, 6)
skin = K.colorize(K.fbm(S, 56, 4, 4), (0.52, 0.47, 0.43), (0.7, 0.66, 0.6))
skin[np.abs(veins - 0.5) < 0.015] = (0.3, 0.32, 0.42)
save("Skin_Giant", skin)
save("Skin_Tan", grime(K.colorize(K.fbm(S, 57, 4, 4), (0.45, 0.28, 0.19), (0.6, 0.39, 0.27)), 58, 0.15))
suit = K.colorize(weave * 0.5 + K.fbm(S, 59, 3, 8) * 0.5, (0.02, 0.02, 0.025), (0.09, 0.09, 0.1))
tear = K.fbm(S, 60, 4, 5) > 0.7
suit[tear] = (0.2, 0.08, 0.06)
save("Suit_Torn", suit)
denim = K.colorize(((x + y * 2) % 5 < 2) * 0.35 + K.fbm(S, 61, 3, 8) * 0.65, (0.07, 0.1, 0.18), (0.2, 0.27, 0.42))
save("Denim", grime(denim, 62, 0.25))
save("Shirt_White", grime(solid((0.84, 0.84, 0.82), 63, 0.04, 0.0), 64, 0.3, 5))
save("Sneaker", grime(K.colorize(K.fbm(S, 65, 3, 6), (0.75, 0.75, 0.74), (0.92, 0.92, 0.9)), 66, 0.35))
save("Backpack", grime(K.colorize(weave, (0.03, 0.12, 0.1), (0.07, 0.25, 0.2)), 67, 0.3))
save("Hair_Black", K.colorize(np.repeat(K.value_noise(S, 32, 68)[:1], S, 0), (0.02, 0.02, 0.02), (0.1, 0.08, 0.07)))
truck = solid((0.82, 0.82, 0.8), 69, 0.03, 0.0)
truck = grime(truck, 70, 0.45, 4)
drip = (K.value_noise(S, 16, 71)[:1].repeat(S, 0) > 0.6) & (y > 30)
truck[drip] *= 0.8
save("Truck_Paint", truck)
save("Truck_Red", grime(solid((0.5, 0.05, 0.04), 72, 0.06, 0.0), 73, 0.4))
tread = np.full((S, S, 3), 0.05); tread[(y % 8) < 3] = 0.1
save("Tire", tread)
mop = K.colorize(np.repeat(K.value_noise(S, 32, 74)[:1], S, 0) * 0.7 + K.fbm(S, 75, 3, 6) * 0.3, (0.4, 0.38, 0.3), (0.7, 0.68, 0.58))
save("Mop", grime(mop, 76, 0.5))
blood = K.colorize(K.fbm(S, 77, 4, 4), (0.12, 0.0, 0.0), (0.35, 0.02, 0.02))
save("Blood_Dry", blood)
