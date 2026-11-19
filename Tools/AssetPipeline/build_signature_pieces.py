"""Author the rigid wardrobe signature pieces (emblem badges, Letter badge glyphs, headphones, bobble beanie, messenger bag)."""
import argparse
import datetime
import hashlib
import json
import math
import sys
from pathlib import Path

import bpy
from mathutils import Vector
from mathutils.bvhtree import BVHTree


REPO = Path(__file__).resolve().parents[2]
AVATAR = REPO / "Assets/_Project/Art/Imported/Avatar/Avatar.fbx"
LETTERS = REPO / "Assets/_Project/Art/Imported/Letters"
WARDROBE = REPO / "Assets/_Project/Data/Config/wardrobe.json"
OUTPUT = REPO / "Assets/_Project/Art/Wardrobe/Signature/Resources/Wardrobe/SignaturePieces.fbx"
EVIDENCE = REPO / "BuildNotes/F"
ALPHABET = "ABCDEFGHIJKLMNOPQRSTUVWXYZ"
AXES = {"front": Vector((0, -1, 0)), "right": Vector((1, 0, 0)), "up": Vector((0, 0, 1))}

CLASSIC = {
    "gold_Classic": (0.959, 0.786, 0.449), "glow_Classic": (0.982, 0.774, 0.332), "soap_Classic": (0.435, 0.839, 0.690),
    "pink_Classic": (0.987, 0.665, 0.755), "red_Classic": (0.941, 0.341, 0.260), "leaf_Classic": (0.437, 0.687, 0.381),
    "white_Classic": (1.0, 1.0, 1.0), "dark_Classic": (0.206, 0.240, 0.276), "wood_dark_Classic": (0.461, 0.293, 0.193),
    "ivory_Classic": (0.973, 0.945, 0.865),
}
AVATAR_FIXED = {"ivory_badge": (0.95, 0.93, 0.85), "accessory_hardware": (0.84, 0.68, 0.38), "rubber": (0.12, 0.12, 0.13),
    "ivory_vinyl": (0.96, 0.90, 0.80), "eyes": (0.08, 0.07, 0.07), "hair": (0.10, 0.07, 0.05)}

LOOKS = {
    "sunroom": {"top": "Crewneck", "head": "Cap", "glasses": True, "back": "Satchel", "badge": "SunBadge",
        "colours": {"Top": "sunflower", "Bottoms": "cream", "Gloves": "putty", "Footwear": "mustard", "Headwear": "sunflower", "Face": "gold", "Back": "mustard"}},
    "courier": {"top": "Hoodie", "head": "Hood", "glasses": False, "back": "MessengerBag", "badge": "StampBadge",
        "colours": {"Top": "mint", "Bottoms": "olive", "Gloves": "charcoal", "Footwear": "classic", "Headwear": "mint", "Face": "classic", "Back": "olive"}},
    "candy": {"top": "Hoodie", "head": "Cap", "glasses": True, "back": "Satchel", "badge": "SweetBadge",
        "colours": {"Top": "bubblegum", "Bottoms": "plum", "Gloves": "putty", "Footwear": "cherry", "Headwear": "oat", "Face": "cherry", "Back": "canvas"}},
    "arcade": {"top": "Crewneck", "head": "Headphones", "glasses": True, "back": "Satchel", "badge": "PixelBadge",
        "colours": {"Top": "grape", "Bottoms": "charcoal", "Gloves": "ink", "Footwear": "navy", "Headwear": "mint", "Face": "sky", "Back": "navy"}},
    "lantern": {"top": "Hoodie", "head": "Hood", "glasses": True, "back": "Satchel", "badge": "LanternBadge",
        "colours": {"Top": "tangerine", "Bottoms": "cocoa", "Gloves": "sunflower", "Footwear": "tan", "Headwear": "tomato", "Face": "gold", "Back": "brick"}},
    "winter": {"top": "Hoodie", "head": "Beanie", "glasses": False, "back": "Satchel", "badge": "HollyBadge",
        "colours": {"Top": "tomato", "Bottoms": "forest", "Gloves": "putty", "Footwear": "forest", "Headwear": "oat", "Face": "gold", "Back": "saddle"}},
}
SEATS = [("W", "tomato"), ("O", "pool"), ("R", "mint"), ("D", "sunflower")]


def linear(c):
    return c / 12.92 if c <= .04045 else ((c + .055) / 1.055) ** 2.4


def material(name, srgb=None, emissive=False):
    mat = bpy.data.materials.get(name) or bpy.data.materials.new(name)
    if srgb is not None:
        lin = tuple(linear(c) for c in srgb)
        mat.diffuse_color = (*lin, 1)
        mat.use_nodes = True
        shader = mat.node_tree.nodes.get("Principled BSDF")
        if shader:
            shader.inputs["Base Color"].default_value = (*lin, 1)
            shader.inputs["Roughness"].default_value = .55
            if emissive and "Emission Color" in shader.inputs:
                shader.inputs["Emission Color"].default_value = (*lin, 1)
    return mat


class Frame:
    def __init__(self, origin, u, v, n):
        self.o, self.u, self.v, self.n = Vector(origin), Vector(u).normalized(), Vector(v).normalized(), Vector(n).normalized()

    def p(self, u, v, n):
        return self.o + self.u * u + self.v * v + self.n * n

    def at(self, u, v, n):
        return Frame(self.p(u, v, n), self.u, self.v, self.n)


def basis(axis):
    axis = Vector(axis).normalized()
    helper = Vector((0, 0, 1)) if abs(axis.z) < .9 else Vector((1, 0, 0))
    u = helper.cross(axis).normalized()
    return u, axis.cross(u).normalized()


