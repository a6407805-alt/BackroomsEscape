import sys,json,struct,numpy as np
def load(f):
    b=open(f,'rb').read(); n=struct.unpack('<I',b[12:16])[0]; j=json.loads(b[20:20+n]); off=20+n; bl=struct.unpack('<I',b[off:off+4])[0]; bin_=b[off+8:off+8+bl]
    def acc(i):
        a=j['accessors'][i]; bv=j['bufferViews'][a['bufferView']]; k={'SCALAR':1,'VEC3':3,'VEC4':4,'MAT4':16}[a['type']]
        return np.frombuffer(bin_,dtype=np.float32,count=a['count']*k,offset=bv.get('byteOffset',0)+a.get('byteOffset',0)).reshape(-1,k)
    out={}
    for an in j.get('animations',[]):
        d={}
        for c in an['channels']:
            s=an['samplers'][c['sampler']]; t=acc(s['input'])[:,0]; v=acc(s['output'])
            d[(j['nodes'][c['target']['node']].get('name'),c['target']['path'])]=(t,v)
        out[an['name']]=d
    rest={nd.get('name'):nd for nd in j['nodes']}
    return out,rest
A,ra=load(sys.argv[1]); B,rb=load(sys.argv[2])
for name in A:
    worst=0;wk=None;missing=[]
    for k,(t,v) in A[name].items():
        if k not in B[name]:
            # compare against static rest value in B
            missing.append(k); continue
        t2,v2=B[name][k]
        for ki,tt in enumerate(t):
            a=v[ki]; b=np.array([np.interp(tt,t2,v2[:,i]) for i in range(v2.shape[1])])
            if k[1]=='rotation' and np.dot(a,b)<0: b=-b
            e=np.abs(a-b).max()
            if e>worst: worst,wk=e,(k,round(float(tt),2))
    print(name,'dur',round(float(max(t[-1] for t,v in A[name].values())),2),round(float(max(t[-1] for t,v in B[name].values())),2),'maxerr',round(float(worst),4),wk,'missing',missing[:3],'extra',[k for k in B[name] if k not in A[name]][:4])
