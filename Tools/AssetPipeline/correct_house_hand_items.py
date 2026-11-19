import argparse
import json
import math
import os
import shutil
import sys
import tempfile
import traceback

import bmesh
import bpy
from mathutils import Vector

VERSION = "d4-1"
MARK = "wb_d4"
SCRIPT = "Tools/AssetPipeline/correct_house_hand_items.py"
WORDS = ("APPLE", "BALL", "BOMB", "SODA", "MUG", "WATER", "PIE", "PAN", "POT", "SOAP", "FOAM",
         "BLADE", "SPEAR", "ARROW", "BOW", "SHIELD")
ITEMS = "Assets/_Project/Art/Imported/Items"
REPORT = "Assets/_Project/Data/Generated/build_report.json"
MATERIALS = "Assets/_Project/Data/Generated/materials.json"
CATALOGUES = ("Assets/_Project/Data/Config/items.json", "Assets/_Project/Data/Config/items_house.json")
APPEND = {"PIE": ["red_Classic"], "SPEAR": ["red_Classic"]}
THICKEN = {"SPEAR": (2,)}
LONG = ("BLADE", "SPEAR", "ARROW", "BOW")
TOLERANCE = 0.05
CATALOGUE_MARGIN = 0.019
AUTHORED_MARGIN = 0.02
TAU = 2.0 * math.pi
VIEWS = (("front", (0.0, 0.25, 1.0)), ("left", (-1.0, 0.25, 0.0)),
         ("three-quarter", (-0.579, 0.574, 0.579)), ("party", (0.0, 30.0, -22.0)))


def parse_args():
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    p = argparse.ArgumentParser()
    p.add_argument("--repo", required=True)
    p.add_argument("--only", default="")
    p.add_argument("--out", default="")
    p.add_argument("--report", default="")
    p.add_argument("--preview", default="")
    p.add_argument("--inspect", action="store_true")
    return p.parse_args(argv)


def need(ok, message):
    if not ok:
        raise RuntimeError(message)


def fresh():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    units = bpy.context.scene.unit_settings
    units.system = "METRIC"
    units.scale_length = 1.0


def import_fbx(path):
    fresh()
    try:
        bpy.ops.import_scene.fbx(filepath=path, use_custom_normals=True)
    except (AttributeError, RuntimeError, TypeError):
        bpy.ops.wm.fbx_import(filepath=path)
    units = bpy.context.scene.unit_settings
    units.system = "METRIC"
    units.scale_length = 1.0


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


def unity(world):
    return Vector((world.x, world.z, world.y))


def scene_facts(word):
    obj = bpy.data.objects.get(word)
    need(obj is not None and obj.type == "MESH", f"{word}: no mesh object named {word}")
    lo = [math.inf] * 3
    hi = [-math.inf] * 3
    tris = 0
    for o in (o for o in bpy.data.objects if o.type == "MESH"):
        mw = o.matrix_world
        for v in o.data.vertices:
            u = unity(mw @ v.co)
            for i in range(3):
                lo[i] = min(lo[i], u[i])
                hi[i] = max(hi[i], u[i])
        o.data.calc_loop_triangles()
        tris += len(o.data.loop_triangles)
    grips = [o for o in bpy.data.objects if o.name.startswith("Grip_R")]
    grip = unity(grips[0].matrix_world.translation) if grips else None
    marker = obj.get(MARK)
    return {
        "objects": sorted(f"{o.name}:{o.type}:{o.parent.name if o.parent else ''}" for o in bpy.data.objects),
        "mesh": obj.data.name,
        "slots": [s.material.name if s.material else "" for s in obj.material_slots],
        "used": sorted({p.material_index for p in obj.data.polygons}),
        "triangles": tris,
        "vertices": len(obj.data.vertices),
        "min": lo,
        "max": hi,
        "grip": list(grip) if grip else None,
        "matrix": [list(row) for row in obj.matrix_world],
        "marker": str(marker) if marker is not None else None,
    }


def inset(poly, shifts):
    n = len(poly)
    lines = []
    for i in range(n):
        a = Vector(poly[i])
        b = Vector(poly[(i + 1) % n])
        t = (b - a).normalized()
        lines.append((a + Vector((-t.y, t.x)) * shifts[i], t))
    out = []
    for i in range(n):
        p1, t1 = lines[i - 1]
        p2, t2 = lines[i]
        den = t1.x * t2.y - t1.y * t2.x
        if abs(den) < 1e-9:
            out.append((p2.x, p2.y))
            continue
        s = ((p2.x - p1.x) * t2.y - (p2.y - p1.y) * t2.x) / den
        q = p1 + t1 * s
        out.append((q.x, q.y))
    return out


def inside(poly, p):
    n = len(poly)
    for i in range(n):
        a, b = poly[i], poly[(i + 1) % n]
        if (b[0] - a[0]) * (p[1] - a[1]) - (b[1] - a[1]) * (p[0] - a[0]) < -1e-9:
            return False
    return True


def rounded_rect(hx, hz, rc, steps):
    rc = min(rc, hx, hz)
    pts = []
    for cx, cz, a0 in ((hx - rc, hz - rc, 0.0), (rc - hx, hz - rc, 90.0), (rc - hx, rc - hz, 180.0), (hx - rc, rc - hz, 270.0)):
        for k in range(steps + 1):
            a = math.radians(a0 + 90.0 * k / steps)
            pts.append((cx + rc * math.cos(a), cz + rc * math.sin(a)))
    return pts


def interpolate(table, y):
    if y <= table[0][0]:
        return table[0][1]
    for (y0, r0), (y1, r1) in zip(table, table[1:]):
        if y <= y1:
            return r0 + (r1 - r0) * (y - y0) / max(y1 - y0, 1e-9)
    return table[-1][1]


def on_sphere(c, radius):
    def place(p, t, front, back):
        dx, dy = p.x - c[0], p.y - c[1]
        d = Vector((dx, dy, math.sqrt(max(radius * radius - dx * dx - dy * dy, 1e-10)))).normalized()
        return Vector(c) + d * (radius - back + t * (front + back))
    return place


def on_cylinder(cx, cz, radius):
    def place(p, t, front, back):
        r = radius(p.y)
        dx = p.x - cx
        d = Vector((dx, 0.0, math.sqrt(max(r * r - dx * dx, 1e-10)))).normalized()
        rho = r - back + t * (front + back)
        return Vector((cx + d.x * rho, p.y, cz + d.z * rho))
    return place


