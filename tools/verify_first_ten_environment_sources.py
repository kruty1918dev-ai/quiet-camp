#!/usr/bin/env python3
"""Validate compact source completeness and provenance, not visual acceptance."""
import hashlib,json,math
from pathlib import Path
ROOT=Path(__file__).resolve().parents[1]
REPORT=ROOT/'Design/Campaign100/2026-10-11/FirstTenRefinement'

def main():
    source=REPORT/'Authoring/places.json';data=json.loads(source.read_text());places=data['places']
    base=json.loads((REPORT/'Baseline/published-first-ten.json').read_text());assert len(places)==10 and data['schemaVersion']==2
    models=set()
    for name in ('roadmap_models','roadmap_story_models','campaign100_models'):
        models.update(m['id'] for m in json.loads((ROOT/'QuietCamp/Assets/QuietCamp/Resources/QuietCamp'/f'{name}.json').read_text()))
    for index,(place,old) in enumerate(zip(places,base),1):
        assert place['id']==old['id']==f'QC{index:03d}' and place['sourceLevelHash']==old['contentHash']
        current=json.loads((ROOT/'QuietCamp/Assets/QuietCamp/Resources/QuietCamp/Levels'/f'QC{index:03d}.json').read_text());assert current==old,'Published content changed after baseline'
        for orientation in ('portrait','landscape'):
            capture=REPORT/'Baseline'/f'level-{index:03d}-{orientation}.json';receipt=json.loads(capture.read_text())
            assert receipt['contentHash']==old['contentHash'] and receipt['imageSha256']==hashlib.sha256(capture.with_suffix('.png').read_bytes()).hexdigest()
        assert place['width']==old['width'] and place['height']==old['height']
        assert place['status']=='authoring-draft-not-baked' and not place['nativeAccepted'] and not place['puzzleRevisionPublished']
        assert len(place['storyUk'])>60 and len(place['acceptanceUk'])>50
        assert len(place['terrain']['features'])>=2
        assert 0<place['sunElevation']<90 and place['terrain']['sampleStep']>0
        assert len(place['rockGroups'])>0 and 0<place['planting']['groundDensity']<=1
        ids=[p['id'] for p in place['props']]+[r['id'] for r in place['routes']];assert len(ids)==len(set(ids))
        for prop in place['props']:assert prop['sourceModel'] in models and prop['height']>0 and all(math.isfinite(x) for x in prop['target'])
        for route in place['routes']:assert len(route['points'])>=2 and route['width']>0 and all(len(p)==3 and all(math.isfinite(v) for v in p) for p in route['points'])
        entry=place['entryConnection'];route=next(r for r in place['routes'] if r['id']==entry['routeId'])
        assert route['points'][0 if entry['end']=='first' else -1]==entry['point']
        ex,ez=old['entry'];w,h=old['width'],old['height'];expected=[ex+.5-w/2,0,ez+.5-h/2]
        if ex==0:expected[0]=-w/2
        elif ex==w-1:expected[0]=w/2
        elif ez==0:expected[2]=-h/2
        else:expected[2]=h/2
        assert entry['point']==expected
    assert len({json.dumps(p['terrain']['features']) for p in places})==10
    assert len({json.dumps(p['routes']) for p in places})==10
    assert next(p for p in places if p['id']=='QC002')['routes'].__len__()==2
    assert all(p['sourceModel']!='ua_bus_shelter_mosaic' for p in places[-1]['props'])
    assert data['baseCampaignCount']==100 and data['roadmapDeferred'] and not data['runtimePublication']
    for name in ('core-contracts','integration-contracts','unity-source-checks'):
        receipt=json.loads((REPORT/f'{name}.json').read_text())
        for check in receipt['checks']:
            for path,sha in check['sources'].items():assert hashlib.sha256((ROOT/path).read_bytes()).hexdigest()==sha,path
    spring=places[2];yard=places[6]
    assert any(e['kind']=='spring' for e in spring['extras']) and not spring['props']
    assert any(e['kind']=='fence' and e['closed'] and e['gateTo']>e['gateFrom'] for e in yard['extras'])
    print('PASS 10 schema-2 recipes: canonical baseline, model IDs, distinct relief/routes, exact entry anchors, source spring/yard; runtime publication/native acceptance pending.')

if __name__=='__main__':main()
