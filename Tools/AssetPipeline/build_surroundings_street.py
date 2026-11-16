import argparse
import json
import math
import sys
from pathlib import Path

REPO = Path(__file__).resolve().parents[2]
KIT_DIR = "Assets/_Project/Art/Surroundings/Kit"
MANIFEST = "Assets/_Project/Data/Surroundings/kit_street.json"
SHEET_DIR = "BuildNotes/surroundings/kit"
GROUPS = {"street": "Street", "nature": "Nature", "vehicles": "Vehicles", "garden": "Garden"}
SLOTS = {"bk_plaster": "E9DCC6", "bk_brick": "B5735A", "bk_stone": "CFC6B8", "bk_roof": "A9573F", "bk_wood": "B88A5E",
    "bk_paint": "FFFFFF", "bk_metal": "4C4F56", "bk_glass_dark": "4F6A7A", "bk_glass_lit": "FFD9A0", "bk_leaf": "7FA06A",
    "bk_bark": "7A5A44", "bk_grass": "8DAA78", "bk_road": "6E6E72", "bk_line": "ECE8DF", "bk_card": "FFFFFF"}
ROAD_TOP, PAVE_TOP, KERB = .02, .14, .12
TAU = 2 * math.pi
UP = (0., 1., 0.)


def add(a, b):
    return (a[0] + b[0], a[1] + b[1], a[2] + b[2])


def sub(a, b):
    return (a[0] - b[0], a[1] - b[1], a[2] - b[2])


def mul(a, s):
    return (a[0] * s, a[1] * s, a[2] * s)


def dot(a, b):
    return a[0] * b[0] + a[1] * b[1] + a[2] * b[2]


def cross(a, b):
    return (a[1] * b[2] - a[2] * b[1], a[2] * b[0] - a[0] * b[2], a[0] * b[1] - a[1] * b[0])


def length(a):
    return math.sqrt(dot(a, a))


def unit(a):
    n = length(a)
    return mul(a, 1 / n) if n > 1e-12 else (0., 0., 0.)


def mean(points):
    n = len(points)
    return (sum(p[0] for p in points) / n, sum(p[1] for p in points) / n, sum(p[2] for p in points) / n)


def clamp(x, lo=0., hi=1.):
    return max(lo, min(hi, x))


def ease(t):
    t = clamp(t)
    return t * t * (3 - 2 * t)


def corner(spec, p):
    if callable(spec):
        spec = spec(p)
    if isinstance(spec, tuple):
        return (round(float(spec[0]), 4), round(float(spec[1]), 4))
    return (round(float(spec), 4), 0.)


def ramp(y0, y1, v0, v1, sway=None):
    def shade(p):
        return (v0 + (v1 - v0) * ease((p[1] - y0) / (y1 - y0)), sway(p) if sway else 0.)
    return shade


class Mesh:
    def __init__(self, pid):
        self.id, self.v, self.f, self.joins = pid, [], [], []

    def add(self, p):
        self.v.append((float(p[0]), float(p[1]), float(p[2])))
        return len(self.v) - 1

    def normal(self, idx):
        n = [0., 0., 0.]
        for i in range(len(idx)):
            a, b = self.v[idx[i]], self.v[idx[(i + 1) % len(idx)]]
            n[0] += (a[1] - b[1]) * (a[2] + b[2])
            n[1] += (a[2] - b[2]) * (a[0] + b[0])
            n[2] += (a[0] - b[0]) * (a[1] + b[1])
        return tuple(n)

    def poly(self, idx, slot, shade, hint=None):
        idx = list(idx)
        if hint is not None:
            if callable(hint):
                hint = hint(mean([self.v[i] for i in idx]))
            if dot(self.normal(idx), hint) < 0:
                idx = [idx[0]] + idx[:0:-1]
        cols = {i: corner(shade, self.v[i]) for i in idx}
        for k in range(1, len(idx) - 1):
            a, b, c = idx[0], idx[k], idx[k + 1]
            self.f.append((a, b, c, slot, (cols[a], cols[b], cols[c])))

    def merge(self, other, fn=lambda p: p):
        base = len(self.v)
        self.v.extend(fn(p) for p in other.v)
        self.f.extend((a + base, b + base, c + base, s, cols) for a, b, c, s, cols in other.f)

    def used(self):
        return sorted({i for f in self.f for i in f[:3]})

    def bounds(self):
        pts = [self.v[i] for i in self.used()]
        return tuple(min(p[k] for p in pts) for k in range(3)), tuple(max(p[k] for p in pts) for k in range(3))

    def transform(self, fn):
        self.v = [fn(p) for p in self.v]

    def centre(self):
        lo, hi = self.bounds()
        dx, dz = (lo[0] + hi[0]) / 2, (lo[2] + hi[2]) / 2
        self.transform(lambda p: (p[0] - dx, p[1], p[2] - dz))
        self.joins = [(x - dx, z - dz, yaw) for x, z, yaw in self.joins]

    def fit(self, width=None, height=None, depth=None):
        lo, hi = self.bounds()
        sx = width / (hi[0] - lo[0]) if width else 1.
        sz = depth / (hi[2] - lo[2]) if depth else 1.
        sy = height / hi[1] if height else 1.
        self.transform(lambda p: (p[0] * sx, p[1] * sy, p[2] * sz))

    def slots(self):
        return sorted({f[3] for f in self.f})


def box(m, lo, hi, slot, shade, skip=()):
    (x0, y0, z0), (x1, y1, z1) = lo, hi
    sides = (("+x", ((x1, y0, z0), (x1, y1, z0), (x1, y1, z1), (x1, y0, z1)), (1, 0, 0)),
             ("-x", ((x0, y0, z0), (x0, y0, z1), (x0, y1, z1), (x0, y1, z0)), (-1, 0, 0)),
             ("+y", ((x0, y1, z0), (x0, y1, z1), (x1, y1, z1), (x1, y1, z0)), (0, 1, 0)),
             ("-y", ((x0, y0, z0), (x1, y0, z0), (x1, y0, z1), (x0, y0, z1)), (0, -1, 0)),
             ("+z", ((x0, y0, z1), (x1, y0, z1), (x1, y1, z1), (x0, y1, z1)), (0, 0, 1)),
             ("-z", ((x0, y0, z0), (x0, y1, z0), (x1, y1, z0), (x1, y0, z0)), (0, 0, -1)))
    for key, pts, n in sides:
        if key not in skip:
            m.poly([m.add(p) for p in pts], slot, shade, n)


def hexa(m, pts, slot, shade, skip=()):
    c = mean(pts)
    faces = (("bottom", (0, 1, 2, 3)), ("top", (4, 5, 6, 7)), ("s0", (0, 1, 5, 4)), ("s1", (1, 2, 6, 5)),
             ("s2", (2, 3, 7, 6)), ("s3", (3, 0, 4, 7)))
    for key, order in faces:
        if key not in skip:
            m.poly([m.add(pts[i]) for i in order], slot, shade, lambda cen: sub(cen, c))


AXES = {"x": ((1, 0, 0), (0, 0, 1), (0, 1, 0)), "-x": ((-1, 0, 0), (0, 1, 0), (0, 0, 1)),
        "y": ((0, 1, 0), (1, 0, 0), (0, 0, 1)), "-y": ((0, -1, 0), (0, 0, 1), (1, 0, 0)),
        "z": ((0, 0, 1), (0, 1, 0), (1, 0, 0)), "-z": ((0, 0, -1), (1, 0, 0), (0, 1, 0))}


def lathe(m, centre, profile, sectors, slot, shade, axis="y", phase=0., scale=(1., 1.), face=None):
    a, e1, e2 = AXES[axis]
    rings = []
    for t, r in profile:
        base = add(centre, mul(a, t))
        if r <= 1e-9:
            rings.append([m.add(base)])
            continue
        ring = []
        for j in range(sectors):
            ang = phase + TAU * j / sectors
            ring.append(m.add(add(base, add(mul(e1, r * scale[0] * math.cos(ang)), mul(e2, r * scale[1] * math.sin(ang))))))
        rings.append(ring)
    for k in range(len(rings) - 1):
        lo, hi = rings[k], rings[k + 1]
        for j in range(sectors):
            s, sh = face(k, j) if face else (slot, shade)
            j2 = (j + 1) % sectors
            if len(lo) == 1 and len(hi) == 1:
                continue
            if len(lo) == 1:
                m.poly([lo[0], hi[j], hi[j2]], s, sh)
            elif len(hi) == 1:
                m.poly([lo[j], hi[0], lo[j2]], s, sh)
            else:
                m.poly([lo[j], hi[j], hi[j2], lo[j2]], s, sh)
    return rings


