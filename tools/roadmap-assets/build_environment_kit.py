#!/usr/bin/env python3
"""Original fallback/study geometry, explicitly NOT downloaded Sketchfab meshes.

Extends the project's existing primitive/palette language. Authoring output is
Editor-only until a separately verified native composition bake publishes it.
"""
from __future__ import annotations

import argparse
import hashlib
import json
import math
from pathlib import Path
import sys

ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(ROOT / "tools"))
import build_ukrainian_roadmap_models as rural

PALETTE = dict(rural.PALETTE)
PALETTE.update(earth=0x897B58, earth_cut=0x74664C, meadow=0x71804F,
               plaster_valley=0xADA995, roof_valley=0x666C61, moss=0x697C51)
class Mesh(rural.Mesh):
    palette = PALETTE

SOURCE = ROOT / "QuietCamp/Assets/QuietCamp/Authoring/Roadmap/Models/EnvironmentKit"
CATALOG = ROOT / "QuietCamp/Assets/QuietCamp/Authoring/Roadmap/Composition/assets.json"


def clone(model, identity, description, transform=lambda p: p, colors=None):
    result = Mesh(identity, model.budget, description)
    for a, b, c, _, color in model.tris:
        result.tri(transform(a), transform(b), transform(c), (colors or {}).get(color, color))
    return result


def pitched_roof(m, width, depth, eave, ridge, damaged=False):
    # Separate slabs over a hollow shell: removing a slab creates a real opening.
    for side in (-1, 1):
        for section in range(4):
            if damaged and side == -1 and section == 1:
                continue
            z = -depth / 2 + (section + .5) * depth / 4
            p = [(0, ridge), (side*width/2, eave),
                 (side*width/2, eave+.1), (0, ridge+.1)]
            if sum(p[i][0]*p[(i+1)%4][1]-p[(i+1)%4][0]*p[i][1] for i in range(4)) < 0:
                p.reverse()
            m.extrude(p, depth/4, "roof", z)
    m.beam((0, eave+.1, -depth/2), (0, ridge, -depth/2), .12, "wood_dark", True)


def building(identity, width, depth, height, description, material="plaster_worn", damaged=True):
    m = Mesh(identity, 320, description)
    t = .16
    # A hollow interior, not an opaque solid extending through the roof hole.
    m.box((0, height/2, depth/2), (width, height, t), material)
    for x in (-width/2, width/2):
        m.box((x, height/2, 0), (t, height, depth), material)
    # Front shell has an actual dark doorway gap and two plain window marks.
    for x, w in [(-width*.31, width*.38), (width*.31, width*.38)]:
        m.box((x, height/2, -depth/2), (w, height, t), material)
    m.box((0, height*.89, -depth/2), (width*.24, height*.22, t), material)
    m.box((0, .02, 0), (width, .04, depth), "wood_dark")
    for x in (-width*.30, width*.30):
        m.box((x, height*.58, -depth/2-.085), (width*.18, height*.25, .035), "window")
    pitched_roof(m, width+.3, depth+.25, height, height+width*.23, damaged)
    m.box((0, .06, -depth/2-.3), (width*.30, .12, .6), "stone")
    return m


def abandoned_house():
    m = building("ua_abandoned_house", 5.1, 6, 2.7,
                 "Whitewashed abandoned home; real missing roof slab, hollow interior, dark doorway and empty windows.")
    m.box((.95, 3.55, 1), (.45, 1, .45), "plaster_worn")
    for x in (-1.7, 1.7):
        m.box((x, .14, -3.09), (1.6, .28, .06), "blue_dark")
    return m


def forester_hut():
    return building("ua_forester_hut", 3.6, 4, 2.1,
                    "Small abandoned forester cabin for an isolated service clearing; no roadside stop.", "wood", True)


def barn():
    return building("ua_barn", 3.2, 3.4, 2,
                    "Project-authored fallback shed/barn, empty doorway and missing roof strip; NOT a Sketchfab import.", "wood_dark", True)


