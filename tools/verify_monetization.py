#!/usr/bin/env python3
import os
from pathlib import Path
import subprocess
import tempfile

ROOT = Path(__file__).resolve().parent.parent
PROJECT = ROOT / "QuietCamp"
ARTIFACTS = PROJECT / "Library/Bee/artifacts/1300b0aEDbg.dag"


def main():
    sdk = subprocess.check_output(["dotnet", "--list-sdks"], text=True).splitlines()[-1]
    version, directory = sdk.split(" ", 1)
    compiler = Path(directory.strip("[]")) / version / "Roslyn/bincore/csc.dll"
    assemblies = [("Domain", "Scripts/Domain"), ("Application", "Scripts/Application"),
                  ("Infrastructure", "Scripts/Infrastructure"), ("Presentation", "Scripts/Presentation"),
                  ("Tests.Editor", "Tests/Editor"), ("Tests.PlayMode", "Tests/PlayMode")]
    with tempfile.TemporaryDirectory(prefix="quietcamp-monetization-compile-") as temporary:
        output = Path(temporary)
        compiled = {}
        for name, folder in assemblies:
            assembly = "QuietCamp." + name
            original = ARTIFACTS / (assembly + ".rsp")
            if not original.exists():
                raise RuntimeError("Missing existing Unity compiler response: " + str(original))
            options = []
            for line in original.read_text().splitlines():
                if line.startswith(("-out:", "-refout:")) or line.strip('"').endswith(".cs"):
                    continue
                if line.startswith("-r:"):
                    reference = Path(line[3:].strip('"'))
                    stem = reference.name.removesuffix(".ref.dll").removesuffix(".dll")
                    if stem in compiled:
                        line = '-r:"' + str(compiled[stem]) + '"'
                    elif not reference.is_absolute() and not (PROJECT / reference).exists():
                        fallback = PROJECT / "Library/ScriptAssemblies" / (stem + ".dll")
                        if fallback.exists():
                            line = '-r:"' + str(fallback) + '"'
                options.append(line)
            destination = output / (assembly + ".dll")
            options.extend(['-out:"' + str(destination) + '"', '-refout:"' + str(output / (assembly + ".ref.dll")) + '"'])
            options.extend('"' + str(path) + '"' for path in sorted((PROJECT / "Assets/QuietCamp" / folder).rglob("*.cs")))
            response = output / (assembly + ".rsp")
            response.write_text("\n".join(options) + "\n")
            result = subprocess.run(["dotnet", str(compiler), "@" + str(response)], cwd=PROJECT,
                                    env=dict(os.environ, DOTNET_CLI_TELEMETRY_OPTOUT="1"), capture_output=True, text=True)
            if result.returncode != 0:
                print(result.stdout + result.stderr, flush=True)
                result.check_returncode()
            compiled[assembly] = destination
            warnings = (result.stdout + result.stderr).count(": warning ")
            print("PASS current-source compiler check: " + assembly + " (warnings: " + str(warnings) + ")", flush=True)
    print("Compiler checks only; no Unity import, player build or runtime scene check performed.")


if __name__ == "__main__":
    main()
