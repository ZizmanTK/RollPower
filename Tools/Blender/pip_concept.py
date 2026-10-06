# Pip-6 concept model for Roll Power, built from code so it can be re-run and reviewed.
# blender -b -P pip.py -- turn <outdir>               studio turnaround + eye expressions
# blender -b -P pip.py -- game <outdir> <frame.json> [hint]   transparent render matching a game frame's camera
# blender -b -P pip.py -- export <file.fbx>
import bpy, bmesh, math, json, sys, os
from mathutils import Vector, Matrix

argv = sys.argv[sys.argv.index('--') + 1:]
MODE = argv[0]

def hexc(h, a=1.0):
    h = h.lstrip('#'); c = [int(h[i:i + 2], 16) / 255 for i in (0, 2, 4)]
    return tuple(pow(x, 2.2) for x in c) + (a,)  # sRGB -> linear

GUN = {1: ('RAILGUN', '#35E6FF'), 2: ('TWIN', '#5CFF8A'), 3: ('TRI', '#FFE14D'),
       4: ('PLASMA', '#FF3FA4'), 5: ('SCATTER', '#FF8A2A'), 6: ('MISSILE', '#FF4B3A')}
# Unity face layout (DiceModel.FaceNormals/FaceNumbers) converted to Blender axes (x, z, y).
FACES = {1: (0, 0, 1), 6: (0, 0, -1), 2: (1, 0, 0), 5: (-1, 0, 0), 3: (0, 1, 0), 4: (0, -1, 0)}

bpy.ops.wm.read_factory_settings(use_empty=True)
scene = bpy.context.scene
ROOT = bpy.data.objects.new('Pip', None); scene.collection.objects.link(ROOT)

def mat(name, base, metal=0.0, rough=0.5, emit=None, strength=0.0):
    m = bpy.data.materials.get(name)
    if m: return m
    m = bpy.data.materials.new(name); m.use_nodes = True
    b = m.node_tree.nodes['Principled BSDF']
    b.inputs['Base Color'].default_value = base
    b.inputs['Metallic'].default_value = metal
    b.inputs['Roughness'].default_value = rough
    if emit:
        b.inputs['Emission Color'].default_value = emit
        b.inputs['Emission Strength'].default_value = strength
    return m

def obj(name, mesh, material, parent=ROOT):
    o = bpy.data.objects.new(name, mesh); scene.collection.objects.link(o)
    o.parent = parent
    if material: o.data.materials.append(material)
    return o

def box(name, size, loc, material, parent=ROOT, bevel=0.0, rot=None):
    bm = bmesh.new(); bmesh.ops.create_cube(bm, size=1.0)
    for v in bm.verts: v.co = Vector((v.co.x * size[0], v.co.y * size[1], v.co.z * size[2]))
    me = bpy.data.meshes.new(name); bm.to_mesh(me); bm.free()
    o = obj(name, me, material, parent); o.location = loc
    if rot is not None: o.rotation_euler = rot
    if bevel > 0:
        md = o.modifiers.new('bevel', 'BEVEL'); md.width = bevel; md.segments = 4; md.limit_method = 'NONE'
    return o

def cyl(name, r, depth, loc, material, parent=ROOT, rot=(0, 0, 0), seg=32):
    bm = bmesh.new(); bmesh.ops.create_cone(bm, cap_ends=True, segments=seg, radius1=r, radius2=r, depth=depth)
    me = bpy.data.meshes.new(name); bm.to_mesh(me); bm.free()
    for p in me.polygons: p.use_smooth = True
    o = obj(name, me, material, parent); o.location = loc; o.rotation_euler = rot
    return o

def sphere(name, r, loc, material, parent=ROOT, scale=(1, 1, 1)):
    bm = bmesh.new(); bmesh.ops.create_uvsphere(bm, u_segments=48, v_segments=24, radius=r)
    me = bpy.data.meshes.new(name); bm.to_mesh(me); bm.free()
    for p in me.polygons: p.use_smooth = True
    o = obj(name, me, material, parent); o.location = loc; o.scale = scale
    return o

