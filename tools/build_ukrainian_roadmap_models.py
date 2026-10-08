#!/usr/bin/env python3
"""Bake original Ukrainian rural presentation meshes; never edits gameplay data.

Default: deterministic OBJ/MTL + normalized RoadmapModelLibrary JSON export.
--check: validate geometry, budgets, source files and repeatable export without writes.
--preview: additionally render a software contact sheet (not a Unity acceptance render).
Only Python's standard library is required unless --preview is requested.
"""
from __future__ import annotations

import argparse
import hashlib
import json
import math
from pathlib import Path
import random
import uuid

ROOT = Path(__file__).resolve().parents[1]
ASSETS = ROOT / "QuietCamp/Assets/QuietCamp"
SOURCE = ASSETS / "Authoring/Roadmap/Models/UkrainianRural"
RESOURCE = ASSETS / "Resources/QuietCamp/roadmap_culture_models.json"
PREVIEW = ROOT / "TestResults/ukrainian-roadmap-2026-10-08/original-model-contact-sheet.png"
GUID_NAMESPACE = uuid.UUID("f00e5768-1262-403a-a44b-0dba3441e126")

# Shared subdued palette: no textures or private material instances required.
PALETTE = {
    "steel": 0x687776, "steel_dark": 0x465951, "rust": 0x8E7058,
    "insulator": 0xC5CFB8, "concrete": 0xB0B1A1, "concrete_dark": 0x898E80,
    "wood": 0x94775A, "wood_dark": 0x6D604A, "wood_light": 0xB2A083,
    "wicker": 0x9B8360, "wicker_dark": 0x74674C,
    "plaster": 0xE7E2CA, "plaster_worn": 0xC6C3AF, "blue": 0x7293A4,
    "blue_dark": 0x4B727F, "window": 0x405F60, "roof": 0x727B72,
    "roof_light": 0x90968B, "stone": 0xACA88D,
    "leaf": 0x6E8650, "leaf_light": 0x8A9C60, "leaf_dark": 0x4C6B44,
    "stalk": 0xAFA45F, "wheat": 0xD4B568, "wheat_light": 0xE1CC87,
    "sunflower": 0xE0BB4E, "seed": 0x6B6142, "apple": 0xB78657,
}


def add(a, b): return tuple(a[i] + b[i] for i in range(3))
def sub(a, b): return tuple(a[i] - b[i] for i in range(3))
def scale(a, n): return tuple(v * n for v in a)
def dot(a, b): return sum(a[i] * b[i] for i in range(3))
def cross(a, b): return (a[1]*b[2]-a[2]*b[1], a[2]*b[0]-a[0]*b[2], a[0]*b[1]-a[1]*b[0])
def length(a): return math.sqrt(dot(a, a))
def unit(a):
    size = length(a)
    if size < 1e-10: raise ValueError("zero length vector")
    return scale(a, 1 / size)


