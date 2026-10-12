"""Prepare QC007's presentation-only JSON from existing project OBJ models."""
from pathlib import Path
import importlib.util
import json
import math
import random
import re
import uuid

ROOT = Path(__file__).resolve().parents[1]
PROJECT = ROOT / "QuietCamp"
spec = importlib.util.spec_from_file_location("concept_sources", ROOT / "tools/render_level_visual_concepts.py")
sources = importlib.util.module_from_spec(spec)
spec.loader.exec_module(sources)
spec = importlib.util.spec_from_file_location("rural_models", ROOT / "tools/build_ukrainian_roadmap_models.py")
models_module = importlib.util.module_from_spec(spec)
spec.loader.exec_module(models_module)

def prepare_water():
    # Retain vendor shader, textures and wave LUT references; author a camp-scale preset.
    source = PROJECT / "Assets/ThirdParty/Stylized Water 3/Materials/StylizedWater3_River.mat"
    target = PROJECT / "Assets/QuietCamp/Resources/QuietCamp/ArtCompositions/QC007Water.mat"
    text = source.read_text().replace("m_Name: StylizedWater3_River", "m_Name: QC007Water")
    values = {"_DepthHorizontal": 2.6, "_DepthVertical": 6, "_EdgeFade": .04,
              "_IntersectionLength": 1.25, "_IntersectionFalloff": .65, "_IntersectionTiling": 1.6,
              "_IntersectionSpeed": .14, "_IntersectionSharp": 0, "_IntersectionSource": 1, "_FogSource": 1, "_IntersectionClipping": .25, "_NormalStrength": .42,
              "_NormalSpeed": .55, "_NormalTiling": 1.6, "_FoamTiling": 1.2, "_FoamSpeed": .45,
              "_FoamStrength": .7, "_FoamBaseAmount": .32, "_FoamSize": .12, "_CausticsBrightness": .32,
              "_CausticsTiling": .8, "_WaveHeight": .025, "_WaveMaxLayers": 4,
              "_WaveSpeed": .35, "_WaveNormalStr": .35, "_WaveSteepness": .04,
              "_WaveFrequency": 1.5, "_SunReflectionStrength": .6, "_SunReflectionSize": .16,
              "_ReflectionStrength": .16, "_RefractionStrength": .022, "_ShadowStrength": .75,
              "_TranslucencyStrength": .25, "_WorldSpaceUV": 0, "_VertexColorWaveFlattening": 0}
    for key, value in values.items():
        text = re.sub(r"(?m)^(    - " + re.escape(key) + r": )[0-9.eE+-]+$", lambda m: m[1] + str(value), text)
    colors = {"_BaseColor": (.012, .105, .14, 1), "_ShallowColor": (.095, .29, .25, .88),
              "_IntersectionColor": (.88, .97, .9, .48), "_FoamColor": (.84, .94, .9, .65),
              "_WaveDirection": (1, 0, 1, 0), "_Direction": (0, -1, 0, 0),
              "_NormalTiling": (1.3, .85, 0, 0), "_FoamTiling": (1.4, .95, 0, 0)}
    for key, c in colors.items():
        text = re.sub(r"(?m)^(    - " + re.escape(key) + r": ).*$", lambda m: m[1] + "{r: %s, g: %s, b: %s, a: %s}" % c, text)
    target.write_text("\n".join(line.rstrip() for line in text.splitlines()) + "\n")
    meta = target.with_suffix(".mat.meta")
    if not meta.exists():
        meta.write_text("fileFormatVersion: 2\nguid: " + uuid.uuid4().hex + "\nNativeFormatImporter:\n  externalObjects: {}\n  mainObjectFileID: 2100000\n  userData:\n  assetBundleName:\n  assetBundleVariant:\n")


def prepare():
    models = []
    for relative, title in sources.SELECTION:
        mesh = sources.load_mesh(models_module, sources.ASSETS / relative, title)
        positions, normals, colors = [], [], []
        # Camp presentation: tilt the existing flower disks towards an elevated view.
        # Stems and leaves retain their authored geometry; no scene coordinates here.
        centres = []
        if Path(relative).stem == "ua_sunflower_patch":
            from collections import Counter
            frequencies = Counter(p for tri in mesh.tris if tri[4] == "seed" for p in tri[:3])
            centres = [p for p, count in frequencies.items() if count >= 5]
            assert len(centres) == 9

        for a, b, c, normal, material in mesh.tris:
            source_normal = normal
            for point in [a, b, c]:
                normal = source_normal
                if centres and material in ("sunflower", "seed"):
                    centre = min(centres, key=lambda p: sum((p[i]-point[i])**2 for i in range(3)))
                    dx, dy, dz = ((point[i]-centre[i]) * (1.32 if i < 2 else 1) for i in range(3))
                    angle = math.radians(35)
                    point = (centre[0]+dx, centre[1]+dy*math.cos(angle)-dz*math.sin(angle), centre[2]+dy*math.sin(angle)+dz*math.cos(angle))
                    normal = (normal[0], normal[1]*math.cos(angle)-normal[2]*math.sin(angle), normal[1]*math.sin(angle)+normal[2]*math.cos(angle))
                positions.extend(round(value, 6) for value in point)
                normals.extend(round(value, 6) for value in normal)
                colors.append(mesh.palette[material])
        # The same source geometry, with a coordinated sunlit foliage palette.
        remap = {"leaf": 0x719842, "leaf_light": 0x93B655, "leaf_dark": 0x4E753B,
                 "sunflower": 0xFFD54C, "seed": 0x654727, "reed": 0x8E9A51, "reed_light": 0xB5B771}
        palette = {mesh.palette[key]: value for key, value in remap.items() if key in mesh.palette}
        colors = [palette.get(value, value) for value in colors]
        models.append(dict(shading="foliage" if centres else "solid", id=Path(relative).stem, vertices=positions, normals=normals, colors=colors))
    target = PROJECT / "Assets/QuietCamp/Resources/QuietCamp/ArtCompositions/CampModelLibrary.json"
    target.write_text(json.dumps(dict(models=models), separators=(",", ":")) + "\n")
    prepare_water()
    print(f"Prepared {len(models)} real source meshes and Stylized Water 3 preset; placement is generated by C#.")


if __name__ == "__main__":
    prepare()
