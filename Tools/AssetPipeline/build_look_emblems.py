"""Draw the Shop look emblems (UI/Shop/Looks/Emblems/<look>.png) to match the rigid signature badges."""
import argparse
import math
from pathlib import Path

from PIL import Image, ImageDraw

REPO = Path(__file__).resolve().parents[2]
OUTPUT = REPO / "Assets/_Project/Resources/UI/Shop/Looks/Emblems"
SIZE, SCALE = 128, 4
S = SIZE * SCALE
C = S / 2
INK = (46, 42, 59, 255)
GOLD, GLOW, SOAP, PINK = (245, 200, 115, 255), (255, 214, 92, 255), (111, 214, 176, 255), (252, 170, 193, 255)
RED, LEAF, WHITE, DARK = (240, 87, 66, 255), (111, 175, 97, 255), (255, 255, 255, 255), (53, 61, 70, 255)
WOOD_DARK, IVORY, BRASS = (118, 75, 49, 255), (248, 241, 221, 255), (214, 174, 98, 255)
STROKE = int(S * .035)


def px(u, v):
    return (C + u * S, C - v * S)


def poly(draw, points, fill, outline=INK, width=STROKE):
    draw.polygon([px(u, v) for u, v in points], fill=fill, outline=outline, width=width)


def oval(draw, cu, cv, ru, rv, fill, outline=INK, width=STROKE):
    draw.ellipse([px(cu - ru, cv + rv), px(cu + ru, cv - rv)], fill=fill, outline=outline, width=width)


def rect(draw, u0, v0, u1, v1, fill, radius=0.0, outline=INK, width=STROKE):
    a, b = px(u0, v1), px(u1, v0)
    draw.rounded_rectangle([a, b], radius=radius * S, fill=fill, outline=outline, width=width)


def heart(size, cu=0.0, cv=0.0, n=48):
    out = []
    for i in range(n):
        t = 2 * math.pi * i / n
        x = 16 * math.sin(t) ** 3
        y = 13 * math.cos(t) - 5 * math.cos(2 * t) - 2 * math.cos(3 * t) - math.cos(4 * t)
        out.append((cu + x * size / 32, cv + y * size / 32))
    return out


def sun(draw):
    for i in range(10):
        a = math.radians(90 + 36 * i)
        poly(draw, [(.20 * math.cos(a - .30), .20 * math.sin(a - .30)), (.48 * math.cos(a), .48 * math.sin(a)), (.20 * math.cos(a + .30), .20 * math.sin(a + .30))],
            GLOW, width=int(STROKE * .7))
    oval(draw, 0, 0, .28, .28, GOLD)
    for side in (-1, 1):
        oval(draw, side * .095, .06, .034, .048, INK, width=0)
    draw.arc([px(-.13, .06), px(.13, -.16)], 25, 155, fill=INK, width=int(S * .032))


def perforated(w, h, notch, spacing):
    out = []
    corners = [(-w / 2, -h / 2), (w / 2, -h / 2), (w / 2, h / 2), (-w / 2, h / 2)]
    for c in range(4):
        (ax, ay), (bx, by) = corners[c], corners[(c + 1) % 4]
        length = math.hypot(bx - ax, by - ay)
        ex, ey = (bx - ax) / length, (by - ay) / length
        count = max(1, int(length / spacing))
        centres = [length * (k + .5) / count for k in range(count)]
        steps = int(length / .004)
        for k in range(steps):
            t = length * k / steps
            depth = max([math.sqrt(notch * notch - (t - m) ** 2) for m in centres if abs(t - m) < notch] or [0])
            out.append((ax + ex * t - ey * depth, ay + ey * t + ex * depth))
    return out


def stamp(draw):
    poly(draw, perforated(.70, .82, .042, .105), WHITE)
    rect(draw, -.25, -.30, .25, .30, SOAP, .03, width=int(STROKE * .7))
    rect(draw, -.19, -.15, .19, .11, WHITE, .02, width=int(STROKE * .7))
    draw.line([px(-.19, .11), px(0, -.04), px(.19, .11)], fill=INK, width=int(S * .025), joint="curve")
    poly(draw, heart(.11, 0, -.05), RED, width=int(STROKE * .6))


