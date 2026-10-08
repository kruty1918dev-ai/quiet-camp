#!/usr/bin/env python3
"""Asset/authoring integrity checks; does not launch Unity or build a player."""
import hashlib
import json
from pathlib import Path
import sys
import numpy as np
import jsonschema
import build_environment_kit as kit
import inventory
import optimize_ship

ROOT=Path(__file__).resolve().parents[2]
BASE=ROOT/"QuietCamp/Assets/QuietCamp"
COMPOSITION=BASE/"Authoring/Roadmap/Composition"


def require(ok,message):
    if not ok:raise AssertionError(message)


def main():
    models={};legacy_degenerates={}
    for relative,_,_ in inventory.CATALOGS:
        for m in json.loads((BASE/relative).read_text()):
            require(m["id"] not in models,"Duplicate model "+m["id"])
            data=np.array(m["data"]).reshape(-1,6);triangles=data[:,:3].reshape(-1,3,3)
            require(len(m["colors"])==len(triangles),"Color stream length "+m["id"])
            require(np.isfinite(data).all(),"Nonfinite geometry "+m["id"])
            bad=int(np.sum(np.linalg.norm(np.cross(triangles[:,1]-triangles[:,0],triangles[:,2]-triangles[:,0]),axis=1)<=1e-10))
            if bad and relative.startswith("Resources/"):legacy_degenerates[m["id"]]=bad
            else:require(bad==0,"Degenerate geometry "+m["id"])
            require(np.all(np.abs(np.linalg.norm(data[:,3:],axis=1)-1)<.002),"Nonunit normals "+m["id"])
            require((data[:,1]>=-.00001).all(),"Model below normalized ground "+m["id"])
            models[m["id"]]=m
    schemas=ROOT/"tools/scene-composition"
    assets=json.loads((COMPOSITION/"assets.json").read_text());templates=json.loads((COMPOSITION/"templates.json").read_text())
    for name,value in [("visual-assets.schema.json",assets),("ensemble-templates.schema.json",templates)]:
        jsonschema.Draft202012Validator(json.loads((schemas/name).read_text())).validate(value)
    for lod in assets:
        if lod.get("lod"):require(lod["lod"] in models,"Missing LOD "+lod["id"])
    used={r["asset"] for t in templates for r in t["roles"] if not r["asset"].startswith("$")}
    manifest=json.loads((COMPOSITION/"manifest.json").read_text())
    for file in manifest["regions"]:
        doc=json.loads((COMPOSITION/file).read_text())
        used|={z["species"] for z in doc["zones"] if z.get("species")}
        used|={r["supportAsset"] for r in doc["routes"] if r.get("supportAsset")}
        used|={p["asset"] for p in doc.get("landmarks",[])}
    require(used<=set(models),"Used authoring IDs without mesh: "+str(used-set(models)))
    kit_items=kit.models();kit.rural.validate(kit_items)
    for path,expected in kit.files_for(kit_items).items():require(path.read_text()==expected,"Stale kit: "+str(path))
    metadata={a["id"]:a for a in assets}
    for expected in kit.asset_entries(kit_items):
        require(metadata[expected["id"]]==expected,"Mismatched kit metadata "+expected["id"])
    for lod in ("staging_cargo_ship_coarse","staging_cargo_ship_silhouette"):
        require(models[lod]["sourceHeight"]==models["staging_cargo_ship"]["sourceHeight"],"Ship LOD rescaled")
    optimized=optimize_ship.build();kit.rural.validate([m for m,_ in optimized])
    for path,expected in optimize_ship.files_for(optimized).items():require(path.read_text()==expected,"Stale ship "+str(path))
    for path,expected in inventory.files_for().items():require(path.read_text()==expected,"Stale matrix "+str(path))
    donor=BASE/"Authoring/Roadmap/Models/StagingLandmarks/ship-source.glb"
    require(hashlib.sha256(donor.read_bytes()).hexdigest()==optimize_ship.EXPECTED,"Donor changed")
    # The dam's breach is empty even in LOD; no hidden bridging triangle.
    for mesh in kit_items:
        if mesh.id.startswith("ua_dam_breached"):
            require(all(max(p[0] for p in t[:3])<=2.550001 or min(p[0] for p in t[:3])>=7.449999 for t in mesh.tris),"Dam breach blocked")
    # Source GUIDs are preserved; all new Unity files have a metadata sibling.
    for path in kit.SOURCE.iterdir():
        if not path.name.endswith(".meta"):require(Path(str(path)+".meta").exists(),"Missing Unity meta "+str(path))
    print(f"PASS {len(models)} model streams: lengths, finite coordinates, normals, palette metadata, LOD, source hashes, source bindings and matrix; edited meshes nondegenerate")
    print("Unchanged legacy degenerate faces:",legacy_degenerates)
    print("No Unity import, native render, player build or mobile performance check performed.")


if __name__=="__main__":main()
