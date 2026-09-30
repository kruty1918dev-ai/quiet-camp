#!/usr/bin/env python3
"""Writes the 10 hand-authored Quiet Camp levels (QC001-QC010)."""
import json, hashlib, random
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent / "QuietCamp/Assets/QuietCamp/Resources/QuietCamp/Levels"

def guest(i, name_i, shade=False, quiet=False, big=False):
    return {"id": f"g{i}", "nameKey": f"guest.{name_i}",
            "assetId": "tent_detailedOpen" if big else "tent_smallOpen",
            "shade": shade, "quiet": quiet}

def w(g, x, z, r):
    return {"x": x, "z": z, "rotation": r, "guestId": g}

LEVELS = [
    # L1 — Перший вечір: 2 guests, open field, tutorial.1
    dict(id="QC001", order=1, w=5, h=5, entry=[4, 4],
         blocked=[], shade=[], noise=[],
         guests=[guest(1, 1), guest(2, 2)],
         friends=[], witness=[w("g1", 0, 0, 0), w("g2", 2, 3, 2)],
         lighting="day", tutorial="tutorial.1"),
    # L2 — Перестановка: 3 guests, tutorial.2
    dict(id="QC002", order=2, w=5, h=5, entry=[4, 4],
         blocked=[], shade=[], noise=[],
         guests=[guest(1, 1), guest(2, 2), guest(3, 3)],
         friends=[], witness=[w("g1", 0, 0, 0), w("g2", 3, 0, 0), w("g3", 0, 3, 1)],
         lighting="day", tutorial="tutorial.2"),
    # L3 — Доріжка до дверей: off-center rock, tutorial.3
    dict(id="QC003", order=3, w=5, h=5, entry=[4, 4],
         blocked=[[3, 2]], shade=[], noise=[],
         guests=[guest(1, 1), guest(2, 2), guest(3, 3)],
         friends=[], witness=[w("g1", 0, 0, 1), w("g2", 3, 0, 0), w("g3", 0, 3, 1)],
         lighting="day", tutorial="tutorial.3"),
    # L4 — Під кронами: shade corner, tutorial.4
    dict(id="QC004", order=4, w=5, h=5, entry=[4, 4],
         blocked=[], shade=[[0, 0], [1, 0], [2, 0], [0, 1], [1, 1], [2, 1]], noise=[],
         guests=[guest(1, 1), guest(2, 2), guest(3, 3, shade=True)],
         friends=[], witness=[w("g1", 3, 2, 2), w("g2", 0, 3, 1), w("g3", 0, 0, 0)],
         lighting="day", tutorial="tutorial.4"),
    # L5 — Куди двері?: rock wall, door must face the corridor, tutorial.5
    dict(id="QC005", order=5, w=5, h=5, entry=[4, 4],
         blocked=[[1, 2], [2, 2], [3, 2]], shade=[], noise=[],
         guests=[guest(1, 1), guest(2, 2)],
         friends=[], witness=[w("g1", 1, 0, 1), w("g2", 0, 3, 1)],
         lighting="day", tutorial="tutorial.5"),
    # L6 — Тісний куточок: 6x6, corner rocks, tutorial.6
    dict(id="QC006", order=6, w=6, h=6, entry=[5, 5],
         blocked=[[0, 0], [1, 0], [0, 1]], shade=[], noise=[],
         guests=[guest(1, 1), guest(2, 2), guest(3, 3)],
         friends=[], witness=[w("g1", 3, 0, 0), w("g2", 0, 3, 1), w("g3", 3, 3, 2)],
         lighting="day", tutorial="tutorial.6"),
    # L7 — Друзі поруч: friends pair, tutorial.7
    dict(id="QC007", order=7, w=6, h=6, entry=[5, 5],
         blocked=[], shade=[], noise=[],
         guests=[guest(1, 1), guest(2, 2), guest(3, 3), guest(4, 4)],
         friends=[["g1", "g2"]],
         witness=[w("g1", 0, 0, 1), w("g2", 3, 0, 3), w("g3", 0, 3, 1), w("g4", 3, 3, 0)],
         lighting="day", tutorial="tutorial.7"),
    # L8 — Тихий куточок: campfire + quiet guest, tutorial.8
    dict(id="QC008", order=8, w=6, h=6, entry=[5, 5],
         blocked=[], shade=[], noise=[[0, 0]],
         guests=[guest(1, 1), guest(2, 2), guest(3, 3), guest(4, 4, quiet=True)],
         friends=[],
         witness=[w("g1", 0, 2, 1), w("g2", 3, 0, 0), w("g3", 0, 4, 1), w("g4", 3, 3, 0)],
         lighting="day", tutorial="tutorial.8"),
    # L9 — Сімейний табір: 7x7, all mechanics, no tutorial
    dict(id="QC009", order=9, w=7, h=7, entry=[6, 6],
         blocked=[[0, 1]], shade=[[0, 5], [1, 5], [2, 5], [0, 6], [1, 6], [2, 6]],
         noise=[[0, 0]],
         guests=[guest(1, 1), guest(2, 2), guest(3, 3, shade=True),
                 guest(4, 4), guest(5, 5, quiet=True)],
         friends=[["g1", "g2"]],
         witness=[w("g1", 1, 0, 0), w("g2", 3, 0, 0), w("g3", 0, 5, 2),
                  w("g4", 5, 0, 0), w("g5", 3, 3, 2)],
         lighting="day", tutorial=""),
    # L10 — Вечір біля вогню: finale, 6 guests, evening
    dict(id="QC010", order=10, w=7, h=7, entry=[6, 6],
         blocked=[[0, 1], [3, 3]],
         shade=[[4, 0], [5, 0], [6, 0], [4, 1], [5, 1], [6, 1]],
         noise=[[0, 0]],
         guests=[guest(1, 1), guest(2, 2), guest(3, 3, shade=True),
                 guest(4, 4), guest(5, 5, quiet=True), guest(6, 6, big=True)],
         friends=[["g1", "g2"]],
         witness=[w("g1", 0, 2, 0), w("g2", 3, 5, 2), w("g3", 5, 0, 0),
                  w("g4", 1, 0, 1), w("g5", 0, 5, 1), w("g6", 4, 3, 0)],
         lighting="evening", tutorial=""),
]

def main():
    rng = random.Random(20241010)
    for spec in LEVELS:
        doc = {
            "schemaVersion": 1,
            "ruleVersion": 1,
            "id": spec["id"],
            "order": spec["order"],
            "chapter": 1,
            "seed": rng.randrange(1, 2**31),
            "generatorVersion": "levelkit-hand-1",
            "generationAttempt": 0,
            "width": spec["w"],
            "height": spec["h"],
            "entry": spec["entry"],
            "blocked": spec["blocked"],
            "shade": spec["shade"],
            "noise": spec["noise"],
            "guests": spec["guests"],
            "friends": spec["friends"],
            "witness": spec["witness"],
            "lighting": spec["lighting"],
            "tutorialKey": spec["tutorial"],
            "decorSeed": rng.randrange(1, 2**31),
            "contentHash": "",
        }
        text = json.dumps(doc, indent=1) + "\n"
        doc["contentHash"] = hashlib.sha256(text.encode("utf-8")).hexdigest()
        path = ROOT / (spec["id"] + ".json")
        path.write_text(json.dumps(doc, indent=1) + "\n", encoding="utf-8")
        print("wrote", path.name)

if __name__ == "__main__":
    main()
