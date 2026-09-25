"""
Sign atlas for the CECADEC interior: every sign, notice and label cut straight out of the
site photos (docs/map/reference/cecadec-interior-pasillo/) and packed into one texture, so
all of the corridor's signage is one material and one draw call.

Runs with the system python3 (needs Pillow), NOT inside Blender:

    python3 Tools/blender/gen_sign_atlas.py

Writes Assets/_Project/Art/Textures/Kit/T_Signs.png and Tools/blender/sign_atlas.json:
{name: {"uv": [u0, v0, u1, v1], "size": [w_m, h_m]}} with Blender UVs (v up). The interior
generator reads it to size and map each Decal_* card. Crop boxes were checked on a
contact sheet; `quad` entries are perspective-corrected (TL, TR, BR, BL photo pixels).
"""

import json
import os

from PIL import Image, ImageDraw, ImageFont

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.normpath(os.path.join(HERE, "..", ".."))
REFERENCE = os.path.join(ROOT, "docs", "map", "reference")
PHOTOS = os.path.join(REFERENCE, "cecadec-interior-pasillo")
PHOTOS2 = os.path.join(REFERENCE, "cecadec-interior-piso2")
OUT_PNG = os.path.join(ROOT, "Assets", "_Project", "Art", "Textures", "Kit", "T_Signs.png")
OUT_JSON = os.path.join(HERE, "sign_atlas.json")

ATLAS = 2048
PAD = 6
MAX_SIDE = 260

# name: (photo, box (x0, y0, x1, y1) or quad ((TL), (TR), (BR), (BL)), real size (w, h) metres)
SIGNS = {
    "ALTO_VOLTAJE": (2, (1303, 493, 1442, 673), (0.30, 0.40)),
    "AULA1": (6, (1290, 551, 1423, 652), (0.30, 0.23)),
    "PROHIBIDO_A1": (6, (1305, 650, 1422, 712), (0.27, 0.14)),
    "AULA2": (5, (711, 615, 840, 715), (0.28, 0.215)),
    "HORARIO_A2": (5, (226, 590, 363, 698), (0.28, 0.215)),
    "CC9": (7, (1630, 450, 1765, 560), (0.30, 0.24)),
    "PROHIBIDO_CC9": (7, (1636, 576, 1770, 642), (0.30, 0.15)),
    "AHORREMOS": (7, (1655, 648, 1722, 700), (0.15, 0.115)),
    "HORARIO_CC9": (7, (1728, 607, 1868, 722), (0.28, 0.23)),
    "LAB_PROCESOS": (13, (349, 472, 491, 518), (0.34, 0.11)),
    "LAB_NOTICE": (13, (340, 520, 513, 742), (0.40, 0.52)),
    "EVAC_LEFT": (13, (684, 466, 828, 537), (0.40, 0.20)),
    "APAGA": (13, (711, 566, 808, 662), (0.25, 0.25)),
    "EVAC_RIGHT": (8, (892, 280, 1036, 353), (0.40, 0.20)),
    "AHORRA_AGUA": (8, (911, 412, 1008, 511), (0.25, 0.25)),
    "COLOCA": (8, (662, 517, 761, 614), (0.25, 0.25)),
    "BIN_PET": (8, (593, 1140, 724, 1262), (0.22, 0.21)),
    "BIN_ORG": (8, (850, 1156, 968, 1272), (0.20, 0.20)),
    "BIN_OTROS": (8, (1144, 1160, 1258, 1306), (0.20, 0.26)),
    "SWITCH": (8, (905, 737, 945, 795), (0.07, 0.115)),
    "EXTINTOR": (11, (46, 395, 188, 505), (0.30, 0.23)),
    "VIGILANCIA": (11, (1858, 234, 1992, 428), (0.28, 0.40)),
    "BOTIQUIN": (10, (1903, 457, 1950, 530), (0.20, 0.30)),
    "ALARMA": (10, (114, 556, 160, 628), (0.20, 0.30)),
    "LAB_IOT": (12, (918, 432, 1165, 494), (0.90, 0.225)),
    "UTEZ": (20, ((1506, 545), (1647, 515), (1647, 776), (1506, 748)), (1.60, 1.20)),
    # Waiting tables of the cross corridor (mesas-entrada2-b.png): what is left on top.
    "BOOK_BOX": ("cecadec-interior-pasillo/mesas-entrada2-b", (498, 288, 600, 400), (0.145, 0.22)),
    "BOOK_SPINES": ("cecadec-interior-pasillo/mesas-entrada2-b", (500, 395, 690, 545), (0.20, 0.21)),
    "MAG_HYPATIA": ("cecadec-interior-pasillo/mesas-entrada2-b", (760, 425, 995, 575), (0.21, 0.28)),
    # Upper floor (docs/map/reference/cecadec-interior-piso2/, photos 1-19 as delivered).
    "TI1": ("cecadec-interior-piso2/3", (213, 551, 358, 587), (0.30, 0.05)),
    "TI2": ("cecadec-interior-piso2/6", (1256, 478, 1394, 503), (0.30, 0.05)),
    "TI3": ("cecadec-interior-piso2/6", (554, 476, 727, 500), (0.30, 0.05)),
    "TI6": ("cecadec-interior-piso2/5", (1352, 462, 1504, 494), (0.30, 0.05)),
    "AULA_VIRTUAL": ("cecadec-interior-piso2/5", (672, 458, 826, 492), (0.30, 0.05)),
    "AREA_CONSULTORIA": ("cecadec-interior-piso2/3", (836, 554, 958, 576), (0.30, 0.05)),
    "CENTRO_CERT": ("cecadec-interior-piso2/4", (897, 451, 1028, 474), (0.30, 0.05)),
    "SITE_COM": ("cecadec-interior-piso2/15", (1165, 394, 1302, 425), (0.30, 0.05)),
    "AULA_VIRTUAL_CARD": ("cecadec-interior-piso2/5", (685, 495, 818, 600), (0.27, 0.215)),
    "AULA_DOBLE": ("cecadec-interior-piso2/6", (591, 518, 718, 627), (0.27, 0.215)),
    "SALA_MAESTROS": ("cecadec-interior-piso2/3", (833, 596, 991, 664), (0.27, 0.215)),
    "SOLO_PERSONAL": ("cecadec-interior-piso2/3", (1091, 773, 1222, 855), (0.24, 0.15)),
    "PUERTA_CERRADA": ("cecadec-interior-piso2/15", (1163, 482, 1336, 582), (0.27, 0.16)),
    "HORARIO_TI3": ("cecadec-interior-piso2/6", (585, 627, 718, 709), (0.28, 0.17)),
    "NOTICE_CERT": ("cecadec-interior-piso2/4", (854, 518, 964, 682), (0.21, 0.30)),
    "NOTICE_CERT2": ("cecadec-interior-piso2/4", (982, 518, 1091, 682), (0.21, 0.30)),
    "LINEAMIENTOS": ("cecadec-interior-piso2/6", (1154, 509, 1272, 691), (0.21, 0.30)),
    "CISCO": ("cecadec-interior-piso2/15", (809, 155, 945, 236), (0.30, 0.18)),
    "PROHIBIDO_TI": ("cecadec-interior-piso2/5", (598, 655, 700, 725), (0.21, 0.14)),
}


