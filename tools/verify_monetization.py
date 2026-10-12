#!/usr/bin/env python3
import os
import re
import sys
from pathlib import Path
import subprocess
import tempfile

ROOT = Path(__file__).resolve().parent.parent
PROJECT = ROOT / "QuietCamp"
CACHE_PROJECT = Path(os.environ.get("QC_UNITY_CACHE_PROJECT", str(PROJECT)))
ARTIFACTS = CACHE_PROJECT / "Library/Bee/artifacts"


def main():
    player_sources = "--player-source-check" in sys.argv
    sdk = subprocess.check_output(["dotnet", "--list-sdks"], text=True).splitlines()[-1]
    version, directory = sdk.split(" ", 1)
    compiler = Path(directory.strip("[]")) / version / "Roslyn/bincore/csc.dll"
    assemblies = [("Kruty1918.LevelKit", "Packages/com.kruty1918.levelkit/Runtime"),
                  ("Kruty1918.Atmos", "Packages/com.kruty1918.atmos/Runtime"),
                  ("Kruty1918.LevelGen", "Packages/com.kruty1918.levelgen/Runtime"),
                  ("Kruty1918.LevelGen.LevelKitBridge", "Packages/com.kruty1918.levelgen/Runtime/LevelKitBridge"),
                  ("Kruty1918.AgentVerify", "Packages/com.kruty1918.agentverify/Runtime"),
                  ("Kruty1918.ScenePlacement.Core", "Packages/com.kruty1918.scene-placement/Runtime/Core"),
                  ("Kruty1918.VegetationPlacement.Core", "Packages/com.kruty1918.vegetation-placement/Runtime/Core"),
                  ("Kruty1918.RockPlacement.Core", "Packages/com.kruty1918.rock-placement/Runtime/Core"),
                  ("Domain", "Scripts/Domain"), ("Application", "Scripts/Application"),
                  ("Infrastructure", "Scripts/Infrastructure"), ("Presentation", "Scripts/Presentation"),
                  ("Tests.Editor", "Tests/Editor"), ("Tests.PlayMode", "Tests/PlayMode")]
    if player_sources:
        assemblies = [(name, folder) for name, folder in assemblies if not name.startswith("Tests.")]
    if "--include-editor" in sys.argv and not player_sources:
        assemblies.append(("Editor", "Editor"))
    checks=[]
    with tempfile.TemporaryDirectory(prefix="quietcamp-monetization-compile-") as temporary:
        output = Path(temporary)
        compiled = {}
        for name, folder in assemblies:
            is_package = name.startswith("Kruty1918.")
            assembly = name if is_package else "QuietCamp." + name
            template = assembly if is_package and name.endswith(".Core") else "QuietCamp.Presentation" if is_package else assembly
            candidates = [path for path in ARTIFACTS.glob("*E*.dag/" + template + ".rsp")
                          if re.fullmatch(r"[0-9a-f]+E(?:Dbg|Rel)?\.dag", path.parent.name)]
            if not candidates:
                raise RuntimeError("Missing existing Unity compiler response: " + assembly)
            original = max(candidates, key=lambda path: path.stat().st_mtime)
            options = []
            for line in original.read_text().splitlines():
                if line.startswith(("-out:", "-refout:")) or line.strip('"').endswith(".cs"):
                    continue
                if player_sources and line.startswith("-define:"):
                    symbols = re.split(r"[;,]", line[len("-define:"):])
                    symbols = [symbol for symbol in symbols if not symbol.startswith("UNITY_EDITOR")
                               and symbol != "UNITY_INCLUDE_TESTS"]
                    if not symbols:
                        continue
                    line = "-define:" + ";".join(symbols)
                if line.startswith("/additionalfile:"):
                    additional = Path(line.split(":",1)[1].strip('"'))
                    if not additional.is_absolute() and CACHE_PROJECT != PROJECT:
                        line = '/additionalfile:"' + str(CACHE_PROJECT / additional) + '"'
                if line.startswith("-r:"):
                    reference = Path(line[3:].strip('"'))
                    stem = reference.name.removesuffix(".ref.dll").removesuffix(".dll")
                    if player_sources and (stem.startswith("UnityEditor") or ".Editor" in stem
                                           or stem.startswith("QuietCamp.Tests")):
                        continue
                    if is_package and stem.startswith("QuietCamp."):
                        continue
                    if stem in compiled:
                        line = '-r:"' + str(compiled[stem]) + '"'
                    elif not reference.is_absolute() and not (PROJECT / reference).exists():
                        fallback = CACHE_PROJECT / reference
                        if not fallback.exists():
                            fallback = CACHE_PROJECT / "Library/ScriptAssemblies" / (stem + ".dll")
                        if fallback.exists():
                            line = '-r:"' + str(fallback) + '"'
                options.append(line)
            # Read new declared assembly dependencies even before Unity regenerates Bee's rsp.
            import json
            declaration = PROJECT / "Assets/QuietCamp" / folder / (assembly + ".asmdef")
            if declaration.exists():
                for reference in json.loads(declaration.read_text()).get("references", []):
                    cached = CACHE_PROJECT / "Library/ScriptAssemblies" / (reference + ".dll")
                    if not reference.startswith("GUID:") and cached.exists() and not any(Path(line[3:].strip('"')).name in (reference + ".dll", reference + ".ref.dll") for line in options if line.startswith("-r:")):
                        options.append('-r:"' + str(cached) + '"')
            destination = output / (assembly + ".dll")
            options.extend(['-out:"' + str(destination) + '"', '-refout:"' + str(output / (assembly + ".ref.dll")) + '"'])
            options.append('-parallel-')
            source_root = PROJECT / folder if is_package else PROJECT / "Assets/QuietCamp" / folder
            sources = sorted(source_root.rglob("*.cs"))
            if name == "Kruty1918.LevelGen":
                sources = [path for path in sources if "LevelKitBridge" not in path.parts]
            if name == "Kruty1918.LevelGen.LevelKitBridge":
                options.append("-define:KRUTY1918_LEVELKIT")
            # The saved Bee graph predates the restored packages. Include their
            # freshly compiled references rather than relying on stale caches.
            options.extend('-r:"' + str(path) + '"' for key, path in compiled.items()
                           if key.startswith("Kruty1918."))
            options.extend('"' + str(path) + '"' for path in sources)
            response = output / (assembly + ".rsp")
            response.write_text("\n".join(options) + "\n")
            result = subprocess.run(["dotnet", str(compiler), "@" + str(response)], cwd=PROJECT,
                                    env=dict(os.environ, DOTNET_CLI_TELEMETRY_OPTOUT="1"), capture_output=True, text=True)
            if result.returncode != 0:
                print(result.stdout + result.stderr, flush=True)
                result.check_returncode()
            compiled[assembly] = destination
            if player_sources and name in ("Infrastructure", "Presentation"):
                metadata = destination.read_bytes()
                for editor_type in (b"RoadmapEditorArchive", b"RoadmapWorldPresenter", b"RoadmapWorldInput",
                                    b"RoadmapWorldAsset", b"RoadmapWorldChunk"):
                    assert editor_type not in metadata, "Editor-only map type in player-conditioned assembly: " + str(editor_type)
            warnings = (result.stdout + result.stderr).count(": warning ")
            import hashlib
            checks.append(dict(assembly=assembly,warnings=warnings,sources={str(path.relative_to(ROOT)):hashlib.sha256(path.read_bytes()).hexdigest() for path in sources}))
            label = "player-conditioned source compiler check" if player_sources else "current-source compiler check"
            print("PASS " + label + ": " + assembly + " (warnings: " + str(warnings) + ")", flush=True)
    print("Compiler checks only; no Unity import, player build or runtime scene check performed.")
    if '--receipt' in sys.argv:
        import datetime,json
        destination=Path(sys.argv[sys.argv.index('--receipt')+1]);destination.parent.mkdir(parents=True,exist_ok=True)
        destination.write_text(json.dumps(dict(checkedUtc=datetime.datetime.now(datetime.timezone.utc).isoformat(),checks=checks,cachedReferences=True,nativeVerified=False,unityLaunched=False,playerBuild=False),indent=2)+'\n')


if __name__ == "__main__":
    main()
