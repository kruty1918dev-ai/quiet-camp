#!/usr/bin/env python3
"""Quiet Camp level checker — mirrors Domain/RuleEvaluator + LevelContentValidator.

Usage: python tools/check_levels.py [QC001 ...]   (default: all QC*.json)
Per level prints: validator errors, witness solvability, and a comfort
metric = number of distinct full-board solutions found (capped).
Exit code 1 when any level is invalid/unsolved.
"""
import argparse, json, sys, itertools
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent / "QuietCamp/Assets/QuietCamp/Resources/QuietCamp/Levels"
DELTA = [(0, 1), (1, 0), (0, -1), (-1, 0)]
CONTENT = ROOT.parent

def walking(l, c):
    return inside(l, c) or l.get("ruleVersion") == 2 and list(c) in l.get("exteriorWalkable", [])

def access(l):
    return {tuple(l["entry"])} | {(p["x"], p["z"]) for p in l.get("accessPoints", [])}

def footprint(p):
    x, z = p["x"], p["z"]
    return [(x, z), (x + 1, z), (x + 1, z + 1), (x, z + 1)]

def door(p):
    x, z, r = p["x"], p["z"], p["rotation"]
    return [(x + 1, z + 2), (x + 2, z), (x, z - 1), (x - 1, z + 1)][r]

def inside(l, c):
    return 0 <= c[0] < l["width"] and 0 <= c[1] < l["height"]

def bfs(l, occupied, start, goal):
    if not walking(l, start) or not walking(l, goal):
        return None
    if start in occupied or goal in occupied:
        return None
    q, prev = [start], {start: start}
    for c in q:
        if c == goal:
            path = [c]
            while path[-1] != start:
                path.append(prev[path[-1]])
            return path[::-1]
        for dx, dz in DELTA:
            n = (c[0] + dx, c[1] + dz)
            if walking(l, n) and n not in occupied and n not in prev:
                prev[n] = c
                q.append(n)
    return None

def evaluate(l, placements, require_all=True):
    issues, occupied = [], set(map(tuple, l["blocked"]))
    shaded = set(map(tuple, l["shade"]))
    entry = tuple(l["entry"])
    guests = {g["id"]: g for g in l["guests"]}
    by_id = {}
    for p in placements:
        gid = p.get("guestId")
        if not gid or gid not in guests or gid in by_id:
            issues.append(("guest-id", True)); continue
        by_id[gid] = p
        if not (0 <= p["rotation"] <= 3):
            issues.append(("rotation", True)); continue
        f = footprint(p)
        if any(not inside(l, c) for c in f):
            issues.append(("bounds", True))
        if any(c in occupied or c in access(l) for c in f):
            issues.append(("overlap", True))
        occupied.update(f)
    if any(h for _, h in issues):
        return issues, occupied
    for point in access(l):
        if bfs(l, occupied, entry, point) is None:
            issues.append(("path", False))
    if require_all:
        for g in l["guests"]:
            if g["id"] not in by_id:
                issues.append(("missing", False))
    for p in placements:
        if p.get("guestId") not in by_id or not (0 <= p["rotation"] <= 3):
            continue
        f, g, d = footprint(p), guests[p["guestId"]], door(p)
        if bfs(l, occupied, entry, d) is None:
            issues.append(("path", False))
        if g.get("shade") and any(c not in shaded for c in f):
            issues.append(("shade", False))
        if g.get("quiet") and any(abs(c[0] - n[0]) + abs(c[1] - n[1]) <= 2
                                 for n in l["noise"] for c in f):
            issues.append(("quiet", False))
    for a, b in l.get("friends") or []:
        if a not in by_id or b not in by_id:
            if require_all:
                issues.append(("friends-missing", False))
            continue
        path = bfs(l, occupied, door(by_id[a]), door(by_id[b]))
        if path is None or len(path) - 1 > 3:
            issues.append(("friends", False))
    return issues, occupied

def validate(l):
    e = []
    if l.get("schemaVersion") != 1: e.append("schemaVersion")
    if l.get("ruleVersion") not in (1, 2): e.append("ruleVersion")
    if not l.get("id"): e.append("id")
    if not (4 <= l["width"] <= 8 and 4 <= l["height"] <= 8): e.append("size")
    if l.get("entry") is None or len(l["entry"]) != 2: e.append("entry")
    elif not inside(l, tuple(l["entry"])): e.append("entry:outside")
    elif l["ruleVersion"] == 2 and l["entry"][0] not in (0, l["width"] - 1) and l["entry"][1] not in (0, l["height"] - 1):
        e.append("entry:edge")
    exterior = set()
    for c in l.get("exteriorWalkable", []):
        if len(c) != 2: e.append("exterior:shape"); continue
        cell = tuple(c)
        if l["ruleVersion"] != 2: e.append("exterior:version")
        if inside(l, cell): e.append("exterior:inside")
        if not (-2 <= c[0] <= l["width"] + 1 and -2 <= c[1] <= l["height"] + 1): e.append("exterior:bounds")
        if cell in exterior: e.append("exterior:duplicate")
        exterior.add(cell)
    for name in ("blocked", "shade", "noise"):
        m = l.get(name)
        if m is None: e.append(name + ":null"); continue
        seen = set()
        for c in m:
            cell = tuple(c)
            if not inside(l, cell): e.append(f"{name}:outside:{cell}")
            if cell in seen: e.append(f"{name}:dup:{cell}")
            seen.add(cell)
    if l.get("entry") and tuple(l["entry"]) in set(map(tuple, l.get("blocked") or [])):
        e.append("entry:blocked")
    for p in l.get("accessPoints", []):
        cell = p["x"], p["z"]
        if not inside(l, cell): e.append("access:outside")
        if cell in set(map(tuple, l.get("blocked") or [])): e.append("access:blocked")
    if not e and any(bfs(l, set(map(tuple, l["blocked"])), tuple(l["entry"]), c) is None for c in exterior):
        e.append("exterior:disconnected")
    ids = set()
    gs = l.get("guests")
    if not gs or not (2 <= len(gs) <= 6): e.append("guests:count")
    else:
        for g in gs:
            if not g.get("id"): e.append("guest:id"); continue
            if g["id"] in ids: e.append("guest:dup")
            if not g.get("assetId"): e.append("guest:asset")
            if not g.get("nameKey"): e.append("guest:nameKey")
            ids.add(g["id"])
    for pair in l.get("friends") or []:
        if pair[0] == pair[1] or pair[0] not in ids or pair[1] not in ids:
            e.append("friends:unknown")
    w = l.get("witness")
    if w is not None:
        seen = set()
        for p in w:
            if p.get("guestId") not in ids: e.append("witness:guest")
            elif p["guestId"] in seen: e.append("witness:dup")
            seen.add(p.get("guestId"))
            if not (0 <= p.get("rotation", -1) <= 3): e.append("witness:rotation")
        if not e:
            issues, _ = evaluate(l, w)
            if issues: e.append("witness:unsolved:" + ",".join(c for c, _ in issues))
    return e

