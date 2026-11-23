"""Load a .glb written by b3d2glb, pose it (rest pose + middle frame of each clip) with linear-blend skinning,
and save front/side point renders so orientation, scale and animation can be checked by eye.
usage: python3 -I glbcheck.py in.glb out.png"""
import sys, json, struct
import numpy as np
import matplotlib
matplotlib.use("Agg")
import matplotlib.pyplot as plt

f = open(sys.argv[1], "rb").read()
jl, = struct.unpack_from("<I", f, 12)
G = json.loads(f[20:20 + jl])
bin_ = f[20 + jl + 8:]
CT = {5126: np.float32, 5125: np.uint32, 5123: np.uint16, 5121: np.uint8}
NC = {"SCALAR": 1, "VEC2": 2, "VEC3": 3, "VEC4": 4, "MAT4": 16}


def acc(i):
    a = G["accessors"][i]; v = G["bufferViews"][a["bufferView"]]
    n = a["count"] * NC[a["type"]]
    arr = np.frombuffer(bin_, CT[a["componentType"]], n, v.get("byteOffset", 0) + a.get("byteOffset", 0))
    return arr.reshape(a["count"], NC[a["type"]]) if NC[a["type"]] > 1 else arr


def qmat(q):
    x, y, z, w = q
    return np.array([[1 - 2 * (y * y + z * z), 2 * (x * y - z * w), 2 * (x * z + y * w)],
                     [2 * (x * y + z * w), 1 - 2 * (x * x + z * z), 2 * (y * z - x * w)],
                     [2 * (x * z - y * w), 2 * (y * z + x * w), 1 - 2 * (x * x + y * y)]])


def local(n, over):
    t = np.array(over.get("translation", n.get("translation", [0, 0, 0])))
    r = np.array(over.get("rotation", n.get("rotation", [0, 0, 0, 1])))
    s = np.array(over.get("scale", n.get("scale", [1, 1, 1])))
    m = np.eye(4); m[:3, :3] = qmat(r) * s; m[:3, 3] = t
    return m


def worlds(over):
    N = G["nodes"]; W = [None] * len(N)
    parent = {c: i for i, n in enumerate(N) for c in n.get("children", [])}
    def w(i):
        if W[i] is None:
            l = local(N[i], over.get(i, {}))
            W[i] = l if i not in parent else w(parent[i]) @ l
        return W[i]
    for i in range(len(N)): w(i)
    return W


skin = G["skins"][0]; joints = skin["joints"]
ibm = acc(skin["inverseBindMatrices"]).reshape(-1, 4, 4).transpose(0, 2, 1)


def pose(over):
    W = worlds(over)
    J = np.array([W[j] for j in joints]) @ ibm
    pts = []
    for p in G["meshes"][0]["primitives"]:
        P = acc(p["attributes"]["POSITION"]).astype(np.float64)
        Ji = acc(p["attributes"]["JOINTS_0"]).astype(int); Wt = acc(p["attributes"]["WEIGHTS_0"])
        Ph = np.c_[P, np.ones(len(P))]
        out = np.zeros((len(P), 3))
        for k in range(4):
            M = J[Ji[:, k]]
            out += Wt[:, k:k + 1] * np.einsum("nij,nj->ni", M, Ph)[:, :3]
        pts.append(out)
    return np.concatenate(pts)


def sample(anim, frac):
    over = {}
    for ch in anim["channels"]:
        s = anim["samplers"][ch["sampler"]]
        t = acc(s["input"]); v = acc(s["output"])
        k = int(round(frac * (len(t) - 1)))
        over.setdefault(ch["target"]["node"], {})[ch["target"]["path"]] = v[k].tolist()
    return over


poses = [("rest", {})] + [(a["name"], sample(a, 0.5)) for a in G.get("animations", [])]
fig, axs = plt.subplots(2, len(poses), figsize=(3 * len(poses), 6))
axs = np.array(axs).reshape(2, -1)
for c, (name, over) in enumerate(poses):
    P = pose(over)
    P = P[:: max(1, len(P) // 4000)]
    axs[0, c].scatter(P[:, 0], P[:, 1], s=0.3); axs[0, c].set_title(name + " front (x,y)"); axs[0, c].set_aspect("equal")
    axs[1, c].scatter(P[:, 2], P[:, 1], s=0.3); axs[1, c].set_title("side (z,y)"); axs[1, c].set_aspect("equal")
    if c == 0:
        print("rest bbox", P.min(0).round(3), P.max(0).round(3))
plt.tight_layout(); plt.savefig(sys.argv[2], dpi=70)
