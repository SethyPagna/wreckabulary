import argparse
import json
import math
import os
import random

from PIL import Image, ImageDraw, ImageFilter

HERE = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.dirname(os.path.dirname(HERE))
PATTERNS = "Assets/_Project/Materials/Library/Patterns"
SIZE = 512
SUPER = 4


def parse_args():
    parser = argparse.ArgumentParser(description="Draw the tileable finish pattern textures.")
    parser.add_argument("--repo", default=REPO)
    parser.add_argument("--check", action="store_true")
    return parser.parse_args()


def smooth(edge0, edge1, x):
    t = min(max((x - edge0) / (edge1 - edge0), 0.0), 1.0)
    return t * t * (3 - 2 * t)


def wrapped_dots(rng, count, radii, spacing):
    dots = []
    tries = 0
    while len(dots) < count and tries < count * 400:
        tries += 1
        x, y, r = rng.random() * SIZE, rng.random() * SIZE, rng.uniform(*radii)
        if all(min(abs(x - a), SIZE - abs(x - a)) ** 2 + min(abs(y - b), SIZE - abs(y - b)) ** 2 >= (spacing + r + c) ** 2
               for a, b, c, _ in dots):
            dots.append((x, y, r, rng.randrange(1 << 30)))
    return dots


def stamp(draw, x, y, r, fill, scale):
    for dx in (-SIZE, 0, SIZE):
        for dy in (-SIZE, 0, SIZE):
            cx, cy = (x + dx) * scale, (y + dy) * scale
            draw.ellipse((cx - r * scale, cy - r * scale, cx + r * scale, cy + r * scale), fill=fill)


SPRINKLES = [(255, 132, 178), (120, 186, 255), (255, 222, 96), (126, 226, 176), (196, 150, 255), (255, 170, 120)]


def candy_stripes():
    rng = random.Random(1601)
    period, band = 64, 27
    img = Image.new("RGB", (SIZE, SIZE))
    px = img.load()
    for y in range(SIZE):
        for x in range(SIZE):
            d = (x + y) % period
            t = smooth(0, 2.2, d) * (1 - smooth(band - 2.2, band, d))
            v = round(255 * (1 - 0.14 * t))
            px[x, y] = (v, v, v)
    big = img.resize((SIZE * SUPER, SIZE * SUPER), Image.NEAREST)
    draw = ImageDraw.Draw(big)
    for x, y, r, k in wrapped_dots(rng, 64, (3.2, 4.4), 15):
        stamp(draw, x, y, r, SPRINKLES[k % len(SPRINKLES)], SUPER)
    return big.resize((SIZE, SIZE), Image.LANCZOS)


def arcade_grid():
    img = Image.new("RGB", (SIZE, SIZE))
    px = img.load()
    cell = 32
    for y in range(SIZE):
        for x in range(SIZE):
            gx = min(x % cell, cell - x % cell)
            gy = min(y % cell, cell - y % cell)
            line = 0.4 * max(math.exp(-(gx / 1.1) ** 2), math.exp(-(gy / 1.1) ** 2))
            scan = 0.1 if y % 8 == 4 else 0.0
            node = 1.0 if gx <= 2 and gy <= 2 else 0.0
            v = min(1.0, line + scan + node)
            g = round(255 * v)
            px[x, y] = (g, g, g)
    return img


def tile_noise(x, y, rng_terms):
    total = 0.0
    for fx, fy, phase, amp in rng_terms:
        total += amp * math.sin(2 * math.pi * (fx * x + fy * y) / SIZE + phase)
    return total


def winter_flecks(rng):
    return wrapped_dots(rng, 120, (2.2, 6.4), 7)


def winter_frost():
    rng = random.Random(2412)
    terms = [(rng.choice([1, 2, 3, 4, 5]) * rng.choice([-1, 1]), rng.choice([1, 2, 3, 4, 5]), rng.random() * 6.283, rng.uniform(.6, 1))
             for _ in range(9)]
    norm = sum(t[3] for t in terms)
    img = Image.new("RGB", (SIZE, SIZE))
    px = img.load()
    for y in range(SIZE):
        for x in range(SIZE):
            v = 0.95 + 0.03 * tile_noise(x, y, terms) / norm
            g = round(255 * v)
            px[x, y] = (g, g, g)
    layer = Image.new("L", (SIZE * SUPER, SIZE * SUPER), 0)
    draw = ImageDraw.Draw(layer)
    for x, y, r, _ in winter_flecks(random.Random(2413)):
        stamp(draw, x, y, r, 255, SUPER)
    mask = layer.resize((SIZE, SIZE), Image.LANCZOS).filter(ImageFilter.GaussianBlur(1.2))
    white = Image.new("RGB", (SIZE, SIZE), (255, 255, 255))
    return Image.composite(white, img, mask)


def winter_glints():
    layer = Image.new("L", (SIZE * SUPER, SIZE * SUPER), 0)
    draw = ImageDraw.Draw(layer)
    for x, y, r, _ in winter_flecks(random.Random(2413)):
        stamp(draw, x, y, r, 255, SUPER)
    soft = layer.resize((SIZE, SIZE), Image.LANCZOS).filter(ImageFilter.GaussianBlur(1.6))
    return Image.merge("RGB", (soft, soft, soft))


PATTERN_SET = {
    "candy_stripes": candy_stripes,
    "arcade_grid": arcade_grid,
    "winter_frost": winter_frost,
    "winter_glints": winter_glints,
}


def main():
    args = parse_args()
    folder = os.path.join(args.repo, PATTERNS)
    os.makedirs(folder, exist_ok=True)
    written, differs = [], []
    for name, draw in PATTERN_SET.items():
        path = os.path.join(folder, name + ".png")
        image = draw()
        tmp = path + ".tmp.png"
        image.save(tmp, optimize=True)
        new = open(tmp, "rb").read()
        old = open(path, "rb").read() if os.path.exists(path) else None
        if old != new:
            differs.append(name)
        if args.check:
            os.remove(tmp)
        else:
            os.replace(tmp, path)
            written.append(name)
    print("FINISH_PATTERNS " + json.dumps({"check": args.check, "written": written, "differs": differs}))
    if args.check and differs:
        raise SystemExit(1)


if __name__ == "__main__":
    main()
