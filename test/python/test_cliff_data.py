import io
import math
import pathlib
import struct
import sys
import tempfile
import unittest

sys.path.insert(0, str(pathlib.Path(__file__).resolve().parents[2] / "scripts"))
from ra3_cliff_data import AssetStream, big_index, hierarchy_matrices, model_bounds, pivot_matrix


def archive(files):
    table_size = 16 + sum(8 + len(n.encode("ascii")) + 1 for n in files)
    offset = table_size
    table = bytearray()
    body = bytearray()
    for name, data in files.items():
        table += struct.pack(">II", offset, len(data)) + name.encode("ascii") + b"\0"
        body += data
        offset += len(data)
    return struct.pack(">4sIII", b"BIG4", offset, len(files), table_size) + table + body


class CliffDataTests(unittest.TestCase):
    def test_read_actual_stream_layout_and_mesh_not_collision_box(self):
        payload = bytes(12) + struct.pack("<6f", -25, -40, -75, 25, 70, 78)
        name = b"W3DMesh:TEST\0"
        header = struct.pack("<12I", 393472, 0, 0, 1, len(payload), 0, 0, 0, 0, 0, len(name), 0)
        entry = struct.pack("<12I", 0, 0, 0, 0, 0, 0, 0, 0, len(payload), 0, 0, 0)
        data = archive({"data/worldbuilder.bin": bytes(4) + payload,
                        "data/worldbuilder.manifest": header + entry + name})
        with tempfile.TemporaryDirectory() as directory:
            path = pathlib.Path(directory) / "test.big"
            path.write_bytes(data)
            stream = AssetStream(path)
            try:
                self.assertEqual(stream.read("W3DMESH:test"), payload)
                bounds = model_bounds(stream, "TEST")
                self.assertEqual(bounds["sizeWorld"], [50, 110, 153])
                self.assertEqual(bounds["centerWorld"], [0, 15, 1.5])
            finally:
                stream.close()

    def test_parent_bone_transform_and_fixup_scale(self):
        identity = [1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1]
        scale = identity.copy()
        scale[0] = 2
        root = struct.pack("<Ii23f", 0, -1, 10, 20, 0, 0, 0, 0, 1, *identity)
        child = struct.pack("<Ii23f", 0, 0, 3, 4, 0, 0, 0, math.sqrt(.5), math.sqrt(.5), *scale)
        matrices = hierarchy_matrices(struct.pack("<3I", 0, 2, 12) + root + child)
        self.assertAlmostEqual(matrices[1][0][3], 13)
        self.assertAlmostEqual(matrices[1][1][3], 24)
        self.assertAlmostEqual(matrices[1][1][0], 2)
        self.assertAlmostEqual(matrices[1][0][1], -1)

    def test_rejects_truncation_invalid_pivot_and_cycles(self):
        with self.assertRaises(ValueError):
            big_index(io.BytesIO(b"BIG4"))
        with self.assertRaises(ValueError):
            pivot_matrix([0, 0, 0], [0, 0, 0, 0], [0] * 16)
        pivot = struct.pack("<Ii23f", 0, 0, *([0] * 6), 1, *([0] * 16))
        with self.assertRaises(ValueError):
            hierarchy_matrices(struct.pack("<3I", 0, 1, 12) + pivot)


if __name__ == "__main__":
    unittest.main()
