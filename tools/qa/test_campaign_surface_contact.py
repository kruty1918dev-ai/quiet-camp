import sys,unittest
from pathlib import Path
sys.path.insert(0,str(Path(__file__).resolve().parents[1]))
from campaign_surface_contact import surface_height,world_vertices,settle

class AuthoredContact(unittest.TestCase):
    def test_support_uses_transformed_upward_surface_not_bounds(self):
        model=dict(vertices=[-1,1,-1,-1,1,1,1,1,1, -1,1,-1,1,1,1,1,1,-1])
        pose=dict(x=4,y=2,z=-3,yaw=40,scale=2)
        self.assertAlmostEqual(4,surface_height(pose,model,4,-3))
        self.assertIsNone(surface_height(pose,model,20,20))
    def test_settlement_keeps_the_low_feet_above_a_slope(self):
        art=dict(landscape=dict(resolution=3,size=8,originX=-4,originZ=-4,heights=[-.2,0,.2]*3))
        model=dict(vertices=[-.3,0,-.3,-.3,0,.3,.3,0,.3, -.3,0,-.3,.3,0,.3,.3,0,-.3])
        pose=dict(x=0,y=0,z=0,yaw=35,scale=1);settle(pose,model,art)
        for x,y,z in world_vertices(pose,model):self.assertGreaterEqual(y-x*.05,.005)
        self.assertNotEqual([0,35,0],pose['rotationEuler'])

if __name__=='__main__':unittest.main()