class Model:
    def __init__(self, word, path, specs):
        import_fbx(path)
        self.word = word
        self.specs = specs
        self.obj = bpy.data.objects.get(word)
        need(self.obj is not None and self.obj.type == "MESH", f"{word}: no mesh object named {word}")
        self.mesh = self.obj.data
        self.slots = [s.material.name if s.material else "" for s in self.obj.material_slots]
        self.matrix = self.obj.matrix_world.copy()
        self.inverse = self.matrix.inverted()
        self.turn = self.inverse.to_3x3()
        self.bm = None
        self.deviation = 0.0

    def add_slot(self, name):
        mat = bpy.data.materials.get(name) or bpy.data.materials.new(name)
        spec = self.specs.get(name)
        if spec:
            mat.diffuse_color = spec["baseColor"]
        self.mesh.materials.append(mat)
        self.slots.append(name)

    def begin(self):
        mesh = self.mesh
        if "wb_n" in mesh.attributes:
            mesh.attributes.remove(mesh.attributes["wb_n"])
        flat = []
        for n in mesh.corner_normals:
            flat.extend(n.vector)
        attr = mesh.attributes.new("wb_n", "FLOAT_VECTOR", "CORNER")
        attr.data.foreach_set("vector", flat)
        self.bm = bmesh.new()
        self.bm.from_mesh(mesh)
        self.nl = self.bm.loops.layers.float_vector.get("wb_n")
        need(self.nl is not None, f"{self.word}: corner normal layer missing in bmesh")
        self.uv = self.bm.loops.layers.uv.verify()
        self.bm.normal_update()

    def end(self):
        bm = self.bm
        for f in bm.faces:
            f.smooth = True
        bm.normal_update()
        bm.to_mesh(self.mesh)
        bm.free()
        self.bm = None
        mesh = self.mesh
        values = [0.0] * (len(mesh.loops) * 3)
        mesh.attributes["wb_n"].data.foreach_get("vector", values)
        mesh.attributes.remove(mesh.attributes["wb_n"])
        normals = []
        missing = 0
        for i in range(len(mesh.loops)):
            n = Vector(values[3 * i:3 * i + 3])
            if n.length < 0.5:
                missing += 1
                n = Vector((0.0, 0.0, 1.0))
            normals.append(tuple(n.normalized()))
        need(missing == 0, f"{self.word}: {missing} corners have no normal")
        mesh.normals_split_custom_set(normals)
        mesh.update()
        worst = 0.0
        for i, n in enumerate(mesh.corner_normals):
            worst = max(worst, Vector(n.vector).angle(Vector(normals[i]), 0.0))
        self.deviation = math.degrees(worst)
        self.obj[MARK] = VERSION

    def u(self, v):
        return unity(self.matrix @ v.co)

    def local(self, p):
        return self.inverse @ Vector((p[0], p[2], p[1]))

    def put(self, v, p):
        v.co = self.local(p)

    def ldir(self, d):
        return (self.turn @ Vector((d[0], d[2], d[1]))).normalized()

    def mat(self, name):
        need(name in self.slots, f"{self.word}: no slot {name}")
        return self.slots.index(name)

    def faces(self, name):
        i = self.mat(name)
        return [f for f in self.bm.faces if f.material_index == i]

    @staticmethod
    def verts(faces):
        return list({v for f in faces for v in f.verts})

    def box(self, faces):
        pts = [self.u(v) for v in self.verts(faces)]
        need(pts, f"{self.word}: empty face set")
        return (Vector([min(p[i] for p in pts) for i in range(3)]),
                Vector([max(p[i] for p in pts) for i in range(3)]))

    def centre(self, faces):
        lo, hi = self.box(faces)
        return (lo + hi) / 2

    def parts(self, faces):
        pool = set(faces)
        seen = set()
        out = []
        for f in faces:
            if f in seen:
                continue
            seen.add(f)
            stack = [f]
            part = []
            while stack:
                g = stack.pop()
                part.append(g)
                for v in g.verts:
                    for h in v.link_faces:
                        if h in pool and h not in seen:
                            seen.add(h)
                            stack.append(h)
            out.append(part)
        return out

    def expect(self, faces, points, tol=0.0015):
        have = [self.u(v) for v in self.verts(faces)]
        for p in points:
            need(any((h - Vector(p)).length <= tol for h in have), f"{self.word}: expected a vertex near {p}")

    def delete(self, faces):
        bmesh.ops.delete(self.bm, geom=list(set(faces)), context="FACES")

    def vert(self, p):
        return self.bm.verts.new(self.local(p))

    def face(self, verts, name):
        f = self.bm.faces.new(verts)
        f.material_index = self.mat(name)
        f.smooth = True
        return f

    def close(self, faces):
        bmesh.ops.recalc_face_normals(self.bm, faces=list(faces))

    def shade(self, faces, angle=40.0, flat=False):
        limit = math.cos(math.radians(angle))
        for f in faces:
            f.normal_update()
        around = {}
        for f in faces:
            area = max(f.calc_area(), 1e-12)
            for v in f.verts:
                around.setdefault(v.co.to_tuple(5), []).append((f.normal.copy(), area))
        for f in faces:
            for loop in f.loops:
                n = Vector(f.normal)
                if not flat:
                    total = Vector()
                    for m, a in around[loop.vert.co.to_tuple(5)]:
                        if m.dot(f.normal) >= limit:
                            total += m * a
                    if total.length > 1e-9:
                        n = total
                loop[self.nl] = n.normalized()

    def box_uv(self, faces):
        if not faces:
            return
        pts = [v.co for v in self.verts(faces)]
        lo = Vector([min(p[i] for p in pts) for i in range(3)])
        span = max(max(p[i] for p in pts) - lo[i] for i in range(3)) or 1.0
        for f in faces:
            f.normal_update()
            axis = max(range(3), key=lambda i: abs(f.normal[i]))
            a, b = [i for i in range(3) if i != axis]
            for loop in f.loops:
                co = loop.vert.co
                loop[self.uv].uv = ((co[a] - lo[a]) / span, (co[b] - lo[b]) / span)

    def grid(self, rows, name, closed_u=True, closed_v=False):
        verts = [[self.vert(p) for p in row] for row in rows]
        faces = []
        n = len(rows[0])
        for j in range(len(rows) - (0 if closed_v else 1)):
            a = verts[j]
            b = verts[(j + 1) % len(rows)]
            for i in range(n if closed_u else n - 1):
                k = (i + 1) % n
                faces.append(self.face([a[i], a[k], b[k], b[i]], name))
        return verts, faces

    def capped(self, rows, name, tip=None):
        verts, faces = self.grid(rows, name)
        faces.append(self.face(list(reversed(verts[0])), name))
        if tip is None:
            faces.append(self.face(verts[-1], name))
        else:
            apex = self.vert(tip)
            last = verts[-1]
            for i in range(len(last)):
                faces.append(self.face([last[i], last[(i + 1) % len(last)], apex], name))
        self.close(faces)
        return faces

    def lathe(self, cx, cz, profile, name, seg=32):
        rows = [[(cx + r * math.cos(TAU * i / seg), y, cz + r * math.sin(TAU * i / seg)) for i in range(seg)] for y, r in profile]
        return self.capped(rows, name)

    def sphere(self, c, radii, name, seg=16, rings=10):
        rows = []
        for j in range(1, rings):
            lat = -math.pi / 2 + math.pi * j / rings
            rows.append([(c[0] + radii[0] * math.cos(lat) * math.cos(TAU * i / seg), c[1] + radii[1] * math.sin(lat),
                          c[2] + radii[2] * math.cos(lat) * math.sin(TAU * i / seg)) for i in range(seg)])
        verts, faces = self.grid(rows, name)
        bottom = self.vert((c[0], c[1] - radii[1], c[2]))
        top = self.vert((c[0], c[1] + radii[1], c[2]))
        for i in range(seg):
            k = (i + 1) % seg
            faces.append(self.face([bottom, verts[0][k], verts[0][i]], name))
            faces.append(self.face([top, verts[-1][i], verts[-1][k]], name))
        self.close(faces)
        return faces

    def ring(self, c, big, small, name, seg=24, sides=8):
        rows = []
        for i in range(seg):
            a = TAU * i / seg
            rows.append([(c[0] + (big + small * math.cos(TAU * j / sides)) * math.cos(a), c[1] + small * math.sin(TAU * j / sides),
                          c[2] + (big + small * math.cos(TAU * j / sides)) * math.sin(a)) for j in range(sides)])
        verts, faces = self.grid(rows, name, closed_v=True)
        self.close(faces)
        return faces

    def sweep(self, path, radii, name, sides=8, tip=None):
        rows = []
        for k, p in enumerate(path):
            t = (Vector(path[min(k + 1, len(path) - 1)]) - Vector(path[max(k - 1, 0)])).normalized()
            side = t.cross(Vector((0.0, 1.0, 0.0)))
            if side.length < 1e-6:
                side = t.cross(Vector((1.0, 0.0, 0.0)))
            side.normalize()
            up = side.cross(t)
            rows.append([tuple(Vector(p) + (side * math.cos(TAU * j / sides) + up * math.sin(TAU * j / sides)) * radii[k]) for j in range(sides)])
        return self.capped(rows, name, tip)

    def band(self, path, ups, profile, name):
        rows = []
        for k, p in enumerate(path):
            t = (path[min(k + 1, len(path) - 1)] - path[max(k - 1, 0)]).normalized()
            n = (ups[k] - t * ups[k].dot(t)).normalized()
            side = t.cross(n).normalized()
            rows.append([tuple(p + side * q + n * h) for q, h in profile])
        return self.capped(rows, name)

    def arcbox(self, cx, cz, r0, r1, y0, y1, a0, a1, name, steps=12):
        rows = []
        for k in range(steps + 1):
            a = a0 + (a1 - a0) * k / steps
            c, s = math.cos(a), math.sin(a)
            rows.append([(cx + r * c, y, cz + r * s) for r, y in ((r0, y0), (r1, y0), (r1, y1), (r0, y1))])
        return self.capped(rows, name)

    def slab(self, poly, z0, z1, b0, b1, segs, name):
        steps = []
        if b0 > 0:
            for k in range(segs, -1, -1):
                phi = math.pi / 2 * k / segs
                steps.append((b0 * (1 - math.cos(phi)), z0 + b0 - b0 * math.sin(phi)))
        else:
            steps.append((0.0, z0))
        if b1 > 0:
            for k in range(segs + 1):
                phi = math.pi / 2 * k / segs
                steps.append((b1 * (1 - math.cos(phi)), z1 - b1 + b1 * math.sin(phi)))
        else:
            steps.append((0.0, z1))
        rows = [[(x, y, z) for x, y in inset(poly, [s] * len(poly))] for s, z in steps]
        return self.capped(rows, name)

    def octa(self, tip, low, b1, b2, f1, f2, name):
        t, n, p, q, r, s = (self.vert(x) for x in (tip, low, b1, b2, f1, f2))
        faces = [self.face(v, name) for v in ([t, r, p], [t, q, r], [t, s, q], [t, p, s],
                                              [n, p, r], [n, r, q], [n, q, s], [n, s, p])]
        self.close(faces)
        return faces

    def bisect(self, faces, point, normal, clear):
        geom = self.verts(faces) + list({e for f in faces for e in f.edges}) + list(set(faces))
        return bmesh.ops.bisect_plane(self.bm, geom=geom, dist=1e-6, plane_co=self.local(point),
                                      plane_no=self.ldir(normal), clear_outer=clear, clear_inner=False)

    def trim(self, faces, point, normal, name):
        bmesh.ops.remove_doubles(self.bm, verts=self.verts(faces), dist=1e-6)
        faces = [f for f in faces if f.is_valid]
        res = self.bisect(faces, point, normal, True)
        cut = [e for e in res["geom_cut"] if isinstance(e, bmesh.types.BMEdge) and e.is_valid and e.is_boundary]
        need(cut, f"{self.word}: the trim plane missed the part")
        caps = bmesh.ops.edgeloop_fill(self.bm, edges=cut, mat_nr=self.mat(name), use_smooth=True)["faces"]
        if not caps:
            caps = bmesh.ops.holes_fill(self.bm, edges=cut, sides=len(cut))["faces"]
            for f in caps:
                f.material_index = self.mat(name)
        need(caps, f"{self.word}: could not cap the trimmed part ({len(cut)} cut edges)")
        return caps