class Mesh:
    def __init__(self, identity, budget, description):
        self.id, self.budget, self.description = identity, budget, description
        self.tris = []

    def tri(self, a, b, c, color):
        normal = unit(cross(sub(b, a), sub(c, a)))
        self.tris.append((a, b, c, normal, color))

    def quad(self, a, b, c, d, color, outward=None):
        if outward is not None and dot(cross(sub(b, a), sub(c, a)), outward) < 0:
            a, b, c, d = d, c, b, a
        self.tri(a, b, c, color)
        self.tri(a, c, d, color)

    def box(self, center, size, color):
        x, y, z = center; w, h, d = (v/2 for v in size)
        p = [(x-w,y-h,z-d),(x+w,y-h,z-d),(x+w,y+h,z-d),(x-w,y+h,z-d),
             (x-w,y-h,z+d),(x+w,y-h,z+d),(x+w,y+h,z+d),(x-w,y+h,z+d)]
        for f, n in [((0,3,2,1),(0,0,-1)),((4,5,6,7),(0,0,1)),
                     ((0,4,7,3),(-1,0,0)),((1,2,6,5),(1,0,0)),
                     ((0,1,5,4),(0,-1,0)),((3,7,6,2),(0,1,0))]:
            self.quad(*(p[i] for i in f), color, n)

    def beam(self, a, b, width, color, closed=False, depth=None):
        """Square/rectangular prism; hidden end caps omitted unless requested."""
        axis = unit(sub(b, a))
        reference = (0,1,0) if abs(axis[1]) < .9 else (1,0,0)
        u = scale(unit(cross(axis, reference)), width/2)
        v = scale(unit(cross(axis, u)), (width if depth is None else depth)/2)
        ring = [add(u,v),sub(v,u),scale(add(u,v),-1),sub(u,v)]
        bottom, top = ([add(a,r) for r in ring], [add(b,r) for r in ring])
        for i in range(4):
            j = (i+1)%4
            self.quad(bottom[i],bottom[j],top[j],top[i],color,add(ring[i],ring[j]))
        if closed:
            self.quad(*bottom,color,scale(axis,-1)); self.quad(*top,color,axis)

    def tube(self,a,b,radius,color,sides=6,closed=False,top_radius=None):
        """Round faceted twig along any axis; used for genuinely woven rods."""
        axis=unit(sub(b,a));reference=(0,1,0) if abs(axis[1])<.9 else (1,0,0)
        u=unit(cross(axis,reference));v=unit(cross(axis,u))
        top_radius=radius if top_radius is None else top_radius
        ring=[add(scale(u,math.cos((i+.17)*math.tau/sides)),scale(v,math.sin((i+.17)*math.tau/sides))) for i in range(sides)]
        bottom=[add(a,scale(r,radius)) for r in ring];top=[add(b,scale(r,top_radius)) for r in ring]
        for i in range(sides):
            j=(i+1)%sides
            self.quad(bottom[i],bottom[j],top[j],top[i],color,add(ring[i],ring[j]))
            if closed:
                self.tri(a,bottom[j],bottom[i],color);self.tri(b,top[i],top[j],color)

    def cylinder(self, center, radius, height, color, sides=6, top_radius=None, caps=True):
        x,y,z = center; top_radius = radius if top_radius is None else top_radius
        bottom=[(x+radius*math.cos(i*math.tau/sides),y-height/2,z+radius*math.sin(i*math.tau/sides)) for i in range(sides)]
        top=[(x+top_radius*math.cos(i*math.tau/sides),y+height/2,z+top_radius*math.sin(i*math.tau/sides)) for i in range(sides)]
        for i in range(sides):
            j=(i+1)%sides
            self.quad(bottom[i],bottom[j],top[j],top[i],color,(math.cos((i+.5)*math.tau/sides),0,math.sin((i+.5)*math.tau/sides)))
            if caps:
                self.tri((x,y-height/2,z),bottom[i],bottom[j],color)
                self.tri((x,y+height/2,z),top[j],top[i],color)

    def extrude(self, polygon, depth, color, z=0):
        """Convex polygon in XY, extruded along Z."""
        back=[(x,y,z-depth/2) for x,y in polygon]; front=[(x,y,z+depth/2) for x,y in polygon]
        cx=sum(x for x,y in polygon)/len(polygon);cy=sum(y for x,y in polygon)/len(polygon)
        for i in range(1,len(polygon)-1):
            # Polygon is counterclockwise in XY.
            self.tri(back[0],back[i+1],back[i],color);self.tri(front[0],front[i],front[i+1],color)
        for i in range(len(polygon)):
            j=(i+1)%len(polygon)
            self.quad(back[i],back[j],front[j],front[i],color,((polygon[i][0]+polygon[j][0])/2-cx,(polygon[i][1]+polygon[j][1])/2-cy,0))

    def leaf(self, origin, tip, width, color):
        axis=unit(sub(tip,origin)); side=scale(unit(cross(axis,(0,0,1))),width/2)
        middle=add(origin,scale(sub(tip,origin),.6))
        # A folded leaf has area and readable shading, rather than one billboard.
        fold=add(middle,(0,.025,0)); left=add(middle,side);right=sub(middle,side)
        self.tri(origin,left,fold,color);self.tri(left,tip,fold,color)
        self.tri(origin,fold,right,color);self.tri(right,fold,tip,color)
        self.tri(origin,fold,left,color);self.tri(left,fold,tip,color)
        self.tri(origin,right,fold,color);self.tri(right,tip,fold,color)

    def crown(self, center, radius, color, sides=7, height=None):
        x,y,z=center; height=radius*1.45 if height is None else height
        ring=[(x+radius*math.cos(i*math.tau/sides),y,z+radius*math.sin(i*math.tau/sides)) for i in range(sides)]
        upper=(x+.1*radius,y+height/2,z-.06*radius);lower=(x-.07*radius,y-height/2,z+.06*radius)
        for i in range(sides):
            j=(i+1)%sides
            self.tri(upper,ring[j],ring[i],color);self.tri(lower,ring[i],ring[j],color)

    def bounds(self):
        vertices=[p for t in self.tris for p in t[:3]]
        low=tuple(min(p[i] for p in vertices) for i in range(3))
        high=tuple(max(p[i] for p in vertices) for i in range(3))
        return low,high

    def baked(self):
        lo,hi=self.bounds();height=hi[1]-lo[1]
        origin=((lo[0]+hi[0])/2,lo[1],(lo[2]+hi[2])/2)
        data=[];colors=[]
        for a,b,c,normal,color in self.tris:
            for p in (a,b,c):data.extend(round(v,6) for v in (*scale(sub(p,origin),1/height),*normal))
            colors.append(PALETTE[color])
        return {"id":self.id,"data":data,"colors":colors}


