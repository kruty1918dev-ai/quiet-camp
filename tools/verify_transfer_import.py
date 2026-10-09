#!/usr/bin/env python3
"""Check local donor payloads against layered receipts, without starting Unity."""
import hashlib
import json
import re
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
RECEIPTS = ROOT / "docs/transfer/2026-10-09/import"


def main():
    expected = {}
    for name in ["city", "meadow", "swamp", "particles", "battle-royale", "abilities", "nature-urp"]:
        receipt = json.loads((RECEIPTS / (name + ".json")).read_text())
        base = ROOT / "QuietCamp"
        if name == "abilities":
            base /= "OptionalAssets~/Abilities"
        for row in receipt["entries"]:
            if name == "particles":
                expected[base / row["path"]] = row
            elif not row["action"].startswith("excluded"):
                for part, info in row["parts"].items():
                    if row["folder"] and part == "asset":
                        continue
                    path = base / (row["path"] + (".meta" if part == "asset.meta" else ""))
                    expected[path] = info
    for path, info in expected.items():
        with path.open("rb") as stream:
            actual = hashlib.file_digest(stream, "sha256").hexdigest()
        if path.stat().st_size != info["bytes"] or actual != info["sha256"]:
            raise ValueError("Missing/changed imported payload: " + str(path.relative_to(ROOT)))
    guids = {}
    for meta in (ROOT / "QuietCamp/Assets").rglob("*.meta"):
        match = re.search(rb"(?m)^guid: ([0-9a-f]{32})", meta.read_bytes())
        if match:
            guid = match[1].decode()
            if guid in guids:
                raise ValueError("Duplicate Unity GUID: " + str(meta) + " / " + str(guids[guid]))
            guids[guid] = meta
    result = {"passed": True, "payloadFilesVerified": len(expected),
              "activeAssetGuidsVerified": len(guids), "nativeImport": False,
              "editorLaunched": False, "playerBuild": False}
    (RECEIPTS / "verification.json").write_text(json.dumps(result, indent=2) + "\n")
    print(json.dumps(result))


if __name__ == "__main__":
    main()
