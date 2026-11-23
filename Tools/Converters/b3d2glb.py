"""Convert an SCP:CB animated .b3d (via assimp_py) to a skinned, animated .glb.

usage: python3 -I b3d2glb.py <in.b3d> <texture_dir> <out.glb> <clips "name:start-end,...">
CB stores every animation on one long timeline; each clip is a frame range on it.
"""
import sys, os, json, struct
import numpy as np
import assimp_py as a

FPS = 24.0  # playback rate for exported clips (CB frames are retimed in-game anyway)


def mat(t):
    return np.array(t, dtype=np.float64).reshape(4, 4)


def decompose(m):
    t = m[:3, 3].copy()
    c = m[:3, :3].copy()
    s = np.linalg.norm(c, axis=0)
    s[s == 0] = 1.0
    r = c / s
    if np.linalg.det(r) < 0:
        s[0] = -s[0]
        r[:, 0] = -r[:, 0]
    q = rot_to_quat(r)
    return t, q, s


def rot_to_quat(r):
    # returns x, y, z, w
    tr = r[0, 0] + r[1, 1] + r[2, 2]
    if tr > 0:
        S = np.sqrt(tr + 1.0) * 2
        w = 0.25 * S; x = (r[2, 1] - r[1, 2]) / S; y = (r[0, 2] - r[2, 0]) / S; z = (r[1, 0] - r[0, 1]) / S
    elif r[0, 0] > r[1, 1] and r[0, 0] > r[2, 2]:
        S = np.sqrt(1.0 + r[0, 0] - r[1, 1] - r[2, 2]) * 2
        w = (r[2, 1] - r[1, 2]) / S; x = 0.25 * S; y = (r[0, 1] + r[1, 0]) / S; z = (r[0, 2] + r[2, 0]) / S
    elif r[1, 1] > r[2, 2]:
        S = np.sqrt(1.0 + r[1, 1] - r[0, 0] - r[2, 2]) * 2
        w = (r[0, 2] - r[2, 0]) / S; x = (r[0, 1] + r[1, 0]) / S; y = 0.25 * S; z = (r[1, 2] + r[2, 1]) / S
    else:
        S = np.sqrt(1.0 + r[2, 2] - r[0, 0] - r[1, 1]) * 2
        w = (r[1, 0] - r[0, 1]) / S; x = (r[0, 2] + r[2, 0]) / S; y = (r[1, 2] + r[2, 1]) / S; z = 0.25 * S
    q = np.array([x, y, z, w])
    return q / np.linalg.norm(q)


class Glb:
    def __init__(self):
        self.bin = bytearray()
        self.g = {"asset": {"version": "2.0", "generator": "b3d2glb (SCP:CB CC BY-SA 3.0)"},
                  "buffers": [], "bufferViews": [], "accessors": [], "nodes": [], "meshes": [],
                  "materials": [], "textures": [], "images": [], "samplers": [{"magFilter": 9729, "minFilter": 9987}],
                  "skins": [], "animations": [], "scenes": [{"nodes": []}], "scene": 0}

    def view(self, data, target=None):
        while len(self.bin) % 4:
            self.bin.append(0)
        off = len(self.bin)
        self.bin += data
        v = {"buffer": 0, "byteOffset": off, "byteLength": len(data)}
        if target:
            v["target"] = target
        self.g["bufferViews"].append(v)
        return len(self.g["bufferViews"]) - 1

    def acc(self, arr, typ, comp=5126, target=None, minmax=False, normalized=False):
        arr = np.ascontiguousarray(arr)
        dt = {5126: np.float32, 5125: np.uint32, 5123: np.uint16, 5121: np.uint8}[comp]
        arr = arr.astype(dt)
        bv = self.view(arr.tobytes(), target)
        n = arr.shape[0]
        a_ = {"bufferView": bv, "componentType": comp, "count": int(n), "type": typ}
        if normalized:
            a_["normalized"] = True
        if minmax:
            flat = arr.reshape(n, -1)
            a_["min"] = [float(x) for x in flat.min(0)]
            a_["max"] = [float(x) for x in flat.max(0)]
        self.g["accessors"].append(a_)
        return len(self.g["accessors"]) - 1

    def write(self, path):
        while len(self.bin) % 4:
            self.bin.append(0)
        self.g["buffers"] = [{"byteLength": len(self.bin)}]
        for k in ["textures", "images", "skins", "animations", "materials"]:
            if not self.g[k]:
                del self.g[k]
        if "textures" not in self.g:
            self.g.pop("samplers", None)
        js = json.dumps(self.g, separators=(",", ":")).encode()
        while len(js) % 4:
            js += b" "
        out = struct.pack("<III", 0x46546C67, 2, 12 + 8 + len(js) + 8 + len(self.bin))
        out += struct.pack("<II", len(js), 0x4E4F534A) + js
        out += struct.pack("<II", len(self.bin), 0x004E4942) + bytes(self.bin)
        with open(path, "wb") as f:
            f.write(out)