def pylon():
    m=Mesh("ua_power_pylon",1100,"Tapered open steel lattice utility tower, three crossarms and six hanging insulators; generic rural infrastructure, not a site replica.")
    levels=[(0,1.8),(3.6,1.35),(7.5,.95),(10.2,.62),(13,.45),(16,.34)]
    for (y0,w0),(y1,w1) in zip(levels,levels[1:]):
        for sx in (-1,1):
            for sz in (-1,1):m.beam((sx*w0,y0,sz*w0),(sx*w1,y1,sz*w1),.13,"steel")
        # X braces on all four faces. Small profile stays open at miniature scale.
        for side in (-1,1):
            m.beam((-w0,y0,side*w0),(w1,y1,side*w1),.095,"steel_dark")
            m.beam((w0,y0,side*w0),(-w1,y1,side*w1),.095,"steel_dark")
            m.beam((side*w0,y0,-w0),(side*w1,y1,w1),.095,"steel")
            m.beam((side*w0,y0,w0),(side*w1,y1,-w1),.095,"steel")
        if y1 <16:
            for side in (-1,1):
                m.beam((-w1,y1,side*w1),(w1,y1,side*w1),.105,"steel")
                m.beam((side*w1,y1,-w1),(side*w1,y1,w1),.105,"steel")
    for y,half in [(10.2,3.9),(13,3.45),(15.8,2.8)]:
        m.beam((-half,y,0),(half,y,0),.19,"steel",True,.28)
        for sx in (-1,1):
            m.beam((sx*.45,y-1,0),(sx*(half-.25),y,0),.095,"steel_dark")
            m.cylinder((sx*(half-.22),y-.48,0),.18,.8,"insulator",4)
            # Two saucer bands make porcelain strings recognizable.
            for dy in (-.19,.15):m.cylinder((sx*(half-.22),y-.48+dy,0),.245,.055,"insulator",4,caps=False)
    for sx in (-1,1):
        for sz in (-1,1):m.box((sx*1.8,.09,sz*1.8),(.55,.18,.55),"concrete")
    return m


def rural_pole():
    m=Mesh("ua_rural_pole",200,"Concrete village utility pole with two narrow crossarms and porcelain insulators.")
    m.cylinder((0,3.65,0),.12,7.3,"concrete",5,.08)
    for y,w in [(6.4,1.1),(7.1,.72)]:
        m.beam((-w,y,0),(w,y,0),.11,"steel_dark",True)
        for sx in (-1,1):m.cylinder((sx*(w-.12),y+.12,0),.075,.18,"insulator",4)
    m.box((0,.35,-.14),(.19,.24,.07),"steel_dark")
    return m


def wattle():
    m=Mesh("ua_wattle_fence",650,"Modular woven willow fence: fifteen dense rows of thin round twigs weave front/back around four rough tapered stakes, with real gaps and irregular tops.")
    for i,x in enumerate([-1.6,-.53,.53,1.6]):m.tube((x,0,0),(x+(.025 if i%2 else -.025),1.18+(i%3)*.04,0),.049,"wood_dark",5,True,.038)
    for row in range(15):
        points=[(-1.66+col*(3.32/3),.14+row*.057+math.sin(col+row)*.009, (.075 if (col+row)%2 else -.075)) for col in range(4)]
        for col,(a,b) in enumerate(zip(points,points[1:])):
            radius=.019+(col+row)%3*.0015
            m.tube(a,b,radius,"wicker" if (col+row)%3 else "wicker_dark",6)
    return m


def picket():
    m=Mesh("ua_picket_fence",400,"Modular weathered vertical wooden picket fence, imperfect tops, two rails and open spacing.")
    for y in (.35,.9):m.box((0,y,.06),(3.25,.10,.09),"wood_dark")
    for i in range(11):
        x=-1.44+i*.288;h=1.13+(i%3)*.055
        m.extrude([(x-.105,0),(x+.105,0),(x+.105,h-.13),(x,h),(x-.105,h-.13)],.07,"wood" if i%3 else "wood_light",-.025)
    for x in (-1.6,1.6):m.box((x,.66,.025),(.13,1.32,.13),"wood_dark")
    return m


def concrete_fence():
    m=Mesh("ua_concrete_fence",400,"Modular precast concrete village fence: three inset panels, raised diamond relief, slotted upper course and darker seams.")
    for x in (-1.65,1.65):m.box((x,.82,0),(.22,1.64,.25),"concrete_dark")
    for y in (.24,.73,1.21):
        m.box((0,y,0),(3.1,.43,.11),"concrete")
        for sy in (-1,1):m.box((0,y+sy*.175,-.072),(3.00,.045,.03),"plaster_worn")
    for x in (-1.12,-.56,0,.56,1.12):
        for y in (.73,1.21):
            # Small pointed relief on the visible side; distinguish it from an opaque slab.
            m.extrude([(x-.16,y),(x,y-.13),(x+.16,y),(x,y+.13)],.04,"concrete_dark",-.079)
    for x in (-1.4,0,1.4):m.box((x,1.49,0),(.23,.15,.11),"concrete")
    m.box((0,1.61,0),(3.2,.10,.13),"plaster_worn")
    return m


