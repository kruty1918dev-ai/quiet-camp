"""The uniqueness guard must reject renamed, recoloured, resized or turned clones."""
import sys
import unittest
from pathlib import Path
sys.path.insert(0,str(Path(__file__).resolve().parents[1]))
from build_campaign_landmarks import build,fingerprint,Mesh,rigid_shape_profile,is_rigid_clone


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
        self.assertEqual(78,len({fingerprint(e['vertices']) for e in m['models']}))

    def test_a_baked_tilt_does_not_create_a_new_model(self):
        import math
        m=Mesh('box',100,'test');m.box((0,.8,0),(1.3,1.6,2.4),'wood')
        original=[v for t in m.tris for p in t[:3] for v in p];changed=[]
        for x,y,z in zip(original[::3],original[1::3],original[2::3]):
            x,z=x*math.cos(.37)+z*math.sin(.37),-x*math.sin(.37)+z*math.cos(.37)
            y,z=y*math.cos(.61)-z*math.sin(.61),y*math.sin(.61)+z*math.cos(.61)
            changed.extend((x*3+7,y*3-2,z*3+9))
        self.assertTrue(is_rigid_clone(rigid_shape_profile(original),rigid_shape_profile(changed)))


if __name__=='__main__':unittest.main()
