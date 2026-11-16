import argparse
import json
import math
import sys
from pathlib import Path

sys.dont_write_bytecode = True
sys.path.insert(0, str(Path(__file__).resolve().parent))

import bpy
import surroundings_kit as kit


FBX_BUILDINGS = "Assets/_Project/Art/Surroundings/Kit/Buildings.fbx"
FBX_CARDS = "Assets/_Project/Art/Surroundings/Kit/Cards.fbx"
MANIFEST = "Assets/_Project/Data/Surroundings/kit_buildings.json"
SHEET = "BuildNotes/surroundings/kit/buildings.png"
RECEIPT = "BuildNotes/surroundings/kit/buildings.json"

PLASTER, BRICK, STONE, ROOF, WOOD = "bk_plaster", "bk_brick", "bk_stone", "bk_roof", "bk_wood"
PAINT, METAL, GLASS, CARD = "bk_paint", "bk_metal", "bk_glass_dark", "bk_card"
FRONT, BACK, RIGHT, LEFT = (0, 0, 1), (0, 0, -1), (1, 0, 0), (-1, 0, 0)

TARGETS = {
    "house_detached_a": (2000, (10.0, 9.0, 8.6)),
    "house_detached_b": (2000, (11.0, 8.0, 8.2)),
    "house_detached_c": (2000, (9.0, 10.0, 7.6)),
    "house_bungalow_a": (1600, (12.0, 8.0, 5.6)),
    "garage_a": (500, (4.0, 6.0, 3.0)),
    "shed_a": (400, (2.4, 2.0, 2.4)),
    "landmark_water_tower": (1500, (8.0, 8.0, 24.0)),
    "card_roofline_a": (120, (80.0, None, 14.0)),
    "card_hills_a": (80, (160.0, None, 30.0)),
}
FAR_TRIS = 160


def win(p, far, centre, normal, width, height, **options):
    if far:
        p.flat_window(centre, normal, width * 0.86, height * 0.86)
    else:
        p.window(centre, normal, width, height, **options)


def piece(name, far, family):
    return kit.Piece(name + ("_far" if far else ""), "far" if far else family)


def house_a(far):
    p = piece("house_detached_a", far, "house")
    w, d, eave, ridge = 9.0, 7.6, 6.0, 8.6
    hz, top = d / 2, eave + 0.04
    p.box(PLASTER, (0, top / 2, 0), (w, top, d), bevel=0 if far else 0.05, bevel_axes="y", drop=("-y",))
    p.gable_roof((0, 0), w, d, eave, ridge, overhang=0.45, gable_overhang=0.35, thickness=0.26,
                 courses=1 if far else 6, ridge_cap=not far, fill_slot=PLASTER)
    p.chimney((2.9, 0.0), ridge - 1.1, 8.8, size=(0.9, 0.7), slot=BRICK, pots=0 if far else 2,
              bevel=0 if far else 0.03)
    if far:
        p.prism(ROOF, [(-1.07, 2.78), (1.07, 2.78), (0.0, 3.55)], "z", (hz - 0.1, hz + 1.2))
        p.flat_window((0, 1.5, hz), FRONT, 1.0, 2.1, slot=ROOF)
    else:
        p.box(BRICK, (0, 0.22, 0), (w + 0.08, 0.44, d + 0.08), bevel=0.03, bevel_axes="y", drop=("-y",), tone=0.95)
        p.box(PAINT, (0, 3.0, 0), (w + 0.06, 0.12, d + 0.06))
        p.box(BRICK, (0, 0.225, hz + 0.575), (2.1, 0.45, 1.15), drop=("-y",), tone=0.92)
        p.door((0, 0.44, hz), FRONT, 1.1, 2.15, frame_slot=PAINT, leaf_slot=ROOF, glass_slot=GLASS)
        for side in (-1, 1):
            p.box(PAINT, (side * 0.85, 1.685, hz + 0.98), (0.14, 2.49, 0.14), bevel=0.02, bevel_axes="y", drop=("-y",))
        p.gable_roof((0, hz + 0.55), 1.1, 1.9, 2.85, 3.55, axis="z", overhang=0.12, gable_overhang=0.1,
                     thickness=0.14, courses=2, ridge_cap=False, fill_slot=PAINT)
    for x in (-2.75, 2.75):
        win(p, far, (x, 1.55, hz), FRONT, 1.4, 1.5)
        win(p, far, (x, 4.5, hz), FRONT, 1.3, 1.4)
    win(p, far, (0, 4.5, hz), FRONT, 0.8, 1.1, cols=1)
    for sx, n in ((w / 2, RIGHT), (-w / 2, LEFT)):
        win(p, far, (sx, 1.55, 1.2), n, 1.2, 1.4)
        win(p, far, (sx, 4.5, 1.2), n, 1.1, 1.3)
        win(p, far, (sx, 7.05, 0.0), n, 0.55, 0.7, cols=1, rows=1, sill=False)
    for x in (-2.5, 2.5):
        win(p, far, (x, 1.55, -hz), BACK, 1.3, 1.4)
        win(p, far, (x, 4.5, -hz), BACK, 1.2, 1.3)
    return p