def wrap(m, name, surface, front, back, cuts=3):
    lo, hi = m.box(m.faces(name))
    bmesh.ops.remove_doubles(m.bm, verts=m.verts(m.faces(name)), dist=1e-6)
    edges = list({e for f in m.faces(name) for e in f.edges})
    bmesh.ops.subdivide_edges(m.bm, edges=edges, cuts=cuts, use_grid_fill=True)
    faces = m.faces(name)
    for v in m.verts(faces):
        p = m.u(v)
        t = min(max((p.z - lo.z) / max(hi.z - lo.z, 1e-9), 0.0), 1.0)
        m.put(v, surface(p, t, front, back))
    m.shade(faces, 35)
    return faces


def body_part(m, name):
    parts = [p for p in m.parts(m.faces(name)) if m.box(p)[0].y < 0.01]
    need(len(parts) == 1, f"{m.word}: expected one {name} body on the floor, found {len(parts)}")
    return parts[0]


def fix_apple(m):
    body = None
    lobes = []
    for part in m.parts(m.faces("red_Classic")):
        if m.box(part)[0].y < 0.01:
            body = part
        else:
            lobes += part
    need(body and lobes, "APPLE: body or intersecting lobe not found")
    m.delete(lobes)
    stem = m.centre(m.faces("wood_dark_Classic"))
    top = max(m.u(v).y for v in m.verts(body))
    for v in m.verts(body):
        p = m.u(v)
        r = math.hypot(p.x - stem.x, p.z - stem.z)
        if p.y > top - 0.06 and r < 0.05:
            p.y -= 0.02 * (1 - (r / 0.05) ** 2) ** 2
            m.put(v, p)
    m.shade(body, 80)


def fix_ball(m):
    lo, hi = m.box(body_part(m, "red_Classic"))
    c = (lo + hi) / 2
    need(abs((hi.x - lo.x) / 2 - 0.18) < 0.002, "BALL: sphere radius changed")
    wrap(m, "ivory_Classic", on_sphere(c, (hi.x - lo.x) / 2), 0.005, 0.003)


def fix_bomb(m):
    lo, hi = m.box(body_part(m, "dark_Classic"))
    c = (lo + hi) / 2
    need(abs((hi.x - lo.x) / 2 - 0.17) < 0.002, "BOMB: sphere radius changed")
    wrap(m, "ivory_Classic", on_sphere(c, (hi.x - lo.x) / 2), 0.0035, 0.004)


