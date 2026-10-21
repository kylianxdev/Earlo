"""
Renders the Galaxy Buds artwork in src/PodPilot.App/Assets/Devices (galaxy-*.png).

Every model is built from signed distance functions, meshed with marching cubes and rendered
with Cycles on a transparent background, in the same three-quarter studio view as the AirPods
and Beats renders: the open case at the back, both buds in front of it.

Needs Python 3.13 with: pip install bpy==5.2.2 scikit-image pillow
Usage: python tools/render-galaxy-buds.py [model ...] [--samples N] [--out DIR]
"""

import argparse
import math
import os
import tempfile

import numpy as np

# ---------------------------------------------------------------------------------------------
# Signed distance functions. Points are (N, 3) arrays in millimetres; negative is inside.
# ---------------------------------------------------------------------------------------------


def _v(x):
    return np.asarray(x, dtype=np.float64)


def length(v):
    return np.sqrt((v * v).sum(-1))


def rotation(axis, degrees):
    a = _v(axis) / np.linalg.norm(axis)
    t = math.radians(degrees)
    c, s = math.cos(t), math.sin(t)
    x, y, z = a
    return _v([
        [c + x * x * (1 - c), x * y * (1 - c) - z * s, x * z * (1 - c) + y * s],
        [y * x * (1 - c) + z * s, c + y * y * (1 - c), y * z * (1 - c) - x * s],
        [z * x * (1 - c) - y * s, z * y * (1 - c) + x * s, c + z * z * (1 - c)],
    ])


def smin(a, b, k):
    if k <= 0:
        return np.minimum(a, b)
    h = np.clip(0.5 + 0.5 * (b - a) / k, 0.0, 1.0)
    return b * (1 - h) + a * h - k * h * (1 - h)


class Shape:
    def __init__(self, f):
        self.f = f

    def __call__(self, p):
        return self.f(p)

    def move(self, *offset):
        o = _v(offset)
        return Shape(lambda p: self.f(p - o))

    def rotate(self, axis, degrees):
        r = rotation(axis, degrees)
        return Shape(lambda p: self.f(p @ r))

    def stretch(self, sx, sy, sz):
        s = _v([sx, sy, sz])
        return Shape(lambda p: self.f(p / s) * s.min())

    def mirror_x(self):
        return Shape(lambda p: self.f(p * _v([-1, 1, 1])))

    def place(self, frame_):
        """Moves the shape into a frame from `frame()`: local axes as columns of r, origin at t."""
        r, t = frame_
        return Shape(lambda p: self.f((p - t) @ r))

    def grow(self, r):
        return Shape(lambda p: self.f(p) - r)


def union(*shapes, k=0.0):
    def f(p):
        d = shapes[0](p)
        for s in shapes[1:]:
            d = smin(d, s(p), k)
        return d
    return Shape(f)


def intersect(*shapes, k=0.0):
    def f(p):
        d = shapes[0](p)
        for s in shapes[1:]:
            d = -smin(-d, -s(p), k)
        return d
    return Shape(f)


def subtract(a, *cuts, k=0.0):
    def f(p):
        d = a(p)
        for s in cuts:
            d = -smin(-d, s(p), k)
        return d
    return Shape(f)


def ellipsoid(r, c=(0, 0, 0)):
    r, c = _v(r), _v(c)

    def f(p):
        q = p - c
        k0 = length(q / r)
        k1 = length(q / (r * r))
        return k0 * (k0 - 1) / np.maximum(k1, 1e-9)
    return Shape(f)


def round_cone(a, b, r1, r2):
    """A capsule from a (radius r1) to b (radius r2)."""
    a, b = _v(a), _v(b)
    ba = b - a
    l2 = ba @ ba
    rr = r1 - r2
    a2 = l2 - rr * rr
    il2 = 1.0 / l2

    def f(p):
        pa = p - a
        y = pa @ ba
        z = y - l2
        x2v = pa * l2 - y[:, None] * ba
        x2 = (x2v * x2v).sum(-1)
        y2 = y * y * l2
        z2 = z * z * l2
        k = np.sign(rr) * rr * rr * x2
        d_cap_b = np.sqrt(x2 + z2) * il2 - r2
        d_cap_a = np.sqrt(x2 + y2) * il2 - r1
        d_side = (np.sqrt(np.maximum(x2 * a2 * il2, 0)) + y * rr) * il2 - r1
        return np.where(np.sign(z) * a2 * z2 > k, d_cap_b, np.where(np.sign(y) * a2 * y2 < k, d_cap_a, d_side))
    return Shape(f)


def box(half, r=0.0, c=(0, 0, 0)):
    half, c = _v(half), _v(c)

    def f(p):
        q = np.abs(p - c) - half + r
        return length(np.maximum(q, 0)) + np.minimum(q.max(-1), 0) - r
    return Shape(f)


def half_space(normal, offset):
    """Everything on the far side of the plane n.p = offset, against the normal."""
    n = _v(normal) / np.linalg.norm(normal)
    return Shape(lambda p: p @ n - offset)


def rounded_rect(hx, hy, corner):
    def f(u, v):
        qx = np.abs(u) - hx + corner
        qy = np.abs(v) - hy + corner
        return np.hypot(np.maximum(qx, 0), np.maximum(qy, 0)) + np.minimum(np.maximum(qx, qy), 0) - corner
    return f