class Builder:
    def __init__(self):
        self.verts, self.faces, self.smooth = [], [], []

    def add(self, points, faces, smooth=False):
        base = len(self.verts)
        self.verts.extend(Vector(p) for p in points)
        for face in faces:
            self.faces.append(tuple(base + i for i in face))
            self.smooth.append(smooth)
        return self

    def prism(self, frame, outline, n0, n1):
        k = len(outline)
        points = [frame.p(u, v, n0) for u, v in outline] + [frame.p(u, v, n1) for u, v in outline]
        faces = [tuple(range(k, 2 * k)), tuple(reversed(range(k)))]
        faces += [(i, (i + 1) % k, k + (i + 1) % k, k + i) for i in range(k)]
        return self.add(points, faces)

    def box(self, frame, u0, u1, v0, v1, n0, n1):
        return self.prism(frame, [(u0, v0), (u1, v0), (u1, v1), (u0, v1)], n0, n1)

    def revolve(self, centre, axis, profile, sectors=24, smooth=True, cap=True):
        x, y = basis(axis)
        axis = Vector(axis).normalized()
        points = []
        for r, t in profile:
            for i in range(sectors):
                a = 2 * math.pi * i / sectors
                points.append(Vector(centre) + axis * t + (x * math.cos(a) + y * math.sin(a)) * r)
        faces = []
        for j in range(len(profile) - 1):
            for i in range(sectors):
                k = (i + 1) % sectors
                faces.append((j * sectors + i, j * sectors + k, (j + 1) * sectors + k, (j + 1) * sectors + i))
        if cap:
            faces.append(tuple(reversed(range(sectors))))
            last = (len(profile) - 1) * sectors
            faces.append(tuple(last + i for i in range(sectors)))
        return self.add(points, faces, smooth)

    def ellipsoid(self, frame, centre, radii, rings=8, sectors=16):
        points = [frame.p(centre[0], centre[1], centre[2] - radii[2])]
        for ring in range(1, rings):
            phi = math.pi * ring / rings
            for i in range(sectors):
                a = 2 * math.pi * i / sectors
                points.append(frame.p(centre[0] + radii[0] * math.sin(phi) * math.cos(a),
                    centre[1] + radii[1] * math.sin(phi) * math.sin(a), centre[2] - radii[2] * math.cos(phi)))
        points.append(frame.p(centre[0], centre[1], centre[2] + radii[2]))
        top = len(points) - 1
        faces = [(0, 1 + (i + 1) % sectors, 1 + i) for i in range(sectors)]
        for ring in range(rings - 2):
            a0, a1 = 1 + ring * sectors, 1 + (ring + 1) * sectors
            for i in range(sectors):
                k = (i + 1) % sectors
                faces.append((a0 + i, a0 + k, a1 + k, a1 + i))
        last = 1 + (rings - 2) * sectors
        faces += [(last + i, last + (i + 1) % sectors, top) for i in range(sectors)]
        return self.add(points, faces, True)

    def torus(self, centre, axis, major, minor, segments=24, cross=8):
        x, y = basis(axis)
        axis = Vector(axis).normalized()
        points = []
        for i in range(segments):
            a = 2 * math.pi * i / segments
            radial = x * math.cos(a) + y * math.sin(a)
            for j in range(cross):
                b = 2 * math.pi * j / cross
                points.append(Vector(centre) + radial * (major + minor * math.cos(b)) + axis * minor * math.sin(b))
        faces = []
        for i in range(segments):
            for j in range(cross):
                i2, j2 = (i + 1) % segments, (j + 1) % cross
                faces.append((i * cross + j, i2 * cross + j, i2 * cross + j2, i * cross + j2))
        return self.add(points, faces, True)

    def sweep(self, points, normals, half_width, thickness, smooth=False):
        rings = []
        for i, p in enumerate(points):
            ahead = points[min(i + 1, len(points) - 1)] - points[max(i - 1, 0)]
            side = ahead.cross(normals[i]).normalized() * half_width
            n = normals[i].normalized()
            rings += [p - side, p + side, p + side + n * thickness, p - side + n * thickness]
        faces = []
        for i in range(len(points) - 1):
            a, b = 4 * i, 4 * (i + 1)
            for j in range(4):
                k = (j + 1) % 4
                faces.append((a + j, a + k, b + k, b + j))
        faces.append((3, 2, 1, 0))
        last = 4 * (len(points) - 1)
        faces.append((last, last + 1, last + 2, last + 3))
        return self.add(rings, faces, smooth)

    def build(self, name, mat, parent, bevel=0.0, uv_scale=6.0):
        mesh = bpy.data.meshes.new(name)
        mesh.from_pydata([tuple(v) for v in self.verts], [], self.faces)
        for poly, smooth in zip(mesh.polygons, self.smooth):
            poly.use_smooth = smooth
        mesh.validate(clean_customdata=False)
        mesh.update()
        uv = mesh.uv_layers.new(name="UVMap")
        for poly in mesh.polygons:
            normal = poly.normal
            for li in poly.loop_indices:
                co = mesh.vertices[mesh.loops[li].vertex_index].co
                if abs(normal.z) >= max(abs(normal.x), abs(normal.y)):
                    uv.data[li].uv = (co.x * uv_scale, co.y * uv_scale)
                elif abs(normal.x) >= abs(normal.y):
                    uv.data[li].uv = (co.y * uv_scale, co.z * uv_scale)
                else:
                    uv.data[li].uv = (co.x * uv_scale, co.z * uv_scale)
        mesh.materials.append(mat)
        obj = bpy.data.objects.new(name, mesh)
        bpy.context.scene.collection.objects.link(obj)
        obj.parent = parent
        if bevel:
            modifier = obj.modifiers.new("Soft toy edges", "BEVEL")
            modifier.width = bevel
            modifier.segments = 2
            modifier.limit_method = "ANGLE"
            modifier.angle_limit = math.radians(35)
        return obj


def world(r, f, z):
    return AXES["right"] * r + AXES["front"] * f + Vector((0, 0, z))


def rc(p):
    return Vector(p).dot(AXES["right"])


def fc(p):
    return Vector(p).dot(AXES["front"])


def empty(name, parent=None, location=(0, 0, 0)):
    obj = bpy.data.objects.new(name, None)
    bpy.context.scene.collection.objects.link(obj)
    obj.empty_display_size = .03
    obj.location = location
    obj.parent = parent
    return obj


def points_of(obj):
    return [obj.matrix_world @ v.co for v in obj.data.vertices]


def bounds(points):
    lo = Vector([min(p[i] for p in points) for i in range(3)])
    hi = Vector([max(p[i] for p in points) for i in range(3)])
    return lo, hi


def bvh(objects, keep=None):
    points, polys = [], []
    for obj in objects:
        base = len(points)
        world = points_of(obj)
        points.extend(world)
        for poly in obj.data.polygons:
            if keep is None or keep(obj, poly):
                polys.append([base + i for i in poly.vertices])
    return BVHTree.FromPolygons(points, polys)


def cast(tree, origin, direction, distance=2.0):
    hit, normal, _, _ = tree.ray_cast(Vector(origin), Vector(direction).normalized(), distance)
    if hit is None:
        return None, None
    if normal.dot(direction) > 0:
        normal = -normal
    return hit, normal