def bay_outline(x0, z0, y, grow=0.0):
    return [(x0 - 1.9 - grow * 1.4, y, z0 - 0.05), (x0 + 1.9 + grow * 1.4, y, z0 - 0.05),
            (x0 + 1.0 + grow * 0.4, y, z0 + 0.9 + grow), (x0 - 1.0 - grow * 0.4, y, z0 + 0.9 + grow)]


def bay_roof_meta(p):
    roof, fascia, soffit = p.meta(ROOF, 0.9), p.meta(PAINT, 1.0), p.meta(PAINT, 0.82)
    return lambda n, c: roof if n.y > 0.35 else soffit if n.y < -0.5 else fascia


def house_b(far):
    p = piece("house_detached_b", far, "house")
    w, d, eave, ridge = 10.0, 6.8, 6.0, 8.2
    hz, top = d / 2, eave + 0.04
    bevel = 0 if far else 0.05
    p.box(BRICK, (0, 1.5, 0), (w, 3.0, d), bevel=bevel, bevel_axes="y", drop=("-y",))
    p.box(PLASTER, (0, (3.0 + top) / 2, 0), (w, top - 3.0, d), bevel=bevel, bevel_axes="y", drop=("-y",))
    p.hip_roof((0, 0), w, d, eave, ridge, overhang=0.5, thickness=0.24, courses=1 if far else 5, ridge_cap=not far)
    for x in (-1.6, 1.6):
        p.chimney((x, 0.0), ridge - 1.2, 8.45, size=(0.75, 0.75), slot=BRICK, pots=0 if far else 2,
                  bevel=0 if far else 0.03)
    x0 = -2.8
    roof_points = bay_outline(x0, hz, 2.6, 0.12) + bay_outline(x0, hz, 2.72, 0.12)
    roof_points += [(x0 - 1.1, 3.25, hz - 0.05), (x0 + 1.1, 3.25, hz - 0.05)]
    p.hull(PAINT, roof_points, meta_fn=bay_roof_meta(p))
    if far:
        p.hull(BRICK, bay_outline(x0, hz, 0.0) + bay_outline(x0, hz, 2.6))
        p.flat_window((x0, 1.65, hz + 0.9), FRONT, 1.5, 1.4)
        p.flat_window((0.9, 1.25, hz), FRONT, 0.95, 2.2, slot=PAINT)
    else:
        p.box(PAINT, (0, 3.0, 0), (w + 0.06, 0.14, d + 0.06))
        p.hull(BRICK, bay_outline(x0, hz, 0.0) + bay_outline(x0, hz, 0.7), tone=0.95)
        p.hull(PAINT, bay_outline(x0, hz, 0.7) + bay_outline(x0, hz, 2.6))
        p.window((x0, 1.65, hz + 0.9), FRONT, 1.6, 1.5, cols=3, sill=False)
        for side in (-1, 1):
            a = (x0 + side * 1.9, hz - 0.05)
            b = (x0 + side * 1.0, hz + 0.9)
            mx, mz = (a[0] + b[0]) / 2, (a[1] + b[1]) / 2
            nx, nz = (b[1] - a[1]) * side, -(b[0] - a[0]) * side
            length = math.hypot(nx, nz)
            p.window((mx, 1.65, mz), (nx / length, 0, nz / length), 0.85, 1.5, cols=1, sill=False)
        p.door((0.9, 0.16, hz), FRONT, 1.05, 2.2, frame_slot=PAINT, leaf_slot=PAINT, glass_slot=GLASS,
               step_slot=BRICK, leaf_tone=0.88)
        p.box(PAINT, (0.9, 2.62, hz + 0.375), (1.6, 0.14, 0.75), drop=("-z",))
        for bx in (0.28, 1.52):
            p.prism(PAINT, [(hz, 2.55), (hz + 0.55, 2.55), (hz, 2.0)], "x", (bx - 0.05, bx + 0.05))
    win(p, far, (3.3, 1.55, hz), FRONT, 1.3, 1.4, lintel_slot=PAINT)
    win(p, far, (-2.8, 4.5, hz), FRONT, 1.6, 1.4, cols=3)
    win(p, far, (0.9, 4.5, hz), FRONT, 0.9, 1.2, cols=1)
    win(p, far, (3.3, 4.5, hz), FRONT, 1.3, 1.4)
    for sx, n in ((w / 2, RIGHT), (-w / 2, LEFT)):
        win(p, far, (sx, 1.55, 0.0), n, 1.2, 1.4, lintel_slot=PAINT)
        win(p, far, (sx, 4.5, 0.0), n, 1.2, 1.3)
    for x in (-2.5, 2.5):
        win(p, far, (x, 1.55, -hz), BACK, 1.3, 1.4, lintel_slot=PAINT)
    for x in (-3.0, 0.0, 3.0):
        win(p, far, (x, 4.5, -hz), BACK, 1.2, 1.3)
    return p