def fix_soda(m):
    lo, hi = m.box(body_part(m, "red_Classic"))
    c = (lo + hi) / 2
    radius = (hi.x - lo.x) / 2
    wrap(m, "ivory_Classic", on_cylinder(c.x, c.z, lambda y: radius), 0.0018, 0.0025)
    tab = m.faces("gold_Classic")
    tlo, thi = m.box(tab)
    need(tlo.y >= hi.y - 0.002, "SODA: the tab is not above the top")
    for v in m.verts(tab):
        p = m.u(v)
        p.y = hi.y - 0.0015 + (p.y - tlo.y) * 0.6
        m.put(v, p)
    m.shade(tab, 75)


def fix_mug(m):
    body = body_part(m, "red_Classic")
    lo, hi = m.box(body)
    c = (lo + hi) / 2
    need(abs((hi.x - lo.x) / 2 - 0.125) < 0.002, "MUG: body radius changed")
    wrap(m, "ivory_Classic", on_cylinder(c.x, c.z, lambda y: 0.12 + min(max(y - 0.035, 0.0), 0.215) * 0.005 / 0.215), 0.0018, 0.0025)
    handles = [p for p in m.parts(m.faces("red_Classic")) if m.box(p)[0].x < c.x - 0.2]
    need(len(handles) == 1, "MUG: handle not found")
    caps = m.trim(handles[0], (c.x - 0.113, 0.0, 0.0), (1.0, 0.0, 0.0), "red_Classic")
    handle = [p for p in m.parts(m.faces("red_Classic")) if m.box(p)[0].x < c.x - 0.2][0]
    m.close(handle)
    m.shade(handle, 60)
    m.box_uv(caps)


def fix_water(m):
    shell = m.faces("water_Classic")
    c = m.centre(shell)
    rings = {}
    for v in m.verts(shell):
        p = m.u(v)
        key = round(p.y, 4)
        rings[key] = max(rings.get(key, 0.0), math.hypot(p.x - c.x, p.z - c.z))
    profile = sorted(rings.items())
    need(len(profile) >= 5, "WATER: shell profile not found")
    bmesh.ops.remove_doubles(m.bm, verts=m.verts(shell), dist=1e-6)
    m.shade(m.faces("water_Classic"), 55)
    label = m.faces("blue_Classic")
    lab_lo, lab_hi = m.box(label)
    caps = [f for f in label if max(m.u(v).y for v in f.verts) - min(m.u(v).y for v in f.verts) < 1e-4]
    need(caps, "WATER: label caps not found")
    m.delete(caps)
    for part in m.parts(m.faces("ceramic_Classic")):
        rv = m.verts(part)
        pts = [m.u(v) for v in rv]
        yc = sum(p.y for p in pts) / len(pts)
        rads = [math.hypot(p.x - c.x, p.z - c.z) for p in pts]
        shift = interpolate(profile, yc) + 0.0005 - (max(rads) + min(rads)) / 2
        for v in rv:
            p = m.u(v)
            d = Vector((p.x - c.x, 0.0, p.z - c.z)).normalized()
            m.put(v, p + d * shift)
    core = m.lathe(c.x, c.z, [(y, interpolate(profile, y) - 0.0035) for y in (0.004, 0.015, 0.03, 0.12, 0.23, 0.275)], "blue_Classic")
    m.shade(core, 50)
    m.box_uv(core)
    label_radius = (lab_hi.x - lab_lo.x) / 2
    wrap(m, "white_Classic", on_cylinder(c.x, c.z, lambda y: label_radius), 0.0018, 0.0022)


def fix_pie(m):
    dome = m.faces("wood_light_Classic")
    lo, hi = m.box(dome)
    a = (hi.x - lo.x) / 2
    b = (hi.y - lo.y) / 2
    cy = (hi.y + lo.y) / 2
    cx, cz = (hi.x + lo.x) / 2, (hi.z + lo.z) / 2
    need(abs(a - 0.235) < 0.002 and abs(hi.y - 0.18) < 0.002, "PIE: dome changed")
    red = m.mat("red_Classic")
    for f in dome:
        for loop in f.loops:
            p = m.u(loop.vert)
            loop[m.uv].uv = ((p.x - lo.x) / (2 * a), (p.z - lo.z) / (2 * a))
        if sum(m.u(v).y for v in f.verts) / len(f.verts) > 0.142:
            f.material_index = red
    lattice = [f for f in m.faces("cream_Classic") if min(m.u(v).y for v in f.verts) > 0.16]
    need(len(lattice) == 2592, f"PIE: expected 2592 lattice faces, found {len(lattice)}")
    m.delete(lattice)

    def height(x, z):
        return cy + b * math.sqrt(max(1 - ((x - cx) ** 2 + (z - cz) ** 2) / (a * a), 0.0))

    def up(x, z):
        return Vector(((x - cx) / (a * a), (height(x, z) - cy) / (b * b), (z - cz) / (a * a))).normalized()

    width, thick, chamfer, lift, embed, reach, steps = 0.018, 0.008, 0.002, 0.0025, 0.004, 0.216, 25
    profile = [(-width / 2, chamfer), (-width / 2 + chamfer, 0.0), (width / 2 - chamfer, 0.0), (width / 2, chamfer),
               (width / 2, thick - chamfer), (width / 2 - chamfer, thick), (-width / 2 + chamfer, thick), (-width / 2, thick - chamfer)]
    strips = []
    for i, off in enumerate((-0.1, 0.0, 0.1)):
        for along_z in (True, False):
            length = math.sqrt(reach * reach - off * off)
            path, ups = [], []
            for k in range(steps):
                s = -length + 2 * length * k / (steps - 1)
                x, z = (cx + off, cz + s) if along_z else (cx + s, cz + off)
                wave = lift * (-1) ** i * math.cos(math.pi * s / 0.1) * (1 if along_z else -1)
                n = up(x, z)
                path.append(Vector((x, height(x, z), z)) + n * (lift - embed + wave))
                ups.append(n)
            strips += m.band(path, ups, profile, "cream_Classic")
    m.shade(strips, 50)
    m.box_uv(strips)


def fix_pan(m):
    body = body_part(m, "dark_Classic")
    lo, hi = m.box(body)
    c = (lo + hi) / 2
    wall = (hi.x - lo.x) / 2
    need(abs(wall - 0.23) < 0.002, "PAN: wall radius changed")
    handle = m.faces("wood_dark_Classic")
    need(m.box(handle)[1].x > c.x - wall + 0.02, "PAN: handle no longer reaches inside")
    caps = m.trim(handle, (c.x - wall + 0.001, 0.0, 0.0), (1.0, 0.0, 0.0), "wood_dark_Classic")
    handle = m.faces("wood_dark_Classic")
    m.close(handle)
    m.shade(handle, 60)
    m.box_uv(caps)
    joint = m.centre(caps)
    half = 0.054 / (wall + 0.0016)
    plate = m.arcbox(c.x, c.z, wall - 0.0004, wall + 0.0036, joint.y - 0.039, joint.y + 0.039, math.pi - half, math.pi + half, "metal_Classic")
    m.shade(plate, 40)
    m.box_uv(plate)
    for side in (1, -1):
        a = math.pi + side * 0.045 / (wall + 0.0036)
        rr = wall + 0.0011
        rivet = m.sphere((c.x + rr * math.cos(a), joint.y, c.z + rr * math.sin(a)), (0.0065, 0.0065, 0.0065), "metal_Classic", 12, 8)
        m.shade(rivet, 80)
        m.box_uv(rivet)


