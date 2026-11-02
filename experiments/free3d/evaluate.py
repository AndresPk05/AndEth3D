"""Offline acceptance harness for externally generated binary STL.

Never executes or repairs a model. No production/API integration. Exact vertex
matching is deliberately conservative; a mesh needing repair stays in review.
"""
import argparse
from collections import Counter, defaultdict
import hashlib
import json
import math
from pathlib import Path
import struct


def mesh_report(data, expected, tolerance):
    if len(expected) != 3 or any(not math.isfinite(n) or not 0 < n <= 5000 for n in expected):
        raise ValueError("Invalid expected dimensions")
    if not math.isfinite(tolerance) or not 0 <= tolerance <= 10:
        raise ValueError("Invalid tolerance")
    if not 84 <= len(data) <= 2500084:
        raise ValueError("Only bounded binary STL is accepted")
    count = struct.unpack_from("<I", data, 80)[0]
    if not 1 <= count <= 50000 or len(data) != 84 + count * 50:
        raise ValueError("Invalid triangle count or file length")
    directed = Counter()
    undirected = defaultdict(list)
    minimum = [math.inf] * 3
    maximum = [-math.inf] * 3
    degenerate = 0
    volume = 0.0
    for i in range(count):
        points = [struct.unpack_from("<3f", data, 84 + 50 * i + 12 + 12 * j) for j in range(3)]
        for point in points:
            if any(not math.isfinite(n) or abs(n) > 5000 for n in point):
                raise ValueError("Nonfinite or excessive coordinates")
            for k, n in enumerate(point):
                minimum[k] = min(minimum[k], n)
                maximum[k] = max(maximum[k], n)
        a, b, c = points
        cross = ((b[1]-a[1])*(c[2]-a[2])-(b[2]-a[2])*(c[1]-a[1]),
                 (b[2]-a[2])*(c[0]-a[0])-(b[0]-a[0])*(c[2]-a[2]),
                 (b[0]-a[0])*(c[1]-a[1])-(b[1]-a[1])*(c[0]-a[0]))
        if sum(n*n for n in cross) < 1e-16:
            degenerate += 1
        volume += (a[0]*(b[1]*c[2]-b[2]*c[1]) + a[1]*(b[2]*c[0]-b[0]*c[2]) + a[2]*(b[0]*c[1]-b[1]*c[0])) / 6
        for u, v in [(a, b), (b, c), (c, a)]:
            directed[(u, v)] += 1
            undirected[tuple(sorted((u, v)))].append(i)
    boundary = sum(len(faces) == 1 for faces in undirected.values())
    nonmanifold = sum(len(faces) != 2 for faces in undirected.values())
    inconsistent = sum(directed[(u, v)] != 1 or directed[(v, u)] != 1 for u, v in undirected)
    adjacent = defaultdict(set)
    for faces in undirected.values():
        # No quadratic expansion for a malicious nonmanifold edge.
        for face in faces[1:]:
            adjacent[faces[0]].add(face)
            adjacent[face].add(faces[0])
    remaining = set(range(count))
    components = 0
    while remaining:
        components += 1
        pending = [remaining.pop()]
        while pending:
            for neighbor in adjacent[pending.pop()]:
                if neighbor in remaining:
                    remaining.remove(neighbor)
                    pending.append(neighbor)
    dimensions = [maximum[i] - minimum[i] for i in range(3)]
    errors = [abs(dimensions[i] - expected[i]) for i in range(3)]
    passed = all(n <= tolerance for n in errors) and not (degenerate or nonmanifold or inconsistent) and components == 1 and volume > 0
    return {"sha256": hashlib.sha256(data).hexdigest(), "triangles": count,
            "dimensionsMm": dimensions, "absoluteDimensionErrorsMm": errors,
            "boundaryEdges": boundary, "nonmanifoldEdges": nonmanifold,
            "inconsistentEdges": inconsistent, "degenerateTriangles": degenerate,
            "components": components, "signedVolumeMm3": volume, "basicGeometryPassed": passed}


def evaluate(original, expected, tolerance, evidence=None, repaired=None):
    before = mesh_report(original, expected, tolerance)
    after = mesh_report(repaired, expected, tolerance) if repaired is not None else before
    evidence = evidence or {}
    checks = ["selfIntersectionsChecked", "minimumWallChecked", "slicingChecked", "physicalPrintPassed"]
    if repaired is not None:
        checks.append("repairedArtifactReviewed")
    reviewed = (evidence.get("modelSha256") == after["sha256"]
                and isinstance(evidence.get("reviewer"), str) and bool(evidence["reviewer"].strip())
                and isinstance(evidence.get("reference"), str) and bool(evidence["reference"].strip())
                and all(evidence.get(key) is True for key in checks))
    return {"protocol": "free3d-evaluation-v1", "before": before, "after": after,
            "repairProvided": repaired is not None, "humanEvidenceComplete": reviewed,
            "decision": "ReadyForHumanReview" if reviewed and after["basicGeometryPassed"] else "NotReady",
            "productionEnabled": False,
            "limitations": ["No automatic repair, code execution, model generation or printer access.",
                            "Self-intersections, minimum wall, strength, appearance and manufacturability require independent review.",
                            "Human evidence is an operator declaration bound to the exact artifact, not an automated certification.",
                            "Results never enable a production provider or automatic confirmation."]}


def read_model(path):
    if not 84 <= path.stat().st_size <= 2500084:
        raise ValueError("File size out of bounds")
    return path.read_bytes()


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("model", type=Path)
    parser.add_argument("--expected-mm", type=float, nargs=3, required=True)
    parser.add_argument("--tolerance-mm", type=float, default=0.5)
    parser.add_argument("--repaired", type=Path)
    parser.add_argument("--evidence", type=Path)
    parser.add_argument("--output", type=Path, required=True)
    args = parser.parse_args()
    evidence = None
    if args.evidence:
        if args.evidence.stat().st_size > 32000:
            raise ValueError("Evidence too large")
        evidence = json.loads(args.evidence.read_text(encoding="utf-8"))
        if not isinstance(evidence, dict):
            raise ValueError("Expected evidence object")
    report = evaluate(read_model(args.model), args.expected_mm, args.tolerance_mm,
                      evidence, read_model(args.repaired) if args.repaired else None)
    # Never silently overwrite the original, repaired model, evidence or prior report.
    with args.output.open("x", encoding="utf-8") as output:
        json.dump(report, output, indent=2, allow_nan=False)


if __name__ == "__main__":
    main()
