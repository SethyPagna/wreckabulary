import hashlib
import json
import math
import random
import re
from pathlib import Path

import bmesh
import bpy
from mathutils import Matrix, Vector


REPO = Path(__file__).resolve().parents[2]
KIT_DIR = REPO / "Assets/_Project/Art/Surroundings/Kit"
DATA_DIR = REPO / "Assets/_Project/Data/Surroundings"
SHEET_DIR = REPO / "BuildNotes/surroundings/kit"

SLOTS = {
    "bk_plaster": "E9DCC6",
    "bk_brick": "B5735A",
    "bk_stone": "CFC6B8",
    "bk_roof": "A9573F",
    "bk_wood": "B88A5E",
    "bk_paint": "FFFFFF",
    "bk_metal": "4C4F56",
    "bk_glass_dark": "4F6A7A",
    "bk_glass_lit": "FFD9A0",
    "bk_leaf": "7FA06A",
    "bk_bark": "7A5A44",
    "bk_grass": "8DAA78",
    "bk_road": "6E6E72",
    "bk_line": "ECE8DF",
    "bk_card": "FFFFFF",
}
TINTABLE = ("bk_plaster", "bk_roof", "bk_paint")
RGB_FREE = ("bk_card",)
SWAY_SLOTS = ("bk_leaf",)
FRONT_SLOTS = ("bk_paint", "bk_glass_dark", "bk_glass_lit")
MAX_SLOTS = 5
SHADE_MIN = 0.75
SHADE_MAX = 1.0
FLOOR_LIMIT = -0.05
SIZE_TOLERANCE = 0.10
NAME_RULE = re.compile(r"^[a-z][a-z0-9_]*$")
ROUGHNESS = {
    "bk_glass_dark": 0.16,
    "bk_glass_lit": 0.3,
    "bk_metal": 0.42,
    "bk_paint": 0.5,
    "bk_road": 0.92,
    "bk_grass": 0.95,
    "bk_leaf": 0.85,
    "bk_card": 0.9,
}
UP = Vector((0.0, 1.0, 0.0))
VIEW_34 = Vector((0.62, -1.0, 0.62)).normalized()
VIEW_COUCH = Vector((0.0, -math.cos(math.radians(54.0)), math.sin(math.radians(54.0))))
VIEW_FRONT = Vector((0.0, -1.0, 0.16)).normalized()
GUTTER = 6
UNITY_FRONT = Matrix(((-1.0, 0.0, 0.0, 0.0), (0.0, -1.0, 0.0, 0.0), (0.0, 0.0, 1.0, 0.0), (0.0, 0.0, 0.0, 1.0)))


def clamp(x, lo=0.0, hi=1.0):
    return lo if x < lo else hi if x > hi else x


def lerp(a, b, t):
    return a + (b - a) * t


def smoothstep(a, b, x):
    if b <= a:
        return 1.0
    t = clamp((x - a) / (b - a))
    return t * t * (3.0 - 2.0 * t)


def linear_channel(v):
    return v / 12.92 if v <= 0.04045 else ((v + 0.055) / 1.055) ** 2.4


def srgb_channel(v):
    return v * 12.92 if v <= 0.0031308 else 1.055 * v ** (1 / 2.4) - 0.055


def hex_srgb(value):
    return tuple(int(value[i:i + 2], 16) / 255 for i in (0, 2, 4))


def hex_linear(value):
    return tuple(linear_channel(c) for c in hex_srgb(value))


def to_blender(p):
    return Vector((-p[0], -p[2], p[1]))


def to_unity(v):
    return Vector((-v[0], v[2], -v[1]))


def sha256(path):
    return hashlib.sha256(Path(path).read_bytes()).hexdigest()


def repo_path(path):
    path = Path(path).resolve()
    try:
        return path.relative_to(REPO).as_posix()
    except ValueError:
        return path.as_posix()


def _freeze(value):
    if isinstance(value, (list, tuple)):
        return tuple(_freeze(v) for v in value)
    if isinstance(value, float):
        return round(value, 6)
    return value


def _gradient(value, y):
    if isinstance(value, (int, float)):
        return float(value)
    a, b, y0, y1 = value
    t = clamp((y - y0) / (y1 - y0)) if y1 != y0 else 1.0
    if isinstance(a, (int, float)):
        return lerp(a, b, t)
    return tuple(lerp(a[i], b[i], t) for i in range(3))


def _key(normal):
    axis = max(range(3), key=lambda i: abs(normal[i]))
    return ("+" if normal[axis] >= 0 else "-") + "xyz"[axis]


def _edge_axis(edge):
    d = edge.verts[1].co - edge.verts[0].co
    return "xyz"[max(range(3), key=lambda i: abs(d[i]))]


def _unique(items):
    return list(dict.fromkeys(items))


def clean_scene():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    scene = bpy.context.scene
    scene.unit_settings.system = "METRIC"
    scene.unit_settings.scale_length = 1.0
    return scene


def _node_tree(owner):
    if getattr(owner, "node_tree", None) is None:
        try:
            owner.use_nodes = True
        except (AttributeError, TypeError):
            pass
    return owner.node_tree


def _principled(mat):
    tree = _node_tree(mat)
    node = next((n for n in tree.nodes if n.type == "BSDF_PRINCIPLED"), None)
    if node is None:
        node = tree.nodes.new("ShaderNodeBsdfPrincipled")
        out = next((n for n in tree.nodes if n.type == "OUTPUT_MATERIAL"), None) or tree.nodes.new("ShaderNodeOutputMaterial")
        tree.links.new(node.outputs["BSDF"], out.inputs["Surface"])
    return node


def material(slot):
    if slot not in SLOTS:
        raise ValueError(f"unknown slot {slot}")
    mat = bpy.data.materials.get(slot)
    if mat is not None:
        return mat
    mat = bpy.data.materials.new(slot)
    colour = hex_linear(SLOTS[slot])
    node = _principled(mat)
    node.inputs["Base Color"].default_value = (*colour, 1.0)
    node.inputs["Roughness"].default_value = ROUGHNESS.get(slot, 0.72)
    node.inputs["Metallic"].default_value = 0.0
    if slot == "bk_glass_lit":
        node.inputs["Emission Color"].default_value = (*colour, 1.0)
        node.inputs["Emission Strength"].default_value = 2.0
    mat.diffuse_color = (*colour, 1.0)
    return mat


def slot_materials(names=None):
    return {name: material(name) for name in (names or SLOTS)}


def _write_colours(attr, flat):
    try:
        attr.data.foreach_set("color_srgb", flat)
    except (AttributeError, TypeError, RuntimeError):
        lin = []
        for i in range(0, len(flat), 4):
            lin.extend((linear_channel(flat[i]), linear_channel(flat[i + 1]), linear_channel(flat[i + 2]), flat[i + 3]))
        attr.data.foreach_set("color", lin)


def read_colours(mesh, name="Col"):
    attr = mesh.color_attributes.get(name)
    if attr is None:
        return None
    flat = [0.0] * (len(attr.data) * 4)
    try:
        attr.data.foreach_get("color_srgb", flat)
    except (AttributeError, TypeError, RuntimeError):
        attr.data.foreach_get("color", flat)
        for i in range(0, len(flat), 4):
            flat[i], flat[i + 1], flat[i + 2] = (srgb_channel(flat[i]), srgb_channel(flat[i + 1]), srgb_channel(flat[i + 2]))
    return flat


