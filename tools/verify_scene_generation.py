"""Run reusable Core contracts against the packages embedded in this checkout. No Unity/build."""
from pathlib import Path
import os
import subprocess

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
    print("Core contracts only; native rendering, UI flow and performance remain separate checks.")


if __name__ == "__main__":
    main()