def mesh(name):
    obj = bpy.data.objects.get(name)
    if obj is None or obj.type != "MESH":
        raise RuntimeError(f"signature: the avatar has no {name}")
    return obj


def import_avatar():
    bpy.ops.import_scene.fbx(filepath=str(AVATAR), use_anim=False)
    rigs = [o for o in bpy.data.objects if o.type == "ARMATURE"]
    if len(rigs) != 1:
        raise RuntimeError(f"signature: expected one armature, found {len(rigs)}")
    rig = rigs[0]
    bone = {name: rig.matrix_world @ rig.data.bones[name].head_local for name in ("pelvis", "chest", "head")}
    badge_points = points_of(mesh("SK_Badge_T"))
    lo, hi = bounds(badge_points)
    badge = (lo + hi) / 2
    if not .5 < bone["head"].z < 1.0 or bone["pelvis"].z >= bone["chest"].z:
        raise RuntimeError(f"signature: the avatar is not upright in metres: {bone}")
    facing = badge - bone["chest"]
    facing.z = 0
    if facing.length < .05:
        raise RuntimeError(f"signature: the badge does not sit in front of the chest: badge {badge}, chest {bone['chest']}")
    AXES["front"] = facing.normalized()
    AXES["right"] = Vector((0, 0, 1)).cross(AXES["front"]).normalized()
    return rig, bone, badge, badge_points


def badge_frame(badge, badge_points, shirts):
    import numpy as np
    pts = np.array([tuple(p) for p in badge_points])
    centre = pts.mean(axis=0)
    _, vectors = np.linalg.eigh(np.cov((pts - centre).T))
    normal = Vector(vectors[:, 0]).normalized()
    if normal.dot(AXES["front"]) < 0:
        normal = -normal
    up = Vector((0, 0, 1))
    v = (up - normal * up.dot(normal)).normalized()
    u = v.cross(normal).normalized()
    probe = Frame(badge, u, v, normal)
    deepest = -1.0
    for i in range(-5, 6):
        for j in range(-5, 6):
            if i * i + j * j > 25:
                continue
            start = probe.p(i * .011, j * .011, .3)
            hit, _ = cast(shirts, start, -normal, .6)
            if hit is not None:
                deepest = max(deepest, (hit - probe.o).dot(normal))
    if deepest < -.5:
        raise RuntimeError("signature: no shirt surface under the badge")
    return probe.at(0, 0, deepest + .0015), {"normal": list(normal), "tilt_deg": math.degrees(math.asin(max(-1, min(1, -normal.z)))), "back_offset_m": deepest + .0015}


def circle(r, n=32, cu=0.0, cv=0.0, start=0.0):
    return [(cu + r * math.cos(start + 2 * math.pi * i / n), cv + r * math.sin(start + 2 * math.pi * i / n)) for i in range(n)]


def rounded_rect(w, h, r, steps=5, cu=0.0, cv=0.0):
    out = []
    for cx, cy, a0 in ((w / 2 - r, -h / 2 + r, -90), (w / 2 - r, h / 2 - r, 0), (-w / 2 + r, h / 2 - r, 90), (-w / 2 + r, -h / 2 + r, 180)):
        for s in range(steps + 1):
            a = math.radians(a0 + 90 * s / steps)
            out.append((cu + cx + r * math.cos(a), cv + cy + r * math.sin(a)))
    return out


def heart(size, cu=0.0, cv=0.0, n=40):
    out = []
    for i in range(n):
        t = 2 * math.pi * i / n
        x = 16 * math.sin(t) ** 3
        y = 13 * math.cos(t) - 5 * math.cos(2 * t) - 2 * math.cos(3 * t) - math.cos(4 * t)
        out.append((cu + x * size / 32, cv + y * size / 32))
    return list(reversed(out)) if area(out) < 0 else out


def area(outline):
    return sum(outline[i][0] * outline[(i + 1) % len(outline)][1] - outline[(i + 1) % len(outline)][0] * outline[i][1] for i in range(len(outline))) / 2


def ccw(outline):
    return outline if area(outline) > 0 else list(reversed(outline))


def perforated(w, h, notch, spacing):
    out = []
    corners = [(-w / 2, -h / 2), (w / 2, -h / 2), (w / 2, h / 2), (-w / 2, h / 2)]
    for c in range(4):
        a, b = Vector(corners[c] + (0,)), Vector(corners[(c + 1) % 4] + (0,))
        edge = b - a
        length = edge.length
        inward = Vector((-edge.y, edge.x, 0)).normalized()
        count = max(1, int(length / spacing))
        centres = [length * (k + .5) / count for k in range(count)]
        steps = int(length / .0012)
        for s in range(steps):
            t = length * s / steps
            depth = 0.0
            for m in centres:
                d = abs(t - m)
                if d < notch:
                    depth = max(depth, math.sqrt(notch * notch - d * d))
            p = a + edge.normalized() * t + inward * depth
            out.append((p.x, p.y))
    return ccw(out)


def rotate(outline, degrees, cu=0.0, cv=0.0):
    a = math.radians(degrees)
    return [(cu + x * math.cos(a) - y * math.sin(a), cv + x * math.sin(a) + y * math.cos(a)) for x, y in outline]


def holly_leaf(length, width, spikes=4):
    right = []
    for i in range(1, 2 * spikes + 1):
        t = i / (2 * spikes + 1)
        w = width * math.sin(math.pi * t) ** .8 * (1.28 if i % 2 else .72)
        right.append((w, length * t))
    left = [(-x, y) for x, y in reversed(right)]
    return ccw([(0.0, 0.0)] + right + [(0.0, length)] + left)


def sun_badge(f, mats, root):
    rays = Builder()
    for i in range(10):
        a = math.radians(90 + 36 * i)
        base, tip = .027, .055
        outline = [(base * math.cos(a - .2), base * math.sin(a - .2)), (tip * math.cos(a), tip * math.sin(a)), (base * math.cos(a + .2), base * math.sin(a + .2))]
        rays.prism(f, ccw(outline), 0, .006)
    rays.build("SunBadge_Rays", mats["glow_Classic"], root, .0008)
    Builder().revolve(f.p(0, 0, 0), f.n, [(.031, 0), (.032, .004), (.028, .009), (.016, .0118), (.002, .0125)], 32).build("SunBadge_Disc", mats["gold_Classic"], root)
    face = Builder()
    for side in (-1, 1):
        face.ellipsoid(f, (side * .0095, .005, .0112), (.0033, .0045, .0022), 6, 10)
    smile = [(.012 * math.cos(math.radians(a)), .012 * math.sin(math.radians(a)) - .001) for a in range(205, 336, 10)]
    smile += [(.0082 * math.cos(math.radians(a)), .0082 * math.sin(math.radians(a)) - .001) for a in range(335, 204, -10)]
    face.prism(f, ccw(smile), .0095, .0128)
    face.build("SunBadge_Face", mats["dark_Classic"], root)


