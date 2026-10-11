#!/usr/bin/env python3
"""Inspect every existing catalog level before authoring the replacement campaign.
No Unity launch, puzzle generation, save modification or player build.
"""
from pathlib import Path
import collections
import hashlib
import json
import datetime

ROOT = Path(__file__).resolve().parents[1]
CONTENT = ROOT / 'QuietCamp/Assets/QuietCamp/Resources/QuietCamp'
OUTPUT = ROOT / 'Design/Campaign100/2026-10-11'


def read(name):
    baseline = OUTPUT / 'Baseline' / name
    return json.loads((baseline if baseline.is_file() else CONTENT / name).read_text())


def frozen_path(identifier):
    folder = 'GeneratedLevels' if identifier.startswith('gen:') else 'Levels'
    baseline = OUTPUT / 'Baseline/Levels' / (identifier.replace(':', '_') + '.json')
    path = baseline if baseline.is_file() else CONTENT / folder / (identifier.replace(':', '_') + '.json')
    assert path.is_file(), identifier
    return path


def main():
    OUTPUT.mkdir(parents=True, exist_ok=True)
    baseline = OUTPUT / 'Baseline'
    baseline.mkdir(exist_ok=True)
    for name in ['campaign.json', 'monetization.json', 'bonus_camps.json', 'level_summaries.json', 'journey_summaries.json', 'world_story.json']:
        dest = baseline / name
        if not dest.exists():
            dest.write_bytes((CONTENT / name).read_bytes())
    campaign, monetization, bonuses = read('campaign.json'), read('monetization.json'), read('bonus_camps.json')
    main_ids = campaign['mvpLevelIds'] + campaign['generatedLevelIds']
    contexts = [(identifier, 'main', i + 1) for i, identifier in enumerate(main_ids)]
    for journey in monetization['journeys']:
        contexts += [(identifier, journey['id'], i + 1) for i, identifier in enumerate(journey['levelIds'])]
    contexts += [(slot['levelId'], 'bonus.' + slot['id'], slot['afterLevel']) for slot in bonuses]
    assert len(contexts) == len({row[0] for row in contexts}) == 274
    rows = []
    signatures = collections.defaultdict(list)
    for identifier, journey, number in contexts:
        path = frozen_path(identifier)
        raw = path.read_bytes()
        level = json.loads(raw)
        guests = level['guests']
        counts = dict(guests=len(guests), shadeWishes=sum(bool(g.get('shade')) for g in guests),
                      quietWishes=sum(bool(g.get('quiet')) for g in guests), friends=len(level['friends']),
                      blocked=len(level['blocked']), shadedCells=len(level['shade']), fires=len(level['noise']),
                      accessPoints=len(level.get('accessPoints', [])), exteriorCells=len(level.get('exteriorWalkable', [])))
        footprint = 4 * counts['guests']
        free = level['width'] * level['height'] - counts['blocked']
        guest_order = {g['id']: i for i, g in enumerate(guests)}
        layout = {key: level.get(key, []) for key in ['width', 'height', 'entry', 'blocked', 'shade', 'noise', 'accessPoints', 'exteriorWalkable']}
        layout['wishes'] = [(bool(g['shade']), bool(g['quiet'])) for g in guests]
        layout['friends'] = [[guest_order[g] for g in pair] for pair in level['friends']]
        signature = hashlib.sha256(json.dumps(layout, sort_keys=True).encode()).hexdigest()
        signatures[signature].append(identifier)
        # Relative pressure describes authoring priorities, not measured human difficulty.
        pressure = round(footprint / max(1, free), 3)
        topology = counts['friends'] * 2 + counts['shadeWishes'] + counts['quietWishes'] + counts['accessPoints'] + bool(counts['exteriorCells'])
        score = round(pressure * 5 + topology + max(0, counts['guests'] - 2), 2)
        env = level.get('environment', {})
        notes = []
        if number > 5 and counts['guests'] <= 2 and topology <= 1:
            notes.append('Придатний як перепочинок; не додавати навантаження лише заради номера.')
        if not level.get('environmentalStory'):
            notes.append('Немає індивідуального ансамблю історії: загальні motifs не пояснюють саме це місце.')
        if counts['exteriorCells']:
            notes.append('Видимий зовнішній прохід повинен відповідати authored walking cells.')
        if counts['fires']:
            notes.append('Багаття має бути видиме; quiet-mask відповідає реальному джерелу шуму.')
        if counts['shadeWishes']:
            notes.append('Крони мають читатися й точно збігатися з логічною тінню.')
        witness = level.get('witness', [])
        assert len(witness) == len(guests) and {p['guestId'] for p in witness} == set(guest_order), identifier
        cells = []
        for placement in witness:
            assert placement['rotation'] in range(4), identifier
            cells += [(placement['x'] + x, placement['z'] + z) for x in range(2) for z in range(2)]
        assert len(cells) == len(set(cells)) and all(0 <= x < level['width'] and 0 <= z < level['height'] for x, z in cells), identifier
        assert not set(cells).intersection(map(tuple, level['blocked'])), identifier
        row = dict(id=identifier, journey=journey, originalPosition=number, source=str(path.relative_to(ROOT)),
                   sourceSha256=hashlib.sha256(raw).hexdigest(), contentHash=level['contentHash'],
                   dimensions=[level['width'], level['height']], ruleVersion=level['ruleVersion'],
                   counts=counts, occupancyPressure=pressure, authoringPressureScore=score,
                   difficultyScope='Proxy from masks/wishes/topology; not human playtest or solver time',
                   puzzleSignature=signature, environment=env, lighting=level['lighting'],
                   witnessGeometryChecked=True, narrativeNotesUk=notes, environmentalStoryPresent=bool(level.get('environmentalStory')))
        rows.append(row)
    # Keep snapshots as design evidence, outside Unity Assets/Resources.
    duplicates = [ids for ids in signatures.values() if len(ids) > 1]
    models = json.loads((ROOT / 'Design/Roadmap/ModelCatalogue/catalogue.json').read_text())['models']
    by_path = {row['path']: row for row in models}
    model_rows = []
    next_number = max(int(row['displayId'][1:]) for row in models) + 1
    for path in sorted(list((ROOT / 'QuietCamp/Assets').rglob('*')) + list((ROOT / 'QuietCamp/Packages').rglob('*'))):
        if path.suffix.lower() not in {'.fbx', '.obj', '.blend', '.dae', '.gltf', '.glb'} or '~' in str(path):
            continue
        asset = str(path.relative_to(ROOT / 'QuietCamp'))
        previous = by_path.get(asset)
        sha = hashlib.sha256(path.read_bytes()).hexdigest()
        unchanged = bool(previous and previous['sourceHash'] == sha)
        display = previous['displayId'] if previous else 'M' + str(next_number).zfill(4)
        if not previous:
            next_number += 1
        model_rows.append(dict(number=display, path=asset, sourceSha256=sha, existingNativeProof=unchanged,
                               needsCapture=not unchanged, container=path.suffix.lower(),
                               previousImages=previous.get('images', []) if previous else [],
                               previousCaptureDate='2026-10-10' if unchanged else None))
    result = dict(schemaVersion=1, inspectedUtc=datetime.datetime.now(datetime.timezone.utc).isoformat(),
                  totalLevels=len(rows), main=110, journeys=143, bonuses=21, levels=rows,
                  exactDuplicatePuzzleGroups=duplicates, modelSources=model_rows, modelSourceCount=len(model_rows),
                  nativeModelCapturesNeeded=sum(r['needsCapture'] for r in model_rows),
                  independentSolverEvidence='../../Roadmap/Paused/2026-10-11/source-check-receipt.json',
                  nativeAcceptance=False, playerBuild=False)
    (OUTPUT / 'existing-content-audit.json').write_text(json.dumps(result, ensure_ascii=False, indent=2) + '\n')
    print('Inspected',len(rows),'catalog levels; exact duplicate puzzle groups:',len(duplicates))
    print('Model sources',len(model_rows),'requiring new source-linked native capture:',result['nativeModelCapturesNeeded'])
    print('Every witness footprint checked; full Domain witness/independent solver proof remains separate.')


if __name__ == '__main__':
    main()