def ring(name, outer, inner, thick, material, parent):
    """Square frame lying in the parent's XY plane (used for seams and socket rims)."""
    bm = bmesh.new()
    w = (outer - inner)
    for sx, sy, cx, cy in ((outer * 2, w, 0, (outer + inner) / 2), (outer * 2, w, 0, -(outer + inner) / 2),
                           (w, inner * 2, (outer + inner) / 2, 0), (w, inner * 2, -(outer + inner) / 2, 0)):
        r = bmesh.ops.create_cube(bm, size=1.0)
        for v in r['verts']: v.co = Vector((v.co.x * sx + cx, v.co.y * sy + cy, v.co.z * thick))
    me = bpy.data.meshes.new(name); bm.to_mesh(me); bm.free()
    return obj(name, me, material, parent)

def face_frame(n):
    """Empty sitting on the face with normal n; its local Z points out of the face, local -Y toward the 'bottom' edge."""
    n = Vector(n)
    up = Vector((0, 0, 1)) if abs(n.z) < 0.5 else Vector((0, 1, 0))
    x = up.cross(n).normalized(); y = n.cross(x).normalized()
    e = bpy.data.objects.new('Face', None); scene.collection.objects.link(e); e.parent = BODY
    m = Matrix((x, y, n)).transposed().to_4x4(); m.translation = n * 0.5
    e.matrix_parent_inverse = Matrix.Identity(4); e.matrix_basis = m
    return e

# ---------------- body ----------------
M_BODY = mat('PipBody', hexc('#13304A'), metal=0.55, rough=0.32)
M_INK = mat('PipInk', hexc('#08131F'), metal=0.2, rough=0.6)
M_SEAM = mat('PipSeam', hexc('#29B6F6'), emit=hexc('#29B6F6'), strength=1.8)
M_PIP = mat('PipNumber', hexc('#DCEBF5'), emit=hexc('#DCEBF5'), strength=1.6)
BODY = bpy.data.objects.new('Body', None); scene.collection.objects.link(BODY); BODY.parent = ROOT; BODY.location = (0, 0, 0.5)
core = box('Shell', (1, 1, 1), (0, 0, 0), M_BODY, BODY, bevel=0.1)

def glyph(gun, F, material):
    """The stowed module's silhouette, so each side reads at gameplay zoom."""
    z = 0.035
    if gun == 1:
        box('G', (0.36, 0.055, 0.02), (0, 0.02, z), material, F); box('G', (0.1, 0.1, 0.02), (0, -0.09, z), material, F)
    elif gun == 2:
        for x in (-0.06, 0.06): box('G', (0.05, 0.3, 0.02), (x, 0, z), material, F)
    elif gun == 3:
        for a in (-24, 0, 24):
            box('G', (0.045, 0.26, 0.02), (math.sin(math.radians(a)) * 0.06, 0.0, z), material, F, rot=(0, 0, math.radians(a)))
    elif gun == 4:
        for i in range(18):
            a = i * math.tau / 18
            box('G', (0.04, 0.024, 0.02), (math.cos(a) * 0.105, math.sin(a) * 0.105, z), material, F, rot=(0, 0, a + math.pi / 2))
        cyl('G', 0.045, 0.02, (0, 0, z), material, F)
    elif gun == 5:
        for i in range(5):
            a = math.radians(-56 + i * 28)
            cyl('G', 0.026, 0.02, (math.sin(a) * 0.17, math.cos(a) * 0.17 - 0.1, z), material, F)
    elif gun == 6:
        for ix in range(3):
            for iy in range(2):
                cyl('G', 0.034, 0.02, ((ix - 1) * 0.085, (iy - 0.5) * 0.09, z), material, F)

