# SCP:CB model converter
`b3d2glb.py` turns an animated Blitz3D `.b3d` from SCP - Containment Breach into a skinned, animated `.glb`
(requires `pip install assimp_py numpy`). CB keeps every animation on one timeline; pass clips as frame ranges:

    python3 b3d2glb.py GFX/npcs/scp096.b3d GFX/npcs out/model.glb "Idle:1085-1412,Trigger:312-422,Run:907-935"

Frame ranges come from `AnimateNPC(n, start, end, ...)` calls in CB's `NPCs.bb`. `glbcheck.py` renders the rest pose
and the middle of every clip as point clouds (orientation/animation check); `uvtest.py` checks texture V orientation.

## Higher-quality pass (Blender)
`hq_upgrade.py` runs in Blender's Python (`pip install bpy==5.3.0`, or `blender -b -P`) and upgrades a converted `.glb`
in place of hand work: it merges split vertices, applies Catmull-Clark subdivision before the armature (weights and UVs
interpolate, so the skeleton and every clip still drive it), upscales each base texture to 2048 px with sharpening, and
derives a tangent-space detail normal map from it. Levels default to 2 under 2.5k tris, 1 under 13k, 0 above.

    python3 hq_upgrade.py in/model.glb out/model.glb [levels] [normal_strength]

`hq_compare_anim.py old.glb new.glb` checks every animation channel matches at the original keys;
`hq_render.py model.glb out.png [clip]` renders a Cycles preview of a clip's middle frame.
