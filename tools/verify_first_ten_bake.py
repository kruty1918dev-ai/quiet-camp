#!/usr/bin/env python3
"""Inspect authored buffers and ownership without Unity; not visual acceptance."""
import array,datetime,hashlib,json,math,struct,sys
from pathlib import Path
ROOT=Path(__file__).resolve().parents[1]
REPORT=ROOT/'Design/Campaign100/2026-10-11/FirstTenRefinement'
def sha(path):return hashlib.sha256(path.read_bytes()).hexdigest()
def main():
    revision=json.loads((REPORT/'Baked/current.json').read_text())['revision']
    assert len(revision)==64 and all(c in '0123456789abcdef' for c in revision)
    folder=REPORT/'Baked'/revision;receipt=json.loads((folder/'receipt.json').read_text())
    assert receipt['sourceRevision']==revision and receipt['allRequiredPlacements'] and not receipt['nativeAccepted']
    for path,value in receipt['sources'].items():assert sha(ROOT/path)==value,path
    places=json.loads((REPORT/'Authoring/places.json').read_text())['places'];results=[]
    for place in places:
        path=folder/place['id']/'manifest.json';manifest=json.loads(path.read_text())
        assert manifest['id']==place['id'] and manifest['sourceRevision']==revision and not manifest['nativeAccepted']
        assert manifest['entryConnection']==place['entryConnection'] and manifest['camera']==place['camera']
        assert {p['kind'] for p in manifest['parts']}=={'terrain','roads','structures','trees','groundcover'}
        identifiers=[];triangles=0
        for part in manifest['parts']:
            name=part['file'];assert name.isalnum() and part['kind']==name
            data=(path.parent/(name+'.bytes')).read_bytes()
            assert hashlib.sha256(data).hexdigest()==part['sha256'],name
            assert data[:8]==b'QCENV001'
            vertices,indices=struct.unpack_from('<ii',data,8)
            assert vertices==part['vertices'] and indices==part['triangles']*3
            end=16+vertices*44;assert len(data)==end+indices*4
            for values in struct.iter_unpack('<6f4B4f',memoryview(data)[16:end]):
                assert all(math.isfinite(v) for v in values),name
                assert .9<sum(v*v for v in values[3:6])<1.1,(place['id'],name,'normal')
            indexes=array.array('i');indexes.frombytes(data[end:])
            if sys.byteorder!='little':indexes.byteswap()
            assert min(indexes)>=0 and max(indexes)<vertices
            assert sum(o['triangles'] for o in part['objects'])==part['triangles']
            for obj in part['objects']:
                identifiers.append(obj['id']);assert all(math.isfinite(v) for v in obj['bounds']+obj['position'])
                assert all(v>=0 for v in obj['bounds'][3:])
                if part['kind']=='groundcover':
                    x,_,z=obj['position'];assert abs(x)>=place['width']/2 or abs(z)>=place['height']/2,'Plant on puzzle board'
            triangles+=part['triangles']
        assert len(identifiers)==len(set(identifiers)),place['id']
        assert all(p['id'] in identifiers for p in place['props'])
        assert all(e['id'] in identifiers for e in place['extras'] if e['kind'] in ('spring','ruin'))
        for link in manifest['links']:
            if link['kind']=='road':assert link['id'] in identifiers and len(link['points'])>=2
            if link['kind']=='fence':
                for join in link['connections']:assert join['a'] in identifiers and join['b'] in identifiers
                if place['id']=='QC007':assert link['closed'] and any(c['gate'] for c in link['connections'])
            if link['kind']=='power':
                for support in link['supports']:assert support in identifiers
                for span in link['spans']:
                    assert span['id'] in identifiers
                    assert span['from'] is None or span['from'] in identifiers
                    assert span['to'] is None or span['to'] in identifiers
                    if span['state']=='Intact':assert span['from'] and span['to']
                    elif span['state']=='HangingFrom':assert bool(span['from'])!=bool(span['to'])
                assert any(span['state']=='HangingFrom' for span in link['spans'])
        result=dict(id=place['id'],triangles=triangles,objects=len(identifiers),parts=len(manifest['parts']),trees=len(next(p for p in manifest['parts'] if p['kind']=='trees')['objects']),groundPlants=len(next(p for p in manifest['parts'] if p['kind']=='groundcover')['objects']),manifestSha256=sha(path))
        results.append(result);print('PASS',place['id'],triangles,'triangles;',result['trees'],'trees;',result['groundPlants'],'ground plants',flush=True)
    data=dict(checkedUtc=datetime.datetime.now(datetime.timezone.utc).isoformat(),result='Passed',sourceRevision=revision,sources=receipt['sources'],levels=results,bufferCount=50,nativeAccepted=False,unityLaunched=False,playerBuild=False,mobileFpsMeasured=False)
    (REPORT/'bake-verification.json').write_text(json.dumps(data,indent=2)+'\n')
    print('PASS 50 baked buffers: hashes, dimensions, finite/unit attributes, index ranges, IDs, narrative anchors and infrastructure connections. Native visuals pending.')
if __name__=='__main__':main()