FACE_OBJ = {}
for num, n in FACES.items():
    F = face_frame(n); FACE_OBJ[num] = F
    name, col = GUN[num]
    ring('Seam', 0.405, 0.385, 0.02, M_SEAM, F).location = (0, 0, 0.0)
    gm = mat('Gun%d' % num, hexc(col), emit=hexc(col), strength=1.7)
    dim = mat('Socket%d' % num, tuple(c * 0.18 for c in hexc(col)[:3]) + (1,), metal=0.3, rough=0.4)
    box('Socket', (0.56, 0.56, 0.03), (0, 0.035, 0.012), dim, F, bevel=0.012)
    ring('Rim', 0.3, 0.272, 0.03, gm, F).location = (0, 0.035, 0.02)
    glyph(num, F, gm)
    # Number pips in a row under the socket, so it still reads as a die.
    for i in range(num):
        cyl('Pip', 0.022, 0.012, ((i - (num - 1) / 2) * 0.06, -0.335, 0.004), M_PIP, F)

# ---------------- eye-pod (head) ----------------
HEAD = bpy.data.objects.new('EyePod', None); scene.collection.objects.link(HEAD); HEAD.parent = ROOT
HEAD.location = (0, 0, 1.36)
M_SHELL = mat('PodShell', hexc('#C3D2DD'), metal=0.35, rough=0.3)
M_BAND = mat('PodBand', hexc('#1A2C3C'), metal=0.6, rough=0.35)
top_name, top_col = GUN[1]
M_TOP = mat('TopGun', hexc(top_col), emit=hexc(top_col), strength=1.7)
sphere('Shell', 0.27, (0, 0, 0), M_SHELL, HEAD)
# Head band over the top joining the two cheek mounts. It runs around the eye axis, so it never crosses the eye.
bpy.ops.mesh.primitive_torus_add(major_radius=0.282, minor_radius=0.024, major_segments=64, minor_segments=12,
                                 location=(0, 0, 0), rotation=(math.radians(90), 0, 0))
band = bpy.context.active_object; band.name = 'Band'; band.data.materials.append(M_BAND); band.parent = HEAD
for p in band.data.polygons: p.use_smooth = True
# Eye: recessed dark socket on the front (+Y), with an iris that carries the expressions.
EYE = bpy.data.objects.new('Eye', None); scene.collection.objects.link(EYE); EYE.parent = HEAD
EYE.location = (0, 0.235, 0.02); EYE.rotation_euler = (math.radians(-90), 0, 0)
cyl('EyeSocket', 0.17, 0.06, (0, 0, -0.01), M_INK, EYE)
cyl('EyeRim', 0.19, 0.04, (0, 0, -0.03), M_BAND, EYE)
M_IRIS = mat('Iris', hexc('#29B6F6'), emit=hexc('#29B6F6'), strength=1.8)
M_IRIS_OFF = mat('IrisOff', hexc('#33485A'), emit=hexc('#33485A'), strength=0.4)
M_GLINT = mat('Glint', (1, 1, 1, 1), emit=(1, 1, 1, 1), strength=3.0)
IRIS = cyl('Iris', 0.1, 0.02, (0, 0, 0.022), M_IRIS, EYE)
PUPIL = cyl('Pupil', 0.04, 0.022, (0, 0, 0.026), M_INK, EYE)
GLINT = cyl('Glint', 0.016, 0.02, (0.035, -0.035, 0.03), M_GLINT, EYE)
# Happy eye: an arc made from a short curve.
cu = bpy.data.curves.new('HappyArc', 'CURVE'); cu.dimensions = '3D'; cu.bevel_depth = 0.018
sp = cu.splines.new('POLY'); pts = [(math.cos(a) * 0.075, math.sin(a) * 0.075 - 0.03, 0.03) for a in [math.radians(20 + i * 7) for i in range(21)]]
sp.points.add(len(pts) - 1)
for i, p in enumerate(pts): sp.points[i].co = (p[0], p[1], p[2], 1)
HAPPY = bpy.data.objects.new('Happy', cu); scene.collection.objects.link(HAPPY); HAPPY.parent = EYE; cu.materials.append(M_IRIS)
HAPPY.hide_render = True
# The active (top) module mounts on the pod's cheeks and aims with the eye.
for sx in (-1, 1):
    box('Cheek', (0.1, 0.2, 0.14), (sx * 0.29, 0.02, -0.02), M_BAND, HEAD, bevel=0.03)
    cyl('Barrel', 0.03, 0.34, (sx * 0.29, 0.2, -0.02), M_BAND, HEAD, rot=(math.radians(90), 0, 0))
    cyl('Muzzle', 0.034, 0.04, (sx * 0.29, 0.37, -0.02), M_TOP, HEAD, rot=(math.radians(90), 0, 0))
    box('Strip', (0.012, 0.16, 0.02), (sx * 0.345, 0.02, -0.02), M_TOP, HEAD)