def loft(m, rings, slot, shade, closed=False, caps=(False, False), face=None, cap=None):
    idx = [[m.add(p) for p in ring] for ring in rings]
    centres = [mean(r) for r in rings]
    n = len(rings[0])
    for k in range(len(rings) - 1):
        c = mean([centres[k], centres[k + 1]])
        for i in range(n if closed else n - 1):
            j = (i + 1) % n
            s, sh = face(k, i) if face else (slot, shade)
            m.poly([idx[k][i], idx[k][j], idx[k + 1][j], idx[k + 1][i]], s, sh, lambda cen, c=c: sub(cen, c))
    along = unit(sub(centres[-1], centres[0]))
    cs, csh = cap if cap else (slot, shade)
    if caps[0]:
        m.poly(idx[0], cs, csh, mul(along, -1))
    if caps[1]:
        m.poly(idx[-1], cs, csh, along)
    return idx


def plane_point(axis, t, u, v):
    return {"x": (t, u, v), "y": (u, t, v), "z": (u, v, t)}[axis]


def prism(m, profile, axis, t0, t1, slot, shade, caps=(True, True), face=None):
    rings = [[plane_point(axis, t, u, v) for u, v in profile] for t in (t0, t1)]
    return loft(m, rings, slot, shade, closed=True, caps=caps, face=face)


def tube(m, p0, p1, r0, r1, sectors, slot, shade, phase=0.):
    d = unit(sub(p1, p0))
    helper = (0., 1., 0.) if abs(d[1]) < .9 else (1., 0., 0.)
    e1 = unit(cross(d, helper))
    e2 = cross(d, e1)
    angles = [phase + TAU * j / sectors for j in range(sectors)]
    rings = [[add(p, add(mul(e1, r * math.cos(a)), mul(e2, r * math.sin(a)))) for a in angles] for p, r in ((p0, r0), (p1, r1))]
    return loft(m, rings, slot, shade, closed=True)


def cord(m, points, r, sectors, slot, shade):
    rings = []
    for k, p in enumerate(points):
        a, b = points[max(0, k - 1)], points[min(len(points) - 1, k + 1)]
        d = unit(sub(b, a))
        e1 = unit(cross(d, (0., 1., 0.)))
        e2 = cross(d, e1)
        rings.append([add(p, add(mul(e1, r * math.cos(TAU * j / sectors)), mul(e2, r * math.sin(TAU * j / sectors)))) for j in range(sectors)])
    return loft(m, rings, slot, shade, closed=True)


def blob(m, centre, radii, sectors, rings, slot, shade, floor=None, phase=0.):
    cx, cy, cz = centre
    rx, ry, rz = radii
    profile = []
    for k in range(rings + 1):
        th = -math.pi / 2 + math.pi * k / rings
        t = ry * math.sin(th)
        if floor is not None:
            t = max(t, floor - cy)
        profile.append((t, 0. if k in (0, rings) else math.cos(th)))
    return lathe(m, centre, profile, sectors, slot, shade, "y", phase, (rx, rz))


def octa(m, c, r, slot, shade):
    rx, ry, rz = r
    pts = [add(c, d) for d in ((rx, 0, 0), (-rx, 0, 0), (0, ry, 0), (0, -ry, 0), (0, 0, rz), (0, 0, -rz))]
    idx = [m.add(p) for p in pts]
    for a in (0, 1):
        for b in (2, 3):
            for d in (4, 5):
                m.poly([idx[a], idx[b], idx[d]], slot, shade, lambda cen: sub(cen, c))


def chamfer_ring(cx, cz, hw, hd, ch, y):
    return [(cx + hw, y, cz - hd + ch), (cx + hw, y, cz + hd - ch), (cx + hw - ch, y, cz + hd), (cx - hw + ch, y, cz + hd),
            (cx - hw, y, cz + hd - ch), (cx - hw, y, cz - hd + ch), (cx - hw + ch, y, cz - hd), (cx + hw - ch, y, cz - hd)]


def right_normal(dx, dz):
    n = math.hypot(dx, dz)
    return (dz / n, -dx / n)


def path_line(p0, p1, segments):
    n = right_normal(p1[0] - p0[0], p1[1] - p0[1])
    return [((p0[0] + (p1[0] - p0[0]) * i / segments, p0[1] + (p1[1] - p0[1]) * i / segments), n) for i in range(segments + 1)]


def path_arc(c, r, a0, a1, segments):
    sign = 1 if a1 > a0 else -1
    out = []
    for i in range(segments + 1):
        a = a0 + (a1 - a0) * i / segments
        out.append(((c[0] + r * math.cos(a), c[1] + r * math.sin(a)), right_normal(-sign * math.sin(a), sign * math.cos(a))))
    return out


def kerb_strip(m, path, width, top, shades, back=None, kerb_shades=(1., .94)):
    rows = []
    for (x, z), (nx, nz) in path:
        rows.append((m.add((x, 0., z)), m.add((x, PAVE_TOP, z)), m.add((x + nx * KERB, PAVE_TOP, z + nz * KERB)),
                     m.add((x + nx * width, PAVE_TOP, z + nz * width)), m.add((x + nx * width, 0., z + nz * width))))
    face_shade = ramp(0., PAVE_TOP, .8, .95)
    for i in range(len(rows) - 1):
        (a0, b0, c0, d0, e0), (a1, b1, c1, d1, e1) = rows[i], rows[i + 1]
        n0, n1 = path[i][1], path[i + 1][1]
        n = unit((n0[0] + n1[0], 0., n0[1] + n1[1]))
        alt = i % 2
        m.poly([a0, a1, b1, b0], "bk_stone", face_shade, mul(n, -1))
        m.poly([b0, b1, c1, c0], "bk_stone", kerb_shades[alt], UP)
        m.poly([c0, c1, d1, d0], top, shades[alt], UP)
        m.poly([d0, d1, e1, e0], back or top, .8, n)
    for row, (p, q, sign) in ((rows[0], (path[0][0], path[1][0], -1)), (rows[-1], (path[-2][0], path[-1][0], 1))):
        t = unit((q[0] - p[0], 0., q[1] - p[1]))
        m.poly(list(row), back or top, .8, mul(t, sign))


def joins_of(m, *items):
    m.joins = [(float(x), float(z), float(yaw)) for x, z, yaw in items]


GUTTER, LANE = .84, .93
ROAD_COLUMNS = (3.5, 3.2, .075, -.075, -3.2, -3.5)


def road_straight(m):
    zs = [-4., -3., -1., 1., 3., 4.]

    def p(x, z):
        return m.add((x, ROAD_TOP, z))
    for x0, x1 in ((-3.5, -3.2), (3.2, 3.5)):
        m.poly([p(x0, -4.), p(x1, -4.), p(x1, 4.), p(x0, 4.)], "bk_road", GUTTER, UP)
    for x in (-3.5, 3.5):
        m.poly([m.add((x, 0., -4.)), m.add((x, 0., 4.)), m.add((x, ROAD_TOP, 4.)), m.add((x, ROAD_TOP, -4.))], "bk_road", .8, (x, 0, 0))
    for outer, inner in ((-3.2, -.075), (3.2, .075)):
        m.poly([p(outer, -4.)] + [p(inner, z) for z in zs] + [p(outer, 4.)], "bk_road", LANE, UP)
    for z0, z1 in zip(zs, zs[1:]):
        dash = z0 in (-3., 1.)
        m.poly([p(-.075, z0), p(.075, z0), p(.075, z1), p(-.075, z1)], "bk_line" if dash else "bk_road", 1. if dash else LANE, UP)
    joins_of(m, (0, -4, 180), (0, 4, 0))


def road_bend(m):
    cx, cz, segments = 5.5, -5.5, 12
    radii = [4., 4.3, 7.425, 7.575, 10.7, 11.]
    grid = []
    for i in range(segments + 1):
        th = math.pi - (math.pi / 2) * i / segments
        grid.append([m.add((cx + r * math.cos(th), ROAD_TOP, cz + r * math.sin(th))) for r in radii])
    for i in range(segments):
        dash = i % 4 in (1, 2)
        for j in range(5):
            if j in (0, 4):
                slot, shade = "bk_road", GUTTER
            elif j == 2 and dash:
                slot, shade = "bk_line", 1.
            else:
                slot, shade = "bk_road", LANE
            m.poly([grid[i][j], grid[i][j + 1], grid[i + 1][j + 1], grid[i + 1][j]], slot, shade, UP)
        for j, sign in ((0, -1), (5, 1)):
            a, b = grid[i][j], grid[i + 1][j]
            mid = mean([m.v[a], m.v[b]])
            out = unit((mid[0] - cx, 0., mid[2] - cz))
            m.poly([m.add((m.v[a][0], 0., m.v[a][2])), m.add((m.v[b][0], 0., m.v[b][2])), b, a], "bk_road", .8, mul(out, sign))
    joins_of(m, (-2, -5.5, 180), (5.5, 2, 90))


