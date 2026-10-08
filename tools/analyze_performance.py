#!/usr/bin/env python3
"""Summarize the opt-in native Editor audit. Times are milliseconds, never device FPS.

Usage: python3 tools/analyze_performance.py audit.json[.gz] output-directory [--plots]
The analysis uses only stdlib; --plots additionally requires matplotlib.
"""
import argparse
import csv
import gzip
import hashlib
import json
import math
import statistics
from collections import defaultdict
from pathlib import Path


def percentile(values, fraction):
    values = sorted(values)
    if not values:
        return None
    at = (len(values) - 1) * fraction
    lo = math.floor(at)
    return values[lo] + (values[math.ceil(at)] - values[lo]) * (at - lo)


def distribution(values):
    values = list(values)
    if not values:
        return None
    return {"samples": len(values), "p50": percentile(values, .5), "p95": percentile(values, .95),
            "p99": percentile(values, .99), "max": max(values), "mean": statistics.mean(values)}


def analyze(data):
    counters = {c["name"]: (i, c) for i, c in enumerate(data["counters"])}

    def value(frame, name):
        if name not in counters:
            return None
        index, descriptor = counters[name]
        if index >= len(frame["values"]):
            return None
        raw = frame["values"][index]
        return raw / 1e6 if descriptor["unit"] == "TimeNanoseconds" else raw

    snapshots = {s["frame"] for s in data["snapshots"] if "frame" in s}
    frames = [f for f in data["frames"] if f["frame"] not in snapshots]
    by_stage = defaultdict(list)
    by_frame = defaultdict(list)
    for f in frames:
        if not f["stage"].startswith("audit.snapshot."):
            by_stage[f["stage"]].append(f)
    for span in data["spans"]:
        # A diagnostic scan is outside these method scopes. Keep real method calls
        # sharing its frame; exclude only that frame's mixed wall/counter sample.
        by_frame[span["frame"]].append(span)

    def frame_stats(items):
        result = {"frames": len(items), "wallMs": distribution(f["wallMs"] for f in items),
                  "wallOver33Ms": sum(f["wallMs"] > 1000 / 30 for f in items),
                  "wallOver50Ms": sum(f["wallMs"] > 50 for f in items),
                  "wallOver100Ms": sum(f["wallMs"] > 100 for f in items)}
        for name in counters:
            result[name] = distribution(v for f in items if (v := value(f, name)) is not None)
        result["gcCollections0"] = sum(max(0, b["gc0"] - a["gc0"]) for a, b in zip(items, items[1:])
                                              if b["frame"] == a["frame"] + 1)
        return result

    methods = defaultdict(list)
    for spans in by_frame.values():
        for s in spans:
            methods[s["name"]].append(s)
    method_stats = []
    for name, spans in methods.items():
        maximum = max(spans, key=lambda s: s["durationMs"])
        method_stats.append({"name": name, **distribution(s["durationMs"] for s in spans),
                             "worst": maximum, "inclusiveTotalMs": sum(s["durationMs"] for s in spans)})
    method_stats.sort(key=lambda s: s["max"], reverse=True)

    operations = []
    for op in data["operations"]:
        # The counter is read in the next Update. Its timestamp is the END of the completed frame.
        # Assign route frames by the measured interval, rather than warm-up stage labels left in place.
        selected = [f for f in frames if op["startMs"] <= f["timestampMs"] <= op["endMs"]]
        states = defaultdict(list)
        for f in selected:
            states[f["transition"]].append(f)
        leaf = [s for f in selected for s in by_frame[f["frame"]]
                if s["name"] == "QC.LeafCurtainGraphic.OnPopulateMesh"]
        top = sorted((s for f in selected for s in by_frame[f["frame"]]),
                     key=lambda s: s["durationMs"], reverse=True)[:6]
        operations.append({**op, "stats": frame_stats(selected),
                           "states": {state: {"frames": len(items), "wallTotalMs": sum(f["wallMs"] for f in items),
                                               "wallMs": distribution(f["wallMs"] for f in items)} for state, items in states.items()},
                           "leafMeshMs": distribution(s["durationMs"] for s in leaf), "topSpans": top})

    leaf_tiers = []
    for tier in range(3):
        spans = [s for s in methods.get("QC.LeafCurtainGraphic.OnPopulateMesh", [])
                 if s["stage"].startswith(f"leaves.tier{tier}.normal.")]
        verts = [s["activeLeavesVertices"] for s in data["snapshots"]
                 if s["stage"].startswith(f"leaves.tier{tier}.normal.")]
        leaf_tiers.append({"tier": tier, "meshMs": distribution(s["durationMs"] for s in spans),
                           "coveredVertices": sorted(set(verts))})

    comparisons = []
    for direction in ["menu-camp", "camp-menu"]:
        comparison = {"direction": direction}
        for reduced in [False, True]:
            selected = [op for op in operations if op["label"].startswith("route.high." + ("reduced" if reduced else "normal") + ".r")
                        and op["label"].endswith(direction) and op["reduced"] == reduced]
            comparison["reduced" if reduced else "normal"] = {
                "repetitions": len(selected), "elapsedMs": distribution(op["elapsedMs"] for op in selected),
                "maxWallMsPerRun": [op["stats"]["wallMs"]["max"] for op in selected],
                "wallOver100Ms": sum(op["stats"]["wallOver100Ms"] for op in selected)}
        comparisons.append(comparison)

    health = []
    for name, (_, c) in counters.items():
        vals = [value(f, name) for f in frames]
        vals = [v for v in vals if v is not None]
        nonzero = sum(v != 0 for v in vals)
        health.append({**c, "samples": len(vals), "nonzeroSamples": nonzero,
                       "usable": bool(nonzero), "warning": None if nonzero else "All zero: unavailable/unsupported; not evidence of zero cost."})
    worst = []
    for f in sorted(frames, key=lambda f: f["wallMs"], reverse=True)[:30]:
        worst.append({**{k: v for k, v in f.items() if k != "values"},
                      "mainThreadMs": value(f, "Main Thread"), "playerLoopMs": value(f, "PlayerLoop"),
                      "gcBytes": value(f, "GC Allocated In Frame"), "gcMs": value(f, "GC.Collect"),
                      "topSpans": sorted(by_frame[f["frame"]], key=lambda s: s["durationMs"], reverse=True)[:6]})
    return {"schemaVersion": 1, "capturedUtc": data["capturedUtc"], "unityVersion": data["unityVersion"],
            "host": data["host"], "screen": data["screen"], "source": data["source"],
            "methodology": data["methodology"], "terminalStage": data["terminalStage"],
            "frameCount": len(data["frames"]), "spanCount": len(data["spans"]),
            "excludedSnapshotFrames": sorted(snapshots), "counterHealth": health,
            "threadAllocationApiUsable": any(s["allocatedBytes"] for s in data["spans"]),
            "scenarios": [{"stage": stage, **frame_stats(items)} for stage, items in by_stage.items()],
            "methods": method_stats, "operations": operations, "routeComparisons": comparisons,
            "leafTiers": leaf_tiers, "snapshots": data["snapshots"], "worstFrames": worst,
            "instrumentationOverhead": data["instrumentationOverhead"]}