def sweet(draw):
    left = [(-.17, .06), (-.44, .23), (-.39, .17), (-.46, .11), (-.39, .05), (-.47, 0), (-.39, -.05), (-.46, -.11), (-.39, -.17), (-.44, -.23), (-.17, -.06)]
    poly(draw, left, WHITE)
    poly(draw, [(-u, v) for u, v in left], WHITE)
    for side in (-1, 1):
        rect(draw, side * .27 - .02, -.10, side * .27 + .02, .10, PINK, width=0)
    oval(draw, 0, 0, .24, .24, PINK)
    draw.arc([px(-.14, .14), px(.14, -.14)], 120, 330, fill=WHITE, width=int(S * .035))


def pixel(draw):
    rect(draw, -.40, -.37, .40, .37, DARK, .09)
    rows = [".XX.XX.", "XXXXXXX", "XXXXXXX", ".XXXXX.", "..XXX..", "...X..."]
    shine = {(1, 1), (1, 2), (2, 1)}
    p = .095
    for r, row in enumerate(rows):
        for c, cell in enumerate(row):
            if cell == "X":
                u, v = (c - 3) * p, (2.5 - r) * p - .01
                rect(draw, u - p * .47, v - p * .47, u + p * .47, v + p * .47, WHITE if (r, c) in shine else PINK, width=0)


def lantern(draw):
    draw.ellipse([px(-.05, .45), px(.05, .33)], outline=BRASS, width=int(S * .028))
    rect(draw, -.025, -.47, .025, -.32, BRASS, width=int(STROKE * .6))
    oval(draw, 0, -.47, .045, .035, BRASS, width=int(STROKE * .6))
    oval(draw, 0, -.02, .26, .30, RED)
    oval(draw, 0, -.02, .10, .15, GLOW, width=0)
    for u in (-.11, .11):
        draw.line([px(u, .20), px(u, -.24)], fill=BRASS, width=int(S * .02))
    rect(draw, -.15, .24, .15, .33, GOLD, .02)
    rect(draw, -.13, -.36, .13, -.27, GOLD, .02)


def holly_leaf(length, width, angle, base_v, spikes=4):
    right = []
    for i in range(1, 2 * spikes + 1):
        t = i / (2 * spikes + 1)
        w = width * math.sin(math.pi * t) ** .8 * (1.3 if i % 2 else .7)
        right.append((w, length * t))
    outline = [(0, 0)] + right + [(0, length)] + [(-x, y) for x, y in reversed(right)]
    a = math.radians(angle)
    return [(x * math.cos(a) - y * math.sin(a), base_v + x * math.sin(a) + y * math.cos(a)) for x, y in outline]


def holly(draw):
    for angle in (40, -40):
        poly(draw, holly_leaf(.56, .15, angle, -.14), LEAF)
        a = math.radians(angle)
        tip = (-math.sin(a) * .46, -.14 + math.cos(a) * .46)
        draw.line([px(0, -.14), px(*tip)], fill=WOOD_DARK, width=int(S * .018))
    for u, v in ((-.085, -.19), (.085, -.19), (0, -.06)):
        oval(draw, u, v, .085, .085, RED)


EMBLEMS = {"sunroom": sun, "courier": stamp, "candy": sweet, "arcade": pixel, "lantern": lantern, "winter": holly}


def render(draw_fn):
    image = Image.new("RGBA", (S, S), (0, 0, 0, 0))
    draw_fn(ImageDraw.Draw(image))
    return image.resize((SIZE, SIZE), Image.LANCZOS)


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--output", default=str(OUTPUT))
    parser.add_argument("--sheet", default=None)
    args = parser.parse_args()
    output = Path(args.output)
    output.mkdir(parents=True, exist_ok=True)
    images = {}
    for look, draw_fn in EMBLEMS.items():
        images[look] = render(draw_fn)
        images[look].save(output / f"{look}.png", optimize=True)
        print(f"EMBLEM {look} -> {output / (look + '.png')}")
    if args.sheet:
        cell = SIZE + 16
        sheet = Image.new("RGBA", (cell * len(images), cell + 60), (31, 41, 74, 255))
        for i, image in enumerate(images.values()):
            sheet.alpha_composite(image, (i * cell + 8, 8))
            small = image.resize((20, 20), Image.LANCZOS)
            sheet.alpha_composite(small, (i * cell + 8, cell + 4))
            sheet.alpha_composite(small.resize((40, 40), Image.NEAREST), (i * cell + 40, cell + 4))
        sheet.save(args.sheet)
        print(f"EMBLEM_SHEET {args.sheet}")


if __name__ == "__main__":
    main()