def gate():
    m=Mesh("ua_gate",500,"Village double wooden gate with blue paint worn back to wood, partly open leaves, diagonal braces and stone posts.")
    for x in (-1.72,1.72):m.box((x,.94,0),(.22,1.88,.23),"plaster_worn")
    for side in (-1,1):
        first=len(m.tris)
        for row in (.38,1.28):m.box((side*.83,row,0),(1.56,.105,.095),"blue_dark")
        for col in range(6):
            x=side*(.13+col*.28)
            m.extrude([(x-.115,.15),(x+.115,.15),(x+.115,1.54),(x,1.65),(x-.115,1.54)],.075,"blue" if col%3 else "wood",-.08)
        m.beam((side*.08,.35,.055),(side*1.57,1.31,.055),.08,"wood_dark",True)
        m.box((side*.095,.94,-.145),(.04,.13,.035),"steel_dark")
        # Swing both leaves gently into the yard, leaving a readable central opening.
        angle=math.radians(-side*(28 if side<0 else 24));pivot=side*1.60
        def turn(p,hinge=pivot):
            x,y,z=p;x-=hinge
            return (hinge+x*math.cos(angle)+z*math.sin(angle),y,-x*math.sin(angle)+z*math.cos(angle))
        for index in range(first,len(m.tris)):
            a,b,c,n,color=m.tris[index]
            m.tris[index]=(turn(a),turn(b),turn(c),turn(n,0),color)
    return m


def wheat():
    m=Mesh("ua_wheat_patch",550,"Cluster of twenty grain heads in gently irregular rows; stalks, tapering heads, folded leaves, no terrain slab.")
    rng=random.Random(1918)
    for row in range(4):
        for col in range(5):
            x=(col-2)*.29+rng.uniform(-.07,.07);z=(row-1.5)*.28+rng.uniform(-.06,.06)
            height=rng.uniform(.8,1.06);bend=rng.uniform(.035,.075)
            m.beam((x,0,z),(x+bend,height-.18,z),.018,"stalk")
            m.crown((x+bend,height-.1,z),.055,"wheat" if col%2 else "wheat_light",4,.25)
            # Four folded grain faces + an awn rather than many tiny invisible grains.
            m.beam((x+bend,height+.025,z),(x+bend+.008,height+.10,z),.008,"wheat_light")
    return m


def sunflower():
    m=Mesh("ua_sunflower_patch",550,"Nine full sunflowers with dark seed disks, seven broad petals each and living folded leaves.")
    rng=random.Random(2014)
    for row in range(3):
        for col in range(3):
            x=(col-1)*.49+rng.uniform(-.08,.08);z=(row-1)*.45+rng.uniform(-.07,.07);h=rng.uniform(1.15,1.52)
            m.beam((x,0,z),(x+.07,h-.13,z),.029,"leaf_dark")
            for side in (-1,1):m.leaf((x+.04,h*.48,z),(x+side*.24,h*.58,z-.07),.17,"leaf")
            cx=x+.07;cy=h-.13
            # Flower faces south (-Z), therefore readable from the roadmap camera.
            for i in range(7):
                angle=i*math.tau/7
                inner=(cx+.077*math.cos(angle),cy+.077*math.sin(angle),z-.025)
                a=(cx+.135*math.cos(angle-.32),cy+.135*math.sin(angle-.32),z-.018)
                tip=(cx+.22*math.cos(angle),cy+.22*math.sin(angle),z)
                b=(cx+.135*math.cos(angle+.32),cy+.135*math.sin(angle+.32),z-.018)
                m.quad(inner,b,tip,a,"sunflower",(0,0,-1))
            center=(cx,cy,z-.035)
            for i in range(7):
                a=i*math.tau/7;b=(i+1)*math.tau/7
                m.tri(center,(cx+.092*math.cos(b),cy+.092*math.sin(b),z-.03),(cx+.092*math.cos(a),cy+.092*math.sin(a),z-.03),"seed")
    return m


def wheat_lod():
    m=Mesh("ua_wheat_patch_lod",180,"Far-field grain silhouette: nine stalks and heads, no individual awns; original low-detail counterpart of ua_wheat_patch.")
    for row in range(3):
        for col in range(3):
            x=(col-1)*.635;z=(row-1)*.43;h=.96+((row+col)%3)*.053
            m.beam((x,0,z),(x+.045,h-.18,z),.022,"stalk")
            m.crown((x+.045,h-.1,z),.061,"wheat" if col%2 else "wheat_light",4,.25)
    return m


def sunflower_lod():
    m=Mesh("ua_sunflower_patch_lod",150,"Far-field sunflower silhouette: four stalks and flat petal/seed faces, no small leaves; original low-detail counterpart of ua_sunflower_patch.")
    for row in range(2):
        for col in range(2):
            x=(col-.5)*1.12;z=(row-.5)*.85;h=1.34+((row+col)%2)*.09
            m.beam((x,0,z),(x+.05,h-.13,z),.031,"leaf_dark")
            cx=x+.05;cy=h-.13
            for i in range(7):
                a=i*math.tau/7
                p=[(cx+.077*math.cos(a),cy+.077*math.sin(a),z-.025),
                   (cx+.135*math.cos(a-.32),cy+.135*math.sin(a-.32),z-.018),
                   (cx+.22*math.cos(a),cy+.22*math.sin(a),z),
                   (cx+.135*math.cos(a+.32),cy+.135*math.sin(a+.32),z-.018)]
                m.quad(p[0],p[3],p[2],p[1],"sunflower",(0,0,-1))
                b=(i+1)*math.tau/7
                m.tri((cx,cy,z-.035),(cx+.092*math.cos(b),cy+.092*math.sin(b),z-.03),(cx+.092*math.cos(a),cy+.092*math.sin(a),z-.03),"seed")
    return m


