"""
Facade lettering (photo 35): "CECADEC" in white block capitals near the top of the east
wall's north end. Written with Pillow (system python3, not Blender) as an RGBA map for
the alpha-tested Kit_Letters material:

    python3 Tools/blender/gen_letters.py
"""

import os

from PIL import Image, ImageDraw, ImageFont

HERE = os.path.dirname(os.path.abspath(__file__))
OUT = os.path.normpath(os.path.join(HERE, "..", "..", "Assets", "_Project", "Art", "Textures", "Kit",
                                    "T_Letters.png"))

W, H = 2048, 384
# Transparent texels keep the letter colour so mipmaps do not grow a dark fringe.
im = Image.new("RGBA", (W, H), (240, 240, 236, 0))
draw = ImageDraw.Draw(im)
try:
    font = ImageFont.truetype("/System/Library/Fonts/Helvetica.ttc", 320, index=1)
except OSError:
    font = ImageFont.load_default()
draw.text((W / 2, H / 2), "CECADEC", fill=(242, 242, 238, 255), font=font, anchor="mm")
im.save(OUT)
print("[letters] wrote", OUT)
