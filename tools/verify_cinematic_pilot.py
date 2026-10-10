#!/usr/bin/env python3
"""Validate recorded cinematic pilot evidence without opening Unity or building a player."""
import datetime
import hashlib
from html.parser import HTMLParser
import json
from pathlib import Path
import re
import struct
from urllib.parse import unquote, urlsplit
import xml.etree.ElementTree as ET

ROOT = Path(__file__).resolve().parent.parent
EVIDENCE = ROOT / 'Design/Roadmap/CinematicPilot/2026-10-10'


def read(name):
    return json.loads((EVIDENCE / name).read_text())


def digest(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


class Links(HTMLParser):
    def __init__(self):
        super().__init__()
        self.links = []

    def handle_starttag(self, tag, attributes):
        attrs = dict(attributes)
        if tag == 'img':
            assert attrs.get('alt'), 'Gallery image needs an accessible description'
        self.links.extend(attrs[key] for key in ('src', 'href') if key in attrs)


def main():
    native = read('native-capture-receipt.json')
    bake = read('bake-receipt.json')
    gameview = read('gameview-integration-receipt.json')
    video = read('video-receipt.json')
    acceptance = read('acceptance-receipt.json')
    assert len({d['sourceHash'] for d in (native, bake, gameview, video, acceptance)}) == 1
    assert bake['levelIds'] == ['QC001', 'QC002', 'QC003', 'QC004', 'QC005']
    assert not native['shaderErrors']
    vegetation=read('vegetation-clearance-receipt.json')
    assert vegetation['sourceHash']==bake['sourceHash'] and vegetation['result']=='Passed'
    assert all(s['minimumAnimatedPlantGap']>=.149 for s in vegetation['samples'])
    assert all(frame['opaqueShadowCasters']>0 for frame in native['frames'])
    assert all(frame['renderProfile'] == frame['quality'] for frame in native['frames'])
    assert all(frame['shadowDistance'] > 60 for frame in native['frames']), 'Aerial ground shadows are clipped'
    assert not any('tent' in asset.lower() for chunk in bake['stats'] for asset in chunk['sourceAssets'])

    project = ROOT / 'QuietCamp'
    source = project / 'Assets/QuietCamp/Authoring/Roadmap/Composition/CinematicPilot'
    baker = project / 'Assets/QuietCamp/Editor/CinematicRoadmapBaker.cs'
    hashing = baker.read_text().split('public static string SourceHash()', 1)[1].split('sealed class Terrain', 1)[0]
    files = sorted(source.glob('*.json')) + [project / name for name in re.findall(r'"(Assets/[^"\n]+)"', hashing)]
    bindings = json.loads((source / 'bindings.json').read_text())
    files += [project / path for path in sorted({path for binding in bindings for path in binding['dependencies']})]
    for binding in bindings:
        for path, sha in binding['dependencies'].items():
            assert digest(project / path) == sha, f'Reviewed donor input changed: {path}'
    assert len(bake['donors']) == len(bindings)
    assert all(donor['triangles'] > 0 and donor['paletteColors'] > 0 for donor in bake['donors'])
    assert all(donor['paletteColors'] >= 2 for donor in bake['donors'] if donor['asset'].endswith(('-tree', 'poplar', 'well', 'bench')))
    protocol = '\n'.join(f'{p.relative_to(project).as_posix()}:{digest(p)}' for p in files)
    assert hashlib.sha256(protocol.encode()).hexdigest() == bake['sourceHash'], 'Evidence does not match authoring/baker inputs'
    index = (project / 'Assets/QuietCamp/Resources/QuietCamp/CinematicRoadmap/World.asset').read_text()
    assert f'sourceHash: {bake["sourceHash"]}' in index
    assert 'm_Script: {fileID: 11500000,' in index, 'Published index has no script mapping'

    for quality, triangle_limit, material_limit in [('Low', 80000, 8), ('Balanced', 150000, 12)]:
        frames = [frame for frame in native['frames'] if frame['quality'] == quality]
        portrait = [frame for frame in frames if frame['file'].startswith(('composition-', 'low-composition-'))]
        assert {frame['route'] for frame in portrait} == {i * .5 for i in range(9)}
        for frame in frames:
            assert frame['loadedChunks'] <= 3
            assert frame['submittedMeshTriangles'] <= triangle_limit
            assert frame['sharedMaterials'] <= material_limit
    zoom = [frame for frame in native['frames'] if frame['file'].startswith('landscape-zoom-out-')]
    assert len(zoom) == 5 and all(frame['zoom'] == .85 for frame in zoom)
    for quality, prefix in [('Low', 'low-portrait-zoom-out-'), ('Balanced', 'portrait-zoom-out-')]:
        zoom = [frame for frame in native['frames'] if frame['file'].startswith(prefix)]
        assert len(zoom) == 5 and all(frame['zoom'] == .85 and frame['quality'] == quality for frame in zoom)

    edges = [frame for frame in native['frames'] if frame['file'].startswith('edge-')]
    assert len(edges) == 4 and {frame['route'] for frame in edges} == {-.25, 4.2}
    assert all(frame['zoom'] == .85 for frame in edges)

    run = ET.parse(EVIDENCE / 'integration-results.xml').getroot()
    assert run.get('result') == 'Passed' and run.get('failed') == '0'
    end = datetime.datetime.fromisoformat(run.get('end-time').replace(' ', 'T'))
    captured = datetime.datetime.fromisoformat(gameview['capturedUtc'])
    # NUnit writes whole seconds; the capture retains fractions of that same second.
    assert -1 < (end - captured).total_seconds() < 3
    visits = gameview['reentryCounts']
    assert len(visits) == 4 and all(visit == visits[0] for visit in visits)
    assert all(not added for added in read('lifecycle-diagnostics.json')['addedMaterials'])
    assert not gameview['playerBuild'] and not gameview['mobileFpsMeasured']
    assert gameview['audioOutputVerified'] is False
    tracking = gameview['navigation']
    assert len(tracking) == 36
    assert {sample['orientation'] for sample in tracking} == {'portrait', 'landscape'}
    assert {sample['zoom'] for sample in tracking} == {.85, 1, 1.15}
    assert all(.85 <= sample['worldMotionPixels'] / sample['requestedPixels'] <= 1.15 for sample in tracking)
    compiled = read('compiled-assembly-receipt.json')
    assert compiled['sourceHash'] == gameview['sourceHash'] and compiled['captureUtc'] == gameview['capturedUtc']
    assert len(compiled['assemblies']) == 6 and compiled['currentCodeFeatures'] == {'waterMotion': True, 'riverByFrontier': True}
    assert compiled['testsSourceSha256'] == digest(ROOT / 'QuietCamp/Assets/QuietCamp/Tests/PlayMode/RoadmapWorldPlayModeTests.cs')
    assert compiled['presentationSourceSha256'] == digest(ROOT / 'QuietCamp/Assets/QuietCamp/Scripts/Presentation/World/RoadmapWorldPresenter.cs')
    assert compiled['opaqueShadowAssertionExecuted'], 'Current opaque shadow check was not executed'
    water = gameview['waterMotion']
    assert len(water) == 2 and {sample['quality'] for sample in water} == {'Low', 'Balanced'}
    assert all(sample['cameraStationary'] and sample['seconds'] >= 2 and sample['meanRgbByteDifference'] > .15 for sample in water)
    assert all(sample['renderProfile'] == sample['quality'] and 'Stylized Water' in sample['shader'] for sample in water)
    measurements = gameview['measurements']
    for quality, budget in [('Low', 80), ('Balanced', 120)]:
        entries = [entry for entry in measurements if entry['quality'] == quality]
        assert {entry['route'] for entry in entries if entry['orientation'] == 'portrait'} == {i * .5 for i in range(9)}
        assert all(0 < entry['editorDrawCalls'] <= budget for entry in entries)
        assert all(entry['renderProfile'] == quality for entry in entries)

    images = acceptance['images']
    assert {entry['file'] for entry in images} == {p.name for p in EVIDENCE.glob('*.png')}
    for entry in images:
        data = (EVIDENCE / entry['file']).read_bytes()
        assert data[:8] == b'\x89PNG\r\n\x1a\n'
        assert hashlib.sha256(data).hexdigest() == entry['sha256']
        assert list(struct.unpack('>II', data[16:24])) == [entry['width'], entry['height']]
    for path, sha in acceptance['runtimeSources'].items():
        assert digest(ROOT / path) == sha, f'Runtime evidence is stale: {path}'
    assert video['encodedFrames'] == 193 and video['retimed'] is True
    assert video['decoderCheck'] == 'All 193 frames decoded successfully'
    assert digest(EVIDENCE / video['file']) == video['sha256']
    assert video['captureUtc'] == gameview['capturedUtc'], 'Video sequence is stale'
    browser = read('browser-receipt.json')
    assert browser['gallerySha256'] == digest(EVIDENCE / 'index.html')
    assert {result['viewport'] for result in browser['results']} == {320, 720, 1440}
    assert all(result['result'] == 'Passed' and not result['errors'] for result in browser['results'])
    comparison = ROOT / 'Design/Roadmap/ModelRefinement/2026-10-10'
    assert browser['sourceHash'] == bake['sourceHash']
    assert browser['comparisonGallerySha256'] == digest(comparison / 'index.html')
    review = json.loads((comparison / 'review-receipt.json').read_text())
    assert review['sourceHash'] == bake['sourceHash'] and review['gameViewResult'] == 'Passed'
    assert review['gameViewCaptureUtc'] == gameview['capturedUtc']
    for entry in review['images']:
        assert digest(comparison / entry['file']) == entry['sha256']
    for path, sha in acceptance['artifacts'].items():
        assert digest(EVIDENCE / path) == sha, f'Artifact changed after acceptance: {path}'

    page = Links(); page.feed((EVIDENCE / 'index.html').read_text())
    page.links.extend(re.findall(r'!?\[[^\]]*\]\(([^\s)]+)(?:\s+"[^"]*")?\)', (EVIDENCE / 'README.md').read_text()))
    for link in page.links:
        parsed = urlsplit(link)
        if not parsed.scheme and not parsed.netloc:
            assert (EVIDENCE / unquote(parsed.path)).exists(), f'Gallery link is missing: {link}'
    print(f'PASS: {len(native["frames"])} native frames; {len(measurements)} Game View measurements; '
          f'{len(images)} PNG hashes; authoring/runtime provenance; five-place integration, lifecycle and video receipts.')
    print('Recorded Editor evidence only. No Unity launch, player build, mobile FPS or audible-output verification.')


if __name__ == '__main__':
    main()