def circle_path():
    big, rho, half = 8.8, 3., 3.5
    zf = -math.sqrt((big + rho) ** 2 - (half + rho) ** 2)
    cf = (half + rho, zf)
    tx, tz = cf[0] * big / (big + rho), cf[1] * big / (big + rho)
    alpha = math.atan2(tz - cf[1], tx - cf[0])
    phi0 = math.atan2(tz, tx)
    right = path_line((half, -11.), (half, zf), 1) + path_arc(cf, rho, math.pi, alpha, 4)[1:] + path_arc((0., 0.), big, phi0, math.pi / 2, 10)[1:]
    left = [((-x, z), (-nx, nz)) for (x, z), (nx, nz) in reversed(right[:-1])]
    return right + left


def road_circle(m):
    path = circle_path()
    kerb_strip(m, path, 2.2, "bk_stone", (.9, .86))
    centre = m.add((0., ROAD_TOP, 0.))
    rim = [m.add((x, ROAD_TOP, z)) for (x, z), _ in path] + [m.add((x, ROAD_TOP, -11.)) for x in (-3.2, -.075, .075, 3.2)]

    def asphalt(p):
        return .95 - .07 * clamp(math.hypot(p[0], p[2]) / 8.8)
    for i in range(len(rim)):
        m.poly([centre, rim[i], rim[(i + 1) % len(rim)]], "bk_road", asphalt, UP)
    joins_of(m, (0, -11, 180))


def pavement_straight(m):
    kerb_strip(m, path_line((-1.1, -4.), (-1.1, 4.), 8), 2.2, "bk_stone", (.9, .86))
    joins_of(m, (0, -4, 180), (0, 4, 0))


def pavement_corner(m):
    c = (2., -2.)
    kerb_strip(m, path_arc(c, 4., math.pi, math.pi / 2, 12), 2.2, "bk_stone", (.9, .86))
    joins_of(m, (-.9, -2, 180), (2, .9, 90))


def pavement_bend_outer(m):
    c = (6.6, -6.6)
    kerb_strip(m, path_arc(c, 11., math.pi / 2, math.pi, 12), 2.2, "bk_stone", (.9, .86))
    joins_of(m, (6.6, 5.5, 90), (-5.5, -6.6, 180))


def verge(m):
    kerb_strip(m, path_line((-1., -4.), (-1., 4.), 1), 2., "bk_grass", (.95, .95), back="bk_grass", kerb_shades=(.98, .98))
    joins_of(m, (0, -4, 180), (0, 4, 0))


def driveway(m):
    xs, zs = (-1.5, -1.3, 1.3, 1.5), (-3., -2.8, 2.8, 3.)
    grid = [[m.add((x, PAVE_TOP, z)) for x in xs] for z in zs]
    for j in range(3):
        for i in range(3):
            inner = i == 1 and j == 1
            m.poly([grid[j][i], grid[j][i + 1], grid[j + 1][i + 1], grid[j + 1][i]], "bk_road" if inner else "bk_stone",
                   .9 if inner else (1. if (i + j) % 2 else .95), UP)
    for fixed, along, n in ((3., xs, (0, 0, 1)), (-3., xs, (0, 0, -1))):
        idx = [m.add((-1.5, 0., fixed)), m.add((1.5, 0., fixed))] + [m.add((x, PAVE_TOP, fixed)) for x in reversed(along)]
        m.poly(idx, "bk_stone", .82, n)
    for fixed, n in ((1.5, (1, 0, 0)), (-1.5, (-1, 0, 0))):
        idx = [m.add((fixed, 0., -3.)), m.add((fixed, 0., 3.))] + [m.add((fixed, PAVE_TOP, z)) for z in reversed(zs)]
        m.poly(idx, "bk_stone", .82, n)
    joins_of(m, (0, 3, 0))


HEDGE_HALF = ((.33, 0.), (.375, .5), (.36, 1.22), (.26, 1.385), (0., 1.41))
HEDGE_PROFILE = HEDGE_HALF + tuple((-z, y) for z, y in reversed(HEDGE_HALF[:-1]))
HEDGE_LUMP = (0., .035, .04, .035, .03, .035, .04, .035, 0.)


def profile_normals(profile):
    out = []
    for k in range(len(profile)):
        a, b = profile[max(0, k - 1)], profile[min(len(profile) - 1, k + 1)]
        dz, dy = b[0] - a[0], b[1] - a[1]
        n = math.hypot(dz, dy)
        out.append((dy / n, -dz / n))
    return out


HEDGE_NORMALS = profile_normals(HEDGE_PROFILE)


def key(p):
    return (round(p[0], 5), round(p[1], 5), round(p[2], 5))


def hedge_ring(x, wave, tones, sz=1., sy=1.):
    pts = []
    for k, ((z, y), (nz, ny)) in enumerate(zip(HEDGE_PROFILE, HEDGE_NORMALS)):
        bump = wave * HEDGE_LUMP[k] * (1 if k % 2 == 0 else -1)
        p = (x, (y + ny * bump) * sy, (z + nz * bump) * sz)
        tones[key(p)] = bump
        pts.append(p)
    return pts


def hedge_shader(tones):
    def shade(p):
        k = clamp(p[1] / 1.44)
        return (clamp(.76 + .24 * ease(k) + 1.2 * tones.get(key(p), 0.), .76, 1.), .15 * k * k)
    return shade


def hedge_2m(m):
    tones = {}
    rings = [hedge_ring(-1 + .25 * k, round(math.sin(TAU * (-1 + .25 * k)), 6), tones) for k in range(9)]
    shade = hedge_shader(tones)
    idx = loft(m, rings, "bk_leaf", shade)
    m.poly(idx[0], "bk_leaf", shade, (-1, 0, 0))
    m.poly(idx[-1], "bk_leaf", shade, (1, 0, 0))
    joins_of(m, (-1, 0, 270), (1, 0, 90))


def hedge_end(m):
    tones = {}
    ds = (0., .12, .22, .30, .355, .39)
    waves = (0., .7, -.7, .6, -.4, 0.)
    rings = [hedge_ring(d, w, tones, math.sqrt(max(0., 1 - (d / .4) ** 2)), 1 - .25 * (d / .4) ** 2) for d, w in zip(ds, waves)]
    shade = hedge_shader(tones)
    idx = loft(m, rings, "bk_leaf", shade)
    m.poly(idx[0], "bk_leaf", shade, (-1, 0, 0))
    m.poly(idx[-1], "bk_leaf", shade, (1, 0, 0))
    m.centre()
    lo, _ = m.bounds()
    joins_of(m, (lo[0], 0, 270))


def paint_shade(top):
    return ramp(0., top, .84, 1.)


def half_post(m, outer, inner, top, apex, slot, shade, depth=.05):
    ins = ((outer + inner) / 2, top - .1, 0.)
    hint = lambda cen: sub(cen, ins)
    m.poly([m.add(p) for p in ((outer, 0, depth), (inner, 0, depth), (inner, top, depth), (outer, top, depth))], slot, shade, (0, 0, 1))
    m.poly([m.add(p) for p in ((outer, 0, -depth), (inner, 0, -depth), (inner, top, -depth), (outer, top, -depth))], slot, shade, (0, 0, -1))
    side = 1 if inner > outer else -1
    m.poly([m.add(p) for p in ((inner, 0, -depth), (inner, 0, depth), (inner, top, depth), (inner, top, -depth))], slot, shade, (side, 0, 0))
    if apex is None:
        m.poly([m.add(p) for p in ((outer, 0, -depth), (outer, 0, depth), (outer, top, depth), (outer, top, -depth))], slot, shade, (-side, 0, 0))
        return
    a = (outer, apex, 0.)
    m.poly([m.add(p) for p in ((outer, 0, -depth), (outer, 0, depth), (outer, top, depth), a, (outer, top, -depth))], slot, shade, (-side, 0, 0))
    m.poly([m.add(p) for p in ((outer, top, depth), (inner, top, depth), a)], slot, shade, hint)
    m.poly([m.add(p) for p in ((outer, top, -depth), (inner, top, -depth), a)], slot, shade, hint)
    m.poly([m.add(p) for p in ((inner, top, depth), (inner, top, -depth), a)], slot, shade, hint)


def picket(m, xc, hw, z0, z1, y0, shoulder, point, slot, shade):
    c = (xc, (y0 + shoulder) / 2, (z0 + z1) / 2)
    hint = lambda cen: sub(cen, c)
    for z, n in ((z1, (0, 0, 1)), (z0, (0, 0, -1))):
        m.poly([m.add(p) for p in ((xc - hw, y0, z), (xc + hw, y0, z), (xc + hw, shoulder, z), (xc, point, z), (xc - hw, shoulder, z))], slot, shade, n)
    for x in (xc - hw, xc + hw):
        m.poly([m.add(p) for p in ((x, y0, z0), (x, y0, z1), (x, shoulder, z1), (x, shoulder, z0))], slot, shade, hint)
        m.poly([m.add(p) for p in ((x, shoulder, z0), (x, shoulder, z1), (xc, point, z1), (xc, point, z0))], slot, shade, hint)