def _font(size):
    for path in ("/System/Library/Fonts/Helvetica.ttc", "/System/Library/Fonts/Supplemental/Arial Bold.ttf",
                 "/Library/Fonts/Arial.ttf"):
        if os.path.exists(path):
            return ImageFont.truetype(path, size)
    return ImageFont.load_default()


def _plaque():
    """Framed certificate like the ones along the band's north wall (photo 20)."""
    im = Image.new("RGB", (300, 380), (54, 30, 24))
    d = ImageDraw.Draw(im)
    d.rectangle((16, 16, 283, 363), fill=(196, 58, 44))
    d.rectangle((28, 28, 271, 351), fill=(238, 236, 228))
    d.rectangle((48, 52, 251, 64), fill=(60, 60, 70))
    for i, y in enumerate(range(90, 300, 14)):
        w = 180 if i % 3 else 140
        d.rectangle((150 - w // 2, y, 150 + w // 2, y + 4), fill=(140, 140, 146))
    d.ellipse((190, 290, 236, 336), fill=(196, 160, 60))
    return im


def _card_white():
    im = Image.new("RGB", (64, 64), (236, 236, 232))
    ImageDraw.Draw(im).rectangle((0, 0, 63, 63), outline=(200, 200, 196), width=3)
    return im


def _salida():
    im = Image.new("RGB", (400, 160), (20, 130, 70))
    d = ImageDraw.Draw(im)
    d.rectangle((8, 8, 391, 151), outline=(235, 240, 235), width=6)
    d.text((200, 80), "SALIDA", fill=(245, 248, 245), font=_font(84), anchor="mm")
    return im


def _wc(men):
    """Toilet door pictogram: white figure on the same blue as the room plaques."""
    im = Image.new("RGB", (240, 300), (18, 46, 104))
    d = ImageDraw.Draw(im)
    d.rectangle((8, 8, 231, 291), outline=(238, 240, 244), width=4)
    w = (238, 240, 244)
    d.ellipse((104, 46, 136, 78), fill=w)                       # head
    if men:
        d.polygon([(96, 88), (144, 88), (150, 176), (128, 176), (128, 250),
                   (112, 250), (112, 176), (90, 176)], fill=w)
        d.rectangle((96, 176, 112, 250), fill=w)
        d.rectangle((128, 176, 144, 250), fill=w)
    else:
        d.polygon([(120, 86), (156, 186), (84, 186)], fill=w)   # skirt
        d.rectangle((104, 186, 116, 250), fill=w)
        d.rectangle((124, 186, 136, 250), fill=w)
    return im


def _fuera_servicio():
    """Taped-up notice on the blocked toilet: laser print, red rule, curling corner."""
    im = Image.new("RGB", (420, 300), (238, 236, 230))
    d = ImageDraw.Draw(im)
    d.rectangle((0, 0, 419, 299), outline=(206, 202, 194), width=3)
    d.rectangle((26, 60, 393, 72), fill=(178, 32, 30))
    d.text((210, 130), "FUERA DE", fill=(40, 40, 44), font=_font(56), anchor="mm")
    d.text((210, 192), "SERVICIO", fill=(40, 40, 44), font=_font(56), anchor="mm")
    d.rectangle((26, 236, 393, 244), fill=(24, 60, 118))
    return im


def _centro_computo():
    """Vinyl lettering on the glass doors at the corridor's south end (photo 14)."""
    im = Image.new("RGB", (360, 260), (206, 204, 198))
    d = ImageDraw.Draw(im)
    for i, line in enumerate(("CENTRO DE", "COMPUTO", "10")):
        d.text((180, 66 + i * 70), line, fill=(28, 28, 30), font=_font(46), anchor="mm")
    return im


def _triangle_sticker():
    """The purple triangle stuck to the pillar between Aula Virtual and TI 6 (photo 5)."""
    im = Image.new("RGB", (200, 200), (228, 226, 220))
    ImageDraw.Draw(im).polygon([(104, 24), (178, 168), (28, 176)], fill=(66, 34, 132))
    return im


SYNTHETIC = {
    "PLAQUE": (_plaque, (0.40, 0.50)),
    "CARD_WHITE": (_card_white, (0.14, 0.14)),
    "SALIDA": (_salida, (0.40, 0.16)),
    "WC_MEN": (lambda: _wc(True), (0.16, 0.20)),
    "WC_WOMEN": (lambda: _wc(False), (0.16, 0.20)),
    "FUERA_SERVICIO": (_fuera_servicio, (0.21, 0.15)),
    "CENTRO_COMPUTO": (_centro_computo, (0.26, 0.19)),
    "TRIANGLE": (_triangle_sticker, (0.13, 0.13)),
}


def _load(photo, box):
    path = (os.path.join(REFERENCE, f"{photo}.png") if isinstance(photo, str)
            else os.path.join(PHOTOS, f"{photo}.png"))
    im = Image.open(path).convert("RGB")
    if isinstance(box[0], tuple):
        (tl, tr, br, bl) = box
        w = int(max(tr[0] - tl[0], br[0] - bl[0]) * 1.6)
        h = int(max(bl[1] - tl[1], br[1] - tr[1]))
        return im.transform((w, h), Image.QUAD, (*tl, *bl, *br, *tr), Image.BICUBIC)
    return im.crop(box)


def main():
    items = {n: (_load(p, b), size) for n, (p, b, size) in SIGNS.items()}
    items.update({n: (make(), size) for n, (make, size) in SYNTHETIC.items()})
    for n, (im, size) in list(items.items()):
        # Resample to the sign's real aspect: photo crops carry some perspective squash.
        aspect = size[0] / size[1]
        w = MAX_SIDE if aspect >= 1 else int(MAX_SIDE * aspect)
        h = int(w / aspect)
        items[n] = (im.resize((w, h), Image.LANCZOS), size)

    atlas = Image.new("RGB", (ATLAS, ATLAS), (128, 128, 128))
    out, x, y, row_h = {}, PAD, PAD, 0
    for n, (im, size) in sorted(items.items(), key=lambda kv: -kv[1][0].height):
        w, h = im.size
        if x + w + PAD > ATLAS:
            x, y, row_h = PAD, y + row_h + 2 * PAD, 0
        if y + h + PAD > ATLAS:
            raise SystemExit("[sign_atlas] atlas full; lower MAX_SIDE")
        # Bleed: a stretched copy under the sign so mips never pick up the gray ground.
        atlas.paste(im.resize((w + 2 * PAD, h + 2 * PAD)), (x - PAD, y - PAD))
        atlas.paste(im, (x, y))
        out[n] = {"uv": [x / ATLAS, 1 - (y + h) / ATLAS, (x + w) / ATLAS, 1 - y / ATLAS],
                  "size": list(size)}
        x += w + 2 * PAD
        row_h = max(row_h, h)

    atlas.save(OUT_PNG)
    with open(OUT_JSON, "w") as f:
        json.dump(out, f, indent=1, sort_keys=True)
    print(f"[sign_atlas] {len(out)} signs -> {OUT_PNG}, rows used to y={y + row_h}")


main()