def _box_uv(mesh):
    uv = mesh.uv_layers.new(name="UVMap")
    co = [0.0] * (len(mesh.vertices) * 3)
    mesh.vertices.foreach_get("co", co)
    out = [0.0] * (len(mesh.loops) * 2)
    for poly in mesh.polygons:
        axis = max(range(3), key=lambda i: abs(poly.normal[i]))
        for li in poly.loop_indices:
            vi = mesh.loops[li].vertex_index
            x, y, z = co[3 * vi:3 * vi + 3]
            out[2 * li:2 * li + 2] = (x, y) if axis == 2 else (y, z) if axis == 0 else (x, z)
    uv.data.foreach_set("uv", out)


def _dress(mesh, poly_slots, colours, collection=None):
    used = []
    for slot in poly_slots:
        if slot not in SLOTS:
            raise ValueError(f"{mesh.name}: unknown slot {slot}")
        if slot not in used:
            used.append(slot)
    for slot in used:
        mesh.materials.append(material(slot))
    mesh.polygons.foreach_set("material_index", [used.index(s) for s in poly_slots])
    attr = mesh.color_attributes.new(name="Col", type="BYTE_COLOR", domain="CORNER")
    _write_colours(attr, colours)
    try:
        mesh.color_attributes.active_color = attr
        mesh.color_attributes.render_color_index = mesh.color_attributes.active_color_index
    except (AttributeError, TypeError):
        pass
    _box_uv(mesh)
    mesh.update()
    obj = bpy.data.objects.new(mesh.name, mesh)
    (collection or bpy.context.scene.collection).objects.link(obj)
    return obj


def mesh_object(name, verts, faces, slots, colours, smooth=None, collection=None):
    if not NAME_RULE.match(name):
        raise ValueError(f"bad piece name {name}")
    mesh = bpy.data.meshes.new(name)
    mesh.from_pydata([tuple(v) for v in verts], [], [tuple(f) for f in faces])
    mesh.update()
    if smooth is not None:
        mesh.polygons.foreach_set("use_smooth", [bool(s) for s in smooth])
    flat = []
    for c in colours:
        flat.extend(c)
    if len(flat) != len(mesh.loops) * 4:
        raise ValueError(f"{name}: {len(flat) // 4} colours for {len(mesh.loops)} corners")
    return _dress(mesh, list(slots), flat, collection)


class Roof:
    def __init__(self, kind, axis, cx, cz, eave_y, ridge_y, tan, vertical, half, overhang, length):
        self.kind, self.axis, self.cx, self.cz = kind, axis, cx, cz
        self.eave_y, self.ridge_y, self.tan, self.vertical = eave_y, ridge_y, tan, vertical
        self.half, self.overhang, self.length = half, overhang, length

    def top(self, s):
        return self.ridge_y - abs(s) * self.tan