def fence_picket_2m(m):
    shade = paint_shade(1.)
    half_post(m, -1., -.95, .95, 1.05, "bk_paint", shade)
    half_post(m, 1., .95, .95, 1.05, "bk_paint", shade)
    for y0, y1 in ((.22, .29), (.68, .75)):
        box(m, (-.95, y0, -.04), (.95, y1, -.005), "bk_paint", .9, skip=("-y", "+x", "-x"))
    for xc in (-.75, -.45, -.15, .15, .45, .75):
        picket(m, xc, .06, -.005, .035, .05, .9, 1., "bk_paint", shade)
    joins_of(m, (-1, 0, 270), (1, 0, 90))


def fence_board_2m(m):
    def board(i):
        return ramp(0., 1.72, .86 * (.9 if i % 2 else .98), .9 if i % 2 else .98)
    half_post(m, -1., -.94, 1.72, None, "bk_wood", ramp(0., 1.72, .8, .92))
    half_post(m, 1., .94, 1.72, None, "bk_wood", ramp(0., 1.72, .8, .92))
    xs = [-.94 + 1.88 * i / 8 for i in range(9)]
    for z, n in ((.022, (0, 0, 1)), (-.022, (0, 0, -1))):
        bottom = [m.add((x, .04, z)) for x in xs]
        top = [m.add((x, 1.72, z)) for x in xs]
        for i in range(8):
            m.poly([bottom[i], bottom[i + 1], top[i + 1], top[i]], "bk_wood", board(i), n)
    box(m, (-1., 1.72, -.054), (1., 1.8, .054), "bk_wood", .97, skip=("-y",))
    joins_of(m, (-1, 0, 270), (1, 0, 90))


def gate_picket(m):
    shade = paint_shade(1.)
    half_post(m, -.5, -.45, .95, 1.05, "bk_paint", shade)
    half_post(m, .5, .45, .95, 1.05, "bk_paint", shade)
    for y0, y1 in ((.22, .29), (.68, .75)):
        box(m, (-.45, y0, -.04), (.45, y1, -.005), "bk_paint", .9, skip=("-y", "+x", "-x"))
    for xc in (-.3, -.1, .1, .3):
        picket(m, xc, .06, -.005, .035, .05, .9, 1., "bk_paint", shade)
    t = .045
    hexa(m, [(-.36, .29 - t, -.04), (.36, .68 - t, -.04), (.36, .68 - t, -.005), (-.36, .29 - t, -.005),
             (-.36, .29 + t, -.04), (.36, .68 + t, -.04), (.36, .68 + t, -.005), (-.36, .29 + t, -.005)], "bk_paint", .9)
    box(m, (.3, .62, .035), (.42, .68, .05), "bk_metal", .95, skip=("-z",))
    joins_of(m, (-.5, 0, 270), (.5, 0, 90))


def lamp_post(m):
    metal = ramp(0., 4.6, .8, .96)
    lathe(m, (0, 0, 0), ((0., .15), (.1, .15), (.2, .09), (.36, .055)), 8, "bk_metal", metal)
    lathe(m, (0, 0, 0), ((.3, .05), (3.95, .05), (3.98, .08), (4.05, .08), (4.07, .06)), 6, "bk_metal", metal)
    blob(m, (0, 4.22, 0), (.16, .16, .16), 8, 5, "bk_paint", ramp(4.06, 4.38, .9, 1.))
    lathe(m, (0, 0, 0), ((4.34, .07), (4.38, .125), (4.45, .08), (4.6, 0.)), 6, "bk_metal", .9)


def mailbox(m):
    box(m, (-.05, 0., -.05), (.05, .8, .05), "bk_wood", ramp(0., .8, .8, .94), skip=("+y", "-y"))
    box(m, (-.09, .8, -.15), (.09, .86, .15), "bk_wood", .9, skip=("+y",))
    arch = [(.2 * math.cos(math.pi * i / 6), 1.05 + .2 * math.sin(math.pi * i / 6)) for i in range(7)]
    profile = [(.2, .86)] + arch + [(-.2, .86)]
    prism(m, profile, "z", -.18, .18, "bk_paint", ramp(.86, 1.25, .86, 1.))
    door = [(x * .86, 1.04 + (y - 1.04) * .86) for x, y in profile]
    prism(m, door, "z", .18, .2, "bk_paint", .9, caps=(False, True))
    box(m, (-.025, 1.02, .2), (.025, 1.07, .226), "bk_metal", .95, skip=("-z",))
    box(m, (.2, .95, -.06), (.232, 1.2, -.02), "bk_brick", .92)
    box(m, (.2, 1.12, -.06), (.236, 1.21, .08), "bk_brick", 1.)
    m.centre()


def bin_wheelie(m):
    body = ramp(.06, .93, .82, 1.)
    loft(m, [chamfer_ring(0, 0, .25, .28, .05, .06), chamfer_ring(0, 0, .29, .31, .06, .93)], "bk_paint", body, closed=True)
    loft(m, [chamfer_ring(0, .01, .31, .325, .06, .93), chamfer_ring(0, .01, .31, .325, .06, 1.0)], "bk_paint", .92, closed=True, caps=(False, True))
    box(m, (-.22, .9, -.37), (.22, .97, -.31), "bk_paint", .88, skip=("+z",))
    tyre = .9
    wheel = ((-.03, 0.), (-.03, .1), (.03, .1), (.03, 0.))
    lathe(m, (.24, .1, -.25), wheel, 8, "bk_metal", tyre, axis="x")
    lathe(m, (-.24, .1, -.25), wheel, 8, "bk_metal", tyre, axis="-x")
    box(m, (-.18, 0., .16), (.18, .06, .24), "bk_paint", .82, skip=("+y", "-y"))
    m.centre()


def slat(y0, y1, z0, z1, c=.012):
    return [(y0, z0), (y0, z1), (y1 - c, z1), (y1, z1 - c), (y1, z0 + c), (y1 - c, z0)]


def bench_park(m):
    wood = .95
    for zc in (.15, .01, -.13):
        prism(m, slat(.42, .47, zc - .06, zc + .06), "x", -.9, .9, "bk_wood", wood)
    for y0, dz in ((.55, 0.), (.71, -.03)):
        prism(m, [(y0, -.28 + dz), (y0, -.24 + dz), (y0 + .12 - .012, -.24 + dz), (y0 + .12, -.252 + dz), (y0 + .12, -.28 + dz)], "x", -.9, .9, "bk_wood", wood)
    metal = ramp(0., .86, .8, .92)
    for s in (-1, 1):
        x0, x1 = sorted((s * .81, s * .87))
        box(m, (x0, 0., .14), (x1, .62, .2), "bk_metal", metal, skip=("+y", "-y"))
        hexa(m, [(x0, 0., -.22), (x1, 0., -.22), (x1, 0., -.16), (x0, 0., -.16),
                 (x0, .86, -.33), (x1, .86, -.33), (x1, .86, -.27), (x0, .86, -.27)], "bk_metal", metal, skip=("bottom",))
        box(m, (x0 - .005, .62, -.28), (x1 + .005, .67, .25), "bk_metal", metal)
        box(m, (x0, .38, -.2), (x1, .42, .2), "bk_metal", metal)
    m.centre()


def planter_box(m):
    rows = (0., .14, .28, .42)

    def plank(i):
        return ramp(0., .42, .82 if i % 2 == 0 else .86, .9 if i % 2 == 0 else .96)
    for i in range(3):
        y0, y1 = rows[i], rows[i + 1]
        box(m, (-.6, y0, -.25), (.6, y1, .25), "bk_wood", plank(i), skip=("+y", "-y"))
    o, n = (.6, .25), (.55, .2)
    ring_o = [(o[0], .42, o[1]), (-o[0], .42, o[1]), (-o[0], .42, -o[1]), (o[0], .42, -o[1])]
    ring_i = [(n[0], .42, n[1]), (-n[0], .42, n[1]), (-n[0], .42, -n[1]), (n[0], .42, -n[1])]
    io = [m.add(p) for p in ring_o]
    ii = [m.add(p) for p in ring_i]
    for k in range(4):
        m.poly([io[k], io[(k + 1) % 4], ii[(k + 1) % 4], ii[k]], "bk_wood", .97, UP)
    m.poly([m.add((x, .36, z)) for x, _, z in ring_i], "bk_bark", .78, UP)
    low = [m.add((x, .36, z)) for x, _, z in ring_i]
    top = [m.add(p) for p in ring_i]
    for k in range(4):
        a, b = ring_i[k], ring_i[(k + 1) % 4]
        inward = unit((-(a[0] + b[0]) / 2, 0., -(a[2] + b[2]) / 2))
        m.poly([low[k], low[(k + 1) % 4], top[(k + 1) % 4], top[k]], "bk_wood", .78, inward)
    half = ((.2, .36), (.19, .46), (.13, .55), (0., .6))
    prof = half + tuple((-z, y) for z, y in reversed(half[:-1]))
    rings = []
    for k in range(7):
        x = -.52 + .52 * k / 3
        end = .6 if k in (0, 6) else 1.
        wave = .07 * math.sin(TAU * x / .52) if 0 < k < 6 else 0.
        rings.append([(x, .36 + (y - .36) * end * (1 + wave * (1 if j % 2 else -1)), z * end * (1 + wave * .5 * (1 if j % 2 else -1)))
                      for j, (z, y) in enumerate(prof)])
    idx = loft(m, rings, "bk_leaf", ramp(.36, .6, .8, 1.))
    m.poly(idx[0], "bk_leaf", .82, (-1, 0, 0))
    m.poly(idx[-1], "bk_leaf", .82, (1, 0, 0))


