"""Send serial capture requests to an ALREADY OPEN authorized QuietCamp Editor. Never launches Unity."""
import argparse
import json
import time
from pathlib import Path


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--project", type=Path, required=True)
    parser.add_argument("--output", type=Path, required=True)
    parser.add_argument("--level", default="QC007")
    parser.add_argument("--seed", type=int, default=12007)
    parser.add_argument("--name", required=True)
    parser.add_argument("--width", type=int, default=1536)
    parser.add_argument("--height", type=int, default=1024)
    parser.add_argument("--mode", choices=["capture", "game-shot"], default="capture")
    args = parser.parse_args()
    temp = args.project.resolve() / "Temp/ai"
    request = temp / "qc007-art-request.json"
    if request.exists():
        raise SystemExit("A pending Editor request already exists; it will not be overwritten.")
    output = args.output.resolve()
    output.mkdir(parents=True, exist_ok=True)
    receipt = output / (args.name + ".json")
    previous = receipt.stat().st_mtime_ns if receipt.exists() else 0
    result = temp / "qc007-art-result.json"
    previous_result = result.stat().st_mtime_ns if result.exists() else 0
    payload = dict(mode=args.mode, levelId=args.level, profile="riparian-clearing", seed=args.seed,
                   name=args.name, width=args.width, height=args.height, output=output.as_posix())
    staging = temp / "qc007-art-request.pending"
    staging.write_text(json.dumps(payload), encoding="utf-8")
    staging.replace(request)
    started = time.monotonic()
    while time.monotonic() - started < 180:
        if receipt.exists() and receipt.stat().st_mtime_ns != previous:
            data = json.loads(receipt.read_text(encoding="utf-8"))
            print(f"Captured {args.name}: {data['width']}x{data['height']}, {data['kind']}")
            return
        result = temp / "qc007-art-result.json"
        if not request.exists() and result.exists() and result.stat().st_mtime_ns != previous_result:
            data = json.loads(result.read_text(encoding="utf-8-sig"))
            if data.get("status") == "error":
                raise SystemExit(data["error"])
        time.sleep(.5)
    raise SystemExit("Editor capture timed out; no new receipt was accepted.")


if __name__ == "__main__":
    main()
