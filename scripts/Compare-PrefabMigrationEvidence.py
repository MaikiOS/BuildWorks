"""Compare the two locally measured native prefabs; never change game/source files."""
import argparse
import copy
import hashlib
import json
import pathlib
import re

NOTES = {
    "wood_wall_roof": "root/REPLACED BY wood_wall_roof_a",
    "wood_wall_roof_a": 'root/Replaces wood_wall_roof because of "same name and position" snapping issue',
}


def normalized(prefab):
    note = NOTES[prefab["name"]]
    result = {}
    notes = 0
    for node in prefab["nodes"]:
        path = re.sub(r"/\d+:", "/", node["path"])
        if path == note:
            # A developer label is evidence of intent, not part of the model.
            if node["meshSha256"] or node["colliderData"] or node["rendererType"]:
                raise ValueError("Developer note unexpectedly has geometry")
            notes += 1
            continue
        value = dict(node, path=path)
        if path == "root":
            # Placement replaces authoring position/rotation; intrinsic scale stays.
            value.pop("position")
            value.pop("rotation")
        if path in result:
            raise ValueError("Ambiguous repeated child name: " + path)
        result[path] = value
    if notes != 1 or "root" not in result:
        raise ValueError("Expected developer marker/root missing")
    return result


def differences(report):
    if [p["name"] for p in report["prefabs"]] != list(NOTES):
        raise ValueError("Unexpected prefab pair")
    left, right = map(normalized, report["prefabs"])
    return sorted(key for key in left.keys() | right.keys() if left.get(key) != right.get(key))


def negative_controls(report):
    tests = 0
    for field, predicate, change in (
        ("scale", lambda n: n["path"] == "root", lambda v: dict(v, x=v["x"] * 2)),
        ("position", lambda n: n["tag"] == "snappoint", lambda v: dict(v, x=v["x"] + 1)),
        ("rotation", lambda n: n["tag"] == "snappoint", lambda v: dict(v, w=0)),
        ("meshSha256", lambda n: bool(n["meshSha256"]), lambda v: "BAD" + v),
        ("colliderData", lambda n: bool(n["colliderData"]), lambda v: v + ["changed"]),
        ("materials", lambda n: bool(n["materials"]), lambda v: v + ["changed"]),
        ("rendererState", lambda n: bool(n["rendererState"]), lambda v: "changed"),
        ("lodData", lambda n: bool(n["lodData"]), lambda v: v + ["changed"]),
        ("active", lambda n: bool(n["rendererType"]), lambda v: not v),
    ):
        altered = copy.deepcopy(report)
        node = next(n for n in altered["prefabs"][1]["nodes"] if predicate(n))
        node[field] = change(node[field])
        if not differences(altered):
            raise AssertionError("Missed changed " + field)
        tests += 1
    altered = copy.deepcopy(report)
    nodes = altered["prefabs"][1]["nodes"]
    nodes.pop(next(i for i, node in enumerate(nodes) if node["meshSha256"]))
    if not differences(altered):
        raise AssertionError("Missed removed node")
    return tests + 1


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("evidence", type=pathlib.Path)
    parser.add_argument("output", type=pathlib.Path)
    args = parser.parse_args()
    if args.evidence.resolve() == args.output.resolve():
        parser.error("Output must not replace input")
    raw = args.evidence.read_bytes()
    report = json.loads(raw.decode("utf-8-sig"))
    changed = differences(report)
    result = {
        "inputSha256": hashlib.sha256(raw).hexdigest().upper(),
        "utc": report["utc"], "manifestSha256": report["manifestSha256"],
        "comparedNodes": len(normalized(report["prefabs"][0])),
        "excluded": "Developer note objects, child indices, root authoring position/rotation. Root scale and all recorded child transforms compared.",
        "equalRecordedGeometryAndRenderBindings": not changed,
        "differentPaths": changed,
        "negativeControls": negative_controls(report),
        "unverified": ["native costs/support/state behavior", "automatic import/source provenance", "native construction/reload of automatic conversion"],
    }
    args.output.write_text(json.dumps(result, ensure_ascii=False, indent=2), encoding="utf-8")
    print(json.dumps(result, ensure_ascii=False))
    return 1 if changed else 0


if __name__ == "__main__":
    raise SystemExit(main())
