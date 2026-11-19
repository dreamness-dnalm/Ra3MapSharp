"""Validate candidate layouts without touching source maps."""
import collections, json, pathlib, struct, sys
from audit import chunks

class Reader:
    def __init__(self, data): self.data, self.p = data, 0
    def take(self,n):
        if self.p+n > len(self.data): raise ValueError('truncated')
        value=self.data[self.p:self.p+n]; self.p+=n; return value
    def num(self,fmt): return struct.unpack(fmt,self.take(struct.calcsize(fmt)))[0]
    def string(self): return self.take(self.num('<H')).decode('utf-8',errors='replace')

census=json.loads(pathlib.Path(sys.argv[1]).read_text(encoding='utf-8'))
paths=set(e['path'] for s in census['assets'].values() for e in s['examples'])
# Scan the same roots again to validate every occurrence, rather than only examples.
paths=set(p for root in sys.argv[2:] for p in pathlib.Path(root).rglob('*.map'))
summary=collections.defaultdict(collections.Counter); errors=[]; environments=set()
for path in paths:
    for name,version,data in chunks(path):
        if name not in {'MPPositionList','WaypointsList','FogSettings','GlobalWaterSettings','EnvironmentData'}: continue
        r=Reader(data)
        try:
            if name=='MPPositionList':
                if not data: summary[name]['empty']+=1; continue
                count=0
                while r.p < len(data):
                    ident,v,size=r.num('<i'),r.num('<h'),r.num('<i')
                    sub=Reader(r.take(size))
                    sub.take(3); sub.num('<I')
                    for _ in range(sub.num('<i')): sub.string()
                    if sub.p!=len(sub.data): raise ValueError('MP record trailing bytes')
                    if v!=1: raise ValueError('unexpected MP record version')
                    count+=1
                summary[name][f'records_{count}']+=1
            elif name=='WaypointsList':
                n=r.num('<i'); r.take(n*8)
                summary[name]['nonzero' if n else 'zero']+=1
            elif name=='FogSettings': r.num('<i'); [r.num('<f') for _ in range(5)]
            elif name=='GlobalWaterSettings': r.num('<i'); r.num('<f')
            elif name=='EnvironmentData':
                if version>=3: r.take(8)
                if 2<=version<5: r.take(1)
                strings=[r.string(),r.string()]
                if version>=4: strings.append(r.string())
                environments.add(tuple(strings))
            if r.p!=len(data): raise ValueError(f'trailing {len(data)-r.p} bytes')
            summary[name]['passed']+=1
        except Exception as e: errors.append({'path':str(path),'asset':name,'error':str(e)})
result={'summary':summary,'errors':errors,'environment_strings':sorted(environments)}
pathlib.Path(sys.argv[1]).with_name('verification.json').write_text(json.dumps(result,ensure_ascii=False,indent=2),encoding='utf-8')
print(json.dumps(result,ensure_ascii=False,indent=2))