# Mag-lev dock ring between the pod and the body (gun colour of the top face).
t = bpy.data.meshes.new('Dock'); bm = bmesh.new()
bmesh.ops.create_cone(bm, cap_ends=False, segments=48, radius1=0.2, radius2=0.2, depth=0.015); bm.to_mesh(t); bm.free()
d = obj('Dock', t, M_TOP); d.location = (0, 0, 1.03)
md = d.modifiers.new('solid', 'SOLIDIFY'); md.thickness = 0.03

def expression(kind):
    IRIS.hide_render = PUPIL.hide_render = GLINT.hide_render = kind in ('happy',)
    HAPPY.hide_render = kind != 'happy'
    IRIS.data.materials[0] = M_IRIS_OFF if kind == 'offline' else M_IRIS
    IRIS.scale = (1, 0.28, 1) if kind == 'squint' else (1, 1, 1)
    PUPIL.scale = (1, 0.3, 1) if kind == 'squint' else (1, 1, 1)
    off = (-0.04, -0.01, 0) if kind == 'look' else (0, 0, 0)
    for o, base in ((IRIS, 0.022), (PUPIL, 0.026)): o.location = (off[0], off[1], base)
    GLINT.location = (0.035 + off[0], -0.035 + off[1], 0.03)
    GLINT.hide_render = kind in ('happy', 'offline')

# ---------------- render setup ----------------
def setup_render(w, h, samples=96, transparent=False):
    r = scene.render; r.engine = 'CYCLES'; r.resolution_x = w; r.resolution_y = h; r.resolution_percentage = 100
    r.film_transparent = transparent; r.image_settings.file_format = 'PNG'; r.image_settings.color_mode = 'RGBA'
    c = scene.cycles; c.samples = samples; c.use_denoising = True; c.max_bounces = 6
    scene.view_settings.view_transform = 'Standard'; scene.view_settings.look = 'None'
    try:
        prefs = bpy.context.preferences.addons['cycles'].preferences
        for dev in ('OPTIX', 'CUDA', 'HIP', 'ONEAPI'):
            try:
                prefs.compute_device_type = dev; prefs.get_devices()
                if any(d.type == dev for d in prefs.devices):
                    for d in prefs.devices: d.use = d.type == dev
                    c.device = 'GPU'; print('[pip] GPU', dev); break
            except TypeError: pass
    except Exception as e: print('[pip] CPU', e)

def world(color, strength):
    w = bpy.data.worlds.new('W'); scene.world = w; w.use_nodes = True
    bg = w.node_tree.nodes['Background']; bg.inputs[0].default_value = color; bg.inputs[1].default_value = strength

def light(kind, loc, energy, color=(1, 1, 1), size=1.0, rot=None, target=(0, 0, 0.7)):
    ld = bpy.data.lights.new('L', kind); ld.energy = energy; ld.color = color
    if kind == 'AREA': ld.size = size
    if kind == 'SUN': ld.angle = math.radians(6)
    o = bpy.data.objects.new('L', ld); scene.collection.objects.link(o); o.location = loc
    if rot: o.rotation_euler = rot
    else:
        d = (Vector(target) - Vector(loc)).normalized(); o.rotation_euler = d.to_track_quat('-Z', 'Y').to_euler()
    return o

def camera(loc, target, lens=50, fov_y=None):
    cd = bpy.data.cameras.new('C'); co = bpy.data.objects.new('C', cd); scene.collection.objects.link(co); scene.camera = co
    co.location = loc
    co.rotation_euler = (Vector(target) - Vector(loc)).to_track_quat('-Z', 'Y').to_euler()
    if fov_y: cd.sensor_fit = 'VERTICAL'; cd.angle_y = math.radians(fov_y)
    else: cd.lens = lens
    return co