def coop():
    m = Mesh("ua_chicken_coop", 260, "Project-authored fallback raised coop and grounded access ramp; NOT the synistersyrup donor.")
    for x in (-.65, .65):
        for z in (-.55, .55):
            m.box((x, .37, z), (.11, .74, .11), "wood_dark")
    m.box((0, .98, 0), (1.55, .8, 1.3), "wood")
    m.box((0, .87, -.657), (.40, .35, .022), "window")
    pitched_roof(m, 1.8, 1.5, 1.4, 1.78)
    m.beam((0, .015, -1.9), (0, .71, -.62), .48, "wood_dark", True, .06)
    for i in range(4):
        z = -1.72 + i*.29
        y = .015 + (z+1.9)/1.28*.695
        m.box((0, y+.045, z), (.51, .05, .07), "wood_light")
    return m


def beehive():
    m = Mesh("ua_beehive", 96, "Original abandoned rectangular wooden hive on a grounded stand; no bees or honey harvest.")
    for x in (-.28, .28):
        m.box((x, .15, 0), (.09, .3, .62), "wood_dark")
    for i in range(3):
        m.box((0, .42+i*.22, 0), (.70, .205, .60), "blue_dark" if i == 1 else "wood")
    m.box((0, 1.02, 0), (.81, .09, .73), "roof")
    m.box((0, .36, -.31), (.34, .045, .025), "window")
    m.box((0, .32, -.38), (.38, .045, .15), "wood_light")
    return m


def well_sweep():
    m = Mesh("ua_well_sweep", 190, "Open ring well with a grounded forked post and counterweighted sweep beam; original geometry.")
    for i in range(6):
        a, b = i*math.tau/6, (i+1)*math.tau/6
        outer = [(r*math.cos(t), y, r*math.sin(t)) for r,y,t in [(.7,.1,a),(.7,.1,b),(.7,.75,b),(.7,.75,a)]]
        inner = [(.49*math.cos(t), y, .49*math.sin(t)) for y,t in [(.1,a),(.1,b),(.75,b),(.75,a)]]
        m.quad(*outer, "stone", (math.cos((a+b)/2),0,math.sin((a+b)/2)))
        m.quad(*inner, "concrete_dark", (-math.cos((a+b)/2),0,-math.sin((a+b)/2)))
        m.quad(outer[3],outer[2],inner[2],inner[3],"plaster_worn",(0,1,0))
    m.beam((1.5, 0, 0), (1.5, 2.8, 0), .18, "wood_dark", True)
    for z in (-.25,.25):
        m.beam((1.5,2.4,0),(1.5,3,z),.12,"wood_dark",True)
    m.beam((-.2,3.9,0),(2.9,2.2,0),.13,"wood",True)
    m.box((2.7,2.33,0),(.4,.4,.37),"stone")
    m.beam((-.2,3.9,0),(-.2,.6,0),.025,"wood_dark",True)
    return m


def damaged_fence():
    m = Mesh("ua_picket_fence_damaged", 180, "Boundary segment with missing boards and one leaning plank; belongs to a fully owned lot.")
    for y in (.32, .86):
        m.box((0,y,.05),(3.25,.09,.08),"wood_dark")
    for i in range(8):
        if i in (2,5):
            continue
        x = -1.43+i*.4
        m.beam((x,0,-.025),(x+(.24 if i==4 else 0),1.13,-.025),.19,"wood",True,.07)
    for x in (-1.6,1.6):
        m.box((x,.65,.02),(.13,1.3,.13),"wood_dark")
    return m


def rusty_pylon():
    return clone(rural.pylon(), "ua_power_pylon_rusted", "Rust-colored derivative of the existing original tower; conductor anchors unchanged.", colors={"steel":"rust","steel_dark":"wood_dark"})


def fallen_pylon():
    # Rigid rotation preserves a grounded, recognisable long lattice silhouette.
    angle=math.radians(86)
    def turn(p):
        x,y,z=p
        return (x*math.cos(angle)+y*math.sin(angle), -x*math.sin(angle)+y*math.cos(angle),z)
    m=clone(rural.pylon(),"ua_power_pylon_fallen","One toppled original transmission tower; no dangling/floating conductor geometry in this mesh.",turn,{"steel":"rust","steel_dark":"wood_dark"})
    low,_=m.bounds()
    return clone(m,m.id,m.description,lambda p:(p[0],p[1]-low[1],p[2]))