def plots(data, summary, output):
    import matplotlib
    matplotlib.use("Agg")
    import matplotlib.pyplot as plt
    plt.rcParams.update({"font.family": "DejaVu Sans", "font.size": 10, "axes.spines.top": False,
                         "axes.spines.right": False, "figure.facecolor": "#faf9f3", "axes.facecolor": "#faf9f3"})
    colors = {"wall": "#ad6746", "main": "#3f6954", "leaf": "#bd8845"}
    selected = [s for s in summary["scenarios"] if s["stage"].endswith(("steady", "solved", "empty", "high"))
                and s["frames"] >= 100]
    fig, ax = plt.subplots(figsize=(11, max(5, len(selected) * .36)))
    y = list(range(len(selected)))
    ax.barh([i - .17 for i in y], [s["wallMs"]["p95"] for s in selected], .32, color=colors["wall"], label="p95 wall interval (Editor + OS)")
    ax.barh([i + .17 for i in y], [s["Main Thread"]["p95"] for s in selected], .32, color=colors["main"], label="p95 Main Thread recorder")
    ax.axvline(16.67, color="#999", ls="--", lw=1, label="60 Hz budget: 16.67 ms")
    ax.axvline(33.33, color="#bbb", ls=":", lw=1, label="30 Hz budget: 33.33 ms")
    ax.set_yticks(y, [s["stage"] for s in selected]); ax.invert_yaxis()
    ax.set_xlabel("milliseconds per completed Editor frame"); ax.set_title("Quiet Camp · native Editor audit · steady windows")
    ax.legend(loc="lower right", fontsize=8); fig.tight_layout(); fig.savefig(output / "steady-windows.png", dpi=160); plt.close(fig)

    fig, axes = plt.subplots(1, 2, figsize=(11, 4.4))
    names = ["Low", "Balanced", "High"]
    axes[0].bar(names, [s["meshMs"]["p95"] for s in summary["leafTiers"]], color=colors["leaf"])
    axes[0].axhline(16.67, color="#999", ls="--", lw=1); axes[0].set_ylabel("OnPopulateMesh p95, ms per call")
    axes[0].set_title("Leaves alone · two repetitions per tier")
    for i, tier in enumerate(summary["leafTiers"]):
        axes[0].text(i, tier["meshMs"]["p95"] + .35, f'{tier["coveredVertices"][0]:,} vertices', ha="center", fontsize=9)
    axes[0].set_ylim(0, max(s["meshMs"]["p95"] for s in summary["leafTiers"]) * 1.3)
    for i, mode in enumerate(["normal", "reduced"]):
        axes[1].bar([j + (i - .5) * .32 for j in range(2)],
                    [s[mode]["elapsedMs"]["p50"] / 1000 for s in summary["routeComparisons"]], .3,
                    label=mode, color=[colors["leaf"], colors["main"]][i])
    axes[1].set_xticks([0, 1], ["Menu → camp", "Camp → menu"]); axes[1].set_ylabel("route wall duration, seconds (median of 3)")
    axes[1].set_title("Full routes · High · animation time included"); axes[1].legend()
    fig.tight_layout(); fig.savefig(output / "leaves-and-routes.png", dpi=160); plt.close(fig)

    op = next(o for o in summary["operations"] if o["label"] == "route.high.normal.r2.menu-camp")
    frames = [f for f in data["frames"] if op["startMs"] <= f["timestampMs"] <= op["endMs"]]
    leaf = [s for s in data["spans"] if s["name"] == "QC.LeafCurtainGraphic.OnPopulateMesh"
            and op["startMs"] <= s["startMs"] <= op["endMs"]]
    fig, ax = plt.subplots(figsize=(11, 4.8))
    ax.vlines([(f["timestampMs"] - op["startMs"]) / 1000 for f in frames], 0, [f["wallMs"] for f in frames], color=colors["wall"], lw=1, label="wall interval at frame end")
    ax.scatter([(s["startMs"] - op["startMs"]) / 1000 for s in leaf], [s["durationMs"] for s in leaf], color=colors["leaf"], s=8, label="leaf mesh synchronous call")
    annotations = [s for s in op["topSpans"] if s["durationMs"] > 80
                   and s["name"] in ["QC.CampSceneHost.Start", "QC.HtmlSurface.MountDocument"]][:3]
    for i, s in enumerate(annotations):
        at = (s["startMs"] - op["startMs"]) / 1000
        ax.axvline(at, color=colors["main"], alpha=.3)
        ax.annotate(s["name"].replace("QC.", "") + f'\n{s["durationMs"]:.0f} ms inclusive',
                    xy=(at, s["durationMs"]), xytext=(.15 + i * .29, .88 - i * .19), textcoords="axes fraction",
                    fontsize=8, bbox={"facecolor": "#faf9f3", "edgecolor": "none", "alpha": .9},
                    arrowprops={"arrowstyle": "-", "color": colors["main"]})
    ax.axhline(33.33, color="#aaa", ls=":"); ax.set_xlabel("seconds from real router request"); ax.set_ylabel("milliseconds")
    ax.set_title("One normal transition · scene work underneath the leaf curtain"); ax.legend(fontsize=8)
    fig.tight_layout(); fig.savefig(output / "transition-timeline.png", dpi=160); plt.close(fig)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("input", type=Path); parser.add_argument("output", type=Path)
    parser.add_argument("--plots", action="store_true"); args = parser.parse_args()
    payload = args.input.read_bytes()
    if args.input.suffix == ".gz": payload = gzip.decompress(payload)
    data = json.loads(payload); summary = analyze(data)
    summary["rawJsonSha256"] = hashlib.sha256(payload).hexdigest()
    args.output.mkdir(parents=True, exist_ok=True)
    (args.output / "summary.json").write_text(json.dumps(summary, ensure_ascii=False, indent=2) + "\n")
    with (args.output / "operations.csv").open("w", newline="") as stream:
        writer = csv.writer(stream, lineterminator="\n"); writer.writerow(["label", "tier", "reduced", "elapsed_ms", "frames", "wall_p95_ms", "wall_max_ms", "frames_over_100ms"])
        for op in summary["operations"]:
            writer.writerow([op["label"], op["tier"], op["reduced"], op["elapsedMs"], op["stats"]["frames"], op["stats"]["wallMs"]["p95"], op["stats"]["wallMs"]["max"], op["stats"]["wallOver100Ms"]])
    with (args.output / "frames.csv").open("w", newline="") as stream:
        writer = csv.writer(stream, lineterminator="\n"); writer.writerow(["frame", "timestamp_ms", "wall_ms", "stage", "transition", "tier"])
        writer.writerows([f["frame"], f["timestampMs"], f["wallMs"], f["stage"], f["transition"], f["tier"]] for f in data["frames"])
    if args.plots: plots(data, summary, args.output)
    print(json.dumps({"frames": summary["frameCount"], "spans": summary["spanCount"], "operations": len(summary["operations"]), "terminalStage": summary["terminalStage"]}))


if __name__ == "__main__":
    main()