def floor_glow(num, strength):
    """A soft pool of the gun's colour on the floor beside a side face: shows what rolling that way gives."""
    n = Vector(FACES[num]); name, col = GUN[num]
    me = bpy.data.meshes.new('Glow'); bm = bmesh.new(); bmesh.ops.create_grid(bm, x_segments=1, y_segments=1, size=0.5); bm.to_mesh(me); bm.free()
    m = bpy.data.materials.new('Glow%d' % num); m.use_nodes = True; nt = m.node_tree; nt.nodes.clear()
    out = nt.nodes.new('ShaderNodeOutputMaterial'); mix = nt.nodes.new('ShaderNodeMixShader')
    tr = nt.nodes.new('ShaderNodeBsdfTransparent'); em = nt.nodes.new('ShaderNodeEmission')
    em.inputs[0].default_value = hexc(col); em.inputs[1].default_value = strength
    tc = nt.nodes.new('ShaderNodeTexCoord'); grad = nt.nodes.new('ShaderNodeTexGradient'); grad.gradient_type = 'SPHERICAL'
    mp = nt.nodes.new('ShaderNodeMapping'); mp.inputs['Location'].default_value = (0.5, 0.5, 0); mp.inputs['Scale'].default_value = (2.0, 2.0, 1)
    # object coords -> centred spherical falloff
    mp2 = nt.nodes.new('ShaderNodeMapping'); mp2.inputs['Scale'].default_value = (1.0, 1.0, 1)
    ramp = nt.nodes.new('ShaderNodeValToRGB'); ramp.color_ramp.elements[0].position = 0.5; ramp.color_ramp.elements[1].position = 1.0
    ramp.color_ramp.elements[1].color = (0.7, 0.7, 0.7, 1)
    nt.links.new(tc.outputs['Object'], mp2.inputs[0]); nt.links.new(mp2.outputs[0], grad.inputs[0])
    nt.links.new(grad.outputs['Fac'], ramp.inputs[0]); nt.links.new(ramp.outputs['Color'], mix.inputs[0])
    nt.links.new(tr.outputs[0], mix.inputs[1]); nt.links.new(em.outputs[0], mix.inputs[2]); nt.links.new(mix.outputs[0], out.inputs[0])
    # The far side is hidden behind Pip from the game camera, so its marker sits further out.
    dist = 1.9 if n.y > 0.5 else 1.35
    o = obj('Glow', me, m); o.location = n * dist + Vector((0, 0, 0.004)); o.scale = (1.3, 1.3, 1)
    o.visible_shadow = False
    # The module's glyph drawn in light on the floor, so colour is never the only cue.
    g = bpy.data.objects.new('FloorGlyph', None); scene.collection.objects.link(g); g.parent = ROOT
    g.location = n * dist; g.scale = (2.0, 2.0, 0.3)
    g.rotation_euler = (0, 0, math.atan2(n.y, n.x) - math.pi / 2)
    glyph(num, g, mat('Floor%d' % num, hexc(col), emit=hexc(col), strength=strength * 0.6))
    return o

def studio_floor():
    me = bpy.data.meshes.new('Floor'); bm = bmesh.new(); bmesh.ops.create_grid(bm, x_segments=1, y_segments=1, size=30); bm.to_mesh(me); bm.free()
    obj('Floor', me, mat('Floor', hexc('#0F2236'), metal=0.0, rough=0.55), None)

def save(path):
    scene.render.filepath = path; bpy.ops.render.render(write_still=True); print('[pip] wrote', path)

