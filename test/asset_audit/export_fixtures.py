"""Extract distinct requested chunks for portable regression tests."""
import base64, hashlib, json, pathlib, struct, sys
from audit import chunks, unpack_map

names={'GlobalLighting','PostEffectsChunk','MPPositionList','TriggerAreas','FogSettings','MissionHotSpots','WaypointsList'}
fixtures=[]; seen=set()
for root in sys.argv[2:]:
    for path in pathlib.Path(root).rglob('*.map'):
        data=unpack_map(path.read_bytes()); p=4
        n=struct.unpack_from('<i',data,p)[0]; p+=4; declarations={}
        for _ in range(n):
            size=shift=0
            while True:
                b=data[p]; p+=1; size|=(b&127)<<shift; shift+=7
                if b<128: break
            name=data[p:p+size].decode('utf-8'); p+=size
            ident=struct.unpack_from('<i',data,p)[0]; p+=4
            declarations[str(ident)]=name
        for name,version,payload in chunks(path):
            if name not in names: continue
            key=(name,version,hashlib.sha256(payload).hexdigest())
            if key in seen: continue
            seen.add(key)
            used={k:v for k,v in declarations.items() if v in {name,'MPPositionInfo'}}
            fixtures.append({'type':name,'version':version,'declarations':used,'data':base64.b64encode(payload).decode('ascii'),'source':path.name})
pathlib.Path(sys.argv[1]).write_text(json.dumps(fixtures,ensure_ascii=False,indent=2),encoding='utf-8')
print('fixtures',len(fixtures))
for name in sorted(names): print(name,sum(f['type']==name for f in fixtures))