def stamp_badge(f, mats, root):
    Builder().prism(f, perforated(.080, .092, .0034, .0115), 0, .005).build("StampBadge_Paper", mats["white_Classic"], root, .0007)
    Builder().prism(f, rounded_rect(.060, .072, .004), .005, .0066).build("StampBadge_Panel", mats["soap_Classic"], root, .0005)
    Builder().prism(f, rounded_rect(.042, .030, .0025, 3, 0, -.004), .0066, .0096).build("StampBadge_Envelope", mats["white_Classic"], root, .0006)
    flap = Builder()
    for side in (-1, 1):
        a, b = Vector((side * .019, .0095)), Vector((0, -.006))
        d = (b - a).normalized()
        n = Vector((-d.y, d.x)) * .0013
        stroke = [(a.x - n.x, a.y - n.y), (b.x - n.x, b.y - n.y), (b.x + n.x, b.y + n.y), (a.x + n.x, a.y + n.y)]
        flap.prism(f, ccw(stroke), .0096, .0104)
    flap.build("StampBadge_Flap", mats["dark_Classic"], root)
    Builder().prism(f, heart(.011, 0, -.006), .0096, .0118).build("StampBadge_Seal", mats["red_Classic"], root, .0005)


def sweet_badge(f, mats, root):
    wrapper = Builder()
    left = [(-.020, .007), (-.050, .025), (-.045, .019), (-.052, .0125), (-.045, .006), (-.053, 0), (-.045, -.006),
        (-.052, -.0125), (-.045, -.019), (-.050, -.025), (-.020, -.007)]
    wrapper.prism(f, ccw(left), 0, .006)
    wrapper.prism(f, ccw([(-x, y) for x, y in left]), 0, .006)
    wrapper.build("SweetBadge_Wrapper", mats["white_Classic"], root, .0007)
    stripes = Builder()
    for side in (-1, 1):
        for k in (0, 1):
            u0 = side * (.029 + k * .010)
            stripes.box(f, min(u0, u0 + side * .004), max(u0, u0 + side * .004), -.012 + k * .003, .012 - k * .003, .006, .0072)
    stripes.build("SweetBadge_Stripes", mats["pink_Classic"], root)
    Builder().revolve(f.p(0, 0, 0), f.n, [(.026, 0), (.0268, .004), (.0235, .0102), (.013, .0138), (.002, .0145)], 32).build("SweetBadge_Candy", mats["pink_Classic"], root)
    Builder().torus(f.p(0, 0, .0118), f.n, .0155, .0019, 28, 6).build("SweetBadge_Swirl", mats["white_Classic"], root)


def pixel_badge(f, mats, root):
    Builder().prism(f, rounded_rect(.088, .082, .011), 0, .006).build("PixelBadge_Screen", mats["dark_Classic"], root, .0012)
    rows = [".XX.XX.", "XXXXXXX", "XXXXXXX", ".XXXXX.", "..XXX..", "...X..."]
    shine = {(1, 1), (1, 2), (2, 1)}
    pink, white = Builder(), Builder()
    p = .0105
    for r, row in enumerate(rows):
        for c, cell in enumerate(row):
            if cell != "X":
                continue
            u, v = (c - 3) * p, (2.5 - r) * p - .001
            (white if (r, c) in shine else pink).box(f, u - .0049, u + .0049, v - .0049, v + .0049, .006, .0108)
    pink.build("PixelBadge_Heart", mats["pink_Classic"], root, .0006)
    white.build("PixelBadge_Shine", mats["white_Classic"], root, .0006)


def lantern_badge(f, mats, root):
    Builder().ellipsoid(f, (0, -.003, .004), (.026, .030, .0085), 10, 24).build("LanternBadge_Body", mats["red_Classic"], root)
    Builder().ellipsoid(f, (0, -.003, .0118), (.010, .015, .0018), 6, 16).build("LanternBadge_Glow", mats["glow_Classic"], root)
    caps = Builder()
    caps.box(f, -.015, .015, .024, .032, 0, .011)
    caps.box(f, -.013, .013, -.039, -.031, 0, .010)
    caps.box(f, -.0028, .0028, -.054, -.039, .002, .008)
    caps.build("LanternBadge_Caps", mats["gold_Classic"], root, .0012)
    loop = Builder()
    loop.torus(f.p(0, .039, .006), f.u, .0065, .0017, 16, 6)
    loop.ellipsoid(f, (0, -.056, .005), (.0045, .0035, .0035), 6, 10)
    loop.build("LanternBadge_Loop", mats["gold_Classic"], root)
    ribs = Builder()
    for u in (-.011, .011):
        ribs.box(f, u - .0012, u + .0012, -.026, .020, .0105, .0122)
    ribs.build("LanternBadge_Ribs", mats["gold_Classic"], root)


def holly_badge(f, mats, root):
    leaves, veins = Builder(), Builder()
    for angle in (38, -38):
        leaf = rotate(holly_leaf(.058, .015), angle, 0, -.016)
        leaves.prism(f, ccw(leaf), 0, .005)
        a = Vector(rotate([(0, .004)], angle, 0, -.016)[0])
        b = Vector(rotate([(0, .050)], angle, 0, -.016)[0])
        d = (b - a).normalized()
        n = Vector((-d.y, d.x)) * .0011
        veins.prism(f, ccw([(a.x - n.x, a.y - n.y), (b.x - n.x, b.y - n.y), (b.x + n.x, b.y + n.y), (a.x + n.x, a.y + n.y)]), .005, .0058)
    leaves.build("HollyBadge_Leaves", mats["leaf_Classic"], root, .0007)
    veins.build("HollyBadge_Veins", mats["wood_dark_Classic"], root)
    berries = Builder()
    for u, v in ((-.0085, -.019), (.0085, -.019), (0, -.007)):
        berries.ellipsoid(f, (u, v, .0068), (.0088, .0088, .0068), 8, 14)
    berries.build("HollyBadge_Berries", mats["red_Classic"], root)


