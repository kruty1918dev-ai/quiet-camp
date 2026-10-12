"""Prepare QC007's presentation-only JSON from existing project OBJ models."""
from pathlib import Path
import importlib.util
import json
import math
import random
import uuid

ROOT = Path(__file__).resolve().parents[1]
PROJECT = ROOT / "QuietCamp"
spec = importlib.util.spec_from_file_location("concept_sources", ROOT / "tools/render_level_visual_concepts.py")
sources = importlib.util.module_from_spec(spec)
spec.loader.exec_module(sources)
spec = importlib.util.spec_from_file_location("rural_models", ROOT / "tools/build_ukrainian_roadmap_models.py")
models_module = importlib.util.module_from_spec(spec)
spec.loader.exec_module(models_module)


def prepare():
    models = []
    for relative, title in sources.SELECTION:
        mesh = sources.load_mesh(models_module, sources.ASSETS / relative, title)
        positions, normals, colors = [], [], []
        for a, b, c, normal, material in mesh.tris:
            for point in [a, b, c]:
                positions.extend(round(value, 6) for value in point)
                normals.extend(round(value, 6) for value in normal)
                colors.append(mesh.palette[material])
        models.append(dict(id=Path(relative).stem, vertices=positions, normals=normals, colors=colors))
    instances = []

    def add(u, v, model=None, asset=None, scale=1, yaw=0, y=-.025):
        x, z = (-u - v) / math.sqrt(2), (u - v) / math.sqrt(2)
        assert abs(x) >= 3.2 or abs(z) >= 4.2, (u, v, x, z)
        item = dict(x=round(x, 4), y=y, z=round(z, 4), scale=scale, yaw=yaw)
        item["modelId" if model else "assetId"] = model or asset
        instances.append(item)

    # Camera-ground coordinates: u screen-right, v towards the distant background.
    for u, v, scale in [(-8.2, 8, 1.18), (-8.7, 2.5, 1.14), (-8.6, -4, 1.32),
                         (-5.2, 7, 1.12), (-5.4, 2.1, 1.02), (-5.1, -6.4, 1.08)]:
        add(u, v, model="ua_young_willow", scale=scale)
    for u, v, scale in [(5.8, 6.8, 1.3), (8.1, 1.6, 1.16), (6.9, -3.8, 1.32),
                         (-9.8, -8.6, 1.8), (9.2, -8.4, 1.8), (3.2, 9.0, 1.3)]:
        add(u, v, model="ua_orchard_tree", scale=scale)
    add(-6.6, -2.2, model="ua_plank_bridge", scale=.56, yaw=285, y=-.01)
    add(-5.4, -.8, model="ua_wattle_fence", scale=.65, yaw=-45)
    add(5.9, 6.8, model="ua_wattle_fence", scale=.75, yaw=45)
    for u, v in [(5.6, 5.4), (6.1, 5.2), (6.8, -3.3), (7.2, -3.7), (1.8, 8.0)]:
        add(u, v, model="ua_sunflower_patch", scale=.66, yaw=(u * 35) % 360)
    # Existing gameplay trees add layered silhouettes around the selected rural meshes.
    for u, v, scale in [(-9.2, 8.5, 1.45), (-10.0, 4.1, 1.5), (8.8, 8.5, 1.65),
                         (5.0, 10.2, 1.5), (1.2, 10.8, 1.7), (-3.2, 11.0, 1.4),
                         (9.0, 3.7, 1.7), (9.5, -1.2, 1.5), (10.0, -5.7, 1.7),
                         (-9.4, -7.7, 2.1), (8.8, -8.6, 2.15), (-4.7, -10.0, 1.8),
                         (4.0, -10.5, 1.9)]:
        add(u, v, asset="tree_default", scale=scale, yaw=(u * 32) % 360)
    rng = random.Random(12007)
    for v in [-10, -8, -6, -2, 0, 2, 4, 6, 8, 10]:
        centre = -6.7 + math.sin(v * .38) * .32 + math.sin(v * .83) * .11
        for bank in [-1, 1]:
            for cluster in range(3):
                u = centre + bank * (1.03 + cluster * .20)
                add(u, v + rng.uniform(-.55, .55), model="ua_reed_clump", scale=rng.uniform(.52, .83), yaw=rng.uniform(0, 360))
            add(centre + bank * 1.08, v + rng.uniform(-.9, .9), asset="stone_largeA", scale=rng.uniform(.75, 1.24), yaw=rng.uniform(0, 360), y=.005)
    for u, v in [(5.0, 3.1), (5.7, -5.7), (4.8, -7.1), (-2.4, 8.0), (1.2, 9.6)]:
        add(u, v, asset="stone_largeA", scale=rng.uniform(.35, .7), yaw=rng.uniform(0, 360))
    data = dict(levelId="QC007", width=6, height=8, seed=12007,
                cameraEuler=[58, 225, 0], cameraTarget=[.65, 0, -.50], cameraAspect=1.5,
                orthographicSize=6.70, sunEuler=[52, 35, 0], sunIntensity=1.22,
                sunColor=0xFFF4DE, ambientColor=0xC4D5B8,
                groundLow=0x708F46, groundHigh=0x89A651, soilColor=0xAF9671,
                streamCentre=-6.7, streamWidth=1.9, blurPixels=4.5,
                exposure=.12, saturation=2, contrast=5,
                models=models, instances=instances)
    target = PROJECT / "Assets/QuietCamp/Resources/QuietCamp/ArtCompositions/QC007.json"
    target.parent.mkdir(parents=True, exist_ok=True)
    target.write_text(json.dumps(data, separators=(",", ":")) + "\n")
    print(f"Prepared {len(models)} source meshes and {len(instances)} exterior instances; puzzle JSON untouched.")


if __name__ == "__main__":
    prepare()
