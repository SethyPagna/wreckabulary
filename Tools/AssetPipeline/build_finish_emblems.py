import argparse
import json
import math
import os

from PIL import Image, ImageDraw

HERE = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.dirname(os.path.dirname(HERE))
OUT = "Assets/_Project/Resources/UI/Finishes"
SIZE = 256
S = 4
C = SIZE * S / 2


def rgb(hex_colour, alpha=255):
    v = hex_colour.lstrip("#")
    return (int(v[0:2], 16), int(v[2:4], 16), int(v[4:6], 16), alpha)


def px(value):
    return value * S


def badge(face, rim, shine):
    image = Image.new("RGBA", (SIZE * S, SIZE * S), (0, 0, 0, 0))
    draw = ImageDraw.Draw(image)
    r = px(122)
    draw.ellipse((C - r, C - r + px(4), C + r, C + r + px(4)), fill=(0, 0, 0, 70))
    draw.ellipse((C - r, C - r, C + r, C + r), fill=rgb(rim))
    r2 = px(106)
    draw.ellipse((C - r2, C - r2, C + r2, C + r2), fill=rgb(face))
    glare = Image.new("RGBA", image.size, (0, 0, 0, 0))
    r3 = px(92)
    ImageDraw.Draw(glare).chord((C - r3, C - r3, C + r3, C + r3), 205, 245, fill=rgb(shine, 70))
    image.alpha_composite(glare)
    return image, ImageDraw.Draw(image)


def poly(draw, points, fill, ox=0, oy=0):
    draw.polygon([(C + px(x + ox), C + px(y + oy)) for x, y in points], fill=fill)


def ellipse(draw, x, y, rx, ry, fill):
    draw.ellipse((C + px(x - rx), C + px(y - ry), C + px(x + rx), C + px(y + ry)), fill=fill)


def classic():
    image, draw = badge("#C27A3E", "#F2C46B", "#FFFFFF")
    nut = rgb("#E6A55A")
    ellipse(draw, 0, 22, 46, 54, rgb("#8A5530"))
    ellipse(draw, 0, 18, 44, 52, nut)
    ellipse(draw, -14, 6, 12, 22, rgb("#F7CF8E"))
    cap = Image.new("RGBA", image.size, (0, 0, 0, 0))
    cd = ImageDraw.Draw(cap)
    cd.chord((C - px(62), C - px(66), C + px(62), C + px(14)), 180, 360, fill=rgb("#764B31"))
    cd.rectangle((C - px(62), C - px(27), C + px(62), C - px(16)), fill=rgb("#764B31"))
    hatch = Image.new("RGBA", image.size, (0, 0, 0, 0))
    hd = ImageDraw.Draw(hatch)
    for i in range(-5, 6):
        x = i * 16
        hd.line((C + px(x - 24), C - px(66), C + px(x + 24), C - px(16)), fill=rgb("#5A3822"), width=px(4))
        hd.line((C + px(x + 24), C - px(66), C + px(x - 24), C - px(16)), fill=rgb("#5A3822"), width=px(4))
    cap.alpha_composite(Image.composite(hatch, Image.new("RGBA", image.size, (0, 0, 0, 0)), cap.getchannel("A")))
    image.alpha_composite(cap)
    draw.rounded_rectangle((C - px(7), C - px(84), C + px(7), C - px(56)), radius=px(6), fill=rgb("#5A3822"))
    poly(draw, [(8, -76), (52, -92), (40, -66)], rgb("#F2C46B"))
    return image