def main():
    src, texdir, out, clipspec = sys.argv[1:5]
    clips = []
    for part in clipspec.split(","):
        name, rng = part.split(":")
        s_, e_ = rng.split("-")
        clips.append((name, int(float(s_)), int(float(e_))))

    sc = a.import_file(src, a.Process_Triangulate | a.Process_JoinIdenticalVertices | a.Process_LimitBoneWeights
                       | a.Process_GenSmoothNormals)
    G = Glb()

    # ---- node hierarchy
    nodes = []          # (assimp node, parent index)
    name_to_idx = {}

    def walk(n, parent):
        idx = len(nodes)
        nodes.append((n, parent))
        if n.name not in name_to_idx:
            name_to_idx[n.name] = idx
        for c in n.children:
            walk(c, idx)
    walk(sc.root_node, -1)

    world = [None] * len(nodes)
    for i, (n, p) in enumerate(nodes):
        local = mat(n.transformation)
        world[i] = local if p < 0 else world[p] @ local
        t, q, s = decompose(local)
        gn = {"name": n.name or f"node{i}"}
        if np.linalg.norm(t) > 1e-9: gn["translation"] = [float(x) for x in t]
        if abs(q[3] - 1) > 1e-9: gn["rotation"] = [float(x) for x in q]
        if np.linalg.norm(s - 1) > 1e-9: gn["scale"] = [float(x) for x in s]
        G.g["nodes"].append(gn)
    for i, (n, p) in enumerate(nodes):
        if p >= 0:
            G.g["nodes"][p].setdefault("children", []).append(i)
    G.g["scenes"][0]["nodes"].append(0)

    mesh_owner = {}
    for i, (n, p) in enumerate(nodes):
        for mi in n.mesh_indices:
            mesh_owner.setdefault(mi, i)

    # ---- materials / textures
    mat_index = {}
    for mi_, m in enumerate(sc.materials):
        gm = {"name": m.get("NAME", f"mat{mi_}"), "pbrMetallicRoughness": {"metallicFactor": 0.0, "roughnessFactor": 0.85}}
        tex = m.get("TEXTURE_BASE") or (m.get("TEXTURES", {}).get(1, [None])[0])
        if tex:
            fn = tex.replace("\\", "/").split("/")[-1]
            cand = [os.path.join(texdir, fn)] + [os.path.join(texdir, f) for f in os.listdir(texdir) if f.lower() == fn.lower()]
            path = next((c for c in cand if os.path.isfile(c)), None)
            if path:
                data = open(path, "rb").read()
                mime = "image/png" if data[:4] == b"\x89PNG" else "image/jpeg"
                bv = G.view(data)
                G.g["images"].append({"bufferView": bv, "mimeType": mime, "name": fn})
                G.g["textures"].append({"sampler": 0, "source": len(G.g["images"]) - 1})
                gm["pbrMetallicRoughness"]["baseColorTexture"] = {"index": len(G.g["textures"]) - 1}
                if fn.lower().endswith(".png"):
                    gm["alphaMode"] = "MASK"
                    gm["doubleSided"] = True
            else:
                print("  missing texture", fn)
        else:
            c = m.get("COLOR_DIFFUSE", [0.8, 0.8, 0.8])
            gm["pbrMetallicRoughness"]["baseColorFactor"] = [float(c[0]), float(c[1]), float(c[2]), 1.0]
        G.g["materials"].append(gm)
        mat_index[mi_] = len(G.g["materials"]) - 1

    # ---- joints: every bone of every mesh, plus owner nodes of unskinned meshes
    joints, ibms = [], []
    jindex = {}

    def joint_for(node_idx, ibm):
        if node_idx in jindex:
            return jindex[node_idx]
        jindex[node_idx] = len(joints)
        joints.append(node_idx)
        ibms.append(ibm)
        return jindex[node_idx]

    prims = []
    allpos = []
    for mi_, m in enumerate(sc.meshes):
        pos = np.array(m.vertices, dtype=np.float64).reshape(-1, 3)
        nv = pos.shape[0]
        nrm = np.array(m.normals, dtype=np.float64).reshape(-1, 3) if m.normals else None
        uv = None
        if m.texcoords:
            ncomp = m.num_uv_components[0] if hasattr(m, "num_uv_components") and m.num_uv_components else 3
            raw = np.array(m.texcoords[0], dtype=np.float64)
            uv = raw.reshape(nv, -1)[:, :2].copy()
            # assimp already gives top-left (glTF) UVs for B3D: no flip
        idx = np.array(m.indices, dtype=np.uint32)
        J = np.zeros((nv, 4), dtype=np.uint16)
        W = np.zeros((nv, 4), dtype=np.float32)
        cnt = np.zeros(nv, dtype=np.int32)
        if m.bones:
            for b in m.bones:
                bn = name_to_idx.get(b.node_name or b.name)
                if bn is None:
                    continue
                j = joint_for(bn, mat(b.offset_matrix))
                vids = np.array(b.weight_vertex_ids, dtype=np.int64)
                ws = np.array(b.weights, dtype=np.float32)
                for v, w in zip(vids, ws):
                    if w <= 0 or cnt[v] >= 4:
                        continue
                    J[v, cnt[v]] = j
                    W[v, cnt[v]] = w
                    cnt[v] += 1
            # vertices bound to no bone follow the mesh owner node
            owner = mesh_owner.get(mi_, 0)
            loose = cnt == 0
            if loose.any():
                j = joint_for(owner, np.linalg.inv(world[owner]))
                J[loose, 0] = j
                W[loose, 0] = 1.0
            pos_w = pos
        else:
            owner = mesh_owner.get(mi_, 0)
            j = joint_for(owner, np.eye(4))
            J[:, 0] = j
            W[:, 0] = 1.0
            pos_w = (world[owner] @ np.c_[pos, np.ones(nv)].T).T[:, :3]
        s_ = W.sum(1, keepdims=True); s_[s_ == 0] = 1
        W = W / s_
        allpos.append(pos_w)
        attrs = {"POSITION": G.acc(pos, "VEC3", target=34962, minmax=True),
                 "JOINTS_0": G.acc(J, "VEC4", comp=5123, target=34962),
                 "WEIGHTS_0": G.acc(W, "VEC4", target=34962)}
        if nrm is not None:
            n_ = nrm / np.maximum(np.linalg.norm(nrm, axis=1, keepdims=True), 1e-9)
            attrs["NORMAL"] = G.acc(n_, "VEC3", target=34962)
        if uv is not None:
            attrs["TEXCOORD_0"] = G.acc(uv, "VEC2", target=34962)
        prim = {"attributes": attrs, "indices": G.acc(idx, "SCALAR", comp=5125, target=34963)}
        if m.material_index in mat_index:
            prim["material"] = mat_index[m.material_index]
        prims.append(prim)

    G.g["meshes"].append({"name": os.path.splitext(os.path.basename(src))[0], "primitives": prims})
    ibm_arr = np.array([m_.T.reshape(16) for m_ in ibms])
    G.g["skins"].append({"joints": joints, "inverseBindMatrices": G.acc(ibm_arr, "MAT4"), "skeleton": 0})
    G.g["nodes"].append({"name": "Mesh", "mesh": 0, "skin": 0})
    G.g["nodes"][0].setdefault("children", []).append(len(G.g["nodes"]) - 1)

    # ---- animation clips sampled per frame from the single timeline
    clip_info = []
    if sc.animations:
        an = sc.animations[0]
        chans = []
        for ch in an.channels:
            ni = name_to_idx.get(ch.node_name)
            if ni is None:
                continue
            pt = np.array(ch.position_key_times); pv = np.array(ch.position_key_values).reshape(-1, 3)
            rt = np.array(ch.rotation_key_times); rv = np.array(ch.rotation_key_values).reshape(-1, 4)
            st = np.array(ch.scaling_key_times); sv = np.array(ch.scaling_key_values).reshape(-1, 3)
            chans.append((ni, pt, pv, rt, rv, st, sv))
        for name, f0, f1 in clips:
            frames = np.arange(f0, f1 + 1, dtype=np.float64) if f1 >= f0 else np.arange(f0, f1 - 1, -1, dtype=np.float64)
            times = np.arange(len(frames)) / FPS
            tacc = G.acc(times.reshape(-1, 1), "SCALAR", minmax=True)
            samplers, channels = [], []
            for ni, pt, pv, rt, rv, st, sv in chans:
                def interp(t, v):
                    return np.stack([np.interp(frames, t, v[:, k]) for k in range(v.shape[1])], 1)
                for path, t, v in (("translation", pt, pv), ("rotation", rt, rv), ("scale", st, sv)):
                    if len(t) == 0:
                        continue
                    if path == "rotation":
                        # keep quaternions in one hemisphere before blending
                        v = v.copy()
                        for k in range(1, len(v)):
                            if np.dot(v[k], v[k - 1]) < 0: v[k] = -v[k]
                        out_v = interp(t, v)
                        out_v /= np.maximum(np.linalg.norm(out_v, axis=1, keepdims=True), 1e-9)
                        typ = "VEC4"
                    else:
                        out_v = interp(t, v); typ = "VEC3"
                    samplers.append({"input": tacc, "output": G.acc(out_v, typ), "interpolation": "LINEAR"})
                    channels.append({"sampler": len(samplers) - 1, "target": {"node": ni, "path": path}})
            G.g["animations"].append({"name": name, "samplers": samplers, "channels": channels})
            clip_info.append({"name": name, "frames": [f0, f1], "seconds": round(len(frames) / FPS, 3)})

    P = np.concatenate(allpos)
    G.write(out)
    info = {"file": os.path.basename(out), "source": os.path.basename(src), "bboxMin": P.min(0).round(4).tolist(),
            "bboxMax": P.max(0).round(4).tolist(), "joints": len(joints), "clips": clip_info,
            "bytes": os.path.getsize(out)}
    print(json.dumps(info))


if __name__ == "__main__":
    main()
