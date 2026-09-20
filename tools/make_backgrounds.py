"""Draw the four daytime grassland parallax layers.

Everything is built from sums of sine waves with WHOLE-NUMBER frequencies.
A whole number of waves across the image means the left edge continues
perfectly into the right edge, so the layer tiles forever with no seam.
That is the whole trick - no mirroring, no hand-painted edges.

Run:  python tools/make_backgrounds.py
"""
import math, random
from PIL import Image, ImageDraw, ImageFilter

W, H = 2048, 1024
OUT = "Shadow2d/Assets/Sprites/backgrounds"
random.seed(7)          # same art every run, so a rebuild never shuffles


def ridge(freqs, amps, base, phase):
    """Height of the hill line at every x, as a list of W numbers."""
    return [base + sum(a * math.sin(2 * math.pi * f * x / W + p)
                       for f, a, p in zip(freqs, amps, phase))
            for x in range(W)]


def hills(line, fill, shade=None):
    """Fill everything below the given hill line."""
    img = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    d.polygon([(0, H)] + [(x, line[x]) for x in range(W)] + [(W - 1, H)], fill=fill)
    if shade:                                   # thin lit rim along the top
        d.line([(x, line[x]) for x in range(W)], fill=shade, width=6)
    return img


def sky():
    img = Image.new("RGBA", (W, H))
    d = ImageDraw.Draw(img)
    top, bot = (96, 168, 232), (208, 232, 246)  # deep blue overhead, hazy at the horizon
    for y in range(H):
        t = y / (H - 1)
        d.line([(0, y), (W, y)],
               fill=tuple(round(a + (b - a) * t) for a, b in zip(top, bot)) + (255,))

    clouds = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    cd = ImageDraw.Draw(clouds)
    for _ in range(14):
        cx, cy = random.randrange(W), random.randrange(60, 430)
        for _ in range(random.randint(4, 7)):    # a cloud is a few overlapping blobs
            r = random.randint(40, 110)
            ox, oy = random.randint(-130, 130), random.randint(-22, 22)
            for wrap in (-W, 0, W):              # draw past both edges so clouds wrap
                cd.ellipse([cx + ox + wrap - r, cy + oy - r * 0.55,
                            cx + ox + wrap + r, cy + oy + r * 0.55],
                           fill=(255, 255, 255, 150))
    return Image.alpha_composite(img, clouds.filter(ImageFilter.GaussianBlur(14)))


def tree(d, x, y, h, dark, light):
    d.rectangle([x - h * 0.05, y - h * 0.35, x + h * 0.05, y], fill=(86, 62, 44, 255))
    for i in range(3):                           # three stacked cones = conifer
        w = h * (0.38 - i * 0.09)
        ty = y - h * (0.30 + i * 0.22)
        d.polygon([(x - w, ty), (x + w, ty), (x, ty - h * 0.34)],
                  fill=light if i == 2 else dark)


def main():
    sky().save(f"{OUT}/bg1_sky.png")

    far = ridge([1, 2, 3], [70, 40, 22], 620, [0.4, 2.1, 4.3])
    hills(far, (150, 186, 188, 255), (176, 206, 204, 255)) \
        .filter(ImageFilter.GaussianBlur(2)).save(f"{OUT}/bg2_far.png")

    midline = ridge([1, 3, 5], [55, 30, 16], 730, [1.7, 0.9, 3.3])
    mid = hills(midline, (104, 152, 92, 255), (132, 180, 108, 255))
    md = ImageDraw.Draw(mid)
    for x in range(40, W, 118):
        tree(md, x, midline[x] + 6, random.randint(120, 175),
             (58, 96, 58, 255), (74, 118, 68, 255))
    mid.save(f"{OUT}/bg3_mid.png")

    nearline = ridge([2, 4], [34, 18], 860, [0.2, 2.6])
    near = hills(nearline, (72, 124, 66, 255), (108, 166, 88, 255))
    nd = ImageDraw.Draw(near)
    for x in range(0, W, 26):                    # grass tufts along the crest
        y = nearline[x]
        nd.polygon([(x, y + 4), (x + 5, y - random.randint(14, 30)), (x + 11, y + 4)],
                   fill=(94, 150, 76, 255))
    for x in range(70, W, 260):                  # bushes
        y = nearline[x]
        for ox, r in ((-34, 30), (0, 42), (34, 28)):
            nd.ellipse([x + ox - r, y - r, x + ox + r, y + r * 0.4],
                       fill=(60, 108, 58, 255))
    near.save(f"{OUT}/bg4_near.png")
    print("wrote 4 layers")


main()