def house_c(far):
    p = piece("house_detached_c", far, "house")
    w, d, eave, ridge = 8.0, 8.6, 3.5, 7.6
    hz, top = d / 2, eave + 0.04
    p.box(PLASTER, (0, top / 2, 0), (w, top, d), bevel=0 if far else 0.05, bevel_axes="y", drop=("-y",))
    roof = p.gable_roof((0, 0), w, d, eave, ridge, overhang=0.42, gable_overhang=0.35, thickness=0.28,
                        courses=1 if far else 8, ridge_cap=not far, fill_slot=PLASTER)
    for x in (-2.0, 2.0):
        p.dormer(roof, x, 0.75, 1.8, 1.5, wall_slot=PLASTER, far=far)
    cx = -w / 2 - 0.3
    p.box(PLASTER, (cx, 2.3, 0.0), (0.6, 4.6, 1.5), bevel=0 if far else 0.03, bevel_axes="y", drop=("-y",), tone=0.97)
    p.chimney((-w / 2 - 0.25, 0.0), 4.6, 7.8, size=(0.5, 0.9), slot=PLASTER, cap_slot=PAINT, pots=0 if far else 2,
              bevel=0 if far else 0.03, tone=0.97)
    if far:
        p.prism(ROOF, [(-0.9, 2.65), (0.9, 2.65), (0.0, 3.22)], "z", (hz - 0.1, hz + 0.8))
        p.flat_window((0, 1.28, hz), FRONT, 0.95, 2.2, slot=WOOD)
    else:
        p.box(PLASTER, (0, 0.2, 0), (w + 0.08, 0.4, d + 0.08), bevel=0.03, bevel_axes="y", drop=("-y",), tone=0.86)
        x0, x1 = -w / 2, -w / 2 - 0.6
        p.hull(PLASTER, [(x1, 4.58, -0.75), (x0, 4.58, -0.75), (x1, 4.58, 0.75), (x0, 4.58, 0.75),
                         (x0 - 0.5, 5.1, -0.45), (x0, 5.1, -0.45), (x0 - 0.5, 5.1, 0.45), (x0, 5.1, 0.45)], tone=0.97)
        p.door((0, 0.42, hz), FRONT, 1.05, 2.12, frame_slot=PAINT, leaf_slot=WOOD, step_slot=PLASTER, leaf_tone=0.95)
        p.gable_roof((0, hz + 0.35), 0.7, 1.6, 2.72, 3.22, axis="z", overhang=0.1, gable_overhang=0.1,
                     thickness=0.12, courses=2, ridge_cap=False, fill_slot=PAINT)
        for bx in (-0.7, 0.7):
            p.prism(PAINT, [(hz, 2.78), (hz + 0.5, 2.78), (hz, 2.2)], "x", (bx - 0.05, bx + 0.05))
    for x in (-2.3, 2.3):
        win(p, far, (x, 1.45, hz), FRONT, 1.2, 1.3, shutter_slot=WOOD)
    win(p, far, (w / 2, 1.45, 1.2), RIGHT, 1.0, 1.2)
    win(p, far, (w / 2, 5.0, 0.0), RIGHT, 0.9, 1.0, rows=1)
    win(p, far, (-w / 2, 1.45, 2.4), LEFT, 1.0, 1.2)
    win(p, far, (-w / 2, 5.0, -1.4), LEFT, 0.8, 0.9, rows=1)
    for x in (-2.3, 2.3):
        win(p, far, (x, 1.45, -hz), BACK, 1.2, 1.3)
    win(p, far, (0, 1.6, -hz), BACK, 0.7, 0.9, cols=1)
    return p


