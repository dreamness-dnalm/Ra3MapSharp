"""Read RA3 v6 little-endian asset streams from BIG archives without extracting them.

Mesh bounding boxes are transformed through container bone pivots (translation,
quaternion rotation and FixupMatrix); collision Geometry is deliberately kept separate.
The layout is corroborated by the installed AssetTypeW3D.xsd and real WB renders.
"""
import itertools
import math
import struct


def read_exact(stream, size):
    data = stream.read(size)
    if len(data) != size:
        raise ValueError("Truncated asset stream")
    return data


def big_index(stream):
    magic, _, count, table_end = struct.unpack(">4sIII", read_exact(stream, 16))
    if magic not in (b"BIG4", b"BIGF") or count > 1000000:
        raise ValueError("Unsupported BIG archive")
    entries = {}
    for _ in range(count):
        offset, size = struct.unpack(">II", read_exact(stream, 8))
        name = bytearray()
        while True:
            char = read_exact(stream, 1)
            if char == b"\0":
                break
            name.extend(char)
            if len(name) > 4096:
                raise ValueError("Invalid BIG entry name")
        key = name.decode("ascii").lower().replace("\\", "/")
        if key in entries:
            raise ValueError("Duplicate BIG entry")
        entries[key] = (offset, size)
    if stream.tell() > table_end:
        raise ValueError("Invalid BIG table size")
    return entries


class AssetStream:
    def __init__(self, archive, prefix="data/worldbuilder"):
        self.file = archive.open("rb")
        try:
            index = big_index(self.file)
            self.bin_offset, self.bin_size = index[prefix + ".bin"]
            manifest_offset, manifest_size = index[prefix + ".manifest"]
            self.file.seek(manifest_offset)
            data = read_exact(self.file, manifest_size)
            fields = struct.unpack_from("<12I", data)
            if data[:4] != b"\x00\x01\x06\x00":
                raise ValueError("Only little-endian RA3 manifest v6 is supported")
            count = fields[3]
            if count > 1000000:
                raise ValueError("Invalid asset count")
            names = 48 + count * 48 + fields[8] + fields[9]
            if names + fields[10] + fields[11] != len(data):
                raise ValueError("Invalid manifest section sizes")
            cursor = 4  # .bin starts with the stream checksum.
            self.entries = {}
            for i in range(count):
                record = struct.unpack_from("<12I", data, 48 + i * 48)
                pos = names + record[6]
                end = data.find(b"\0", pos, names + fields[10])
                if pos < names or end < 0 or cursor + record[8] > self.bin_size:
                    raise ValueError("Invalid asset range/name")
                name = data[pos:end].decode("ascii")
                self.entries[name.lower()] = dict(name=name, offset=cursor, size=record[8])
                cursor += record[8]
            if cursor != self.bin_size or fields[4] + 4 != self.bin_size:
                raise ValueError("Asset data sizes do not cover binary stream")
        except Exception:
            self.file.close()
            raise

    def read(self, name):
        entry = self.entries[name.lower()]
        self.file.seek(self.bin_offset + entry["offset"])
        return read_exact(self.file, entry["size"])

    def close(self):
        self.file.close()


def identity():
    return [[float(i == j) for j in range(4)] for i in range(4)]


def multiply(a, b):
    return [[sum(a[i][k] * b[k][j] for k in range(4)) for j in range(4)] for i in range(4)]


def pivot_matrix(translation, quaternion, fixup):
    x, y, z, w = quaternion
    length = math.sqrt(sum(v * v for v in quaternion))
    if length < 1e-9 or not all(math.isfinite(v) for v in (*translation, *quaternion, *fixup)):
        raise ValueError("Invalid pivot transform")
    x, y, z, w = (v / length for v in (x, y, z, w))
    rotation = [
        [1 - 2*(y*y + z*z), 2*(x*y - z*w), 2*(x*z + y*w), translation[0]],
        [2*(x*y + z*w), 1 - 2*(x*x + z*z), 2*(y*z - x*w), translation[1]],
        [2*(x*z - y*w), 2*(y*z + x*w), 1 - 2*(x*x + y*y), translation[2]],
        [0, 0, 0, 1],
    ]
    # XSD fields are M00,M10,M20,M30,M01,... (column-major).
    return multiply(rotation, [[fixup[j*4+i] for j in range(4)] for i in range(4)])


def hierarchy_matrices(data):
    _, count, start = struct.unpack_from("<3I", data)
    if count < 1 or count > 4096 or start < 12 or start + count*100 > len(data):
        raise ValueError("Invalid W3DHierarchy pivots")
    matrices = []
    for i in range(count):
        at = start + i*100
        _, parent = struct.unpack_from("<Ii", data, at)
        values = struct.unpack_from("<23f", data, at + 8)
        local = pivot_matrix(values[:3], values[3:7], values[7:])
        if parent < -1 or parent >= i:
            raise ValueError("Invalid pivot parent/cycle")
        matrices.append(local if parent == -1 else multiply(matrices[parent], local))
    return matrices


def model_bounds(stream, model):
    if ("w3dcontainer:" + model.lower()) in stream.entries:
        container = stream.read("W3DContainer:" + model)
        _, _, count, start = struct.unpack_from("<4I", container)
        if count < 1 or count > 4096 or start < 16 or start + count*16 > len(container):
            raise ValueError("Invalid W3DContainer subobjects")
        matrices = hierarchy_matrices(stream.read("W3DHierarchy:" + model))
        references = []
        for i in range(count):
            bone, length, pointer, _ = struct.unpack_from("<4I", container, start + i*16)
            if bone >= len(matrices) or pointer + length >= len(container) or container[pointer+length] != 0:
                raise ValueError("Invalid container mesh reference")
            subobject = container[pointer:pointer+length].decode("ascii")
            references.append(("W3DMesh:" + model + "." + subobject, bone, matrices[bone]))
    else:
        references = [("W3DMesh:" + model, 0, identity())]
    meshes = []
    for name, bone, matrix in references:
        mesh = stream.read(name)
        bounds = struct.unpack_from("<6f", mesh, 12)
        if not all(math.isfinite(v) for v in bounds) or any(bounds[i] > bounds[i+3] for i in range(3)):
            raise ValueError("Invalid mesh bounds")
        corners = [[sum(matrix[i][k] * p[k] for k in range(4)) for i in range(3)]
                   for xyz in itertools.product(*[(bounds[i], bounds[i+3]) for i in range(3)])
                   for p in [(*xyz, 1)]]
        meshes.append(dict(name=name, bone=bone, rawMin=list(bounds[:3]), rawMax=list(bounds[3:]),
                           min=[min(p[i] for p in corners) for i in range(3)],
                           max=[max(p[i] for p in corners) for i in range(3)],
                           decorativeEffect="FXLIGHT" in name.upper()))
    bodies = [m for m in meshes if not m["decorativeEffect"]]
    if not bodies:
        raise ValueError("Model has no structural meshes")
    low = [min(m["min"][i] for m in bodies) for i in range(3)]
    high = [max(m["max"][i] for m in bodies) for i in range(3)]
    return dict(min=low, max=high, sizeWorld=[high[i]-low[i] for i in range(3)],
                centerWorld=[(low[i]+high[i])/2 for i in range(3)], meshes=meshes,
                excludedMeshes=[m["name"] for m in meshes if m["decorativeEffect"]])