def mosaic_stop():
    m=clone(rural.shelter(),"ua_bus_shelter_mosaic","Original faded geometric fantasy bird/plant mural; no specific artwork, flag, emblem or readable text copied.")
    # Broad patches stay legible at map scale; no thousands of mosaic tiles.
    z=.686 # in front of both the rear wall and its existing decorative strips
    for points,color in [([(-.65,1.12),(.12,1.04),(.67,1.40)],"blue_dark"),
                         ([(-.1,1.18),(.13,1.85),(.43,1.52)],"blue"),
                         ([(.48,1.44),(.62,1.71),(.81,1.54)],"wheat"),
                         ([(-1.10,1.12),(-.76,1.82),(-.5,1.52)],"leaf_dark"),
                         ([(.62,1.10),(1.12,1.82),(.93,1.21)],"leaf")]:
        m.tri((points[0][0],points[0][1],z),(points[2][0],points[2][1],z),(points[1][0],points[1][1],z),color)
    return m


def vegetation(identity, kind, lod=False):
    m=Mesh(identity,180,"Original coarse vegetation; no foliage cards, textures, fruit or cultivated crop rows.")
    if kind=="weeds" or kind=="reeds":
        count=3 if lod else 6
        for i in range(count):
            x=(i%3-1)*.24;z=(i//3-.5)*.23;h=(.45 if kind=="weeds" else 1.2)+(i%2)*.16
            m.tri((x-.045,0,z),(x+.06,h,z+.06),(x+.045,0,z),"leaf_dark" if kind=="weeds" else "stalk")
            m.tri((x+.045,0,z),(x+.06,h,z+.06),(x-.045,0,z),"leaf" if kind=="weeds" else "wood_light")
    else:
        h=2.6 if kind=="willow" else 4.7
        m.beam((0,0,0),(0,h*.72,0),.12,"wood_dark",True)
        centers=[(0,h*.68,0)] if lod or kind=="poplar" else [(-.44,h*.61,.1),(.43,h*.66,.14),(0,h*.76,-.33)]
        for center in centers:
            m.crown(center,.66 if kind=="willow" else .65,"leaf_light" if kind=="willow" else "leaf",5 if lod else 7,h*.60 if kind=="poplar" else 1.7)
    return m


def valley_tree(kind):
    m=Mesh("ua_valley_"+kind,180,"Gameplay-scale faceted canopy, restrained common foliage palette and visible coarse timber trunk. No foliage cards or high-frequency twig noise.")
    h=5.6 if kind=="poplar" else 4.7 if kind=="willow" else 3.8
    m.beam((0,0,0),(.08,h*.58,.04),.20,"wood_dark",True)
    def canopy(x,y,z,r,height,sides=6):
        bottom=(x,y-height*.48,z);top=(x+.05,y+height*.52,z)
        low=[(x+math.cos(i*math.tau/sides)*r*.77,y-height*.2,z+math.sin(i*math.tau/sides)*r*.77) for i in range(sides)]
        high=[(x+math.cos(i*math.tau/sides)*r*.92,y+height*.22,z+math.sin(i*math.tau/sides)*r*.92) for i in range(sides)]
        for i in range(sides):
            j=(i+1)%sides
            m.tri(bottom,low[i],low[j],"leaf_dark")
            m.quad(low[i],high[i],high[j],low[j],"leaf",(math.cos((i+.5)*math.tau/sides),0,math.sin((i+.5)*math.tau/sides)))
            m.tri(top,high[j],high[i],"leaf_light")
    if kind=="poplar":canopy(.07,h*.65,0,.83,h*.69)
    else:
        for a,b in [((0,h*.39,0),(-.64,h*.64,.1)),((0,h*.45,0),(.57,h*.65,-.2))]:m.beam(a,b,.12,"wood_dark")
        canopy(0,h*.73,0,1.28 if kind=="willow" else 1.13,h*.56)
    return m


def bridge():
    m=Mesh("ua_plank_bridge",240,"Original straight dry pedestrian deck and bank supports; no water in mesh; span along Z, landings at both ends.")
    for x in (-.62,.62):
        m.box((x,.23,0),(.17,.30,4.7),"wood_dark")
    for i in range(10):
        m.box((0,.40,-2.12+i*.47),(1.55,.12,.43),"wood" if i%3 else "wood_light")
    for z in (-2.28,2.28):
        m.box((0,.13,z),(1.85,.26,.5),"stone")
    return m


def dam(lod=False):
    m=Mesh("ua_dam_breached_lod" if lod else "ua_dam_breached",900,
           "Fictional breached rural pond embankment: surviving earth shoulders, concrete spillway crest, downstream apron, jagged exposed core, gate frame and displaced slabs. Open central channel; no bridge deck. See StyleCoherence research brief.")
    # Upstream is +Z. The old crest runs across X; downstream faces slope to -Z.
    # Each bank shoulder is a trapezoidal earth section, rather than a row of piers.
    def shoulder(lo,hi,broken):
        rings=[]
        for station in range(5):
            x=lo+(hi-lo)*station/4;variation=math.sin(x*1.3)*.17
            rings.append([(x,0,-7+math.sin(x*.7)*.55),(x,0,5+math.cos(x)*.35),
                          (x,2.65+variation,1.35+math.sin(x)*.15),
                          (x,2.8+variation,-1.1+math.cos(x)*.2)])
        for r0,r1 in zip(rings,rings[1:]):
            for i in range(4):
                j=(i+1)%4
                m.quad(r0[i],r1[i],r1[j],r0[j],"meadow" if i in (1,2) else "earth",(0,1 if i in (1,2) else 0,-1 if i==3 else 1))
        for ring,normal in [(rings[0],(-1,0,0)),(rings[-1],(1,0,0))]:
            m.quad(*ring,"earth_cut",normal)
        if not lod:
            # A few large erosion cuts/debris accents carry age without texture noise.
            for x in (lo+(hi-lo)*.25,lo+(hi-lo)*.64):
                m.crown((x,.6,-5.9),.45,"earth_cut",5,.6)
    shoulder(-20,-6,1);shoulder(6,20,-1)
    # Broad surviving concrete weir faces and crest cap: mass replaces bridge bays.
    for profile in [[(-6,0),(-6,2.8),(-2.9,2.7),(-3.6,1.8),(-2.8,1.1),(-3.8,0)],
                    [(3.5,0),(2.9,.85),(4,1.7),(3.3,2.8),(6,2.8),(6,0)]]:
        if sum(profile[i][0]*profile[(i+1)%len(profile)][1]-profile[(i+1)%len(profile)][0]*profile[i][1] for i in range(len(profile)))<0:profile.reverse()
        # Concave damage is built as a fan of convex triangular prisms, with
        # no clipping of a finished mesh and no triangles spanning the breach.
        anchor=profile[0]
        for i in range(1,len(profile)-1):
            q=[anchor,profile[i],profile[i+1]]
            if abs((q[1][0]-q[0][0])*(q[2][1]-q[0][1])-(q[1][1]-q[0][1])*(q[2][0]-q[0][0]))>.001:
                m.extrude(q,1.55,"concrete_dark",0)
    # Intact spillway wing walls, a small operating frame and rust gate remnant.
    for x in (-6.15,6.15):
        m.box((x,1.8,-2.45),(.4,1.4,5.8),"concrete_dark")
        m.box((x,2.72,.15),(.5,.2,1.7),"concrete")
    for x in (-5.35,-3.65):m.beam((x,2.7,.1),(x,4,.1),.13,"rust",True)
    m.beam((-5.5,4,.1),(-3.5,4,.1),.15,"wood_dark",True)
    m.box((-4.5,2.03,.83),(1.6,1.1,.11),"steel_dark")
    # Broken downstream apron and displaced slab: the breach remains open.
    for x,z,w,d in [(-4.8,-3.6,2.7,3.3),(4.8,-3.4,2.2,2.5)]:
        m.box((x,.95,z),(w,.35,d),"concrete_dark")
    if not lod:
        for i,(x,z) in enumerate([(-3.4,-4.5),(-2.1,-5.8),(3.8,-5.2),(5.6,-6.2)]):
            m.crown((x,.9+i*.08,z),.65,"stone",5,.7)
        for x in (-2.95,3.45):
            m.beam((x,1.8,-.7),(x+(.4 if x>0 else -.3),2.4,-.9),.035,"rust",True)
    return m


def valley_house():
    m=Mesh("ua_valley_house",600,"Muted hollow Ukrainian village house with sagged missing corner roof sheets, exposed rafters, intact gable silhouette, weathered plaster and matching slate palette. Age-related collapse, no asserted shell impact.")
    w,d,h=5.4,5.8,2.55
    m.box((0,.14,0),(w+.12,.28,d+.12),"concrete_dark")
    for x in (-w/2,w/2):m.box((x,h/2,0),(.16,h,d),"plaster_valley")
    m.box((0,h/2,d/2),(w,h,.16),"plaster_valley")
    for x,ww in [(-2.07,1.26),(-.66,1.16),(1.93,1.54)]:
        m.box((x,h/2,-d/2),(ww,h,.16),"plaster_valley")
    m.box((.62,2.32,-d/2),(.96,.46,.16),"plaster_valley")
    m.box((.62,.05,-d/2-.25),(1.05,.1,.65),"stone")
    m.box((0,.32,-d/2-.085),(w,.16,.025),"concrete_dark")
    for x in (-1.7,-.32):
        m.box((x,1.52,-d/2-.1),(.79,.9,.04),"wood_dark")
        m.box((x,1.52,-d/2-.13),(.65,.76,.02),"window")
        m.box((x,1.5,-d/2-.16),(.035,.78,.03),"wood_light")
    m.box((0,.3,0),(w-.25,.07,d-.25),"wood_dark")
    for z in (-d/2,d/2):
        # Gable is wall geometry, kept behind the roof rather than a solid roof plug.
        q=[(-w/2,h),(w/2,h),(0,3.72)]
        m.extrude(q,.16,"plaster_valley",z)
    # Left-front sheets have broken, sloped edges; rafters survive below them.
    for side in (-1,1):
        for section in range(6):
            za=-3.1+section*1.04;zb=za+1.04
            outer_a=1 if side<0 and section==1 else 2.88
            outer_b=1.4 if side<0 and section==1 else 2.88
            if side<0 and section==0:outer_b=1
            if side<0 and section==2:outer_a=1.4
            def at(r,z,offset=0):return(side*r,3.82-r*.425+offset,z)
            a,b,c,e=at(0,za),at(0,zb),at(outer_b,zb),at(outer_a,za)
            m.quad(a,b,c,e,"roof_valley",(side,1,0))
            m.quad(at(outer_a,za,-.1),at(outer_b,zb,-.1),c,e,"roof",(side,0,0))
        for z in (-2.55,-1.8,-.95):
            m.beam((0,3.68,z),(side*2.68,2.54,z),.10,"wood_dark",True)
    m.box((1.3,3.51,1.2),(.42,1.25,.46),"plaster_valley")
    m.box((1.3,4.15,1.2),(.55,.12,.57),"concrete_dark")
    for x,z in [(-2.7,-3.1),(-2.3,-3.4),(-3,-2.6)]:
        m.box((x,.21,z),(.6,.14,.44),"roof_valley")
    return m


def valley_well():
    m=Mesh("ua_valley_well",180,"Simple six-sided open village well: one stone ring, two timber posts, windlass and modest slate cover; matched to house, no ornate masonry.")
    for i in range(6):
        a,b=i*math.tau/6,(i+1)*math.tau/6
        outer=[(.55*math.cos(t),y,.55*math.sin(t)) for y,t in [(0,a),(0,b),(.65,b),(.65,a)]]
        inner=[(.38*math.cos(t),y,.38*math.sin(t)) for y,t in [(0,a),(0,b),(.65,b),(.65,a)]]
        m.quad(*outer,"concrete_dark",(math.cos((a+b)/2),0,math.sin((a+b)/2)))
        m.quad(outer[3],outer[2],inner[2],inner[3],"stone",(0,1,0))
        m.quad(*inner,"wood_dark",(-math.cos((a+b)/2),0,-math.sin((a+b)/2)))
    for x in (-.72,.72):m.box((x,.7,0),(.12,1.4,.12),"wood_dark")
    m.beam((-.73,.92,0),(.73,.92,0),.16,"wood",True)
    for side in (-1,1):
        m.quad((-.9,1.6,0),(.9,1.6,0),(.9,1.29,side*.65),(-.9,1.29,side*.65),"roof_valley",(0,1,side))
    return m


def valley_boundary(gate=False):
    m=Mesh("ua_valley_gate" if gate else "ua_valley_fence",180,"Weathered grey timber boundary module. Exact 8/3m span, joined rails; single end post per module prevents doubled posts. Same section/proportions on gate.")
    span=8/3
    # Rails cover exactly the authored perimeter interval. Post belongs to left end.
    for y in (.35,.87):m.box((0,y,.055),(span,.095,.08),"wood_dark")
    m.box((-span/2+.07,.65,0),(.14,1.3,.14),"wood_dark")
    for i in range(7):
        x=-span/2+.23+i*.35;top=1.04+[.05,-.02,.08,0,-.05,.03,0][i]
        m.beam((x,.05,-.035),(x+(.035 if i%3==0 else -.015),top,-.035),.22,"wood",True,.055)
    if gate:m.beam((-1.16,.28,-.09),(1.16,.94,-.09),.08,"wood_dark",True)
    return m


def service_building():
    m=Mesh("ua_hydro_service_building",200,"Small original abandoned flat-roof civil utility building; separate dry-ground support and road approach required.")
    m.box((0,1.3,0),(3.8,2.6,3.2),"plaster_valley")
    m.box((0,2.69,0),(4.05,.18,3.4),"concrete")
    m.box((0,1.05,-1.61),(.85,2.1,.04),"window")
    for x in (-1.3,1.3):m.box((x,1.57,-1.63),(.55,.6,.025),"window")
    m.box((0,.1,-1.85),(1.4,.2,.5),"stone")
    m.box((0,.27,-1.62),(3.75,.35,.04),"concrete_dark")
    m.box((1.27,.75,-1.64),(.55,.5,.02),"concrete_dark")
    m.box((-.5,2.84,.7),(.7,.24,.6),"concrete_dark")
    m.beam((-1.27,1.24,-1.66),(-1.43,1.83,-1.66),.09,"wood_dark",True)
    return m


def mooring():
    m=Mesh("ua_mooring_post",40,"Original stranded mooring marker; only placed at the former upstream shoreline.")
    m.box((0,.5,0),(.24,1,.24),"wood_dark")
    m.box((0,.88,0),(.54,.12,.15),"rust")
    return m


def chicken():
    m=Mesh("ua_chicken",100,"Original coarse hen for local yard/gameplay detail; not scattered along roadmap paths.")
    m.crown((0,.28,0),.25,"plaster_worn",5,.33)
    m.crown((0,.45,-.23),.11,"plaster",4,.20)
    m.tri((-.055,.45,-.3),(.055,.45,-.3),(0,.43,-.43),"wheat")
    for x in (-.09,.09):m.beam((x,0,0),(x,.20,0),.026,"wood_dark",True)
    return m


def models():
    result=[f() for f in [abandoned_house,forester_hut,barn,coop,beehive,well_sweep,
            damaged_fence,valley_house,valley_well,valley_boundary,lambda:valley_boundary(True),rusty_pylon,fallen_pylon,mosaic_stop,bridge,service_building,mooring,chicken]]
    for kind,identity in [("weeds","ua_field_weeds"),("reeds","ua_reed_clump"),("willow","ua_young_willow"),("poplar","ua_poplar")]:
        result.extend([vegetation(identity,kind),vegetation(identity+"_lod",kind,True)])
    result.extend([dam(),dam(True),valley_tree("poplar"),valley_tree("willow"),valley_tree("orchard")])
    return result


def files_for(items):
    files={}
    streams=[];entries=[]
    for m in items:
        lo,hi=m.bounds();h=hi[1]-lo[1]
        obj=rural.source_obj(m).replace("Original Quiet Camp Ukrainian rural presentation asset","Original Quiet Camp environment study; NOT a downloaded Sketchfab donor")
        files[SOURCE/(m.id+".obj")]=obj
        streams.append(dict(m.baked(),sourceHeight=h))
        entries.append(dict(id=m.id,source=m.id+".obj",triangles=len(m.tris),triangleBudget=m.budget,
                            dimensionsXYZ=[hi[i]-lo[i] for i in range(3)],sourceHeight=h,
                            bounds=dict(min=lo,max=hi),sourceSha256=hashlib.sha256(obj.encode()).hexdigest(),
                            description=m.description,geometry="original-project-owned",externalGeometry=False,
                            lod=m.id+"_lod" if m.id+"_lod" in {x.id for x in items} else None))
    files[SOURCE/"models.json"]=json.dumps(streams,separators=(",",":"))+"\n"
    palette=rural.build_files([])[rural.SOURCE/"palette.mtl"]
    for name,rgb in PALETTE.items():
        if name not in rural.PALETTE:
            channels=[((rgb>>shift)&255)/255 for shift in (16,8,0)]
            palette+=f"\nnewmtl {name}\nKd {channels[0]:.6f} {channels[1]:.6f} {channels[2]:.6f}\nKa 0 0 0\nKs 0 0 0\nd 1\nillum 1\n"
    files[SOURCE/"palette.mtl"]=palette
    files[SOURCE/"manifest.json"]=rural.text_json(dict(schemaVersion=1,staging=True,published=False,
        generator="tools/roadmap-assets/build_environment_kit.py",license="original-project-owned",
        totalTriangles=sum(e["triangles"] for e in entries),models=entries))
    for p in list(files):files[Path(str(p)+".meta")]=rural.meta(p)
    files[Path(str(SOURCE)+".meta")]="\n".join(line.rstrip() for line in rural.meta(SOURCE,True).splitlines())+"\n"
    return files


def asset_entries(items):
    result=[]
    ids={m.id for m in items}
    for m in items:
        lo,hi=m.bounds();h=hi[1]-lo[1];w=hi[0]-lo[0];d=hi[2]-lo[2]
        kind="groundcover" if any(x in m.id for x in ("weeds","reed")) else "canopy" if any(x in m.id for x in ("willow","poplar","orchard")) else "boundary" if "fence" in m.id or "gate" in m.id else "solid"
        entry=dict(id=m.id,source="../Models/EnvironmentKit/"+m.id+".obj",license="original-project-owned",
                   sourceHash=hashlib.sha256(files_for([m])[SOURCE/(m.id+".obj")].encode()).hexdigest(),
                   sourceHeight=h,height=h,width=w,depth=d,radius=math.hypot(w,d)/2,
                   pivot=dict(x=(lo[0]+hi[0])/2,y=lo[1],z=(lo[2]+hi[2])/2),
                   wind=kind in ("groundcover","canopy"),placementClass=kind,
                   lod=m.id+"_lod" if m.id+"_lod" in ids else None)
        if m.id=="ua_power_pylon_rusted":
            original=next(a for a in json.loads(CATALOG.read_text()) if a["id"]=="ua_power_pylon")
            entry["conductors"]=original["conductors"];entry["supportKind"]="power"
        if m.id=="ua_abandoned_house":entry["conductors"]=[dict(x=2.3,y=2.8,z=1.8)]
        if m.id.startswith("ua_dam_breached"):entry["sockets"]={"bankA":{"x":-14,"z":0},"bankB":{"x":14,"z":0}}
        if m.id=="ua_plank_bridge":entry["sockets"]={"north":{"x":0,"z":-2.53},"south":{"x":0,"z":2.53}}
        result.append(entry)
    return result


def main():
    p=argparse.ArgumentParser(description=__doc__)
    p.add_argument("--check",action="store_true")
    p.add_argument("--register",action="store_true",help="register new authoring assets; never writes native resources")
    p.add_argument("--preview",action="store_true")
    args=p.parse_args()
    items=models();rural.validate(items);files=files_for(items)
    if files!=files_for(models()):raise AssertionError("Nondeterministic export")
    if args.check:
        for path,text in files.items():
            if not path.exists() or path.read_text()!=text:raise AssertionError("Stale output: "+str(path))
    else:
        for path,text in files.items():path.parent.mkdir(parents=True,exist_ok=True);path.write_text(text)
    if args.register:
        if args.check:raise ValueError("--check must not write")
        previous=json.loads(CATALOG.read_text());owned={m.id for m in items}
        CATALOG.write_text(rural.text_json([a for a in previous if a["id"] not in owned]+asset_entries(items)))
    if args.preview:
        if args.check:raise ValueError("--check must not write")
        rural.preview(items, output=ROOT/"Design/Roadmap/Assets/environment-kit-contact-sheet.png",
                      title="Quiet Camp · original environment studies · not Sketchfab imports", map_camera=True)
    print(f"PASS {len(items)} original study meshes / {sum(len(m.tris) for m in items)} triangles; deterministic, finite, flat-shaded, no degenerate triangles")


if __name__=="__main__":main()