def read_glyphs():
    glyphs = {}
    up = Vector((0, 0, 1))
    for letter in ALPHABET:
        before = set(bpy.data.objects)
        bpy.ops.import_scene.fbx(filepath=str(LETTERS / f"Tile_{letter}.fbx"))
        added = [o for o in bpy.data.objects if o not in before]
        tile, dark, faces = [], [], []
        for obj in added:
            if obj.type != "MESH":
                continue
            points = points_of(obj)
            tile.extend(points)
            for poly in obj.data.polygons:
                mat = obj.data.materials[poly.material_index] if obj.data.materials else None
                if mat and mat.name.startswith("wood_dark"):
                    base = len(dark)
                    dark.extend(points[i] for i in poly.vertices)
                    faces.append(tuple(range(base, base + len(poly.vertices))))
        for obj in added:
            bpy.data.objects.remove(obj, do_unlink=True)
        if not dark:
            raise RuntimeError(f"signature: Tile_{letter} has no wood_dark letter faces")
        lo, hi = bounds(dark)
        tlo, thi = bounds(tile)
        extent = hi - lo
        thin = Vector((1, 0, 0)) if extent.x < extent.y else Vector((0, 1, 0))
        if min(extent.x, extent.y) >= extent.z:
            raise RuntimeError(f"signature: Tile_{letter} letter is not an upright relief: {lo} {hi}")
        tile_centre, glyph_centre = (tlo + thi) / 2, (lo + hi) / 2
        front = thin if (glyph_centre - tile_centre).dot(thin) > 0 else -thin
        right = up.cross(front)
        back = min(p.dot(front) for p in dark)
        uvn = [((p - tile_centre).dot(right), p.z - glyph_centre.z, p.dot(front) - back) for p in dark]
        glyphs[letter] = {"uvn": uvn, "faces": faces, "width": max(u for u, _, _ in uvn) - min(u for u, _, _ in uvn),
            "height": extent.z, "front": list(front)}
    return glyphs


def letter_badge(f, mats, root, glyphs):
    Builder().prism(f, circle(.042, 40), 0, .006).build("LetterBadge_Enamel", mats["ivory_badge"], root, .0012)
    Builder().torus(f.p(0, 0, .0048), f.n, .0425, .0036, 40, 8).build("LetterBadge_Rim", mats["accessory_hardware"], root)
    report = {}
    for letter, glyph in glyphs.items():
        scale = min(.046 / glyph["height"], .060 / glyph["width"])
        points = [f.p(u * scale, v * scale, .0055 + n * scale) for u, v, n in glyph["uvn"]]
        Builder().add(points, glyph["faces"]).build("Glyph_" + letter, mats["wood_dark_Classic"], root)
        report[letter] = {"scale": round(scale, 4), "faces": len(glyph["faces"]), "tile_front": glyph["front"]}
    return report


def head_centre():
    skin = mesh("SK_Head")
    index = [i for i, m in enumerate(skin.data.materials) if m and m.name.startswith("ivory_vinyl")]
    world = points_of(skin)
    pts = [world[v] for p in skin.data.polygons if p.material_index in index for v in p.vertices]
    lo, hi = bounds(pts)
    return (lo + hi) / 2


def headphones(mats, root, head_tree, centre):
    plane = centre - AXES["front"] * .012
    band_points, band_normals = [], []
    for step in range(-23, 24):
        theta = math.radians(step * 4)
        d = AXES["right"] * math.sin(theta) + Vector((0, 0, math.cos(theta)))
        hit, _ = cast(head_tree, plane + d * .6, -d, 1.2)
        if hit is None:
            raise RuntimeError("signature: the headphone band missed the head")
        band_points.append(hit + d * .016)
        band_normals.append(d)
    Builder().sweep(band_points, band_normals, .015, .011).build("Headphones_Band", mats["headwear_color"], root, .003)
    cups, cushions, rings, sliders = Builder(), Builder(), Builder(), Builder()
    for side in (-1, 1):
        axis = AXES["right"] * side
        hit, _ = cast(head_tree, plane + axis * .6, -axis, 1.2)
        if hit is None:
            raise RuntimeError("signature: the headphone cup missed the side of the head")
        cushions.revolve(hit, axis, [(.047, .003), (.050, .007), (.050, .013), (.046, .016)], 28)
        cups.revolve(hit, axis, [(.052, .013), (.056, .020), (.055, .034), (.047, .041), (.030, .043)], 28)
        rings.torus(hit + axis * .041, axis, .036, .0038, 28, 6)
        sliders.revolve(hit + Vector((0, 0, .058)), Vector((0, 0, 1)), [(.008, 0), (.0085, .02), (.006, .028)], 12)
    cushions.build("Headphones_Cushions", mats["rubber"], root)
    cups.build("Headphones_Cups", mats["headwear_color"], root)
    rings.build("Headphones_Glow", mats["glow_Classic"], root)
    sliders.build("Headphones_Sliders", mats["accessory_hardware"], root)
    return {"band_points": len(band_points), "band_plane_behind_centre_m": .012}


