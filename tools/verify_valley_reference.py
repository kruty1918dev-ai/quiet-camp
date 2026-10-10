#!/usr/bin/env python3
"""Verify current art-pass evidence and its provenance; never approve art or FPS."""
import argparse
from datetime import datetime
import hashlib
import json
from pathlib import Path
import re
import subprocess

from PIL import Image

ROOT = Path(__file__).resolve().parents[1]
UNITY = ROOT / 'QuietCamp'
SOURCE = UNITY / 'Assets/QuietCamp/Authoring/Roadmap/Composition/CinematicPilot'
RENDERER_SHA = 'a1a30b94df5b0fa694c556a101a692e96055150bd99525b25d0db9321f0611bd'


def digest(path):
    with path.open('rb') as stream:
        return hashlib.file_digest(stream, 'sha256').hexdigest()


def source_hash():
    # Read the baker's ordered dependency declaration, and independently hash its
    # bytes. All paths stay project relative, exactly as in the native receipt.
    baker = (UNITY / 'Assets/QuietCamp/Editor/CinematicRoadmapBaker.cs').read_text()
    declaration = baker.split('public static string SourceHash()', 1)[1].split('static ValleyTerrain', 1)[0]
    extras = re.search(r'Concat\(new\[\]\{(.*?)\}\)', declaration, re.S).group(1)
    files = sorted(p.relative_to(UNITY).as_posix() for p in SOURCE.glob('*.json'))
    files += re.findall(r'"(Assets/[^"\n]+)"', extras)
    bindings = json.loads((SOURCE / 'bindings.json').read_text())
    files += sorted({p for binding in bindings for p in binding.get('dependencies', {})})
    text = '\n'.join(f'{p}:{digest(UNITY / p)}' for p in files)
    return hashlib.sha256(text.encode()).hexdigest()


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('--evidence', type=Path, default=ROOT / 'Design/Roadmap/ValleyRework/2026-10-10/ArtReference')
    parser.add_argument('--encoder', type=Path, default=Path.home() / '.cache/quietcamp/roadmap-tools/ffmpeg-7.0.2')
    args = parser.parse_args()
    folder = args.evidence
    read = lambda name: json.loads((folder / name).read_text())
    native, bake, plants = (read(n) for n in ('native-capture-receipt.json', 'bake-receipt.json', 'vegetation-clearance-receipt.json'))
    current = source_hash()
    assert all(r['sourceHash'] == current for r in (native, bake, plants)), 'Stale evidence/source'
    publication = (UNITY / 'Assets/QuietCamp/Resources/QuietCamp/CinematicRoadmap/World.asset').read_text()
    assert f'sourceHash: {current}' in publication
    assert native['artisticReference'] and not native['shaderErrors']
    assert bake['levelIds'] == [f'QC{i:03d}' for i in range(1, 6)]
    assert bake['vegetation']['low'] == bake['vegetation']['balanced'], 'Art pass lost content in Low'
    assert all(s['balancedTriangles'] == s['lowTriangles'] and s['balancedVertices'] == s['lowVertices'] and len(s['geometryHashes']) == 6 for s in bake['stats'])
    assert len(native['frames']) == 115
    assert len({f['file'] for f in native['frames']}) == 115
    datetime.fromisoformat(native['capturedUtc'].replace('Z', '+00:00'))
    artifacts = []
    for frame in native['frames']:
        file = folder / frame['file']
        with Image.open(file) as pixels:
            assert pixels.size == (frame['width'], frame['height'])
            pixels.verify()
        assert frame['loadedChunks'] <= 3
        assert frame['shadowAtlasResolution'] == 2048 and frame['shadowCascades'] == 1
        artifacts.append({'file': frame['file'], 'sha256': digest(file)})
    for quality in ('low', 'balanced'):
        for orientation in ('portrait', 'landscape'):
            for zoom in ('in', 'out'):
                for index in range(9):
                    assert (folder / f'{quality}-{orientation}-zoom-{zoom}-{index:02d}.png').is_file()
    assert len(native['movies']) == 2
    encoder_sha = digest(args.encoder)
    for movie in native['movies']:
        file = folder / movie['file']
        assert digest(file) == movie['sha256']
        assert encoder_sha == movie['encoderSha256']
        assert movie['sourceHash'] == current
        assert movie['frames'] == 289 and movie['fps'] == 24
        assert len(movie['frameRgbSha256']) == len(set(movie['frameRgbSha256'])) == 289
        assert movie['offlineRetimedRecording'] and not movie['mobileFpsMeasured']
        result = subprocess.run([str(args.encoder), '-hide_banner', '-loglevel', 'error', '-threads', '1', '-i', str(file), '-map', '0:v:0', '-f', 'framemd5', '-'], text=True, capture_output=True, check=True)
        decoded = [line for line in result.stdout.splitlines() if line and not line.startswith('#')]
        assert len(decoded) == 289, 'Incomplete film decode'
        artifacts.append({'file': movie['file'], 'sha256': movie['sha256'], 'decodedFrames': len(decoded)})
    assert digest(UNITY / 'Assets/QuietCamp/Settings/URP/QC_Renderer.asset') == RENDERER_SHA, 'User renderer changed'
    receipt = {'sourceHash': current, 'captureUtc': native['capturedUtc'], 'result': 'Passed',
               'artifacts': artifacts, 'artisticApproval': 'Pending', 'optimizationBudgetsAccepted': False,
               'capture': 'Production URP SingleCameraRequest, separate from Game View integration',
               'playerBuild': False, 'mobileFpsMeasured': False}
    (folder / 'artifact-integrity-receipt.json').write_text(json.dumps(receipt, indent=2) + '\n')
    print(f'PASS current authoring/native provenance; 115 PNGs; 2 fully decoded films; identical Low/Balanced art; preserved renderer. Art acceptance and strict optimization remain pending. {current}')


if __name__ == '__main__':
    main()
