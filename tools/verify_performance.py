#!/usr/bin/env python3
"""Check public native receipts, raw hashes, percentile results and source provenance."""
import gzip
import hashlib
import json
import math
import statistics
import subprocess
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
BASE = ROOT / "docs/performance/2026-10-09"


def check_dataset(directory, checkpoint):
    payload = gzip.decompress((directory / "editor-audit.json.gz").read_bytes())
    raw = json.loads(payload)
    summary = json.loads((directory / "summary.json").read_text())
    receipt = json.loads((directory / "native-result.json").read_text())
    sha = hashlib.sha256(payload).hexdigest()
    assert sha == summary["rawJsonSha256"] == receipt["rawJsonSha256"]
    assert receipt["result"] == "Passed" and all(receipt["sessionGuards"].values())
    assert receipt["editorReused"] and not receipt["playerBuild"]
    assert all(c["result"] == "Passed" for c in receipt["nativeTests"])
    assert raw["terminalStage"] == summary["terminalStage"] == "finished"
    assert len(raw["frames"]) == summary["frameCount"] < 30000
    assert len(raw["spans"]) == summary["spanCount"] < 250000
    assert sum(m["samples"] for m in summary["methods"]) == len(raw["spans"])
    assert not raw["methodology"]["playerBuild"] and not raw["methodology"]["deepProfiling"]
    assert raw["methodology"]["workerCount"] == 1
    assert all(f["wallMs"] >= 0 for f in raw["frames"])
    excluded = set(summary["excludedSnapshotFrames"])
    for scenario in summary["scenarios"]:
        values = [f["wallMs"] for f in raw["frames"] if f["stage"] == scenario["stage"] and f["frame"] not in excluded]
        assert len(values) == scenario["frames"]
        assert math.isclose(statistics.median(values), scenario["wallMs"]["p50"], abs_tol=1e-7)
        if len(values) >= 2:
            expected = statistics.quantiles(values, n=100, method="inclusive")[94]
            assert math.isclose(expected, scenario["wallMs"]["p95"], abs_tol=1e-7)
    for counter in summary["counterHealth"]:
        if not counter["nonzeroSamples"]:
            assert not counter["usable"] and counter["warning"]
    for path, expected in raw["source"]["instrumentedSourceSha256"].items():
        captured = subprocess.check_output(["git", "show", checkpoint + ":" + path], cwd=ROOT)
        assert hashlib.sha256(captured).hexdigest() == expected, path
    fixture = "QuietCamp/Assets/QuietCamp/Tests/PlayMode/PerformanceAuditPlayModeTests.cs"
    captured = subprocess.check_output(["git", "show", checkpoint + ":" + fixture], cwd=ROOT)
    assert hashlib.sha256(captured).hexdigest() == raw["source"]["fixtureSha256"] == receipt["fixtureSha256"]
    print(f"PASS {directory.relative_to(ROOT)}: raw/native/source hashes, frame percentiles, counter validity and complete run")
    return summary


def main():
    primary = check_dataset(BASE, "235804a")
    check_dataset(BASE / "roadmap", "ca3476b")
    for comparison in primary["routeComparisons"]:
        for mode in ["normal", "reduced"]:
            selected = [op["elapsedMs"] for op in primary["operations"]
                        if op["label"].startswith("route.high." + mode + ".r")
                        and op["label"].endswith(comparison["direction"])]
            assert len(selected) == comparison[mode]["repetitions"] == 3
            assert math.isclose(statistics.median(selected), comparison[mode]["elapsedMs"]["p50"], abs_tol=1e-7)
    assert [t["coveredVertices"] for t in primary["leafTiers"]] == [[46800], [51120], [55440]]
    regression = json.loads((BASE / "foliage-tests.json").read_text())
    assert len(regression["tests"]) == 5 and all(t["result"] == "Passed" for t in regression["tests"])
    assert all(regression["sessionGuards"].values())
    print("PASS route comparison: exactly 3 repetitions per direction/mode; leaf geometry and 5 native foliage regressions")


if __name__ == "__main__":
    main()
