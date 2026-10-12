"""Render existing OBJ meshes and exact board references for visual concepts.

Read-only with respect to Unity assets. Uses the existing depth-buffered mesh
renderer; outputs are geometry previews, not native Unity acceptance captures.
"""
from pathlib import Path
import importlib.util
import inspect
import json
import random
import re

from PIL import Image, ImageDraw, ImageFont

ROOT = Path(__file__).resolve().parents[1]
OUT = ROOT / "Design/LevelVisualConcepts/2026-10-12"
ACTIVE = Path("C:/Users/Public/qcui-20261006/QuietCamp")
ASSETS = ROOT / "QuietCamp/Assets/QuietCamp/Authoring/Roadmap/Models"
SELECTION = [
    ("UkrainianRural/ua_orchard_tree.obj", "01 · Садове дерево"),
    ("EnvironmentKit/ua_young_willow.obj", "02 · Молода верба"),
    ("EnvironmentKit/ua_reed_clump.obj", "03 · Очерет"),
    ("EnvironmentKit/ua_plank_bridge.obj", "04 · Дерев’яний місток"),
    ("UkrainianRural/ua_wattle_fence.obj", "05 · Плетений тин"),
    ("UkrainianRural/ua_sunflower_patch.obj", "06 · Соняшники"),
]


def load_mesh(module, path, title):
    mesh = module.Mesh(title, 100000, "Existing project OBJ")
    vertices, normals, palette = [], [], {}
    material = None
    for line in (path.parent / "palette.mtl").read_text().splitlines():
        fields = line.split()
        if not fields:
            continue
        if fields[0] == "newmtl":
            material = fields[1]
        elif fields[0] == "Kd":
            rgb = [round(float(value) * 255) for value in fields[1:4]]
            palette[material] = rgb[0] << 16 | rgb[1] << 8 | rgb[2]
    mesh.palette = palette
    for line in path.read_text().splitlines():
        fields = line.split()
        if not fields:
            continue
        if fields[0] == "v":
            vertices.append(tuple(float(v) for v in fields[1:4]))
        elif fields[0] == "vn":
            normals.append(tuple(float(v) for v in fields[1:4]))
        elif fields[0] == "usemtl":
            material = fields[1]
        elif fields[0] == "f":
            face = fields[1:]
            for index in range(1, len(face) - 1):
                items = [face[0], face[index], face[index + 1]]
                points = [vertices[int(item.split("/")[0]) - 1] for item in items]
                normal_id = items[0].split("/")[-1]
                normal = normals[int(normal_id) - 1]
                mesh.tris.append((*points, normal, material))
    assert mesh.tris and all(t[4] in palette for t in mesh.tris), path
    return mesh


def board_reference(level):
    cell, margin, header = 78, 45, 90
    width, height = level["width"], level["height"]
    image = Image.new("RGB", (max(640, margin * 2 + width * cell), header + height * cell + 80), "#f6f2e8")
    draw = ImageDraw.Draw(image)
    font = ImageFont.truetype("C:/Windows/Fonts/segoeui.ttf", 25)
    small = ImageFont.truetype("C:/Windows/Fonts/segoeui.ttf", 17)
    draw.text((margin, 18), f"{level['id']} · {width} × {height} · {len(level['witness'])} tents", fill="#355344", font=font)
    draw.text((margin, 53), "Exact layout reference. x → right; z → down.", fill="#355344", font=small)

    def rect(x, z, span=1):
        return (margin + x * cell, header + z * cell, margin + (x + span) * cell, header + (z + span) * cell)

    shaded, blocked = set(map(tuple, level["shade"])), set(map(tuple, level["blocked"]))
    for z in range(height):
        for x in range(width):
            color = "#809577" if (x, z) in shaded else "#c6d0a0"
            if (x, z) in blocked:
                color = "#7c7b68"
            draw.rectangle(rect(x, z), fill=color, outline="#829272", width=1)
    for index, placement in enumerate(level["witness"]):
        box = rect(placement["x"], placement["z"], 2)
        draw.rectangle(box, fill="#bc6946", outline="#794b34", width=3)
        draw.line((box[0], (box[1] + box[3]) / 2, box[2], (box[1] + box[3]) / 2), fill="#e4a76d", width=5)
        draw.text((box[0] + 9, box[1] + 8), f"T{index + 1} · r{placement['rotation']}", fill="white", font=small)
    draw.rectangle(rect(*level["entry"]), outline="#f4d372", width=7)
    draw.text((margin, header + height * cell + 24), "Decorative additions belong outside this board.", fill="#355344", font=small)
    image.save(OUT / f"{level['id']}-layout.png")


def main():
    OUT.mkdir(parents=True, exist_ok=True)
    spec = importlib.util.spec_from_file_location("rural_models", ROOT / "tools/build_ukrainian_roadmap_models.py")
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    meshes = [load_mesh(module, ASSETS / rel, title) for rel, title in SELECTION]
    preview_source = inspect.getsource(module.preview).replace(
        'font_path="/usr/share/fonts/truetype/dejavu/DejaVuSans.ttf"',
        'font_path="C:/Windows/Fonts/segoeui.ttf"',
    )
    preview_source = re.sub(
        r'"Depth-buffered software mesh inspection[^"\n]*"',
        '"Точні OBJ-моделі та їхні MTL-кольори · геометричний рендер, не Unity-кадр"',
        preview_source,
    )
    namespace = dict(vars(module))
    exec(preview_source, namespace)
    namespace["preview"](meshes, OUT / "selected-project-models.png", "Quiet Camp · Обрані моделі з проєкту")
    resources = ACTIVE / "Assets/QuietCamp/Resources/QuietCamp"
    campaign = json.loads((resources / "campaign.json").read_text(encoding="utf-8-sig"))
    authored = [id for id in campaign["mvpLevelIds"] + campaign.get("generatedLevelIds", []) if id.startswith("QC")]
    selected_ids = random.Random(20261012).sample(authored, 3)
    assert selected_ids == ["QC007", "QC001", "QC009"]
    for id in selected_ids:
        board_reference(json.loads((resources / f"Levels/{id}.json").read_text(encoding="utf-8-sig")))
    print("PASS: six existing meshes rendered with source materials; three exact board references created.")


if __name__ == "__main__":
    main()