class Piece:
    def __init__(self, name, family="", base_shade=0.8, base_rise=1.6):
        if not NAME_RULE.match(name):
            raise ValueError(f"bad piece name {name}")
        self.name, self.family = name, family
        self.base_shade, self.base_rise = base_shade, base_rise
        self.bm = bmesh.new()
        self.layer = self.bm.faces.layers.int.new("meta")
        self.metas = []
        self._index = {}
        self.offset = Vector((0.0, 0.0, 0.0))
        self.obj = None

    def meta(self, slot, tone=1.0, rgb=None, sway=0.0):
        if slot not in SLOTS:
            raise ValueError(f"{self.name}: unknown slot {slot}")
        key = (slot, _freeze(tone), _freeze(rgb), _freeze(sway))
        if key not in self._index:
            self._index[key] = len(self.metas)
            self.metas.append({"slot": slot, "tone": tone, "rgb": rgb, "sway": sway})
        return self._index[key]

    def _commit(self, tmp, meta_of):
        tmp.normal_update()
        vmap = {v: self.bm.verts.new(to_blender(v.co)) for v in tmp.verts}
        for f in tmp.faces:
            mid = meta_of if isinstance(meta_of, int) else meta_of(f)
            nf = self.bm.faces.new([vmap[v] for v in reversed(list(f.verts))])
            nf[self.layer] = mid
            nf.smooth = f.smooth
        tmp.free()

    @staticmethod
    def _place(tmp, centre, yaw):
        c, s = math.cos(math.radians(yaw)), math.sin(math.radians(yaw))
        for v in tmp.verts:
            x, y, z = v.co
            v.co = Vector((centre[0] + x * c + z * s, centre[1] + y, centre[2] - x * s + z * c))

    @staticmethod
    def _face(tmp, verts, want=None):
        f = tmp.faces.new(verts)
        if want is not None:
            f.normal_update()
            if f.normal.dot(Vector(want)) < 0:
                f.normal_flip()
        return f

    def box(self, slot, centre, size, bevel=0.0, bevel_axes="xyz", yaw=0.0, tone=1.0, drop=(), faces=None,
            boards=0.0, board_tones=(1.0, 0.9), sway=0.0):
        tmp = bmesh.new()
        bmesh.ops.create_cube(tmp, size=1.0)
        sx, sy, sz = size
        for v in tmp.verts:
            v.co = Vector((v.co.x * sx, v.co.y * sy, v.co.z * sz))
        if boards > 0:
            _bisect_grid(tmp, sx, sz, boards)
        elif bevel > 0:
            edges = [e for e in tmp.edges if _edge_axis(e) in bevel_axes]
            if edges:
                bmesh.ops.bevel(tmp, geom=edges, offset=min(bevel, 0.45 * min(size)), offset_type="OFFSET",
                                segments=1, profile=0.5, affect="EDGES", clamp_overlap=True)
        tmp.normal_update()
        if drop:
            gone = [f for f in tmp.faces if _key(f.normal) in drop]
            if gone:
                bmesh.ops.delete(tmp, geom=gone, context="FACES")
        ids = {}
        for f in tmp.faces:
            k = _key(f.normal)
            s, t = slot, tone
            if faces and k in faces:
                s, t = faces[k]
            if boards > 0 and k[1] in "xz":
                c = f.calc_center_median()
                along, extent = (c.z, sz) if k[1] == "x" else (c.x, sx)
                cell = int(math.floor((along + extent / 2) / boards + 1e-6))
                t = t * board_tones[cell % len(board_tones)]
            ids[f] = self.meta(s, t, sway=sway)
        self._place(tmp, centre, yaw)
        self._commit(tmp, ids.__getitem__)

    def prism(self, slot, profile, axis="x", span=(-0.5, 0.5), origin=(0.0, 0.0, 0.0), tone=1.0,
              edge_metas=None, cap_meta=None, sway=0.0, cap_fan=None):
        tmp = bmesh.new()
        ox, oy, oz = origin

        def place(a, b, t):
            if axis == "x":
                return Vector((ox + t, oy + b, oz + a))
            if axis == "z":
                return Vector((ox + a, oy + b, oz + t))
            return Vector((ox + a, oy + t, oz + b))

        v0 = [tmp.verts.new(place(a, b, span[0])) for a, b in profile]
        v1 = [tmp.verts.new(place(a, b, span[1])) for a, b in profile]
        base = self.meta(slot, tone, sway=sway)
        ids = {}
        n = len(profile)
        for i in range(n):
            j = (i + 1) % n
            f = tmp.faces.new((v0[i], v0[j], v1[j], v1[i]))
            ids[f] = edge_metas[i] if edge_metas else base
        cap = base if cap_meta is None else cap_meta
        for ring in (v0, v1):
            if cap_fan is None:
                ids[tmp.faces.new(ring)] = cap
                continue
            for k in range(1, n - 1):
                i, j = (cap_fan + k) % n, (cap_fan + k + 1) % n
                ids[tmp.faces.new((ring[cap_fan], ring[i], ring[j]))] = cap
        bmesh.ops.recalc_face_normals(tmp, faces=tmp.faces[:])
        self._commit(tmp, ids.__getitem__)

    def hull(self, slot, points, tone=1.0, meta_fn=None, sway=0.0):
        tmp = bmesh.new()
        verts = [tmp.verts.new(Vector(p)) for p in points]
        result = bmesh.ops.convex_hull(tmp, input=verts, use_existing_faces=False)
        junk = _unique(g for g in result["geom_interior"] + result["geom_unused"] if isinstance(g, bmesh.types.BMVert))
        if junk:
            bmesh.ops.delete(tmp, geom=junk, context="VERTS")
        bmesh.ops.recalc_face_normals(tmp, faces=tmp.faces[:])
        tmp.normal_update()
        base = self.meta(slot, tone, sway=sway)
        ids = {f: (meta_fn(f.normal.copy(), f.calc_center_median()) if meta_fn else base) for f in tmp.faces}
        self._commit(tmp, ids.__getitem__)

    def lathe(self, slot, profile, centre=(0.0, 0.0, 0.0), segments=16, cap_bottom=True, cap_top=True, smooth=True,
              closed=False, tone=1.0, band_metas=None, cap_meta=None, phase=0.0, sway=0.0):
        tmp = bmesh.new()
        cx, cy, cz = centre
        angles = [2 * math.pi * (k + phase) / segments for k in range(segments)]
        rings = []
        for r, y in profile:
            if r <= 1e-6 and not closed:
                rings.append([tmp.verts.new((cx, cy + y, cz))])
            else:
                rings.append([tmp.verts.new((cx + r * math.cos(a), cy + y, cz + r * math.sin(a))) for a in angles])
        base = self.meta(slot, tone, sway=sway)
        ids = {}
        count = len(rings) if closed else len(rings) - 1
        for i in range(count):
            a, b = rings[i], rings[(i + 1) % len(rings)]
            mid = band_metas[i] if band_metas else base
            for k in range(segments):
                k2 = (k + 1) % segments
                if len(a) == 1 and len(b) == 1:
                    continue
                if len(a) == 1:
                    verts = (a[0], b[k2], b[k])
                elif len(b) == 1:
                    verts = (a[k], a[k2], b[0])
                else:
                    verts = (a[k], a[k2], b[k2], b[k])
                f = tmp.faces.new(verts)
                f.smooth = smooth
                ids[f] = mid
        if not closed:
            for ring, wanted in ((rings[0], cap_bottom), (rings[-1], cap_top)):
                if wanted and len(ring) > 2:
                    f = tmp.faces.new(ring)
                    ids[f] = base if cap_meta is None else cap_meta
        bmesh.ops.recalc_face_normals(tmp, faces=tmp.faces[:])
        self._commit(tmp, ids.__getitem__)

    def cylinder(self, slot, base, radius, height, segments=12, radius_top=None, cap_bottom=True, cap_top=True,
                 smooth=True, tone=1.0, phase=0.0, sway=0.0):
        top = radius if radius_top is None else radius_top
        self.lathe(slot, [(radius, 0.0), (top, height)], base, segments, cap_bottom, cap_top, smooth,
                   tone=tone, phase=phase, sway=sway)

    def strut(self, slot, a, b, thickness, bevel=0.0, tone=1.0, roll=(0.0, 0.0, 1.0), drop_ends=False, sway=0.0):
        a, b = Vector(a), Vector(b)
        d = b - a
        length = d.length
        y = d.normalized()
        ref = Vector(roll).normalized()
        if abs(y.dot(ref)) > 0.95:
            ref = Vector((1.0, 0.0, 0.0)) if abs(y.x) < 0.95 else Vector((0.0, 0.0, 1.0))
        x = y.cross(ref).normalized()
        z = x.cross(y)
        tx, tz = (thickness, thickness) if isinstance(thickness, (int, float)) else thickness
        tmp = bmesh.new()
        bmesh.ops.create_cube(tmp, size=1.0)
        for v in tmp.verts:
            v.co = Vector((v.co.x * tx, v.co.y * length, v.co.z * tz))
        if bevel > 0:
            edges = [e for e in tmp.edges if _edge_axis(e) == "y"]
            bmesh.ops.bevel(tmp, geom=edges, offset=min(bevel, 0.45 * min(tx, tz)), offset_type="OFFSET",
                            segments=1, profile=0.5, affect="EDGES", clamp_overlap=True)
        tmp.normal_update()
        if drop_ends:
            gone = [f for f in tmp.faces if abs(f.normal.y) > 0.9]
            bmesh.ops.delete(tmp, geom=gone, context="FACES")
        mid = a + d / 2
        for v in tmp.verts:
            p = v.co.copy()
            v.co = mid + x * p.x + y * p.y + z * p.z
        self._commit(tmp, self.meta(slot, tone, sway=sway))

    def ico(self, slot, centre, radius, subdivisions=1, jitter=0.0, seed=0, tone=1.0, smooth=True, sway=0.0):
        tmp = bmesh.new()
        bmesh.ops.create_icosphere(tmp, subdivisions=subdivisions, radius=1.0)
        rx, ry, rz = (radius, radius, radius) if isinstance(radius, (int, float)) else radius
        rng = random.Random(seed)
        for v in tmp.verts:
            k = 1.0 + (rng.uniform(-jitter, jitter) if jitter else 0.0)
            v.co = Vector((centre[0] + v.co.x * rx * k, centre[1] + v.co.y * ry * k, centre[2] + v.co.z * rz * k))
        for f in tmp.faces:
            f.smooth = smooth
        bmesh.ops.recalc_face_normals(tmp, faces=tmp.faces[:])
        self._commit(tmp, self.meta(slot, tone, sway=sway))

    def polygon(self, slot, points, facing, tone=1.0, rgb=None, sway=0.0):
        tmp = bmesh.new()
        self._face(tmp, [tmp.verts.new(Vector(p)) for p in points], facing)
        self._commit(tmp, self.meta(slot, tone, rgb, sway))

    def custom(self, slot, verts, faces, closed=True, tone=1.0, face_metas=None, wants=None, smooth=False, sway=0.0):
        tmp = bmesh.new()
        vs = [tmp.verts.new(Vector(p)) for p in verts]
        base = self.meta(slot, tone, sway=sway)
        ids = {}
        for i, face in enumerate(faces):
            f = self._face(tmp, [vs[k] for k in face], wants[i] if wants else None)
            f.smooth = smooth
            ids[f] = face_metas[i] if face_metas else base
        if closed:
            bmesh.ops.recalc_face_normals(tmp, faces=tmp.faces[:])
        self._commit(tmp, ids.__getitem__)

    @staticmethod
    def wall(centre, normal):
        n = Vector((normal[0], 0.0, normal[2])).normalized()
        u = UP.cross(n)
        c = Vector(centre)
        yaw = math.degrees(math.atan2(n.x, n.z))
        return (lambda a, b, d: c + u * a + UP * b + n * d), n, u, yaw

    def frame_ring(self, slot, centre, normal, width, height, border=0.1, depth=0.09, tone=1.0, reveal_tone=0.86):
        P, n, u, _ = self.wall(centre, normal)
        hw, hh = width / 2, height / 2
        iw, ih = hw - border, hh - border
        corners = ((-1, -1), (1, -1), (1, 1), (-1, 1))
        tmp = bmesh.new()
        o0 = [tmp.verts.new(P(sa * hw, sb * hh, 0.0)) for sa, sb in corners]
        o1 = [tmp.verts.new(P(sa * hw, sb * hh, depth)) for sa, sb in corners]
        i0 = [tmp.verts.new(P(sa * iw, sb * ih, 0.0)) for sa, sb in corners]
        i1 = [tmp.verts.new(P(sa * iw, sb * ih, depth)) for sa, sb in corners]
        front, reveal = self.meta(slot, tone), self.meta(slot, tone * reveal_tone)
        ids = {}
        for i in range(4):
            j = (i + 1) % 4
            ma = (corners[i][0] + corners[j][0]) / 2
            mb = (corners[i][1] + corners[j][1]) / 2
            out = u * ma + UP * mb
            ids[self._face(tmp, [o1[i], o1[j], i1[j], i1[i]], n)] = front
            ids[self._face(tmp, [o0[i], o0[j], o1[j], o1[i]], out)] = front
            ids[self._face(tmp, [i0[i], i0[j], i1[j], i1[i]], -out)] = reveal
        self._commit(tmp, ids.__getitem__)

    def flat_window(self, centre, normal, width, height, slot="bk_glass_dark", inset=0.025, tone=1.0):
        P, n, _, _ = self.wall(centre, normal)
        hw, hh = width / 2, height / 2
        self.polygon(slot, [P(-hw, -hh, inset), P(hw, -hh, inset), P(hw, hh, inset), P(-hw, hh, inset)], n, tone)

    def window(self, centre, normal, width, height, frame_slot="bk_paint", glass_slot="bk_glass_dark",
               sill_slot="bk_paint", frame=0.1, depth=0.09, cols=2, rows=2, sill=True, lintel_slot=None,
               shutter_slot=None, glass_tone=(0.8, 1.0), bar=0.06):
        P, n, u, yaw = self.wall(centre, normal)
        cy = centre[1]
        hw, hh = width / 2, height / 2
        iw, ih = hw - frame, hh - frame
        glass = (glass_tone[0], glass_tone[1], cy - ih, cy + ih)
        self.polygon(glass_slot, [P(-iw, -ih, 0.012), P(iw, -ih, 0.012), P(iw, ih, 0.012), P(-iw, ih, 0.012)], n, glass)
        self.frame_ring(frame_slot, centre, normal, width, height, frame, depth)
        for i in range(1, cols):
            a = -iw + 2 * iw * i / cols
            self.box(frame_slot, P(a, 0.0, 0.03), (bar, 2 * ih, 0.06), yaw=yaw, drop=("-z",))
        for j in range(1, rows):
            b = -ih + 2 * ih * j / rows
            self.box(frame_slot, P(0.0, b, 0.025), (2 * iw, bar, 0.05), yaw=yaw, drop=("-z",))
        if sill:
            self.box(sill_slot, P(0.0, -hh - 0.04, (depth + 0.08) / 2), (width + 0.16, 0.08, depth + 0.08), yaw=yaw, drop=("-z",))
        if lintel_slot:
            self.box(lintel_slot, P(0.0, hh + 0.08, 0.03), (width + 0.12, 0.16, 0.06), yaw=yaw, drop=("-z",))
        if shutter_slot:
            sw = max(0.3, width * 0.36)
            for side in (-1, 1):
                self.box(shutter_slot, P(side * (hw + 0.04 + sw / 2), 0.0, 0.03), (sw, height, 0.06), yaw=yaw,
                         drop=("-z",), tone=0.95)

    def door(self, base, normal, width, height, frame_slot="bk_paint", leaf_slot="bk_paint", frame=0.12, depth=0.1,
             glass_slot=None, step_slot=None, panels=True, leaf_tone=0.94):
        P, n, u, yaw = self.wall(base, normal)
        hw = width / 2
        iw = hw - frame
        top = height - frame
        self.polygon(leaf_slot, [P(-iw, 0.0, 0.012), P(iw, 0.0, 0.012), P(iw, top, 0.012), P(-iw, top, 0.012)], n, leaf_tone)
        if panels:
            pw = iw * 1.4
            self.box(leaf_slot, P(0.0, top * 0.25, 0.03), (pw, top * 0.32, 0.036), yaw=yaw, drop=("-z",))
            if glass_slot:
                b0, b1 = top * 0.55, top * 0.9
                self.polygon(glass_slot, [P(-pw / 2, b0, 0.016), P(pw / 2, b0, 0.016), P(pw / 2, b1, 0.016), P(-pw / 2, b1, 0.016)], n,
                             (0.82, 1.0, base[1] + b0, base[1] + b1))
            else:
                self.box(leaf_slot, P(0.0, top * 0.72, 0.03), (pw, top * 0.4, 0.036), yaw=yaw, drop=("-z",))
        for side in (-1, 1):
            self.box(frame_slot, P(side * (hw - frame / 2), top / 2, depth / 2), (frame, top, depth), yaw=yaw, drop=("-z", "-y"))
        self.box(frame_slot, P(0.0, height - frame / 2, depth / 2), (width, frame, depth), yaw=yaw, drop=("-z",))
        if step_slot and base[1] > 0.02:
            self.box(step_slot, P(0.0, -base[1] / 2, 0.25), (width + 0.5, base[1], 0.5), yaw=yaw, drop=("-z", "-y"))

    def strips(self, slot, centre, normal, width, height, count, tones=(1.0, 0.88), horizontal=True, offset=0.012):
        P, n, _, _ = self.wall(centre, normal)
        hw, hh = width / 2, height / 2
        for k in range(count):
            t = tones[k % len(tones)]
            if horizontal:
                b0, b1 = -hh + 2 * hh * k / count, -hh + 2 * hh * (k + 1) / count
                pts = [P(-hw, b0, offset), P(hw, b0, offset), P(hw, b1, offset), P(-hw, b1, offset)]
            else:
                a0, a1 = -hw + 2 * hw * k / count, -hw + 2 * hw * (k + 1) / count
                pts = [P(a0, -hh, offset), P(a1, -hh, offset), P(a1, hh, offset), P(a0, hh, offset)]
            self.polygon(slot, pts, n, t)

    def gable_roof(self, centre, length, span, eave_y, ridge_y, axis="x", overhang=0.45, gable_overhang=0.35,
                   thickness=0.26, roof_slot="bk_roof", fascia_slot="bk_paint", courses=6, course_tones=(1.0, 0.93),
                   soffit_tone=0.82, ridge_cap=True, fill_slot=None, fill_tone=1.0, roof_tone=1.0, fill_span=None):
        cx, cz = centre
        half = span / 2
        pitch = math.atan2(ridge_y - eave_y, half)
        for _ in range(16):
            pitch = math.atan2(ridge_y - thickness / math.cos(pitch) - eave_y, half)
        tp = math.tan(pitch)
        cv = thickness / math.cos(pitch)
        e = half + overhang
        top = lambda s: ridge_y - abs(s) * tp
        n = max(1, int(courses))
        profile = [(-e + e * k / n, top(-e + e * k / n)) for k in range(n)]
        profile.append((0.0, ridge_y))
        profile += [(e * k / n, top(e * k / n)) for k in range(1, n + 1)]
        profile += [(e, top(e) - cv), (0.0, ridge_y - cv), (-e, top(e) - cv)]
        fascia = self.meta(fascia_slot, 1.0)
        soffit = self.meta(fascia_slot, soffit_tone)
        metas = []
        for i in range(n):
            metas.append(self.meta(roof_slot, roof_tone * course_tones[i % len(course_tones)]))
        for j in range(n):
            metas.append(self.meta(roof_slot, roof_tone * course_tones[(n - 1 - j) % len(course_tones)]))
        metas += [fascia, soffit, soffit, fascia]
        lo, hi = -length / 2 - gable_overhang, length / 2 + gable_overhang
        self.prism(roof_slot, profile, axis, (lo, hi), (cx, 0.0, cz), edge_metas=metas, cap_meta=fascia,
                   cap_fan=2 * n + 2)
        if ridge_cap:
            run = hi - lo + 0.08
            size = (run, 0.24, 0.3) if axis == "x" else (0.3, 0.24, run)
            self.box(roof_slot, (cx, ridge_y - 0.05, cz), size, tone=roof_tone * 0.84, drop=("-y",))
        if fill_slot:
            fl = length if fill_span is None else fill_span
            fill = [(-half, eave_y + 0.038), (half, eave_y + 0.038), (0.0, ridge_y - cv + 0.05)]
            self.prism(fill_slot, fill, axis, (-fl / 2, fl / 2), (cx, 0.0, cz), tone=fill_tone)
        return Roof("gable", axis, cx, cz, eave_y, ridge_y, tp, cv, half, overhang, length)

    def hip_roof(self, centre, size_x, size_z, eave_y, ridge_y, overhang=0.5, thickness=0.24, roof_slot="bk_roof",
                 fascia_slot="bk_paint", courses=5, course_tones=(1.0, 0.93), soffit_tone=0.82, ridge_cap=True,
                 roof_tone=1.0):
        cx, cz = centre
        ex, ez = size_x / 2 + overhang, size_z / 2 + overhang
        y0, y1 = eave_y, eave_y + thickness
        run = min(ex, ez)
        rise = ridge_y - y1
        n = max(1, int(courses))
        tmp = bmesh.new()
        signs = ((-1, -1), (1, -1), (1, 1), (-1, 1))

        def ring(hx, hz, y):
            return [tmp.verts.new((cx + sx * hx, y, cz + sz * hz)) for sx, sz in signs]

        bottom = ring(ex, ez, y0)
        rings = [ring(ex - run * k / n, ez - run * k / n, y1 + rise * k / n) for k in range(n)]
        dx, dz = ex - run, ez - run
        if dx > 1e-4:
            ridge = [tmp.verts.new((cx - dx, ridge_y, cz)), tmp.verts.new((cx + dx, ridge_y, cz))]
            top_of = (0, 1, 1, 0)
        elif dz > 1e-4:
            ridge = [tmp.verts.new((cx, ridge_y, cz - dz)), tmp.verts.new((cx, ridge_y, cz + dz))]
            top_of = (0, 0, 1, 1)
        else:
            ridge = [tmp.verts.new((cx, ridge_y, cz))]
            top_of = (0, 0, 0, 0)
        fascia = self.meta(fascia_slot, 1.0)
        soffit = self.meta(fascia_slot, soffit_tone)
        ids = {tmp.faces.new(bottom): soffit}
        for i in range(4):
            j = (i + 1) % 4
            ids[tmp.faces.new((bottom[i], bottom[j], rings[0][j], rings[0][i]))] = fascia
        for k in range(n - 1):
            m = self.meta(roof_slot, roof_tone * course_tones[k % len(course_tones)])
            for i in range(4):
                j = (i + 1) % 4
                ids[tmp.faces.new((rings[k][i], rings[k][j], rings[k + 1][j], rings[k + 1][i]))] = m
        m = self.meta(roof_slot, roof_tone * course_tones[(n - 1) % len(course_tones)])
        last = rings[n - 1]
        for i in range(4):
            j = (i + 1) % 4
            a, b = ridge[top_of[i]], ridge[top_of[j]]
            verts = (last[i], last[j], b) if a is b else (last[i], last[j], b, a)
            ids[tmp.faces.new(verts)] = m
        bmesh.ops.recalc_face_normals(tmp, faces=tmp.faces[:])
        self._commit(tmp, ids.__getitem__)
        if ridge_cap and max(dx, dz) > 1e-4:
            size = (2 * dx + 0.3, 0.24, 0.3) if dx > dz else (0.3, 0.24, 2 * dz + 0.3)
            self.box(roof_slot, (cx, ridge_y - 0.05, cz), size, tone=roof_tone * 0.84, drop=("-y",))
        return Roof("hip", "x" if dx >= dz else "z", cx, cz, eave_y, ridge_y, rise / run, thickness, run - overhang,
                    overhang, max(size_x, size_z))

    def chimney(self, centre, base_y, top_y, size=(0.9, 0.7), slot="bk_brick", cap_slot=None, pot_slot="bk_roof",
                pots=2, bevel=0.03, tone=1.0, cap_tone=0.88, pot_h=0.25):
        cx, cz = centre
        sx, sz = size
        self.box(slot, (cx, (base_y + top_y) / 2, cz), (sx, top_y - base_y, sz), bevel=bevel, bevel_axes="y",
                 tone=tone, drop=("-y",))
        cap_h = 0.13
        self.box(cap_slot or slot, (cx, top_y + cap_h / 2 - 0.02, cz), (sx + 0.12, cap_h, sz + 0.12),
                 tone=cap_tone if cap_slot is None else 1.0)
        peak = top_y + cap_h - 0.02
        for k in range(pots):
            px = cx + (0.0 if pots == 1 else (k / (pots - 1) - 0.5) * sx * 0.5)
            self.cylinder(pot_slot, (px, peak - 0.01, cz), 0.11, pot_h, segments=8, radius_top=0.095, cap_bottom=False)
        return peak + (pot_h - 0.01 if pots else 0.0)

    def dormer(self, roof, x, inset, width, wall_h, wall_slot="bk_plaster", roof_slot="bk_roof",
               fascia_slot="bk_paint", glass_slot="bk_glass_dark", pitch=42.0, overhang=0.14, front_overhang=0.14,
               thickness=0.16, courses=3, far=False):
        s_front = roof.half - inset
        zf = roof.cz + s_front
        yb = roof.top(s_front)
        ye = yb + wall_h
        hw = width / 2
        ridge = ye + hw * math.tan(math.radians(pitch)) + thickness
        s_back = (roof.ridge_y - ridge - 0.12) / roof.tan
        if s_back > s_front - 0.3:
            raise ValueError("dormer too tall for its roof")
        zb = roof.cz + s_back
        y0 = yb - 0.4
        self.box(wall_slot, (x, (y0 + ye + 0.04) / 2, (zb + zf) / 2), (width, ye + 0.04 - y0, zf - zb),
                 bevel=0.0 if far else 0.02, bevel_axes="y", drop=("-y",))
        back = zb - 0.2
        if far:
            self.prism(roof_slot, [(-hw - overhang, ye - 0.04), (hw + overhang, ye - 0.04), (0.0, ridge)], "z",
                       (back, zf + front_overhang), (x, 0.0, 0.0))
            self.flat_window((x, (yb + ye) / 2 + 0.04, zf), (0, 0, 1), width - 0.5, wall_h - 0.35, glass_slot)
        else:
            self.gable_roof((x, (back + zf) / 2), zf - back, width, ye, ridge, axis="z", overhang=overhang,
                            gable_overhang=front_overhang, thickness=thickness, roof_slot=roof_slot,
                            fascia_slot=fascia_slot, courses=courses, ridge_cap=False, fill_slot=wall_slot)
            self.window((x, (yb + 0.18 + ye - 0.12) / 2, zf), (0, 0, 1), width - 0.5, wall_h - 0.3,
                        frame_slot=fascia_slot, glass_slot=glass_slot, cols=2, rows=1, sill=False, frame=0.09)
        return ridge

    def finish(self, collection=None, centre=True, offset=None):
        bm = self.bm
        bmesh.ops.triangulate(bm, faces=bm.faces[:], quad_method="BEAUTY", ngon_method="BEAUTY")
        if offset is None:
            offset = Vector((0.0, 0.0, 0.0))
            if centre and bm.verts:
                pts = [to_unity(v.co) for v in bm.verts]
                offset = Vector((-(min(p.x for p in pts) + max(p.x for p in pts)) / 2, 0.0,
                                 -(min(p.z for p in pts) + max(p.z for p in pts)) / 2))
        shift = to_blender(offset)
        for v in bm.verts:
            v.co += shift
        self.offset = Vector(offset)
        mesh = bpy.data.meshes.new(self.name)
        bm.to_mesh(mesh)
        bm.free()
        self.bm = None
        attr = mesh.attributes.get("meta")
        ids = [0] * len(mesh.polygons)
        attr.data.foreach_get("value", ids)
        mesh.attributes.remove(attr)
        co = [0.0] * (len(mesh.vertices) * 3)
        mesh.vertices.foreach_get("co", co)
        colours = [0.0] * (len(mesh.loops) * 4)
        slots = []
        for poly, mid in zip(mesh.polygons, ids):
            m = self.metas[mid]
            slots.append(m["slot"])
            for li in poly.loop_indices:
                vi = mesh.loops[li].vertex_index
                colours[4 * li:4 * li + 4] = self._colour(m, to_unity(co[3 * vi:3 * vi + 3]))
        self.obj = _dress(mesh, slots, colours, collection)
        return self.obj

    def _colour(self, m, p):
        sway = m["sway"]
        alpha = clamp((p.y - sway[0]) / (sway[1] - sway[0])) if isinstance(sway, (list, tuple)) else float(sway)
        if m["rgb"] is not None:
            c = m["rgb"] if len(m["rgb"]) == 3 and isinstance(m["rgb"][0], (int, float)) else _gradient(m["rgb"], p.y)
            return (clamp(c[0]), clamp(c[1]), clamp(c[2]), alpha)
        tone = _gradient(m["tone"], p.y)
        shade = self.base_shade + (1.0 - self.base_shade) * smoothstep(0.0, self.base_rise, p.y) if self.base_rise > 0 else 1.0
        v = clamp(shade * tone, SHADE_MIN, SHADE_MAX)
        return (v, v, v, alpha)


