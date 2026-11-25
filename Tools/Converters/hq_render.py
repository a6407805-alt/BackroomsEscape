"""Render a glb in its first clip's middle frame: usage python3 render.py in.glb out.png [clip]"""
import sys, os, bpy, math, mathutils
glb, out = sys.argv[1], sys.argv[2]
clip = sys.argv[3] if len(sys.argv) > 3 else None
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.gltf(filepath=glb)
arm = next((o for o in bpy.context.scene.objects if o.type == 'ARMATURE'), None)
if arm and arm.animation_data:
    acts = bpy.data.actions
    a = next((x for x in acts if clip and clip.lower() in x.name.lower()), None) or (acts[0] if len(acts) else None)
    if a:
        arm.animation_data.action = a
        if hasattr(arm.animation_data, "action_slot") and a.slots: arm.animation_data.action_slot = a.slots[0]
        f0, f1 = a.frame_range
        bpy.context.scene.frame_set(int((f0 + f1) / 2))
dg = bpy.context.evaluated_depsgraph_get()
pts = []
for o in bpy.context.scene.objects:
    if o.type == 'MESH':
        e = o.evaluated_get(dg); m = e.to_mesh()
        pts += [e.matrix_world @ v.co for v in m.vertices]; e.to_mesh_clear()
mn = mathutils.Vector([min(p[i] for p in pts) for i in range(3)]); mx = mathutils.Vector([max(p[i] for p in pts) for i in range(3)])
c = (mn + mx) / 2; size = (mx - mn).length
sc = bpy.context.scene
sc.render.engine = 'CYCLES'; sc.cycles.samples = int(os.environ.get("SPP","24")); sc.cycles.device = 'CPU'
sc.render.resolution_x = int(os.environ.get("RES","700")); sc.render.resolution_y = int(os.environ.get("RES","700"))
sc.world = bpy.data.worlds.new("w"); sc.world.color = (0.05, 0.05, 0.05)
cam = bpy.data.objects.new("cam", bpy.data.cameras.new("cam")); sc.collection.objects.link(cam); sc.camera = cam
d = mathutils.Vector((0.45, float(os.environ.get("CAMY","1.0")), 0.25)).normalized() * size * 0.9
cam.location = c + d
cam.rotation_euler = (c - cam.location).to_track_quat('-Z', 'Y').to_euler()
for i, (dx, e) in enumerate([((1, -1, 1), 1500), ((-1, -0.5, 0.5), 600)]):
    l = bpy.data.objects.new(f"L{i}", bpy.data.lights.new(f"L{i}", 'AREA')); sc.collection.objects.link(l)
    l.location = c + mathutils.Vector(dx) * size; l.data.energy = e * (size ** 2) / 14; l.data.size = size
    l.rotation_euler = (c - l.location).to_track_quat('-Z', 'Y').to_euler()
sc.render.filepath = out
bpy.ops.render.render(write_still=True)
print("rendered", out, "size", round(size, 3))