def tree(m, trunk, blobs, size, limbs=()):
    trunk_h, trunk_r = trunk
    anchor = (0., trunk_h * .8, 0.)
    lathe(m, (0, 0, 0), ((0., trunk_r * 1.45), (.22, trunk_r), (trunk_h, trunk_r * .7)), 6, "bk_bark", ramp(0., trunk_h, .8, .95))
    for p0, p1, r in limbs:
        tube(m, p0, p1, r, r * .7, 6, "bk_bark", .88)
    for c, r, sectors, rings in blobs:
        def shade(p, c=c, r=r):
            return (.78 + .22 * ease((p[1] - (c[1] - r[1] * .9)) / (r[1] * 1.8)), length(sub(p, anchor)))
        blob(m, c, r, sectors, rings, "bk_leaf", shade, phase=.37 * len(m.f))
    normalise_sway(m)
    m.centre()
    if size:
        m.fit(width=size[0], height=size[1], depth=size[0])
        m.centre()


def normalise_sway(m):
    top = max(c[1] for f in m.f if f[3] == "bk_leaf" for c in f[4])
    out = []
    for a, b, c, s, cols in m.f:
        if s == "bk_leaf":
            cols = tuple((v, round(clamp(w / top) ** 1.15, 4)) for v, w in cols)
        out.append((a, b, c, s, cols))
    m.f = out


def tree_round_a(m):
    tree(m, (3.3, .15), [((0, 4.15, 0), (1.55, 1.35, 1.55), 12, 8), ((.95, 3.55, .45), (1.05, .95, 1.05), 10, 7),
                         ((-.8, 3.7, -.6), (1.0, .9, 1.0), 10, 7)], (4., 6.))


def tree_round_b(m):
    tree(m, (3.6, .2), [((0, 5.2, 0), (1.9, 1.7, 1.9), 12, 8), ((1.35, 4.5, .4), (1.3, 1.15, 1.3), 10, 7),
                        ((-1.1, 4.6, .9), (1.25, 1.1, 1.25), 10, 7), ((-.2, 4.4, -1.35), (1.25, 1.1, 1.25), 10, 7)], (5.5, 7.5),
         limbs=(((0, 2.6, 0), (.9, 3.9, .25), .1), ((0, 2.8, 0), (-.7, 4.0, .5), .09)))


def tree_round_c(m):
    tree(m, (2.4, .11), [((0, 3.2, 0), (1.25, 1.1, 1.25), 12, 8), ((.55, 3.85, .25), (.85, .75, .85), 10, 7)], (3., 4.5))


def tree_poplar(m):
    tree(m, (2.0, .14), [((0, 3.5, 0), (1.05, 1.9, 1.05), 10, 8), ((.08, 5.6, .05), (.9, 1.85, .9), 10, 8),
                         ((0, 7.6, 0), (.6, 1.4, .6), 8, 6)], (2.2, 9.))


def tree_far(m):
    profile = ((0., .2), (2.2, .15), (2.5, 1.7), (4.4, 2.1), (6.2, 1.3), (7.0, 0.))

    def face(k, j):
        if k == 0:
            return "bk_bark", .82
        return "bk_leaf", lambda p: (.8 + .2 * ease((p[1] - 2.2) / 4.8), ease((p[1] - 2.2) / 4.8))
    lathe(m, (0, 0, 0), profile, 6, None, None, face=face)


def bush(m, blobs, size):
    h = size[1]
    for c, r, sectors, rings in blobs:
        blob(m, c, r, sectors, rings, "bk_leaf", lambda p: (.78 + .22 * ease(p[1] / h), .3 * ease(p[1] / h)), floor=-.03, phase=.5 * len(m.f))
    m.centre()
    m.fit(width=size[0], depth=size[0])
    lo, hi = m.bounds()
    m.transform(lambda p: (p[0], -.03 + (p[1] + .03) * (h + .03) / (hi[1] + .03), p[2]))
    m.centre()


def bush_a(m):
    bush(m, [((0, .42, 0), (.5, .45, .5), 8, 5), ((.32, .3, .18), (.33, .3, .33), 8, 5), ((-.28, .32, -.15), (.34, .3, .34), 8, 5)], (1.2, .9))


def bush_b(m):
    bush(m, [((0, .55, 0), (.66, .55, .66), 8, 6), ((.45, .38, .25), (.45, .38, .45), 8, 6), ((-.4, .4, -.2), (.46, .4, .46), 8, 6)], (1.6, 1.1))


def flowerbed_2m(m):
    box(m, (-1., 0., -.4), (1., .12, .4), "bk_stone", ramp(0., .12, .82, .92), skip=("+y", "-y"))
    o = [m.add(p) for p in ((1., .12, .4), (-1., .12, .4), (-1., .12, -.4), (1., .12, -.4))]
    i = [m.add(p) for p in ((.92, .12, .32), (-.92, .12, .32), (-.92, .12, -.32), (.92, .12, -.32))]
    for k in range(4):
        m.poly([o[k], o[(k + 1) % 4], i[(k + 1) % 4], i[k]], "bk_stone", .96, UP)
    m.poly([m.add(p) for p in ((.92, .1, .32), (-.92, .1, .32), (-.92, .1, -.32), (.92, .1, -.32))], "bk_bark", .8, UP)
    lo_i = [m.add(p) for p in ((.92, .1, .32), (-.92, .1, .32), (-.92, .1, -.32), (.92, .1, -.32))]
    hi_i = [m.add(p) for p in ((.92, .12, .32), (-.92, .12, .32), (-.92, .12, -.32), (.92, .12, -.32))]
    for k in range(4):
        a, b = m.v[lo_i[k]], m.v[lo_i[(k + 1) % 4]]
        m.poly([lo_i[k], lo_i[(k + 1) % 4], hi_i[(k + 1) % 4], hi_i[k]], "bk_stone", .84, (-(a[0] + b[0]) / 2, 0., -(a[2] + b[2]) / 2))
    half = ((.3, .08), (.27, .18), (.17, .26), (0., .3))
    prof = half + tuple((-z, y) for z, y in reversed(half[:-1]))
    rings = []
    for k in range(7):
        x = -.88 + .88 * k / 3
        end = .55 if k in (0, 6) else 1.
        wave = .08 * math.sin(TAU * x / .6) if 0 < k < 6 else 0.
        rings.append([(x, .08 + (y - .08) * end * (1 + wave * (1 if j % 2 else -1)), z * (.8 + .2 * end)) for j, (z, y) in enumerate(prof)])
    idx = loft(m, rings, "bk_leaf", ramp(.08, .3, .8, .98))
    m.poly(idx[0], "bk_leaf", .82, (-1, 0, 0))
    m.poly(idx[-1], "bk_leaf", .82, (1, 0, 0))
    for k in range(11):
        x = -.8 + .16 * k
        z = .1 if k % 2 else -.1
        top = .08 + .22 * (1 - (abs(z) / .3) ** 2)
        octa(m, (x, top + .03, z), (.075, .04, .075), "bk_paint", 1. if k % 3 else .9)