if MODE == 'turn':
    out = argv[1]; os.makedirs(out, exist_ok=True)
    world(hexc('#0A1726'), 0.6); studio_floor()
    for num in (2, 3, 4, 5): floor_glow(num, 1.3)
    light('AREA', (-3, -3.5, 4.5), 650, size=3)
    light('AREA', (3.5, 2.5, 2.5), 260, color=(0.55, 0.75, 1.0), size=2)
    light('AREA', (0, 4, 1.2), 160, color=(0.4, 0.7, 1.0), size=2)
    setup_render(1400, 1400, samples=96)
    expression('open')
    HEAD.rotation_euler = (0, 0, math.radians(180))  # eye toward the camera side (-Y)
    views = {'front34': (-35, 18, 5.2), 'side': (90, 12, 5.2), 'back34': (145, 22, 5.2), 'game': (0, 50, 6.0)}
    for name, (az, el, dist) in views.items():
        a, e = math.radians(az - 90), math.radians(el)
        loc = (math.cos(a) * math.cos(e) * dist, math.sin(a) * math.cos(e) * dist, math.sin(e) * dist + 0.6)
        if name == 'game': loc = (0, -math.cos(e) * dist, math.sin(e) * dist + 0.4)
        c = camera(loc, (0, 0, 0.75), lens=55)
        save(os.path.join(out, 'pip_%s.png' % name))
        bpy.data.objects.remove(c)
    # Expressions: close-up on the eye-pod.
    setup_render(700, 700, samples=64)
    for kind in ('open', 'look', 'squint', 'happy', 'offline'):
        expression(kind)
        c = camera((0, -1.9, 1.62), (0, 0, 1.36), lens=85)
        save(os.path.join(out, 'eye_%s.png' % kind)); bpy.data.objects.remove(c)

elif MODE == 'game':
    out, fj = argv[1], argv[2]; hint = len(argv) > 3 and argv[3] == 'hint'
    os.makedirs(out, exist_ok=True)
    f = json.load(open(fj))
    U = lambda v: Vector((v[0], v[2], v[1]))  # Unity (x, y, z) -> Blender (x, z, y)
    ROOT.location = U(f['diePos'])
    world(hexc('#1A3350'), 0.9)
    light('SUN', (0, 0, 10), 3.2, rot=(math.radians(35), math.radians(-20), math.radians(-30)))
    light('AREA', tuple(ROOT.location + Vector((-2.5, -2.5, 3.5))), 220, size=3, target=tuple(ROOT.location + Vector((0, 0, 0.7))))
    # Shadow catcher so Pip sits on the real deck.
    me = bpy.data.meshes.new('Catcher'); bm = bmesh.new(); bmesh.ops.create_grid(bm, x_segments=1, y_segments=1, size=12); bm.to_mesh(me); bm.free()
    cat = obj('Catcher', me, mat('Catch', (0.1, 0.2, 0.3, 1)), None); cat.location = ROOT.location; cat.is_shadow_catcher = True
    for num in (2, 3, 4, 5): floor_glow(num, 3.5 if (hint and num == 3) else 1.6)
    if hint:
        expression('look'); HEAD.rotation_euler = (0, 0, math.radians(200))
        bpy.data.materials['Gun3'].node_tree.nodes['Principled BSDF'].inputs['Emission Strength'].default_value = 12.0
    else:
        expression('open'); HEAD.rotation_euler = (0, 0, math.radians(150))
    cd = bpy.data.cameras.new('C'); co = bpy.data.objects.new('C', cd); scene.collection.objects.link(co); scene.camera = co
    fwd, up = U(f['camFwd']).normalized(), U(f['camUp']).normalized()
    right = fwd.cross(up).normalized()
    m = Matrix((right, up, -fwd)).transposed().to_4x4(); m.translation = U(f['camPos']); co.matrix_world = m
    cd.sensor_fit = 'VERTICAL'; cd.angle_y = math.radians(f['fov'])
    setup_render(1920, 1080, samples=128, transparent=True)
    tag = os.path.splitext(os.path.basename(fj))[0] + ('_hint' if hint else '')
    save(os.path.join(out, 'pip_%s.png' % tag))

elif MODE == 'export':
    bpy.ops.export_scene.fbx(filepath=argv[1], object_types={'EMPTY', 'MESH'}, apply_unit_scale=True, use_mesh_modifiers=True)