def orchard():
    m=Mesh("ua_orchard_tree",300,"Low fruit-tree silhouette with visible branching, clustered crowns and sparse warm apples; not a conifer cone.")
    m.cylinder((0,.78,0),.13,1.56,"wood_dark",5,.075)
    for a,b in [((0,.73,0),(-.85,1.85,.12)),((0,.95,0),(.82,2,.25)),((0,1.1,0),(.12,2.35,-.52))]:m.beam(a,b,.085,"wood")
    for center,radius,color in [((-.72,2.01,.13),.82,"leaf"),((.63,2.13,.25),.87,"leaf_light"),((.1,2.4,-.42),.78,"leaf"),((0,1.86,.5),.67,"leaf_dark")]:m.crown(center,radius,color,7)
    for p in [(-.84,1.72,-.43),(.67,1.89,-.38),(.27,1.53,.86),(-.04,2.11,-1.05)]:m.crown(p,.055,"apple",4,.085)
    return m


def shelter():
    m=Mesh("ua_bus_shelter",600,"Small rural stop with pale plaster sides, faded blue fascia, lightweight flat roof and wooden bench; no invented historical markings.")
    m.box((0,.07,0),(3.3,.14,1.72),"concrete_dark")
    m.box((0,1.06,.77),(3.08,1.9,.12),"plaster_worn")
    for x in (-1.49,1.49):m.box((x,1.06,.07),(.14,1.9,1.47),"plaster")
    # Asymmetric thin roof, not a box silhouette.
    m.extrude([(-1.74,2.06),(1.74,2.06),(1.74,2.2),(-1.74,2.2)],1.99,"roof",0)
    m.box((0,2.015,-.77),(3.07,.15,.1),"blue")
    m.box((0,.56,.45),(2.2,.12,.36),"wood")
    m.box((0,.84,.66),(2.2,.28,.065),"wood_light")
    for x in (-.82,.82):m.box((x,.33,.45),(.095,.49,.2),"steel_dark")
    for x in (-.97,0,.97):m.box((x,1.33,.695),(.045,.77,.025),"blue_dark")
    for x,y in [(-1.02,.28),(.91,.45),(.35,1.5)]:m.box((x,y,.693),(.31,.13,.02),"plaster")
    return m


def window(m,x,y,z,width=.86,height=.72):
    m.box((x,y,z),(width,height,.035),"window")
    for dx in (-width/2,width/2):m.box((x+dx,y,z-.035),(.075,height+.10,.065),"blue")
    for dy in (-height/2,height/2):m.box((x,y+dy,z-.035),(width+.14,.075,.065),"blue")
    m.box((x,y,z-.04),(.04,height,.055),"plaster")
    m.box((x,y,z-.04),(width,.035,.055),"plaster")
    m.box((x,y-height/2-.06,z-.06),(width+.22,.085,.13),"blue_dark")


def house():
    m=Mesh("ua_whitewashed_house",900,"Anonymous single-storey village home: white lime plaster, blue lower band and wood window trim, modest gable roof, chimney and doorstep. No national costume caricature.")
    w=5.1;d=6.0;wall=2.7;ridge=4.02
    m.extrude([(-w/2,0),(w/2,0),(w/2,wall),(0,ridge),(-w/2,wall)],d,"plaster")
    m.box((0,.17,-3.026),(5.10,.34,.07),"blue")
    m.box((0,.17,3.026),(5.10,.34,.07),"blue")
    for x in (-2.575,2.575):m.box((x,.17,0),(.07,.34,6),"blue")
    # Two extruded slabs follow the gable slope and overlap it at the eaves.
    for side in (-1,1):
        coords=[(0,4.03),(side*2.88,2.57),(side*2.88,2.7),(0,4.16)]
        # Keep polygon counterclockwise for consistent normal/export contract.
        area=sum(coords[i][0]*coords[(i+1)%4][1]-coords[(i+1)%4][0]*coords[i][1] for i in range(4))
        if area<0:coords.reverse()
        m.extrude(coords,6.5,"roof")
        for i in range(9):
            z=-3.17+i*.79
            m.beam((0,4.171,z),(side*2.9,2.71,z),.028,"roof_light")
    for x in (-1.65,.12):window(m,x,1.48,-3.025,.83,.94)
    m.box((1.69,1.10,-3.035),(.87,2.04,.07),"wood_dark")
    for x in (1.24,2.14):m.box((x,1.12,-3.085),(.075,2.15,.08),"blue")
    m.box((1.69,2.19,-3.085),(1,.085,.08),"blue")
    for y in (.55,1.11,1.7):m.box((1.69,y,-3.082),(.64,.055,.025),"wood")
    m.box((1.97,1.08,-3.12),(.055,.055,.04),"steel_dark")
    m.box((1.69,.08,-3.3),(1.16,.16,.68),"stone")
    m.box((-.12,2.47,-3.024),(.42,.30,.045),"window")
    m.box((.94,3.94,.98),(.43,1.02,.43),"plaster_worn")
    m.box((.94,4.48,.98),(.52,.13,.52),"concrete_dark")
    # Restrained chips at the foot of the wall; geometry stays clean/readable.
    for x in (-2.16,-.81,.67):m.box((x,.30,-3.066),(.29,.115,.022),"plaster_worn")
    return m


