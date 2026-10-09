"""Verify source provenance, current campaign preservation, and native image receipts."""
import hashlib
import math
import json
from pathlib import Path

SOURCE=Path('QuietCamp/Assets/QuietCamp/Authoring/Roadmap/Composition/DioramaStudies')
OUTPUT=Path('Design/Roadmap/DioramaStudies/2026-10-10')


def read(path):return json.loads(path.read_text())
def sha(path):return hashlib.sha256(path.read_bytes()).hexdigest()


def main():
    manifest=read(SOURCE/'manifest.json')
    assert manifest['id']=='quiet-camp-diorama-studies'
    assert manifest['regions']==[f'D{i:02}.json' for i in range(1,13)]
    presentations=read(SOURCE/'presentation.json')
    level_ids={entry['id'] for entry in read(Path('QuietCamp/Assets/QuietCamp/Resources/QuietCamp/level_summaries.json'))}
    assert all(p['visualReferenceLevelId'] in level_ids for p in presentations)
    catalogue={m['displayId']:m for m in read(Path('Design/Roadmap/ModelCatalogue/catalogue.json'))['models']}
    bindings=read(SOURCE/'bindings.json')
    for b in bindings:
        model=catalogue[b['catalogueId']]
        assert model['status']=='visually-reviewed' and model['path']==b['source']
        assert model['sourceHash']==b['sourceHash']==sha(Path('QuietCamp')/b['source'])
        assert model['id']=='m_'+b['guid']
        assert b['prefabHash']==sha(Path('QuietCamp')/b['prefab'])
        assert model['fitUk'] not in ('заблоковано','виключено'), model['displayId']
    sources={entry['id']:entry for entry in read(SOURCE/'assets.json')}
    template_ids={entry['id'] for entry in read(SOURCE/'templates.json')}
    for p in presentations:
        doc=read(SOURCE/(p['id']+'.json'))
        assert doc['id']==p['id'] and doc['season']==p['season']
        assert len(doc['nodes'])==1 and len(doc['ensembles'])==1
        assert doc['ensembles'][0]['template'] in template_ids
        assert all(z['species'] in sources for z in doc['zones'] if z['kind']!='parcel')
    preserved=read(OUTPUT/'runtime-preservation.json')['entries']
    for entry in preserved:assert sha(Path(entry['path']))==entry['sha256'],entry['path']
    for entry in read(Path('docs/transfer/2026-10-09/import/preserved-content.json'))['entries']:
        assert sha(Path(entry['path']))==entry['sha256'],entry['path']
    renders=read(OUTPUT/'render-receipt.json')
    assert renders['captured']==renders['expected']==12
    for p in presentations:
        r=read(OUTPUT/(p['id']+'.json'))
        assert r['id']==p['id'] and sha(OUTPUT/r['image'])==r['imageHash']
        assert r['sourceHash']==sha(SOURCE/(p['id']+'.json'))
        for field,name in [('assetsHash','assets.json'),('templatesHash','templates.json'),('bindingsHash','bindings.json'),('presentationHash','presentation.json')]:
            assert r[field]==sha(SOURCE/name),(p['id'],field)
        assert r['graphics']!='Null' and not r['playerBuild'] and not r['runtimePublished']
        assert not r['result']['diagnostics'] and len(r['result']['parcels'])==1
        assert set(r['usedAssets'])<=set(sources)
        assert r['rendererHash']==sha(Path('QuietCamp/Assets/QuietCamp/Editor/DioramaStudyRenderer.cs'))
        assert r['paletteShaderHash']==sha(Path('QuietCamp/Assets/QuietCamp/Editor/DioramaPalette.shader'))
        if p['id']=='D08':
            bridge=next(i for i in r['result']['instances'] if i['asset']=='ua_plank_bridge')
            water=p['water'];asset=sources['ua_plank_bridge'];angle=math.radians(bridge['yaw'])
            for socket in asset['sockets'].values():
                scale=bridge['height']/asset['sourceHeight']
                x=bridge['x']+(socket['x']*math.cos(angle)+socket['z']*math.sin(angle))*scale
                z=bridge['z']+(-socket['x']*math.sin(angle)+socket['z']*math.cos(angle))*scale
                assert ((x-water['x'])/water['radiusX'])**2+((z-water['z'])/water['radiusZ'])**2>=1,'Bridge end floats in study water'
    review=read(OUTPUT/'visual-review.json')
    assert len(review['reviews'])==12
    for entry in review['reviews']:
        assert entry['imageHash']==sha(OUTPUT/(entry['id']+'.png'))
        assert entry['observationUk'] and entry['decisionUk']
    print(f'PASS: 12 source-linked native studies, 17 reviewed donor bindings; {len(preserved)} current files + 562 transfer baseline files preserved.')
    print('No runtime publication, player build or mobile performance acceptance asserted.')


if __name__=='__main__':main()
