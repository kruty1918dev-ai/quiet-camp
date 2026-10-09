#!/usr/bin/env python3
"""Restore a unitypackage's Assets tree without launching Unity or executing code.

Preserves original metadata, rejects unsafe paths/GUID collisions and writes a
receipt. Unity's AssetDatabase import and shader conversion remain separate.
"""
import argparse
import gzip
import hashlib
import json
import re
import shutil
import tarfile
from pathlib import Path, PurePosixPath

GUID = re.compile(r"^[0-9a-f]{32}$")
META_GUID = re.compile(rb"(?m)^guid: ([0-9a-f]{32})\s*$")


def digest(path):
    with path.open("rb") as stream:
        return hashlib.file_digest(stream, "sha256").hexdigest()


def asset_path(raw):
    value = raw.decode("utf-8-sig")
    # Older supplied packages have the literal suffix LF + 00 in pathname.
    # This is packaging padding, not part of the intended Unity filename.
    corrected = value.endswith("\n00")
    if corrected:
        value = value[:-3]
    value = value.rstrip("\r\n")
    path = PurePosixPath(value)
    if (path.is_absolute() or ".." in path.parts or not path.parts
            or path.parts[0] != "Assets" or "\\" in value
            or any(ord(c) < 32 for c in value) or ":" in value):
        raise ValueError("Unsafe asset path: " + repr(value))
    return str(path), corrected


def members(package):
    # gzip handles Unity's FEXTRA header; tarfile's direct r|gz does not.
    with gzip.open(package, "rb") as stream:
        with tarfile.open(fileobj=stream, mode="r|") as archive:
            seen = set()
            for item in archive:
                name = item.name.removeprefix("./").rstrip("/")
                parts = name.split("/")
                if name == ".icon.png" and item.isfile():
                    continue
                if item.isdir() and len(parts) == 1 and GUID.fullmatch(parts[0]):
                    continue
                if (not item.isfile() or len(parts) != 2
                        or not GUID.fullmatch(parts[0])
                        or parts[1] not in ("pathname", "asset", "asset.meta", "preview.png")
                        or name in seen):
                    raise ValueError("Unsafe/duplicate tar member: " + name)
                seen.add(name)
                yield parts[0], parts[1], item.size, archive.extractfile(item)


def inventory(package):
    rows = {}
    for guid, part, size, stream in members(package):
        row = rows.setdefault(guid, {"guid": guid, "parts": {}})
        if part == "pathname":
            row["path"], row["normalizedPathname"] = asset_path(stream.read())
        elif part in ("asset", "asset.meta"):
            if part == "asset.meta":
                raw = stream.read()
                match = META_GUID.search(raw)
                if not match or match[1].decode() != guid:
                    raise ValueError("Missing/mismatched metadata GUID: " + guid)
                row["folder"] = b"folderAsset: yes" in raw
                checksum = hashlib.sha256(raw).hexdigest()
            else:
                checksum = hashlib.file_digest(stream, "sha256").hexdigest()
            row["parts"][part] = {"bytes": size, "sha256": checksum}
    folded = set()
    parent_paths = {str(parent) for row in rows.values() if "path" in row
                    for parent in PurePosixPath(row["path"]).parents}
    for row in rows.values():
        if "path" not in row or "asset.meta" not in row["parts"]:
            raise ValueError("Incomplete package entry: " + row["guid"])
        if row["path"].casefold() in folded:
            raise ValueError("Duplicate/case-colliding path: " + row["path"])
        folded.add(row["path"].casefold())
        if not row["folder"] and "asset" not in row["parts"] and row["path"] in parent_paths:
            row["folder"] = True
            row["inferredFolder"] = True
        if not row["folder"] and "asset" not in row["parts"]:
            raise ValueError("Missing asset: " + row["path"])
    return rows


def remapped(raw, mapping):
    if not mapping:
        return raw
    return re.sub(rb"[0-9a-f]{32}", lambda m: mapping.get(m[0].decode(), m[0].decode()).encode(), raw)


