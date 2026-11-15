import argparse
import json
import math
import os
import shutil
import sys
import traceback

import bmesh
import bpy
from mathutils import Matrix, Vector

VERSION = "d2-2026-11-15"
SCRIPT = "Tools/AssetPipeline/correct_house_furniture.py"
HERE = os.path.dirname(os.path.abspath(__file__))
REPORT = "Assets/_Project/Data/Generated/build_report.json"
MATERIALS = "Assets/_Project/Data/Generated/materials.json"
ITEMS = "Assets/_Project/Art/Imported/Items/"
RUG = "Assets/_Project/Art/Imported/Environment/Round_Rug.fbx"
FABRIC = "Assets/_Project/Art/Imported/Textures/fabric_subtle_pattern.png"
UNITY = Matrix(((1.0, 0.0, 0.0), (0.0, 0.0, 1.0), (0.0, 1.0, 0.0)))
MARKER = "wb_correction"
SIZE_TOLERANCE = 0.05
AUTHORED_TOLERANCE = 0.01
FLOOR_TOLERANCE = 0.0005

RECOLOURS = {
    "wood_light_Classic": [0.79, 0.376, 0.102, 1],
    "wood_Classic": [0.456, 0.141, 0.038, 1],
    "wood_dark_Classic": [0.18, 0.07, 0.031, 1],
    "soap_Classic": [0.159, 0.672, 0.434, 1],
}


def added_spec(name, colour, roughness, base_map):
    family, skin = name.rsplit("_", 1)
    return {"name": name, "baseColor": colour, "metallic": 0, "roughness": roughness, "emissive": [0, 0, 0],
            "alphaMode": "OPAQUE", "alphaCutoff": 0.5, "doubleSided": False, "baseMap": base_map,
            "normalMap": None, "emissionMap": None, "family": family, "skin": skin,
            "sources": []}


ADDED = [added_spec("kraft_Classic", [0.392, 0.181, 0.065, 1], 0.9, FABRIC),
         added_spec("kraft_Candy", [0.98, 0.55, 0.36, 1], 0.7, FABRIC),
         added_spec("kraft_Arcade", [0.1, 0.45, 0.5, 1], 0.7, FABRIC),
         added_spec("plush_Classic", [0.342, 0.127, 0.042, 1], 0.92, FABRIC)]


def parse_args():
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    parser = argparse.ArgumentParser()
    parser.add_argument("--repo", default=os.path.dirname(os.path.dirname(HERE)))
    parser.add_argument("--only", default="")
    parser.add_argument("--out", default="")
    parser.add_argument("--backup", default="")
    parser.add_argument("--inspect", action="store_true")
    parser.add_argument("--force", action="store_true")
    parser.add_argument("--source", default="")
    parser.add_argument("--refresh-report", default="")
    return parser.parse_args(argv)


def newell(points):
    normal = Vector((0.0, 0.0, 0.0))
    for a, b in zip(points, points[1:] + points[:1]):
        normal.x += (a.y - b.y) * (a.z + b.z)
        normal.y += (a.z - b.z) * (a.x + b.x)
        normal.z += (a.x - b.x) * (a.y + b.y)
    return normal


def shoelace(points):
    return 0.5 * sum(a.x * b.y - b.x * a.y for a, b in zip(points, points[1:] + points[:1]))


class Geo:
    def __init__(self):
        self.points = []
        self.faces = []

    def point(self, position):
        self.points.append(Vector(position))
        return len(self.points) - 1

    def face(self, indices, normals, uvs):
        self.faces.append((list(indices), [Vector(n) for n in normals], [tuple(uv) for uv in uvs]))

    def extend(self, other):
        base = len(self.points)
        self.points.extend(other.points)
        self.faces.extend(([i + base for i in indices], normals, uvs) for indices, normals, uvs in other.faces)
        return self

    def transformed(self, matrix):
        normal_matrix = matrix.to_3x3().inverted().transposed()
        result = Geo()
        result.points = [matrix @ p for p in self.points]
        result.faces = [(indices, [(normal_matrix @ n).normalized() for n in normals], uvs)
                        for indices, normals, uvs in self.faces]
        return result