def well():
    m=Mesh("ua_well",400,"Open stone well with wooden roof, winding axle and handle, visible inner dark shaft; nothing floating over a solid block.")
    sides=8;r=.61;inner=.43
    for i in range(sides):
        a=i*math.tau/sides;b=(i+1)*math.tau/sides
        p=[(r*math.cos(a),.12,r*math.sin(a)),(r*math.cos(b),.12,r*math.sin(b)),
           (r*math.cos(b),.88,r*math.sin(b)),(r*math.cos(a),.88,r*math.sin(a))]
        m.quad(*p,"stone",(math.cos((a+b)/2),0,math.sin((a+b)/2)))
        q=[(inner*math.cos(a),.12,inner*math.sin(a)),(inner*math.cos(b),.12,inner*math.sin(b)),
           (inner*math.cos(b),.88,inner*math.sin(b)),(inner*math.cos(a),.88,inner*math.sin(a))]
        m.quad(*q,"concrete_dark",(-math.cos((a+b)/2),0,-math.sin((a+b)/2)))
        m.quad(p[3],p[2],q[2],q[3],"plaster_worn",(0,1,0))
    for x in (-.74,.74):m.box((x,1,0),(.12,2,.14),"wood_dark")
    m.beam((-.82,1.39,0),(.9,1.39,0),.13,"wood",True)
    m.beam((.88,1.39,0),(.88,1.12,0),.06,"steel_dark",True)
    m.beam((.87,1.12,0),(1.02,1.12,0),.07,"wood",True)
    m.beam((0,1.36,0),(0,.55,0),.02,"wood_dark")
    for side in (-1,1):
        coords=[(0,2.16),(side*.95,1.88),(side*.95,1.98),(0,2.26)]
        if sum(coords[i][0]*coords[(i+1)%4][1]-coords[(i+1)%4][0]*coords[i][1] for i in range(4))<0:coords.reverse()
        m.extrude(coords,1.48,"roof")
    return m


BUILDERS = [pylon,rural_pole,wattle,picket,concrete_fence,gate,wheat,sunflower,orchard,shelter,house,well,wheat_lod,sunflower_lod]


def validate(models):
    identities=set()
    for m in models:
        if m.id in identities:raise AssertionError("duplicate identity "+m.id)
        identities.add(m.id)
        if not 0<len(m.tris)<=m.budget:raise AssertionError(f"{m.id}: {len(m.tris)} exceeds budget {m.budget}")
        for a,b,c,n,color in m.tris:
            if not all(math.isfinite(v) for p in (a,b,c,n) for v in p):raise AssertionError(m.id+": nonfinite")
            if length(cross(sub(b,a),sub(c,a)))<1e-9:raise AssertionError(m.id+": degenerate")
            if abs(length(n)-1)>1e-5:raise AssertionError(m.id+": invalid normal")
            if color not in PALETTE:raise AssertionError(m.id+": missing palette")
        bake=m.baked();ys=bake["data"][1::6]
        if min(ys)!=0 or max(ys)!=1:raise AssertionError(m.id+": normalized height")
        if len(bake["data"])!=len(bake["colors"])*18:raise AssertionError(m.id+": stream contract")
        for offset in range(0,len(bake["data"]),18):
            values=bake["data"][offset:offset+18]
            a,b,c=tuple(values[:3]),tuple(values[6:9]),tuple(values[12:15])
            if length(cross(sub(b,a),sub(c,a)))<1e-12:raise AssertionError(m.id+": rounded triangle degenerates")
    return identities


def text_json(data):return json.dumps(data,ensure_ascii=False,indent=2)+"\n"


def meta(path, folder=False):
    identity=str(path.relative_to(ROOT)).replace("\\","/")
    value="fileFormatVersion: 2\nguid: "+uuid.uuid5(GUID_NAMESPACE,identity).hex+"\n"
    if folder:value+="folderAsset: yes\nDefaultImporter:\n  externalObjects: {}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n"
    return value


def source_obj(m):
    lines=["# Original Quiet Camp Ukrainian rural presentation asset", "# Units: metres; Y up; no external geometry or textures", "mtllib palette.mtl", "o "+m.id]
    for tri in m.tris:
        for vertex in tri[:3]:lines.append("v "+" ".join(f"{v:.7f}" for v in vertex))
    for tri in m.tris:lines.append("vn "+" ".join(f"{v:.7f}" for v in tri[3]))
    for i,tri in enumerate(m.tris):
        lines.append("usemtl "+tri[4]);start=i*3+1
        lines.append("f "+" ".join(f"{start+j}//{i+1}" for j in range(3)))
    return "\n".join(lines)+"\n"


