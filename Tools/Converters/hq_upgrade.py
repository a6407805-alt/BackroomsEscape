"""HQ upgrade of a game entity GLB, keeping skeleton, weights and clips.
usage: python3 upgrade.py in.glb out.glb [subdiv_levels] [detail_strength]
 - merges split vertices, applies Catmull-Clark subdivision (weights/UVs interpolate) for smoother silhouettes
 - upscales each base-colour texture to 2048 (Lanczos + unsharp) and adds a micro-contrast pass
 - derives a tangent-space detail normal map from each texture and wires it as the glTF normalTexture
 - sets a sensible roughness"""
import sys, os, bpy, numpy as np
from PIL import Image, ImageFilter
src, dst = sys.argv[1], sys.argv[2]
levels = int(sys.argv[3]) if len(sys.argv) > 3 else -1
strength = float(sys.argv[4]) if len(sys.argv) > 4 else 2.5
work = os.path.join(os.path.dirname(dst) or '.', '_tex'); os.makedirs(work, exist_ok=True)

bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.gltf(filepath=src, merge_vertices=True)
meshes = [o for o in bpy.data.objects if o.type == 'MESH' and o.users_collection[0].name != 'glTF_not_exported']
tris0 = sum(sum(len(p.vertices) - 2 for p in o.data.polygons) for o in meshes)
if levels < 0: levels = 2 if tris0 < 2500 else (1 if tris0 < 13000 else 0)

for o in meshes:
    bpy.context.view_layer.objects.active = o
    for p in o.data.polygons: p.use_smooth = True
    if levels > 0:
        sub = o.modifiers.new("HQSub", 'SUBSURF'); sub.levels = levels; sub.render_levels = levels
        sub.uv_smooth = 'PRESERVE_BOUNDARIES'; sub.boundary_smooth = 'PRESERVE_CORNERS'
        while o.modifiers[0] != sub: bpy.ops.object.modifier_move_up(modifier=sub.name)
        bpy.ops.object.modifier_apply(modifier=sub.name)
tris1 = sum(sum(len(p.vertices) - 2 for p in o.data.polygons) for o in meshes)

def upscale(img):
    path = os.path.join(work, bpy.path.clean_name(img.name))
    img.filepath_raw = path + '_src.png'; img.file_format = 'PNG'; img.save()
    im = Image.open(path + '_src.png'); hasA = im.mode in ('RGBA', 'LA') and np.asarray(im.convert('RGBA'))[..., 3].min() < 250
    im = im.convert('RGBA' if hasA else 'RGB')
    tgt = min(2048, max(im.size) * 2)
    if max(im.size) < tgt:
        sc = tgt / max(im.size); im = im.resize((round(im.size[0] * sc), round(im.size[1] * sc)), Image.LANCZOS)
    rgb = im.convert('RGB').filter(ImageFilter.UnsharpMask(radius=2.2, percent=70, threshold=2))
    if hasA: rgb.putalpha(im.getchannel('A'))
    out = path + ('_hq.png' if hasA else '_hq.jpg'); (rgb.save(out, optimize=True) if hasA else rgb.save(out, quality=90, optimize=True))
    # detail normal: height = high-passed luminance
    L = np.asarray(im.convert('L'), dtype=np.float32) / 255.0
    blur = np.asarray(Image.fromarray((L * 255).astype(np.uint8)).filter(ImageFilter.GaussianBlur(6)), dtype=np.float32) / 255.0
    H = np.asarray(Image.fromarray(((L - blur) * 0.5 + 0.5).clip(0, 1).__mul__(255).astype(np.uint8)).filter(ImageFilter.GaussianBlur(0.8)), dtype=np.float32) / 255.0
    H = H * 0.7 + L * 0.3
    dx = (np.roll(H, -1, 1) - np.roll(H, 1, 1)) * strength
    dy = (np.roll(H, -1, 0) - np.roll(H, 1, 0)) * strength
    n = np.dstack([-dx, dy, np.ones_like(H)]); n /= np.linalg.norm(n, axis=2, keepdims=True)
    nm = ((n * 0.5 + 0.5) * 255).astype(np.uint8)
    nout = path + '_n.jpg'; Image.fromarray(nm, 'RGB').save(nout, quality=92, optimize=True)
    return out, nout

done = {}
for mat in bpy.data.materials:
    if not mat.use_nodes: continue
    nt = mat.node_tree
    bsdf = next((n for n in nt.nodes if n.type == 'BSDF_PRINCIPLED'), None)
    if not bsdf: continue
    bsdf.inputs['Roughness'].default_value = 0.62
    bsdf.inputs['Metallic'].default_value = 0.0
    link = next((l for l in nt.links if l.to_node == bsdf and l.to_socket.name == 'Base Color' and l.from_node.type == 'TEX_IMAGE'), None)
    if not link or not link.from_node.image: continue
    tex = link.from_node; img = tex.image
    if img.name not in done: done[img.name] = upscale(img)
    hq, nrm = done[img.name]
    newimg = bpy.data.images.load(hq); newimg.name = img.name + '_hq'
    tex.image = newimg; tex.interpolation = 'Linear'
    if not bsdf.inputs['Normal'].is_linked:
        ni = nt.nodes.new('ShaderNodeTexImage'); ni.image = bpy.data.images.load(nrm); ni.image.colorspace_settings.name = 'Non-Color'
        nmn = nt.nodes.new('ShaderNodeNormalMap'); nmn.inputs['Strength'].default_value = 1.0
        # share the base texture's UV mapping if it has one
        if tex.inputs['Vector'].is_linked: nt.links.new(tex.inputs['Vector'].links[0].from_socket, ni.inputs['Vector'])
        nt.links.new(ni.outputs['Color'], nmn.inputs['Color']); nt.links.new(nmn.outputs['Normal'], bsdf.inputs['Normal'])

for img in list(bpy.data.images):
    if img.users == 0: bpy.data.images.remove(img)
bpy.ops.export_scene.gltf(filepath=dst, export_format='GLB', export_animations=True, export_animation_mode='ACTIONS',
                          export_skins=True, export_yup=True, export_image_format='AUTO', export_jpeg_quality=88,
                          export_tangents=False, use_selection=False, export_force_sampling=True)
print("HQDONE", os.path.basename(src), "tris", tris0, "->", tris1, "levels", levels, "textures", len(done), "bytes", os.path.getsize(src), "->", os.path.getsize(dst))
