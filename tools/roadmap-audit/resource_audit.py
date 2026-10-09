#!/usr/bin/env python3
"""Read-only resource checks and an explicitly hypothetical three-region model."""
import hashlib
import json
import math
import pathlib
import random
import subprocess

ROOT = pathlib.Path(__file__).resolve().parents[2]
CONTENT = ROOT / "QuietCamp/Assets/QuietCamp/Resources/QuietCamp"
OUT = ROOT / "TestResults/roadmap-audit-2026-10-07"


def read(name):
    return json.loads((CONTENT / name).read_text())


def require(condition, message):
    if not condition:
        raise AssertionError(message)


def main():
    campaign = read("campaign.json")
    summaries = read("level_summaries.json")
    models = read("roadmap_models.json")
    ids = campaign["mvpLevelIds"] + campaign.get("generatedLevelIds", [])
    require([s["id"] for s in summaries] == ids, "Art and button order disagree")
    require(len(ids) == len(set(ids)), "Duplicate campaign ID")
    model_ids = [m["id"] for m in models]
    require(len(model_ids) == len(set(model_ids)), "Duplicate model ID")
    require({"tree_default", "tree_pineRoundA", "plant_bushSmall"} <= set(model_ids), "Missing base model")
    raw_bytes = expanded_bytes = triangles = 0
    for m in models:
        require(len(m["data"]) > 0 and len(m["data"]) % 18 == 0, "Malformed triangle stream: " + m["id"])
        require(len(m["colors"]) == len(m["data"]) // 18, "Color stream mismatch: " + m["id"])
        require(all(math.isfinite(v) for v in m["data"]), "Non-finite vertex")
        require(all(0 <= v <= 0xFFFFFF for v in m["colors"]), "Invalid RGB")
        n = len(m["data"]) // 6
        raw_bytes += len(m["data"]) * 4 + len(m["colors"]) * 4
        expanded_bytes += n * 24 + len(m["colors"]) * 16
        triangles += n // 3
    winter_fire = []
    for number, s in enumerate(summaries, 1):
        name = ("GeneratedLevels/" + s["id"].replace(":", "_") if s["id"].startswith("gen:") else "Levels/" + s["id"]) + ".json"
        level = read(name)
        require(s["number"] == number, "Wrong ordinal")
        for key in ("id", "width", "height", "decorSeed", "lighting", "environmentPreset", "entry", "accessPoints", "canopies", "exteriorWalkable", "environment"):
            require(s.get(key) == level.get(key), f"Stale summary {s['id']}:{key}")
        require(s["mapObjects"] == level["objects"], "Stale object layout")
        for o in s["mapObjects"]:
            require(o["assetId"] in model_ids, "Missing object model: " + o["assetId"])
        require(s["fire"] == any("campfire" in o["assetId"] for o in level["objects"]), "Fire flag mismatch")
        if s.get("environment", {}).get("seasonId") == "winter" and s["fire"]:
            winter_fire.append({"id": s["id"], "noiseSources": len(level["noise"])})

    # Feasibility simulation ONLY. No replacement renderer/planner is shipped.
    window_results = []
    for count in (30, 300, 1000):
        regions = (count + 9) // 10
        requests = list(range(regions)) + list(reversed(range(regions)))
        rng = random.Random(1918)
        requests += [rng.randrange(regions) for _ in range(10000)]
        resident = set()
        retained = entered = 0
        maximum = 0
        for current in requests:
            desired = set(range(max(0, current - 1), min(regions, current + 2)))
            retained += len(resident & desired)
            entered += len(desired - resident)
            resident = desired
            maximum = max(maximum, len(resident))
            require(current in resident and len(resident) <= 3, "Broken region window")
            require(all(abs(r - current) <= 1 for r in resident), "Distant active region")
        window_results.append({"levels": count, "requests": len(requests), "maxActiveRegions": maximum, "retainedLeases": retained, "enteredLeases": entered})

    base = ROOT / "QuietCamp/Assets/QuietCamp"
    files = sorted((base / "Scripts/Presentation/UI").glob("Roadmap*.cs"))
    files += [base / f for f in ("Scripts/Presentation/UI/MenuScreens.cs", "Scripts/Presentation/UI/MenuMapBinding.cs", "Scripts/Infrastructure/CampContent.cs", "Scripts/Infrastructure/LevelLoader.cs", "Resources/QuietCamp/campaign.json", "Resources/QuietCamp/level_summaries.json", "Resources/QuietCamp/roadmap_models.json")]
    result = {
        "scope": "resource integrity + proposed-window simulation; not Unity rendering, profiler or solver",
        "head": subprocess.check_output(["git", "rev-parse", "HEAD"], cwd=ROOT, text=True).strip(),
        "resourceChecks": "PASS", "campaignLevels": len(ids), "models": len(models), "modelTriangles": triangles,
        "modelArrayPayloadBytes": {"raw": raw_bytes, "expanded": expanded_bytes, "retainedTotalLowerBound": raw_bytes + expanded_bytes, "excludes": "array/object headers, JSON text, story/bare variants, native meshes/materials"},
        "winterFireLevelsAffectedByMissingNoiseInSnowTerrain": winter_fire,
        "resourceFileBytes": {n: (CONTENT/n).stat().st_size for n in ("campaign.json", "level_summaries.json", "roadmap_models.json")},
        "proposedWindowSimulation": window_results,
        "sha256": {str(f.relative_to(ROOT)): hashlib.sha256(f.read_bytes()).hexdigest() for f in files}
    }
    OUT.mkdir(parents=True, exist_ok=True)
    (OUT / "resource-audit.json").write_text(json.dumps(result, indent=2) + "\n")
    print(json.dumps({k: v for k, v in result.items() if k != "sha256"}, indent=2))


if __name__ == "__main__":
    main()