def vehicle(m, spec):
    body, rt, cb = spec["body"], .14, .06

    def section(z, hw, yb, yt):
        r = min(rt, (yt - yb) / 2)
        c45 = math.cos(math.pi / 4)
        return [(hw - cb, yb, z), (hw, yb + cb, z), (hw, yt - r, z), (hw - r + r * c45, yt - r + r * c45, z), (hw - r, yt, z),
                (-hw + r, yt, z), (-hw + r - r * c45, yt - r + r * c45, z), (-hw, yt - r, z), (-hw, yb + cb, z), (-hw + cb, yb, z)]
    lower = ramp(.25, 1., .8, 1.)
    loft(m, [section(*s) for s in body], "bk_paint", lower, closed=True, caps=(True, True))
    roles = spec["roles"]
    rings = []
    for y, zf, zb, hw, rc in spec["cabin"]:
        zB = zf - (zf - zb) * spec["pillar"]
        bw = .05
        rings.append([(-hw + rc, y, zf), (hw - rc, y, zf), (hw, y, zf - rc), (hw, y, zB + bw), (hw, y, zB - bw), (hw, y, zb + rc),
                      (hw - rc, y, zb), (-hw + rc, y, zb), (-hw, y, zb + rc), (-hw, y, zB - bw), (-hw, y, zB + bw), (-hw, y, zf - rc)])

    def face(k, i):
        if k == 1 and roles[i] == "glass":
            return "bk_glass_dark", 1. if i in (0, 6) else .92
        return "bk_paint", .97
    idx = loft(m, rings, None, None, closed=True, face=face)
    m.poly(idx[-1], "bk_paint", 1., UP)
    radius, width, xo = spec["wheel"]
    c, hub = .08, radius * .52
    prof = ((-width / 2, radius), (width / 2 - c, radius), (width / 2, radius - c), (width / 2, hub), (width / 2 + .04, 0.))

    def wface(k, j):
        return ("bk_metal", .9) if k < 3 else ("bk_line", .95)
    for z in spec["axles"]:
        for side, axis, phase in ((1, "x", math.radians(18)), (-1, "-x", 0.)):
            lathe(m, (side * (xo - width / 2), radius, z), prof, 10, None, None, axis=axis, phase=phase, face=wface)
    zf, zr = body[-1][0], body[0][0]
    hw = spec["bumper_hw"]
    for z0, z1 in ((zf - .1, zf + .05), (zr - .05, zr + .1)):
        prism(m, slat(.24, .44, z0, z1, .03), "x", -hw, hw, "bk_metal", .9)
    lx0, lx1, ly0, ly1 = spec["lights"]
    for s in (-1, 1):
        x0, x1 = sorted((s * lx0, s * lx1))
        box(m, (x0, ly0, zf - .03), (x1, ly1, zf + .025), "bk_line", 1., skip=("-z",))
        box(m, (x0, ly0 + .02, zr - .025), (x1, ly1 + .04, zr + .03), "bk_brick", .95, skip=("+z",))
    mz, mx = spec["mirror"]
    my = spec["cabin"][0][0]
    for s in (-1, 1):
        x0, x1 = sorted((s * (mx - .1), s * mx))
        box(m, (x0, my + .03, mz - .05), (x1, my + .12, mz + .05), "bk_paint", .95)
    for x0, x1, y, z0, z1 in spec.get("rails", ()):
        box(m, (x0, y, z0), (x1, y + .04, z1), "bk_metal", .9, skip=("-y",))
    m.centre()


GLASS_CAR = ("glass", "pillar", "glass", "pillar", "glass", "pillar", "glass", "pillar", "glass", "pillar", "glass", "pillar")
GLASS_VAN = ("glass", "pillar", "glass", "pillar", "panel", "pillar", "panel", "pillar", "panel", "pillar", "glass", "pillar")


def car_hatch(m):
    vehicle(m, {"body": [(-1.88, .74, .4, .8), (-1.8, .86, .32, .9), (-1.5, .89, .3, .92), (-.5, .9, .3, .93),
                         (.7, .9, .3, .9), (1.35, .89, .3, .84), (1.72, .86, .32, .76), (1.88, .74, .4, .66)],
                "cabin": [(.88, .72, -1.72, .84, .16), (.95, .62, -1.7, .83, .16), (1.34, .2, -1.6, .74, .2), (1.47, .08, -1.52, .66, .24)],
                "pillar": .48, "roles": GLASS_CAR, "wheel": (.37, .26, .92), "axles": (1.25, -1.2), "bumper_hw": .8,
                "lights": (.48, .72, .5, .6), "mirror": (.6, .94)})


def car_estate(m):
    vehicle(m, {"body": [(-2.18, .76, .4, .82), (-2.1, .87, .32, .92), (-1.8, .9, .3, .95), (-.5, .91, .3, .96),
                         (.9, .91, .3, .93), (1.6, .9, .3, .86), (2.02, .87, .32, .77), (2.18, .76, .4, .66)],
                "cabin": [(.91, .92, -2.04, .85, .16), (.98, .82, -2.04, .84, .16), (1.4, .38, -2.0, .75, .2), (1.53, .26, -1.96, .68, .24)],
                "pillar": .38, "roles": GLASS_CAR, "wheel": (.38, .26, .93), "axles": (1.5, -1.45), "bumper_hw": .82,
                "lights": (.5, .74, .5, .6), "mirror": (.8, .96),
                "rails": ((-.52, -.46, 1.53, -1.8, .1), (.46, .52, 1.53, -1.8, .1))})


def van_small(m):
    vehicle(m, {"body": [(-2.34, .92, .4, .9), (-2.28, .98, .34, .95), (1.7, .98, .34, .95), (2.06, .96, .34, .9),
                         (2.28, .9, .36, .82), (2.34, .8, .42, .74)],
                "cabin": [(.92, 1.8, -2.28, .96, .14), (1.0, 1.68, -2.28, .95, .14), (1.68, 1.2, -2.28, .93, .16), (2.08, 1.0, -2.26, .88, .22)],
                "pillar": .2, "roles": GLASS_VAN, "wheel": (.4, .28, 1.0), "axles": (1.6, -1.6), "bumper_hw": .9,
                "lights": (.56, .8, .54, .66), "mirror": (1.62, 1.07)})


def trampoline(m):
    lathe(m, (0, 0, 0), ((.78, 1.5), (.88, 1.5), (.91, 1.41), (.87, 1.31), (.79, 1.3)), 24, "bk_paint", ramp(.78, .91, .84, 1.))
    lathe(m, (0, 0, 0), ((.8, 1.3), (.8, 0.)), 24, "bk_metal", .88)
    for k in range(6):
        a = TAU * (k + .5) / 6
        ca, sa = math.cos(a), math.sin(a)
        tx, tz = -sa * .025, ca * .025

        def at(r, y, s):
            return (ca * r + tx * s, y, sa * r + tz * s)
        hexa(m, [at(1.45, 0., -1), at(1.51, 0., -1), at(1.51, 0., 1), at(1.45, 0., 1),
                 at(1.38, .8, -1), at(1.44, .8, -1), at(1.44, .8, 1), at(1.38, .8, 1)], "bk_metal", ramp(0., .8, .8, .92), skip=("bottom", "top"))


def washing_line(m):
    wood = ramp(0., 1.95, .82, .95)
    for s in (-1, 1):
        x0, x1 = sorted((s * 1.86, s * 1.94))
        box(m, (x0, 0., -.04), (x1, 1.95, .04), "bk_wood", wood, skip=("-y",))
        cx0, cx1 = sorted((s * 1.865, s * 1.935))
        box(m, (cx0, 1.84, -.2), (cx1, 1.9, .2), "bk_wood", .95)
    xs, ys = (-1.86, -.62, .62, 1.86), (1.86, 1.8, 1.8, 1.86)
    for z in (-.15, .15):
        cord(m, [(x, y, z) for x, y in zip(xs, ys)], .015, 3, "bk_metal", .9)

    def line_y(x):
        return 1.86 - .06 * clamp(1 - max(0., abs(x) - .62) / 1.24)

    def cloth(points, z, slot, shade):
        for dz, n in ((.006, (0, 0, 1)), (-.006, (0, 0, -1))):
            m.poly([m.add((x, y, z + dz + w)) for x, y, w in points], slot, shade, n)
    sheet_x = (-1.45, -1.08, -.72, -.35)
    waves = (0., .03, -.02, .01)
    for k in range(3):
        xa, xb = sheet_x[k], sheet_x[k + 1]
        cloth([(xa, 1.05, waves[k]), (xb, 1.05, waves[k + 1]), (xb, line_y(xb) - .02, waves[k + 1]), (xa, line_y(xa) - .02, waves[k])], -.15, "bk_line", .97)
    cloth([(-.2, 1.3, 0.), (.3, 1.3, 0.), (.3, line_y(.3) - .02, 0.), (-.2, line_y(-.2) - .02, 0.)], .15, "bk_brick", .95)
    top = line_y(.8) - .02
    cloth([(.6, 1.25, 0.), (1.05, 1.25, 0.), (1.05, top, 0.), (.6, top, 0.)], .15, "bk_paint", .97)
    cloth([(.6, top - .2, 0.), (.6, top, 0.), (.5, top - .03, 0.), (.47, top - .17, 0.)], .15, "bk_paint", .93)
    cloth([(1.05, top - .2, 0.), (1.18, top - .17, 0.), (1.15, top - .03, 0.), (1.05, top, 0.)], .15, "bk_paint", .93)


def swing_set(m):
    frame = ramp(0., 2.1, .84, 1.)
    for x in (-1.42, 1.42):
        for z in (-.7, .7):
            tube(m, (x, 0., z), (x, 2.1, 0.), .045, .045, 6, "bk_paint", frame)
        x0, x1 = x - .03, x + .03
        box(m, (x0, 0., -.75), (x1, .05, .75), "bk_paint", .86, skip=("-y",))
    lathe(m, (0, 2.1, 0), ((-1.5, 0.), (-1.5, .065), (1.5, .065), (1.5, 0.)), 8, "bk_paint", .97, axis="x")
    for xc in (-.6, .6):
        box(m, (xc - .23, .42, -.11), (xc + .23, .47, .11), "bk_wood", .94)
        for dx in (-.2, .2):
            box(m, (xc + dx - .0125, .47, -.0125), (xc + dx + .0125, 2.06, .0125), "bk_metal", .9, skip=("+y", "-y"))


