"""The uniqueness guard must reject renamed, recoloured, resized or turned clones."""
import sys
import unittest
from pathlib import Path
sys.path.insert(0,str(Path(__file__).resolve().parents[1]))
from build_campaign_landmarks import build,fingerprint,Mesh,rigid_shape_profile,is_rigid_clone
from story_campaign_review import fit_primary_camera,projected_bounds


class PrimaryLandmarks(unittest.TestCase):
    def test_transformed_clone_keeps_its_identity(self):
        points=[(0,0,0),(2,0,0),(0,1,0),(0,0,3)]
        original=[v for p in points for v in p]
        turned=[v for x,y,z in reversed(points) for v in (-z*7+15,y*7-8,x*7+3)]
        self.assertEqual(fingerprint(original),fingerprint(turned))

    def test_construction_change_is_required(self):
        c,m=build()
        self.assertEqual(100,len(c['entries']))
        self.assertEqual(100,len({e['modelId'] for e in c['entries']}))
        self.assertEqual(79,len({fingerprint(e['vertices']) for e in m['models']}))
        profiles=[(e['id'],rigid_shape_profile(e['vertices'])) for e in m['models']]
        for i,(ident,profile) in enumerate(profiles):
            for other,shape in profiles[:i]:
                self.assertFalse(is_rigid_clone(profile,shape),f'{ident} duplicates {other}')
        self.assertEqual('transformer:damaged',c['entries'][47]['construction'])

    def test_a_baked_tilt_does_not_create_a_new_model(self):
        import math
        m=Mesh('box',100,'test');m.box((0,.8,0),(1.3,1.6,2.4),'wood')
        original=[v for t in m.tris for p in t[:3] for v in p];changed=[]
        for x,y,z in zip(original[::3],original[1::3],original[2::3]):
            x,z=x*math.cos(.37)+z*math.sin(.37),-x*math.sin(.37)+z*math.cos(.37)
            y,z=y*math.cos(.61)-z*math.sin(.61),y*math.sin(.61)+z*math.cos(.61)
            changed.extend((x*3+7,y*3-2,z*3+9))
        self.assertTrue(is_rigid_clone(rigid_shape_profile(original),rigid_shape_profile(changed)))

    def test_camera_fit_contains_a_rotated_landmark_and_the_playable_field(self):
        mesh=Mesh('tail',100,'tail');mesh.box((0,3,0),(22,6,12),'wood')
        model=dict(id='tail',vertices=[v for t in mesh.tris for p in t[:3] for v in p])
        pose=dict(id='primary',modelId='tail',x=-15,y=-1,z=-.6,yaw=101,rotationEuler=[-5,101,-13],scale=1)
        art=dict(models=[model],instances=[pose],primaryLandmarkInstanceId='primary',cameraEuler=[60,90,0])
        document=dict(art=art,level=dict(width=8,height=5));fit_primary_camera(document)
        centre=projected_bounds(dict(x=art['cameraTarget'][0],y=art['cameraTarget'][1],z=art['cameraTarget'][2],scale=1),dict(vertices=[0,0,0]),art)[0]
        for low_high in projected_bounds(pose,model,art,exact=True):
            for axis in range(2):self.assertLess(abs(low_high[axis]-centre[axis]),art['orthographicSize'])
        for x in (-4,4):
            for z in (-2.5,2.5):
                point=projected_bounds(dict(x=x,y=1.8,z=z,scale=1),dict(vertices=[0,0,0]),art)[0]
                for axis in range(2):self.assertLess(abs(point[axis]-centre[axis]),art['orthographicSize'])


if __name__=='__main__':unittest.main()