def import_package(package, project, roots, replace=False, apply=False, guid_remap=None):
    rows = inventory(package)
    guid_remap = guid_remap or {}
    if (any(not GUID.fullmatch(k) or not GUID.fullmatch(v) for k, v in guid_remap.items())
            or len(set(guid_remap.values())) != len(guid_remap)):
        raise ValueError("Invalid GUID remapping")
    if guid_remap:
        for guid, part, size, stream in members(package):
            row = rows[guid]
            if part == "asset.meta" or (part == "asset" and PurePosixPath(row["path"]).suffix
                    in (".prefab", ".mat", ".asset", ".unity", ".shadergraph", ".json", ".asmdef")):
                raw = remapped(stream.read(), guid_remap)
                row["parts"][part]["sourceSha256"] = row["parts"][part]["sha256"]
                row["parts"][part]["sha256"] = hashlib.sha256(raw).hexdigest()
            if guid in guid_remap:
                row["originalGuid"] = guid
                row["guid"] = guid_remap[guid]
    assets = project / "Assets"
    guids = {}
    if assets.exists():
        for meta in assets.rglob("*.meta"):
            match = META_GUID.search(meta.read_bytes())
            if match:
                key = match[1].decode()
                if key in guids and guids[key] != meta:
                    raise ValueError("Existing duplicate GUID: " + key)
                guids[key] = meta
    paths = {str(p.relative_to(project)).casefold(): p for p in assets.rglob("*")} if assets.exists() else {}
    actions = {}
    for guid, row in rows.items():
        installed_guid = row["guid"]
        path = row["path"]
        if not any(path == root or path.startswith(root + "/") for root in roots):
            row["action"] = "excluded-outside-selected-vendor-roots"
            continue
        destination = project / path
        if not destination.resolve().is_relative_to(project.resolve()):
            raise ValueError("Destination escapes project: " + path)
        for parent in destination.parents:
            if parent == project:
                break
            if parent.is_symlink():
                raise ValueError("Symlink destination: " + str(parent))
        existing = paths.get(path.casefold())
        if existing is not None and existing != destination:
            raise ValueError("Existing case collision: " + path)
        meta = Path(str(destination) + ".meta")
        if installed_guid in guids and guids[installed_guid] != meta:
            raise ValueError("GUID already belongs to " + str(guids[installed_guid]))
        if meta.exists():
            match = META_GUID.search(meta.read_bytes())
            if not match or match[1].decode() != installed_guid:
                raise ValueError("Destination has a different GUID: " + path)
        row["action"] = "restore"
        for part, info in row["parts"].items():
            target = meta if part == "asset.meta" else destination
            if row["folder"] and part == "asset":
                continue
            if target.exists():
                if not target.is_file():
                    raise ValueError("Expected file: " + str(target))
                if digest(target) == info["sha256"]:
                    actions[(guid, part)] = "identical"
                elif replace:
                    actions[(guid, part)] = "replace"
                    row["action"] = "replace-same-guid"
                else:
                    raise ValueError("Different existing payload: " + str(target))
            else:
                actions[(guid, part)] = "restore"
    if apply:
        for row in rows.values():
            if row["action"].startswith("excluded"):
                continue
            target = project / row["path"]
            if row["folder"]:
                target.mkdir(parents=True, exist_ok=True)
            else:
                target.parent.mkdir(parents=True, exist_ok=True)
        for guid, part, size, stream in members(package):
            action = actions.get((guid, part))
            if action not in ("restore", "replace"):
                continue
            row = rows[guid]
            target = project / (row["path"] + (".meta" if part == "asset.meta" else ""))
            temporary = target.with_name(target.name + ".importing")
            try:
                with temporary.open("xb") as output:
                    if "sourceSha256" in row["parts"][part]:
                        output.write(remapped(stream.read(), guid_remap))
                    else:
                        shutil.copyfileobj(stream, output, 1024 * 1024)
                if temporary.stat().st_size != size or digest(temporary) != row["parts"][part]["sha256"]:
                    raise ValueError("Restored payload mismatch: " + row["path"])
                temporary.replace(target)
            finally:
                temporary.unlink(missing_ok=True)
    return {"package": package.name, "packageSha256": digest(package),
            "applied": apply, "unityLaunched": False, "selectedRoots": roots,
            "guidRemap": guid_remap,
            "entries": list(rows.values()),
            "normalizedPathnames": sum(r["normalizedPathname"] for r in rows.values()),
            "restoredFiles": sum(a == "restore" for a in actions.values()),
            "replacedFiles": sum(a == "replace" for a in actions.values()),
            "identicalFiles": sum(a == "identical" for a in actions.values())}


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("package", type=Path)
    parser.add_argument("--project", type=Path, required=True)
    parser.add_argument("--root", action="append", required=True)
    parser.add_argument("--replace-same-guid", action="store_true")
    parser.add_argument("--apply", action="store_true")
    parser.add_argument("--receipt", type=Path, required=True)
    parser.add_argument("--guid-remap", type=Path)
    args = parser.parse_args()
    receipt = import_package(args.package, args.project.resolve(), args.root,
                             args.replace_same_guid, args.apply,
                             json.loads(args.guid_remap.read_text()) if args.guid_remap else None)
    args.receipt.parent.mkdir(parents=True, exist_ok=True)
    args.receipt.write_text(json.dumps(receipt, indent=2, ensure_ascii=False) + "\n")
    print(json.dumps({k: v for k, v in receipt.items() if k != "entries"}, ensure_ascii=False))


if __name__ == "__main__":
    main()