def beanie(mats, root, head_tree, centre):
    sectors, rows = 36, 14
    e_front, e_back = math.radians(23), math.radians(4)
    cuff_rows = 4
    rings = []
    for i in range(sectors):
        a = 2 * math.pi * i / sectors
        e_rim = e_front + (e_back - e_front) * (1 - math.cos(a)) / 2
        column = []
        for r in range(rows):
            t = r / (rows - 1)
            e = e_rim + (math.radians(86) - e_rim) * t
            d = AXES["right"] * (math.cos(e) * math.sin(a)) + AXES["front"] * (math.cos(e) * math.cos(a)) + Vector((0, 0, math.sin(e)))
            hit, _ = cast(head_tree, centre + d * .6, -d, 1.2)
            if hit is None:
                raise RuntimeError("signature: the beanie missed the head")
            offset = .012 + .016 * t ** 1.5
            if r < cuff_rows:
                offset += .009 + .0016 * math.cos(24 * a)
            column.append(hit + d * offset)
        inner = column[0] - (column[0] - centre).normalized() * .009
        rings.append([inner] + column)
    top_hit, _ = cast(head_tree, centre + Vector((0, 0, .6)), Vector((0, 0, -1)), 1.2)
    pole = top_hit + Vector((0, 0, .028))
    points = [p for column in rings for p in column] + [pole]
    count = rows + 1
    faces = []
    for i in range(sectors):
        k = (i + 1) % sectors
        for r in range(count - 1):
            faces.append((i * count + r, k * count + r, k * count + r + 1, i * count + r + 1))
        faces.append((i * count + count - 1, k * count + count - 1, len(points) - 1))
    Builder().add(points, faces, True).build("Beanie_Knit", mats["headwear_color"], root)
    pom = Builder()
    sphere_rings, sphere_sectors = 8, 14
    centre_pom = pole + Vector((0, 0, .034))
    pts = [centre_pom + Vector((0, 0, -.040))]
    for ring in range(1, sphere_rings):
        phi = math.pi * ring / sphere_rings
        for s in range(sphere_sectors):
            a = 2 * math.pi * s / sphere_sectors
            d = Vector((math.sin(phi) * math.cos(a), math.sin(phi) * math.sin(a), -math.cos(phi)))
            lump = 1 + .13 * math.sin(7 * d.x + 1) * math.sin(5 * d.y + 2) * math.sin(6 * d.z + 3) + .06 * math.cos(11 * a)
            pts.append(centre_pom + d * .040 * lump)
    pts.append(centre_pom + Vector((0, 0, .040)))
    top = len(pts) - 1
    f = [(0, 1 + (s + 1) % sphere_sectors, 1 + s) for s in range(sphere_sectors)]
    for ring in range(sphere_rings - 2):
        a0, a1 = 1 + ring * sphere_sectors, 1 + (ring + 1) * sphere_sectors
        for s in range(sphere_sectors):
            k = (s + 1) % sphere_sectors
            f.append((a0 + s, a0 + k, a1 + k, a1 + s))
    last = 1 + (sphere_rings - 2) * sphere_sectors
    f += [(last + s, last + (s + 1) % sphere_sectors, top) for s in range(sphere_sectors)]
    pom.add(pts, f, True).build("Beanie_PomPom", mats["ivory_badge"], root)
    return {"rim_front_deg": 23, "rim_back_deg": 4, "pole": list(pole)}


def messenger_bag(mats, root, body_tree, strap_tree):
    yaw = math.radians(20)
    n = AXES["right"] * math.sin(yaw) + AXES["front"] * math.cos(yaw)
    v = Vector((0, 0, 1))
    u = v.cross(n).normalized()
    anchor = world(.105, 0, .300)
    probe = Frame(anchor, u, v, n)
    deepest = -1.0
    for i in range(-4, 5):
        for j in range(-3, 4):
            hit, _ = cast(body_tree, probe.p(i * .017, j * .016, .4), -n, .8)
            if hit is not None:
                deepest = max(deepest, (hit - anchor).dot(n))
    if deepest < -.5:
        raise RuntimeError("signature: no body behind the messenger bag")
    f = probe.at(0, 0, deepest + .006)
    depth = .040
    Builder().prism(f, rounded_rect(.140, .100, .017), 0, depth).build("MessengerBag_Body", mats["satchel_canvas"], root, .004)
    flap = Builder()
    flap.prism(f, rounded_rect(.146, .068, .015, 5, 0, .020), depth - .001, depth + .005)
    flap.box(f, -.073, .073, .047, .054, .004, depth + .005)
    flap.build("MessengerBag_Flap", mats["satchel_canvas"], root, .002)
    Builder().box(f, -.007, .007, -.020, .012, depth + .005, depth + .008).build("MessengerBag_Tab", mats["satchel_straps"], root, .001)
    Builder().box(f, -.011, .011, -.022, -.011, depth + .007, depth + .011).build("MessengerBag_Buckle", mats["accessory_hardware"], root, .0012)

    def surface(origin, direction):
        hit, normal = cast(strap_tree, origin, direction)
        return (hit + normal * .004, normal) if hit is not None else None

    start = f.p(-.060, .050, depth * .5)
    shoulder_r, shoulder_z = -.125, .600
    front, right = AXES["front"], AXES["right"]
    path, normals = [start], [v.copy()]
    for k in range(1, 19):
        t = k / 18
        found = surface(world(rc(start) + (shoulder_r - rc(start)) * t, 1, start.z + (shoulder_z - start.z) * t), -front)
        if found:
            path.append(found[0]); normals.append(found[1])
    for k in range(13):
        found = surface(world(shoulder_r, .09 - .20 * k / 12, .66), Vector((0, 0, -1)))
        if found and found[0].z < .655 and fc(found[0]) < fc(path[-1]):
            path.append(found[0]); normals.append(found[1])
    back_r, back_z = .150, .330
    for k in range(1, 15):
        t = k / 14
        found = surface(world(shoulder_r + (back_r - shoulder_r) * t, -1, shoulder_z + (back_z - shoulder_z) * t), front)
        if found and fc(found[0]) < 0:
            path.append(found[0]); normals.append(found[1])
    for k in range(1, 10):
        t = k / 9
        found = surface(world(1, -.07 + .15 * t, .325 - .01 * t), -right)
        if found and fc(found[0]) > fc(path[-1]):
            path.append(found[0]); normals.append(found[1])
    path.append(f.p(.068, .044, depth * .4))
    normals.append(u.copy())
    if len(path) < 24:
        raise RuntimeError(f"signature: the messenger strap found only {len(path)} points")
    Builder().sweep(path, normals, .016, .005).build("MessengerBag_Strap", mats["satchel_straps"], root, .0015)
    ahead = [p for p in path if fc(p) > 0]
    crossing = min(ahead, key=lambda p: abs(rc(p)))
    return {"strap_points": len(path), "crosses_centre_at_z": crossing.z, "back_plane_m": deepest + .006,
        "outer_edge_right_m": rc(f.p(.070, 0, depth))}


def descendants(obj):
    return [obj] + list(obj.children_recursive)


def triangles(obj):
    depsgraph = bpy.context.evaluated_depsgraph_get()
    evaluated = obj.evaluated_get(depsgraph)
    data = evaluated.to_mesh()
    data.calc_loop_triangles()
    count = len(data.loop_triangles)
    evaluated.to_mesh_clear()
    return count


def export(root, path):
    path.parent.mkdir(parents=True, exist_ok=True)
    bpy.ops.object.select_all(action="DESELECT")
    for obj in descendants(root):
        obj.hide_set(False)
        obj.select_set(True)
    bpy.context.view_layer.objects.active = root
    bpy.ops.export_scene.fbx(filepath=str(path), use_selection=True, object_types={"EMPTY", "MESH"},
        apply_unit_scale=True, apply_scale_options="FBX_SCALE_UNITS", axis_forward="-Z", axis_up="Y",
        bake_space_transform=False, use_mesh_modifiers=True, mesh_smooth_type="FACE", use_tspace=False,
        use_triangles=True, use_custom_props=False, add_leaf_bones=False, bake_anim=False, path_mode="AUTO", embed_textures=False)