def placements_for(l, g):
    """All placements passing bounds + personal (shade/quiet) rules."""
    base = set(map(tuple, l["blocked"])) | access(l)
    shaded = set(map(tuple, l["shade"]))
    out = []
    for x in range(l["width"] - 1):
        for z in range(l["height"] - 1):
            for r in range(4):
                p = {"guestId": g["id"], "x": x, "z": z, "rotation": r}
                f = set(footprint(p))
                if f & base:
                    continue
                if g.get("shade") and not f <= shaded:
                    continue
                if g.get("quiet") and any(abs(c[0] - n[0]) + abs(c[1] - n[1]) <= 2
                                         for n in l["noise"] for c in f):
                    continue
                if not walking(l, door(p)):
                    continue
                out.append(p)
    return out

def count_solutions(l, cap=200):
    """Backtracking count of complete valid layouts (comfort metric)."""
    guests = l["guests"]
    fps, doors, opts = {}, {}, {}
    for g in guests:
        opts[g["id"]] = placements_for(l, g)
        if not opts[g["id"]]:
            return 0, opts
        for p in opts[g["id"]]:
            key = (g["id"], p["x"], p["z"], p["rotation"])
            fps[key] = set(footprint(p))
            doors[key] = door(p)
    order = sorted(guests, key=lambda g: len(opts[g["id"]]))
    friends = [tuple(p) for p in (l.get("friends") or [])]
    count, cur, occ = [0], [], set(map(tuple, l["blocked"]))

    def key(p): return (p["guestId"], p["x"], p["z"], p["rotation"])

    def rec(i):
        nonlocal occ
        if count[0] >= cap:
            return
        if i == len(order):
            if not evaluate(l, cur)[0]:
                count[0] += 1
            return
        g = order[i]
        for p in opts[g["id"]]:
            k, f = key(p), fps[key(p)]
            if f & occ:
                continue
            occ |= f
            d = doors[k]
            ok = bfs(l, occ, tuple(l["entry"]), d) is not None
            if ok:
                ok = all(bfs(l, occ, tuple(l["entry"]), point) is not None for point in access(l))
            if ok:
                ok = all(bfs(l, occ, tuple(l["entry"]), doors[key(previous)]) is not None for previous in cur)
            if ok:
                by_id = {q["guestId"]: q for q in cur} | {g["id"]: p}
                for a, b in friends:
                    if a in by_id and b in by_id:
                        path = bfs(l, occ, doors[key(by_id[a])], doors[key(by_id[b])])
                        if path is None or len(path) - 1 > 3:
                            ok = False
                            break
            if ok:
                cur.append(p)
                rec(i + 1)
                cur.pop()
            occ -= f

    rec(0)
    return count[0], opts

def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("names", nargs="*")
    parser.add_argument("--witness-only", action="store_true")
    parser.add_argument("--cap", type=int, default=200)
    args = parser.parse_args()
    campaign = json.loads((CONTENT / "campaign.json").read_text())
    names = args.names or campaign["mvpLevelIds"] + campaign["generatedLevelIds"]
    bad = 0
    for name in names:
        folder = CONTENT / "GeneratedLevels" if name.startswith("gen:") else ROOT
        l = json.loads((folder / (name.replace(":", "_") + ".json")).read_text())
        errs = validate(l)
        sol, opts = (int(not errs), {}) if args.witness_only else (0, {}) if errs else count_solutions(l, args.cap)
        status = "OK " if not errs else "FAIL"
        print(f"{status} {name}: guests={len(l['guests'])} grid={l['width']}x{l['height']} "
              f"solutions={sol}" + (f" errors={errs}" if errs else ""))
        if errs:
            bad += 1
        elif sol == 0:
            print(f"FAIL {name}: witness ok but no complete solutions?")
            bad += 1
        for g in ([] if args.witness_only else l["guests"]):
            print(f"     {g['id']} shade={g.get('shade')} quiet={g.get('quiet')} "
                  f"opts={len(opts.get(g['id'], []))}")
    sys.exit(1 if bad else 0)

if __name__ == "__main__":
    main()
