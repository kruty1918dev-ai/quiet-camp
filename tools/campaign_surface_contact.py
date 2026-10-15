"""Authoring geometry queries using the same frozen poses as the game renderer."""
import math
from story_campaign_review import terrain_height

def world_vertices(pose,model):
    scale=pose.get('scaleXYZ') or [pose['scale']]*3
    rx,ry,rz=map(math.radians,pose.get('rotationEuler') or [0,pose.get('yaw',0),0])
    v=model['vertices']
    for xyz in zip(v[::3],v[1::3],v[2::3]):
        x,y,z=[xyz[k]*scale[k] for k in range(3)]
        x,y=x*math.cos(rz)-y*math.sin(rz),x*math.sin(rz)+y*math.cos(rz)
        y,z=y*math.cos(rx)-z*math.sin(rx),y*math.sin(rx)+z*math.cos(rx)
        x,z=x*math.cos(ry)+z*math.sin(ry),-x*math.sin(ry)+z*math.cos(ry)
        yield x+pose['x'],y+pose['y'],z+pose['z']

def surface_height(pose,model,x,z):
    """Highest upward-facing triangle under a vertical support point."""
    vertices=list(world_vertices(pose,model));heights=[]
    for k in range(0,len(vertices),3):
        a,b,c=vertices[k:k+3]
        normal_y=(b[2]-a[2])*(c[0]-a[0])-(b[0]-a[0])*(c[2]-a[2])
        if normal_y<=.00001:continue
        det=(b[2]-c[2])*(a[0]-c[0])+(c[0]-b[0])*(a[2]-c[2])
        if abs(det)<.00001:continue
        u=((b[2]-c[2])*(x-c[0])+(c[0]-b[0])*(z-c[2]))/det
        v=((c[2]-a[2])*(x-c[0])+(a[0]-c[0])*(z-c[2]))/det
        if min(u,v,1-u-v)>=-.0001:heights.append(u*a[1]+v*b[1]+(1-u-v)*c[1])
    return max(heights) if heights else None

def settle(pose,model,art):
    """Small ground props follow gentle terrain slope without penetrating it."""
    x,z=pose['x'],pose['z'];eps=.2
    dx=(terrain_height(art,x+eps,z)-terrain_height(art,x-eps,z))/(2*eps)
    dz=(terrain_height(art,x,z+eps)-terrain_height(art,x,z-eps))/(2*eps)
    yaw=math.radians(pose.get('yaw',0));local_dx=dx*math.cos(yaw)-dz*math.sin(yaw);local_dz=dx*math.sin(yaw)+dz*math.cos(yaw)
    clamp=lambda v:max(-7,min(7,v))
    pose['rotationEuler']=[clamp(-math.degrees(math.atan(local_dz))),pose.get('yaw',0),clamp(math.degrees(math.atan(local_dx)))]
    pose['y']=0
    vertices=list(world_vertices(pose,model));bottom=min(p[1] for p in vertices)
    feet=[p for p in vertices if p[1]<bottom+.18]
    pose['y']=max(terrain_height(art,p[0],p[2])-p[1] for p in feet)+.006
    pose['surfaceAttached']=True;pose['surfaceOffset']=pose['y']-terrain_height(art,x,z)
