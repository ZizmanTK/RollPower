# itch.io cover art for Roll Power: Pip-6 hovering on its field, surrounded by robots that each show their defence.
#   blender -b --factory-startup -P cover.py -- cover <out.png>
# Builds Pip with pip_concept.py and the robots with roster.py, then stages and renders them (the title is laid on
# afterwards in 2D, so the type stays crisp at any size).
import bpy, math, os, sys
from mathutils import Vector

HERE = os.path.dirname(os.path.abspath(__file__))
OUT = sys.argv[sys.argv.index('--') + 2]

g_pip = {'__file__': os.path.join(HERE, 'pip_concept.py'), '__name__': 'pip'}
exec(open(os.path.join(HERE, 'pip_concept.py'), encoding='utf-8').read(), g_pip)
g_ros = {'__file__': os.path.join(HERE, 'roster.py'), '__name__': 'roster'}
src = open(os.path.join(HERE, 'roster.py'), encoding='utf-8').read().replace('bpy.ops.wm.read_factory_settings(use_empty=True)', '')
exec(src, g_ros)

scene = bpy.context.scene
R = math.radians
hexc = g_ros['hexc']
get = {m.name: m for m in g_ros['MODELS']}
for m in g_ros['MODELS']: m.root.location = (0, 200, 0)
dup = g_ros['duplicate']

def emit_mat(name, col, strength):
    m = bpy.data.materials.new(name); m.use_nodes = True
    b = m.node_tree.nodes['Principled BSDF']
    b.inputs['Base Color'].default_value = hexc(col); b.inputs['Emission Color'].default_value = hexc(col); b.inputs['Emission Strength'].default_value = strength
    return m

def recolor(obj, key, mat):
    for o in [obj] + list(obj.children_recursive):
        if o.type == 'MESH' and o.name.startswith(key + '_'):
            o.data = o.data.copy(); o.data.materials.clear(); o.data.materials.append(mat)

def place(name, loc, yaw=200, scale=1.0, coat=None, coat_scale=0.54):
    a = dup(get[name]); a.location = loc; a.rotation_euler = (0, 0, R(yaw)); a.scale = (scale,) * 3
    if coat:
        c = dup(get[coat]); c.location = (loc[0], loc[1], 0); c.rotation_euler = (0, 0, R(yaw)); c.scale = (coat_scale * scale, coat_scale * scale, coat_scale * 0.96 * scale)
    return a

# ---- Pip, hovering, eye toward the camera ----
PIP = g_pip['ROOT']; PIP.location = (0, 0, 0.16)
g_pip['HEAD'].rotation_euler = (0, 0, R(195))
g_pip['expression']('open')
ring = bpy.data.objects.new('HoverRing', None); scene.collection.objects.link(ring)
bpy.ops.mesh.primitive_torus_add(major_radius=0.68, minor_radius=0.03, location=(0, 0, 0.015))
t = bpy.context.active_object; t.data.materials.append(emit_mat('Hover', '#29B6F6', 6))
bpy.ops.mesh.primitive_torus_add(major_radius=0.46, minor_radius=0.014, location=(0, 0, 0.015))
bpy.context.active_object.data.materials.append(emit_mat('Hover2', '#29B6F6', 4))
for num in (2, 3, 4, 5): g_pip['floor_glow'](num, 2.4)

# ---- the House's robots, each wearing its defence ----
place('Crawler', (-2.3, 1.2, 0), yaw=150, coat='Vines')
place('Crawler', (-1.25, 2.9, 0), yaw=175, coat='Ice')
place('Tank', (2.35, 1.3, 0), yaw=215, scale=1.25)
dr = place('Drone', (0.35, 3.3, 2.1), yaw=195, scale=1.2)
place('Mite', (0.95, -1.35, 0), yaw=240, scale=1.2)
place('Crawler', (3.0, 4.0, 0), yaw=210, coat='Shield')
place('Bomber', (-3.2, 4.2, 0), yaw=170)
# A blast going off behind the tank.
fire = emit_mat('CoverFire', '#FF5A0A', 6); fire2 = emit_mat('CoverFire2', '#FFB000', 7)
for i, (loc, s, m) in enumerate((((4.3, 4.9, 0.8), 0.85, fire), ((3.9, 5.2, 1.25), 0.55, fire2), ((4.7, 4.6, 0.45), 0.6, fire))):
    f = dup(get['FireballA' if i % 2 == 0 else 'FireballB']); f.location = loc; f.scale = (s,) * 3; f.rotation_euler = (i, i * 2, i * 3)
    recolor(f, 'Emit', m)