def build_files(models):
    files={RESOURCE:text_json([m.baked() for m in models])}
    palette=[]
    for name,rgb in PALETTE.items():
        values=tuple(((rgb>>shift)&255)/255 for shift in (16,8,0))
        palette.extend(["newmtl "+name,"Kd "+" ".join(f"{v:.6f}" for v in values),"Ka 0.1 0.1 0.1","Ks 0 0 0","Ns 1","d 1","illum 1",""])
    files[SOURCE/"palette.mtl"]="\n".join(palette)
    entries=[]
    for m in models:
        obj=source_obj(m);files[SOURCE/(m.id+".obj")]=obj
        lo,hi=m.bounds();size=tuple(round(hi[i]-lo[i],6) for i in range(3))
        entry={"id":m.id,"source":m.id+".obj","units":"metres","sourceHeight":size[1],"dimensionsXYZ":size,
                        "sourceBounds":{"min":lo,"max":hi},"triangles":len(m.tris),"triangleBudget":m.budget,
                        "sourceSha256":hashlib.sha256(obj.encode()).hexdigest(),"description":m.description,
                        "geometry":"original-project-owned","externalGeometry":False,
                        "roadmapOnly":True,"materials":"shared vertex palette; runtime does not instantiate OBJ materials"}
        if m.id=="ua_power_pylon":
            source_height=hi[1]-lo[1]
            origin=((lo[0]+hi[0])/2,lo[1],(lo[2]+hi[2])/2)
            anchors=[(side*(half-.22),y-.88,0) for y,half in [(10.2,3.9),(13,3.45),(15.8,2.8)] for side in (-1,1)]
            entry["attachmentPoints"]={"conductorsMetric":anchors,"conductorsNormalized":[scale(sub(a,origin),1/source_height) for a in anchors],"crossarmAxis":"X","spanAxis":"Z","yaw":0}
        if m.id.endswith("_lod"):entry["lodOf"]=m.id[:-4]
        entries.append(entry)
    manifest={"schemaVersion":1,"generator":"tools/build_ukrainian_roadmap_models.py","palette":{n:f"#{v:06x}" for n,v in PALETTE.items()},
              "license":"Original Quiet Camp project-authored geometry; no third-party source models, images or textures. Subject to project distribution terms.",
              "totalTriangles":sum(len(m.tris) for m in models),"runtimeFormat":"Model[]; xyz + normal xyz per vertex, packed RGB per triangle; height normalized to 1; XZ bounds centred; Y base 0",
              "copyrightNotice":"Original models prepared for the Quiet Camp project (2026).","models":entries}
    files[SOURCE/"manifest.json"]=text_json(manifest)
    files[SOURCE/"README.md"]="""# Original Ukrainian rural roadmap kit

Twelve original low-poly models plus two crop LOD meshes for Quiet Camp's roadmap
presentation. The kit is
not a reconstruction of a particular village, electrical installation or event.
Its Ukrainian rural identity comes from the composition of agricultural fields,
forest belts, modest village houses, utility infrastructure, orchards and fences.
These individual motifs are also found elsewhere; none is claimed to be exclusive
to Ukraine.

All geometry is authored by the deterministic Python generator. No third-party
models, photographs, Sketchfab geometry or texture images were copied. Assets are
project-owned original geometry and follow the project's distribution terms;
this file does not grant a separate third-party asset license.

OBJ sources use metres, Y up, and a bottom-ground pivot. `palette.mtl` is an
editable source palette, not a request for independent runtime materials.
`manifest.json` gives exact dimensions, source heights, SHA-256 and triangle caps.
Runtime JSON is `Resources/QuietCamp/roadmap_culture_models.json`: the existing
triangle stream contract, with height 1 and shared per-face RGB colors.

Regenerate from the repository root:

```sh
python3 tools/build_ukrainian_roadmap_models.py
python3 tools/build_ukrainian_roadmap_models.py --check
python3 tools/build_ukrainian_roadmap_models.py --preview
```

`--check` does not write files. It rejects degenerates, invalid normals, nonfinite
coordinates, mismatched stream lengths, duplicate IDs, over-budget meshes, changed
exports and nondeterministic generation. The contact sheet is a software mesh
inspection, not a Unity import, scene integration or mobile-performance proof.

The kit changes roadmap presentation only. It must not add gameplay obstacles,
modify level content hashes, issue entitlements or expose unrevealed campaign
content. Connected power wires belong to the region layout and must be bounded by
the existing chunk system; the tower model itself has no miles-long wire bounds.
"""
    for path in list(files):files[Path(str(path)+".meta")]=meta(path)
    # Folder GUIDs predate this exporter; never replace an existing Unity folder
    # identity. The per-model generated files remain fully checked above.
    folder_meta=Path(str(SOURCE)+".meta")
    if not folder_meta.exists():files[folder_meta]=meta(SOURCE,True)
    return files


