"""Run reusable Core contracts against the packages embedded in this checkout. No Unity/build."""
from pathlib import Path
import os
import subprocess
import json

ROOT = Path(__file__).resolve().parents[1]
PACKAGES = ROOT / "QuietCamp/Packages"


def main():
    logs = ROOT / "Temp/ai/scene-generation"
    logs.mkdir(parents=True, exist_ok=True)
    for name in ["scene-placement", "vegetation-placement", "rock-placement"]:
        package = PACKAGES / ("com.kruty1918." + name)
        args = ["dotnet", "run", "--project", str(package / "Tests~/Contracts.csproj"),
                "-m:1", "-p:UseSharedCompilation=false", "-p:ScenePlacementRoot=" + str(PACKAGES / "com.kruty1918.scene-placement")]
        result = subprocess.run(args, cwd=ROOT, capture_output=True, text=True,
                                env=dict(os.environ, DOTNET_CLI_TELEMETRY_OPTOUT="1"))
        (logs / (name + ".log")).write_text(result.stdout + result.stderr, encoding="utf-8")
        if result.returncode:
            print("\n".join((result.stdout + result.stderr).splitlines()[-30:]))
            raise SystemExit(result.returncode)
        for line in result.stdout.splitlines():
            if "assertions passed" in line:
                print(line)
    gallery = PACKAGES / "com.kruty1918.scene-placement/Samples~/CompositionGallery"
    build = subprocess.run(["dotnet", "build", str(gallery / "CompositionGallery.csproj"), "-m:1", "-p:UseSharedCompilation=false", "--verbosity", "quiet"],
                           cwd=ROOT, capture_output=True, text=True)
    (logs / "portable-host-build.log").write_text(build.stdout + build.stderr, encoding="utf-8")
    if build.returncode:
        print("\n".join((build.stdout + build.stderr).splitlines()[-30:])); raise SystemExit(build.returncode)
    for profile, width, depth, seed in [("riverside-garden", 6, 8, 12007), ("cooling-courtyard", 8, 10, 73)]:
        outputs = []
        for replay in range(2):
            output = logs / f"{profile}-{replay}.json"
            result = subprocess.run(["dotnet", "run", "--no-build", "--project", str(gallery / "CompositionGallery.csproj"), "--",
                "--profile", str(gallery / (profile + ".json")), "--width", str(width), "--depth", str(depth), "--seed", str(seed), "--output", str(output)],
                cwd=ROOT, capture_output=True, text=True)
            if result.returncode:
                print(result.stdout + result.stderr); raise SystemExit(result.returncode)
            outputs.append(output.read_bytes())
        if outputs[0] != outputs[1]: raise SystemExit("Non-deterministic portable host: " + profile)
        artifact = json.loads(outputs[0]); assert artifact["review"]["passed"] and len(artifact["attempts"]) == 8
        assert artifact["kind"] == "portable-composition-plan" and len(artifact["instances"]) > 0 and len(artifact["groundcover"]) > 0
        assert artifact["request"]["width"] == width and artifact["request"]["depth"] == depth and artifact["request"]["seed"] == seed
        print(f"PASS portable host/replay: {profile}, selected attempt {artifact['attempt']}, no Unity/QuietCamp dependency.")
    print("Core contracts and portable plans only; native rendering, UI flow and performance remain separate checks.")


if __name__ == "__main__":
    main()
