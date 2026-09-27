"""Cut the four prop sheets into one PNG per object.

Pieces are cut by their own pixel shape, not by a rectangle, because some
objects' boxes overlap (the axe's box covers half the ceiling bracket) and a
rectangle would drag a slice of the neighbour along.

Every piece from one sheet is scaled by the SAME factor, so a small crate
stays smaller than a big one. Sizes are baked for Unity's default 100 pixels
per unit, so the files import at the right size with no settings to change.

Run:  python tools/slice_props.py
"""
import os
import numpy as np
from PIL import Image
from scipy import ndimage

SRC = "source-art/props"
OUT = "Shadow2d/Assets/Sprites/props"
PPU = 100

# sheet -> (names left to right, index of the piece that sets the scale,
#           how wide or tall that piece should be in world units, axis)
SHEETS = {
    "pushable props.png":    (["crate", "stone_block", "barrel", "crate_small"], 0, 1.2, "w"),
    "falling platforms.png": (["falling_intact", "falling_cracked",
                               "falling_half_left", "falling_half_right"], 0, 4.0, "w"),
    "swinging hazards.png":  (["spiked_log", "pendulum_axe", "ceiling_bracket",
                               "spiked_ball"], 0, 3.2, "h"),
}


def pieces(alpha, min_area=3000):
    """Masks of each separate object, left to right. A small dilation first
    glues chain links and loose sparkle to the object they belong to."""
    lab, n = ndimage.label(ndimage.binary_dilation(alpha > 40, iterations=6))
    keep = [i for i in range(1, n + 1) if (lab == i).sum() > min_area]
    boxes = ndimage.find_objects(lab)
    keep.sort(key=lambda i: boxes[i - 1][1].start)
    return [(lab == i) & (alpha > 0) for i in keep]


def seesaw_pieces(alpha):
    """The seesaw sheet is one blob: every piece touches. Eroding 4px snaps
    the thin rope contact to the bridge plank; the pivot is painted into the
    plank, so it is cut off with a line just under the plank's underside."""
    solid = alpha > 0
    lab, n = ndimage.label(ndimage.binary_erosion(alpha > 128, iterations=4))
    sizes = ndimage.sum(np.ones_like(lab), lab, range(1, n + 1))
    bridge_id = 1 + max((i for i in range(n) if sizes[i] < max(sizes)), key=lambda i: sizes[i])
    bridge = ndimage.binary_dilation(lab == bridge_id, iterations=6) & solid
    rest = solid & ~bridge
    ys, xs = np.mgrid[0:alpha.shape[0], 0:alpha.shape[1]]
    pivot = rest & (ys >= 296) & (xs >= 741) & (xs <= 1319)
    plank = rest & ~pivot
    return [plank, pivot, bridge]


def save(img, mask, name, scale):
    # Drop crumbs: any blob under 1% of the biggest one is stray dust the
    # image AI sprinkled around, not part of the object. Left in, one
    # crumb far below a plank doubles its height.
    lab, n = ndimage.label(mask & (np.array(img)[..., 3] > 40))
    if n > 1:
        sizes = ndimage.sum(np.ones_like(lab), lab, range(1, n + 1))
        big = np.isin(lab, [i + 1 for i in range(n) if sizes[i] >= sizes.max() * 0.01])
        mask = mask & ndimage.binary_dilation(big, iterations=3)
    rgba = np.array(img)
    rgba[..., 3] = np.where(mask, rgba[..., 3], 0)
    piece = Image.fromarray(rgba)
    # Crop to pixels at least ~15% visible. Cropping to ANY opacity keeps
    # near-invisible dust specks, and one speck far away doubles the box.
    piece = piece.crop(piece.getchannel("A").point(lambda a: 255 if a > 40 else 0).getbbox())
    w, h = piece.size
    piece = piece.resize((max(1, round(w * scale)), max(1, round(h * scale))), Image.LANCZOS)
    piece.save(f"{OUT}/{name}.png")
    return piece.size


def main():
    os.makedirs(OUT, exist_ok=True)
    jobs = []
    for sheet, (names, ref, units, axis) in SHEETS.items():
        img = Image.open(f"{SRC}/{sheet}").convert("RGBA")
        masks = pieces(np.array(img)[..., 3])
        assert len(masks) == len(names), f"{sheet}: found {len(masks)} pieces, expected {len(names)}"
        jobs.append((img, masks, names, ref, units, axis))

    img = Image.open(f"{SRC}/seesaw and bridge.png").convert("RGBA")
    jobs.append((img, seesaw_pieces(np.array(img)[..., 3]),
                 ["seesaw_plank", "seesaw_pivot", "bridge_plank"], 0, 7.0, "w"))

    for img, masks, names, ref, units, axis in jobs:
        ys, xs = np.nonzero(masks[ref])
        ref_px = (xs.max() - xs.min() + 1) if axis == "w" else (ys.max() - ys.min() + 1)
        scale = units * PPU / ref_px
        for mask, name in zip(masks, names):
            w, h = save(img, mask, name, scale)
            print(f"{name:20s} {w:4d}x{h:<4d}  = {w / PPU:.2f} x {h / PPU:.2f} units")


main()
