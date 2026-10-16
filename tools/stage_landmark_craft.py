"""Stage revised original landmarks through the existing PWB/aging authoring path.

Never publishes campaign content. Requires the already open QA Unity Editor.
"""
import argparse
import hashlib
import json
import math
import shutil
import subprocess
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
PROJECT = ROOT / 'QuietCamp'
QA = Path('C:/Users/Public/qcui-20261006/QuietCamp')
CONTENT = Path('Assets/QuietCamp/Resources/QuietCamp')


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--stage', type=Path, required=True)
    parser.add_argument('--orders', type=int, nargs='+', default=[17])
    parser.add_argument('--position', type=float, nargs=2, metavar=('X', 'Z'))
    parser.add_argument('--ruin-traces', action='store_true')
    parser.add_argument('--stone-wear', action='store_true')
    parser.add_argument('--buried-foundation', action='store_true')
    args = parser.parse_args()
    args.stage.mkdir(parents=True, exist_ok=True)
    ids = json.loads((PROJECT / CONTENT / 'campaign.json').read_text(encoding='utf-8'))['campaignLevelIds']
    catalog = json.loads((PROJECT / 'Assets/QuietCamp/Authoring/CampaignLandmarks/catalog.json').read_text(encoding='utf-8'))['entries']
    for name in ('catalog.json', 'models.json'):
        relative = Path('Assets/QuietCamp/Authoring/CampaignLandmarks') / name
        shutil.copy2(PROJECT / relative, QA / relative)
    for order in args.orders:
        if not 1 <= order <= 100:
            raise ValueError('Campaign order must be 1..100')
        entry = catalog[order - 1]
        if not entry['modelId'].startswith('qc_campaign_landmark_'):
            raise ValueError('This pass only rebuilds original construction meshes.')
        ident = ids[order - 1]
        safe = ident.replace(':', '_')
        d = json.loads((PROJECT / 'Assets/QuietCamp/Authoring/CampaignLevels' / (safe + '.draft.json')).read_text(encoding='utf-8'))
        for filename, key in [(CONTENT / ('GeneratedLevels' if ident.startswith('gen:') else 'Levels') / (safe + '.json'), 'sourceContentHash'), (CONTENT / 'ArtCompositions' / (safe + '.json'), 'sourceArtHash')]:
            assert (PROJECT / filename).read_bytes() == (QA / filename).read_bytes(), 'QA publication differs: ' + str(filename)
            d[key] = hashlib.sha256((PROJECT / filename).read_bytes()).hexdigest()
        pose = next(i for i in d['art']['instances'] if i['id'] == d['art']['primaryLandmarkInstanceId'])
        if pose.get('scaleXYZ') or pose.get('rotationEuler'):
            raise ValueError('Preserve authored nonuniform or tilted placements with a dedicated profile.')
        anchor = dict(instanceId=pose['id'], model=entry['modelId'], label=entry['label'], x=pose['x'], z=pose['z'], y=pose.get('surfaceOffset', 0), yaw=pose['yaw'], scale=pose['scale'], water=False, age='dirt', ageAmount=.38)
        if args.position:
            if len(args.orders) != 1:
                raise ValueError('A reviewed position belongs to one composition.')
            anchor['x'], anchor['z'] = args.position
        if args.stone_wear:
            anchor.update(age='streaks', ageAmount=.5, weathering=dict(material='stone', distribution='sides', patchSize=1.1))
        if args.buried_foundation:
            if entry['construction'] != 'house:hearth':
                raise ValueError('Partial burial is for the ruined masonry foundation, not furniture.')
            from campaign_surface_contact import world_vertices
            from story_campaign_review import terrain_height
            library=json.loads((PROJECT/'Assets/QuietCamp/Authoring/CampaignLandmarks/models.json').read_text(encoding='utf-8'))['models']
            model=next(m for m in library if m['id']==entry['modelId'])
            world_pose=dict(x=anchor['x'], y=0, z=anchor['z'], yaw=anchor['yaw'], scale=anchor['scale'])
            low=min(model['vertices'][1::3])
            feet=[p for k,p in enumerate(world_vertices(world_pose,model)) if model['vertices'][k*3+1]<low+.03]
            root=min(terrain_height(d['art'],x,z)-y for x,y,z in feet)-.012
            anchor['y']=root-terrain_height(d['art'],anchor['x'],anchor['z'])
            if abs(anchor['y'])>.5:
                raise ValueError('Terrain requires a designed foundation rather than deeper burial.')
        profile = dict(story=dict(id=ident), world=dict(seed=4157 + order * 97, anchors=[anchor], refineExistingPlants=False))
        trace_ids = []
        if args.ruin_traces:
            if entry['construction'] != 'house:hearth':
                raise ValueError('Roof debris belongs to the roofless hearth ruin.')
            angle = math.radians(anchor['yaw'])
            for model, x, z, yaw, scale in [('qc_broken_beam', 1.15, -.2, 36, .85), ('qc_stone_wall_fragment', -.3, .95, -24, .6)]:
                trace_id = pose['id'] + '/craft/' + model
                trace_ids.append(trace_id)
                profile['world']['anchors'].append(dict(instanceId=trace_id, model=model, label='Уламки старої споруди', x=anchor['x'] + x*math.cos(angle)+z*math.sin(angle), z=anchor['z']-x*math.sin(angle)+z*math.cos(angle), y=0, yaw=anchor['yaw']+yaw, scale=scale, water=False, age='dirt', ageAmount=.4))
        source, settings, output = (args.stage / f'L{order:03}{suffix}' for suffix in ('.source.json', '.profile.json', '.draft.json'))
        if output.exists():
            raise FileExistsError('Refusing to overwrite reviewed work: ' + str(output))
        source.write_text(json.dumps(d, ensure_ascii=False), encoding='utf-8')
        settings.write_text(json.dumps(profile, ensure_ascii=False), encoding='utf-8')
        subprocess.run([sys.executable, str(ROOT / 'tools/request_authoring_props.py'), '--project', str(QA), '--mode', 'opening-details', '--draft', str(source), '--profile', str(settings.resolve()), '--output', str(output)], check=True)
        revised = json.loads(output.read_text(encoding='utf-8'))
        if trace_ids:
            from campaign_surface_contact import settle
            models = {m['id']: m for m in revised['art']['models']}
            for item in revised['art']['instances']:
                if item['id'] in trace_ids:
                    settle(item, models[item['modelId']], revised['art'])
            output.write_text(json.dumps(revised, ensure_ascii=False), encoding='utf-8')
        # Native campaign replay uses the published ID; the editable draft keeps its preview ID.
        revised['art']['levelId'] = ident
        (args.stage / (safe + '.json')).write_text(json.dumps(revised['art'], ensure_ascii=False), encoding='utf-8')
        print(f'Staged {order}: {ident}; campaign unchanged.', flush=True)


if __name__ == '__main__':
    main()