def bungalow(far):
    p = piece("house_bungalow_a", far, "house")
    w, d, eave, ridge = 11.0, 6.6, 3.2, 5.6
    hz, top = d / 2, eave + 0.04
    bevel = 0 if far else 0.05
    p.box(BRICK, (0, top / 2, 0), (w, top, d), bevel=bevel, bevel_axes="y", drop=("-y",))
    p.gable_roof((0, 0), w, d, eave, ridge, overhang=0.45, gable_overhang=0.3, thickness=0.24,
                 courses=1 if far else 5, ridge_cap=not far, fill_slot=BRICK)
    wx0, wx1 = -5.49, -1.7
    wcx, ww = (wx0 + wx1) / 2, wx1 - wx0
    zf = hz + 1.0
    p.box(BRICK, (wcx, top / 2, (hz - 1.0 + zf) / 2), (ww, top, 2.0), bevel=bevel, bevel_axes="y", drop=("-y",))
    p.gable_roof((wcx, 2.8), 3.0, ww, eave, 4.75, axis="z", overhang=0.3, gable_overhang=0.3, thickness=0.24,
                 courses=1 if far else 3, ridge_cap=False, fill_slot=BRICK)
    p.chimney((3.2, 0.0), ridge - 0.9, 5.72, size=(0.8, 0.7), slot=BRICK, pots=0 if far else 2,
              bevel=0 if far else 0.03, pot_h=0.22)
    porch = [(hz - 0.05, 2.8), (hz + 1.25, 2.48), (hz + 1.25, 2.36), (hz - 0.05, 2.68)]
    if far:
        p.prism(ROOF, porch, "x", (-1.65, 5.55))
        p.flat_window((0.6, 1.25, hz), FRONT, 0.9, 2.1, slot=WOOD)
    else:
        top_m, fascia, soffit = p.meta(ROOF, 0.92), p.meta(PAINT, 1.0), p.meta(PAINT, 0.82)
        p.prism(ROOF, porch, "x", (-1.65, 5.55), edge_metas=[top_m, fascia, soffit, fascia], cap_meta=fascia)
        p.box(WOOD, (1.9, 0.1, hz + 0.55), (7.1, 0.2, 1.1), drop=("-y",), tone=0.9)
        for x in (-1.45, 1.95, 5.25):
            p.box(WOOD, (x, 1.34, hz + 0.95), (0.16, 2.28, 0.16), bevel=0.02, bevel_axes="y", drop=("-y",))
        p.door((0.6, 0.2, hz), FRONT, 1.0, 2.15, frame_slot=PAINT, leaf_slot=WOOD, glass_slot=GLASS)
    win(p, far, (3.4, 1.5, hz), FRONT, 1.8, 1.3, cols=3)
    win(p, far, (-0.85, 1.5, hz), FRONT, 0.9, 1.2, cols=1)
    win(p, far, (wcx, 1.5, zf), FRONT, 2.2, 1.4, cols=3)
    win(p, far, (wcx, 3.75, zf), FRONT, 0.5, 0.5, cols=1, rows=1, sill=False)
    win(p, far, (-w / 2, 1.5, -1.2), LEFT, 1.2, 1.2)
    win(p, far, (w / 2, 1.5, 0.0), RIGHT, 1.2, 1.2)
    for x in (-3.5, 0.0, 3.5):
        win(p, far, (x, 1.5, -hz), BACK, 1.3, 1.2)
    return p