def fix_pot(m):
    body = body_part(m, "dark_Classic")
    lo, hi = m.box(body)
    c = (lo + hi) / 2
    wall = (hi.x - lo.x) / 2
    need(abs(wall - 0.23) < 0.002, "POT: wall radius changed")
    caps = []
    for side in (1, -1):
        handles = [p for p in m.parts(m.faces("metal_Classic")) if side * (m.centre(p).x - c.x) > 0.2 and m.box(p)[0].y < hi.y - 0.01]
        need(len(handles) == 1, f"POT: handle on side {side} not found")
        caps += m.trim(handles[0], (c.x + side * (wall - 0.011), 0.0, 0.0), (-side, 0.0, 0.0), "metal_Classic")
    for part in m.parts(m.faces("metal_Classic")):
        if abs(m.centre(part).x - c.x) > 0.2 and m.box(part)[0].y < hi.y - 0.01:
            m.close(part)
            m.shade(part, 60)
    m.box_uv(caps)


def fix_soap(m):
    lo, hi = m.box(m.faces("soap_Classic"))
    c = (lo + hi) / 2
    hx, hz = (hi.x - lo.x) / 2, (hi.z - lo.z) / 2
    need(abs(hx - 0.16) < 0.002 and abs(hz - 0.105) < 0.002, "SOAP: bar size changed")
    top = m.box(m.faces("soap_Classic") + m.faces("ivory_Classic"))[1].y
    m.delete(m.faces("soap_Classic") + m.faces("ivory_Classic"))
    rc, rb, rt, side_top, crown = 0.055, 0.018, 0.03, 0.112, 0.008
    rows = []
    for k in range(5):
        a = math.radians(-90 + 22.5 * k)
        s = rb * (1 - math.cos(a))
        rows.append([(c.x + x, rb + rb * math.sin(a), c.z + z) for x, z in rounded_rect(hx - s, hz - s, rc - s, 6)])
    for k in range(5):
        a = math.radians(22.5 * k)
        s = rt * (1 - math.cos(a))
        rows.append([(c.x + x, side_top - rt + rt * math.sin(a), c.z + z) for x, z in rounded_rect(hx - s, hz - s, rc - s, 6)])
    edge = rounded_rect(hx - rt, hz - rt, rc - rt, 6)
    for scale in (0.8, 0.6, 0.4, 0.2):
        rows.append([(c.x + x * scale, side_top + crown * (1 - scale * scale), c.z + z * scale) for x, z in edge])
    bar = m.capped(rows, "soap_Classic", (c.x, side_top + crown, c.z))
    m.shade(bar, 60)
    m.box_uv(bar)
    limit = top * (1 + TOLERANCE) - 0.0004
    bubbles = ((0.030, 0.124, -0.020, 0.020), (-0.045, 0.121, 0.030, 0.016), (0.085, 0.118, 0.035, 0.012),
               (-0.078, 0.117, -0.032, 0.010), (-0.005, 0.145, 0.040, 0.009), (0.065, 0.143, -0.050, 0.0075))
    for x, y, z, r in bubbles:
        need(y + r <= limit, "SOAP: a bubble would raise the bounds past 5%")
        bubble = m.sphere((c.x + x, y, c.z + z), (r, r, r), "ivory_Classic", 16, 10)
        m.shade(bubble, 80)
        m.box_uv(bubble)


def fix_foam(m):
    nozzles = [p for p in m.parts(m.faces("dark_Classic")) if m.box(p)[0].z > 0.06]
    need(len(nozzles) == 1, "FOAM: front nozzle not found")
    nlo, nhi = m.box(nozzles[0])
    tip = Vector(((nlo.x + nhi.x) / 2, (nlo.y + nhi.y) / 2, nhi.z))
    cx, cz = tip.x, tip.z + 0.008
    puffs = ((0.0, -0.002, 0.0, 0.028, 0.020, 0.0205), (-0.030, -0.008, 0.001, 0.019, 0.016, 0.0185),
             (0.029, -0.007, 0.0, 0.018, 0.015, 0.0185))
    for dx, dy, dz, rx, ry, rz in puffs:
        puff = m.sphere((cx + dx, tip.y + dy, cz + dz), (rx, ry, rz), "white_Classic", 16, 10)
        m.shade(puff, 80)
        m.box_uv(puff)
    turns, steps = 2.0, 64
    path = [(cx, tip.y + 0.008, cz)]
    radii = [0.007]
    for k in range(steps + 1):
        f = k / steps
        theta = -math.pi / 2 + TAU * turns * f
        rho = 0.012 * (1 - f) + 0.002 * f
        path.append((cx + rho * math.cos(theta), tip.y + 0.014 + 0.020 * f, cz + rho * math.sin(theta)))
        radii.append(0.007 * (1 - f) + 0.0035 * f)
    end = path[-1]
    swirl = m.sweep(path, radii, "white_Classic", 10, (end[0], end[1] + 0.0055, end[2]))
    m.shade(swirl, 70)
    m.box_uv(swirl)


BLADE_OUTLINE = [(0.095, 0.246), (0.09, 0.696), (0.0, 0.866), (-0.09, 0.696), (-0.095, 0.246)]
BLADE_FULLER = [(0.014, 0.286), (0.014, 0.696), (0.0, 0.786), (-0.014, 0.696), (-0.014, 0.286)]


def fix_blade(m):
    metal = m.faces("metal_Classic")
    ivory = m.faces("ivory_Classic")
    m.expect(metal, [(x, y, z) for x, y in BLADE_OUTLINE for z in (-0.0275, 0.0275)])
    m.expect(ivory, [(x, y, z) for x, y in BLADE_FULLER for z in (0.027, 0.035)])
    m.delete(metal + ivory)
    depth = (0.0045, 0.0085, 0.0275)
    steps = [inset(BLADE_OUTLINE, [s, s, s, s, 0.0]) for s in (0.0, 0.007, 0.032)]
    need(all(inside(steps[2], p) for p in BLADE_FULLER), "BLADE: fuller leaves the flat")
    front = [[m.vert((x, y, depth[k])) for x, y in steps[k]] for k in range(3)]
    back = [[m.vert((x, y, -depth[k])) for x, y in steps[k]] for k in range(3)]
    faces = []
    for rings in (front, back):
        for k in range(2):
            for i in range(4):
                faces.append(m.face([rings[k][i], rings[k][i + 1], rings[k + 1][i + 1], rings[k + 1][i]],
                                    "ivory_Classic" if k == 0 else "metal_Classic"))
        faces.append(m.face(rings[2], "metal_Classic"))
    for i in range(4):
        faces.append(m.face([front[0][i], front[0][i + 1], back[0][i + 1], back[0][i]], "ivory_Classic"))
    base = [front[0][4], front[1][4], front[2][4], front[2][0], front[1][0], front[0][0],
            back[0][0], back[1][0], back[2][0], back[2][4], back[1][4], back[0][4]]
    faces.append(m.face(base, "metal_Classic"))
    m.close(faces)
    m.shade(faces, flat=True)
    m.box_uv(faces)
    for sign in (1, -1):
        outer = [m.vert((x, y, sign * 0.0283)) for x, y in BLADE_FULLER]
        inner = [m.vert((x, y, sign * 0.0255)) for x, y in BLADE_FULLER]
        inlay = [m.face(outer, "ivory_Classic"), m.face(list(reversed(inner)), "ivory_Classic")]
        for i in range(5):
            k = (i + 1) % 5
            inlay.append(m.face([outer[i], inner[i], inner[k], outer[k]], "ivory_Classic"))
        m.close(inlay)
        m.shade(inlay, flat=True)
        m.box_uv(inlay)


