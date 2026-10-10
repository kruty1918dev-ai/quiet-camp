#!/usr/bin/env python3
"""Independent checks of the native bake's full animated plant footprints."""
import argparse
import hashlib
import json
import math
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
EVIDENCE = ROOT / 'Design/Roadmap/CinematicPilot/2026-10-10'
SOURCE = ROOT / 'QuietCamp/Assets/QuietCamp/Authoring/Roadmap/Composition/CinematicPilot'


def main():
    parser=argparse.ArgumentParser();parser.add_argument('--evidence',type=Path,default=EVIDENCE);options=parser.parse_args()
    evidence=options.evidence
    bake = json.loads((evidence / 'bake-receipt.json').read_text())
    landscape=json.loads((SOURCE/'valley.json').read_text()).get('landscape')
    def wet_distance(x,z):
        if not landscape:
            if z<88:return 100
            t=max(0,min(1,(z-138)/42));return abs(x-14-6*math.sin(z*.029))-(2.5+7.5*t*t*(3-2*t))
        stations=landscape['channel'];distance=100
        if stations and z>=stations[0]['z']:
            x_center=stations[-1]['x'];half_width=stations[-1]['halfWidth']
            for a,b in zip(stations,stations[1:]):
                if z<=b['z']:
                    t=max(0,min(1,(z-a['z'])/(b['z']-a['z'])));t=t*t*(3-2*t)
                    x_center=a['x']+(b['x']-a['x'])*t;half_width=a['halfWidth']+(b['halfWidth']-a['halfWidth'])*t;break
            distance=abs(x-x_center)-half_width
        for pool in landscape['pools']:
            distance=min(distance,(math.hypot((x-pool['x'])/pool['radiusX'],(z-pool['z'])/pool['radiusZ'])-(1+.09*math.sin(math.atan2((z-pool['z'])/pool['radiusZ'],(x-pool['x'])/pool['radiusX'])*3+pool['x']*.1)+.05*math.sin(math.atan2((z-pool['z'])/pool['radiusZ'],(x-pool['x'])/pool['radiusX'])*5-.8)))*min(pool['radiusX'],pool['radiusZ']))
        return distance
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
                required=radius+q['radius']
                if p['tree']!=q['tree'] and 'baseY' in p and 'baseY' in q:
                    tree,cover=(p,q) if p['tree'] else (q,p)
                    if cover['baseY']+cover['height']*1.04<tree['crownMinY']-.08:
                        required=tree['trunkRadius']+cover['radius']
                gap = math.dist(position, (q['x'], q['z']))-required
                minimum = min(minimum, gap)
                assert gap >= .149, f'{quality}: intersecting animated plants {p["id"]} / {q["id"]}'
            for key in keys:
                bins.setdefault(key,[]).append(p)
            for i in range(32):
                angle = i * math.pi / 16
                x, z = p['x'] + math.cos(angle)*radius, p['z'] + math.sin(angle)*radius
                assert wet_distance(x,z)>.34, f'{quality}: animated plant reaches authored river/pool {p["id"]}'
            for q in bake.get('structureFootprints',[]):
                yaw=math.radians(q['yaw']);dx=p['x']-q['x'];dz=p['z']-q['z']
                local_x=dx*math.cos(yaw)-dz*math.sin(yaw);local_z=dx*math.sin(yaw)+dz*math.cos(yaw)
                gap_x=max(0,abs(local_x)-q['halfX']);gap_z=max(0,abs(local_z)-q['halfZ'])
                clearance=p['trunkRadius'] if p['tree'] and p['crownMinY']>q['topY']+.08 else radius
                assert math.hypot(gap_x,gap_z)>=clearance+.285, f'{quality}: plant reaches rigid geometry {p["id"]} / {q["id"]}'
            for surface in surfaces:
                vertices=surface['points'];crosses=[];edge_distance=float('inf')
                for a,b in zip(vertices,vertices[1:]+vertices[:1]):
                    dx,dz=b['x']-a['x'],b['z']-a['z'];px,pz=p['x']-a['x'],p['z']-a['z']
                    crosses.append(dx*pz-dz*px)
                    t=max(0,min(1,(px*dx+pz*dz)/(dx*dx+dz*dz)))
                    edge_distance=min(edge_distance,math.hypot(px-t*dx,pz-t*dz))
                inside=all(v>=0 for v in crosses) or all(v<=0 for v in crosses)
                assert not inside and edge_distance>=radius+.34, f'{quality}: plant reaches road surface {p["id"]} / {surface["id"]}'
        props = [p for p in plants if p['asset'] in ('stone_largeA','log')]
        samples.append({'quality':quality,'plants':len(plants),'trees':sum(p['tree'] for p in plants),
                        'groundProps':len(props),'grassTufts':sum(p['asset']=='pilot.grass-tuft' for p in plants),
                        'shrubs':sum(p['asset']=='plant_bushSmall' for p in plants),
                        'flowers':sum(p['asset'].startswith('flower_') for p in plants),
                        'groundDetails':sum(p['asset'] in ('stone_largeA','log') for p in plants),
                        'minimumAnimatedPlantGap':minimum})
    receipt = {'sourceHash':bake['sourceHash'],'method':'Independent vertical canopy/trunk clearances, 32-point authored river/pool checks, oriented architecture and owned surfaces',
               'samples':samples,'result':'Passed','playerBuild':False,'mobileFpsMeasured':False}
    (evidence / 'vegetation-clearance-receipt.json').write_text(json.dumps(receipt,indent=2)+'\n')
    print('PASS independent native vegetation clearances:',samples)


if __name__ == '__main__':
    main()
