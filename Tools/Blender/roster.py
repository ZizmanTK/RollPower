# Enemy, coat and gun models for Roll Power, built from code so they can be re-run and reviewed.
# Every model shows its rule: you can tell what hurts an enemy, and what a gun beats, by looking at it.
#   blender -b --factory-startup -P roster.py -- sheet <outdir>      lineup renders for review
#   blender -b --factory-startup -P roster.py -- export <modelsdir>  one FBX per model (Enemies/, Coats/, Guns/)
# Units are metres, Z up, the model faces +Y (Unity +Z). Part names start with a material key ("Steel_Hull"):
# Unity gives each part a material from that key (BlenderModels.cs), so colours stay in one place in the game.
import bpy, bmesh, math, sys, os
from mathutils import Vector, Matrix, Euler

argv = sys.argv[sys.argv.index('--') + 1:]
MODE = argv[0]

def hexc(h, a=1.0):
    h = h.lstrip('#'); c = [int(h[i:i + 2], 16) / 255 for i in (0, 2, 4)]
    return tuple(pow(x, 2.2) for x in c) + (a,)

# Material keys, the same colours the game uses (Unity overrides them by key; these are for the review renders).
PAL = {
    'Shell': ('#D8263A', 0.0, 0.45, None), 'Silver': ('#C9D1DB', 0.2, 0.4, None), 'Metal': ('#2A3039', 0.5, 0.45, None),
    'Eye': ('#FF2A3D', 0.0, 0.3, 6.0), 'Glow': ('#FF3B4E', 0.0, 0.4, 2.5), 'Steel': ('#8A939E', 0.85, 0.42, None),
    'Rivet': ('#D0D6DC', 0.9, 0.35, None), 'Tread': ('#15171B', 0.3, 0.7, None), 'Hazard': ('#F2B21E', 0.1, 0.5, None),
    'Pcb': ('#1F5E3B', 0.0, 0.5, None), 'Trace': ('#E8B84A', 0.6, 0.35, 0.8), 'Chip': ('#101318', 0.2, 0.4, None),
    'Bomb': ('#4A505C', 0.6, 0.4, None), 'BombGlow': ('#FF2A3D', 0.0, 0.4, 8.0),
    'Vine': ('#2E6B33', 0.0, 0.6, None), 'Leaf': ('#4FA34A', 0.0, 0.5, None),
    'Ice': ('#DDF4FF', 0.0, 0.08, None), 'IceGlow': ('#9FE3FF', 0.0, 0.1, 1.5), 'Shield': ('#49C8FF', 0.0, 0.3, 5.0),
    'Panel': ('#E4EAF0', 0.05, 0.4, None), 'Inset': ('#2A3442', 0.2, 0.5, None), 'Gun': ('#6A7686', 0.4, 0.45, None),
    'Fuel': ('#FF7A2A', 0.1, 0.45, None), 'Copper': ('#C7642E', 0.9, 0.35, None), 'Dish': ('#C9D1DB', 0.8, 0.25, None),
    'Slug': ('#B98CFF', 0.9, 0.2, 1.2), 'Flame': ('#FFB04A', 0.0, 0.3, 9.0),
    # obstacles
    'Rust': ('#8A4A28', 0.5, 0.6, None), 'Teal': ('#3F6B70', 0.4, 0.55, None), 'Planter': ('#3A4B52', 0.4, 0.5, None),
    'Glass': ('#BFE8E0', 0.0, 0.05, None), 'Water': ('#3FBF9A', 0.0, 0.2, 0.6), 'PipeWhite': ('#D9E3E6', 0.3, 0.4, None),
    'Ore': ('#3A4F63', 0.4, 0.6, None), 'Ingot': ('#7A828C', 0.9, 0.35, None), 'Hot': ('#FF6A1A', 0.6, 0.4, 2.5),
    'Iron': ('#2A2624', 0.7, 0.5, None), 'Slag': ('#FF7A1A', 0.0, 0.4, 2.5), 'Rack': ('#141B24', 0.4, 0.45, None),
    'Led': ('#29E0FF', 0.0, 0.3, 6.0), 'Tray': ('#3A4554', 0.6, 0.45, None),
    'CableA': ('#C8323C', 0.0, 0.5, None), 'CableB': ('#2F5FBF', 0.0, 0.5, None), 'CableY': ('#E0B030', 0.0, 0.5, None),
}
GUNCOL = {'twin': '#5CFF8A', 'tri': '#5CFF8A', 'scatter': '#5CFF8A', 'missile': '#FF4B3A', 'needler': '#FF4B3A', 'flak': '#FF4B3A',
          'rail': '#B98CFF', 'plasma': '#FF3FA4', 'mortar': '#FF3FA4', 'flamer': '#FF8A2A', 'lance': '#FFE14D'}

bpy.ops.wm.read_factory_settings(use_empty=True)
scene = bpy.context.scene
EMIT = ['#FFFFFF']  # current gun colour for 'Emit' parts

def mat(key):
    name = key if key != 'Emit' else 'Emit' + EMIT[0]
    m = bpy.data.materials.get(name)
    if m: return m
    m = bpy.data.materials.new(name); m.use_nodes = True
    b = m.node_tree.nodes['Principled BSDF']
    if key == 'Emit':
        col, metal, rough, emit = EMIT[0], 0.0, 0.3, 4.0
    else:
        col, metal, rough, emit = PAL[key]
    b.inputs['Base Color'].default_value = hexc(col); b.inputs['Metallic'].default_value = metal; b.inputs['Roughness'].default_value = rough
    if emit:
        b.inputs['Emission Color'].default_value = hexc(col); b.inputs['Emission Strength'].default_value = emit
    if key in ('Ice', 'Glass'):
        b.inputs['Transmission Weight'].default_value = 0.35 if key == 'Ice' else 0.85
    return m