def polygon(points):
    pts = [_v(p) for p in points]

    def f(u, v):
        p = np.stack([u, v], -1)
        d = ((p - pts[0]) ** 2).sum(-1)
        s = np.ones(len(u))
        n = len(pts)
        for i in range(n):
            vi, vj = pts[i], pts[i - 1]
            e = vj - vi
            w = p - vi
            b = w - e * np.clip((w @ e) / (e @ e), 0, 1)[:, None]
            d = np.minimum(d, (b * b).sum(-1))
            c1 = p[:, 1] >= vi[1]
            c2 = p[:, 1] < vj[1]
            c3 = e[0] * w[:, 1] > e[1] * w[:, 0]
            flip = (c1 & c2 & c3) | (~c1 & ~c2 & ~c3)
            s = np.where(flip, -s, s)
        return s * np.sqrt(d)
    return f


def extrude(profile, half_height, edge, axes="xyz"):
    """Extrudes a 2D profile(u, v) along the third axis, with all edges rounded by `edge`."""
    iu, iv, iw = ("xyz".index(axes[0]), "xyz".index(axes[1]), "xyz".index(axes[2]))

    def f(p):
        d2 = profile(p[:, iu], p[:, iv]) + edge
        dz = np.abs(p[:, iw]) - half_height + edge
        return np.minimum(np.maximum(d2, dz), 0) + np.hypot(np.maximum(d2, 0), np.maximum(dz, 0)) - edge
    return Shape(f)


def ellipse(a, b):
    def f(u, v):
        k0 = np.hypot(u / a, v / b)
        k1 = np.hypot(u / (a * a), v / (b * b))
        return k0 * (k0 - 1) / np.maximum(k1, 1e-9)
    return f


def rounded(profile, r):
    return lambda u, v: profile(u, v) - r


def arc(radius, half_angle, thickness):
    """A tube bent along a circular arc in the XY plane, bulging toward +Y, with round ends."""
    sa, ca = math.sin(math.radians(half_angle)), math.cos(math.radians(half_angle))

    def f(p):
        x, y, z = np.abs(p[:, 0]), p[:, 1] + radius, p[:, 2]
        k = np.where(ca * x > sa * y, x * sa + y * ca, np.hypot(x, y))
        return np.sqrt(np.maximum(x * x + y * y + z * z + radius * radius - 2 * radius * k, 0)) - thickness
    return Shape(f)


def domed(shape, radius):
    """Bends a shape so its top (and bottom) bulge like part of a sphere of the given radius."""
    def f(p):
        q = p.copy()
        q[:, 2] += (p[:, 0] ** 2 + p[:, 1] ** 2) / (2 * radius)
        return shape(q)
    return Shape(f)


# ---------------------------------------------------------------------------------------------
# Meshing
# ---------------------------------------------------------------------------------------------