smoke = bpy.data.materials.new('SmokeLight'); smoke.use_nodes = True
smoke.node_tree.nodes['Principled BSDF'].inputs['Base Color'].default_value = hexc('#7A808A')
smoke.node_tree.nodes['Principled BSDF'].inputs['Roughness'].default_value = 1.0
for i in range(3):
    s = dup(get['Smoke']); s.location = (4.2 + i * 0.35, 5.4 + i * 0.2, 1.7 + i * 0.45); s.scale = (0.5 + i * 0.15,) * 3
    recolor(s, 'Smoke', smoke)
# The vines catching fire.
for i, (x, y, s) in enumerate(((-2.55, 1.0, 0.5), (-2.15, 1.35, 0.42), (-2.4, 1.5, 0.35), (-2.05, 0.95, 0.3))):
    f = dup(get['FlameLick']); f.location = (x, y, 0.35); f.scale = (s * 0.6, s * 0.6, s); f.rotation_euler = (R(8 * i - 10), 0, i)
    recolor(f, 'Emit', fire2 if i % 2 else fire)

# ---- deck: dark plates with glowing seams ----
me = bpy.data.meshes.new('Deck'); import bmesh
bm = bmesh.new(); bmesh.ops.create_grid(bm, x_segments=1, y_segments=1, size=30); bm.to_mesh(me); bm.free()
deck = bpy.data.objects.new('Deck', me); scene.collection.objects.link(deck)
dm = bpy.data.materials.new('DeckMat'); dm.use_nodes = True
db = dm.node_tree.nodes['Principled BSDF']; db.inputs['Base Color'].default_value = hexc('#13263A'); db.inputs['Roughness'].default_value = 0.35; db.inputs['Metallic'].default_value = 0.4
me.materials.append(dm)
seam = emit_mat('Seam', '#1F6FA8', 1.4)
for k in range(-6, 7):
    for along in (True, False):
        bpy.ops.mesh.primitive_cube_add(size=1, location=(k * 2.0, 3, 0.004) if along else (0, k * 2.0 + 3, 0.004))
        c = bpy.context.active_object; c.scale = (0.025, 30, 0.004) if along else (30, 0.025, 0.004); c.data.materials.append(seam)

# ---- light and camera ----
w = bpy.data.worlds.new('W'); scene.world = w; w.use_nodes = True
w.node_tree.nodes['Background'].inputs[0].default_value = hexc('#060E1A'); w.node_tree.nodes['Background'].inputs[1].default_value = 1.0
def light(loc, energy, col, size, target=(0, 1, 0.5)):
    ld = bpy.data.lights.new('L', 'AREA'); ld.energy = energy; ld.color = col; ld.size = size
    o = bpy.data.objects.new('L', ld); scene.collection.objects.link(o); o.location = loc
    o.rotation_euler = (Vector(target) - Vector(loc)).to_track_quat('-Z', 'Y').to_euler()
light((-3, -4, 6), 1400, (1, 0.97, 0.92), 5)
light((5, 6, 3), 900, (1.0, 0.45, 0.25), 4)       # warm rim from the blast side
light((-5, 5, 2.5), 700, (0.35, 0.7, 1.0), 4)     # cool rim
cd = bpy.data.cameras.new('C'); co = bpy.data.objects.new('C', cd); scene.collection.objects.link(co); scene.camera = co
co.location = (0.2, -6.4, 4.6); co.rotation_euler = (Vector((0.15, 1.3, 0.45)) - co.location).to_track_quat('-Z', 'Y').to_euler(); cd.lens = 34

r = scene.render; r.engine = 'CYCLES'; r.resolution_x = 1260; r.resolution_y = 1000; r.image_settings.file_format = 'PNG'
scene.cycles.samples = 128; scene.cycles.use_denoising = True; scene.view_settings.view_transform = 'Standard'; scene.view_settings.look = 'Medium High Contrast'
try:
    prefs = bpy.context.preferences.addons['cycles'].preferences
    for dev in ('OPTIX', 'CUDA', 'HIP'):
        try:
            prefs.compute_device_type = dev; prefs.get_devices()
            if any(d.type == dev for d in prefs.devices):
                for d in prefs.devices: d.use = d.type == dev
                scene.cycles.device = 'GPU'; break
        except TypeError: pass
except Exception as e: print('[cover] CPU', e)
scene.render.filepath = OUT
bpy.ops.render.render(write_still=True)
print('[cover] wrote', OUT)
