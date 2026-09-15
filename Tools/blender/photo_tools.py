"""
Photo and preview helpers for the landmark workflow (run inside Blender: numpy + bpy).

  grid_crop      crop of a reference photo with a pixel grid, to read coordinates off it
                 (texture source boxes, proportions)
  contact_sheet  thumbnails of generated maps in one image; alpha shows as magenta
  psx_png        rough PSX look (pixelation, colour crunch, dither, scanlines, vignette)
                 applied to a render, to judge a texture under the in-game filter
  compare_sheet  2 x N grid of images, e.g. photo | render from the same spot

From a live session:
    import sys; sys.path.insert(0, "<repo>/Tools/blender")
    import photo_tools, importlib; importlib.reload(photo_tools)
    photo_tools.grid_crop(photo, (x0, y0, x1, y1), "/path/out.png")
"""

import os

import bpy
import numpy as np


def load_png(path):
    """Image as float RGB in Blender pixel order (row 0 = bottom), plus alpha."""
    img = bpy.data.images.load(path, check_existing=False)
    w, h = img.size
    px = np.empty(w * h * 4, np.float32)
    img.pixels.foreach_get(px)
    bpy.data.images.remove(img)
    a = px.reshape(h, w, 4).astype(np.float64)
    return a[..., :3], a[..., 3]


def save_png(path, rgb):
    h, w = rgb.shape[:2]
    img = bpy.data.images.new(os.path.basename(path), w, h, alpha=False)
    rgba = np.concatenate([np.clip(rgb, 0, 1), np.ones((h, w, 1))], axis=2)
    img.pixels.foreach_set(rgba.astype(np.float32).ravel())
    img.filepath_raw = path
    img.file_format = "PNG"
    img.save()
    bpy.data.images.remove(img)
    return path


def fit(a, w, h):
    """Nearest-neighbour resize."""
    ys = (np.arange(h) * a.shape[0] / h).astype(int)
    xs = (np.arange(w) * a.shape[1] / w).astype(int)
    return a[ys][:, xs]


def grid_crop(photo_path, box, out_path, grid=25, scale=1):
    """Crop box = (x0, y0, x1, y1) in photo pixels (origin top-left, like an image viewer).
    Lines: yellow every 100 px, magenta every 50, cyan every `grid`."""
    rgb, _ = load_png(photo_path)
    top_down = rgb[::-1]
    x0, y0, x1, y1 = box
    c = top_down[y0:y1, x0:x1].copy()
    if scale > 1:
        c = c.repeat(scale, 0).repeat(scale, 1)
    for gx in range((x0 // grid + 1) * grid, x1, grid):
        c[:, (gx - x0) * scale] = [1, 1, 0] if gx % 100 == 0 else ([1, 0, 1] if gx % 50 == 0 else [0, 1, 1])
    for gy in range((y0 // grid + 1) * grid, y1, grid):
        c[(gy - y0) * scale, :] = [1, 1, 0] if gy % 100 == 0 else ([1, 0, 1] if gy % 50 == 0 else [0, 1, 1])
    return save_png(out_path, c[::-1])


def contact_sheet(texture_dir, names, out_path, tile=300, cols=4):
    rows = (len(names) + cols - 1) // cols
    sheet = np.full((rows * tile, cols * tile, 3), 0.5)
    for i, name in enumerate(names):
        rgb, alpha = load_png(os.path.join(texture_dir, name + ".png"))
        rgb = rgb * alpha[..., None] + np.array([1.0, 0.0, 1.0]) * (1 - alpha[..., None])
        r, c = divmod(i, cols)
        sheet[(rows - 1 - r) * tile:(rows - r) * tile, c * tile:(c + 1) * tile] = fit(rgb, tile, tile)
    return save_png(out_path, sheet)


def psx(rgb, pixel_height=240, levels=32):
    """Approximation of PsxScreenRenderFeature: not exact, good enough to judge a map."""
    h, w = rgb.shape[:2]
    ph, pw = pixel_height, int(w * pixel_height / h)
    small = fit(rgb, pw, ph)
    bayer = np.array([[0, 8, 2, 10], [12, 4, 14, 6], [3, 11, 1, 9], [15, 7, 13, 5]]) / 16.0 - 0.5
    d = bayer[np.arange(ph)[:, None] % 4, np.arange(pw)[None, :] % 4][..., None]
    q = np.floor(small * (levels - 1) + 0.5 + d * 0.9) / (levels - 1)
    out = fit(np.clip(q, 0, 1), w, h)
    out *= (1.0 - 0.08 * ((np.arange(h)[:, None] // 2) % 2))[..., None]
    yy, xx = np.mgrid[0:h, 0:w]
    r = np.hypot((xx - w / 2) / (w / 2), (yy - h / 2) / (h / 2))
    return out * (1 - 0.22 * np.clip(r - 0.5, 0, 1) ** 1.5)[..., None]


def psx_png(in_path, out_path, pixel_height=240, levels=32):
    rgb, _ = load_png(in_path)
    return save_png(out_path, psx(rgb, pixel_height, levels))


def compare_sheet(paths, out_path, w=800, h=600, cols=2):
    """Images in reading order (left to right, top to bottom), white gutters."""
    rows = (len(paths) + cols - 1) // cols
    sheet = np.ones((rows * h, cols * w, 3))
    for i, p in enumerate(paths):
        rgb, _ = load_png(p)
        r, c = divmod(i, cols)
        sheet[(rows - 1 - r) * h:(rows - r) * h, c * w:(c + 1) * w] = fit(rgb, w, h)
    for c in range(1, cols):
        sheet[:, c * w - 2:c * w + 2] = 1.0
    for r in range(1, rows):
        sheet[r * h - 2:r * h + 2, :] = 1.0
    return save_png(out_path, sheet)