def chair():
    c = Mesh("chair")
    wood = ramp(0., .84, .82, .96)
    box(c, (-.21, .42, -.21), (.21, .46, .21), "bk_wood", wood)
    box(c, (-.21, .46, -.21), (.21, .84, -.17), "bk_wood", wood)
    for x in (-.17, .17):
        for z in (-.17, .17):
            box(c, (x - .02, 0., z - .02), (x + .02, .42, z + .02), "bk_wood", wood, skip=("+y", "-y"))
    box(c, (-.18, .46, -.17), (.18, .5, .19), "bk_paint", .95, skip=("-y",))
    return c


def garden_table_set(m):
    lathe(m, (0, 0, 0), ((.72, 0.), (.72, .5), (.76, .5), (.76, 0.)), 12, "bk_wood", ramp(.72, .76, .9, 1.))
    lathe(m, (0, 0, 0), ((0., .05), (.72, .04)), 6, "bk_wood", ramp(0., .72, .8, .9))
    box(m, (-.3, 0., -.04), (.3, .05, .04), "bk_wood", .85, skip=("-y",))
    box(m, (-.04, 0., -.3), (.04, .05, .3), "bk_wood", .85, skip=("-y",))
    seat = chair()
    for k in range(4):
        a = TAU * k / 4
        ca, sa = math.cos(a), math.sin(a)

        def place(p, ca=ca, sa=sa):
            x, y, z = p[0], p[1], p[2] - .72
            return (x * ca + z * sa, y, -x * sa + z * ca)
        m.merge(seat, place)
    m.centre()


def sprinkler(m):
    lathe(m, (0, 0, 0), ((0., .15), (.06, .07), (.06, 0.)), 8, "bk_metal", ramp(0., .06, .82, .95))
    lathe(m, (0, 0, 0), ((.05, .022), (.17, .022)), 6, "bk_paint", .92)
    box(m, (-.13, .17, -.025), (.13, .2, .025), "bk_paint", 1.)


PIECES = (
    ("road_straight", "street", "road", road_straight, (7, 8, None), 60, None, 30),
    ("road_bend", "street", "road", road_bend, (11, 11, None), 200, None, 30),
    ("road_circle", "street", "road", road_circle, (22, 22, None), 300, None, 30),
    ("pavement_straight", "street", "pavement", pavement_straight, (2.2, 8, None), 80, None, 30),
    ("pavement_corner", "street", "pavement", pavement_corner, (4, 4, None), 150, None, 30),
    ("pavement_bend_outer", "street", "pavement", pavement_bend_outer, (13.2, 13.2, None), 200, None, 30),
    ("driveway", "street", "driveway", driveway, (3, 6, None), 40, None, 30),
    ("verge", "street", "verge", verge, (2, 8, None), 20, None, 30),
    ("hedge_2m", "street", "hedge", hedge_2m, (2, .8, 1.4), 150, None, 80),
    ("hedge_end", "street", "hedge", hedge_end, (None, .8, 1.4), 100, None, 80),
    ("fence_picket_2m", "street", "fence", fence_picket_2m, (2, .1, 1.), 120, None, 30),
    ("fence_board_2m", "street", "fence", fence_board_2m, (2, .1, 1.8), 60, None, 30),
    ("gate_picket", "street", "fence", gate_picket, (1., .1, 1.), 120, None, 30),
    ("lamp_post", "street", "street", lamp_post, (.3, .3, 4.6), 200, None, 50),
    ("mailbox", "street", "street", mailbox, (.45, .4, 1.25), 150, None, 40),
    ("bin_wheelie", "street", "street", bin_wheelie, (.6, .7, 1.05), 200, None, 50),
    ("bench_park", "street", "street", bench_park, (1.8, .6, .85), 250, None, 40),
    ("planter_box", "street", "street", planter_box, (1.2, .5, .6), 150, None, 60),
    ("tree_round_a", "nature", "tree", tree_round_a, (4, 4, 6), 500, "tree_far", 80),
    ("tree_round_b", "nature", "tree", tree_round_b, (5.5, 5.5, 7.5), 600, "tree_far", 80),
    ("tree_round_c", "nature", "tree", tree_round_c, (3, 3, 4.5), 400, "tree_far", 80),
    ("tree_poplar", "nature", "tree", tree_poplar, (2.2, 2.2, 9), 400, "tree_far", 80),
    ("tree_far", "nature", "far", tree_far, (None, None, 7), 60, None, 80),
    ("bush_a", "nature", "bush", bush_a, (1.2, 1.2, .9), 200, None, 80),
    ("bush_b", "nature", "bush", bush_b, (1.6, 1.6, 1.1), 250, None, 80),
    ("flowerbed_2m", "nature", "flowerbed", flowerbed_2m, (2, .8, .35), 200, None, 80),
    ("car_hatch", "vehicles", "car", car_hatch, (1.8, 3.9, 1.5), 700, None, 50),
    ("car_estate", "vehicles", "car", car_estate, (1.85, 4.5, 1.55), 800, None, 50),
    ("van_small", "vehicles", "van", van_small, (2.0, 4.8, 2.1), 800, None, 50),
    ("trampoline", "garden", "garden", trampoline, (3, 3, .9), 400, None, 50),
    ("washing_line", "garden", "garden", washing_line, (4, .4, 2.), 200, None, 40),
    ("swing_set", "garden", "garden", swing_set, (3, 1.5, 2.2), 300, None, 50),
    ("garden_table_set", "garden", "garden", garden_table_set, (1.8, 1.8, .8), 400, None, 40),
    ("sprinkler", "garden", "garden", sprinkler, (.3, .3, None), 60, None, 50),
)


def build_all():
    out = []
    for pid, group, family, fn, target, tris, far, smooth in PIECES:
        m = Mesh(pid)
        fn(m)
        m.group, m.family, m.target, m.max_tris, m.far, m.smooth = group, family, target, tris, far, smooth
        out.append(m)
    return out


def measure(m):
    lo, hi = m.bounds()
    return {"width": hi[0] - lo[0], "depth": hi[2] - lo[2], "height": hi[1] - lo[1], "lo": lo, "hi": hi}


def edge_points(m, test):
    return sorted({(round(p[0], 4), round(p[1], 4), round(p[2], 4)) for p in (m.v[i] for i in m.used()) if test(p)})


def seam_problems(by):
    problems = []

    def same(name, a, b):
        if a != b:
            problems.append(f"{name}: seam mismatch {len(a)} vs {len(b)} points")
    rs = by["road_straight"]
    lo_end = edge_points(rs, lambda p: abs(p[2] + 4) < 1e-6)
    hi_end = [(x, y, -z) for x, y, z in edge_points(rs, lambda p: abs(p[2] - 4) < 1e-6)]
    same("road_straight ends", lo_end, sorted(hi_end))
    road_profile = sorted({(x, y) for x, y, _ in lo_end})
    bend = by["road_bend"]
    entry = sorted({(round(x + 2, 4), y) for x, y, _ in edge_points(bend, lambda p: abs(p[2] + 5.5) < 1e-6)})
    exit_ = sorted({(round(-(z - 2), 4), y) for _, y, z in edge_points(bend, lambda p: abs(p[0] - 5.5) < 1e-6)})
    same("road_bend entry", entry, road_profile)
    same("road_bend exit", exit_, road_profile)
    neck = sorted({(x, y) for x, y, _ in edge_points(by["road_circle"], lambda p: abs(p[2] + 11) < 1e-6 and abs(p[1] - ROAD_TOP) < 1e-6)})
    same("road_circle neck", neck, [q for q in road_profile if abs(q[1] - ROAD_TOP) < 1e-6])
    ps = by["pavement_straight"]
    cap = sorted({(x, y) for x, y, _ in edge_points(ps, lambda p: abs(p[2] + 4) < 1e-6)})
    cap_hi = sorted({(x, y) for x, y, _ in edge_points(ps, lambda p: abs(p[2] - 4) < 1e-6)})
    same("pavement_straight ends", cap, cap_hi)
    right_neck = sorted({(round(x - 4.6, 4), y) for x, y, _ in edge_points(by["road_circle"], lambda p: abs(p[2] + 11) < 1e-6 and p[0] > 3.4 and abs(p[1] - ROAD_TOP) > 1e-6)})
    same("road_circle right pavement", right_neck, cap)
    corner_end = sorted({(round(x + .9, 4), y) for x, y, _ in edge_points(by["pavement_corner"], lambda p: abs(p[2] + 2) < 1e-6)})
    same("pavement_corner end", corner_end, cap)
    outer_end = sorted({(round(-(x + 5.5), 4), y) for x, y, _ in edge_points(by["pavement_bend_outer"], lambda p: abs(p[2] + 6.6) < 1e-6)})
    same("pavement_bend_outer end", outer_end, cap)
    hedge = by["hedge_2m"]
    a = sorted((y, z) for _, y, z in edge_points(hedge, lambda p: abs(p[0] + 1) < 1e-6))
    b = sorted((y, z) for _, y, z in edge_points(hedge, lambda p: abs(p[0] - 1) < 1e-6))
    same("hedge_2m ends", a, b)
    end = by["hedge_end"]
    lo, _ = end.bounds()
    c = sorted((y, z) for _, y, z in edge_points(end, lambda p: abs(p[0] - lo[0]) < 1e-6))
    same("hedge_end join", c, a)
    for pid in ("fence_picket_2m", "fence_board_2m", "gate_picket"):
        f = by[pid]
        lo, hi = f.bounds()
        a = sorted((y, z) for _, y, z in edge_points(f, lambda p: abs(p[0] - lo[0]) < 1e-6))
        b = sorted((y, z) for _, y, z in edge_points(f, lambda p: abs(p[0] - hi[0]) < 1e-6))
        same(f"{pid} ends", a, b)
    return problems


