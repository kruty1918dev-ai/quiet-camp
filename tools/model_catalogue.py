#!/usr/bin/env python3
"""Local native-photo gallery and explicit visual-review ledger (commercial art stays local)."""
import argparse
import hashlib
import json
from pathlib import Path
from PIL import Image, ImageDraw, ImageFont

ROOT = Path(__file__).resolve().parents[1]
CAT = ROOT / "Design/Roadmap/ModelCatalogue"
PHOTOS = CAT / "LocalPreviews~"
LEDGER = CAT / "visual-review.json"
FONT = "/usr/share/fonts/truetype/dejavu/DejaVuSans.ttf"


def entries():
    return [json.loads(p.read_text()) for p in sorted(PHOTOS.glob("M*.json"))]


def sheets():
    rows = entries()
    font = ImageFont.truetype(FONT, 12)
    title = ImageFont.truetype(FONT, 19)
    output = PHOTOS / "sheets"
    output.mkdir(exist_ok=True)
    for start in range(0, len(rows), 20):
        group = rows[start:start + 20]
        if len(group) < 20 and len(rows) != 1210:
            break
        target = output / f"sheet-{start // 20 + 1:02d}.jpg"
        if target.exists():
            continue
        canvas = Image.new("RGB", (1760, 1240), "#f0f0e9")
        draw = ImageDraw.Draw(canvas)
        draw.text((14, 8), f"Native Unity / two views / {group[0]['displayId']} – {group[-1]['displayId']}", font=title, fill="#24352c")
        for i, row in enumerate(group):
            x, y = (i % 4) * 440, 42 + (i // 4) * 238
            for view, photo in enumerate(row.get("images") or []):
                with Image.open(PHOTOS / photo) as im:
                    canvas.paste(im.convert("RGB").resize((212, 196)), (x + 5 + view * 218, y))
            draw.text((x + 5, y + 198), row["displayId"] + "  " + row["name"][:49], font=font, fill="#12291d")
            draw.text((x + 5, y + 214), f"{row['triangles']:,} tris · " + row["materialBinding"], font=font, fill="#44554b")
        canvas.save(target, quality=90)
    print(f"{len(rows)} native receipts; {len(list(output.glob('*.jpg')))} contact sheets")


def accept(number, note):
    sheet = PHOTOS / "sheets" / f"sheet-{number:02d}.jpg"
    if not sheet.exists():
        raise SystemExit("Render sheet first, inspect its actual image, then explicitly record the review")
    rows = entries()[(number - 1) * 20:number * 20]
    state = json.loads(LEDGER.read_text()) if LEDGER.exists() else {"date": "2026-10-10", "method": "Native OpenGL photographs, two views per source; agent visual inspection of contact sheets and enlarged exceptions", "sheets": {}}
    state["sheets"][str(number)] = {"imageSha256": hashlib.sha256(sheet.read_bytes()).hexdigest(), "models": [r["id"] for r in rows], "displayIds": [r["displayId"] for r in rows], "observationUk": note}
    CAT.mkdir(exist_ok=True)
    LEDGER.write_text(json.dumps(state, ensure_ascii=False, indent=2) + "\n")
    print(f"Recorded explicit visual review of {len(rows)} models on sheet {number}")


if __name__ == "__main__":
    parser = argparse.ArgumentParser()
    parser.add_argument("command", choices=["sheets", "accept"])
    parser.add_argument("--sheet", type=int)
    parser.add_argument("--note")
    args = parser.parse_args()
    if args.command == "sheets":
        sheets()
    elif not args.sheet or not args.note:
        parser.error("Explicit review requires --sheet and --note")
    else:
        accept(args.sheet, args.note)
