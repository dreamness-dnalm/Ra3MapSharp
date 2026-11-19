"""Read-only RA3 chunk census. Never invokes asset parsers or saves maps."""
import argparse, collections, json, pathlib, re, struct

def unpack_map(data):
    flag = struct.unpack_from('<I', data)[0]
    if flag == 1884121923:
        return data
    if flag != 5390661:
        raise ValueError('unknown signature')
    p = 8
    wide = data[p] & 128
    p += 6 if wide else 5
    out = bytearray()
    while True:
        c = data[p]; p += 1
        if c < 128:
            b = data[p]; p += 1
            lit, distance, length = c & 3, 1 + b + (c & 96)*8, (c & 28)//4 + 3
        elif c < 192:
            b, d = data[p:p+2]; p += 2
            lit, distance, length = b >> 6, 1 + ((b & 63)<<8) + d, (c & 63)+4
        elif c < 224:
            b, d, e = data[p:p+3]; p += 3
            lit, distance, length = c & 3, 1 + ((c & 16)<<12) + (b<<8) + d, ((c & 12)<<6)+e+5
        else:
            lit = (c & 31)*4+4
            if lit > 112:
                out.extend(data[p:p+(c & 3)])
                break
            out.extend(data[p:p+lit]); p += lit
            continue
        out.extend(data[p:p+lit]); p += lit
        if distance > len(out): raise ValueError('invalid backreference')
        block = out[-distance:]
        out.extend((block * ((length + distance - 1)//distance))[:length])
    expected = struct.unpack_from('<I', data, 4)[0]
    if len(out) != expected: raise ValueError('decompressed size mismatch')
    return bytes(out)

def chunks(path):
    data = unpack_map(path.read_bytes()); p = 4
    n = struct.unpack_from('<i', data, p)[0]; p += 4
    names = {}
    for _ in range(n):
        size = shift = 0
        while True:
            b = data[p]; p += 1; size |= (b & 127) << shift; shift += 7
            if b < 128: break
        name = data[p:p+size].decode('utf-8'); p += size
        ident = struct.unpack_from('<i', data, p)[0]; p += 4
        names[ident] = name
    while p < len(data):
        ident, version, size = struct.unpack_from('<ihi', data, p); p += 10
        if size < 0 or p + size > len(data): raise ValueError('invalid chunk size')
        payload = data[p:p+size]; p += size
        yield names[ident], version, payload

def main():
    ap = argparse.ArgumentParser(); ap.add_argument('roots', nargs='+'); ap.add_argument('--out', required=True)
    args = ap.parse_args(); stats = {}; failures = []; total = 0
    for root in args.roots:
        for path in pathlib.Path(root).rglob('*.map'):
            try:
                records = list(chunks(path)); total += 1
                for name, version, payload in records:
                    s = stats.setdefault(name, {'count':0,'versions':{},'sizes':{},'examples':[]})
                    s['count'] += 1
                    for key, value in [('versions',version),('sizes',len(payload))]:
                        value = str(value); s[key][value] = s[key].get(value,0)+1
                    if len(s['examples']) < 3:
                        s['examples'].append({'path':str(path),'version':version,'size':len(payload),'head':payload[:64].hex(),'strings':[x.decode('ascii') for x in re.findall(rb'[ -~]{5,}',payload)[:12]]})
            except Exception as e: failures.append({'path':str(path),'error':str(e)})
    pathlib.Path(args.out).write_text(json.dumps({'maps':total,'failures':failures,'assets':stats},ensure_ascii=False,indent=2),encoding='utf-8')
    print('maps:',total,'failures:',len(failures))
    for name,s in sorted(stats.items()):
        sizes = list(map(int,s['sizes'])); print(name,s['count'],'versions',s['versions'],'size',min(sizes),max(sizes))

if __name__ == '__main__': main()
