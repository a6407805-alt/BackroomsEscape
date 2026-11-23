import sys, io, json, struct, numpy as np
from PIL import Image
for glb in sys.argv[1:]:
    f=open(glb,'rb').read(); jl,=struct.unpack_from('<I',f,12); G=json.loads(f[20:20+jl]); b=f[20+jl+8:]
    CT={5126:np.float32,5125:np.uint32,5123:np.uint16}; NC={'SCALAR':1,'VEC2':2,'VEC3':3,'VEC4':4}
    def acc(i):
        a=G['accessors'][i]; v=G['bufferViews'][a['bufferView']]
        arr=np.frombuffer(b,CT[a['componentType']],a['count']*NC[a['type']],v.get('byteOffset',0)); return arr.reshape(a['count'],-1)
    res=[0,0]
    for p in G['meshes'][0]['primitives']:
        m=G['materials'][p.get('material',0)]; ti=m['pbrMetallicRoughness'].get('baseColorTexture')
        if ti is None or 'TEXCOORD_0' not in p['attributes']: continue
        img=G['images'][G['textures'][ti['index']]['source']]; bv=G['bufferViews'][img['bufferView']]
        a=np.asarray(Image.open(io.BytesIO(b[bv['byteOffset']:bv['byteOffset']+bv['byteLength']])).convert('L'),dtype=float)
        uv=acc(p['attributes']['TEXCOORD_0']); tri=acc(p['indices']).reshape(-1,3)
        for k,fl in enumerate([False,True]):
            u=np.mod(uv[:,0],1); v=np.mod(uv[:,1],1)
            if fl: v=1-v
            c=a[(v*(a.shape[0]-1)).astype(int),(u*(a.shape[1]-1)).astype(int)]
            d=np.abs(c[tri[:,0]]-c[tri[:,1]])+np.abs(c[tri[:,1]]-c[tri[:,2]])
            res[k]+=d.mean()
    print(glb.split('/')[-1], 'as-exported %.2f  flipped %.2f'%tuple(res))