def garage():
    p = kit.Piece("garage_a", "garage")
    w, d, eave, ridge = 3.8, 5.7, 2.35, 3.0
    hz, top = d / 2, eave + 0.04
    p.box(PLASTER, (0, top / 2, 0), (w, top, d), bevel=0.04, bevel_axes="y", drop=("-y",))
    p.gable_roof((0, 0), d, w, eave, ridge, axis="z", overhang=0.1, gable_overhang=0.15, thickness=0.16,
                 courses=3, ridge_cap=False, fill_slot=PLASTER)
    dw, dh, fr = 2.5, 2.05, 0.12
    for side in (-1, 1):
        p.box(PAINT, (side * (dw / 2 - fr / 2), (dh - fr) / 2, hz + 0.04), (fr, dh - fr, 0.08), drop=("-z", "-y"))
    p.box(PAINT, (0, dh - fr / 2, hz + 0.04), (dw, fr, 0.08), drop=("-z",))
    p.strips(PAINT, (0, (dh - fr) / 2, hz), FRONT, dw - 2 * fr, dh - fr, 6, tones=(1.0, 0.86))
    p.window((w / 2, 1.45, 0.9), RIGHT, 1.0, 0.6, cols=2, rows=1)
    return p


def shed():
    p = kit.Piece("shed_a", "shed", base_rise=0.8)
    w, d, eave, ridge = 2.2, 1.8, 1.85, 2.4
    hz, top = d / 2, eave + 0.04
    p.box(WOOD, (0, 0.1 + (top - 0.1) / 2, 0), (w, top - 0.1, d), boards=0.2, drop=("-y", "+y"))
    p.box(WOOD, (0, 0.05, 0), (w + 0.06, 0.1, d + 0.06), drop=("-y",), tone=0.82)
    p.gable_roof((0, 0), d, w, eave, ridge, axis="z", overhang=0.1, gable_overhang=0.1, thickness=0.08,
                 courses=1, ridge_cap=False, fill_slot=WOOD, roof_tone=0.95)
    p.door((0, 0.1, hz), FRONT, 0.8, 1.62, frame_slot=PAINT, leaf_slot=WOOD, frame=0.08, depth=0.06, panels=False,
           leaf_tone=0.86)
    for y in (0.5, 1.3):
        p.box(WOOD, (0, y, hz + 0.032), (0.62, 0.1, 0.04), drop=("-z",))
    p.window((w / 2, 1.25, 0.0), RIGHT, 0.7, 0.5, cols=2, rows=1, sill=False, frame=0.07)
    return p


