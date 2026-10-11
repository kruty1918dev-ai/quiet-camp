#!/usr/bin/env python3
"""Verify preservation and player exclusion without launching a player build.
Native imported dependency/flow evidence is checked when it exists.
"""
import hashlib
import json
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
ASSETS = ROOT / 'QuietCamp/Assets/QuietCamp'
EVIDENCE = ROOT / 'Design/Roadmap/Paused/2026-10-11'


def digest(path):
    sha = hashlib.sha256()
    with path.open('rb') as source:
        for block in iter(lambda: source.read(1024 * 1024), b''):
            sha.update(block)
    return sha.hexdigest()


def main():
    manifest = json.loads((EVIDENCE / 'archive-manifest.json').read_text())
    rows = manifest['assets']
    assert rows and len({row['archived'] for row in rows}) == len(rows)
    for row in rows:
        path = ROOT / row['archived']
        assert '/Editor/RoadmapArchive/' in row['archived']
        assert '/Resources/' not in row['archived']
        assert not (ROOT / row['source']).exists(), row['source']
        assert path.stat().st_size == row['bytes'] and digest(path) == row['sha256'], row['archived']
    assert sum(row['bytes'] for row in rows) == manifest['sourceBytes']
    renderer = ASSETS / 'Settings/URP/QC_Renderer.asset'
    assert digest(renderer) == 'a1a30b94df5b0fa694c556a101a692e96055150bd99525b25d0db9321f0611bd', 'User renderer change altered'
    for name in ['roadmap_models.json', 'roadmap_story_models.json', 'campaign.json', 'monetization.json', 'bonus_camps.json']:
        assert (ASSETS / 'Resources/QuietCamp' / name).is_file(), 'Shared gameplay or progression data removed: ' + name
    for name in ['RoadmapWorldPresenter', 'RoadmapWorldInput', 'RoadmapWorldAsset', 'RoadmapWorldChunk']:
        source = (ASSETS / 'Scripts/Presentation/World' / (name + '.cs')).read_text()
        assert source.startswith('#if UNITY_EDITOR\n') and source.rstrip().endswith('#endif')
    css = (ASSETS / 'Resources/QuietCamp/Html/Camp.css.txt').read_text()
    assert '.cinematic-roadmap' not in css and '.roadmap-exit' not in css
    main_menu = (ASSETS / 'Scripts/Presentation/UI/MenuScreens.cs').read_text()
    assert 'MenuCard("levels"' not in main_menu
    assert 'LevelLoader.MvpLevelIds()' in main_menu
    assert 'RoadmapPilotPolicy.LevelIds' not in main_menu
    guard = (ASSETS / 'Editor/DeferredRoadmapBuildGuard.cs').read_text()
    assert 'IPreprocessBuildWithReport' in guard and 'GetDependencies' in guard and 'BuildFailedException' in guard
    # Static checks and imported dependency evidence have distinct scopes.
    result = dict(result='Passed', archivedFiles=len(rows), archivedSourceBytes=manifest['sourceBytes'],
                  archiveHashesPreserved=True, rendererSha256=digest(renderer),
                  sourceGuards=True, menuMapCardRemoved=True, playerBuildPerformed=False,
                  actualPackageSizeMeasured=False, mobileFpsMeasured=False)
    native = EVIDENCE / 'native-flow-receipt.json'
    if native.exists():
        evidence = json.loads(native.read_text())
        assert evidence['result'] == 'Passed' and evidence['archiveReachable'] is False
        assert evidence['freshStart'] == 'QC001' and evidence['next'] == 'QC002' and evidence['afterPilot'] == 'QC006'
        assert evidence['noMapComponents'] and evidence['noMapMenuControl']
        result['nativeFlowCaptureUtc'] = evidence['capturedUtc']
        result['nativeReceiptSha256'] = digest(native)
    (EVIDENCE / 'verification-receipt.json').write_text(json.dumps(result, ensure_ascii=False, indent=2) + '\n')
    print(f"PASS: {len(rows)} archived files / {manifest['sourceBytes']} source bytes; GUIDs and hashes preserved")
    print('PASS: deferred world source guards, menu route removal, build dependency guard, user renderer preservation')
    print('No player build or compressed package-size measurement performed.')


if __name__ == '__main__':
    main()
