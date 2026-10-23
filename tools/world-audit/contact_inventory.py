"""Read-only contact hints and explicit native-inventory gaps, never art writes.

Root-height differences are diagnostics against the frozen height field, not
proof that a prop floats: real supports, pivots, and runtime banks need review.
"""
import csv
import gzip
import json
import sys
from pathlib import Path

import numpy as np

root = Path(sys.argv[1])
out = root / "analysis"
totals = {"surfaceAttachedChecked": 0, "rootHeightHints": 0, "knownUnrenderedCampfires": 0}
summary = []
for order in range(1, 101):
    with gzip.open(root / "bundles" / f"{order:03}.json.gz", "rt", encoding="utf-8-sig") as f:
        doc = json.load(f)
    art, level = doc["art"], doc["level"]
    land = art["landscape"]
    resolution = land["resolution"]
    heights = np.asarray(land["heights"]).reshape(resolution, resolution)
    rows = []
    checked = 0
    for pose in art["instances"]:
        if pose.get("surfaceAttached") is not True:
            continue
        u = (pose["x"] - land["originX"]) / land["size"] * (resolution - 1)
        v = (pose["z"] - land["originZ"]) / land["size"] * (resolution - 1)
        if not (0 <= u <= resolution - 1 and 0 <= v <= resolution - 1):
            continue
        ix, iz = min(int(u), resolution - 2), min(int(v), resolution - 2)
        tx, tz = u - ix, v - iz
        ground = ((1 - tx) * heights[iz, ix] + tx * heights[iz, ix + 1]) * (1 - tz)
        ground += ((1 - tx) * heights[iz + 1, ix] + tx * heights[iz + 1, ix + 1]) * tz
        residual = pose.get("y", 0) - ground - pose.get("surfaceOffset", 0)
        checked += 1
        if abs(residual) > .06:
            rows.append({"id": pose["id"], "modelId": pose.get("modelId"),
                         "sourceY": pose.get("y", 0), "frozenHeight": float(ground),
                         "declaredSurfaceOffset": pose.get("surfaceOffset", 0),
                         "residualMetres": float(residual),
                         "status": "hint-only-not-a-proven-visual-defect"})
    facts_path = out / f"{order:03}-facts.json"
    facts = json.loads(facts_path.read_text(encoding="utf-8-sig"))
    facts["sourceLandscapeMeshes"] = [
        {"name": m["name"], "vertices": len(m["vertices"]) // 3,
         "triangles": len(m["triangles"]) // 3, "water": m.get("water", False),
         "decoration": m.get("decoration", False)} for m in land["meshes"]]
    facts["frozenRootContactHints"] = {"checked": checked, "thresholdMetres": .06,
        "count": len(rows), "hints": rows,
        "limitation": "Root-pivot diagnostics only. Does not test full support polygons, object-supported growth or rebuilt runtime riverbanks."}
    fires = [{"id": f"runtime/campfire/{x}/{z}", "cell": [x, z],
              "source": "BoardRenderer.BuildObstacles -> CampfireSite.Create",
              "rendered": False} for x, z in level.get("noise", [])]
    facts["knownUnrenderedInstances"] = fires
    facts["nativeInventoryGaps"] = [
        "CampfireSite procedural rings, embers, flame and smoke at the listed noise cells.",
        "CampGroundDetails procedural meadow, flowers, pebbles and snow layers.",
        "RiverLandscapeGeometry / RiverBankEcology runtime rebuild and extra bank vegetation.",
        "Weather particles, wetness/snow shader surfaces, animated cloth and guest belongings.",
        "Active gameplay HUD, feedback overlays and dynamic interaction states."]
    facts_path.write_text(json.dumps(facts, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    summary.append({"order": order, "id": doc["id"], "surfaceAttachedChecked": checked,
                    "rootHeightHints": len(rows), "knownUnrenderedCampfires": len(fires)})
    for key in totals:
        totals[key] += summary[-1][key]
    print("CONTACT INVENTORY", order, checked, "roots", len(rows), "hints", flush=True)
with (out / "contact-inventory-summary.csv").open("w", newline="", encoding="utf-8-sig") as f:
    writer = csv.DictWriter(f, fieldnames=list(summary[0]),lineterminator="\n")
    writer.writeheader()
    writer.writerows(summary)
(out / "contact-inventory-summary.json").write_text(json.dumps({"totals": totals, "levels": summary}, indent=2) + "\n", encoding="utf-8")
print("CONTACT INVENTORY TOTALS", totals)