def _bisect_grid(tmp, sx, sz, period):
    for axis, extent in ((0, sx), (2, sz)):
        other = 2 if axis == 0 else 0
        k = 1
        while True:
            pos = -extent / 2 + k * period
            if pos > extent / 2 - period * 0.3:
                break
            tmp.normal_update()
            faces = [f for f in tmp.faces if abs(f.normal[other]) > 0.9]
            edges = _unique(e for f in faces for e in f.edges)
            verts = _unique(v for f in faces for v in f.verts)
            co = [0.0, 0.0, 0.0]
            no = [0.0, 0.0, 0.0]
            co[axis], no[axis] = pos, 1.0
            bmesh.ops.bisect_plane(tmp, geom=verts + edges + faces, dist=1e-6, plane_co=co, plane_no=no)
            k += 1


def measure(obj):
    mw = obj.matrix_world
    pts = [to_unity(mw @ v.co) for v in obj.data.vertices]
    lo = Vector((min(p.x for p in pts), min(p.y for p in pts), min(p.z for p in pts)))
    hi = Vector((max(p.x for p in pts), max(p.y for p in pts), max(p.z for p in pts)))
    return {"min": list(lo), "max": list(hi), "width": hi.x - lo.x, "height": hi.y - lo.y, "depth": hi.z - lo.z,
            "centre": [(lo.x + hi.x) / 2, (lo.z + hi.z) / 2]}


