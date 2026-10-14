"""Verify delivered aircraft images, frozen composition and installed source provenance."""
import argparse
import hashlib
import json
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
QA = Path(r'C:\Users\Public\qcui-20261006\QuietCamp')
GALLERY = ROOT / 'Design/LevelAuthoring/AircraftSeasons'
parser=argparse.ArgumentParser(description=__doc__);parser.add_argument('--surface',action='store_true');args=parser.parse_args()
REVIEW = GALLERY / ('GroundReview/G08' if args.surface else 'AirReview/A15')
SEASONS = ('spring', 'summer', 'autumn', 'winter')


def read(path):
    return json.loads(path.read_text(encoding='utf-8'))


def digest(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def source_path(relative):
    direct = QA / relative
    if direct.exists():
        return direct
    if relative.startswith('Packages/'):
        _, package, remainder = relative.split('/', 2)
        candidates = list((QA / 'Library/PackageCache').glob(package + '@*/' + remainder))
        assert len(candidates) == 1, relative
        return candidates[0]
    raise AssertionError(relative)


draft_root=QA/'Temp/ai/GroundReview/G08' if args.surface else REVIEW
drafts = [read(draft_root / season / (season + '.draft.json')) for season in SEASONS]
baseline = drafts[0]
original = read(GALLERY / 'Review/R08/spring/spring.draft.json')
assert {k:v for k,v in original['level'].items() if k not in ('contentHash','compositionHash')} == {k:v for k,v in baseline['level'].items() if k not in ('contentHash','compositionHash')}, 'Puzzle changed'
frames = []
for season, draft in zip(SEASONS, drafts):
    for key in ('models', 'instances', 'landscape'):
        assert draft['art'][key] == baseline['art'][key], (season, key)
    exported = read(GALLERY / 'Levels' / season / ('QC_STUDY_AIRCRAFT_' + season.upper() + '.draft.json'))
    assert exported['art'] == draft['art'], 'Export changed captured art: ' + season
    asset = Path('Assets/QuietCamp/Authoring/WorldStudies/AircraftSeasons') / (season + '.draft.json')
    canonical = ROOT / 'QuietCamp' / asset
    assert read(canonical) == exported
    assert canonical.read_bytes() == (QA / asset).read_bytes()
    for suffix in ('', '-detail'):
        name = season + suffix
        receipt = read(REVIEW / (name + '.json'))
        assert digest(REVIEW / (name + '.png')) == receipt['imageSha256'], name
        assert receipt['kind'] == 'native-unity-production-world-components'
        assert receipt['playerBuild'] is False and receipt['shaderErrors'] == []
        source = receipt['sourceReceipt']
        for entry in source['sources']:
            assert digest(source_path(entry['path'])) == entry['sha256'], entry['path']
        for entry in source['assemblies']:
            assert digest(QA / 'Library/ScriptAssemblies' / (entry['name'] + '.dll')) == entry['sha256']
        frames.append(dict(frame=name, imageSha256=receipt['imageSha256'], sources=len(source['sources']),
                           capturedUtc=receipt['capturedUtc']))
if args.surface:
    for name in ('spring-empty','spring-used','rain-empty','rain-used','winter-empty','winter-used'):
        receipt=read(REVIEW/(name+'.json'));assert digest(REVIEW/(name+'.png'))==receipt['imageSha256']
        assert not receipt['playerBuild'] and not receipt['shaderErrors']
        for entry in receipt['sourceReceipt']['sources']:assert digest(source_path(entry['path']))==entry['sha256'],entry['path']
        frames.append(dict(frame=name,imageSha256=receipt['imageSha256'],capturedUtc=receipt['capturedUtc']))
    log=read(GALLERY/'GroundVerification/ground-review.json');assert log['correctionCyclesPerFrame']==7
    assert len([c for c in log['cycles'] if c['countsAsCorrection']])==7
    assert all(len(c['frames'])==14 for c in log['cycles'])
else:
    log = read(GALLERY / 'air-review.json')
    expected = {season + suffix for season in SEASONS for suffix in ('', '-detail')}
    for cycle in range(1, 8):
        assert set(log['A' + str(cycle).zfill(2)]['reviewedFrames']) == expected
result = dict(seasons=4, nativeFrames=len(frames), sameModelsAndPoses=True, sameTerrain=True,
              puzzleUnchanged=True, exportMatchesCapture=True, installedCopiesIdentical=True,
              noShaderErrors=True, currentSourceHashesMatch=True, sevenCorrectionCycles=True,
              frames=frames)
(GALLERY / ('GroundVerification/composition.json' if args.surface else 'AirVerification/composition.json')).write_text(json.dumps(result, indent=2), encoding='utf-8')
print(str(len(frames))+' native frames, four exact exports, source hashes and seven review cycles verified.')