def preview(models, output=None, title=None, map_camera=False):
    import numpy as np
    from PIL import Image,ImageDraw,ImageFont
    # Actual triangle rasterisation with a depth buffer: painter sorting causes
    # false holes where roof/wall/window faces intersect in a 3D plotting tool.
    width=1800;cell_w,cell_h=600,490;top=100
    height=max(640 if output else 2600,top+math.ceil(len(models)/3)*cell_h+60)
    canvas=Image.new("RGB",(width,height),"#edead6");draw=ImageDraw.Draw(canvas)
    font_path="/usr/share/fonts/truetype/dejavu/DejaVuSans.ttf"
    title_font=ImageFont.truetype(font_path,27);label_font=ImageFont.truetype(font_path,21)
    small_font=ImageFont.truetype(font_path,17)
    draw.text((width/2,35),title or "Quiet Camp · Original Ukrainian rural roadmap kit",font=title_font,fill="#304e3c",anchor="mm")
    camera=unit((0,math.sin(math.radians(55)),-math.cos(math.radians(55)))) if map_camera else unit((.6,.45,-1))
    right=unit(cross(camera,(0,1,0)));up=unit(cross(right,camera))
    light=unit((-.45,.8,-.6))
    for i,m in enumerate(models):
        image_w,image_h=560,375
        all_vertices=[p for tri in m.tris for p in tri[:3]]
        xy=np.array([(dot(p,right),dot(p,up)) for p in all_vertices])
        low,high=xy.min(axis=0),xy.max(axis=0)
        centre=(low+high)*.5
        fit=min((image_w-30)/(high[0]-low[0]),(image_h-20)/(high[1]-low[1]))
        color_buffer=np.full((image_h,image_w,3),(237,234,214),dtype=np.uint8)
        depth=np.full((image_h,image_w),-np.inf,dtype=np.float32)
        for a,b,c,n,name in m.tris:
            if dot(n,camera)<1e-5:continue
            projected=np.array([((dot(p,right)-centre[0])*fit+image_w/2,
                                  image_h/2-(dot(p,up)-centre[1])*fit,dot(p,camera)) for p in (a,b,c)])
            minx=max(0,int(math.floor(projected[:,0].min())));maxx=min(image_w-1,int(math.ceil(projected[:,0].max())))
            miny=max(0,int(math.floor(projected[:,1].min())));maxy=min(image_h-1,int(math.ceil(projected[:,1].max())))
            if minx>maxx or miny>maxy:continue
            ax,ay,az=projected[0];bx,by,bz=projected[1];cx,cy,cz=projected[2]
            denominator=(by-cy)*(ax-cx)+(cx-bx)*(ay-cy)
            if abs(denominator)<1e-9:continue
            sx,sy=np.meshgrid(np.arange(minx,maxx+1)+.5,np.arange(miny,maxy+1)+.5)
            u=((by-cy)*(sx-cx)+(cx-bx)*(sy-cy))/denominator
            v=((cy-ay)*(sx-cx)+(ax-cx)*(sy-cy))/denominator;w=1-u-v
            z=u*az+v*bz+w*cz
            current_depth=depth[miny:maxy+1,minx:maxx+1]
            mask=(u>=-1e-6)&(v>=-1e-6)&(w>=-1e-6)&(z>current_depth)
            rgb=PALETTE[name];base=np.array([((rgb>>shift)&255)/255 for shift in (16,8,0)])
            brightness=.70+.30*max(0,dot(n,light))
            color_buffer[miny:maxy+1,minx:maxx+1][mask]=np.clip(base*brightness*255,0,255).astype(np.uint8)
            current_depth[mask]=z[mask]
        lo,hi=m.bounds();dx=hi[0]-lo[0];dy=hi[1]-lo[1];dz=hi[2]-lo[2]
        col,row=i%3,i//3;x=col*cell_w;y=top+row*cell_h
        draw.text((x+cell_w/2,y+13),m.id,font=label_font,fill="#304e3c",anchor="mm")
        draw.text((x+cell_w/2,y+43),f"{len(m.tris)} tris · {dx:.2f} × {dy:.2f} × {dz:.2f} m",font=small_font,fill="#586858",anchor="mm")
        canvas.paste(Image.fromarray(color_buffer),(x+20,y+70))
    draw.text((width/2,height-32),"Depth-buffered software mesh inspection · not a Unity/mobile acceptance render",font=small_font,fill="#586858",anchor="mm")
    target=Path(output) if output else PREVIEW
    target.parent.mkdir(parents=True,exist_ok=True);canvas.save(target)
    print("Preview:",target.relative_to(ROOT))


def main():
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--check",action="store_true",help="validate without writes")
    parser.add_argument("--preview",action="store_true",help="render software inspection sheet")
    args=parser.parse_args()
    models=[builder() for builder in BUILDERS];validate(models)
    files=build_files(models)
    # Re-create independently to detect accidental mutable RNG/global state.
    again=[builder() for builder in BUILDERS];validate(again)
    if files!=build_files(again):raise AssertionError("nondeterministic generation")
    if args.check:
        for path,content in files.items():
            if not path.exists() or path.read_text()!=content:raise AssertionError("stale or missing generated file: "+str(path.relative_to(ROOT)))
    else:
        for path,content in files.items():
            path.parent.mkdir(parents=True,exist_ok=True);path.write_text(content)
    for m in models:
        lo,hi=m.bounds();size=[round(hi[i]-lo[i],3) for i in range(3)]
        print(f"{m.id}: {len(m.tris)}/{m.budget} triangles, XYZ metres {size}")
    print(f"PASS: {len(models)} original meshes; {sum(len(m.tris) for m in models)} triangles; deterministic streams, finite positions, normals, no degenerates")
    if args.preview:
        if args.check:raise ValueError("--check and --preview are mutually exclusive (preview writes an artifact)")
        preview(models)


if __name__=="__main__":main()