def file_to_unity(v):
    return Vector((v[0], v[2], v[1]))


def facing(obj, to_u=to_unity):
    mesh = obj.data
    rot = obj.matrix_world.to_3x3()
    names = [m.name if m else "" for m in mesh.materials]
    card = bool(names) and all(n in RGB_FREE for n in names)
    front = back = toward = total = 0.0
    for p in mesh.polygons:
        if not card and names[p.material_index] not in FRONT_SLOTS:
            continue
        n = to_u((rot @ p.normal).normalized())
        area = p.area
        total += area
        if n.z > 0.5:
            toward += area
        if n.z > 0.7:
            front += area
        elif n.z < -0.7:
            back += area
    bias = (front - back) / (front + back) if front + back > 0 else 0.0
    return {"frontBias": round(bias, 4), "facingZ": round(toward / total, 4) if total else 0.0, "card": card}


def triangles(mesh):
    return sum(p.loop_total - 2 for p in mesh.polygons)


def slot_triangles(mesh):
    out = {}
    for p in mesh.polygons:
        name = mesh.materials[p.material_index].name
        out[name] = out.get(name, 0) + p.loop_total - 2
    return out


def unity_vertices(mesh):
    colours = read_colours(mesh) or []
    uv = mesh.uv_layers.active
    try:
        normals = [tuple(round(c, 3) for c in n.vector) for n in mesh.corner_normals]
    except AttributeError:
        normals = [tuple(round(c, 3) for c in p.normal) for p in mesh.polygons for _ in p.loop_indices]
    keys = set()
    for li, loop in enumerate(mesh.loops):
        col = tuple(round(c, 3) for c in colours[4 * li:4 * li + 4])
        tex = tuple(round(c, 3) for c in uv.data[li].uv) if uv else ()
        keys.add((loop.vertex_index, normals[li], tex, col))
    return len(keys)