def thicken_shaft(m, name, taper, top):
    shaft = m.faces(name)
    lo, hi = m.box(shaft)
    c = (lo + hi) / 2
    for y in taper:
        m.bisect(m.faces(name), (c.x, y, c.z), (0.0, 1.0, 0.0), False)
    shaft = m.faces(name)
    for v in m.verts(shaft):
        p = m.u(v)
        f = 1.5 if p.y <= taper[0] else 1.04 if p.y >= taper[1] else 1.5 + (1.04 - 1.5) * (p.y - taper[0]) / (taper[1] - taper[0])
        q = Vector((c.x + (p.x - c.x) * f, top if p.y > hi.y - 0.002 else p.y, c.z + (p.z - c.z) * f))
        m.put(v, q)
    m.shade(shaft, 50)
    return c


def fix_spear(m):
    head = m.faces("metal_Classic")
    m.expect(head, [(0.085, 0.905, 0.019), (-0.085, 0.905, -0.019), (0.0, 0.965, 0.019), (0.0, 1.235, 0.019)])
    m.delete(head)
    c = thicken_shaft(m, "wood_light_Classic", (0.86, 0.90), 1.0)
    tip = m.octa((c.x, 1.235, c.z), (c.x, 0.965, c.z), (c.x + 0.085, 0.905, c.z), (c.x - 0.085, 0.905, c.z),
                 (c.x, 1.0, c.z + 0.034), (c.x, 1.0, c.z - 0.034), "metal_Classic")
    m.shade(tip, flat=True)
    m.box_uv(tip)
    sleeve = m.lathe(c.x, c.z, [(0.405 + 0.12 * k / 16, 0.0392 if k % 2 == 0 else 0.0416) for k in range(17)], "red_Classic", 24)
    m.shade(sleeve, 80)
    m.box_uv(sleeve)


def fix_arrow(m):
    head = m.faces("metal_Classic")
    m.expect(head, [(0.055, 0.525, 0.0125), (-0.055, 0.525, -0.0125), (0.0, 0.545, 0.0125), (0.0, 0.665, 0.0125)])
    m.delete(head)
    c = thicken_shaft(m, "wood_light_Classic", (0.48, 0.52), 0.572)
    tip = m.octa((c.x, 0.665, c.z), (c.x, 0.545, c.z), (c.x + 0.055, 0.525, c.z), (c.x - 0.055, 0.525, c.z),
                 (c.x, 0.575, c.z + 0.022), (c.x, 0.575, c.z - 0.022), "metal_Classic")
    m.shade(tip, flat=True)
    m.box_uv(tip)
    for y in (0.195, 0.465):
        binding = m.ring((c.x, y, c.z), 0.020, 0.0035, "red_Classic", 24, 8)
        m.shade(binding, 80)
        m.box_uv(binding)


def fix_bow(m):
    string = m.faces("cream_Classic")
    groups = {}
    for v in m.verts(string):
        p = m.u(v)
        groups.setdefault(0 if p.y < 0.2 else 2 if p.y > 0.55 else 1, []).append(v)
    need(len(groups) == 3, "BOW: string rings not found")
    for vs in groups.values():
        pts = [m.u(v) for v in vs]
        cx = sum(p.x for p in pts) / len(pts)
        cz = sum(p.z for p in pts) / len(pts)
        for v in vs:
            p = m.u(v)
            m.put(v, Vector((cx + (p.x - cx) * 3, p.y, cz + (p.z - cz) * 3)))
    m.shade(string, 70)
    grip = m.faces("wood_dark_Classic")
    lo, hi = m.box(m.faces("wood_light_Classic"))
    for y in (lo.y + 0.055, hi.y - 0.055):
        m.bisect(m.faces("wood_light_Classic"), (0.0, y, 0.0), (0.0, 1.0, 0.0), False)
    limbs = m.faces("wood_light_Classic")
    m.shade(limbs, 60)
    cream, dark, light = m.mat("cream_Classic"), m.mat("wood_dark_Classic"), m.mat("wood_light_Classic")
    for f in limbs:
        y = sum(m.u(v).y for v in f.verts) / len(f.verts)
        f.material_index = cream if y < lo.y + 0.055 or y > hi.y - 0.055 else dark
    for f in grip:
        f.material_index = light


SHIELD_OUTLINE = [(0.0, 0.0), (0.22, 0.18), (0.23, 0.5), (0.0, 0.57), (-0.23, 0.5), (-0.22, 0.18)]
SHIELD_PANEL = [(0.0, 0.0348), (0.1936, 0.1932), (0.2024, 0.4748), (0.0, 0.5364), (-0.2024, 0.4748), (-0.1936, 0.1932)]


def fix_shield(m):
    gold = m.faces("gold_Classic")
    blue = m.faces("blue_Classic")
    m.expect(gold, [(x, y, z) for x, y in SHIELD_OUTLINE for z in (-0.036, 0.036)])
    m.expect(blue, [(x, y, z) for x, y in SHIELD_PANEL for z in (0.04, 0.054)])
    m.delete(gold + blue)
    for faces in (m.slab(SHIELD_OUTLINE, -0.036, 0.036, 0.010, 0.010, 4, "gold_Classic"),
                  m.slab(SHIELD_PANEL, 0.030, 0.054, 0.0, 0.004, 2, "blue_Classic"),
                  m.slab(SHIELD_PANEL, -0.050, -0.030, 0.004, 0.0, 2, "blue_Classic")):
        m.shade(faces, 30)
        m.box_uv(faces)


FIXES = {"APPLE": fix_apple, "BALL": fix_ball, "BOMB": fix_bomb, "SODA": fix_soda, "MUG": fix_mug, "WATER": fix_water,
         "PIE": fix_pie, "PAN": fix_pan, "POT": fix_pot, "SOAP": fix_soap, "FOAM": fix_foam, "BLADE": fix_blade,
         "SPEAR": fix_spear, "ARROW": fix_arrow, "BOW": fix_bow, "SHIELD": fix_shield}