def palette_colour(palettes, slot, colour):
    for entry in palettes.get(slot, []):
        if entry["id"] == colour:
            return tuple(entry["rgb"])
    raise RuntimeError(f"signature preview: {colour} is not a {slot} colour")


def tint_preview(mats, palettes, colours):
    top = palette_colour(palettes, "Top", colours["Top"])
    tints = {"fabric_main": top, "fabric_rib": tuple(c * .75 for c in top),
        "joggers_color": palette_colour(palettes, "Bottoms", colours["Bottoms"]),
        "gloves_color": palette_colour(palettes, "Gloves", colours["Gloves"]),
        "boots_color": palette_colour(palettes, "Footwear", colours["Footwear"]),
        "headwear_color": palette_colour(palettes, "Headwear", colours["Headwear"]),
        "glasses_frame": palette_colour(palettes, "Face", colours["Face"]),
        "satchel_canvas": palette_colour(palettes, "Back", colours["Back"]),
        "satchel_straps": palette_colour(palettes, "Back", colours["Back"])}
    for name, rgb in tints.items():
        for mat in bpy.data.materials:
            if mat.name == name or mat.name.startswith(name + "."):
                mat.diffuse_color = (*(linear(c) for c in rgb), 1)


def show_look(look, nodes, letter=None):
    visible = {"SK_Crewneck": look["top"] == "Crewneck", "SK_Hoodie": look["top"] == "Hoodie", "SK_Cap": look["head"] == "Cap",
        "SK_Hood": look["head"] == "Hood", "SK_Glasses": look["glasses"], "SK_Satchel": look["back"] == "Satchel", "SK_Badge_T": False}
    for name, on in visible.items():
        obj = bpy.data.objects.get(name)
        if obj:
            obj.hide_render = not on
    for name, root in nodes.items():
        on = name in ("Sig_" + look["head"], "Sig_" + look["back"], "Sig_" + look["badge"])
        for obj in descendants(root):
            obj.hide_render = not on
            if on and obj.name.startswith("Glyph_"):
                obj.hide_render = obj.name != "Glyph_" + str(letter)


def camera_rig():
    data = bpy.data.cameras.new("Preview camera")
    data.type = "ORTHO"
    cam = bpy.data.objects.new("Preview camera", data)
    bpy.context.scene.collection.objects.link(cam)
    bpy.context.scene.camera = cam
    world = bpy.data.worlds.new("Preview")
    world.color = (linear(.97), linear(.94), linear(.85))
    bpy.context.scene.world = world
    return cam


def shoot(cam, path, target, direction, scale, size=(400, 480)):
    scene = bpy.context.scene
    cam.location = Vector(target) - Vector(direction).normalized() * 6
    cam.rotation_euler = Vector(direction).to_track_quat("-Z", "Y").to_euler()
    cam.data.ortho_scale = scale
    scene.render.resolution_x, scene.render.resolution_y = size
    scene.render.filepath = str(path)
    bpy.ops.render.render(write_still=True)


def previews(folder, nodes, mats, palettes, frame):
    scene = bpy.context.scene
    scene.render.engine = "BLENDER_WORKBENCH"
    shading = scene.display.shading
    shading.light = "STUDIO"
    shading.color_type = "MATERIAL"
    shading.show_shadows = True
    shading.show_cavity = True
    scene.render.image_settings.file_format = "PNG"
    scene.render.film_transparent = False
    cam = camera_rig()
    pitch, yaw = math.radians(54), math.radians(20)
    toward = -AXES["front"]
    couch = -AXES["right"] * (math.sin(yaw) * math.cos(pitch)) + toward * (math.cos(yaw) * math.cos(pitch)) - Vector((0, 0, math.sin(pitch)))
    front = AXES["right"] * math.sin(math.radians(20)) + toward
    files = []
    for look_id, look in LOOKS.items():
        tint_preview(mats, palettes, look["colours"])
        show_look(look, nodes)
        for view, direction, scale, target in (("front", front, 1.25, (0, 0, .55)), ("couch", couch, 1.4, (0, 0, .5)),
                ("badge", toward, .16, tuple(frame.o))):
            path = folder / f"{look_id}_{view}.png"
            shoot(cam, path, target, direction, scale, (320, 320) if view == "badge" else (400, 480))
            files.append(path.name)
    seat_look = dict(LOOKS["courier"], top="Hoodie", head="Hood", back="Satchel", glasses=False, badge="LetterBadge")
    for letter, top in SEATS:
        colours = dict(LOOKS["courier"]["colours"], Top=top, Gloves="charcoal", Bottoms="charcoal", Footwear="ink", Back="ink")
        tint_preview(mats, palettes, colours)
        show_look(seat_look, nodes, letter)
        for view, direction, scale, target in (("front", front, 1.25, (0, 0, .55)), ("badge", toward, .16, tuple(frame.o))):
            path = folder / f"seat_{letter}_{view}.png"
            shoot(cam, path, target, direction, scale, (320, 320) if view == "badge" else (400, 480))
            files.append(path.name)
    for obj in bpy.data.objects:
        if obj.type == "MESH" and not any(obj in descendants(r) for r in nodes.values()):
            obj.hide_render = True
    for root in nodes.values():
        for obj in descendants(root):
            obj.hide_render = root.name != "Sig_LetterBadge"
    for letter in ALPHABET:
        for obj in descendants(nodes["Sig_LetterBadge"]):
            if obj.name.startswith("Glyph_"):
                obj.hide_render = obj.name != "Glyph_" + letter
        path = folder / f"glyph_{letter}.png"
        shoot(cam, path, tuple(frame.o), toward, .11, (128, 128))
        files.append(path.name)
    return files


def digest(path):
    return hashlib.sha256(Path(path).read_bytes()).hexdigest()