def check_piece(obj, max_tris, target=None, tolerance=SIZE_TOLERANCE, rgb_free=RGB_FREE, sway_slots=SWAY_SLOTS):
    problems = []
    mesh = obj.data
    if not NAME_RULE.match(obj.name):
        problems.append("name breaks the rule")
    if mesh.name != obj.name:
        problems.append(f"mesh name {mesh.name}")
    if obj.parent is not None or obj.children:
        problems.append("has a parent or children")
    tris = triangles(mesh)
    if tris > max_tris:
        problems.append(f"{tris} tris > {max_tris}")
    m = measure(obj)
    for key, value in zip(("width", "depth", "height"), target or ()):
        if value and abs(m[key] - value) / value > tolerance:
            problems.append(f"{key} {m[key]:.3f} vs target {value} (> {tolerance:.0%})")
    if m["min"][1] < FLOOR_LIMIT:
        problems.append(f"geometry at y {m['min'][1]:.3f}")
    if abs(m["centre"][0]) > 0.02 * m["width"] + 0.01 or abs(m["centre"][1]) > 0.02 * m["depth"] + 0.01:
        problems.append(f"pivot off centre {m['centre']}")
    names = [mat.name if mat else None for mat in mesh.materials]
    if len(names) > MAX_SLOTS:
        problems.append(f"{len(names)} slots")
    for name in names:
        if name not in SLOTS:
            problems.append(f"bad slot {name}")
    counts = slot_triangles(mesh)
    for name in names:
        if not counts.get(name):
            problems.append(f"slot {name} unused")
    attr = mesh.color_attributes.get("Col")
    if attr is None or attr.data_type != "BYTE_COLOR" or attr.domain != "CORNER":
        problems.append("Col attribute missing or not byte corner")
    else:
        flat = read_colours(mesh)
        bad_rgb = bad_alpha = 0
        for p in mesh.polygons:
            slot = names[p.material_index]
            for li in p.loop_indices:
                r, g, b, a = flat[4 * li:4 * li + 4]
                if slot not in rgb_free and min(r, g, b) < SHADE_MIN - 0.004 or max(r, g, b, a) > 1.0001:
                    bad_rgb += 1
                if slot not in sway_slots and a > 0.004:
                    bad_alpha += 1
        if bad_rgb:
            problems.append(f"{bad_rgb} corners with RGB outside {SHADE_MIN}..{SHADE_MAX}")
        if bad_alpha:
            problems.append(f"{bad_alpha} corners with sway outside {sway_slots}")
    thin = [p for p in mesh.polygons if p.area < 1e-7]
    if thin:
        where = [f"{names[p.material_index]}@{tuple(round(c, 2) for c in to_unity(p.center))}" for p in thin[:4]]
        problems.append(f"{len(thin)} degenerate faces " + " ".join(where))
    face = facing(obj)
    if face["card"] and face["facingZ"] < 0.95:
        problems.append(f"card faces +Z on only {face['facingZ']:.0%} of its area")
    report = {"id": obj.name, "tris": tris, "max_tris": max_tris, "verts": unity_vertices(mesh),
              "width": m["width"], "depth": m["depth"], "height": m["height"], "min_y": m["min"][1],
              "centre": m["centre"], "slots": names, "slotTris": counts, "target": list(target or ()),
              "frontBias": face["frontBias"], "facingZ": face["facingZ"], "problems": problems}
    print("KIT_CHECK " + json.dumps(report), flush=True)
    if problems:
        raise RuntimeError(f"{obj.name}: " + "; ".join(problems))
    return report