def water_tower():
    p = kit.Piece("landmark_water_tower", "landmark", base_rise=2.0)
    a0, a1, y0, y1 = 2.6, 2.0, 0.4, 14.75

    def leg(y):
        return a0 + (a1 - a0) * (y - y0) / (y1 - y0)

    corners = ((-1, -1), (1, -1), (1, 1), (-1, 1))
    for sx, sz in corners:
        p.box(STONE, (sx * a0, 0.25, sz * a0), (1.0, 0.5, 1.0), bevel=0.04, bevel_axes="y", drop=("-y",))
        p.strut(METAL, (sx * a0, y0, sz * a0), (sx * a1, y1, sz * a1), 0.42, bevel=0.03)
    levels = (0.9, 5.2, 9.6, 13.6)
    for i in range(4):
        (sx0, sz0), (sx1, sz1) = corners[i], corners[(i + 1) % 4]

        def at(sx, sz, y):
            return (sx * leg(y), y, sz * leg(y))

        for k in range(len(levels) - 1):
            ya, yb = levels[k], levels[k + 1]
            p.strut(METAL, at(sx0, sz0, ya), at(sx1, sz1, yb), 0.18, drop_ends=True)
            p.strut(METAL, at(sx1, sz1, ya), at(sx0, sz0, yb), 0.18, drop_ends=True)
        for y in levels[1:3]:
            p.strut(METAL, at(sx0, sz0, y), at(sx1, sz1, y), 0.24, drop_ends=True)
    tank = [p.meta(PAINT, 0.9), p.meta(PAINT, 0.95), p.meta(PAINT, 1.0), p.meta(PAINT, 0.86), p.meta(PAINT, 1.0)]
    p.lathe(PAINT, [(0.0, 13.7), (2.3, 14.15), (3.3, 14.8), (3.3, 16.9), (3.3, 17.35), (3.3, 19.9)],
            segments=18, band_metas=tank, cap_bottom=False)
    lid = [p.meta(ROOF, 0.85), p.meta(ROOF, 1.0), p.meta(ROOF, 1.0)]
    p.lathe(ROOF, [(3.62, 19.8), (3.62, 20.05), (0.4, 22.85), (0.0, 22.95)], segments=18, band_metas=lid,
            cap_top=False, cap_meta=p.meta(ROOF, 0.8))
    p.lathe(PAINT, [(0.0, 22.85), (0.2, 23.0), (0.2, 23.35), (0.0, 23.6)], segments=8)
    p.lathe(METAL, [(3.0, 14.55), (4.0, 14.55), (4.0, 14.72), (3.0, 14.72)], segments=18, closed=True, smooth=False)
    p.lathe(METAL, [(3.86, 15.58), (4.0, 15.58), (4.0, 15.72), (3.86, 15.72)], segments=18, closed=True, smooth=False)
    for k in range(6):
        a = 2 * math.pi * (k + 0.5) / 6
        c, s = math.cos(a) * 3.93, math.sin(a) * 3.93
        p.strut(METAL, (c, 14.7, s), (c, 15.62, s), 0.12, drop_ends=True)
    return p


def gable_house(x, w, eave, ridge):
    return [(x - w / 2, 0.0), (x + w / 2, 0.0), (x + w / 2, eave), (x, ridge), (x - w / 2, eave)]


def roof_at(house, x):
    hx, w, eave, ridge = house
    return ridge - (ridge - eave) * abs(x - hx) / (w / 2)


def stack(house, offset, rise, width=0.9):
    x = house[0] + offset
    top = max(roof_at(house, x - width / 2), roof_at(house, x + width / 2)) + rise
    return [(x - width / 2, house[2] - 0.5), (x + width / 2, house[2] - 0.5), (x + width / 2, top), (x - width / 2, top)]


def crown(x, w, h, base=2.0):
    return [(x - w / 2, base), (x + w / 2, base), (x + w / 2, h * 0.62), (x + w * 0.3, h * 0.91), (x, h),
            (x - w * 0.3, h * 0.91), (x - w / 2, h * 0.62)]


def block(x, w, h):
    return [(x - w / 2, 0.0), (x + w / 2, 0.0), (x + w / 2, h), (x - w / 2, h)]


def dome(x, w, h, steps=8):
    return [(x + w / 2 * math.cos(math.pi * k / steps), h * math.sin(math.pi * k / steps)) for k in range(steps + 1)]


def card_layers(p, height, layers, step=0.03):
    for z, bottom, top, shapes in layers:
        colour = (bottom, top, 0.0, height)
        sign = 1.0 if z >= 0 else -1.0
        for k, shape in enumerate(shapes):
            depth = z + sign * k * step
            p.polygon(CARD, [(x, y, depth) for x, y in shape], FRONT, rgb=colour)