def checks(word, before, after, size, authored):
    out = []

    def add(ok, label):
        out.append({"check": label, "ok": bool(ok)})

    base = len(before["slots"])
    add(after["objects"] == before["objects"], f"objects {after['objects']}")
    add(after["mesh"] == before["mesh"], f"mesh {after['mesh']}")
    add(after["slots"][:base] == before["slots"] and after["slots"][base:] == APPEND.get(word, []), f"slots {after['slots']}")
    add(after["used"] == list(range(len(after["slots"]))), f"every slot has faces {after['used']}")
    add(all(abs(a - b) < 1e-5 for ra, rb in zip(after["matrix"], before["matrix"]) for a, b in zip(ra, rb)), "object transform unchanged")
    add(before["grip"] and after["grip"] and (Vector(after["grip"]) - Vector(before["grip"])).length < 1e-5, f"grip {after['grip']}")
    add(abs(after["min"][1]) <= 0.0005, f"bottom {after['min'][1]:.5f}")
    for i, axis in enumerate("xyz"):
        s0 = before["max"][i] - before["min"][i]
        s1 = after["max"][i] - after["min"][i]
        lim = TOLERANCE * s0
        if i in THICKEN.get(word, ()):
            ok = s1 >= s0 - lim and after["min"][i] <= before["min"][i] + lim and after["max"][i] >= before["max"][i] - lim
        else:
            ok = abs(s1 - s0) <= lim and abs(after["min"][i] - before["min"][i]) <= lim and abs(after["max"][i] - before["max"][i]) <= lim
        add(ok, f"bounds {axis} {s0:.4f} -> {s1:.4f}")
        if size:
            add(abs(s1 - size[i]) <= CATALOGUE_MARGIN, f"catalogue size {axis} {size[i]} vs {s1:.4f}")
        if authored:
            add(abs(s1 - authored[i]) <= AUTHORED_MARGIN, f"authored size {axis} {authored[i]} vs {s1:.4f}")
    if word in LONG:
        add(after["max"][1] - after["min"][1] <= before["max"][1] - before["min"][1] + 0.0005, "length never grows")
    return out


def correct(word, source, dest, specs, size, authored):
    m = Model(word, source, specs)
    before = scene_facts(word)
    if before["marker"] is not None:
        return {"word": word, "status": "skipped", "reason": f"already marked {before['marker']}", "before": before}
    for name in APPEND.get(word, []):
        m.add_slot(name)
    m.begin()
    FIXES[word](m)
    m.end()
    after = scene_facts(word)
    result = {"word": word, "before": before, "after": after, "normal_deviation_deg": round(m.deviation, 4),
              "checks": checks(word, before, after, size, authored)}
    result["checks"].append({"check": f"normal deviation {m.deviation:.3f} deg", "ok": m.deviation < 1.0})
    if not all(c["ok"] for c in result["checks"]):
        result["status"] = "failed"
        return result
    staging = tempfile.mkdtemp(prefix="wb_d4_")
    staged = os.path.join(staging, word + ".fbx")
    export_fbx(staged)
    import_fbx(staged)
    again = scene_facts(word)
    same = (again["objects"] == after["objects"] and again["mesh"] == after["mesh"] and again["slots"] == after["slots"]
            and again["triangles"] == after["triangles"] and again["marker"] == VERSION
            and all(abs(a - b) < 1e-4 for a, b in zip(again["min"] + again["max"], after["min"] + after["max"]))
            and (Vector(again["grip"]) - Vector(before["grip"])).length < 1e-4
            and all(abs(a - b) < 1e-4 for ra, rb in zip(again["matrix"], before["matrix"]) for a, b in zip(ra, rb)))
    result["reimport"] = again
    result["checks"].append({"check": "written FBX re-imports with the same names, slots, bounds, grip, transform and marker", "ok": same})
    if not same:
        result["status"] = "failed"
        shutil.rmtree(staging, ignore_errors=True)
        return result
    os.makedirs(os.path.dirname(dest), exist_ok=True)
    shutil.move(staged, dest)
    shutil.rmtree(staging, ignore_errors=True)
    result["status"] = "corrected"
    result["output"] = dest.replace("\\", "/")
    return result


def inspect(word, source, specs):
    m = Model(word, source, specs)
    facts = scene_facts(word)
    m.bm = bmesh.new()
    m.bm.from_mesh(m.mesh)
    parts = []
    small = {}
    for i, name in enumerate(m.slots):
        for part in m.parts([f for f in m.bm.faces if f.material_index == i]):
            if len(part) <= 2:
                small[name] = small.get(name, 0) + 1
                continue
            lo, hi = m.box(part)
            parts.append({"material": name, "faces": len(part), "min": [round(x, 4) for x in lo], "max": [round(x, 4) for x in hi]})
    m.bm.free()
    facts.update({"word": word, "status": "inspected", "parts": parts, "small_parts": small,
                  "custom_props": {k: str(m.obj[k]) for k in m.obj.keys() if not k.startswith("_")}})
    return facts


def paint(specs):
    for mat in bpy.data.materials:
        spec = specs.get(mat.name)
        if not spec:
            continue
        mat.diffuse_color = spec["baseColor"]
        try:
            mat.use_nodes = True
        except AttributeError:
            pass
        if mat.node_tree is None:
            continue
        nodes = mat.node_tree.nodes
        nodes.clear()
        out = nodes.new("ShaderNodeOutputMaterial")
        bsdf = nodes.new("ShaderNodeBsdfPrincipled")
        mat.node_tree.links.new(bsdf.outputs["BSDF"], out.inputs["Surface"])
        bsdf.inputs["Base Color"].default_value = spec["baseColor"]
        bsdf.inputs["Metallic"].default_value = spec["metallic"]
        bsdf.inputs["Roughness"].default_value = spec["roughness"]
        bsdf.inputs["Alpha"].default_value = spec["baseColor"][3]


