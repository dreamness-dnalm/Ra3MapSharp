import collections, json, base64, struct, pathlib
fixtures=json.loads(pathlib.Path(__file__).with_name('chunk-fixtures.json').read_text(encoding='utf-8'))
heights=collections.Counter(); layers=collections.Counter(); ids=[]
for fixture in fixtures:
    if fixture['type']!='TriggerAreas': continue
    data=base64.b64decode(fixture['data']); p=4
    for _ in range(struct.unpack_from('<i',data)[0]):
        texts=[]
        for _ in range(2):
            n=struct.unpack_from('<H',data,p)[0];p+=2
            texts.append(data[p:p+n].decode('gbk',errors='replace'));p+=n
        ident,n=struct.unpack_from('<ii',data,p);p+=8+n*8
        height=struct.unpack_from('<i',data,p)[0];p+=4
        heights[height]+=1; layers[texts[1]]+=1; ids.append(ident)
print('polygon final int32:',heights)
print('layers:',layers)
print('IDs:',min(ids),max(ids))
