# SCP:CB model converter
`b3d2glb.py` turns an animated Blitz3D `.b3d` from SCP - Containment Breach into a skinned, animated `.glb`
(requires `pip install assimp_py numpy`). CB keeps every animation on one timeline; pass clips as frame ranges:

    python3 b3d2glb.py GFX/npcs/scp096.b3d GFX/npcs out/model.glb "Idle:1085-1412,Trigger:312-422,Run:907-935"

Frame ranges come from `AnimateNPC(n, start, end, ...)` calls in CB's `NPCs.bb`. `glbcheck.py` renders the rest pose
and the middle of every clip as point clouds (orientation/animation check); `uvtest.py` checks texture V orientation.