def shoot(path, specs, prefix, pixels):
    import_fbx(path)
    paint(specs)
    scene = bpy.context.scene
    meshes = [o for o in bpy.data.objects if o.type == "MESH"]
    pts = [o.matrix_world @ v.co for o in meshes for v in o.data.vertices]
    lo = Vector([min(p[i] for p in pts) for i in range(3)])
    hi = Vector([max(p[i] for p in pts) for i in range(3)])
    centre = (lo + hi) / 2
    corners = [Vector((x, y, z)) for x in (lo.x, hi.x) for y in (lo.y, hi.y) for z in (lo.z, hi.z)]
    world = bpy.data.worlds.new("Preview")
    scene.world = world
    world.color = (0.42, 0.45, 0.48)
    try:
        world.use_nodes = True
    except AttributeError:
        pass
    background = world.node_tree.nodes.get("Background") if world.node_tree else None
    if background:
        background.inputs[0].default_value = (0.42, 0.45, 0.48, 1.0)
        background.inputs[1].default_value = 0.9
    camera = bpy.data.objects.new("Preview_Camera", bpy.data.cameras.new("Preview_Camera"))
    camera.data.type = "ORTHO"
    scene.collection.objects.link(camera)
    scene.camera = camera
    sun = bpy.data.objects.new("Preview_Sun", bpy.data.lights.new("Preview_Sun", "SUN"))
    sun.data.energy = 3.0
    scene.collection.objects.link(sun)
    scene.render.engine = "CYCLES"
    scene.cycles.device = "CPU"
    scene.cycles.samples = 12
    scene.cycles.use_denoising = False
    scene.render.resolution_x = pixels
    scene.render.resolution_y = pixels
    scene.render.resolution_percentage = 100
    scene.render.film_transparent = False
    scene.view_settings.view_transform = "Standard"
    scene.render.image_settings.file_format = "PNG"
    files = []
    for name, d_u in VIEWS:
        d = Vector((d_u[0], d_u[2], d_u[1])).normalized()
        camera.location = centre + d * 5.0
        camera.rotation_euler = (-d).to_track_quat("-Z", "Y").to_euler()
        inv = camera.rotation_euler.to_matrix().transposed()
        proj = [inv @ (p - centre) for p in corners]
        camera.data.ortho_scale = max(max(p.x for p in proj) - min(p.x for p in proj), max(p.y for p in proj) - min(p.y for p in proj)) * 1.15
        light = (d + Vector((0.3, 0.0, 1.2))).normalized()
        sun.rotation_euler = (-light).to_track_quat("-Z", "Y").to_euler()
        scene.render.filepath = f"{prefix}_{name}.png"
        bpy.ops.render.render(write_still=True)
        files.append(scene.render.filepath)
    return files


def compose(rows, path, pixels):
    import numpy as np
    sheet = np.zeros((len(rows) * pixels, len(rows[0]) * pixels, 4), dtype=np.float32)
    for r, files in enumerate(rows):
        for c, f in enumerate(files):
            img = bpy.data.images.load(f)
            px = np.array(img.pixels[:], dtype=np.float32).reshape(img.size[1], img.size[0], 4)
            y0 = (len(rows) - 1 - r) * pixels
            sheet[y0:y0 + pixels, c * pixels:(c + 1) * pixels] = px[:pixels, :pixels]
            bpy.data.images.remove(img)
    out = bpy.data.images.new("Preview_Sheet", sheet.shape[1], sheet.shape[0], alpha=True)
    out.pixels.foreach_set(sheet.ravel())
    out.filepath_raw = path
    out.file_format = "PNG"
    out.save()


def catalogue_sizes(repo):
    sizes = {}
    for rel in CATALOGUES:
        with open(os.path.join(repo, rel), encoding="utf-8") as f:
            data = json.load(f)
        for item in data["items"] if isinstance(data, dict) else data:
            if item.get("size"):
                sizes[item["id"]] = item["size"]
    return sizes


def update_report(path, corrected):
    with open(path, encoding="utf-8") as f:
        report = json.load(f)
    for entry in report["files"]:
        result = corrected.get(entry["name"]) if entry["kind"] == "item" else None
        if not result:
            continue
        facts = result["reimport"]
        entry["triangles"] = facts["triangles"]
        entry["materials"] = sorted(set(facts["slots"]))
        entry["bounds_min"] = [round(x, 5) + 0.0 for x in facts["min"]]
        entry["bounds_max"] = [round(x, 5) + 0.0 for x in facts["max"]]
        entry["bytes"] = os.path.getsize(result["output"])
        entry["corrected_by"] = SCRIPT
        entry["correction"] = VERSION
    with open(path, "w", encoding="utf-8") as f:
        json.dump(report, f, indent=1)


def main():
    a = parse_args()
    repo = os.path.abspath(a.repo)
    chosen = {w.strip().upper() for w in a.only.split(",") if w.strip()}
    words = [w for w in WORDS if not chosen or w in chosen]
    with open(os.path.join(repo, MATERIALS), encoding="utf-8") as f:
        specs = {s["name"]: s for s in json.load(f)["materials"]}
    with open(os.path.join(repo, REPORT), encoding="utf-8") as f:
        authored = {e["name"]: e.get("authored_size_xyz") for e in json.load(f)["files"] if e["kind"] == "item"}
    sizes = catalogue_sizes(repo)
    originals = tempfile.mkdtemp(prefix="wb_d4_before_") if a.preview and not a.out and not a.inspect else ""
    if originals:
        for word in words:
            shutil.copy2(os.path.join(repo, ITEMS, word + ".fbx"), os.path.join(originals, word + ".fbx"))
    results = []
    for word in words:
        source = os.path.join(repo, ITEMS, word + ".fbx")
        dest = os.path.join(os.path.abspath(a.out), word + ".fbx") if a.out else source
        try:
            if a.inspect:
                results.append(inspect(word, source, specs))
            else:
                results.append(correct(word, source, dest, specs, sizes.get(word), authored.get(word)))
        except Exception as exc:
            results.append({"word": word, "status": "error", "error": str(exc), "trace": traceback.format_exc()})
        last = results[-1]
        detail = last.get("error") or "; ".join(c["check"] for c in last.get("checks", []) if not c["ok"])
        print("HAND_ITEM " + word + " " + last["status"] + (" " + detail if detail else ""), flush=True)
    corrected = {r["word"]: r for r in results if r["status"] == "corrected"}
    if not a.inspect and not a.out and corrected:
        update_report(os.path.join(repo, REPORT), corrected)
    sheets = []
    if a.preview and not a.inspect:
        os.makedirs(a.preview, exist_ok=True)
        for word, result in corrected.items():
            try:
                pixels = 320
                rows = [shoot(os.path.join(originals or os.path.join(repo, ITEMS), word + ".fbx"), specs, os.path.join(a.preview, word + "_before"), pixels),
                        shoot(result["output"], specs, os.path.join(a.preview, word + "_after"), pixels)]
                sheet = os.path.join(os.path.abspath(a.preview), word + ".png")
                compose(rows, sheet, pixels)
                sheets.append(sheet.replace("\\", "/"))
            except Exception as exc:
                result["preview_error"] = traceback.format_exc()
                print("HAND_ITEM_PREVIEW_FAILED " + word + " " + str(exc), flush=True)
    summary = {"script": SCRIPT, "version": VERSION, "blender": bpy.app.version_string, "mode": "inspect" if a.inspect else "dry-run" if a.out else "in-place",
               "results": results, "sheets": sheets}
    if a.report:
        os.makedirs(os.path.dirname(os.path.abspath(a.report)), exist_ok=True)
        with open(a.report, "w", encoding="utf-8") as f:
            json.dump(summary, f, indent=1, default=str)
    counts = {}
    for r in results:
        counts[r["status"]] = counts.get(r["status"], 0) + 1
    print("HAND_ITEMS_RESULT " + json.dumps({"mode": summary["mode"], "counts": counts, "sheets": len(sheets),
                                             "failed": [r["word"] for r in results if r["status"] in ("failed", "error")]}), flush=True)
    if any(r["status"] in ("failed", "error") for r in results):
        sys.exit(1)


try:
    main()
except SystemExit:
    raise
except Exception as error:
    traceback.print_exc()
    print("HAND_ITEMS_FAILED " + str(error), flush=True)
    sys.exit(1)