def candy():
    image, draw = badge("#FF8DB9", "#FFFFFF", "#FFFFFF")
    poly(draw, [(-44, 0), (-92, -34), (-84, 0), (-92, 34)], rgb("#FFE58A"))
    poly(draw, [(44, 0), (92, -34), (84, 0), (92, 34)], rgb("#FFE58A"))
    for side in (-1, 1):
        for k in (-18, 0, 18):
            draw.line((C + px(side * 50), C + px(k * .4), C + px(side * 86), C + px(k)), fill=rgb("#F5C64E"), width=px(4))
    r = px(50)
    sweet = Image.new("RGBA", image.size, (0, 0, 0, 0))
    sd = ImageDraw.Draw(sweet)
    sd.ellipse((C - r, C - r, C + r, C + r), fill=rgb("#FFFFFF"))
    stripes = Image.new("RGBA", image.size, (0, 0, 0, 0))
    st = ImageDraw.Draw(stripes)
    for i in range(-6, 7):
        x = i * 26
        poly(st, [(x - 60, 60), (x - 47, 60), (x + 73, -60), (x + 60, -60)], rgb("#F2386F"))
    mask = Image.new("L", image.size, 0)
    ImageDraw.Draw(mask).ellipse((C - r, C - r, C + r, C + r), fill=255)
    sweet.paste(stripes, (0, 0), Image.composite(stripes.getchannel("A"), Image.new("L", image.size, 0), mask))
    ImageDraw.Draw(sweet).ellipse((C - px(30), C - px(28), C - px(8), C - px(14)), fill=(255, 255, 255, 150))
    image.alpha_composite(sweet)
    return image


def arcade():
    image = Image.new("RGBA", (SIZE * S, SIZE * S), (0, 0, 0, 0))
    draw = ImageDraw.Draw(image)
    r = px(122)
    draw.rounded_rectangle((C - r, C - r + px(4), C + r, C + r + px(4)), radius=px(34), fill=(0, 0, 0, 70))
    draw.rounded_rectangle((C - r, C - r, C + r, C + r), radius=px(34), fill=rgb("#2BE6FF"))
    r2 = px(108)
    draw.rounded_rectangle((C - r2, C - r2, C + r2, C + r2), radius=px(24), fill=rgb("#1A1650"))
    for k in range(-96, 100, 24):
        draw.line((C - r2 + px(6), C + px(k), C + r2 - px(6), C + px(k)), fill=rgb("#2A2470"), width=px(2))
        draw.line((C + px(k), C - r2 + px(6), C + px(k), C + r2 - px(6)), fill=rgb("#2A2470"), width=px(2))
    heart = ["0110110", "1111111", "1111111", "0111110", "0011100", "0001000"]
    cell = 22
    ox, oy = -cell * 3.5, -cell * 3.2
    for row, line in enumerate(heart):
        for col, bit in enumerate(line):
            if bit == "1":
                x, y = ox + col * cell, oy + row * cell
                draw.rectangle((C + px(x + 1), C + px(y + 1), C + px(x + cell - 1), C + px(y + cell - 1)), fill=rgb("#FF3FD2"))
    for x, y in ((1, 1), (2, 1)):
        draw.rectangle((C + px(ox + x * cell + 4), C + px(oy + y * cell + 4), C + px(ox + x * cell + cell - 6), C + px(oy + y * cell + cell - 6)), fill=rgb("#FFC2F2"))
    return image


def winter():
    image, draw = badge("#2E7A5C", "#EEF4F8", "#FFFFFF")
    white = rgb("#F4FAFF")
    for k in range(6):
        a = math.radians(90 + k * 60)
        dx, dy = math.cos(a), -math.sin(a)
        draw.line((C, C, C + px(dx * 78), C + px(dy * 78)), fill=white, width=px(11))
        for t, length in ((0.45, 26), (0.72, 18)):
            bx, by = dx * 78 * t, dy * 78 * t
            for turn in (-50, 50):
                b = a + math.radians(turn)
                draw.line((C + px(bx), C + px(by), C + px(bx + math.cos(b) * length), C + px(by - math.sin(b) * length)), fill=white, width=px(8))
        ellipse(draw, dx * 80, dy * 80, 7, 7, white)
    ellipse(draw, 0, 0, 16, 16, white)
    ellipse(draw, 0, 0, 7, 7, rgb("#BFE3F2"))
    return image


EMBLEMS = {"Classic": classic, "Candy": candy, "Arcade": arcade, "Winter": winter}


def main():
    parser = argparse.ArgumentParser(description="Draw the finish emblem badges.")
    parser.add_argument("--repo", default=REPO)
    args = parser.parse_args()
    folder = os.path.join(args.repo, OUT)
    os.makedirs(folder, exist_ok=True)
    for name, draw in EMBLEMS.items():
        draw().resize((SIZE, SIZE), Image.LANCZOS).save(os.path.join(folder, name + ".png"), optimize=True)
    print("FINISH_EMBLEMS " + json.dumps(sorted(EMBLEMS)))


if __name__ == "__main__":
    main()
