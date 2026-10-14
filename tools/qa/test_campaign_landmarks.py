"""The uniqueness guard must reject renamed, recoloured, resized or turned clones."""
import sys
import unittest
from pathlib import Path
sys.path.insert(0,str(Path(__file__).resolve().parents[1]))
from build_campaign_landmarks import build,fingerprint


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


if __name__=='__main__':unittest.main()