def card_roofline():
    p = kit.Piece("card_roofline_a", "card", base_rise=0.0)
    back_houses = [(-34.0, 9.0, 7.5, 10.5), (-20.0, 8.0, 7.0, 9.8), (-3.0, 10.0, 8.0, 11.0), (12.0, 9.0, 7.2, 10.4),
                   (27.0, 8.0, 6.8, 9.6)]
    back = [block(0.0, 80.0, 5.0)] + [gable_house(*h) for h in back_houses]
    back += [block(-11.5, 6.0, 12.5), block(35.0, 7.0, 11.0)]
    back += [crown(-27.0, 6.0, 12.5), crown(4.0, 7.0, 13.0), crown(20.0, 6.0, 14.0)]
    back += [stack(back_houses[0], -1.5, 1.3), stack(back_houses[2], 2.0, 1.4), stack(back_houses[3], 1.5, 1.2)]
    front_houses = [(-36.0, 7.0, 4.6, 7.0), (-25.0, 8.0, 4.8, 7.4), (-9.0, 7.0, 4.4, 6.8), (6.0, 8.0, 5.0, 7.6),
                    (22.0, 7.0, 4.6, 7.1), (33.0, 8.0, 4.8, 7.3)]
    front = [block(0.0, 80.0, 3.2)] + [gable_house(*h) for h in front_houses]
    front += [crown(-30.5, 5.0, 8.8, 1.5), crown(-16.5, 6.0, 9.2, 1.5), crown(-1.0, 5.0, 8.4, 1.5),
              crown(14.0, 6.0, 9.0, 1.5), crown(27.5, 4.0, 8.6, 1.5), crown(38.0, 4.0, 7.6, 1.5)]
    front += [stack(front_houses[1], 1.5, 0.9, 0.7), stack(front_houses[3], -1.8, 1.0, 0.7),
              stack(front_houses[5], 1.6, 0.9, 0.7)]
    card_layers(p, 14.0, [(-0.25, (0.75, 0.81, 0.82), (0.62, 0.71, 0.73), back),
                          (0.25, (0.63, 0.71, 0.68), (0.49, 0.59, 0.56), front)])
    return p


def card_hills():
    p = kit.Piece("card_hills_a", "card", base_rise=0.0)
    back = [block(0.0, 160.0, 8.0), dome(-52.0, 56.0, 23.0), dome(0.0, 70.0, 30.0), dome(52.0, 56.0, 25.0),
            dome(-26.0, 40.0, 19.0), dome(27.0, 40.0, 20.0)]
    front = [block(0.0, 160.0, 5.0), dome(-62.0, 36.0, 14.0), dome(-30.0, 44.0, 17.0), dome(2.0, 38.0, 13.0),
             dome(34.0, 46.0, 16.0), dome(64.0, 32.0, 12.0)]
    card_layers(p, 30.0, [(-0.25, (0.7, 0.78, 0.67), (0.6, 0.7, 0.58), back),
                          (0.25, (0.58, 0.69, 0.5), (0.48, 0.61, 0.41), front)])
    return p


def build():
    entries = []
    for name, fn in (("house_detached_a", house_a), ("house_detached_b", house_b), ("house_detached_c", house_c),
                     ("house_bungalow_a", bungalow)):
        main = fn(False)
        obj = main.finish()
        far = fn(True)
        far_obj = far.finish(offset=main.offset)
        tris, target = TARGETS[name]
        entries.append({"id": name, "family": "house", "obj": obj, "max": tris, "target": target, "far": name + "_far"})
        entries.append({"id": name + "_far", "family": "far", "obj": far_obj, "max": FAR_TRIS, "target": target})
    for name, family, fn in (("garage_a", "garage", garage), ("shed_a", "shed", shed),
                             ("landmark_water_tower", "landmark", water_tower)):
        tris, target = TARGETS[name]
        entries.append({"id": name, "family": family, "obj": fn().finish(), "max": tris, "target": target})
    for name, fn in (("card_roofline_a", card_roofline), ("card_hills_a", card_hills)):
        tris, target = TARGETS[name]
        entries.append({"id": name, "family": "card", "obj": fn().finish(), "max": tris, "target": target,
                        "card": True, "fbx": FBX_CARDS})
    return entries