def _evaluate(shape, xs, ys, zs):
    vol = np.empty((len(xs), len(ys), len(zs)), dtype=np.float32)
    yy, zz = np.meshgrid(ys, zs, indexing="ij")
    plane = np.stack([np.zeros(yy.size), yy.ravel(), zz.ravel()], -1)
    step = max(1, 3_000_000 // plane.shape[0])
    for i in range(0, len(xs), step):
        chunk = xs[i:i + step]
        pts = np.repeat(plane[None], len(chunk), 0)
        pts[:, :, 0] = chunk[:, None]
        vol[i:i + len(chunk)] = shape(pts.reshape(-1, 3)).reshape(len(chunk), len(ys), len(zs))
    return vol


def _gradient(shape, p, eps):
    g = np.empty_like(p)
    for i in range(3):
        o = np.zeros(3)
        o[i] = eps
        g[:, i] = shape(p + o) - shape(p - o)
    n = np.linalg.norm(g, axis=1, keepdims=True)
    return g / np.maximum(n, 1e-12)


def mesh(shape, voxel, extent=60.0):
    """Returns (vertices, faces, normals) of the zero surface of `shape`."""
    from skimage.measure import marching_cubes

    coarse = 1.0
    axis = np.arange(-extent, extent + coarse, coarse)
    vol = _evaluate(shape, axis, axis, axis)
    inside = np.argwhere(vol < coarse)
    if len(inside) == 0:
        raise ValueError("shape is empty")
    lo = axis[inside.min(0)] - 2 * coarse
    hi = axis[inside.max(0)] + 2 * coarse

    xs, ys, zs = (np.arange(lo[i], hi[i] + voxel, voxel) for i in range(3))
    vol = _evaluate(shape, xs, ys, zs)
    verts, faces, _, _ = marching_cubes(vol, 0.0, spacing=(voxel, voxel, voxel))
    verts = verts.astype(np.float64) + _v([xs[0], ys[0], zs[0]])

    # Pull the vertices onto the true surface and take the normals from the field itself,
    # so glossy reflections stay smooth instead of showing the marching cubes facets.
    eps = voxel * 0.25
    for _ in range(2):
        n = _gradient(shape, verts, eps)
        verts -= shape(verts)[:, None] * n
    normals = _gradient(shape, verts, eps)

    tri = verts[faces]
    fn = np.cross(tri[:, 1] - tri[:, 0], tri[:, 2] - tri[:, 0])
    if (fn * normals[faces].mean(1)).sum() < 0:
        faces = faces[:, ::-1]
    return verts, faces.astype(np.int32), normals


# ---------------------------------------------------------------------------------------------
# Blender scene
# ---------------------------------------------------------------------------------------------

SCALE = 0.02  # Blender units per millimetre: a 50 mm case is one unit wide.


def srgb(hex_color):
    h = hex_color.lstrip("#")
    c = [int(h[i:i + 2], 16) / 255 for i in (0, 2, 4)]
    return tuple(x / 12.92 if x <= 0.04045 else ((x + 0.055) / 1.055) ** 2.4 for x in c)


class Mat:
    """A Principled BSDF material description."""

    def __init__(self, color, roughness=0.3, metallic=0.0, coat=0.0, emission=0.0, subsurface=0.0, specular=0.5,
                 clear=False):
        self.color = srgb(color) if isinstance(color, str) else color
        self.roughness, self.metallic, self.coat = roughness, metallic, coat
        self.emission, self.subsurface, self.specular, self.clear = emission, subsurface, specular, clear

    def build(self, name):
        import bpy
        m = bpy.data.materials.new(name)
        if hasattr(m, "use_nodes"):
            m.use_nodes = True
        if self.clear:
            return self._build_clear(m)
        bsdf = next(n for n in m.node_tree.nodes if n.type == "BSDF_PRINCIPLED")
        i = bsdf.inputs
        i["Base Color"].default_value = (*self.color, 1)
        i["Roughness"].default_value = self.roughness
        i["Metallic"].default_value = self.metallic
        i["IOR"].default_value = 1.45
        i["Specular IOR Level"].default_value = self.specular
        i["Coat Weight"].default_value = self.coat
        i["Coat Roughness"].default_value = 0.05
        if self.subsurface:
            i["Subsurface Weight"].default_value = self.subsurface
            i["Subsurface Radius"].default_value = (0.4, 0.4, 0.4)
            i["Subsurface Scale"].default_value = 0.02
        if self.emission:
            i["Emission Color"].default_value = (*self.color, 1)
            i["Emission Strength"].default_value = self.emission
        return m

    def _build_clear(self, m):
        """Thin clear plastic: tinted see-through with a glossy, Fresnel-weighted reflection."""
        nodes, links = m.node_tree.nodes, m.node_tree.links
        for n in list(nodes):
            if n.type != "OUTPUT_MATERIAL":
                nodes.remove(n)
        out = next(n for n in nodes if n.type == "OUTPUT_MATERIAL")
        see = nodes.new("ShaderNodeBsdfTransparent")
        see.inputs["Color"].default_value = (*self.color, 1)
        shine = nodes.new("ShaderNodeBsdfGlossy")
        shine.inputs["Roughness"].default_value = self.roughness
        weight = nodes.new("ShaderNodeLayerWeight")
        weight.inputs["Blend"].default_value = 0.12
        # Cap the reflection so grazing edges stay see-through instead of mirroring the dark floor.
        cap = nodes.new("ShaderNodeMath")
        cap.operation = "MULTIPLY"
        cap.inputs[1].default_value = 0.22
        links.new(weight.outputs["Fresnel"], cap.inputs[0])
        # A little milky scatter keeps the lid visible against a dark background.
        haze = nodes.new("ShaderNodeBsdfDiffuse")
        haze.inputs["Color"].default_value = (*self.color, 1)
        clear = nodes.new("ShaderNodeMixShader")
        clear.inputs["Fac"].default_value = 0.07
        links.new(see.outputs[0], clear.inputs[1])
        links.new(haze.outputs[0], clear.inputs[2])
        mix = nodes.new("ShaderNodeMixShader")
        links.new(cap.outputs[0], mix.inputs["Fac"])
        links.new(clear.outputs[0], mix.inputs[1])
        links.new(shine.outputs[0], mix.inputs[2])
        links.new(mix.outputs[0], out.inputs["Surface"])
        return m


class Part:
    """A shape with one or more materials; `regions(p)` picks the material index per face."""

    def __init__(self, shape, mats, regions=None, voxel=0.15):
        self.shape, self.mats, self.regions, self.voxel = shape, mats, regions, voxel


def add_part(part, name, matrix):
    import bpy
    from mathutils import Matrix

    verts, faces, normals = mesh(part.shape, part.voxel)
    me = bpy.data.meshes.new(name)
    me.vertices.add(len(verts))
    me.vertices.foreach_set("co", (verts * SCALE).astype(np.float32).ravel())
    me.loops.add(faces.size)
    me.loops.foreach_set("vertex_index", faces.ravel())
    me.polygons.add(len(faces))
    me.polygons.foreach_set("loop_start", np.arange(0, faces.size, 3, dtype=np.int32))
    me.update(calc_edges=True)

    for k, mat in enumerate(part.mats):
        me.materials.append(mat.build(f"{name}.{k}"))
    if part.regions is not None:
        centers = verts[faces].mean(1)
        me.polygons.foreach_set("material_index", part.regions(centers).astype(np.int32))
    me.polygons.foreach_set("use_smooth", np.ones(len(faces), dtype=bool))
    me.normals_split_custom_set_from_vertices(normals.astype(np.float32))

    obj = bpy.data.objects.new(name, me)
    obj.matrix_world = Matrix([list(r) for r in matrix])
    bpy.context.scene.collection.objects.link(obj)
    return obj


def frame(position, z_dir, y_dir):
    """Rotation (local axes as columns) and origin that put local +Z along z_dir and +Y along y_dir."""
    z = _v(z_dir) / np.linalg.norm(z_dir)
    y = _v(y_dir) - (_v(y_dir) @ z) * z
    y /= np.linalg.norm(y)
    return np.stack([np.cross(y, z), y, z], 1), _v(position)


def placement(position, z_dir, y_dir, mirror=False):
    """A 4x4 matrix for `frame()`, in Blender units; `mirror` turns the right bud into the left one."""
    r, t = frame(position, z_dir, y_dir)
    m = np.eye(4)
    m[:3, :3] = r
    if mirror:
        m[:3, 0] = -m[:3, 0]
    m[:3, 3] = t * SCALE
    return m


def hinge(position, open_degrees):
    """A matrix that swings a lid modelled closed around a hinge along X through `position`."""
    t = np.eye(4)
    t[:3, 3] = _v(position) * SCALE
    back = np.eye(4)
    back[:3, 3] = -_v(position) * SCALE
    r = np.eye(4)
    r[:3, :3] = rotation((1, 0, 0), -open_degrees)
    return t @ r @ back


def setup_scene(samples, size):
    import bpy

    bpy.ops.wm.read_factory_settings(use_empty=True)
    scene = bpy.context.scene
    scene.render.engine = "CYCLES"
    scene.cycles.device = "CPU"
    scene.cycles.samples = samples
    scene.cycles.use_denoising = True
    scene.cycles.max_bounces = 12
    scene.cycles.transmission_bounces = 12
    scene.cycles.glossy_bounces = 6
    scene.render.film_transparent = True
    scene.cycles.film_transparent_glass = True
    scene.render.resolution_x = size
    scene.render.resolution_y = size
    scene.render.image_settings.file_format = "PNG"
    scene.render.image_settings.color_mode = "RGBA"
    scene.view_settings.view_transform = "AgX"
    scene.view_settings.look = "AgX - Medium High Contrast"
    scene.view_settings.exposure = 0.0

    # A soft studio: bright above, dim below, so glossy plastic picks up a horizon line.
    world = bpy.data.worlds.new("Studio")
    scene.world = world
    if hasattr(world, "use_nodes"):
        world.use_nodes = True
    nt = world.node_tree
    nodes, links = nt.nodes, nt.links
    bg = next(n for n in nodes if n.type == "BACKGROUND")
    coord = nodes.new("ShaderNodeTexCoord")
    sep = nodes.new("ShaderNodeSeparateXYZ")
    ramp = nodes.new("ShaderNodeValToRGB")
    links.new(coord.outputs["Generated"], sep.inputs[0])
    links.new(sep.outputs["Z"], ramp.inputs["Fac"])
    ramp.color_ramp.elements[0].position = 0.35
    ramp.color_ramp.elements[0].color = (0.04, 0.04, 0.045, 1)
    ramp.color_ramp.elements[1].position = 0.75
    ramp.color_ramp.elements[1].color = (1.0, 1.0, 1.0, 1)
    links.new(ramp.outputs["Color"], bg.inputs["Color"])
    bg.inputs["Strength"].default_value = 0.45

    def area(name, location, target, size_x, size_y, energy):
        light = bpy.data.lights.new(name, "AREA")
        light.shape = "RECTANGLE"
        light.size, light.size_y, light.energy = size_x, size_y, energy
        obj = bpy.data.objects.new(name, light)
        obj.location = location
        scene.collection.objects.link(obj)
        direction = (_v(target) - _v(location))
        from mathutils import Vector
        obj.rotation_euler = Vector(direction).to_track_quat("-Z", "Y").to_euler()
        return obj

    area("Key", (-3.4, -3.2, 4.0), (0, 0, 0.2), 2.6, 2.6, 330)
    area("Fill", (3.8, -2.4, 1.6), (0, 0, 0.2), 2.4, 1.6, 55)
    area("Top", (0.4, 0.8, 5.0), (0, 0, 0), 3.6, 1.0, 120)
    area("Rim", (1.0, 4.2, 2.2), (0, 0, 0.3), 3.6, 0.9, 220)
    return scene


def frame_camera(scene, elevation=36.0, lens=100.0, margin=1.08):
    import bpy
    from mathutils import Vector

    corners = []
    for obj in scene.objects:
        if obj.type == "MESH":
            corners += [obj.matrix_world @ Vector(c) for c in obj.bound_box]
    lo = Vector([min(c[i] for c in corners) for i in range(3)])
    hi = Vector([max(c[i] for c in corners) for i in range(3)])
    center = (lo + hi) / 2

    cam_data = bpy.data.cameras.new("Camera")
    cam_data.lens = lens
    cam_data.sensor_width = 36
    cam = bpy.data.objects.new("Camera", cam_data)
    scene.collection.objects.link(cam)
    scene.camera = cam

    e = math.radians(elevation)
    direction = Vector((0, -math.cos(e), math.sin(e)))
    half_fov = math.atan(18 / lens)
    distance = 2.0
    for _ in range(30):
        cam.location = center + direction * distance
        cam.rotation_euler = (-direction).to_track_quat("-Z", "Y").to_euler()
        bpy.context.view_layer.update()
        inv = cam.matrix_world.inverted()
        worst = 0.0
        for c in corners:
            q = inv @ c
            worst = max(worst, abs(q.x) / -q.z, abs(q.y) / -q.z)
        distance *= worst / math.tan(half_fov) * margin
    return cam


def finish(raw_path, out_path, width=440):
    from PIL import Image

    img = Image.open(raw_path).convert("RGBA")
    alpha = np.asarray(img)[:, :, 3]
    ys, xs = np.nonzero(alpha > 24)
    pad = int(max(np.ptp(xs), np.ptp(ys)) * 0.04)
    box_ = (max(0, xs.min() - pad), max(0, ys.min() - pad), min(img.width, xs.max() + 1 + pad), min(img.height, ys.max() + 1 + pad))
    img = img.crop(box_)
    height = round(img.height * width / img.width)
    img = img.resize((width, height), Image.LANCZOS)
    img.save(out_path, optimize=True)
    print(f"Wrote {out_path} ({width} x {height})")


# ---------------------------------------------------------------------------------------------
# Shared parts. Buds are modelled as the right bud: +Z is the touch surface facing away from
# the head, +Y is up and +X points forward, toward the face. The left bud is its mirror image.
# ---------------------------------------------------------------------------------------------

NOZZLE = _v([0.42, -0.10, -1.0]) / np.linalg.norm([0.42, -0.10, -1.0])


def nozzle(start, length_, r1=3.3, r2=2.5, direction=NOZZLE):
    s = _v(start)
    return round_cone(s, s + direction * length_, r1, r2)


def ear_tip(start, direction=NOZZLE, radius=5.1):
    """A silicone bell that sits on the end of the nozzle, open at the tip."""
    s = _v(start)
    bell = round_cone(s, s + direction * 4.0, radius, radius * 0.55)
    inner = round_cone(s - direction * 2.0, s + direction * 6.0, radius * 0.30, radius * 0.30)
    return subtract(bell, inner, k=0.5)


def wing_tip(body, z_center, width=1.9, fin_at=(-3.0, 8.0, -2.0), fin_angle=42.0):
    """A silicone sleeve around the bud below the touch surface, with a fin that tucks into the ear."""
    band = intersect(body.grow(0.9), box((30, 30, width), c=(0, 0, z_center)), k=0.5)
    band = subtract(band, body.grow(0.05))
    fin = ellipsoid((1.4, 3.6, 1.2)).rotate((0, 0, 1), fin_angle).move(*fin_at)
    return union(band, fin, k=1.6)


def at(points_of_interest, radius):
    """A region test: true near any of the given points."""
    pts = [_v(p) for p in points_of_interest]

    def f(p):
        hit = np.zeros(len(p), dtype=bool)
        for q in pts:
            hit |= length(p - q) < radius
        return hit
    return f


# Cases --------------------------------------------------------------------------------------

class Case:
    def __init__(self, base, lid, hinge_at, open_degrees, size):
        self.base, self.lid = base, lid
        self.hinge_at, self.open_degrees, self.size = hinge_at, open_degrees, size


def clamshell(outer, split, wall, wells, outside, inside, lid_inside=None, lid_mat=None, leds=(), hinge_y=None,
              open_degrees=110.0, voxel=0.22, size=(50, 50, 27.8)):
    """Splits a closed case shape at z = split into a base with wells and a hollow lid."""
    gap = 0.18
    base = intersect(outer, half_space((0, 0, 1), split - gap), k=0.7)
    if wells:
        base = subtract(base, *wells, k=0.8)
    lid = intersect(outer, half_space((0, 0, -1), -(split + gap)), k=0.7)
    lid = subtract(lid, outer.grow(-wall), k=0.4)

    led = at(leds, 0.75)
    led_mat = Mat("#2A2B2E", 0.2)

    def base_regions(p):
        r = np.where(outer(p) < -0.6, 1, 0)
        return np.where(led(p), 2, r)

    def lid_regions(p):
        return np.where(outer(p) < -0.5, 1, 0)

    base_part = Part(base, [outside, inside, led_mat], base_regions, voxel)
    lid_part = Part(lid, [lid_mat or outside, lid_inside or lid_mat or inside], lid_regions, voxel)
    return Case(base_part, lid_part, (0, hinge_y, split), open_degrees, size)


def square_case(outside, inside, wells, lid_inside=None):
    w, d, h = 50.0, 50.0, 27.8
    outer = extrude(rounded_rect(w / 2, d / 2, 15.5), h / 2, 9.0)
    split = h / 2 - 10.8
    return clamshell(outer, split, 1.5, wells, outside, inside, lid_inside,
                     leds=[(0, -d / 2, split - 6.5)], hinge_y=d / 2 - 4.0, size=(w, d, h))


def pill_case(outside, inside, wells):
    w, d, h = 70.0, 38.8, 26.5
    outer = extrude(rounded_rect(w / 2, d / 2, 17.0), h / 2, 9.5)
    split = 0.8
    return clamshell(outer, split, 1.5, wells, outside, inside,
                     leds=[(0, -d / 2, split - 5.5)], hinge_y=d / 2 - 3.5, size=(w, d, h))


def blade_case(outside, inside, lid_mat, wells):
    """Buds3 family: an upright case with a clear lid; the buds stand in it stem down."""
    w, h, d = 58.9, 48.7, 24.4
    outer = extrude(rounded_rect(w / 2, h / 2, 17.0), d / 2, 8.0, axes="xzy")
    split = h / 2 - 17.5
    return clamshell(outer, split, 1.3, wells, outside, inside, lid_mat=lid_mat,
                     leds=[(0, -d / 2, split - 9.0)], hinge_y=d / 2 - 3.0, open_degrees=105.0, size=(w, d, h))


# Materials ----------------------------------------------------------------------------------

def gloss(color, metallic=0.0, roughness=0.12, coat=0.6):
    return Mat(color, roughness, metallic, coat=coat)


def matte(color, roughness=0.48):
    return Mat(color, roughness, specular=0.4)


def silicone(color):
    return Mat(color, 0.55, subsurface=0.25, specular=0.35)


GRILLE = Mat("#1B1C1F", 0.6)
MIC = Mat("#15161A", 0.35)


# Buds ---------------------------------------------------------------------------------------

def on_surface(shape, guess):
    p = _v([guess])
    for _ in range(4):
        n = _gradient(shape, p, 0.01)
        p = p - shape(p)[:, None] * n
    return p[0]


def touch_cap(profile, z=2.2, thickness=5.0, edge=2.3, dome=24.0):
    """The outer shell of a bud: a domed, rounded slab in the shape of `profile`."""
    return domed(extrude(profile, thickness / 2, edge).move(0, 0, z), dome)


def in_ear_bud(cap, finish, tip_mat, face_side, mics=(), wing_mat=None, body=((0.6, -0.4, -0.4), 7.4),
               neck=((1.3, -0.7, -4.0), 4.8), nozzle_len=4.6):
    """
    A Galaxy in-ear bud: the touch surface (`cap`) on a body that narrows into a nozzle with a
    silicone ear tip. `finish` is (touch surface, body); `face_side(p)` picks the touch surface faces.
    """
    inner = round_cone(body[0], neck[0], body[1], neck[1])
    outer = union(cap, inner, k=2.2)
    shell = union(outer, nozzle(neck[0], nozzle_len), k=1.6)
    end = _v(neck[0]) + NOZZLE * nozzle_len
    mic = at([on_surface(shell, m) for m in mics], 0.3)

    def regions(p):
        r = np.where(face_side(p), 0, 1)
        r = np.where(((p - end) @ NOZZLE) > -0.6, 2, r)
        return np.where(mic(p), 2, r)

    parts = [Part(shell, [finish[0], finish[1], MIC], regions),
             Part(ear_tip(end - NOZZLE * 1.3), [tip_mat])]
    if wing_mat is not None:
        parts.append(Part(wing_tip(inner, z_center=-1.9), [wing_mat]))
    return parts


def round_bud(finish, tip_mat, wing_mat=None):
    """Galaxy Buds and Buds+: a large, nearly round touchpad over a rounded body."""
    cap = touch_cap(ellipse(8.6, 9.2), z=2.0, thickness=5.4, edge=2.6, dome=20.0)
    return in_ear_bud(cap, finish, tip_mat, lambda p: p[:, 2] > 0.9, wing_mat=wing_mat, body=((0.5, -0.4, -0.6), 7.8))


def buds2_bud(finish, tip_mat):
    """Galaxy Buds2: a small bud with a perfectly round, domed touch surface."""
    cap = touch_cap(ellipse(8.3, 8.3), z=2.2, thickness=5.0, edge=2.4, dome=17.0)
    return in_ear_bud(cap, finish, tip_mat, lambda p: p[:, 2] > 1.2,
                      mics=[(5.9, 4.6, 3.4), (-6.2, -4.0, 2.4)], body=((0.6, -0.4, -0.4), 7.0))


PRO_FACE = [(-4.2, -3.6), (4.8, -3.0), (0.4, 4.4)]


def pro_bud(finish, tip_mat, wing_mat=None, scale=1.0, cap_line=0.25):
    """
    Buds Pro, Buds2 Pro, Buds FE and Buds Core: a rounded-triangle touch surface. The glossy part
    ends in a sloping line across the side (`cap_line` is the slope; None makes the whole cap glossy).
    """
    face = rounded(polygon([(x * scale, y * scale) for x, y in PRO_FACE]), 5.0 * scale)
    cap = touch_cap(face, z=2.2 * scale, thickness=5.0 * scale, edge=2.3 * scale, dome=30.0 * scale)
    if cap_line is None:
        side = lambda p: p[:, 2] > 0.7 * scale
    else:
        side = lambda p: p[:, 2] + cap_line * p[:, 1] > 1.9 * scale
    sc = lambda v: _v(v) * scale
    return in_ear_bud(cap, finish, tip_mat, side, mics=[sc((6.4, 5.0, 3.6)), sc((-7.0, -3.6, 2.4))],
                      wing_mat=wing_mat, body=(sc((0.6, -0.4, -0.4)), 7.4 * scale),
                      neck=(sc((1.3, -0.7, -4.0)), 4.8 * scale), nozzle_len=4.6 * scale)


def bean(grow=0.0):
    return arc(9.5, 48, 7.0 + grow).stretch(1, 1, 0.93)


def live_bud(outside, inside, wing_mat):
    """Galaxy Buds Live: a glossy kidney bean with a speaker grille and a small wing underneath."""
    shell = subtract(bean(), ellipsoid((2.4, 1.8, 0.9), (4.8, -2.4, -7.0)), k=0.4)
    grille = lambda p: length((p - _v((4.8, -2.4, -6.6))) / _v((2.6, 2.0, 1.6))) < 1.0
    inner = lambda p: p[:, 2] < -1.4 - 0.10 * p[:, 1]
    wing = ellipsoid((3.2, 2.4, 2.0), (-3.4, 0.8, -5.3))

    def regions(p):
        return np.where(grille(p), 2, np.where(inner(p), 1, 0))
    return [Part(shell, [outside, inside, GRILLE], regions), Part(subtract(wing, shell.grow(0.02)), [wing_mat])]


BLADE = [(-2.6, -2.1), (2.6, -2.1), (2.6, 0.1), (0.0, 2.5), (-2.6, 0.1)]


def stem_bud(head_mat, stem_mat, tip_mat=None, light_mat=None, grille_mat=GRILLE):
    """
    Buds3 family: a rounded head with a triangular 'blade' stem. Without `tip_mat` it is the
    open-type Buds3 with a speaker dome; with it, an in-ear bud. `light_mat` adds the Pro's
    light strip along the ridge of the blade.
    """
    head = ellipsoid((6.8, 7.8, 6.1)).rotate((0, 0, 1), -18)
    blade = extrude(polygon(BLADE), 11.0, 0.7, axes="xzy")
    r = rotation((0, 0, 1), 10) @ rotation((1, 0, 0), -8)
    t = _v((0.4, 0.0, 2.2))
    m = _v((0, -13.0, 0))
    stem = Shape(lambda p: blade((p - t) @ r - m))
    shell = union(head, stem, k=3.5)

    parts = []
    if tip_mat is None:
        shell = union(shell, ellipsoid((6.4, 6.4, 4.2), (1.8, 0.4, -4.6)), k=2.2)
        grille = lambda p: length((p - _v((2.1, 0.5, -8.8))) / _v((3.4, 3.4, 1.6))) < 1.0
    else:
        start = (1.2, 0.3, -4.0)
        shell = union(shell, nozzle(start, 4.4), k=1.8)
        end = _v(start) + NOZZLE * 4.4
        grille = lambda p: ((p - end) @ NOZZLE) > -0.6
        parts.append(Part(ear_tip(end - NOZZLE * 1.3), [tip_mat]))

    def regions(p):
        local = (p - t) @ r - m
        reg = np.where(stem(p) < head(p) - 0.3, 1, 0)
        if light_mat is not None:
            ridge = (np.abs(local[:, 0]) < 0.5) & (local[:, 2] > 1.5) & (np.abs(local[:, 1]) < 9.5)
            reg = np.where(ridge, 3, reg)
        return np.where(grille(p), 2, reg)

    mats = [head_mat, stem_mat, grille_mat] + ([light_mat] if light_mat is not None else [])
    parts.insert(0, Part(shell, mats, regions))
    return parts


# ---------------------------------------------------------------------------------------------
# The models
# ---------------------------------------------------------------------------------------------

class Model:
    def __init__(self, case, bud, layout):
        self.case, self.bud, self.layout = case, bud, layout


SQUARE_SPLIT = 27.8 / 2 - 10.8
PILL_SPLIT = 0.8
BLADE_SPLIT = 48.7 / 2 - 17.5


def wells(bud, spread, top, sink=1.6, tilt=16.0, y=0.4, clearance=0.45, face=None, up=(0, 1, 0)):
    """Cut-outs shaped like the buds themselves, as they sit in the case."""
    shape = union(*[p.shape for p in bud]).grow(clearance)
    out = []
    for s in (1, -1):
        z_dir = (s * face[0], face[1], face[2]) if face else (s * math.sin(math.radians(tilt)), 0, math.cos(math.radians(tilt)))
        f = frame((s * spread, y, top - sink), z_dir, (s * up[0], up[1], up[2]))
        out.append((shape if s > 0 else shape.mirror_x()).place(f))
    return out


def lying_in_front(case, spread=15.0, ahead=10.0, lift=9.0, face=(-0.6, -0.62, 0.5), up=(0.0, 0.2, 1.0)):
    """In-ear buds standing in front of the case, turned toward each other so the ear tips show."""
    def layout(mirror):
        s = -1.0 if mirror else 1.0
        return placement((s * spread, -case.size[1] / 2 - ahead, lift),
                         (face[0] * s, face[1], face[2]), (up[0] * s, up[1], up[2]), mirror)
    return layout


def beside_case(case, spread=None, ahead=-3.0, lift=9.0, face=(-0.55, -0.68, 0.48), up=(0.0, 0.2, 1.0)):
    """In-ear buds next to the front corners of the case, so the picture stays wide rather than tall."""
    spread = case.size[0] / 2 + 4.5 if spread is None else spread
    return lying_in_front(case, spread, ahead, lift, face, up)


def stems_in_front(case, spread=17.0, ahead=6.0, lift=7.0, face=(0.5, -0.3, 0.81), stem=(0.8, -0.55, -0.12)):
    """Stem buds lying in front of the case: stems toward the camera, ear tips pointing at each other."""
    def layout(mirror):
        s = -1.0 if mirror else 1.0
        return placement((s * spread, -case.size[1] / 2 - ahead, lift),
                         (face[0] * s, face[1], face[2]), (-stem[0] * s, -stem[1], -stem[2]), mirror)
    return layout


def galaxy_buds(color, case_color, satin_body):
    body = matte(color, 0.32) if satin_body else gloss(color)
    bud = round_bud((gloss(color), body), silicone(color), wing_mat=silicone(color))
    c = pill_case(matte(case_color, 0.36), gloss(case_color, coat=0.3), wells(bud, 16.5, PILL_SPLIT, tilt=12))
    return Model(c, bud, lying_in_front(c, spread=17.0, face=(-0.78, -0.45, 0.43)))


def model_buds():
    return galaxy_buds("#F1F1F1", "#F2F2F2", satin_body=False)


def model_buds_plus():
    return galaxy_buds("#232326", "#26272A", satin_body=True)


def model_buds_live():
    bud = live_bud(gloss("#C9A07E", metallic=0.55, roughness=0.18, coat=0.5), Mat("#B98F70", 0.45, metallic=0.25),
                   silicone("#B48D72"))
    c = square_case(Mat("#C69E7E", 0.3, metallic=0.45), gloss("#C49B7B", metallic=0.4),
                    wells(bud, 11.0, SQUARE_SPLIT, sink=2.5, tilt=8, up=(-1, 0.25, 0)))
    return Model(c, bud, beside_case(c, spread=c.size[0] / 2 + 8.0, ahead=-6.0, lift=7.5, face=(-0.25, -0.6, 0.76), up=(0.0, 0.9, 0.4)))


def square_case_for(bud, outside, inside, lid_inside=None, spread=11.0):
    return square_case(outside, inside, wells(bud, spread, SQUARE_SPLIT), lid_inside)


def model_buds_pro():
    violet = "#BDB3DA"
    bud = pro_bud((gloss(violet, metallic=0.35), matte("#B4AAD0", 0.45)), silicone("#B7AED3"))
    c = square_case_for(bud, matte("#B9AFD5", 0.4), gloss("#BBB1D8", metallic=0.25))
    return Model(c, bud, beside_case(c))


def model_buds2():
    olive = "#A3A587"
    bud = buds2_bud((gloss(olive, coat=0.8), gloss(olive, roughness=0.2)), silicone("#AEAF95"))
    c = square_case_for(bud, matte("#F0F0EE", 0.42), gloss(olive), lid_inside=gloss(olive))
    return Model(c, bud, beside_case(c))


def model_buds2_pro():
    bud = pro_bud((Mat("#3E3F44", 0.32, coat=0.2), matte("#3A3B40", 0.55)), silicone("#46474C"), scale=0.93)
    c = square_case_for(bud, matte("#404146", 0.5), matte("#3A3B3F", 0.38))
    return Model(c, bud, beside_case(c))


def model_buds_fe():
    bud = pro_bud((gloss("#F4F4F4"), matte("#EEEEEE", 0.42)), silicone("#E9E9E9"), wing_mat=silicone("#ECECEC"), cap_line=None)
    c = square_case_for(bud, matte("#F1F1F1", 0.42), gloss("#F3F3F3"))
    return Model(c, bud, beside_case(c))


def model_buds_core():
    bud = pro_bud((Mat("#222326", 0.25, coat=0.4), matte("#252629", 0.5)), silicone("#2D2E31"),
                  wing_mat=silicone("#2B2C2F"), scale=0.97, cap_line=None)
    c = square_case_for(bud, matte("#26272A", 0.45), matte("#232427", 0.32))
    return Model(c, bud, beside_case(c))


def blade_case_for(bud, outside, inside, lid):
    cut = wells(bud, 13.5, BLADE_SPLIT, sink=3.5, y=0.5, face=(0.85, -0.5, 0.0), up=(0, 0, 1))
    return blade_case(outside, inside, lid, cut)


def model_buds3():
    """Galaxy Buds3 in White: open-type buds, no ear tips."""
    bud = stem_bud(gloss("#F1F1F2"), gloss("#E9EAEC", roughness=0.18), grille_mat=Mat("#C9CACD", 0.5))
    c = blade_case_for(bud, matte("#EEEFF0", 0.38), Mat("#E4E5E7", 0.3), Mat("#F4F5F7", 0.03, clear=True))
    return Model(c, bud, stems_in_front(c))


def model_buds3_pro():
    """Galaxy Buds3 Pro in Silver, with the light strip along the blade."""
    bud = stem_bud(gloss("#C9CBCF", metallic=0.35, roughness=0.16), gloss("#B4B6BB", metallic=0.6, roughness=0.2, coat=0.3),
                   silicone("#C2C4C8"), Mat("#FFFFFF", 0.2, emission=2.5))
    c = blade_case_for(bud, Mat("#B9BBC0", 0.3, metallic=0.35), Mat("#AEB0B5", 0.3), Mat("#F4F5F7", 0.03, clear=True))
    return Model(c, bud, stems_in_front(c))


def model_buds3_fe():
    bud = stem_bud(gloss("#9A9CA1", metallic=0.1), gloss("#1C1C1F", roughness=0.15), silicone("#8E9095"))
    c = blade_case_for(bud, matte("#8D8F94", 0.4), Mat("#1E1F22", 0.35), Mat("#8E9095", 0.03, clear=True))
    return Model(c, bud, stems_in_front(c))


MODELS = {
    "galaxy-buds": model_buds,
    "galaxy-buds-plus": model_buds_plus,
    "galaxy-buds-live": model_buds_live,
    "galaxy-buds-pro": model_buds_pro,
    "galaxy-buds2": model_buds2,
    "galaxy-buds2-pro": model_buds2_pro,
    "galaxy-buds-fe": model_buds_fe,
    "galaxy-buds3": model_buds3,
    "galaxy-buds3-pro": model_buds3_pro,
    "galaxy-buds3-fe": model_buds3_fe,
    "galaxy-buds-core": model_buds_core,
}


# ---------------------------------------------------------------------------------------------

_meshes = {}


def add(part, name, matrix):
    import bpy
    key = id(part)
    if key in _meshes:
        obj = bpy.data.objects.new(name, _meshes[key])
        from mathutils import Matrix
        obj.matrix_world = Matrix([list(r) for r in matrix])
        bpy.context.scene.collection.objects.link(obj)
        return obj
    obj = add_part(part, name, matrix)
    _meshes[key] = obj.data
    return obj


def render(name, out_dir, samples, size):
    import bpy

    _meshes.clear()
    scene = setup_scene(samples, size)
    model = MODELS[name]()
    case = model.case
    stand = np.eye(4)
    stand[2, 3] = case.size[2] / 2 * SCALE
    add(case.base, "CaseBase", stand)
    add(case.lid, "CaseLid", stand @ hinge(case.hinge_at, case.open_degrees))
    bud = model.bud if isinstance(model.bud, list) else [model.bud]
    for side, mirror in (("Right", False), ("Left", True)):
        matrix = model.layout(mirror)
        for i, part in enumerate(bud):
            add(part, f"{side}{i}", matrix)

    frame_camera(scene)
    raw = os.path.join(tempfile.gettempdir(), f"{name}-raw.png")
    scene.render.filepath = raw
    bpy.ops.render.render(write_still=True)
    finish(raw, os.path.join(out_dir, f"{name}.png"))


def main():
    here = os.path.dirname(os.path.abspath(__file__))
    parser = argparse.ArgumentParser()
    parser.add_argument("models", nargs="*", default=list(MODELS))
    parser.add_argument("--samples", type=int, default=128)
    parser.add_argument("--size", type=int, default=1000)
    parser.add_argument("--out", default=os.path.join(here, "..", "src", "PodPilot.App", "Assets", "Devices"))
    args = parser.parse_args()
    for name in args.models:
        render(name, args.out, args.samples, args.size)


if __name__ == "__main__":
    main()