def verify(path, expected):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=str(path))
    names = {o.name for o in bpy.data.objects}
    missing = [name for name in expected if name not in names]
    meshes = [o for o in bpy.data.objects if o.type == "MESH"]
    count = 0
    for obj in meshes:
        obj.data.calc_loop_triangles()
        count += len(obj.data.loop_triangles)
    cameras = [o.name for o in bpy.data.objects if o.type in {"CAMERA", "LIGHT", "ARMATURE"}]
    return {"missing": missing, "triangles": count, "unexpected": cameras, "passed": not missing and not cameras}


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--output", default=str(OUTPUT))
    parser.add_argument("--evidence", default=None)
    parser.add_argument("--no-render", action="store_true")
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    args = parser.parse_args(argv)
    stamp = datetime.datetime.now(datetime.timezone.utc).strftime("%Y%m%dT%H%M%SZ")
    folder = Path(args.evidence) if args.evidence else EVIDENCE / f"signature-blender-{stamp}"
    folder.mkdir(parents=True, exist_ok=True)
    output = Path(args.output).resolve()
    bpy.ops.wm.read_factory_settings(use_empty=True)
    scene = bpy.context.scene
    scene.unit_settings.system = "METRIC"
    scene.unit_settings.scale_length = 1
    rig, bone, badge_centre, badge_points = import_avatar()
    glyphs = read_glyphs()
    mats = {name: material(name, rgb, name == "glow_Classic") for name, rgb in CLASSIC.items()}
    for name in ("ivory_badge", "accessory_hardware", "rubber", "headwear_color", "satchel_canvas", "satchel_straps"):
        mats[name] = material(name, AVATAR_FIXED.get(name))
    for name, rgb in AVATAR_FIXED.items():
        material(name, rgb)
    shirts = bvh([mesh("SK_Crewneck"), mesh("SK_Hoodie")])
    frame, frame_report = badge_frame(badge_centre, badge_points, shirts)
    head_tree = bvh([mesh("SK_Head")])
    centre = head_centre()
    body_tree = bvh([mesh("SK_Crewneck"), mesh("SK_Hoodie"), mesh("SK_Joggers")])
    strap_tree = bvh([mesh("SK_Crewneck"), mesh("SK_Hoodie"), mesh("SK_Hood")])
    root = empty("SignaturePieces")
    for name, at in (("Ref_pelvis", bone["pelvis"]), ("Ref_chest", bone["chest"]), ("Ref_head", bone["head"]), ("Ref_badge", badge_centre)):
        empty(name, root, at)
    nodes = {}
    report = {"axes": {k: list(v) for k, v in AXES.items()}, "frame": frame_report, "head_centre": list(centre), "bones": {k: list(v) for k, v in bone.items()}, "badge_centre": list(badge_centre)}
    for name, builder in (("Sig_SunBadge", sun_badge), ("Sig_StampBadge", stamp_badge), ("Sig_SweetBadge", sweet_badge),
            ("Sig_PixelBadge", pixel_badge), ("Sig_LanternBadge", lantern_badge), ("Sig_HollyBadge", holly_badge)):
        nodes[name] = empty(name, root)
        builder(frame, mats, nodes[name])
    nodes["Sig_LetterBadge"] = empty("Sig_LetterBadge", root)
    report["glyphs"] = letter_badge(frame, mats, nodes["Sig_LetterBadge"], glyphs)
    nodes["Sig_Headphones"] = empty("Sig_Headphones", root)
    report["headphones"] = headphones(mats, nodes["Sig_Headphones"], head_tree, centre)
    nodes["Sig_Beanie"] = empty("Sig_Beanie", root)
    report["beanie"] = beanie(mats, nodes["Sig_Beanie"], head_tree, centre)
    nodes["Sig_MessengerBag"] = empty("Sig_MessengerBag", root)
    report["messenger_bag"] = messenger_bag(mats, nodes["Sig_MessengerBag"], body_tree, strap_tree)
    bpy.context.view_layer.update()
    pieces = {}
    for name, node in nodes.items():
        meshes = [o for o in descendants(node) if o.type == "MESH"]
        tris = sum(triangles(o) for o in meshes)
        pts = [p for o in meshes for p in points_of(o)]
        lo, hi = bounds(pts)
        local = [((p - frame.o).dot(frame.u), (p - frame.o).dot(frame.v), (p - frame.o).dot(frame.n)) for p in pts]
        pieces[name] = {"triangles": tris, "meshes": len(meshes), "bounds_min": list(lo), "bounds_max": list(hi),
            "materials": sorted({s.material.name for o in meshes for s in o.material_slots if s.material})}
        if "Badge" in name:
            reach = max(math.hypot(a, b) for a, b, _ in local)
            depth = max(c for _, _, c in local)
            pieces[name].update(reach_m=round(reach, 4), depth_m=round(depth, 4))
            if reach > .062 or depth > .02 or min(c for _, _, c in local) < -.006:
                raise RuntimeError(f"signature: {name} is too big for the chest: reach {reach}, depth {depth}")
        middle = (lo + hi) / 2
        if math.hypot(rc(middle), fc(middle)) > .35 or not .15 < middle.z < 1.15:
            raise RuntimeError(f"signature: {name} is not on the avatar: centre {middle}")
        if tris > 12000:
            raise RuntimeError(f"signature: {name} has {tris} triangles")
    report["pieces"] = pieces
    files = []
    if not args.no_render:
        files = previews(folder, nodes, mats, json.loads(WARDROBE.read_text(encoding="utf-8"))["palettes"], frame)
    keep = set(descendants(root))
    for obj in list(bpy.data.objects):
        if obj not in keep:
            bpy.data.objects.remove(obj, do_unlink=True)
    bpy.ops.wm.save_as_mainfile(filepath=str(folder / "signature_pieces.blend"))
    export(root, output)
    expected = ["Ref_pelvis", "Ref_chest", "Ref_head", "Ref_badge"] + list(nodes) + ["Glyph_" + c for c in ALPHABET]
    check = verify(output, expected)
    manifest = {"generator": "Tools/AssetPipeline/build_signature_pieces.py", "blender": bpy.app.version_string, "utc": stamp,
        "inputs": {"avatar": str(AVATAR.relative_to(REPO)), "avatar_sha256": digest(AVATAR), "letters": "Tile_A..Z.fbx (wood_dark_Classic faces)"},
        "output": str(output.relative_to(REPO)) if REPO in output.parents else str(output), "output_sha256": digest(output),
        "placement": "Pieces are authored in the avatar's own Blender space. Ref_* empties mark the pelvis, chest and head joints and the T badge bounds centre; WardrobeRig solves art-to-avatar from them, so no axis convention is assumed.",
        "report": report, "verify": check, "renders": files}
    (folder / "manifest.json").write_text(json.dumps(manifest, indent=2, default=list) + "\n", encoding="utf-8")
    print("SIGNATURE_PIECES " + json.dumps({"output": manifest["output"], "sha256": manifest["output_sha256"], "verify": check, "evidence": str(folder)}), flush=True)
    if not check["passed"]:
        raise RuntimeError("signature: FBX round trip failed " + json.dumps(check))


if __name__ == "__main__":
    main()
