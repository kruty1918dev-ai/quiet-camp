#!/usr/bin/env python3
"""Author first-ten recipes. Never edit generated placements or historical PNGs."""
import json
from pathlib import Path
ROOT=Path(__file__).resolve().parents[1]
DIR=ROOT/'Design/Campaign100/2026-10-11/FirstTenRefinement/Authoring'

def main():
    path=DIR/'places.json';data=json.loads(path.read_text())
    if data['schemaVersion']!=1:raise SystemExit('Refined source already exists. Edit Authoring/places.json directly; do not regenerate it.')
    data['schemaVersion']=2
    for p in data['places']:
        n=int(p['id'][2:]);p['terrain']['extent']=[-22,22,-22,22]
        p['terrain']['sampleStep']=.4
        p['terrain']['baseUndulation']=.18
        p['terrain']['soilSeed']=p['seed']
        p['planting']['treeZone']=[-18,18,-18,18]
        p['planting']['groundZone']=[-16,16,-16,16]
        p['planting']['treeCellSize']=2.0
        p['planting']['groundCellSize']=.36
        p['planting']['treeDensity']=.75
        p['planting']['groundDensity']=.82
        p['planting']['patchContrast']=.9
        p['planting']['patchScale']=.72
        p['planting']['daisyVariant']={'source':'flower_yellowA','petalColor':[.92,.92,.82,1], 'centreColor':[.89,.68,.19,1], 'minimumHeight':.22,'maximumHeight':.32}
        p['sightline']={'directionXZ':[-.707,-.707], 'treeClearanceFromBoard':2.4,'foregroundTreeClearance':5.5}
        p['camera']={'pitch':58,'yaw':225,'includeProps':True}
        p['sunElevation']=50
        rock_centres=[[6.7,0,4.3],[-8.2,0,6.5],[6.6,0,5],[-6.2,0,4.5],[6.8,0,-6],[5.8,0,5],[5.4,0,6.3],[-6.5,0,4.6],[5.7,0,-6],[6,0,-5]]
        p['rockGroups']=[{'id':p['id']+'/slope-bank','center':rock_centres[n-1],'axis':[1,0,.45],'radius':.9,'count':4}]
        p['extras']=[]
        for prop in p['props']:
            prop['supportProfile']='bottom-contact-hull; max gap 0.035; settlement 0.018'
        for r in p['routes']:
            r['edgeRoughness']=.13 if r['surface']=='dirt' else .18
            r['sampleStep']=.18;r['junctionOverlap']=.22
    def place(n):return data['places'][n-1]
    # Keep the playable rectangle flat, sculpt the actual surroundings.
    place(1)['extras']=[{'kind':'rest-pocket','center':[-5.2,0,2.4],'radius':1.15}]
    place(2)['terrain']['features'][0]['height']=.48
    place(2)['terrain']['features'] += [dict(kind='ridge',centerXZ=[-5,5],height=.48,radius=4)]
    place(2)['terrain']['transitionWidth']=3.2
    place(2)['props'][0]['target']=[-4.8,0,5.8]
    place(2)['terrain']['supportPads']=[{'centerXZ':[-4.8,5.8],'halfSizeXZ':[1.25,.95],'blendWidth':.65}]
    place(3)['props']=[]
    place(3)['extras']=[{'kind':'spring','id':'QC003/spring','center':[-5.2,0,-2.1],'radius':.64,'waterDepth':.12},
                        {'kind':'fence','id':'QC003/spring-rail','contour':[[-6.3,0,-2.8],[-6.4,0,-.9],[-5,0,-.7]],'closed':False,'height':.55}]
    place(3)['routes'][0]['points'][-1]=[-5.2,0,-3]
    # The entry is the FIRST endpoint on this route, unchanged.
    place(4)['terrain']['features'] += [dict(kind='hollow',centerXZ=[4.7,-1],height=-.18,radius=1.25)]
    place(4)['props'][0]['supportGap']=.07;place(4)['props'][0]['settlement']=.045
    place(5)['planting']['orchard']=True
    place(5)['props'] += [dict(id='QC005/orchard-old-3',owner='QC005/orchard',sourceModel='ua_valley_orchard',target=[-5.1,0,-3.5],height=2.7,yaw=41,weatheringIntensity=.25),dict(id='QC005/orchard-old-4',owner='QC005/orchard',sourceModel='ua_valley_orchard',target=[2.4,0,6.4],height=2.5,yaw=127,weatheringIntensity=.25)]
    p=place(6)
    bus=next(x for x in p['props'] if x['sourceModel']=='ua_bus_shelter_mosaic')
    bus['target']=[-7.2,0,3.7];bus['height']=1.75
    p['extras']=[{'kind':'power','id':'QC006/distribution','routeId':p['routes'][0]['id'],'offset':1.25,'supportHeight':3.25,'minimumSpan':3.5,'maximumSpan':6.8,'sag':.32,'severedSpan':1}]
    p['terrain']['supportPads']=[{'centerXZ':[-7.2,3.7],'halfSizeXZ':[1.05,.7],'blendWidth':.6}]
    # A recognisable former home: low perimeter walls, a missing entrance,
    # path ending at the threshold, garden enclosure with a real gate.
    p=place(7);p['props']=[]
    p['extras']=[{'kind':'ruin','id':'QC007/home','center':[-5.8,0,2.6],'width':2.1,'depth':1.9,'wallHeight':.42,'doorWidth':.68},
                 {'kind':'fence','id':'QC007/yard','contour':[[-7.9,0,-.2],[-4.5,0,-.3],[-4.6,0,5.4],[-8.2,0,5.2]],'closed':True,'height':.62,'gateFrom':.95,'gateTo':2.0}]
    p['routes'][0]['points'][-1:]=[[-6.45,0,-.25],[-5.8,0,1.65]]
    p['terrain']['supportPads']=[{'centerXZ':[-5.8,2.6],'halfSizeXZ':[1.18,1.04],'blendWidth':.6}]
    p=place(8);p['camera']['yaw']=235
    p['terrain']['transitionWidth']=2.8
    p['terrain']['features'] += [dict(kind='hollow',centerXZ=[6,1],height=-.45,radius=3.2)]
    p['planting']['poplars']=True
    p['routes'][1]['points'][0]=[5,0,.5]
    p=place(9);p['props'][0]['height']=.95
    p['terrain']['supportPads']=[{'centerXZ':[-5.2,2.9],'halfSizeXZ':[.6,.5],'blendWidth':.8}]
    p['terrain']['features'][0]['height']=.19
    p['terrain']['features'] += [dict(kind='ridge',centerXZ=[-5.2,2.9],height=.06,radius=2.1)]
    p=place(10);p['props'][0]['height']=.72;p['props'][1]['height']=.28
    # Source configuration for the shared low fence kit; construction and
    # connections remain the responsibility of fence-authoring.
    data['construction']={'postWidth':.065,'fenceBayLength':1.05,'railWidth':.035,'slatWidth':.055,'slatCount':5,'palette':[.37,.34,.26,1]}
    data['style']={'referenceTriangles':114,'referenceHeight':3.6,'referenceProjectedArea':10000,'maximumWorldError':.014,'preserveFeatureMinimumHeight':.12}
    path.write_text(json.dumps(data,ensure_ascii=False,indent=2)+'\n')
    print('Authored terrain, routes, spring, yard, network and vegetation sources for QC001–QC010.')

if __name__=='__main__':main()