def manifest_entry(report, family, far=None, fbx=None):
    entry = {"id": report["id"], "family": family, "width": round(report["width"], 3),
             "depth": round(report["depth"], 3), "height": round(report["height"], 3), "slots": list(report["slots"]),
             "tris": report["tris"]}
    if far:
        entry["far"] = far
    entry["fbxObject"] = report["id"]
    if fbx:
        entry["fbx"] = fbx
    entry["slotTris"] = {k: report["slotTris"][k] for k in report["slots"]}
    entry["verts"] = report["verts"]
    return entry


def write_manifest(path, kit, fbx, entries):
    path = Path(path)
    path.parent.mkdir(parents=True, exist_ok=True)
    data = {"kit": kit, "fbx": fbx, "pieces": entries}
    path.write_text(json.dumps(data, indent=2) + "\n", encoding="utf-8", newline="\n")
    return path


def _turn(objects):
    for obj in objects:
        obj.data.transform(UNITY_FRONT)
        obj.data.update()


def export_fbx(path, objects, unity_front=True, bake_space=True):
    path = Path(path)
    path.parent.mkdir(parents=True, exist_ok=True)
    chosen = set(objects)
    for obj in bpy.context.scene.objects:
        obj.select_set(obj in chosen)
    bpy.context.view_layer.objects.active = objects[0]
    options = dict(filepath=str(path), use_selection=True, object_types={"MESH"}, apply_unit_scale=True,
                   apply_scale_options="FBX_SCALE_UNITS", axis_forward="-Z", axis_up="Y",
                   bake_space_transform=bake_space, use_mesh_modifiers=True, mesh_smooth_type="FACE",
                   use_tspace=False, use_custom_props=False, add_leaf_bones=False, bake_anim=False,
                   path_mode="STRIP", embed_textures=False, colors_type="SRGB", prioritize_active_color=True)
    if unity_front:
        _turn(objects)
    try:
        try:
            bpy.ops.export_scene.fbx(**options)
        except TypeError:
            options.pop("colors_type")
            options.pop("prioritize_active_color")
            bpy.ops.export_scene.fbx(**options)
    finally:
        if unity_front:
            _turn(objects)
    for obj in objects:
        obj.select_set(False)
    return path


def verify_fbx(path, expected, authored=None):
    path = Path(path)
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=str(path))
    bpy.context.view_layer.update()
    meshes = {o.name: o for o in bpy.context.scene.objects if o.type == "MESH"}
    problems, rows = [], []
    wanted = {e["id"] for e in expected}
    for name in sorted(set(meshes) - wanted):
        problems.append(f"{path.name}: unexpected object {name}")
    for e in expected:
        obj = meshes.get(e["id"])
        if obj is None:
            problems.append(f"{path.name}: missing {e['id']}")
            continue
        mesh = obj.data
        m = measure(obj)
        tris = triangles(mesh)
        mats = sorted(mat.name for mat in mesh.materials if mat)
        coloured = len(mesh.color_attributes) > 0
        error = max(abs(m["width"] - e["width"]), abs(m["height"] - e["height"]), abs(m["depth"] - e["depth"]))
        face = facing(obj, file_to_unity)
        row = {"id": e["id"], "tris": tris, "slots": mats, "coloured": coloured, "bounds_error": round(error, 5),
               "parent": obj.parent.name if obj.parent else None, "unityFrontBias": face["frontBias"],
               "unityFacingZ": face["facingZ"]}
        ok = tris == e["tris"] and mats == sorted(e["slots"]) and coloured and error < 0.003 and obj.parent is None
        if face["card"] and face["facingZ"] < 0.95:
            ok = False
        source = (authored or {}).get(e["id"])
        if source is not None and abs(face["frontBias"] - source["frontBias"]) > 0.05:
            ok = False
        row["passed"] = ok
        rows.append(row)
        print("KIT_FBX " + json.dumps(row), flush=True)
        if not ok:
            problems.append(f"{path.name}: {e['id']} {row}")
    return problems, rows


def _emission(name, colour, strength=1.0):
    mat = bpy.data.materials.get(name) or bpy.data.materials.new(name)
    tree = _node_tree(mat)
    for node in list(tree.nodes):
        tree.nodes.remove(node)
    out = tree.nodes.new("ShaderNodeOutputMaterial")
    em = tree.nodes.new("ShaderNodeEmission")
    em.inputs["Color"].default_value = (*colour, 1.0)
    em.inputs["Strength"].default_value = strength
    tree.links.new(em.outputs["Emission"], out.inputs["Surface"])
    return mat


def _plain(name, srgb_hex, roughness=0.8):
    mat = bpy.data.materials.get(name) or bpy.data.materials.new(name)
    node = _principled(mat)
    node.inputs["Base Color"].default_value = (*hex_linear(srgb_hex), 1.0)
    node.inputs["Roughness"].default_value = roughness
    return mat


def preview_materials():
    for slot in SLOTS:
        mat = bpy.data.materials.get(slot)
        if mat is None:
            continue
        tree = _node_tree(mat)
        node = _principled(mat)
        attr = tree.nodes.new("ShaderNodeAttribute")
        attr.attribute_type = "GEOMETRY"
        attr.attribute_name = "Col"
        mul = tree.nodes.new("ShaderNodeVectorMath")
        mul.operation = "MULTIPLY"
        tree.links.new(attr.outputs["Color"], mul.inputs[0])
        mul.inputs[1].default_value = hex_linear(SLOTS[slot])
        tree.links.new(mul.outputs["Vector"], node.inputs["Base Color"])
        if slot == "bk_glass_lit":
            tree.links.new(mul.outputs["Vector"], node.inputs["Emission Color"])


