#!/usr/bin/env python3
"""Stage the 30-level campaign using the actual C# Domain solver.

Default mode checks and stages only. --write archives all current hashes and
publishes the complete validated set with rollback on filesystem failure.
This runs a .NET authoring tool, not a Unity player build.
"""
import argparse
import json
import os
from pathlib import Path
import subprocess
import uuid
from xml.sax.saxutils import escape

ROOT = Path(__file__).resolve().parent.parent
CONTENT = ROOT / "QuietCamp/Assets/QuietCamp/Resources/QuietCamp"


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--write", action="store_true")
    args = parser.parse_args()
    cache = Path.home() / ".cache/quietcamp/content-authoring"
    cache.mkdir(parents=True, exist_ok=True)
    dll = next((ROOT / "QuietCamp/Library/PackageCache").glob("com.unity.nuget.newtonsoft-json@*/Runtime/Newtonsoft.Json.dll"))
    domain = ROOT / "QuietCamp/Assets/QuietCamp/Scripts/Domain"
    project = cache / "Authoring.csproj"
    sdk_versions = subprocess.check_output(["dotnet", "--list-sdks"], text=True)
    major = max(int(line.split(".", 1)[0]) for line in sdk_versions.splitlines())
    if major < 6:
        raise RuntimeError("A .NET 6 or later SDK is required for the authoring harness")
    project.write_text(f'''<Project Sdk="Microsoft.NET.Sdk">
<PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net{major}.0</TargetFramework><EnableDefaultCompileItems>false</EnableDefaultCompileItems></PropertyGroup>
<ItemGroup><Compile Include="{escape(str(domain))}/*.cs"/><Compile Include="{escape(str(ROOT / 'tools/campaign-authoring/Program.cs'))}"/>
<Reference Include="Newtonsoft.Json"><HintPath>{escape(str(dll))}</HintPath></Reference></ItemGroup></Project>
''')
    staged_json = cache / "campaign-staged.json"
    env = dict(os.environ, DOTNET_CLI_TELEMETRY_OPTOUT="1", DOTNET_NOLOGO="1")
    subprocess.run(["dotnet", "run", "--project", str(project), "--", str(staged_json)], cwd=ROOT, env=env, check=True)
    levels = json.loads(staged_json.read_text())
    if len(levels) != 49:
        raise RuntimeError("Incomplete staged campaign")
    campaign_path = CONTENT / "campaign.json"
    campaign = json.loads(campaign_path.read_text())
    ids = campaign["mvpLevelIds"] + campaign["generatedLevelIds"]
    # The staged set appends side-content (memories journey + bonus glades)
    # after the campaign block; only the ordered campaign must match.
    if ids != [level["id"] for level in levels[:len(ids)]]:
        raise RuntimeError("Campaign order/IDs changed")
    publish = {}
    summaries = []
    for level in levels:
        level_id = level["id"]
        folder = "GeneratedLevels" if level_id.startswith("gen:") else "Levels"
        path = CONTENT / folder / (level_id.replace(":", "_") + ".json")
        # New levels have nothing to archive — their hash list entry is added below.
        current = json.loads(path.read_text()) if path.exists() else {"contentHash": ""}
        content_hash = current["contentHash"]
        if content_hash and (len(content_hash) != 64 or any(c not in "0123456789abcdef" for c in content_hash)):
            raise RuntimeError("Unsafe archive hash")
        archive = CONTENT / "ContentRevisions" / (content_hash + ".json")
        if path.exists() and current["contentHash"] != level["contentHash"] and not archive.exists():
            # Preserve the exact pre-update bytes, never reinterpret old data.
            publish[archive] = path.read_bytes()
        publish[path] = (json.dumps(level, ensure_ascii=False, indent=2) + "\n").encode()
        # Levels without a prior layout still need a baseline snapshot so old
        # saves carrying only a contentHash can resolve through CampContent.Legacy.
        legacy = CONTENT / "LegacyLevels" / (level_id.replace(":", "_") + ".json")
        if not legacy.exists():
            publish[legacy] = publish[path]
        summaries.append(dict(id=level_id, number=level["order"], width=level["width"], height=level["height"],
            decorSeed=level["decorSeed"], lighting=level["lighting"], environmentPreset=level["environmentPreset"],
            entry=level["entry"], accessPoints=level["accessPoints"], mapObjects=level["objects"], canopies=level["canopies"],
            exteriorWalkable=level["exteriorWalkable"], environment=level["environment"],
            shade=any(g["shade"] for g in level["guests"]), quiet=any(g["quiet"] for g in level["guests"]),
            friends=bool(level["friends"]), fire=bool(level["noise"])))
    campaign["contentVersion"] = "cozy-campaign-3"
    campaign["levels"] = [dict(id=level["id"], contentHash=level["contentHash"]) for level in levels[:len(ids)]]
    publish[CONTENT / "level_summaries.json"] = (json.dumps(summaries, ensure_ascii=False, indent=2) + "\n").encode()
    publish[campaign_path] = (json.dumps(campaign, ensure_ascii=False, indent=2) + "\n").encode()
    if not args.write:
        print(f"Staged {len(publish)} files; use --write to publish after review.")
        return
    originals = {path: path.read_bytes() if path.exists() else None for path in publish}
    temporary = {}
    try:
        for path, content in publish.items():
            path.parent.mkdir(parents=True, exist_ok=True)
            tmp = path.with_name(path.name + ".campaign-tmp")
            tmp.write_bytes(content)
            temporary[path] = tmp
        for path, tmp in temporary.items():
            os.replace(tmp, path)
    except BaseException:
        for path, original in originals.items():
            if original is None:
                path.unlink(missing_ok=True)
            else:
                path.write_bytes(original)
        raise
    finally:
        for tmp in temporary.values():
            tmp.unlink(missing_ok=True)
    for path in publish:
        if path.suffix == ".json" and not path.with_suffix(".json.meta").exists():
            path.with_suffix(".json.meta").write_text(f"fileFormatVersion: 2\nguid: {uuid.uuid4().hex}\nTextScriptImporter:\n  externalObjects: {{}}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n")
    print(f"Published {len(levels)} levels; old hashes archived; IDs/order/progress keys preserved.")


if __name__ == "__main__":
    main()