def check(meshes):
    problems = []
    for m in meshes:
        dims = measure(m)
        if len(m.f) > m.max_tris:
            problems.append(f"{m.id}: {len(m.f)} tris > {m.max_tris}")
        slots = m.slots()
        if len(slots) > 5 or any(s not in SLOTS for s in slots):
            problems.append(f"{m.id}: slots {slots}")
        for key, want in zip(("width", "depth", "height"), m.target):
            if want and abs(dims[key] - want) > want * .1 + 1e-6:
                problems.append(f"{m.id}: {key} {dims[key]:.3f} vs {want}")
        if dims["lo"][1] < -.05 - 1e-9:
            problems.append(f"{m.id}: min y {dims['lo'][1]:.3f}")
        if abs(dims["lo"][0] + dims["hi"][0]) > .002 or abs(dims["lo"][2] + dims["hi"][2]) > .002:
            problems.append(f"{m.id}: pivot not centred {dims['lo']} {dims['hi']}")
        for a, b, c, s, cols in m.f:
            area = length(cross(sub(m.v[b], m.v[a]), sub(m.v[c], m.v[a])))
            if area < 2e-7:
                problems.append(f"{m.id}: degenerate triangle")
                break
            for v, w in cols:
                if not .75 - 1e-6 <= v <= 1. + 1e-6 or not 0. <= w <= 1.:
                    problems.append(f"{m.id}: colour {v} {w} in {s}")
                    break
                if w > 0 and s != "bk_leaf":
                    problems.append(f"{m.id}: sway on {s}")
                    break
        if m.family in ("tree", "far") and max(c[1] for f in m.f if f[3] == "bk_leaf" for c in f[4]) < .999:
            problems.append(f"{m.id}: crown sway never reaches 1")
    problems += seam_problems({m.id: m for m in meshes})
    return problems


def report(meshes):
    for m in meshes:
        d = measure(m)
        print(f"STREET_PIECE {m.id} tris={len(m.f)}/{m.max_tris} w={d['width']:.3f} d={d['depth']:.3f} h={d['height']:.3f} slots={','.join(m.slots())}", flush=True)


def compact(m):
    used = m.used()
    remap = {old: new for new, old in enumerate(used)}
    return [m.v[i] for i in used], [(remap[a], remap[b], remap[c], s, cols) for a, b, c, s, cols in m.f]


def blender_object(kit, m):
    verts, faces = compact(m)
    colours = []
    for a, b, c, s, cols in faces:
        for v, w in (cols[0], cols[2], cols[1]):
            colours.append((v, v, v, w))
    obj = kit.mesh_object(m.id, [kit.to_blender(p) for p in verts], [(a, c, b) for a, b, c, _, _ in faces],
                          [f[3] for f in faces], colours, smooth=[True] * len(faces))
    obj.data.set_sharp_from_angle(angle=math.radians(m.smooth))
    obj.data.update()
    return obj


def drop_studio(bpy):
    for obj in [o for o in bpy.data.objects if o.name.startswith("sheet_")]:
        bpy.data.objects.remove(obj, do_unlink=True)
    for col in [c for c in bpy.data.collections if c.name.startswith("sheet_studio")]:
        bpy.data.collections.remove(col)


LINEUPS = {"street": ["hedge_2m", "hedge_end", "fence_picket_2m", "gate_picket", "fence_board_2m", "lamp_post", "mailbox",
                      "bin_wheelie", "bench_park", "planter_box"],
           "nature": ["tree_round_c", "tree_round_a", "tree_round_b", "tree_poplar", "tree_far", "bush_a", "bush_b", "flowerbed_2m"],
           "vehicles": ["car_hatch", "car_estate", "van_small"],
           "garden": ["trampoline", "washing_line", "swing_set", "garden_table_set", "sprinkler"]}


def blender_stage(meshes, args):
    import bpy
    sys.path.insert(0, str(Path(__file__).resolve().parent))
    import surroundings_kit as kit
    kit.clean_scene()
    objects, reports, entries = {}, {}, []
    for m in meshes:
        obj = blender_object(kit, m)
        reports[m.id] = kit.check_piece(obj, m.max_tris, m.target)
        if reports[m.id]["tris"] != len(m.f):
            raise RuntimeError(f"{m.id}: blender tris {reports[m.id]['tris']} != {len(m.f)}")
        objects[m.id] = obj
        entry = kit.manifest_entry(reports[m.id], m.family, m.far, f"{KIT_DIR}/{GROUPS[m.group]}.fbx")
        if m.joins:
            entry["joins"] = [{"x": round(x, 3), "z": round(z, 3), "yaw": round(yaw, 2)} for x, z, yaw in m.joins]
        entries.append(entry)
    fbx = {}
    for group, name in GROUPS.items():
        chosen = [objects[m.id] for m in meshes if m.group == group]
        fbx[group] = kit.export_fbx(REPO / KIT_DIR / f"{name}.fbx", chosen)
        print(f"STREET_FBX {kit.repo_path(fbx[group])} objects={len(chosen)}", flush=True)
    path = kit.write_manifest(REPO / MANIFEST, "street", f"{KIT_DIR}/{GROUPS['street']}.fbx", entries)
    print(f"STREET_MANIFEST {kit.repo_path(path)} pieces={len(entries)}", flush=True)
    sheets = {}
    if not args.no_render:
        for group in GROUPS:
            drop_studio(bpy)
            for obj in objects.values():
                obj.hide_render = True
                obj.location = (0.0, 0.0, 0.0)
            chosen = [{"id": m.id, "obj": objects[m.id], "tris": len(m.f)} for m in meshes if m.group == group]
            sheets[group] = kit.render_sheet(chosen, REPO / SHEET_DIR / f"{group}.png", columns=6 if group == "street" else 4,
                                             samples=args.samples, lineup=LINEUPS[group])
    receipt = {"generator": "Tools/AssetPipeline/build_surroundings_street.py", "blender": bpy.app.version_string,
               "manifest": {"path": MANIFEST, "sha256": kit.sha256(path)},
               "fbx": {g: {"path": kit.repo_path(p), "sha256": kit.sha256(p)} for g, p in fbx.items()},
               "sheets": {g: kit.repo_path(p) for g, p in sheets.items()}, "pieces": [reports[m.id] for m in meshes]}
    problems = []
    for group, p in fbx.items():
        expected = [e for e, m in zip(entries, meshes) if m.group == group]
        found, rows = kit.verify_fbx(p, expected)
        problems += found
        receipt.setdefault("fbx_verify", {})[group] = rows
    receipt["passed"] = not problems
    out = REPO / SHEET_DIR / "street.json"
    out.parent.mkdir(parents=True, exist_ok=True)
    out.write_text(json.dumps(receipt, indent=2) + "\n", encoding="utf-8", newline="\n")
    for p in problems:
        print("STREET_FBX_PROBLEM " + p, flush=True)
    if problems:
        raise RuntimeError(f"FBX verification failed: {len(problems)} problems")
    print(f"STREET_KIT_READY {kit.repo_path(out)}", flush=True)


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--check-only", action="store_true")
    parser.add_argument("--no-render", action="store_true")
    parser.add_argument("--samples", type=int, default=24)
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else sys.argv[1:]
    args, _ = parser.parse_known_args(argv)
    meshes = build_all()
    report(meshes)
    problems = check(meshes)
    for p in problems:
        print("STREET_PROBLEM " + p, flush=True)
    if problems:
        raise SystemExit(f"self-check failed: {len(problems)} problems")
    print(f"STREET_SELF_CHECK PASS pieces={len(meshes)} tris={sum(len(m.f) for m in meshes)}", flush=True)
    if args.check_only:
        return
    blender_stage(meshes, args)


if __name__ == "__main__":
    main()
