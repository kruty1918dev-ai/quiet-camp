#!/usr/bin/env python3
"""Independent checks of the native bake's full animated plant footprints."""
import hashlib
import json
import math
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
EVIDENCE = ROOT / 'Design/Roadmap/CinematicPilot/2026-10-10'
SOURCE = ROOT / 'QuietCamp/Assets/QuietCamp/Authoring/Roadmap/Composition/CinematicPilot'


def main():
    bake = json.loads((EVIDENCE / 'bake-receipt.json').read_text())
    templates = json.loads((SOURCE / 'templates.json').read_text())
    assert all(not role.get('growsThrough') for template in templates for role in template['roles'])
    planned = bake['vegetation']
    assert planned['globalBeforeChunkSplit'] and planned['fullAnimatedFootprints'] and planned['buildingsExcludeAllPlants']
    assets = {a['id']: a for a in json.loads((SOURCE / 'assets.json').read_text())}
    structure = [p for p in bake['composition']['instances'] if not assets[p['asset']].get('wind',False)]
    surfaces = bake.get('road',{}).get('surfaces',[])
    samples = []
    for quality in ['low', 'balanced']:
        plants = planned[quality]
        assert len({p['id'] for p in plants}) == len(plants)
        assert any(p['asset'] == 'pilot.grass-tuft' for p in plants)
        minimum = float('inf')
        bins = {}
        for p in plants:
            radius = p['radius']
            assert radius > 0 and math.isfinite(radius)
            position = p['x'], p['z']
            keys = [(x,z) for x in range(math.floor((p['x']-radius-.15)/6),math.floor((p['x']+radius+.15)/6)+1)
                    for z in range(math.floor((p['z']-radius-.15)/6),math.floor((p['z']+radius+.15)/6)+1)]
            neighbours = {q['id']:q for key in keys for q in bins.get(key,[])}
            for q in neighbours.values():
                gap = math.dist(position, (q['x'], q['z']))-radius-q['radius']
                minimum = min(minimum, gap)
                assert gap >= .149, f'{quality}: intersecting animated plants {p["id"]} / {q["id"]}'
            for key in keys:
                bins.setdefault(key,[]).append(p)
            for i in range(32):
                angle = i * math.pi / 16
                x, z = p['x'] + math.cos(angle)*radius, p['z'] + math.sin(angle)*radius
                if z >= 88:
                    channel = 14 + 6 * math.sin(z*.029)
                    t = max(0, min(1, (z-138)/42)); width = 2.5 + 7.5*t*t*(3-2*t)
                    assert abs(x-channel)-width > .34, f'{quality}: crown reaches river {p["id"]}'
            for q in structure:
                a = assets[q['asset']]
                # Independent oriented rectangle check from source-catalog bounds.
                # For exact normalized donor dimensions use the measured native receipt.
                donor = next((d for d in bake['donors'] if d['asset'] == q['asset']), None)
                w, h, d = donor['bounds'] if donor else [a['width'], a['height'], a['depth']]
                yaw = math.radians(q['yaw']);dx=p['x']-q['x'];dz=p['z']-q['z']
                local_x=dx*math.cos(yaw)-dz*math.sin(yaw);local_z=dx*math.sin(yaw)+dz*math.cos(yaw)
                gap_x=max(0,abs(local_x)-w/h*q['height']/2);gap_z=max(0,abs(local_z)-d/h*q['height']/2)
                assert math.hypot(gap_x,gap_z) >= radius+.285, f'{quality}: plant reaches building {p["id"]} / {q["id"]}'
            for surface in surfaces:
                vertices=surface['points'];crosses=[];edge_distance=float('inf')
                for a,b in zip(vertices,vertices[1:]+vertices[:1]):
                    dx,dz=b['x']-a['x'],b['z']-a['z'];px,pz=p['x']-a['x'],p['z']-a['z']
                    crosses.append(dx*pz-dz*px)
                    t=max(0,min(1,(px*dx+pz*dz)/(dx*dx+dz*dz)))
                    edge_distance=min(edge_distance,math.hypot(px-t*dx,pz-t*dz))
                inside=all(v>=0 for v in crosses) or all(v<=0 for v in crosses)
                assert not inside and edge_distance>=radius+.34, f'{quality}: plant reaches road surface {p["id"]} / {surface["id"]}'
        props = [p for p in planned['groundProps'] if p['quality'] == quality]
        for prop in props:
            for plant in plants:
                gap=math.dist((prop['x'],prop['z']),(plant['x'],plant['z']))-prop['radius']-plant['radius']
                assert gap >= .149, f'{quality}: vegetation intersects a scattered rock {plant["id"]}'
        samples.append({'quality':quality,'plants':len(plants),'trees':sum(p['tree'] for p in plants),
                        'groundProps':len(props),'grassTufts':sum(p['asset']=='pilot.grass-tuft' for p in plants),
                        'shrubs':sum(p['asset']=='plant_bushSmall' for p in plants),
                        'flowers':sum(p['asset'].startswith('flower_') for p in plants),
                        'groundDetails':sum(p['asset'] in ('stone_largeA','log') for p in plants),
                        'minimumAnimatedPlantGap':minimum})
    receipt = {'sourceHash':bake['sourceHash'],'method':'Independent pair distances, 32-point crown/river checks, oriented architecture and owned road surfaces',
               'samples':samples,'result':'Passed','playerBuild':False,'mobileFpsMeasured':False}
    (EVIDENCE / 'vegetation-clearance-receipt.json').write_text(json.dumps(receipt,indent=2)+'\n')
    print('PASS independent native vegetation clearances:',samples)


if __name__ == '__main__':
    main()