def _studio(scene):
    collection = bpy.data.collections.new("sheet_studio")
    scene.collection.children.link(collection)
    bm = bmesh.new()
    s = 2500.0
    bm.faces.new([bm.verts.new(p) for p in ((-s, -s, -0.02), (s, -s, -0.02), (s, s, -0.02), (-s, s, -0.02))])
    mesh = bpy.data.meshes.new("sheet_ground")
    bm.to_mesh(mesh)
    bm.free()
    mesh.materials.append(_plain("sheet_ground", "AEB89E", 0.95))
    ground = bpy.data.objects.new("sheet_ground", mesh)
    collection.objects.link(ground)
    world = bpy.data.worlds.new("sheet_sky")
    tree = _node_tree(world)
    bg = next((n for n in tree.nodes if n.type == "BACKGROUND"), None)
    if bg is None:
        bg = tree.nodes.new("ShaderNodeBackground")
        out = next((n for n in tree.nodes if n.type == "OUTPUT_WORLD"), None) or tree.nodes.new("ShaderNodeOutputWorld")
        tree.links.new(bg.outputs["Background"], out.inputs["Surface"])
    bg.inputs["Color"].default_value = (0.52, 0.66, 0.86, 1.0)
    bg.inputs["Strength"].default_value = 0.8
    scene.world = world
    sun_data = bpy.data.lights.new("sheet_sun", "SUN")
    sun_data.energy = 3.2
    sun_data.angle = math.radians(4.0)
    sun_data.color = (1.0, 0.95, 0.86)
    sun = bpy.data.objects.new("sheet_sun", sun_data)
    collection.objects.link(sun)
    towards = Vector((-0.55, -0.8, 0.95)).normalized()
    sun.rotation_euler = (-towards).to_track_quat("-Z", "Y").to_euler()
    cam_data = bpy.data.cameras.new("sheet_camera")
    cam_data.type = "ORTHO"
    camera = bpy.data.objects.new("sheet_camera", cam_data)
    collection.objects.link(camera)
    scene.camera = camera
    curve = bpy.data.curves.new("sheet_label", "FONT")
    curve.materials.append(_emission("sheet_label", (0.03, 0.035, 0.045)))
    label = bpy.data.objects.new("sheet_label", curve)
    collection.objects.link(label)
    label.parent = camera
    label.visible_shadow = False
    fig = Piece("sheet_figure", base_rise=0.0)
    fig.cylinder("bk_paint", (0.0, 0.0, 0.0), 0.22, 1.42, segments=10)
    fig.ico("bk_paint", (0.0, 1.52, 0.0), 0.2, subdivisions=1)
    figure = fig.finish(collection, centre=False)
    figure.data.materials[0] = _plain("sheet_figure", "E3A93A", 0.6)
    figure.hide_render = True
    return {"collection": collection, "camera": camera, "label": label, "figure": figure, "ground": ground}


def _cycles(scene, samples, threads):
    scene.render.engine = "CYCLES"
    scene.cycles.device = "CPU"
    scene.cycles.samples = samples
    scene.cycles.use_denoising = True
    scene.cycles.max_bounces = 4
    scene.render.threads_mode = "FIXED"
    scene.render.threads = threads
    scene.render.resolution_percentage = 100
    scene.render.film_transparent = False
    scene.render.image_settings.file_format = "PNG"
    scene.render.image_settings.color_mode = "RGB"
    scene.view_settings.view_transform = "AgX"


def _frame(camera, objects, view, res, band=0.13):
    bpy.context.view_layer.update()
    rot = (-view).to_track_quat("-Z", "Y")
    camera.rotation_euler = rot.to_euler()
    right, up, fwd = rot @ Vector((1, 0, 0)), rot @ Vector((0, 1, 0)), rot @ Vector((0, 0, -1))
    corners = [o.matrix_world @ Vector(c) for o in objects for c in o.bound_box]
    xs = [c.dot(right) for c in corners]
    ys = [c.dot(up) for c in corners]
    ds = [c.dot(fwd) for c in corners]
    aspect = res[0] / res[1]
    wx = (max(xs) - min(xs)) * 1.08 + 0.2
    wy = (max(ys) - min(ys)) * 1.08 + 0.2
    scale = max(wx, wy / (1.0 - band) * aspect)
    span_v = scale / aspect
    cx = (max(xs) + min(xs)) / 2
    cy = (max(ys) + min(ys)) / 2 - span_v * band / 2
    depth = min(ds) - 60.0
    camera.location = right * cx + up * cy + fwd * depth
    camera.data.ortho_scale = scale
    camera.data.clip_start = 0.1
    camera.data.clip_end = max(ds) - depth + 3000.0
    return scale, span_v


def _shoot(scene, studio, every, visible, view, res, label, path):
    shown = set(visible)
    for obj in every:
        obj.hide_render = obj not in shown
    studio["figure"].hide_render = studio["figure"] not in shown
    scale, span_v = _frame(studio["camera"], visible, view, res)
    text = studio["label"]
    text.data.body = label
    text.data.size = span_v * 0.07
    text.location = (-scale / 2 + scale * 0.02, -span_v / 2 + span_v * 0.035, -1.0)
    scene.render.resolution_x, scene.render.resolution_y = res
    scene.render.filepath = str(path)
    bpy.ops.render.render(write_still=True)
    return path


def _load(path):
    import numpy as np
    img = bpy.data.images.load(str(path), check_existing=False)
    w, h = img.size
    arr = np.empty(w * h * 4, dtype=np.float32)
    img.pixels.foreach_get(arr)
    bpy.data.images.remove(img)
    return arr.reshape(h, w, 4)[::-1]


def compose(rows, out_path, background=(0.1, 0.11, 0.13)):
    import numpy as np
    loaded = [[_load(p) for p in row] for row in rows if row]
    width = max(sum(a.shape[1] for a in row) + GUTTER * (len(row) + 1) for row in loaded)
    height = sum(max(a.shape[0] for a in row) for row in loaded) + GUTTER * (len(loaded) + 1)
    canvas = np.empty((height, width, 4), dtype=np.float32)
    canvas[:] = (*background, 1.0)
    y = GUTTER
    for row in loaded:
        x = GUTTER
        for a in row:
            canvas[y:y + a.shape[0], x:x + a.shape[1]] = a
            x += a.shape[1] + GUTTER
        y += max(a.shape[0] for a in row) + GUTTER
    img = bpy.data.images.new("contact_sheet", width, height, alpha=False)
    img.pixels.foreach_set(np.ascontiguousarray(canvas[::-1]).ravel())
    img.filepath_raw = str(out_path)
    img.file_format = "PNG"
    img.save()
    bpy.data.images.remove(img)
    return out_path


def render_sheet(entries, out_path, columns=5, tile=(400, 330), samples=24, lineup=None, threads=8, lineup_height=440):
    out_path = Path(out_path)
    out_path.parent.mkdir(parents=True, exist_ok=True)
    tiles = out_path.parent / (out_path.stem + "_tiles")
    tiles.mkdir(parents=True, exist_ok=True)
    scene = bpy.context.scene
    preview_materials()
    studio = _studio(scene)
    _cycles(scene, samples, threads)
    every = [e["obj"] for e in entries]
    three_quarter, couch = [], []
    for e in entries:
        view = VIEW_FRONT if e.get("card") else VIEW_34
        three_quarter.append(_shoot(scene, studio, every, [e["obj"]], view, tile, f"{e['id']}   {e['tris']} tris",
                                    tiles / f"{e['id']}__34.png"))
    for e in entries:
        if not e.get("card"):
            couch.append(_shoot(scene, studio, every, [e["obj"]], VIEW_COUCH, tile, f"{e['id']}   couch 54",
                                tiles / f"{e['id']}__couch.png"))
    rows = [three_quarter[i:i + columns] for i in range(0, len(three_quarter), columns)]
    rows += [couch[i:i + columns] for i in range(0, len(couch), columns)]
    if lineup:
        chosen = [e for e in entries if e["id"] in lineup and not e.get("card")]
        cursor = 0.0
        figure = studio["figure"]
        figure.location = (cursor, 0.0, 0.0)
        cursor += 2.5
        for e in chosen:
            w = measure(e["obj"])["width"]
            e["obj"].location = (cursor + w / 2, 0.0, 0.0)
            cursor += w + 3.0
        width = columns * tile[0] + (columns - 1) * GUTTER
        rows.append([_shoot(scene, studio, every, [e["obj"] for e in chosen] + [figure], VIEW_COUCH,
                            (width, lineup_height), "lineup at true scale, couch 54, figure 1.7 m",
                            tiles / "lineup__couch.png")])
        for e in chosen:
            e["obj"].location = (0.0, 0.0, 0.0)
        figure.location = (0.0, 0.0, 0.0)
    for obj in every:
        obj.hide_render = False
    compose(rows, out_path)
    print("KIT_SHEET " + str(out_path), flush=True)
    return out_path