class Model:
    """A model under construction: an empty root plus named parts."""
    def __init__(self, name, folder):
        self.name, self.folder = name, folder
        self.root = bpy.data.objects.new(name, None); scene.collection.objects.link(self.root)
        self.count = {}
        self.into = None  # parts go under this empty while set (moving pieces of the foremen)

    def _obj(self, key, part, me, loc, rot=(0, 0, 0), scale=(1, 1, 1), smooth=False, parent=None):
        n = self.count.get(part, 0); self.count[part] = n + 1
        o = bpy.data.objects.new('%s_%s%d' % (key, part, n), me); scene.collection.objects.link(o)
        o.parent = parent or self.into or self.root; o.location = loc; o.rotation_euler = rot; o.scale = scale
        me.materials.append(mat(key))
        if smooth:
            for p in me.polygons: p.use_smooth = True
        return o

    def box(self, key, part, size, loc, rot=(0, 0, 0), bevel=0.0):
        bm = bmesh.new(); bmesh.ops.create_cube(bm, size=1.0)
        for v in bm.verts: v.co = Vector((v.co.x * size[0], v.co.y * size[1], v.co.z * size[2]))
        me = bpy.data.meshes.new(part); bm.to_mesh(me); bm.free()
        o = self._obj(key, part, me, loc, rot)
        if bevel > 0:
            md = o.modifiers.new('bevel', 'BEVEL'); md.width = bevel; md.segments = 2; md.limit_method = 'NONE'
        return o

    def cyl(self, key, part, r, depth, loc, rot=(0, 0, 0), seg=24, r2=None, smooth=True):
        bm = bmesh.new(); bmesh.ops.create_cone(bm, cap_ends=True, segments=seg, radius1=r, radius2=r if r2 is None else r2, depth=depth)
        me = bpy.data.meshes.new(part); bm.to_mesh(me); bm.free()
        return self._obj(key, part, me, loc, rot, smooth=smooth)

    def sphere(self, key, part, r, loc, scale=(1, 1, 1), seg=20):
        bm = bmesh.new(); bmesh.ops.create_uvsphere(bm, u_segments=seg, v_segments=max(6, seg // 2), radius=r)
        me = bpy.data.meshes.new(part); bm.to_mesh(me); bm.free()
        return self._obj(key, part, me, loc, scale=scale, smooth=True)

    def ico(self, key, part, r, loc, scale=(1, 1, 1), rot=(0, 0, 0), sub=1):
        bm = bmesh.new(); bmesh.ops.create_icosphere(bm, subdivisions=sub, radius=r)
        me = bpy.data.meshes.new(part); bm.to_mesh(me); bm.free()
        return self._obj(key, part, me, loc, rot, scale)

    def torus(self, key, part, R, r, loc, rot=(0, 0, 0), seg=24, rseg=8):
        bm = bmesh.new()
        verts = []
        for i in range(seg):
            a = 2 * math.pi * i / seg
            ring = []
            for j in range(rseg):
                b = 2 * math.pi * j / rseg
                ring.append(bm.verts.new(((R + r * math.cos(b)) * math.cos(a), (R + r * math.cos(b)) * math.sin(a), r * math.sin(b))))
            verts.append(ring)
        for i in range(seg):
            for j in range(rseg):
                bm.faces.new((verts[i][j], verts[(i + 1) % seg][j], verts[(i + 1) % seg][(j + 1) % rseg], verts[i][(j + 1) % rseg]))
        me = bpy.data.meshes.new(part); bm.to_mesh(me); bm.free()
        return self._obj(key, part, me, loc, rot, smooth=True)

    def limb(self, key, part, a, b, r):
        a, b = Vector(a), Vector(b); d = b - a
        o = self.cyl(key, part, r, d.length, (a + b) / 2, seg=10)
        o.rotation_euler = d.to_track_quat('Z', 'Y').to_euler()
        return o

    def empty(self, name, loc, rot=(0, 0, 0)):
        e = bpy.data.objects.new(name, None); scene.collection.objects.link(e); e.parent = self.into or self.root; e.location = loc; e.rotation_euler = rot
        return e

    def objects(self):
        out = []
        def walk(o):
            out.append(o)
            for c in o.children: walk(c)
        walk(self.root)
        return out

MODELS = []
def model(name, folder):
    m = Model(name, folder); MODELS.append(m); return m

R = math.radians

# ======================= ENEMIES =======================

def crawler():
    # BARE: an open spider frame with its circuit board on show. Nothing protects it: any gun, and a landing crushes it.
    m = model('Crawler', 'Enemies')
    for x in (-0.19, 0.19): m.box('Metal', 'Rail', (0.06, 0.5, 0.06), (x, 0, 0.3), bevel=0.01)
    m.box('Pcb', 'Board', (0.4, 0.44, 0.025), (0, -0.02, 0.35))
    for i, (x, y) in enumerate(((-0.1, 0.08), (0.1, 0.06), (-0.08, -0.12), (0.11, -0.13))):
        m.box('Chip', 'Chip', (0.08, 0.07, 0.03), (x, y, 0.375))
    for y in (-0.16, -0.03, 0.12):
        m.box('Trace', 'Trace', (0.3, 0.012, 0.006), (0, y, 0.364))
    for x in (-0.15, 0.0, 0.15):
        m.box('Trace', 'Trace', (0.012, 0.36, 0.006), (x, -0.02, 0.364))
    m.sphere('Glow', 'Core', 0.065, (0, -0.02, 0.4))
    m.box('Shell', 'Head', (0.3, 0.13, 0.14), (0, 0.26, 0.33), bevel=0.025)
    m.box('Eye', 'Visor', (0.2, 0.02, 0.04), (0, 0.33, 0.34))
    m.box('Silver', 'Brow', (0.24, 0.1, 0.03), (0, 0.25, 0.41), bevel=0.01)
    for i in range(4):
        a = R(45 + i * 90)
        d = Vector((math.sin(a), math.cos(a), 0))
        hip, knee, foot = d * 0.2 + Vector((0, 0, 0.32)), d * 0.46 + Vector((0, 0, 0.52)), d * 0.62
        m.limb('Shell', 'Thigh', hip, knee, 0.05)
        m.limb('Metal', 'Shin', knee, foot + Vector((0, 0, 0.03)), 0.035)
        m.sphere('Silver', 'Knee', 0.06, knee, seg=12)
        m.box('Metal', 'Foot', (0.1, 0.12, 0.04), foot + Vector((0, 0, 0.02)), rot=(0, 0, -a))
    return m

def drone():
    # FLYING: four ducted rotors hold it up above Pip's barrels. Flat shots pass under; seekers climb to it.
    m = model('Drone', 'Enemies')
    m.sphere('Silver', 'Body', 0.25, (0, 0, 0), scale=(1.0, 1.15, 0.8))
    m.box('Shell', 'Spine', (0.12, 0.42, 0.05), (0, -0.02, 0.19), bevel=0.02)
    m.cyl('Metal', 'Lens', 0.14, 0.08, (0, 0.27, 0.0), rot=(R(90), 0, 0))
    m.cyl('Eye', 'Pupil', 0.09, 0.01, (0, 0.315, 0.0), rot=(R(90), 0, 0))
    m.cyl('Shell', 'Belly', 0.16, 0.04, (0, 0, -0.2))
    for sx in (-1, 1):
        for sy in (-1, 1):
            c = Vector((sx * 0.4, sy * 0.36, 0.05))
            m.limb('Metal', 'Arm', Vector((sx * 0.15, sy * 0.13, 0.02)), c, 0.03)
            m.torus('Shell', 'Duct', 0.17, 0.03, c)
            m.cyl('Metal', 'Hub', 0.035, 0.06, c)
            for k in range(2):
                m.box('Glow', 'Blade', (0.3, 0.035, 0.008), c + Vector((0, 0, 0.03)), rot=(0, 0, R(45 + 90 * k + sx * 20)))
    for sx in (-1, 1): m.limb('Metal', 'Skid', Vector((sx * 0.1, 0.05, -0.18)), Vector((sx * 0.16, 0.0, -0.32)), 0.015)
    return m

def tank():
    # STEEL: thick riveted plate everywhere, sloped glacis, armour skirts over the treads. Bolts bounce off;
    # a piercing slug or an explosion cracks it.
    m = model('Tank', 'Enemies')
    for x in (-0.36, 0.36):
        m.box('Tread', 'Tread', (0.22, 1.08, 0.24), (x, 0, 0.12), bevel=0.05)
        for y in (-0.38, -0.13, 0.13, 0.38): m.cyl('Metal', 'Wheel', 0.09, 0.24, (x, y, 0.12), rot=(0, R(90), 0), seg=16)
        m.box('Steel', 'Skirt', (0.05, 1.0, 0.18), (x + (0.13 if x > 0 else -0.13), 0, 0.22), bevel=0.01)
        for y in (-0.4, -0.13, 0.13, 0.4): m.sphere('Rivet', 'Rivet', 0.022, (x + (0.16 if x > 0 else -0.16), y, 0.26), seg=8)
    m.box('Steel', 'Hull', (0.62, 0.86, 0.26), (0, -0.02, 0.38), bevel=0.03)
    m.box('Steel', 'Glacis', (0.6, 0.34, 0.05), (0, 0.48, 0.36), rot=(R(-38), 0, 0), bevel=0.01)
    for x in (-0.22, 0, 0.22): m.sphere('Rivet', 'Rivet', 0.024, (x, 0.53, 0.4), seg=8)
    m.cyl('Steel', 'Turret', 0.3, 0.22, (0, -0.06, 0.62), seg=8, smooth=False)
    m.cyl('Steel', 'TurretTop', 0.26, 0.05, (0, -0.06, 0.75), seg=8, smooth=False)
    for i in range(8):
        a = R(22.5 + i * 45)
        m.sphere('Rivet', 'Rivet', 0.022, (math.sin(a) * 0.28, -0.06 + math.cos(a) * 0.28, 0.7), seg=8)
    m.cyl('Metal', 'Barrel', 0.055, 0.6, (0, 0.42, 0.64), rot=(R(-90), 0, 0))
    m.box('Metal', 'Brake', (0.14, 0.1, 0.1), (0, 0.74, 0.64), bevel=0.01)
    m.box('Eye', 'Slit', (0.22, 0.02, 0.03), (0, 0.22, 0.66))
    m.box('Hazard', 'Mark', (0.14, 0.14, 0.006), (0.17, -0.25, 0.775))
    return m

def mite():
    # LOW: a flat tick that hugs the floor, under Pip's barrels. Guns can't reach it; Pip lands on it.
    m = model('Mite', 'Enemies')
    m.cyl('Shell', 'Disc', 0.22, 0.05, (0, 0, 0.05), seg=20)
    m.sphere('Silver', 'Dome', 0.15, (0, -0.02, 0.08), scale=(1, 1, 0.32))
    m.cyl('Hazard', 'Ring', 0.225, 0.012, (0, 0, 0.078), seg=20)
    m.sphere('Eye', 'Eye', 0.04, (0, 0.17, 0.08), scale=(1.3, 1, 0.6), seg=10)
    for i in range(6):
        a = R(30 + i * 60 + (0 if i < 3 else 0))
        d = Vector((math.sin(a), math.cos(a), 0))
        m.limb('Metal', 'Leg', d * 0.18 + Vector((0, 0, 0.04)), d * 0.32 + Vector((0, 0, 0.01)), 0.016)
    return m

def bomber():
    # BARE carrier: an open cradle on wheels holding a spiked mine. Its frame is unprotected.
    m = model('Bomber', 'Enemies')
    m.cyl('Metal', 'Base', 0.36, 0.1, (0, 0, 0.12), seg=24)
    for i in range(3):
        a = R(i * 120 + 60)
        m.cyl('Tread', 'Wheel', 0.08, 0.06, (math.sin(a) * 0.3, math.cos(a) * 0.3, 0.08), rot=(0, R(90), -a), seg=14)
    m.box('Pcb', 'Board', (0.36, 0.3, 0.02), (0, -0.02, 0.18))
    for x in (-0.09, 0.08): m.box('Chip', 'Chip', (0.08, 0.07, 0.03), (x, -0.04, 0.2))
    m.box('Trace', 'Trace', (0.3, 0.012, 0.006), (0, 0.06, 0.193))
    for i in range(4):
        a = R(45 + i * 90)
        m.limb('Metal', 'Strut', Vector((math.sin(a) * 0.28, math.cos(a) * 0.24, 0.16)), Vector((math.sin(a) * 0.12, math.cos(a) * 0.1, 0.5)), 0.022)
    m.box('Shell', 'Front', (0.3, 0.08, 0.16), (0, 0.32, 0.22), bevel=0.02)
    m.box('Eye', 'Visor', (0.18, 0.02, 0.04), (0, 0.365, 0.24))
    c = Vector((0, 0, 0.6))
    m.sphere('Bomb', 'Mine', 0.17, c)
    m.cyl('BombGlow', 'Band', 0.172, 0.03, c, seg=20)
    for d in (Vector((0, 0, 1)), Vector((1, 0, 0)), Vector((-1, 0, 0)), Vector((0, 1, 0)), Vector((0, -1, 0)),
              Vector((0.7, 0.7, 0.4)).normalized(), Vector((-0.7, -0.7, 0.4)).normalized(), Vector((0.7, -0.7, 0.4)).normalized(), Vector((-0.7, 0.7, 0.4)).normalized()):
        m.limb('Silver', 'Spike', c + d * 0.15, c + d * 0.25, 0.018)
    return m

# ======================= COATS (unit size: radius 1, height ~1; Unity scales them to the robot) =======================

def coat_vines():
    # VINES: shots pass through leaves; fire burns them.
    m = model('Vines', 'Coats')
    for k in range(5):
        a0 = k * 2 * math.pi / 5
        pts = []
        for i in range(13):
            t = i / 12
            a = a0 + t * math.pi * 1.1
            pts.append(Vector((math.cos(a) * 1.0, math.sin(a) * 1.0, 0.05 + t * 0.95)))
        for i in range(len(pts) - 1): m.limb('Vine', 'Vine', pts[i], pts[i + 1], 0.06)
        for i in (3, 7, 11):
            p = pts[i]; a = math.atan2(p.y, p.x)
            m.sphere('Leaf', 'Leaf', 0.16, p * 1.08, scale=(1.0, 0.55, 0.18), seg=10).rotation_euler = (R(20), R(-30), a)
    m.torus('Vine', 'Crown', 0.75, 0.06, (0, 0, 1.0))
    for i in range(4):
        a = i * math.pi / 2 + 0.4
        m.sphere('Leaf', 'Leaf', 0.18, (math.cos(a) * 0.7, math.sin(a) * 0.7, 1.05), scale=(1.0, 0.5, 0.15), seg=10).rotation_euler = (R(15), 0, a)
    return m

def coat_ice():
    # ICE: a crust of shards; shots skid off; fire melts it.
    m = model('Ice', 'Coats')
    import random
    rnd = random.Random(7)
    for i in range(16):
        a = i * 2 * math.pi / 16 + rnd.uniform(-0.15, 0.15)
        z = rnd.uniform(0.1, 0.85)
        p = Vector((math.cos(a), math.sin(a), z))
        m.ico('IceGlow' if i % 5 == 0 else 'Ice', 'Shard', 0.22, p, scale=(0.7, 0.7, 1.7 + rnd.random()),
              rot=(rnd.uniform(-0.5, 0.5), rnd.uniform(-0.6, 0.6), a))
    m.ico('Ice', 'Cap', 0.85, (0, 0, 0.75), scale=(1.15, 1.15, 0.45), sub=1)
    for i in range(5):
        a = i * 2 * math.pi / 5 + 0.3
        m.ico('Ice', 'Spike', 0.16, (math.cos(a) * 0.5, math.sin(a) * 0.5, 1.05), scale=(0.6, 0.6, 1.8), rot=(math.cos(a) * 0.5, math.sin(a) * 0.5, 0))
    return m

def coat_shield():
    # SHIELD: a hexagonal energy dome; it soaks every shot until a shock overloads it.
    m = model('Shield', 'Coats')
    bm = bmesh.new(); bmesh.ops.create_icosphere(bm, subdivisions=2, radius=1.15)
    bmesh.ops.delete(bm, geom=[v for v in bm.verts if v.co.z < -0.05], context='VERTS')
    me = bpy.data.meshes.new('Dome'); bm.to_mesh(me); bm.free()
    o = m._obj('Shield', 'Dome', me, (0, 0, 0.0))
    w = o.modifiers.new('wire', 'WIREFRAME'); w.thickness = 0.035; w.use_replace = True
    m.torus('Shield', 'Base', 1.15, 0.04, (0, 0, 0.0), seg=32)
    m.cyl('Metal', 'Emitter', 0.12, 0.08, (0, 0, 1.12), seg=12)
    m.sphere('Shield', 'Node', 0.07, (0, 0, 1.19), seg=10)
    return m

# ======================= GUNS (turret base at the origin; same size as the game's turrets) =======================

def gun_base(m):
    m.box('Panel', 'Plate', (0.44, 0.44, 0.05), (0, 0, -0.13), bevel=0.012)
    m.box('Inset', 'Inset', (0.3, 0.3, 0.006), (0, 0, -0.104))
    for bx in (-0.17, 0.17):
        for by in (-0.17, 0.17): m.cyl('Gun', 'Bolt', 0.022, 0.014, (bx, by, -0.1), seg=8)
    m.cyl('Inset', 'Ring', 0.1, 0.05, (0, 0, -0.07), seg=20)

def muzzles(m, pts):
    for i, p in enumerate(pts): m.empty('Muzzle_%d' % i, p)

FWD = (R(-90), 0, 0)  # cylinder axis along +Y

def radar(m, loc):
    """Lock-on sensor: seekers carry a little radar dish, so the gun reads as one that tracks fliers."""
    m.limb('Gun', 'Mast', Vector(loc) - Vector((0, 0, 0.08)), Vector(loc), 0.012)
    m.cyl('Dish', 'Dish', 0.07, 0.03, loc, rot=(R(-55), 0, 0), r2=0.02, seg=16)
    m.sphere('Emit', 'Sensor', 0.018, Vector(loc) + Vector((0, 0.025, 0.02)), seg=8)

def bolt_cells(m, x, y):
    """Bolt guns: two glowing energy cells on the back (plain energy bolts)."""
    for dx in (-0.04, 0.04): m.cyl('Emit', 'Cell', 0.022, 0.12, (x + dx, y, 0.05), seg=10)

def gun(id_):
    EMIT[0] = GUNCOL[id_]
    m = model(id_, 'Guns'); gun_base(m)
    if id_ == 'twin':
        m.sphere('Panel', 'Body', 0.15, (0, -0.02, 0.04))
        m.cyl('Inset', 'Band', 0.152, 0.03, (0, -0.02, 0.04), seg=24)
        pts = []
        for x in (-0.15, 0.15):
            m.cyl('Panel', 'Pod', 0.05, 0.3, (x, 0.1, 0.03), rot=FWD)
            m.cyl('Inset', 'Barrel', 0.03, 0.12, (x, 0.29, 0.03), rot=FWD)
            m.cyl('Emit', 'Tip', 0.034, 0.02, (x, 0.35, 0.03), rot=FWD)
            pts.append((x, 0.37, 0.03))
        bolt_cells(m, 0, -0.17); muzzles(m, pts)
    elif id_ == 'tri':
        m.box('Panel', 'Receiver', (0.26, 0.24, 0.13), (0, -0.04, 0.02), bevel=0.02)
        pts = []
        for a in (-14, 0, 14):
            d = Vector((math.sin(R(a)), math.cos(R(a)), 0))
            o = m.cyl('Inset', 'Barrel', 0.028, 0.3, d * 0.22 + Vector((0, 0, 0.02)), seg=12); o.rotation_euler = d.to_track_quat('Z', 'Y').to_euler()
            o = m.cyl('Emit', 'Tip', 0.033, 0.02, d * 0.37 + Vector((0, 0, 0.02)), seg=12); o.rotation_euler = d.to_track_quat('Z', 'Y').to_euler()
            pts.append(tuple(d * 0.39 + Vector((0, 0, 0.02))))
        bolt_cells(m, 0, -0.2); muzzles(m, pts)
    elif id_ == 'scatter':
        m.cyl('Inset', 'Drum', 0.12, 0.2, (0, -0.06, 0.06), rot=(0, R(90), 0))
        for x in (-0.1, 0.1): m.cyl('Panel', 'Cap', 0.125, 0.015, (x, -0.06, 0.06), rot=(0, R(90), 0))
        m.box('Panel', 'Muzzle', (0.4, 0.14, 0.1), (0, 0.17, 0.02), bevel=0.02)
        for i in range(5): m.cyl('Emit', 'Hole', 0.022, 0.01, ((i - 2) * 0.07, 0.245, 0.02), rot=FWD, seg=10)
        bolt_cells(m, 0, -0.2); muzzles(m, [(0, 0.26, 0.02)])
    elif id_ in ('missile', 'needler'):
        # Tubes tilted up: missiles leave climbing. A radar dish locks on.
        tilt = R(-20)
        frame = m.empty('Pod', (0, 0, 0.06), rot=(-tilt, 0, 0))
        pts = []
        if id_ == 'missile':
            for x in (-0.205, 0.205):
                o = m.box('Panel', 'Frame', (0.03, 0.34, 0.3), (x, 0.0, 0.01), bevel=0.008); o.parent = frame
            o = m.box('Panel', 'Lid', (0.44, 0.34, 0.03), (0, 0.0, 0.17), bevel=0.008); o.parent = frame
            for r_ in range(2):
                for c in range(3):
                    p = Vector(((c - 1) * 0.12, 0.0, 0.07 - r_ * 0.12))
                    o = m.cyl('Inset', 'Tube', 0.05, 0.32, p, rot=FWD, seg=14); o.parent = frame
                    o = m.cyl('Hazard', 'Band', 0.052, 0.02, p + Vector((0, 0.08, 0)), rot=FWD, seg=14); o.parent = frame
                    o = m.cyl('Emit', 'Warhead', 0.04, 0.07, p + Vector((0, 0.17, 0)), rot=FWD, r2=0.002, seg=12); o.parent = frame
                    pts.append(tuple(frame.matrix_basis @ (p + Vector((0, 0.22, 0)))))
        else:
            o = m.cyl('Panel', 'Sleeve', 0.16, 0.26, (0, 0, 0), rot=FWD, seg=20); o.parent = frame
            for i in range(8):
                a = i * 2 * math.pi / 8
                p = Vector((math.cos(a) * 0.1, 0, math.sin(a) * 0.1))
                o = m.cyl('Inset', 'Tube', 0.022, 0.32, p, rot=FWD, seg=8); o.parent = frame
                o = m.cyl('Emit', 'Needle', 0.016, 0.06, p + Vector((0, 0.18, 0)), rot=FWD, r2=0.001, seg=8); o.parent = frame
                pts.append(tuple(frame.matrix_basis @ (p + Vector((0, 0.22, 0)))))
        radar(m, (0.16, -0.18, 0.28))
        muzzles(m, pts)
    elif id_ == 'flak':
        # Two short barrels pointed up at the sky, proximity shells in a drum, a radar dish.
        m.box('Panel', 'Cradle', (0.3, 0.24, 0.14), (0, -0.05, 0.02), bevel=0.02)
        pts = []
        for x in (-0.07, 0.07):
            d = Vector((0, math.cos(R(40)), math.sin(R(40))))
            base = Vector((x, 0.02, 0.08))
            o = m.cyl('Inset', 'Barrel', 0.034, 0.3, base + d * 0.15, seg=12); o.rotation_euler = d.to_track_quat('Z', 'Y').to_euler()
            o = m.cyl('Emit', 'Tip', 0.04, 0.02, base + d * 0.3, seg=12); o.rotation_euler = d.to_track_quat('Z', 'Y').to_euler()
            pts.append(tuple(base + d * 0.32))
        radar(m, (-0.16, -0.16, 0.26))
        muzzles(m, pts)
    elif id_ == 'rail':
        # A long rail with coils and the tungsten slug visible in the breech: it punches through steel.
        m.box('Inset', 'Breech', (0.24, 0.3, 0.17), (0, -0.14, 0.02), bevel=0.02)
        for x in (-0.12, 0.12): m.box('Emit', 'Screen', (0.006, 0.16, 0.07), (x, -0.14, 0.03))
        for x in (-0.05, 0.05): m.box('Panel', 'Rail', (0.035, 0.74, 0.06), (x, 0.36, 0.02), bevel=0.008)
        for y in (0.1, 0.28, 0.46, 0.62): m.box('Emit', 'Coil', (0.16, 0.025, 0.1), (0, y, 0.02), bevel=0.01)
        m.cyl('Slug', 'Slug', 0.022, 0.14, (0, 0.03, 0.02), rot=FWD, seg=12)
        m.cyl('Slug', 'SlugTip', 0.022, 0.06, (0, 0.13, 0.02), rot=FWD, r2=0.001, seg=12)
        muzzles(m, [(0, 0.76, 0.02)])
    elif id_ in ('plasma', 'mortar'):
        # Explosives: fat barrel with hazard stripes (blast), a glowing charge.
        stripe = lambda y, r: (m.cyl('Hazard', 'Stripe', r * 1.02, 0.03, (0, y, 0.03), rot=FWD), m.cyl('Tread', 'Stripe', r * 1.02, 0.03, (0, y + 0.03, 0.03), rot=FWD))
        if id_ == 'plasma':
            m.cyl('Gun', 'Drum', 0.19, 0.22, (0, -0.1, 0.04), rot=(0, R(90), 0))
            m.cyl('Emit', 'Core', 0.08, 0.235, (0, -0.1, 0.04), rot=(0, R(90), 0))
            m.cyl('Inset', 'Barrel', 0.12, 0.36, (0, 0.18, 0.03), rot=FWD)
            stripe(0.12, 0.12); stripe(0.24, 0.12)
            m.cyl('Emit', 'Mouth', 0.1, 0.02, (0, 0.37, 0.03), rot=FWD)
            muzzles(m, [(0, 0.4, 0.03)])
        else:
            d = Vector((0, math.cos(R(50)), math.sin(R(50))))
            base = Vector((0, -0.02, 0.06))
            o = m.cyl('Inset', 'Tube', 0.11, 0.36, base + d * 0.16); o.rotation_euler = d.to_track_quat('Z', 'Y').to_euler()
            for k in (0.1, 0.2):
                o = m.cyl('Hazard', 'Stripe', 0.113, 0.03, base + d * k); o.rotation_euler = d.to_track_quat('Z', 'Y').to_euler()
            o = m.cyl('Emit', 'Mouth', 0.09, 0.02, base + d * 0.34); o.rotation_euler = d.to_track_quat('Z', 'Y').to_euler()
            for x in (-0.15, 0.15):
                m.sphere('Bomb', 'Shell', 0.05, (x, -0.14, 0.0), scale=(1, 1, 1.5), seg=12)
                m.cyl('Hazard', 'ShellBand', 0.051, 0.02, (x, -0.14, 0.0), seg=12)
            muzzles(m, [tuple(base + d * 0.38)])
    elif id_ == 'flamer':
        # Fuel tank on the back, hose, flared nozzle with a pilot flame: it burns vines and melts ice.
        m.cyl('Fuel', 'Tank', 0.1, 0.2, (0, -0.12, 0.08), seg=20)
        m.sphere('Fuel', 'TankTop', 0.1, (0, -0.12, 0.18), scale=(1, 1, 0.5), seg=16)
        m.cyl('Tread', 'TankBand', 0.102, 0.03, (0, -0.12, 0.06), seg=20)
        m.box('Hazard', 'Label', (0.006, 0.08, 0.06), (0.1, -0.12, 0.1))
        m.limb('Inset', 'Hose', Vector((0, -0.04, 0.12)), Vector((0, 0.08, 0.04)), 0.025)
        m.cyl('Gun', 'Pipe', 0.04, 0.22, (0, 0.18, 0.03), rot=FWD, seg=14)
        m.cyl('Gun', 'Nozzle', 0.045, 0.1, (0, 0.33, 0.03), rot=FWD, r2=0.08, seg=16)
        m.cyl('Flame', 'Pilot', 0.035, 0.08, (0, 0.42, 0.03), rot=FWD, r2=0.002, seg=12)
        muzzles(m, [(0, 0.4, 0.03)])
    elif id_ == 'lance':
        # A Tesla coil between two prongs, a spark at the tips: an EMP arc.
        m.box('Inset', 'Body', (0.26, 0.24, 0.12), (0, -0.1, 0.0), bevel=0.02)
        for z in range(4): m.torus('Copper', 'Coil', 0.07, 0.018, (0, -0.1, 0.08 + z * 0.04))
        m.sphere('Emit', 'Top', 0.05, (0, -0.1, 0.25), seg=12)
        for x in (-0.08, 0.08):
            m.box('Panel', 'Prong', (0.04, 0.42, 0.05), (x, 0.17, 0.02), bevel=0.01)
            for y in (0.06, 0.16, 0.26): m.torus('Copper', 'Wind', 0.035, 0.012, (x, y, 0.02), rot=FWD)
            m.sphere('Emit', 'Spark', 0.03, (x, 0.39, 0.02), seg=10)
        m.box('Emit', 'Arc', (0.13, 0.012, 0.012), (0, 0.4, 0.02), rot=(0, R(20), 0))
        muzzles(m, [(0, 0.41, 0.02)])
    return m

# ======================= OBSTACLES =======================
# Two kinds, and their shapes say what they do. A STOPPER is tall and solid, too heavy to tip over: a roll into
# it is cancelled. A LOW PIPE sits close to the floor: Pip tips over it, two faces in one go (a vault).
# Footprints match the game: stopper 0.66 x 0.66, pipe 0.48 wide and 3.0 long along +Y.

def pipe_run(m, key, r, z, length=2.9, seg=20):
    m.cyl(key, 'Pipe', r, length, (0, 0, z), rot=FWD, seg=seg)

def scrap_obstacles():
    s = model('Barrier4', 'Obstacles')  # a block of compacted scrap, strapped: far too heavy to tip
    import random
    rnd = random.Random(4)
    keys = ['Rust', 'Metal', 'Teal', 'Rust', 'Steel', 'Rust', 'Teal', 'Metal']
    for i in range(8):
        z = 0.06 + i * 0.105
        s.box(keys[i], 'Layer', (0.64 + rnd.uniform(-0.03, 0.02), 0.62 + rnd.uniform(-0.02, 0.03), 0.1), (rnd.uniform(-0.015, 0.015), rnd.uniform(-0.015, 0.015), z),
              rot=(0, 0, R(rnd.uniform(-4, 4))), bevel=0.01)
    for i in range(10):
        s.box('Metal', 'Bit', (rnd.uniform(0.05, 0.14), 0.02, rnd.uniform(0.03, 0.08)), (rnd.uniform(-0.25, 0.25), -0.325, rnd.uniform(0.1, 0.8)), rot=(0, R(rnd.uniform(-30, 30)), 0))
    for x in (-0.2, 0.2): s.box('Hazard', 'Strap', (0.06, 0.68, 0.9), (x, 0, 0.44))
    s.box('Hazard', 'Strap', (0.68, 0.06, 0.9), (0, 0, 0.44))
    s.cyl('Glow', 'Beacon', 0.05, 0.05, (0, 0, 0.92), seg=12)
    p = model('Conduit4', 'Obstacles')  # a fallen drain pipe on chocks: low enough to tip over
    pipe_run(p, 'Rust', 0.16, 0.17)
    for y in (-1.0, 0.0, 1.0): p.cyl('Hazard', 'Band', 0.165, 0.06, (0, y, 0.17), rot=FWD)
    for y in (-1.3, 1.3):
        p.box('Tread', 'Chock', (0.46, 0.14, 0.1), (0, y, 0.05), bevel=0.01)
        p.cyl('Metal', 'Flange', 0.2, 0.04, (0, y + (0.13 if y > 0 else -0.13), 0.17), rot=FWD)
    return s, p

def hydro_obstacles():
    s = model('Barrier5', 'Obstacles')  # a tall grow column: water tank and plants, bolted to the floor
    s.box('Planter', 'Base', (0.66, 0.66, 0.18), (0, 0, 0.09), bevel=0.02)
    s.cyl('Glass', 'Tank', 0.27, 0.6, (0, 0, 0.48), seg=24)
    s.cyl('Water', 'Water', 0.25, 0.42, (0, 0, 0.4), seg=24)
    s.cyl('Planter', 'Cap', 0.3, 0.06, (0, 0, 0.81), seg=24)
    for i in range(5):
        a = i * 2 * math.pi / 5
        s.sphere('Leaf', 'Leaf', 0.12, (math.cos(a) * 0.14, math.sin(a) * 0.14, 0.9), scale=(1.2, 0.6, 0.35), seg=10).rotation_euler = (R(25), 0, a)
    s.box('Glow', 'Lamp', (0.4, 0.04, 0.03), (0, -0.33, 0.16))
    p = model('Conduit5', 'Obstacles')  # a low irrigation main with drippers: step over it
    pipe_run(p, 'PipeWhite', 0.15, 0.16)
    for y in (-0.9, -0.3, 0.3, 0.9):
        p.cyl('Leaf', 'Dripper', 0.03, 0.08, (0.15, y, 0.24), seg=8)
        p.sphere('Leaf', 'Sprout', 0.07, (0.2, y + 0.1, 0.03), scale=(1, 1, 0.5), seg=8)
    for y in (-1.3, 1.3): p.box('Planter', 'Saddle', (0.42, 0.12, 0.12), (0, y, 0.06), bevel=0.01)
    return s, p

def cryo_obstacles():
    s = model('Barrier6', 'Obstacles')  # an ore pillar frozen into a column of ice
    s.box('Ore', 'Ore', (0.42, 0.42, 0.7), (0, 0, 0.35), rot=(0, 0, R(12)), bevel=0.03)
    s.ico('Ice', 'Ice', 0.42, (0, 0, 0.45), scale=(0.85, 0.85, 1.15), sub=1)
    for i in range(5):
        a = i * 2 * math.pi / 5 + 0.4
        s.ico('Ice' if i % 2 else 'IceGlow', 'Crystal', 0.12, (math.cos(a) * 0.26, math.sin(a) * 0.26, 0.78 + 0.05 * (i % 2)), scale=(0.5, 0.5, 1.9),
              rot=(math.cos(a) * 0.3, math.sin(a) * 0.3, 0))
    s.box('IceGlow', 'Vein', (0.03, 0.4, 0.03), (0.2, 0, 0.5), rot=(R(30), 0, 0))
    p = model('Conduit6', 'Obstacles')  # a low coolant line, frosted over
    pipe_run(p, 'PipeWhite', 0.15, 0.16)
    for y in (-1.0, -0.2, 0.6): p.ico('Ice', 'Frost', 0.18, (0, y, 0.2), scale=(1.0, 2.0, 0.6), sub=1)
    for y in (-1.3, 1.3): p.box('Metal', 'Saddle', (0.42, 0.12, 0.12), (0, y, 0.06), bevel=0.01)
    p.box('IceGlow', 'Valve', (0.1, 0.1, 0.1), (0.18, 0.3, 0.16), bevel=0.02)
    return s, p

def foundry_obstacles():
    s = model('Barrier7', 'Obstacles')  # a stack of steel ingots, red hot on top: solid, heavy
    for i in range(4):
        rot = (0, 0, R(90 if i % 2 else 0))
        for k in (-1, 1):
            off = Vector((0.16 * k, 0, 0)) if i % 2 == 0 else Vector((0, 0.16 * k, 0))
            s.box('Ingot' if i < 3 else 'Hot', 'Ingot', (0.3, 0.62, 0.18), off + Vector((0, 0, 0.1 + i * 0.2)), rot=rot, bevel=0.025)
    s.box('Iron', 'Pallet', (0.68, 0.68, 0.04), (0, 0, 0.02))
    p = model('Conduit7', 'Obstacles')  # a low slag channel: a trough of glowing metal on stands
    p.box('Iron', 'Trough', (0.42, 2.9, 0.18), (0, 0, 0.13), bevel=0.02)
    p.box('Slag', 'Melt', (0.3, 2.84, 0.02), (0, 0, 0.215))
    for y in (-1.2, 0, 1.2): p.box('Iron', 'Stand', (0.48, 0.1, 0.06), (0, y, 0.03))
    return s, p

def core_obstacles():
    s = model('Barrier3', 'Obstacles')  # a server rack, bolted down: the House's own hardware
    s.box('Rack', 'Cabinet', (0.62, 0.6, 0.92), (0, 0, 0.46), bevel=0.02)
    for i in range(7):
        s.box('Inset', 'Blade', (0.54, 0.02, 0.09), (0, -0.305, 0.12 + i * 0.11))
        s.box('Led' if i % 3 else 'Glow', 'Led', (0.04, 0.012, 0.02), (0.2 - 0.06 * (i % 3), -0.315, 0.12 + i * 0.11))
    s.box('Panel', 'Top', (0.64, 0.62, 0.03), (0, 0, 0.935))
    for x in (-0.25, 0.25): s.box('Metal', 'Foot', (0.1, 0.62, 0.04), (x, 0, 0.02))
    p = model('Conduit3', 'Obstacles')  # a floor cable tray: a low bundle of cables in a channel
    p.box('Tray', 'Tray', (0.46, 2.9, 0.06), (0, 0, 0.03))
    for x in (-0.16, 0.16): p.box('Tray', 'Lip', (0.03, 2.9, 0.16), (x, 0, 0.08))
    for k, (x, z) in enumerate(((-0.08, 0.1), (0.0, 0.1), (0.08, 0.1), (-0.04, 0.18), (0.04, 0.18))):
        pipe_run(p, ('CableA', 'CableB', 'CableY', 'CableB', 'CableA')[k], 0.042, z, seg=10)
    for y in (-0.9, 0.0, 0.9): p.box('Hazard', 'Clip', (0.38, 0.05, 0.05), (0, y, 0.22))
    return s, p

# ======================= FOREMEN =======================
# Moving pieces sit under named empties ("Pivot_..."); Unity rebuilds a clean pivot at each and animates it.

def compactor():
    # STEEL press on treads: riveted plating everywhere. Rammed into a wall, its top plates buckle open.
    m = model('Compactor', 'Enemies')
    for x in (-0.85, 0.85):
        m.box('Tread', 'Tread', (0.42, 2.0, 0.52), (x, 0, 0.26), bevel=0.08)
        for y in (-0.7, -0.23, 0.23, 0.7): m.cyl('Metal', 'Wheel', 0.2, 0.44, (x, y, 0.26), rot=(0, R(90), 0), seg=16)
        for i in range(6): m.box('Steel', 'Lug', (0.44, 0.12, 0.04), (x, -0.85 + i * 0.34, 0.53))
    m.box('Steel', 'Hull', (1.32, 1.6, 0.78), (0, -0.1, 0.72), bevel=0.05)
    for x in (-0.6, -0.2, 0.2, 0.6):
        for y in (-0.8, 0.6): m.sphere('Rivet', 'Rivet', 0.04, (x, y, 1.1), seg=8)
    for x in (-0.67, 0.67):
        for z in (0.45, 0.85): m.sphere('Rivet', 'Rivet', 0.04, (x, 0.6, z), seg=8)
    m.sphere('Glow', 'Core', 0.32, (0, -0.1, 1.0), scale=(1, 1.35, 0.42))
    m.box('Metal', 'Jaw', (1.9, 0.26, 1.0), (0, 0.95, 0.62), bevel=0.03)
    for i in range(5): m.box('Hazard', 'Stripe', (0.14, 0.02, 0.95), (-0.72 + i * 0.36, 1.09, 0.62), rot=(0, R(30), 0))
    for i in range(6): m.box('Steel', 'Tooth', (0.18, 0.12, 0.12), (-0.75 + i * 0.3, 1.12, 0.14), rot=(0, R(45), 0))
    m.box('Eye', 'Eye', (0.9, 0.06, 0.08), (0, 0.72, 1.2))
    for x in (-0.35, 0.35):
        m.cyl('Metal', 'Stack', 0.09, 0.7, (x, -0.75, 1.25), seg=12)
        m.cyl('Glow', 'Smoke', 0.07, 0.02, (x, -0.75, 1.61), seg=12)
    for side, name in ((-1, 'Pivot_PlateL'), (1, 'Pivot_PlateR')):
        piv = m.empty(name, (side * 0.68, -0.1, 1.12)); m.into = piv
        m.box('Hazard', 'Plate', (0.7, 1.55, 0.1), (-side * 0.34, 0, 0.04), bevel=0.02)
        for y in (-0.6, 0, 0.6): m.sphere('Rivet', 'Rivet', 0.035, (-side * 0.6, y, 0.1), seg=8)
        m.into = None
    return m

def gardener():
    # A PLANT the House grew in a pot: vines, leaves and spore pods. Shots pass through leaves; fire burns it.
    m = model('Gardener', 'Enemies')
    m.cyl('Planter', 'Pot', 1.15, 0.6, (0, 0, 0.3), r2=1.25, seg=32)
    for z in (0.18, 0.5): m.cyl('Hazard', 'Band', 1.2, 0.05, (0, 0, z), seg=32)
    m.cyl('Pcb', 'Soil', 1.15, 0.05, (0, 0, 0.6), seg=32)
    m.sphere('Vine', 'Bulb', 0.75, (0, 0, 1.05), scale=(1, 1, 0.8), seg=24)
    for i in range(7):
        a = i * 2 * math.pi / 7
        m.sphere('Leaf', 'Petal', 0.55, (math.cos(a) * 0.95, math.sin(a) * 0.95, 0.85), scale=(1.0, 0.45, 0.1), seg=14).rotation_euler = (R(-25), 0, a + math.pi / 2)
    for k in range(5):
        a0 = k * 2 * math.pi / 5 + 0.3
        pts = [Vector((math.cos(a0 + t * 1.6) * (0.8 + 0.3 * t), math.sin(a0 + t * 1.6) * (0.8 + 0.3 * t), 0.6 + math.sin(t * 3) * 0.5 + t * 0.3)) for t in [i / 8 for i in range(9)]]
        for i in range(8): m.limb('Vine', 'Tendril', pts[i], pts[i + 1], 0.07 - i * 0.006)
        m.sphere('Leaf', 'Leaf', 0.18, pts[5], scale=(1, 0.5, 0.15), seg=10)
        m.sphere('Water', 'Pod', 0.12, pts[8], seg=12)
    m.sphere('Eye', 'Core', 0.24, (0, 0.38, 1.5))
    return m

def driller():
    # Underground it's a mound of snow; up, a drill machine iced over from the mine (fire melts through).
    m = model('Driller', 'Enemies')
    mach = m.empty('Pivot_Machine', (0, 0, 0)); m.into = mach
    m.cyl('Steel', 'Body', 0.75, 0.55, (0, -0.1, 0.55), seg=24)
    m.cyl('Hazard', 'Band', 0.76, 0.06, (0, -0.1, 0.7), seg=24)
    for x in (-0.75, 0.75): m.box('Tread', 'Tread', (0.35, 1.6, 0.44), (x, -0.1, 0.22), bevel=0.06)
    for i, (r, d, y) in enumerate(((0.45, 0.25, 0.95), (0.28, 0.2, 1.3), (0.12, 0.2, 1.58))):
        m.cyl('Copper', 'Bit', r, d, (0, y, 0.6), rot=FWD, r2=r * 0.6, seg=16)
    m.cyl('Copper', 'Tip', 0.08, 0.2, (0, 1.75, 0.6), rot=FWD, r2=0.002, seg=12)
    m.box('Eye', 'Eye', (0.7, 0.06, 0.08), (0, 0.45, 1.05))
    import random
    rnd = random.Random(11)
    for i in range(14):
        a = rnd.uniform(0, 2 * math.pi)
        p = Vector((math.cos(a) * 0.7, -0.1 + math.sin(a) * 0.7, rnd.uniform(0.4, 1.0)))
        if i > 9: p = Vector((rnd.uniform(-0.3, 0.3), rnd.uniform(0.9, 1.5), rnd.uniform(0.45, 0.8)))
        m.ico('Ice' if i % 4 else 'IceGlow', 'Frost', 0.14, p, scale=(0.6, 0.6, 1.6), rot=(rnd.uniform(-0.6, 0.6), rnd.uniform(-0.6, 0.6), a))
    m.into = None
    mound = m.empty('Pivot_Mound', (0, 0, 0)); m.into = mound
    for i in range(7):
        a = i * 2 * math.pi / 7
        m.sphere('Ice', 'Snow', 0.4, (math.cos(a) * 0.45, math.sin(a) * 0.45, 0.08), scale=(1, 1, 0.45), seg=12)
    m.cyl('Copper', 'Spin', 0.12, 0.3, (0, 0, 0.2), r2=0.002, seg=10)
    m.into = None
    return m

def smelter():
    # A walking furnace: three riveted STEEL shutters (piercing or explosive), then its core shields up (shock).
    m = model('Smelter', 'Enemies')
    m.box('Iron', 'Furnace', (1.9, 1.7, 1.7), (0, 0, 0.85), bevel=0.06)
    m.box('Slag', 'Mouth', (1.0, 0.04, 0.5), (0, 0.86, 0.7))
    m.cyl('Iron', 'Chimney', 0.22, 0.6, (0.5, -0.4, 2.0), seg=16)
    m.cyl('Slag', 'Glow', 0.17, 0.02, (0.5, -0.4, 2.31), seg=16)
    m.box('Eye', 'Eye', (1.2, 0.05, 0.07), (0, 0.86, 1.4))
    for x in (-0.7, 0.7): m.box('Tread', 'Leg', (0.4, 1.8, 0.3), (x, 0, 0.15), bevel=0.05)
    m.sphere('Hot', 'Core', 0.32, (0, 0.55, 0.95), seg=16)
    for i, (loc, yaw) in enumerate((((0, 0.98, 0.95), 0), ((-1.02, 0, 0.95), 90), ((1.02, 0, 0.95), -90))):
        piv = m.empty('Pivot_Plate%d' % i, loc, rot=(0, 0, R(yaw))); m.into = piv
        m.box('Steel', 'Slab', (1.5, 0.14, 1.3), (0, 0, 0), bevel=0.03)
        m.box('Hot', 'Rim', (1.56, 0.04, 0.08), (0, 0.07, -0.62))
        for x in (-0.6, 0.6):
            for z in (-0.5, 0.5): m.sphere('Rivet', 'Rivet', 0.06, (x, 0.08, z), seg=8)
        m.box('Hazard', 'Mark', (0.5, 0.02, 0.08), (0, 0.08, 0.3))
        m.into = None
    return m

# ======================= build =======================

for f in (crawler, drone, tank, mite, bomber, coat_vines, coat_ice, coat_shield): f()
for g in ('twin', 'tri', 'scatter', 'missile', 'needler', 'flak', 'rail', 'plasma', 'mortar', 'flamer', 'lance'): gun(g)
BOSSES = [('COMPACTOR', 'steel: ram it into a wall', compactor()), ('GARDENER', 'a plant: fire', gardener()), ('DRILLER', 'underground: explosive · iced: fire', driller()), ('SMELTER', 'steel shutters, then a shield', smelter())]
OBST = [('SCRAP BAY', scrap_obstacles()), ('HYDROPONICS', hydro_obstacles()), ('CRYO MINES', cryo_obstacles()), ('FOUNDRY', foundry_obstacles()), ('THE CORE', core_obstacles())]

def setup_render(w, h, samples=64):
    r = scene.render; r.engine = 'CYCLES'; r.resolution_x = w; r.resolution_y = h
    r.image_settings.file_format = 'PNG'; scene.cycles.samples = samples; scene.cycles.use_denoising = True
    scene.view_settings.view_transform = 'Standard'
    try:
        prefs = bpy.context.preferences.addons['cycles'].preferences
        for dev in ('OPTIX', 'CUDA', 'HIP'):
            try:
                prefs.compute_device_type = dev; prefs.get_devices()
                if any(d.type == dev for d in prefs.devices):
                    for d in prefs.devices: d.use = d.type == dev
                    scene.cycles.device = 'GPU'; break
            except TypeError: pass
    except Exception as e: print('[roster] CPU', e)

def label(text, loc, size=0.13, col='#E4EEF5'):
    cu = bpy.data.curves.new('T', 'FONT'); cu.body = text; cu.size = size; cu.align_x = 'CENTER'
    o = bpy.data.objects.new('T', cu); scene.collection.objects.link(o); o.location = loc; o.rotation_euler = (R(50), 0, 0)
    m = bpy.data.materials.new('Lbl' + text); m.use_nodes = True
    b = m.node_tree.nodes['Principled BSDF']; b.inputs['Emission Color'].default_value = hexc(col); b.inputs['Emission Strength'].default_value = 1.5
    b.inputs['Base Color'].default_value = hexc(col); cu.materials.append(m)

def sheet(out):
    os.makedirs(out, exist_ok=True)
    w = bpy.data.worlds.new('W'); scene.world = w; w.use_nodes = True
    w.node_tree.nodes['Background'].inputs[0].default_value = hexc('#0A1726'); w.node_tree.nodes['Background'].inputs[1].default_value = 0.7
    me = bpy.data.meshes.new('Floor'); bm = bmesh.new(); bmesh.ops.create_grid(bm, x_segments=1, y_segments=1, size=40); bm.to_mesh(me); bm.free()
    fl = bpy.data.objects.new('Floor', me); scene.collection.objects.link(fl)
    fm = bpy.data.materials.new('F'); fm.use_nodes = True; fm.node_tree.nodes['Principled BSDF'].inputs['Base Color'].default_value = hexc('#26384A'); me.materials.append(fm)
    def lights(c):
        for loc, e, col in (((-3, -4, 6), 900, (1, 1, 1)), ((4, 2, 3), 300, (0.6, 0.8, 1.0))):
            ld = bpy.data.lights.new('L', 'AREA'); ld.energy = e; ld.size = 4; ld.color = col
            o = bpy.data.objects.new('L', ld); scene.collection.objects.link(o); o.location = Vector(loc) + c
            o.rotation_euler = (c - o.location).to_track_quat('-Z', 'Y').to_euler()
    get = {m.name: m for m in MODELS}
    for m in MODELS: m.root.location = (0, 100, 0)
    # Enemies: each with its defence and what beats it.
    row = [('Crawler', None, 'BARE', 'any gun · landing crushes'), ('Crawler', 'Vines', 'VINES', 'fire'), ('Crawler', 'Ice', 'ICE SHELL', 'fire'),
           ('Crawler', 'Shield', 'ENERGY SHIELD', 'shock'), ('Drone', None, 'FLYING', 'seeker · shock'), ('Tank', None, 'STEEL', 'piercing · explosive'),
           ('Mite', None, 'TOO LOW', 'roll onto it'), ('Bomber', None, 'BARE', 'any gun · shove its bombs')]
    import copy
    placed = []
    for i, (name, coat, d, beat) in enumerate(row):
        x = (i - (len(row) - 1) / 2) * 1.55
        src = get[name]
        dup = duplicate(src); dup.location = (x, 0, 1.2 if name == 'Drone' else 0); dup.rotation_euler = (0, 0, R(-35))
        if coat:
            cd = duplicate(get[coat]); cd.location = (x, 0, 0); cd.scale = (0.5, 0.5, 0.5)
        label(d, (x, -1.0, 0.02), 0.14); label(beat, (x, -1.3, 0.02), 0.1, '#8FB4CC')
    cam(Vector((0, -7.5, 6.0)), Vector((0, 0, 0.3)), 24); lights(Vector((0, 0, 0)))
    setup_render(2400, 900); save(os.path.join(out, 'enemies.png'))
    clear_dups()
    # Guns: grouped by damage type.
    groups = [('BOLT', 'bare robots', ['twin', 'tri', 'scatter']), ('SEEKER', 'fliers', ['missile', 'needler', 'flak']), ('PIERCE', 'steel', ['rail']),
              ('BLAST', 'steel · underground', ['plasma', 'mortar']), ('FIRE', 'vines · ice', ['flamer']), ('SHOCK', 'shields · fliers', ['lance'])]
    flat = [(g, t, b) for t, b, gs in groups for g in gs]
    for i, (g, t, b) in enumerate(flat):
        x = (i - (len(flat) - 1) / 2) * 1.0
        dup = duplicate(get[g]); dup.location = (x, 0, 0.13); dup.scale = (1.4, 1.4, 1.4); dup.rotation_euler = (0, 0, R(-62))
        label(g.upper(), (x, -0.7, 0.02), 0.1)
        label(t, (x, -0.92, 0.02), 0.09, GUNCOL[g])
        label(b, (x, -1.1, 0.02), 0.07, '#8FB4CC')
    cam(Vector((0, -7.2, 4.6)), Vector((0, 0, 0.1)), 24); lights(Vector((0, 0, 0)))
    setup_render(2600, 820); save(os.path.join(out, 'guns.png'))
    clear_dups()
    # Obstacles: per deck, the stopper (tall, solid: stops a roll) and the low pipe (tip over it: a vault).
    for i, (deck, (s, p)) in enumerate(OBST):
        x = (i - 2) * 2.3
        a = duplicate(s); a.location = (x - 0.75, 0.2, 0); a.rotation_euler = (0, 0, R(-20))
        b = duplicate(p); b.location = (x + 0.45, 0.0, 0); b.rotation_euler = (0, 0, R(-20))
        label(deck, (x, -1.75, 0.02), 0.15)
        label('stopper  ·  low pipe (vault)', (x, -2.05, 0.02), 0.09, '#8FB4CC')
    cam(Vector((0, -9.5, 7.0)), Vector((0, -0.3, 0.2)), 26); lights(Vector((0, 0, 0)))
    setup_render(2600, 900); save(os.path.join(out, 'obstacles.png'))
    clear_dups()
    for i, (name, rule, mdl) in enumerate(BOSSES):
        x = (i - 1.5) * 3.4
        a = duplicate(mdl); a.location = (x, 0, 0); a.rotation_euler = (0, 0, R(205))
        if name == 'DRILLER':
            for c in a.children:
                if c.name.startswith('Pivot_Mound'): c.location = (1.6, -1.0, 0)
        label(name, (x, -2.0, 0.02), 0.2); label(rule, (x, -2.4, 0.02), 0.13, '#8FB4CC')
    cam(Vector((0, -12.5, 9.0)), Vector((0, -0.3, 0.6)), 28); lights(Vector((0, 0, 0)))
    setup_render(2600, 1000); save(os.path.join(out, 'foremen.png'))

DUPS = []
def duplicate(m):
    def cp(o, parent):
        n = o.copy()
        if o.data: n.data = o.data
        scene.collection.objects.link(n); n.parent = parent; DUPS.append(n)
        if parent is None: n.matrix_world = Matrix.Identity(4)
        for c in o.children: cp(c, n)
        return n
    return cp(m.root, None)

def clear_dups():
    for o in DUPS: bpy.data.objects.remove(o, do_unlink=True)
    DUPS.clear()
    for o in list(scene.objects):
        if o.type in ('LIGHT', 'CAMERA', 'FONT'): bpy.data.objects.remove(o, do_unlink=True)

def cam(loc, target, lens):
    cd = bpy.data.cameras.new('C'); co = bpy.data.objects.new('C', cd); scene.collection.objects.link(co); scene.camera = co
    co.location = loc; co.rotation_euler = (target - loc).to_track_quat('-Z', 'Y').to_euler(); cd.lens = lens

def save(path):
    scene.render.filepath = path; bpy.ops.render.render(write_still=True); print('[roster] wrote', path)

def export(outdir):
    for m in MODELS:
        d = os.path.join(outdir, m.folder); os.makedirs(d, exist_ok=True)
        m.root.location = (0, 0, 0)
        # Axis markers: Unity turns and scales the model with them (1 m ahead, 1 m up), whatever the FBX conversion does.
        for name, loc in (('AxisFwd', (0, 1, 0)), ('AxisUp', (0, 0, 1))): m.empty(name, loc)
        bpy.context.view_layer.update()
        bpy.ops.object.select_all(action='DESELECT')
        for o in m.objects(): o.select_set(True)
        bpy.context.view_layer.objects.active = m.root
        path = os.path.join(d, m.name + '.fbx')
        bpy.ops.export_scene.fbx(filepath=path, use_selection=True, object_types={'EMPTY', 'MESH'},
                                 apply_scale_options='FBX_SCALE_UNITS', use_mesh_modifiers=True, bake_space_transform=True,
                                 axis_forward='Y', axis_up='Z', mesh_smooth_type='FACE', add_leaf_bones=False)
        print('[roster] exported', path)

if MODE == 'sheet': sheet(argv[1])
elif MODE == 'export': export(argv[1])
