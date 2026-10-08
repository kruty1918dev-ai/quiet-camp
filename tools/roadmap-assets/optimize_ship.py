#!/usr/bin/env python3
"""Restyle the already-attributed Sketchfab donor; no network/authentication.

QEM welds shared vertices before simplifying to avoid cracks at color boundaries. Every export
uses the same donor origin/height, so changing LOD cannot lift or rescale the ship.
"""
import argparse
import hashlib
import importlib.util
import json
import math
from pathlib import Path
import sys
import numpy as np
import fast_simplification

ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(ROOT / "tools"))
import build_ukrainian_roadmap_models as rural

SOURCE = ROOT / "QuietCamp/Assets/QuietCamp/Authoring/Roadmap/Models/StagingLandmarks"
EXPECTED = "48665716be710479635171b3169f3f056e4785ca1e979089e3d47a83bf480d9a"
PALETTE_KEYS = ("concrete", "wood_dark", "roof", "rust", "plaster_worn")


def build():
    donor = SOURCE / "ship-source.glb"
    if hashlib.sha256(donor.read_bytes()).hexdigest() != EXPECTED:
        raise ValueError("Unexpected donor: verify attribution/license before import")
    spec = importlib.util.spec_from_file_location("landmarks", ROOT / "tools/scene-composition/prepare_staging_landmarks.py")
    module = importlib.util.module_from_spec(spec); spec.loader.exec_module(module)
    triangles, _ = module.read_glb(donor)
    points = np.concatenate([t[0] for t in triangles])
    scale = 31 / max(np.ptp(points, axis=0)[[0, 2]])
    points *= scale
    low, high = points.min(axis=0), points.max(axis=0)
    origin = (low + high) / 2; origin[1] = low[1]
    height = float(high[1] - low[1])
    palette = np.array([[(rural.PALETTE[k] >> s) & 255 for s in (16,8,0)] for k in PALETTE_KEYS]) / 255
    raw = []; keys = []
    for p, _, c in triangles:
        c = np.clip(c*.77 + np.array([.61, .62, .53])*.23, 0, 1)
        key = PALETTE_KEYS[int(np.argmin(np.sum((palette - c)**2, axis=1)))]
        keys.append(key); raw.append(p * scale - origin)
    raw = np.array(raw); centers = raw.mean(axis=1)
    source_vertices, inverse = np.unique(np.round(raw.reshape(-1,3),8),axis=0,return_inverse=True)
    source_faces = inverse.reshape(-1,3).astype(np.int32)
    result = []
    for name, cap in [("staging_cargo_ship", 1100), ("staging_cargo_ship_coarse", 650), ("staging_cargo_ship_silhouette", 300)]:
        model = rural.Mesh(name, cap, "Sketchfab cargo donor; welded QEM, flat normals, shared origin/height. See ATTRIBUTION.md.")
        vertices, faces = fast_simplification.simplify(source_vertices, source_faces, target_count=cap, agg=7)
        vertices = np.clip(vertices,low-origin,high-origin)
        simplified = vertices[faces]
        for start in range(0,len(faces),128):
            nearest = np.argmin(np.sum((simplified[start:start+128].mean(axis=1)[:,None,:]-centers[None,:,:])**2,axis=2),axis=1)
            for points,index in zip(simplified[start:start+128],nearest):
                a,b,c = points
                if np.linalg.norm(np.cross(b-a,c-a)) > 1e-8:
                    model.tri(tuple(a),tuple(b),tuple(c),keys[index])
        # A single shared LOD pivot and scale; do not renormalize each mesh's bounds.
        result.append((model, height))
    return result


def files_for(items):
    streams = json.loads((SOURCE / "models.json").read_text())
    manifest = json.loads((SOURCE / "manifest.json").read_text())
    assets_path = ROOT / "QuietCamp/Assets/QuietCamp/Authoring/Roadmap/Composition/Staging/assets.json"
    assets = json.loads(assets_path.read_text())
    files = {}
    for model, height in items:
        lo, hi = model.bounds()
        stream = dict(id=model.id, data=[], colors=[], sourceHeight=height)
        for a, b, c, normal, color in model.tris:
            stream["colors"].append(rural.PALETTE[color])
            for point in (a, b, c):
                stream["data"].extend(round(float(x), 6) for x in (*[v/height for v in point], *normal))
        streams = [s for s in streams if s["id"] != model.id] + [stream]
        obj = rural.source_obj(model).replace("Original Quiet Camp Ukrainian rural presentation asset", "Sketchfab derivative; see ATTRIBUTION.md")
        files[SOURCE / (model.id + ".obj")] = obj
        entry = dict(id=model.id, triangles=len(model.tris), triangleBudget=model.budget,
                     bounds=dict(min=lo, max=hi), height=height, sourceHeight=height,
                     width=hi[0]-lo[0], depth=hi[2]-lo[2], sharedPivot=[0,0,0],
                     sourceSha256=EXPECTED, algorithm="welded QEM; nearest donor-face palette; flat normals; UV/textures removed")
        manifest["models"] = [m for m in manifest["models"] if m["id"] != model.id] + [entry]
        asset = next(a for a in assets if a["id"] == model.id)
        asset.update(height=height, sourceHeight=height, width=entry["width"], depth=entry["depth"], radius=math.hypot(entry["width"], entry["depth"])/2)
    manifest["shipOptimization"] = dict(generator="tools/roadmap-assets/optimize_ship.py", originalTriangles=2384,
        palette=list(PALETTE_KEYS), dependency="fast-simplification==0.2.0", nativeVisualAcceptance=False)
    files[SOURCE / "models.json"] = json.dumps(streams, separators=(",", ":")) + "\n"
    files[SOURCE / "manifest.json"] = rural.text_json(manifest)
    files[assets_path] = rural.text_json(assets)
    files[SOURCE / "palette.mtl"] = rural.build_files([])[rural.SOURCE/"palette.mtl"]
    files[SOURCE / "palette.mtl.meta"] = rural.meta(SOURCE / "palette.mtl")
    return files


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--check", action="store_true")
    parser.add_argument("--preview", action="store_true")
    args = parser.parse_args()
    items = build(); rural.validate([m for m, _ in items]); files = files_for(items)
    for path, text in files.items():
        if args.check:
            if not path.exists() or path.read_text() != text: raise AssertionError("Stale output: " + str(path))
        else: path.write_text(text)
    if args.preview:
        if args.check: raise ValueError("--check must not write")
        rural.preview([m for m, _ in items], ROOT/"Design/Roadmap/Assets/ship-lod-contact-sheet.png",
                      "Quiet Camp · existing Sketchfab cargo derivative · QEM / shared palette")
    print("PASS ship donor hash, palette, shared LOD origin/height; triangles:", [len(m.tris) for m, _ in items])


if __name__ == "__main__": main()
