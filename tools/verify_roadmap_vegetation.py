#!/usr/bin/env python3
"""Independent checks of the native bake's full animated plant footprints."""
import argparse
import hashlib
import json
import math
from functools import lru_cache
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
    world=json.loads((SOURCE/'world.json').read_text());document=json.loads((SOURCE/'valley.json').read_text())
    def smooth(a,b,x):
        t=max(0,min(1,(x-a)/(b-a)));return t*t*(3-2*t)
    def lerp(a,b,t):return a+(b-a)*t
    def channel_x(z):
        for a,b in zip(landscape['channel'],landscape['channel'][1:]):
            if z<=b['z']:return lerp(a['x'],b['x'],smooth(a['z'],b['z'],z))
        return landscape['channel'][-1]['x']
    def landscape_height(x,z):
        h=1.7+.48*math.sin(x*.083+z*.031)+.42*math.cos(z*.047)+5.5*smooth(12,48,abs(x))
        for hill in landscape['hills']:
            h+=hill['height']*math.exp(-1.5*(((x-hill['x'])/hill['radiusX'])**2+((z-hill['z'])/hill['radiusZ'])**2))
        b=landscape['basin'];dx,dz=(x-b['x'])/b['radiusX'],(z-b['z'])/b['radiusZ'];angle=math.atan2(dz,dx)
        radius=math.hypot(dx,dz)/(1+b.get('shoreRoughness',0)*(math.sin(angle*3+.2)+.55*math.sin(angle*7+.8)))
        bed=b['bedHeight']+b.get('bedRoughness',.12)*(.65*math.sin(x*.23+z*.07)+.35*math.sin(z*.31-x*.16))
        h=lerp(h,lerp(bed,b['oldWaterHeight'],smooth(.67,1.03,radius)),1-smooth(1.03,1.22,radius))
        distance=abs(x-channel_x(landscape['damZ']))
        ridge=math.exp(-((z-landscape['damZ'])/5.2)**2)
        shoulder=smooth(3.5,8,distance)*(1-smooth(20,34,distance))
        h=max(h,lerp(h,landscape['damHeight'],ridge*shoulder));wet=wet_distance(x,z)
        if wet<8:
            h=lerp(landscape['waterHeight']+.9+.22*math.sin(z*.12+x*.19),h,smooth(2.8,8,wet))
            h=lerp(landscape['waterHeight']-.7,h,smooth(-.35,2.2,wet))
        return h
    foundations=[];template_by_id={t['id']:t for t in templates}
    for ensemble in document['ensembles']:
        origin=ensemble['placement'];scale=ensemble.get('scale',1);a=math.radians(origin.get('yaw',0));c,s=math.cos(a),math.sin(a)
        for role in template_by_id[ensemble['template']]['roles']:
            if role['id'] not in ('house','shelter'):continue
            asset=assets[role['asset']];factor=role['height']/asset['height']*scale
            foundations.append((origin['x']+(role['x']*c+role['z']*s)*scale,origin['z']+(-role['x']*s+role['z']*c)*scale,
                                math.radians(origin.get('yaw',0)+role.get('yaw',0)),asset['width']*factor*.52,asset['depth']*factor*.52))
    @lru_cache(maxsize=None)
    def graded_height(x,z):
        h=landscape_height(x,z)
        for px,pz,a,hx,hz in foundations:
            dx,dz=x-px,z-pz;qx,qz=dx*math.cos(a)-dz*math.sin(a),dx*math.sin(a)+dz*math.cos(a)
            h=lerp(h,landscape_height(px,pz),1-smooth(0,1.2,max(abs(qx)-hx,abs(qz)-hz)))
        return h
    def native_height(x,z):
        chunk=next((i for i,end in enumerate(world['chunkEnds']) if z<end),len(world['chunkEnds'])-1)
        a=world['minX']+math.floor((x-world['minX'])/2.5)*2.5;b=world['chunkStarts'][chunk]+math.floor((z-world['chunkStarts'][chunk])/2.5)*2.5
        aa,bb=min(a+2.5,world['maxX']),min(b+2.5,world['chunkEnds'][chunk]);u,v=(x-a)/(aa-a),(z-b)/(bb-b)
        return graded_height(a,b)*(1-u-v)+graded_height(aa,b)*u+graded_height(a,bb)*v if u+v<=1 else graded_height(aa,bb)*(u+v-1)+graded_height(a,bb)*(1-u)+graded_height(aa,b)*(1-v)
    structure = [p for p in bake['composition']['instances'] if not assets[p['asset']].get('wind',False)]
    surfaces = bake.get('road',{}).get('surfaces',[])
    samples = []
    for quality in ['low', 'balanced']:
        plants = planned[quality]
        assert len({p['id'] for p in plants}) == len(plants)
        species=json.loads((SOURCE/'vegetation.json').read_text())
        herbs={s['asset'] for s in species['herbs']};shrubs={s['asset'] for s in species['shrubs']}
        assert all(any(p['asset']==asset for p in plants) for asset in herbs|shrubs)
        minimum = float('inf')
        minimum_terrain=float('inf')
        bins = {}
        for p in plants:
            radius = p['radius']
            assert radius > 0 and math.isfinite(radius)
            assert abs(native_height(p['x'],p['z'])-p['baseY'])<.003, f'Plant root does not follow native terrain {p["id"]}'
            position = p['x'], p['z']
            keys = [(x,z) for x in range(math.floor((p['x']-radius-.15)/6),math.floor((p['x']+radius+.15)/6)+1)
                    for z in range(math.floor((p['z']-radius-.15)/6),math.floor((p['z']+radius+.15)/6)+1)]
            neighbours = {q['id']:q for key in keys for q in bins.get(key,[])}
            for q in neighbours.values():
                required=radius+q['radius']
                if p['tree']!=q['tree'] and 'baseY' in p and 'baseY' in q:
                    tree,cover=(p,q) if p['tree'] else (q,p)
                    if cover.get('topY',cover['baseY']+cover['height']*1.04)<tree['crownMinY']-.08:
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
                clearance=native_height(x,z)-landscape['waterHeight'];minimum_terrain=min(minimum_terrain,clearance)
                assert clearance>.255, f'{quality}: animated footprint reaches rendered water bank {p["id"]}'
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
                        'groundProps':len(props),'grassTufts':sum(p['asset'] in herbs for p in plants),
                        'shrubs':sum(p['asset'] in shrubs for p in plants),
                        'species':{asset:sum(p['asset']==asset for p in plants) for asset in sorted(herbs|shrubs)},
                        'flowers':sum(p['asset'].startswith('flower_') for p in plants),
                        'groundDetails':sum(p['asset'] in ('stone_largeA','log') for p in plants),
                        'minimumAnimatedPlantGap':minimum,'minimumNativeTerrainAboveWater':minimum_terrain})
    receipt = {'sourceHash':bake['sourceHash'],'method':'Independent native terrain triangles and root heights, vertical canopy/trunk clearances, 32-point authored river/pool checks, oriented architecture and owned surfaces',
               'samples':samples,'result':'Passed','playerBuild':False,'mobileFpsMeasured':False}
    (evidence / 'vegetation-clearance-receipt.json').write_text(json.dumps(receipt,indent=2)+'\n')
    print('PASS independent native vegetation clearances:',samples)


if __name__ == '__main__':
    main()
