#!/usr/bin/env python3
"""Check local donor payloads against layered receipts, without starting Unity."""
import argparse
import hashlib
import json
import re
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
RECEIPTS = ROOT / "docs/transfer/2026-10-09/import"


def expected_payloads():
    expected = {}
    guid_receipt = RECEIPTS / "particles-guids.json"
    particle_guids = json.loads(guid_receipt.read_text()) if guid_receipt.exists() else {}
    for name in ["city", "meadow", "swamp", "particles", "battle-royale", "abilities", "nature-urp"]:
        receipt = json.loads((RECEIPTS / (name + ".json")).read_text())
        base = ROOT / "QuietCamp"
        if name == "abilities":
            base /= "OptionalAssets~/Abilities"
        for row in receipt["entries"]:
            if name == "particles":
                info = dict(row)
                if row["path"].endswith(".meta") and row["path"] in particle_guids:
                    info["guid"] = particle_guids[row["path"]]
                expected[base / row["path"]] = info
            elif not row["action"].startswith("excluded"):
                for part, info in row["parts"].items():
                    if row["folder"] and part == "asset":
                        continue
                    path = base / (row["path"] + (".meta" if part == "asset.meta" else ""))
                    expected[path] = dict(info)
                    if part == "asset.meta":
                        expected[path]["guid"] = row["guid"]
    return expected


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--record-native-adaptations", action="store_true",
                        help="Record explicitly requested Unity material/metadata upgrades; reject raw-data drift")
    args = parser.parse_args()
    expected = expected_payloads()
    native_path = RECEIPTS / "native-import.json"
    native = json.loads(native_path.read_text()) if native_path.exists() else {}
    adaptation_path = RECEIPTS / "native-adaptations.json"
    if args.record_native_adaptations:
        if not native.get("passed") or not native.get("nativeAssetDatabaseImport"):
            raise ValueError("A successful native import receipt is required before recording upgrades")
        changes = []
        for path, info in expected.items():
            with path.open("rb") as stream:
                actual = hashlib.file_digest(stream, "sha256").hexdigest()
            if actual == info["sha256"] and path.stat().st_size == info["bytes"]:
                continue
            if not path.is_relative_to(ROOT / "QuietCamp/Assets") or path.suffix not in {".meta", ".mat"}:
                raise ValueError("Unexpected raw-data change: " + str(path.relative_to(ROOT)))
            # The donor GUID from the layered package receipt must survive native serialization.
            if path.suffix == ".meta":
                matched = re.search(rb"(?m)^guid: ([0-9a-f]{32})", path.read_bytes())
                if not matched or matched[1].decode() != info.get("guid"):
                    raise ValueError("Native metadata lost its GUID: " + str(path))
            changes.append({"path": str(path.relative_to(ROOT)), "originalSha256": info["sha256"],
                            "bytes": path.stat().st_size, "sha256": actual})
        receipt = {"kind": "Unity 6000.6 material/metadata serialization and scoped URP upgrade",
                   "nativeReportSha256": hashlib.sha256(native_path.read_bytes()).hexdigest(),
                   "adaptedFiles": len(changes), "entries": changes}
        adaptation_path.write_text(json.dumps(receipt, indent=2) + "\n")
    adaptations = json.loads(adaptation_path.read_text()) if adaptation_path.exists() else {"entries": []}
    for row in adaptations["entries"]:
        path = ROOT / row["path"]
        if path not in expected or row["originalSha256"] != expected[path]["sha256"]:
            raise ValueError("Upgrade receipt is not bound to the original payload: " + row["path"])
        if path.suffix not in {".mat", ".meta"} or not path.is_relative_to(ROOT / "QuietCamp/Assets"):
            raise ValueError("Upgrade receipt contains a non-material/non-metadata payload")
        expected[path] = dict(row, guid=expected[path].get("guid"))
    for path, info in expected.items():
        with path.open("rb") as stream:
            actual = hashlib.file_digest(stream, "sha256").hexdigest()
        if path.stat().st_size != info["bytes"] or actual != info["sha256"]:
            raise ValueError("Missing/changed imported payload: " + str(path.relative_to(ROOT)))
        if info.get("guid"):
            match = re.search(rb"(?m)^guid: ([0-9a-f]{32})", path.read_bytes())
            if not match or match[1].decode() != info["guid"]:
                raise ValueError("Imported GUID changed: " + str(path.relative_to(ROOT)))
    guids = {}
    for meta in (ROOT / "QuietCamp/Assets").rglob("*.meta"):
        match = re.search(rb"(?m)^guid: ([0-9a-f]{32})", meta.read_bytes())
        if match:
            guid = match[1].decode()
            if guid in guids:
                raise ValueError("Duplicate Unity GUID: " + str(meta) + " / " + str(guids[guid]))
            guids[guid] = meta
    result = {"passed": True, "payloadFilesVerified": len(expected),
              "nativeAdaptedFilesVerified": len(adaptations["entries"]),
              "activeAssetGuidsVerified": len(guids), "nativeImportReceiptPassed": native.get("passed", False),
              "editorLaunchedByVerifier": False, "playerBuild": False}
    (RECEIPTS / "verification.json").write_text(json.dumps(result, indent=2) + "\n")
    print(json.dumps(result))


if __name__ == "__main__":
    main()
