import hashlib
import struct
import unittest
from evaluate import evaluate, mesh_report


def tetrahedron():
    a, b, c, d = (0, 0, 0), (10, 0, 0), (0, 10, 0), (0, 0, 10)
    faces = [(a, c, b), (a, b, d), (a, d, c), (b, c, d)]
    return bytes(80) + struct.pack("<I", 4) + b"".join(struct.pack("<12fH", 0, 0, 0, *x, *y, *z, 0) for x, y, z in faces)


class Free3DTests(unittest.TestCase):
    def test_closed_geometry_never_enables_production(self):
        data = tetrahedron()
        result = evaluate(data, [10, 10, 10], 0)
        self.assertTrue(result["after"]["basicGeometryPassed"])
        self.assertFalse(result["productionEnabled"])
        self.assertEqual(result["decision"], "NotReady")

    def test_evidence_bound_to_artifact(self):
        data = tetrahedron()
        evidence = {"modelSha256": hashlib.sha256(data).hexdigest(), "reviewer": "Technical reviewer", "reference": "Print trial 1",
                    "selfIntersectionsChecked": True, "minimumWallChecked": True, "slicingChecked": True, "physicalPrintPassed": True}
        self.assertEqual(evaluate(data, [10, 10, 10], 0, evidence)["decision"], "ReadyForHumanReview")
        self.assertEqual(evaluate(data, [10, 10, 10], 0, evidence, data)["decision"], "NotReady")
        evidence["modelSha256"] = "incorrect"
        self.assertEqual(evaluate(data, [10, 10, 10], 0, evidence)["decision"], "NotReady")

    def test_dimensions_and_open_mesh_fail(self):
        data = tetrahedron()
        self.assertFalse(mesh_report(data, [12, 10, 10], 0.5)["basicGeometryPassed"])
        opened = data[:80] + struct.pack("<I", 3) + data[84:-50]
        self.assertGreater(mesh_report(opened, [10, 10, 10], 0.5)["boundaryEdges"], 0)
        self.assertFalse(mesh_report(opened, [10, 10, 10], 0.5)["basicGeometryPassed"])

    def test_malformed_input_rejected(self):
        for data in [b"solid arbitrary script", tetrahedron()[:-1], bytes(2500085)]:
            with self.assertRaises(ValueError):
                mesh_report(data, [10, 10, 10], 0.5)
        with self.assertRaises(ValueError):
            mesh_report(tetrahedron(), [float("nan"), 10, 10], 0.5)


if __name__ == "__main__":
    unittest.main()