def check(entries):
    failures = []
    for e in entries:
        try:
            e["report"] = kit.check_piece(e["obj"], e["max"], e["target"])
        except RuntimeError as error:
            failures.append(str(error))
    if failures:
        raise RuntimeError("self-check failed:\n" + "\n".join(failures))


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--check-only", action="store_true")
    parser.add_argument("--no-render", action="store_true")
    parser.add_argument("--samples", type=int, default=24)
    args = parser.parse_args(sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else [])
    kit.clean_scene()
    kit.slot_materials()
    entries = build()
    check(entries)
    print("E2_SELF_CHECK PASS " + json.dumps({e["id"]: e["report"]["tris"] for e in entries}), flush=True)
    if args.check_only:
        return
    repo = kit.REPO
    buildings = [e for e in entries if not e.get("card")]
    cards = [e for e in entries if e.get("card")]
    kit.export_fbx(repo / FBX_BUILDINGS, [e["obj"] for e in buildings])
    kit.export_fbx(repo / FBX_CARDS, [e["obj"] for e in cards])
    manifest = [kit.manifest_entry(e["report"], e["family"], e.get("far"), e.get("fbx")) for e in entries]
    kit.write_manifest(repo / MANIFEST, "buildings", FBX_BUILDINGS, manifest)
    if not args.no_render:
        lineup = ["house_detached_a", "house_detached_b", "house_detached_c", "house_bungalow_a", "garage_a", "shed_a",
                  "landmark_water_tower"]
        sheet = [{"id": e["id"], "obj": e["obj"], "tris": e["report"]["tris"], "card": e.get("card", False)}
                 for e in entries]
        kit.render_sheet(sheet, repo / SHEET, samples=args.samples, lineup=lineup)
    reports = [e["report"] for e in entries]
    by_id = {m["id"]: m for m in manifest}
    authored = {e["id"]: e["report"] for e in entries}
    problems, rows = kit.verify_fbx(repo / FBX_BUILDINGS, [by_id[e["id"]] for e in buildings], authored)
    more, card_rows = kit.verify_fbx(repo / FBX_CARDS, [by_id[e["id"]] for e in cards], authored)
    problems += more
    receipt = {
        "generator": "Tools/AssetPipeline/build_surroundings_buildings.py",
        "blender": bpy.app.version_string,
        "scripts": {name: kit.sha256(Path(__file__).resolve().parent / name)
                    for name in ("build_surroundings_buildings.py", "surroundings_kit.py")},
        "axes": "authoring: Unity (x, y, z) = Blender (-x, z, -y), front +Z = Blender -Y; export turns meshes 180 deg about Blender Z and bakes the space transform (identity nodes); file: Unity = Blender (x, z, y)",
        "vertex_colours": "Col, byte, face corner, sRGB; RGB shading 0.75..1.0 (bk_card: colour); A sway, 0 here",
        "outputs": {path: kit.sha256(repo / path) for path in (FBX_BUILDINGS, FBX_CARDS, MANIFEST)},
        "sheet": SHEET if not args.no_render else None,
        "pieces": reports,
        "fbx_verify": rows + card_rows,
        "passed": not problems,
        "problems": problems,
    }
    (repo / RECEIPT).parent.mkdir(parents=True, exist_ok=True)
    (repo / RECEIPT).write_text(json.dumps(receipt, indent=2) + "\n", encoding="utf-8", newline="\n")
    if problems:
        raise RuntimeError("FBX verification failed:\n" + "\n".join(problems))
    print("E2_BUILDINGS_READY " + json.dumps({"pieces": len(entries), "tris": sum(r["tris"] for r in reports)}),
          flush=True)


if __name__ == "__main__":
    main()