def axis_samples(half, radius, steps):
    inner = max(half - radius, 0.0)
    count = max(steps // 2, 1)
    ramp = [inner + radius * math.tan(math.radians(45.0 * k / count)) for k in range(count, 0, -1)]
    low = [-value for value in ramp] + [-inner]
    high = [inner] + ramp[::-1]
    return low + high[1:] if inner < 1e-7 else low + high


def rounded_box(centre, size, radius, steps=4, density=2.0, along=0, rotation=None):
    half = [s / 2.0 for s in size]
    radius = max(min(radius, min(half) * 0.98), 1e-5)
    inner = [max(h - radius, 0.0) for h in half]
    samples = [axis_samples(half[k], radius, steps) for k in range(3)]
    turn = rotation if rotation is not None else Matrix.Identity(3)
    centre = Vector(centre)
    geo = Geo()
    for axis in range(3):
        b, c = [k for k in range(3) if k != axis]
        if along in (b, c):
            u_axis, v_axis = along, (c if along == b else b)
        else:
            u_axis, v_axis = (2, 1) if axis == 0 else (b, c)
        for sign in (-1.0, 1.0):
            grid = {}
            for i, sb in enumerate(samples[b]):
                for j, sc in enumerate(samples[c]):
                    p = Vector((0.0, 0.0, 0.0))
                    p[axis] = sign * half[axis]
                    p[b] = sb
                    p[c] = sc
                    q = Vector([max(-inner[k], min(inner[k], p[k])) for k in range(3)])
                    d = p - q
                    n = d.normalized() if d.length > 1e-9 else Vector([sign if k == axis else 0.0 for k in range(3)])
                    local = q + n * radius
                    grid[i, j] = (geo.point(centre + turn @ local), turn @ n,
                                  (local[u_axis] * density, local[v_axis] * density))
            for i in range(len(samples[b]) - 1):
                for j in range(len(samples[c]) - 1):
                    quad = [grid[i, j], grid[i + 1, j], grid[i + 1, j + 1], grid[i, j + 1]]
                    geo.face([q[0] for q in quad], [q[1] for q in quad], [q[2] for q in quad])
    return geo


def lathe(profile, segments, axis="y", centre=(0.0, 0.0, 0.0), density=2.0, planar=False):
    centre = Vector(centre)
    profile = [p for k, p in enumerate(profile)
               if k == 0 or abs(p[0] - profile[k - 1][0]) > 1e-9 or abs(p[1] - profile[k - 1][1]) > 1e-9]
    geo = Geo()
    lengths = [0.0]
    for a, b in zip(profile, profile[1:]):
        lengths.append(lengths[-1] + math.hypot(b[0] - a[0], b[1] - a[1]))
    reach = max(p[0] for p in profile) or 1.0
    rings = []
    for (r, h, nr, nh), length in zip(profile, lengths):
        ring = []
        for j in range(segments + 1):
            t = 2.0 * math.pi * j / segments
            c, s = math.cos(t), math.sin(t)
            if axis == "y":
                position = centre + Vector((r * c, h, r * s))
                normal = Vector((nr * c, nh, nr * s))
                flat = (position.x, position.z)
            else:
                position = centre + Vector((r * c, r * s, h))
                normal = Vector((nr * c, nr * s, nh))
                flat = (position.x, position.y)
            uv = (flat[0] * density, flat[1] * density) if planar else (t * reach * density, length * density)
            ring.append((geo.point(position), normal.normalized(), uv))
        rings.append(ring)
    for k in range(len(profile) - 1):
        r0, r1 = profile[k][0], profile[k + 1][0]
        if r0 < 1e-7 and r1 < 1e-7:
            continue
        for j in range(segments):
            a, b = rings[k][j], rings[k][j + 1]
            c, d = rings[k + 1][j + 1], rings[k + 1][j]
            corners = [a, c, d] if r0 < 1e-7 else [a, b, c] if r1 < 1e-7 else [a, b, c, d]
            geo.face([q[0] for q in corners], [q[1] for q in corners], [q[2] for q in corners])
    return geo


def arc(centre_r, centre_h, radius, start, end, steps):
    points = []
    for k in range(steps + 1):
        angle = math.radians(start + (end - start) * k / steps)
        c, s = math.cos(angle), math.sin(angle)
        points.append((centre_r + radius * c, centre_h + radius * s, c, s))
    return points


def torus(centre, axis, major, minor, height, segments=48, sides=8):
    return lathe(arc(major, height, minor, 0.0, 360.0, sides), segments, axis, centre)


def rod(start, end, radius, segments=8):
    start, end = Vector(start), Vector(end)
    direction = end - start
    geo = lathe([(radius, 0.0, 1.0, 0.0), (radius, direction.length, 1.0, 0.0)], segments)
    turn = Vector((0.0, 1.0, 0.0)).rotation_difference(direction.normalized()).to_matrix().to_4x4()
    return geo.transformed(Matrix.Translation(start) @ turn)


def spline(points, count):
    knots = [Vector(p) for p in points]
    padded = [knots[0] * 2 - knots[1]] + knots + [knots[-1] * 2 - knots[-2]]
    dense = []
    for i in range(1, len(padded) - 2):
        p0, p1, p2, p3 = padded[i - 1], padded[i], padded[i + 1], padded[i + 2]
        for k in range(24):
            t = k / 24.0
            dense.append(0.5 * (2 * p1 + (p2 - p0) * t + (2 * p0 - 5 * p1 + 4 * p2 - p3) * t * t
                                + (3 * p1 - p0 - 3 * p2 + p3) * t * t * t))
    dense.append(knots[-1])
    total = [0.0]
    for a, b in zip(dense, dense[1:]):
        total.append(total[-1] + (b - a).length)
    result, cursor = [], 0
    for k in range(count):
        target = total[-1] * k / (count - 1)
        while cursor < len(total) - 2 and total[cursor + 1] < target:
            cursor += 1
        span = total[cursor + 1] - total[cursor]
        t = 0.0 if span <= 1e-12 else (target - total[cursor]) / span
        result.append(dense[cursor].lerp(dense[cursor + 1], min(max(t, 0.0), 1.0)))
    return result


def with_normals(points, sign=1.0):
    result = []
    for k, p in enumerate(points):
        a = points[max(k - 1, 0)]
        b = points[min(k + 1, len(points) - 1)]
        tangent = (b - a).normalized()
        result.append((p.x, p.y, sign * tangent.y, -sign * tangent.x))
    return result


class Part:
    def __init__(self, faces, verts, lo, hi, material):
        self.faces = faces
        self.verts = verts
        self.lo = lo
        self.hi = hi
        self.material = material

    @property
    def centre(self):
        return (self.lo + self.hi) / 2

    @property
    def size(self):
        return self.hi - self.lo

    def summary(self):
        return {"material": self.material, "faces": len(self.faces),
                "centre": [round(v, 4) for v in self.centre], "size": [round(v, 4) for v in self.size]}


class Editable:
    def __init__(self, obj, specs):
        self.obj = obj
        self.me = obj.data
        self.specs = specs
        self.to_unity = UNITY.to_4x4() @ obj.matrix_world
        self.to_local = self.to_unity.inverted()
        linear = self.to_unity.to_3x3()
        self.normal_to_unity = linear.inverted().transposed()
        self.normal_to_local = linear.transposed()
        corner = [0.0] * (len(self.me.loops) * 3)
        self.me.corner_normals.foreach_get("vector", corner)
        attribute = self.me.attributes.new("wb_normal", "FLOAT_VECTOR", "CORNER")
        attribute.data.foreach_set("vector", corner)
        self.bm = bmesh.new()
        self.bm.from_mesh(self.me)
        self.normal_layer = self.bm.loops.layers.float_vector["wb_normal"]
        self.uv_layers = list(self.bm.loops.layers.uv.values())

    def slot_names(self):
        return [m.name if m else "" for m in self.me.materials]

    def material(self, name):
        found = bpy.data.materials.get(name)
        if found is not None:
            return found
        spec = self.specs[name]
        made = bpy.data.materials.new(name)
        made.diffuse_color = spec["baseColor"]
        made.roughness = spec["roughness"]
        made.metallic = spec["metallic"]
        if getattr(made, "node_tree", None) is None:
            try:
                made.use_nodes = True
            except (AttributeError, TypeError):
                pass
        tree = getattr(made, "node_tree", None)
        for node in (tree.nodes if tree else []):
            if node.type == "BSDF_PRINCIPLED":
                node.inputs["Base Color"].default_value = spec["baseColor"]
                node.inputs["Roughness"].default_value = spec["roughness"]
                node.inputs["Metallic"].default_value = spec["metallic"]
        if made.name != name:
            raise RuntimeError(f"material {name} was created as {made.name}")
        return made

    def slot(self, name):
        names = self.slot_names()
        if name in names:
            return names.index(name)
        self.me.materials.append(self.material(name))
        return len(self.me.materials) - 1

    def rename(self, old, new):
        names = self.slot_names()
        if new in names:
            return names.index(new)
        index = names.index(old)
        self.me.materials[index] = self.material(new)
        return index

    def unity(self, co):
        return self.to_unity @ co

    def face_normal(self, face):
        return (self.normal_to_unity @ newell([v.co.copy() for v in face.verts])).normalized()

    def parts(self):
        bm = self.bm
        bm.verts.index_update()
        parent = list(range(len(bm.verts)))

        def root(i):
            while parent[i] != i:
                parent[i] = parent[parent[i]]
                i = parent[i]
            return i

        def join(a, b):
            ra, rb = root(a), root(b)
            if ra != rb:
                parent[ra] = rb

        seen = {}
        for v in bm.verts:
            key = (round(v.co.x, 5), round(v.co.y, 5), round(v.co.z, 5))
            if key in seen:
                join(v.index, seen[key])
            else:
                seen[key] = v.index
        for e in bm.edges:
            join(e.verts[0].index, e.verts[1].index)
        groups = {}
        for f in bm.faces:
            groups.setdefault(root(f.verts[0].index), []).append(f)
        names = self.slot_names()
        result = []
        for faces in groups.values():
            verts = list({v for f in faces for v in f.verts})
            points = [self.unity(v.co) for v in verts]
            lo = Vector([min(p[k] for p in points) for k in range(3)])
            hi = Vector([max(p[k] for p in points) for k in range(3)])
            material = "+".join(sorted({names[f.material_index] for f in faces}))
            result.append(Part(faces, verts, lo, hi, material))
        result.sort(key=lambda p: (p.material, -p.centre.y, p.centre.x, p.centre.z))
        return result

    def find(self, material, test=None, expect=None, label=""):
        found = [p for p in self.parts() if p.material == material and (test is None or test(p))]
        if expect is not None and len(found) != expect:
            raise RuntimeError(f"{self.obj.name}: expected {expect} {label or material} parts, found {len(found)}: "
                               + json.dumps([p.summary() for p in self.parts()]))
        return found

    def delete(self, parts):
        bmesh.ops.delete(self.bm, geom=[f for p in parts for f in p.faces], context="FACES")

    def map(self, part, point, linear=None):
        for v in part.verts:
            v.co = self.to_local @ point(self.unity(v.co))
        if linear is not None:
            inverse = linear.inverted().transposed()
            for f in part.faces:
                for loop in f.loops:
                    n = self.normal_to_unity @ Vector(loop[self.normal_layer])
                    loop[self.normal_layer] = (self.normal_to_local @ (inverse @ n)).normalized()

    def stretch(self, part, axis, lo, hi):
        middle = (part.lo[axis] + part.hi[axis]) / 2
        low, high = lo - part.lo[axis], hi - part.hi[axis]

        def move(p):
            q = p.copy()
            q[axis] += high if p[axis] > middle else low
            return q

        self.map(part, move)

    def density(self, slot, fallback=2.0):
        if not self.uv_layers:
            return fallback
        layer = self.uv_layers[0]
        area = uv_area = 0.0
        for f in self.bm.faces:
            if f.material_index != slot:
                continue
            area += newell([self.unity(v.co) for v in f.verts]).length / 2
            uv_area += abs(shoelace([loop[layer].uv.copy() for loop in f.loops]))
        return math.sqrt(uv_area / area) if area > 1e-9 and uv_area > 1e-12 else fallback

    def planar_uv(self, faces, axes, density):
        for f in faces:
            for loop in f.loops:
                p = self.unity(loop.vert.co)
                for layer in self.uv_layers:
                    loop[layer].uv = (p[axes[0]] * density, p[axes[1]] * density)

    def box_uv(self, faces, density, along=0):
        for f in faces:
            n = self.face_normal(f)
            axis = max(range(3), key=lambda k: abs(n[k]))
            others = [k for k in range(3) if k != axis]
            if along in others:
                axes = (along, [k for k in others if k != along][0])
            else:
                axes = (2, 1) if axis == 0 else tuple(others)
            self.planar_uv([f], axes, density)

    def add(self, geo, slot):
        verts = [self.bm.verts.new(self.to_local @ p) for p in geo.points]
        for indices, normals, uvs in geo.faces:
            local = [verts[i].co.copy() for i in indices]
            wanted = self.normal_to_local @ sum(normals, Vector((0.0, 0.0, 0.0)))
            if newell(local).dot(wanted) < 0:
                indices, normals, uvs = indices[::-1], normals[::-1], uvs[::-1]
            try:
                face = self.bm.faces.new([verts[i] for i in indices])
            except ValueError:
                continue
            face.material_index = slot
            face.smooth = True
            for loop, normal, uv in zip(face.loops, normals, uvs):
                loop[self.normal_layer] = (self.normal_to_local @ normal).normalized()
                for layer in self.uv_layers:
                    loop[layer].uv = uv
        loose = [v for v in verts if not v.link_faces]
        if loose:
            bmesh.ops.delete(self.bm, geom=loose, context="VERTS")

    def commit(self):
        self.bm.faces.index_update()
        count = len(self.bm.faces)
        self.bm.faces.sort(key=lambda face: face.material_index * count + face.index)
        self.bm.faces.index_update()
        self.bm.to_mesh(self.me)
        self.bm.free()
        flat = [0.0] * (len(self.me.loops) * 3)
        self.me.attributes["wb_normal"].data.foreach_get("vector", flat)
        self.me.attributes.remove(self.me.attributes["wb_normal"])
        normals = []
        for i in range(0, len(flat), 3):
            v = Vector(flat[i:i + 3])
            normals.append(tuple(v.normalized()) if v.length > 1e-8 else (0.0, 0.0, 1.0))
        self.me.polygons.foreach_set("use_smooth", [True] * len(self.me.polygons))
        self.me.normals_split_custom_set(normals)
        self.me.update()


def solid_top(m, planks, slot_name, uv_slot):
    lo = min(p.lo.z for p in planks)
    hi = max(p.hi.z for p in planks)
    top = min(planks, key=lambda p: abs(p.centre.z - (lo + hi) / 2))
    density = m.density(uv_slot)
    m.delete([p for p in planks if p is not top])
    m.stretch(top, 2, lo, hi)
    slot = m.slot(slot_name)
    for f in top.faces:
        f.material_index = slot
    m.box_uv(top.faces, density, 0)
    return top, lo, hi


def correct_table(m):
    planks = m.find("wood_light_Classic", lambda p: p.size.x > 1.0 and p.size.y < 0.15, 3, "top plank")
    top, lo, hi = solid_top(m, planks, "wood_light_Classic", m.slot("wood_light_Classic"))
    m.add(rounded_box((top.centre.x, top.hi.y + 0.002, (lo + hi) / 2), (1.0, 0.006, 0.26), 0.0025, 2, 2.0, 0),
          m.slot("red_Classic"))


def correct_desk(m):
    planks = m.find("wood_light_Classic", lambda p: p.size.x > 1.0 and p.size.y < 0.15, 3, "top plank")
    top, lo, hi = solid_top(m, planks, "wood_Classic", m.slot("wood_Classic"))
    m.add(rounded_box((top.centre.x - 0.12, top.hi.y + 0.003, (lo + hi) / 2), (0.56, 0.008, 0.36), 0.003, 2, 2.0, 0),
          m.slot("green_Classic"))


def correct_cabinet(m):
    body = m.find("wood_Classic", lambda p: p.size.y > 1.0 and p.size.x > 0.9, 1, "body")[0]
    dark = m.slot("wood_dark_Classic")
    density = m.density(dark)
    width = body.size.x + 0.008
    back = body.lo.z - 0.004
    crown_front = body.hi.z + 0.03
    plinth_front = body.hi.z + 0.015
    m.add(rounded_box((body.centre.x, body.hi.y - 0.012, (back + crown_front) / 2),
                      (width, 0.04, crown_front - back), 0.012, 4, density, 0), dark)
    m.add(rounded_box((body.centre.x, 0.0375, (back + plinth_front) / 2),
                      (width, 0.075, plinth_front - back), 0.01, 4, density, 0), dark)


def correct_crate(m):
    lid = m.find("wood_light_Classic", lambda p: p.centre.y > 0.4 and p.size.y < 0.06, 3, "lid plank")
    base = m.find("wood_Classic", lambda p: p.centre.y < 0.06, 1, "base")[0]
    braces = m.find("wood_Classic", lambda p: p.size.z < 0.05 and p.size.x > 0.3 and p.size.y > 0.3, 2, "brace")
    ceiling = min(p.lo.y for p in lid) - 0.004
    floor = base.hi.y + 0.004
    for brace in braces:
        centre = brace.centre
        points = [m.unity(v.co) - centre for v in brace.verts]
        sxx = sum(p.x * p.x for p in points)
        syy = sum(p.y * p.y for p in points)
        sxy = sum(p.x * p.y for p in points)
        angle = 0.5 * math.atan2(2 * sxy, sxx - syy)
        length = Vector((math.cos(angle), math.sin(angle), 0.0))

        def moved(p, d):
            return p - length * d if p.dot(length) > 0 else p + length * d

        def fits(d):
            ys = [moved(p, d).y + centre.y for p in points]
            return max(ys) <= ceiling and min(ys) >= floor

        if fits(0.0):
            continue
        low, high = 0.0, 0.15
        if not fits(high):
            raise RuntimeError("CRATE brace cannot be trimmed below the lid")
        for _ in range(40):
            mid = (low + high) / 2
            low, high = (low, mid) if fits(mid) else (mid, high)
        shrink = high
        m.map(brace, lambda p: centre + moved(p - centre, shrink))


def edge_radius(m, body):
    half_x = body.size.x / 2
    points = [m.unity(v.co) for v in body.verts]
    points = [p for p in points if abs(p.x - body.centre.x) < half_x - 0.02]
    hz, hy = body.size.z / 2, body.size.y / 2
    best, best_error = 0.01, float("inf")
    for step in range(4, 150):
        r = step / 1000.0
        error = 0.0
        for p in points:
            qz = abs(p.z - body.centre.z) - (hz - r)
            qy = abs(p.y - body.centre.y) - (hy - r)
            outside = math.hypot(max(qz, 0.0), max(qy, 0.0))
            error += abs(outside + min(max(qz, qy), 0.0) - r)
        if error < best_error:
            best, best_error = r, error
    return best


def tape_strip(body, radius, start, width, lift, density):
    cx, cz, top = body.centre.x, body.centre.z, body.hi.y
    front, back = body.hi.z, body.lo.z
    path = []

    def push(z, y, nz, ny):
        if path and abs(path[-1][0] - z) < 1e-7 and abs(path[-1][1] - y) < 1e-7:
            return
        path.append((z, y, nz, ny))

    for k in range(5):
        push(front, start + (top - radius - start) * k / 4, 1.0, 0.0)
    for z, y, nz, ny in arc(front - radius, top - radius, radius, 0.0, 90.0, 8):
        push(z, y, nz, ny)
    for k in range(9):
        push(front - radius + (back - front + 2 * radius) * k / 8, top, 0.0, 1.0)
    for z, y, nz, ny in arc(back + radius, top - radius, radius, 90.0, 180.0, 8):
        push(z, y, nz, ny)
    for k in range(5):
        push(back, top - radius - (top - radius - start) * k / 4, -1.0, 0.0)
    distance = [0.0]
    for a, b in zip(path, path[1:]):
        distance.append(distance[-1] + math.hypot(b[0] - a[0], b[1] - a[1]))
    geo = Geo()
    left, right = cx - width / 2, cx + width / 2

    def at(k, x, offset):
        z, y, nz, ny = path[k]
        return Vector((x, y + ny * offset, z + nz * offset))

    for k in range(len(path) - 1):
        n0 = Vector((0.0, path[k][3], path[k][2]))
        n1 = Vector((0.0, path[k + 1][3], path[k + 1][2]))
        v0, v1 = distance[k] * density, distance[k + 1] * density
        quad = [geo.point(at(k, left, lift)), geo.point(at(k, right, lift)),
                geo.point(at(k + 1, right, lift)), geo.point(at(k + 1, left, lift))]
        geo.face(quad, [n0, n0, n1, n1], [(0.0, v0), (width * density, v0), (width * density, v1), (0.0, v1)])
        for x, side in ((left, -1.0), (right, 1.0)):
            n = Vector((side, 0.0, 0.0))
            wall = [geo.point(at(k, x, -0.0005)), geo.point(at(k, x, lift)),
                    geo.point(at(k + 1, x, lift)), geo.point(at(k + 1, x, -0.0005))]
            geo.face(wall, [n] * 4, [(v0, 0.0), (v0, lift * density), (v1, lift * density), (v1, 0.0)])
    last = len(path) - 1
    for k, a, b in ((0, 1, 0), (last, last - 1, last)):
        n = Vector((0.0, path[b][1] - path[a][1], path[b][0] - path[a][0])).normalized()
        cap = [geo.point(at(k, left, -0.0005)), geo.point(at(k, right, -0.0005)),
               geo.point(at(k, right, lift)), geo.point(at(k, left, lift))]
        geo.face(cap, [n] * 4, [(0.0, 0.0), (width * density, 0.0), (width * density, lift * density), (0.0, lift * density)])
    return geo


def correct_box(m):
    body_name = "wood_light_Classic" if "wood_light_Classic" in m.slot_names() else "kraft_Classic"
    body = m.find(body_name, lambda p: p.size.x > 0.5, 1, "body")[0]
    tape = m.find("cream_Classic", None, 3, "tape")
    letter = m.find("wood_dark_Classic", None, 1, "letter")[0]
    m.rename("wood_light_Classic", "kraft_Classic")
    cream = m.slot("cream_Classic")
    density = m.density(cream)
    m.delete(tape)
    radius = edge_radius(m, body)
    m.add(tape_strip(body, radius, letter.hi.y + 0.008, 0.115, 0.0018, density), cream)


def correct_shelf(m):
    boards = sorted(m.find("wood_light_Classic", lambda p: p.size.x > 0.7, 4, "board"), key=lambda p: p.centre.y)
    sides = m.find("wood_Classic", lambda p: p.size.y > 1.0, 2, "side panel")
    back = m.find("wood_dark_Classic", lambda p: p.size.y > 1.0, 1, "back panel")[0]
    edge = max(p.hi.z for p in boards)
    for side in sides:
        m.stretch(side, 2, side.lo.z, edge)
    front = edge - 0.005
    rear = back.centre.z
    for board in boards:
        m.stretch(board, 2, rear, front)
    inner = min(abs(p.centre.x) - p.size.x / 2 for p in sides)
    gaps = [(boards[k].hi.y, boards[k + 1].lo.y) for k in range(3)]
    slots = {name: m.slot(name + "_Classic") for name in ("red", "blue", "green")}
    depth, middle = 0.2, (rear + front) / 2 + 0.01
    rows = [(0, 1.0, [(0.06, 0.25, "green"), (0.05, 0.23, "red"), (0.05, 0.2, "blue")]),
            (1, -1.0, [(0.05, 0.24, "red"), (0.045, 0.21, "blue"), (0.06, 0.26, "green"), (0.05, 0.22, "red"),
                       (0.055, 0.25, "blue")]),
            (2, -1.0, [(0.05, 0.23, "blue"), (0.055, 0.26, "green")])]
    for gap, side, books in rows:
        floor, ceiling = gaps[gap]
        x = side * (inner - 0.015)
        for width, height, colour in books:
            height = min(height, ceiling - floor - 0.03)
            m.add(rounded_box((x - side * width / 2, floor - 0.0005 + height / 2, middle), (width, height, depth),
                              0.006, 2, 2.0, 1), slots[colour])
            x -= side * (width + 0.004)
    top = boards[-1].hi.y
    thickness = min(0.026, min(p.hi.y for p in sides) - top - 0.006)
    for x, z, turn, size, colour in ((0.12, -0.01, 8.0, (0.3, thickness, 0.21), "red"),
                                     (-0.2, 0.005, -6.0, (0.26, thickness * 0.85, 0.19), "blue")):
        m.add(rounded_box((x, top - 0.0005 + size[1] / 2, middle + z), size, 0.006, 2, 2.0, 0,
                          Matrix.Rotation(math.radians(turn), 3, "Y")), slots[colour])


def planar_top(m, part, slot_name, threshold):
    density = m.density(m.slot(slot_name))
    m.planar_uv([f for f in part.faces if m.face_normal(f).y > threshold], (0, 2), density)


def correct_chest(m):
    planar_top(m, m.find("wood_light_Classic", None, 1, "lid")[0], "wood_light_Classic", 0.6)


def correct_stool(m):
    planar_top(m, m.find("wood_light_Classic", None, 1, "seat")[0], "wood_light_Classic", 0.6)


def correct_barrel(m):
    body = m.find("wood_Classic", lambda p: p.size.y > 0.5, 1, "body")[0]
    density = m.density(m.slot("wood_Classic"))
    faces = [f for f in body.faces if m.face_normal(f).y > 0.9
             and sum(m.unity(v.co).y for v in f.verts) / len(f.verts) > body.centre.y]
    m.planar_uv(faces, (0, 2), density)


def apex(m, part):
    centre = part.centre
    points = [m.unity(v.co) - centre for v in part.verts]
    best, best_score = Vector((1.0, 0.0, 0.0)), 0
    for axis, other in ((0, 2), (2, 0)):
        half_axis, half_other = part.size[axis] / 2, part.size[other] / 2
        near = [p for p in points if abs(p[other]) < 0.15 * half_other]
        score = sum(1 for p in near if p[axis] > 0.1 * half_axis) - sum(1 for p in near if p[axis] < -0.1 * half_axis)
        if abs(score) > abs(best_score):
            best_score = score
            best = Vector([math.copysign(1.0, score) if k == axis else 0.0 for k in range(3)])
    if best_score == 0:
        raise RuntimeError("MAT chevron direction is ambiguous")
    return best


def correct_mat(m):
    chevrons = sorted(m.find("ivory_Classic", None, 3, "chevron"), key=lambda p: p.centre.x)
    border = m.find("cream_Classic", None, 1, "border")[0]
    spacing = min(0.18, border.size.z * 0.3)
    for k, chevron in enumerate(chevrons):
        centre = chevron.centre
        tip = apex(m, chevron)
        turn = Matrix.Rotation(math.atan2(-tip.x, tip.z), 3, "Y")
        linear = Matrix(((1.5, 0.0, 0.0), (0.0, 1.0, 0.0), (0.0, 0.0, 0.75))) @ turn
        target = Vector((border.centre.x, centre.y, border.centre.z + (k - 1) * spacing))
        m.map(chevron, lambda p, c=centre, t=target, l=linear: t + l @ (p - c), linear)


def disc(centre, axis, radius, face, base, density, segments=64, side=True):
    outward = 1.0 if face > base else -1.0
    geo = lathe([(0.0, face, 0.0, outward), (radius, face, 0.0, outward)], segments, axis, centre, density, True)
    if side:
        geo.extend(lathe([(radius, face, 1.0, 0.0), (radius, base, 1.0, 0.0)], segments, axis, centre, density))
    return geo


def correct_clock(m):
    body = m.find("wood_Classic", None, 1, "body")[0]
    face = m.find("ivory_Classic", None, 1, "face")[0]
    hands = m.find("dark_Classic", None, 2, "hand")
    ticks = m.find("wood_dark_Classic", None, 12, "tick")
    m.find("gold_Classic", None, 2, "bezel and hub")
    wood, ivory, gold = m.slot("wood_Classic"), m.slot("ivory_Classic"), m.slot("gold_Classic")
    wood_dark, dark = m.slot("wood_dark_Classic"), m.slot("dark_Classic")
    density = m.density(wood)
    cx, cy = body.centre.x, body.centre.y
    centre = (cx, cy, 0.0)
    radius, back, front = body.size.x / 2, body.lo.z, body.hi.z
    face_radius, face_front = face.size.x / 2, face.hi.z
    hand_shapes = []
    for hand in hands:
        points = [m.unity(v.co) for v in hand.verts]
        tip = max(points, key=lambda p: math.hypot(p.x - cx, p.y - cy))
        along = Vector((tip.x - cx, tip.y - cy, 0.0)).normalized()
        across = [(p.x - cx) * -along.y + (p.y - cy) * along.x for p in points]
        hand_shapes.append((tip.x - cx, tip.y - cy, max(across) - min(across)))
    tick_centres = [(p.centre.x, p.centre.y) for p in ticks]
    m.delete([body, face])
    bevel = 0.006
    m.add(lathe([(radius - bevel, back, 0.0, -1.0), (0.0, back, 0.0, -1.0)], 64, "z", centre, density, True), wood)
    rim = arc(radius - bevel, back + bevel, bevel, -90.0, 0.0, 3) + arc(radius - bevel, front - bevel, bevel, 0.0, 90.0, 3)
    m.add(lathe(rim, 64, "z", centre, density), wood)
    m.add(lathe([(0.0, front, 0.0, 1.0), (radius - bevel, front, 0.0, 1.0)], 64, "z", centre, density, True), wood)
    m.add(disc(centre, "z", face_radius, face_front, front - 0.001, density), ivory)
    plate = back - 0.002
    m.add(disc(centre, "z", 0.2 * radius / 0.24, plate, back + 0.0005, density), ivory)
    inner_ring, outer_ring = 0.2 * radius / 0.24, 0.222 * radius / 0.24
    m.add(lathe([(inner_ring, back + 0.0005, -1.0, 0.0), (inner_ring, back - 0.0035, -1.0, 0.0)], 64, "z", centre), gold)
    m.add(lathe([(inner_ring, back - 0.0035, 0.0, -1.0), (outer_ring, back - 0.0035, 0.0, -1.0)], 64, "z", centre,
                density, True), gold)
    m.add(lathe([(outer_ring, back - 0.0035, 1.0, 0.0), (outer_ring, back + 0.0005, 1.0, 0.0)], 64, "z", centre), gold)
    for tx, ty in tick_centres:
        m.add(disc((2 * cx - tx, ty, 0.0), "z", 0.009, plate - 0.0025, plate + 0.0005, density, 16), wood_dark)
    for dx, dy, width in hand_shapes:
        length = math.hypot(dx, dy)
        angle = math.atan2(dy, -dx)
        turn = Matrix.Rotation(angle, 3, "Z")
        offset = turn @ Vector((length / 2 - 0.005, 0.0, 0.0))
        m.add(rounded_box((cx + offset.x, cy + offset.y, plate - 0.0015), (length + 0.01, max(width, 0.01), 0.002),
                          0.0009, 2, density, 0, turn), dark)
    m.add(disc(centre, "z", 0.011, plate - 0.0035, plate + 0.0005, density, 24), gold)


def correct_fan(m):
    ring = m.find("blue_Classic", lambda p: p.centre.y > 0.3, 1, "ring")[0]
    grille = m.find("metal_Classic", lambda p: abs(p.centre.y - ring.centre.y) < 0.05, 4, "grille")
    metal = m.slot("metal_Classic")
    m.delete(grille)
    tube = ring.size.z / 2
    major = ring.size.x / 2 - tube
    wire = 0.011
    centre = (ring.centre.x, ring.centre.y, 0.0)
    hoops = [(major, ring.centre.z + tube + 0.004), (major * 0.66, ring.centre.z + tube + 0.024),
             (major * 0.32, ring.centre.z + tube + 0.038)]
    for radius, depth in hoops:
        m.add(torus(centre, "z", radius, wire, depth), metal)
    for k in range(6):
        angle = math.radians(90.0 + 60.0 * k)
        points = [Vector((ring.centre.x + r * math.cos(angle), ring.centre.y + r * math.sin(angle), d)) for r, d in hoops]
        m.add(rod(points[0], points[1], wire * 0.85), metal)
        m.add(rod(points[1], points[2], wire * 0.85), metal)


def correct_vase(m):
    body = m.find("ceramic_Classic", None, 1, "body")[0]
    lip = m.find("gold_Classic", None, 1, "lip ring")[0]
    if abs(body.size.y - 0.492) > 0.01 or abs(body.size.x - 0.33) > 0.01:
        raise RuntimeError("VASE no longer matches the profile this correction was measured from")
    ceramic, gold, blue = m.slot("ceramic_Classic"), m.slot("gold_Classic"), m.slot("blue_Classic")
    density = m.density(ceramic)
    cx, cz = body.centre.x, body.centre.z
    m.delete([body, lip])
    centre = (cx, 0.0, cz)
    outer_knots = [(0.1, 0.0), (0.13, 0.025), (0.165, 0.15), (0.135, 0.29), (0.065, 0.37), (0.055, 0.46), (0.074, 0.48)]
    inner_knots = [(0.049, 0.492), (0.041, 0.46), (0.05, 0.38), (0.12, 0.28), (0.146, 0.15), (0.095, 0.028)]
    m.add(lathe([(0.0, 0.0, 0.0, -1.0), (0.1, 0.0, 0.0, -1.0)], 48, "y", centre, density, True), ceramic)
    m.add(lathe(with_normals(spline(outer_knots, 22)), 48, "y", centre, density), ceramic)
    m.add(lathe([(0.074, 0.48, 1.0, 0.0), (0.074, 0.492, 1.0, 0.0)], 48, "y", centre, density), ceramic)
    m.add(lathe([(0.074, 0.492, 0.0, 1.0), (0.049, 0.492, 0.0, 1.0)], 48, "y", centre, density, True), ceramic)
    m.add(lathe(with_normals(spline(inner_knots, 12)), 48, "y", centre, density), ceramic)
    m.add(lathe([(0.095, 0.028, 0.0, 1.0), (0.0, 0.028, 0.0, 1.0)], 48, "y", centre, density, True), ceramic)
    belt = [p for p in with_normals(spline(outer_knots, 48)) if 0.11 <= p[1] <= 0.19]
    band = [(r + nr * 0.0015, h + nh * 0.0015, nr, nh) for r, h, nr, nh in belt]
    m.add(lathe(band, 48, "y", centre, density), blue)
    for point, sign in ((belt[0], -1.0), (belt[-1], 1.0)):
        r, h, nr, nh = point
        tr, th = -nh * sign, nr * sign
        m.add(lathe([(r, h, tr, th), (r + nr * 0.0015, h + nh * 0.0015, tr, th)], 48, "y", centre), blue)
    m.add(torus(centre, "y", 0.0755, 0.005, 0.486), gold)


def correct_plate(m):
    ring = m.find("gold_Classic", None, 1, "rim ring")[0]
    body = m.find("ceramic_Classic", lambda p: p.size.x > 0.5, 1, "plate")[0]
    gold = m.slot("gold_Classic")
    scale_r, scale_h = body.size.x / 0.54, body.hi.y / 0.0735
    m.delete([ring])
    rim = [(0.235, 0.063), (0.259, 0.073), (0.27, 0.066)]
    rim = [(r * scale_r, h * scale_h) for r, h in rim]

    def height(r):
        for (r0, h0), (r1, h1) in zip(rim, rim[1:]):
            if r0 <= r <= r1:
                return h0 + (h1 - h0) * (r - r0) / (r1 - r0)
        return rim[-1][1]

    def normal(r0, h0, r1, h1):
        n = Vector((-(h1 - h0), r1 - r0))
        return n.normalized()

    n1 = normal(*rim[0], *rim[1])
    n2 = normal(*rim[1], *rim[2])
    peak = (n1 + n2).normalized()
    radii = [0.243 * scale_r, 0.251 * scale_r, rim[1][0], 0.2625 * scale_r, 0.266 * scale_r]
    lift = 0.0012
    band = []
    for r in radii:
        n = peak if abs(r - rim[1][0]) < 1e-6 else n1 if r < rim[1][0] else n2
        band.append((r + n.x * lift, height(r) + n.y * lift, n.x, n.y))
    centre = (body.centre.x, 0.0, body.centre.z)
    m.add(lathe(band, 64, "y", centre, 2.0), gold)
    for point, sign in ((band[0], -1.0), (band[-1], 1.0)):
        r, h, nr, nh = point
        base_r, base_h = r - nr * (lift + 0.0005), h - nh * (lift + 0.0005)
        m.add(lathe([(base_r, base_h, nh * sign, -nr * sign), (r, h, nh * sign, -nr * sign)], 64, "y", centre), gold)


def ring_profile(inner, outer, base, top, bevel=0.0012):
    return ([(inner, base, -1.0, 0.0), (inner, top - bevel, -1.0, 0.0)]
            + arc(inner + bevel, top - bevel, bevel, 180.0, 90.0, 2)
            + arc(outer - bevel, top - bevel, bevel, 90.0, 0.0, 2)
            + [(outer, top - bevel, 1.0, 0.0), (outer, base, 1.0, 0.0)])


def correct_rug(m):
    trim = m.find("cream_Classic", None, 1, "trim")[0]
    rug = m.find("blue_Classic", None, 1, "rug")[0]
    cream = m.slot("cream_Classic")
    density = m.density(cream)
    m.delete([trim])
    surface = rug.hi.y
    edge = rug.size.x / 2 - 0.025
    centre = (rug.centre.x, 0.0, rug.centre.z)
    m.add(lathe(ring_profile(edge - 0.115, edge, surface - 0.0008, surface + 0.0018), 96, "y", centre, density, True),
          cream)
    m.add(lathe(ring_profile(0.53, 0.58, surface - 0.0008, surface + 0.0014), 96, "y", centre, density, True), cream)


TARGETS = {
    "TABLE": (ITEMS + "TABLE.fbx", correct_table),
    "DESK": (ITEMS + "DESK.fbx", correct_desk),
    "CABINET": (ITEMS + "CABINET.fbx", correct_cabinet),
    "CRATE": (ITEMS + "CRATE.fbx", correct_crate),
    "BOX": (ITEMS + "BOX.fbx", correct_box),
    "SHELF": (ITEMS + "SHELF.fbx", correct_shelf),
    "CHEST": (ITEMS + "CHEST.fbx", correct_chest),
    "STOOL": (ITEMS + "STOOL.fbx", correct_stool),
    "BARREL": (ITEMS + "BARREL.fbx", correct_barrel),
    "MAT": (ITEMS + "MAT.fbx", correct_mat),
    "CLOCK": (ITEMS + "CLOCK.fbx", correct_clock),
    "FAN": (ITEMS + "FAN.fbx", correct_fan),
    "VASE": (ITEMS + "VASE.fbx", correct_vase),
    "PLATE": (ITEMS + "PLATE.fbx", correct_plate),
    "RUG": (RUG, correct_rug),
}


def reset():
    bpy.ops.wm.read_factory_settings(use_empty=True)


def load(path, single=True):
    reset()
    bpy.ops.import_scene.fbx(filepath=path, use_custom_normals=True)
    bpy.context.view_layer.update()
    meshes = [o for o in bpy.data.objects if o.type == "MESH"]
    if not single:
        return meshes
    if len(meshes) != 1:
        raise RuntimeError(f"{path}: expected one mesh object, found {[o.name for o in meshes]}")
    return meshes[0]


def unity_vector(v):
    return [round(v.x, 5), round(v.z, 5), round(v.y, 5)]


def describe():
    meshes = [o for o in bpy.data.objects if o.type == "MESH"]
    lo = Vector((float("inf"),) * 3)
    hi = Vector((float("-inf"),) * 3)
    triangles = 0
    for obj in meshes:
        for v in obj.data.vertices:
            w = obj.matrix_world @ v.co
            lo = Vector(map(min, lo, w))
            hi = Vector(map(max, hi, w))
        obj.data.calc_loop_triangles()
        triangles += len(obj.data.loop_triangles)
    return {"triangles": triangles, "meshes": sorted(o.name for o in meshes),
            "materials": sorted({s.material.name for o in meshes for s in o.material_slots if s.material}),
            "empties": sorted(o.name for o in bpy.data.objects if o.type == "EMPTY"),
            "bounds_min": unity_vector(lo), "bounds_max": unity_vector(hi)}


def export_fbx(path):
    os.makedirs(os.path.dirname(path), exist_ok=True)
    bpy.ops.export_scene.fbx(
        filepath=path,
        use_selection=False,
        object_types={"EMPTY", "MESH", "ARMATURE"},
        apply_unit_scale=True,
        apply_scale_options="FBX_SCALE_UNITS",
        axis_forward="-Z",
        axis_up="Y",
        bake_space_transform=False,
        use_mesh_modifiers=True,
        mesh_smooth_type="FACE",
        use_tspace=False,
        use_custom_props=True,
        add_leaf_bones=False,
        primary_bone_axis="Y",
        secondary_bone_axis="X",
        use_armature_deform_only=False,
        armature_nodetype="NULL",
        bake_anim=False,
        path_mode="STRIP",
        embed_textures=False,
    )
    return os.path.getsize(path)


def size_problems(before, after, authored=None):
    problems = []
    for k, axis in enumerate("xyz"):
        old = before["bounds_max"][k] - before["bounds_min"][k]
        new = after["bounds_max"][k] - after["bounds_min"][k]
        if old > 1e-6 and abs(new - old) / old > SIZE_TOLERANCE:
            problems.append(f"{axis} size {old:.4f} -> {new:.4f}")
        if authored and abs(new - authored[k]) > AUTHORED_TOLERANCE:
            problems.append(f"{axis} size {new:.4f} vs authored {authored[k]:.4f}")
    if abs(after["bounds_min"][1]) > FLOOR_TOLERANCE:
        problems.append(f"bottom at {after['bounds_min'][1]:.4f}")
    return problems


def uv_values(layer):
    if layer is None:
        return []
    if hasattr(layer, "uv"):
        return sorted(tuple(round(x, 4) for x in d.vector) for d in layer.uv)
    return sorted(tuple(round(x, 4) for x in d.uv) for d in layer.data)


def snapshot(obj):
    me = obj.data
    world = obj.matrix_world
    normal_matrix = world.to_3x3().inverted().transposed()
    corner = [0.0] * (len(me.loops) * 3)
    me.corner_normals.foreach_get("vector", corner)
    uv = me.uv_layers.active
    return {
        "objects": sorted((o.name, o.type, o.parent.name if o.parent else "",
                           tuple(round(x, 5) for row in o.matrix_world for x in row)) for o in bpy.data.objects),
        "mesh": me.name,
        "slots": [s.material.name if s.material else "" for s in obj.material_slots],
        "positions": sorted(tuple(round(x, 5) for x in world @ v.co) for v in me.vertices),
        "normals": sorted(tuple(round(x, 3) for x in (normal_matrix @ Vector(corner[i:i + 3])).normalized())
                          for i in range(0, len(corner), 3)),
        "uvs": uv_values(uv),
        "polygons": len(me.polygons),
        "properties": sorted(k for k in obj.keys() if not k.startswith("_")),
    }


def facts(obj):
    me = obj.data
    return {
        "objects": [{"name": o.name, "type": o.type, "parent": o.parent.name if o.parent else "",
                     "location": [round(x, 6) for x in o.location], "rotation_mode": o.rotation_mode,
                     "rotation_euler": [round(math.degrees(x), 4) for x in o.rotation_euler],
                     "scale": [round(x, 6) for x in o.scale],
                     "matrix_world": [[round(x, 6) for x in row] for row in o.matrix_world]}
                    for o in bpy.data.objects],
        "mesh": me.name, "slots": [s.material.name if s.material else "" for s in obj.material_slots],
        "uv_layers": [layer.name for layer in me.uv_layers],
        "attributes": [(a.name, a.domain, a.data_type) for a in me.attributes],
        "has_custom_normals": bool(getattr(me, "has_custom_normals", False)),
        "vertices": len(me.vertices), "polygons": len(me.polygons), "loops": len(me.loops),
        "polygon_sizes": sorted({len(p.vertices) for p in me.polygons}),
        "custom_properties": {k: str(obj[k]) for k in obj.keys()},
        "describe": describe(),
    }


def compare(a, b):
    def worst(x, y):
        if len(x) != len(y):
            return f"count {len(x)} vs {len(y)}"
        return max((max(abs(p - q) for p, q in zip(u, v)) for u, v in zip(x, y)), default=0.0)

    return {"objects_equal": [o[:3] for o in a["objects"]] == [o[:3] for o in b["objects"]],
            "matrix_max_diff": worst([o[3] for o in a["objects"]], [o[3] for o in b["objects"]]),
            "mesh_equal": a["mesh"] == b["mesh"], "slots_equal": a["slots"] == b["slots"],
            "position_max_diff": worst(a["positions"], b["positions"]),
            "normal_max_diff": worst(a["normals"], b["normals"]),
            "uv_max_diff": worst(a["uvs"], b["uvs"]),
            "polygons": [a["polygons"], b["polygons"]]}


def inspect(name, path, out, specs):
    obj = load(path)
    record = facts(obj)
    editable = Editable(obj, specs)
    record["parts"] = [p.summary() for p in editable.parts()]
    record["uv_density_by_slot"] = {n: round(editable.density(i), 3) for i, n in enumerate(editable.slot_names())}
    editable.bm.free()
    original = snapshot(load(path))
    trip = os.path.join(out, "roundtrip", os.path.basename(path))
    export_fbx(trip)
    record["roundtrip"] = compare(original, snapshot(load(trip)))
    return record


def slots_in_first_use_order(obj):
    seen = []
    for polygon in obj.data.polygons:
        if polygon.material_index not in seen:
            seen.append(polygon.material_index)
    return seen == sorted(seen)


def correct(name, path, operation, specs, target, backup, force, authored=None):
    obj = load(path)
    marked = obj.get(MARKER) and not force
    if marked and slots_in_first_use_order(obj):
        return {"status": "skipped", "marker": str(obj[MARKER])}
    before = describe()
    editable = Editable(obj, specs)
    if not marked:
        operation(editable)
    editable.commit()
    if not slots_in_first_use_order(obj):
        raise RuntimeError(f"{name}: faces are not grouped in slot order")
    if not marked:
        obj[MARKER] = VERSION
    after = describe()
    problems = size_problems(before, after, authored)
    if problems:
        raise RuntimeError(f"{name}: size check failed: " + "; ".join(problems))
    if backup:
        kept = os.path.join(backup, os.path.basename(path))
        if not os.path.exists(kept):
            os.makedirs(backup, exist_ok=True)
            shutil.copy2(path, kept)
    size = export_fbx(target)
    return {"status": "reordered" if marked else "corrected", "before": before, "after": after, "bytes": size}


def load_json(path):
    with open(path, encoding="utf-8") as handle:
        return json.load(handle)


def save_json(path, payload):
    os.makedirs(os.path.dirname(path), exist_ok=True)
    with open(path, "w", encoding="utf-8") as handle:
        json.dump(payload, handle, indent=1)


def material_specs(repo):
    specs = {m["name"]: dict(m) for m in load_json(os.path.join(repo, MATERIALS))["materials"]}
    for name, colour in RECOLOURS.items():
        specs[name]["baseColor"] = colour
    for spec in ADDED:
        specs[spec["name"]] = dict(spec)
    return specs


def amended_materials(repo, owners):
    library = load_json(os.path.join(repo, MATERIALS))
    by_name = {m["name"]: m for m in library["materials"]}
    for name, colour in RECOLOURS.items():
        by_name[name]["baseColor"] = colour
    for spec in ADDED:
        if spec["name"] in by_name:
            by_name[spec["name"]].update({k: v for k, v in spec.items() if k != "sources"})
        else:
            by_name[spec["name"]] = dict(spec)
    for name, users in owners.items():
        entry = by_name.get(name)
        if entry is not None:
            entry["sources"] = entry.get("sources", []) + [u for u in users if u not in entry.get("sources", [])]
    for spec in ADDED:
        classic = by_name.get(spec["family"] + "_Classic")
        if spec["skin"] != "Classic" and classic is not None:
            by_name[spec["name"]]["sources"] = list(classic["sources"])
    library["materials"] = [by_name[n] for n in sorted(by_name)]
    return library


def amended_report(repo, updates):
    report = load_json(os.path.join(repo, REPORT))
    for entry in report["files"]:
        if entry["output"] in updates:
            entry.update(updates[entry["output"]])
    return report


def refresh(repo, names):
    report = load_json(os.path.join(repo, REPORT))
    wanted = {n.strip() for n in names.split(",") if n.strip()}
    updates, found = {}, set()
    for entry in report["files"]:
        if entry["name"] not in wanted:
            continue
        found.add(entry["name"])
        path = os.path.join(repo, entry["output"])
        load(path, single=False)
        updates[entry["output"]] = dict(describe(), bytes=os.path.getsize(path))
    missing = sorted(wanted - found)
    save_json(os.path.join(repo, REPORT), amended_report(repo, updates))
    print("REFRESH_RESULT " + json.dumps({"refreshed": sorted(found), "missing": missing}))
    if missing:
        sys.exit(1)


def main():
    args = parse_args()
    repo = os.path.abspath(args.repo)
    if args.refresh_report:
        refresh(repo, args.refresh_report)
        return
    specs = material_specs(repo)
    only = {n.strip() for n in args.only.split(",") if n.strip()}
    unknown = sorted(only - set(TARGETS))
    if unknown:
        raise RuntimeError(f"unknown models: {unknown}")
    out = os.path.abspath(args.out) if args.out else ""
    if args.inspect and not out:
        raise RuntimeError("--inspect needs --out")
    authored = {e["output"]: e.get("authored_size_xyz") for e in load_json(os.path.join(repo, REPORT))["files"]}
    results, failures, inspections, updates, owners = {}, [], {}, {}, {}
    for name, (relative, operation) in TARGETS.items():
        if only and name not in only:
            continue
        path = os.path.join(repo, relative)
        try:
            if args.inspect:
                inspections[name] = inspect(name, path, out, specs)
            target = os.path.join(out, "corrected", os.path.basename(path)) if out else path
            source = os.path.join(os.path.abspath(args.source), os.path.basename(relative)) if args.source else path
            if not os.path.exists(source):
                raise RuntimeError(f"missing source {source}")
            result = correct(name, source, operation, specs, target, args.backup, args.force, authored.get(relative))
            results[name] = result
            if result["status"] in ("corrected", "reordered"):
                kind = "environment" if relative == RUG else "item"
                for material in result["after"]["materials"]:
                    owners.setdefault(material, []).append(f"{kind}/{os.path.splitext(os.path.basename(relative))[0]}")
                updates[relative] = dict(result["after"], bytes=result["bytes"], corrected_by=SCRIPT, correction=VERSION)
            print(f"MODEL {name} {result['status']}", flush=True)
        except Exception as error:
            traceback.print_exc()
            failures.append(name)
            results[name] = {"status": "failed", "error": str(error)}
            print(f"MODEL {name} failed: {error}", flush=True)
    materials = amended_materials(repo, owners)
    report = amended_report(repo, updates)
    report["material_count"] = len(materials["materials"])
    if out:
        save_json(os.path.join(out, "materials.json"), materials)
        save_json(os.path.join(out, "build_report.json"), report)
        save_json(os.path.join(out, "correction.json"), {"version": VERSION, "results": results})
        if args.inspect:
            save_json(os.path.join(out, "inspection.json"), inspections)
    else:
        for relative in (MATERIALS, REPORT):
            kept = os.path.join(args.backup, os.path.basename(relative)) if args.backup else ""
            if kept and not os.path.exists(kept):
                os.makedirs(args.backup, exist_ok=True)
                shutil.copy2(os.path.join(repo, relative), kept)
        save_json(os.path.join(repo, MATERIALS), materials)
        save_json(os.path.join(repo, REPORT), report)
    summary = {name: {"status": r["status"],
                      "triangles": [r["before"]["triangles"], r["after"]["triangles"]] if "after" in r else None,
                      "materials": r["after"]["materials"] if "after" in r else None,
                      "bounds": [r["after"]["bounds_min"], r["after"]["bounds_max"]] if "after" in r else None}
               for name, r in results.items()}
    print("CORRECTION_RESULT " + json.dumps({"version": VERSION, "in_place": not out, "failures": failures,
                                             "models": summary}))
    if failures:
        sys.exit(1)


try:
    main()
except SystemExit:
    raise
except Exception as exc:
    traceback.print_exc()
    print("CORRECTION_FAILED " + str(exc))
    sys.exit(1)
