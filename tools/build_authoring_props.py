"""Reproducible QuietCamp props in metres. Does not modify campaign or roadmap.

--check checks source hashes, triangle budgets, normals and checked-in output.
Reuses only Mesh primitives/palette from the original rural model authoring tool.
Aircraft donor attribution travels with the authoring library.
"""
import argparse
import hashlib
import json
import math
from pathlib import Path

from build_ukrainian_roadmap_models import Mesh, PALETTE, meta, source_obj

ROOT = Path(__file__).resolve().parents[1]
DEST = ROOT / "QuietCamp/Assets/QuietCamp/Authoring/LevelProps"
DONOR = ROOT / "QuietCamp/Assets/QuietCamp/Authoring/Roadmap/Models/StagingLandmarks"
MODELS = []
ENTRIES = []
PALETTE["aircraft_body"] = 0x9DA696


def model(key, label, pack, description, *, large=False, part=False, water=False):
    m = Mesh("qc_" + key, 8000 if large or key == "aircraft_fore" else 2400, description)
    MODELS.append(m)
    ENTRIES.append(dict(id=m.id, label=label, category="Великі об’єкти" if large or part else "Пропси",
                        pack=pack, recommendedScale=1, placement="single" if large or part else "surface",
                        waterRelative=water, part=part, description=description))
    return m


def merge(target, source, position=(0, 0, 0), yaw=0, factor=1):
    ca, sa = math.cos(math.radians(yaw)), math.sin(math.radians(yaw))
    def point(p):
        x, y, z = (v * factor for v in p)
        return (position[0] + x * ca + z * sa, position[1] + y, position[2] - x * sa + z * ca)
    for a, b, c, _, color in source.tris:
        target.tri(point(a), point(b), point(c), color)


def ring(m, center, radius, thickness, color, axis="y", sides=12, arc=math.tau):
    # Closed six-sided cross section, including caps on open arcs.
    cx, cy, cz = center
    def p(angle, section):
        r = radius + thickness * math.cos(section)
        q = thickness * math.sin(section)
        a, b = r * math.cos(angle), r * math.sin(angle)
        return (cx+a, cy+q, cz+b) if axis == "y" else (cx+q, cy+a, cz+b) if axis == "x" else (cx+a, cy+b, cz+q)
    for i in range(sides):
        a, b = i * arc/sides, (i+1) * arc/sides
        for j in range(6):
            c, d = j*math.tau/6, (j+1)*math.tau/6
            # Parameter orientation differs for the three axis conventions.
            points = [p(a,c), p(b,c), p(b,d), p(a,d)]
            if axis == "y": points.reverse()
            m.quad(*points, color)
    if arc < math.tau-.001:
        for angle in (0, arc):
            for j in range(1,5):
                points = [p(angle,0),p(angle,j*math.tau/6),p(angle,(j+1)*math.tau/6)]
                if (angle == 0) == (axis == "y"): points.reverse()
                m.tri(*points,color)


def vessel(m, radius=.3, height=.5, color="wood", sides=10, taper=.8):
    # Real interior and thick rim; avoids black/open single-sided bucket artifacts.
    for i in range(sides):
        a,b = i*math.tau/sides,(i+1)*math.tau/sides
        def p(t,r,y): return (math.cos(t)*r,y,math.sin(t)*r)
        m.quad(p(a,radius*taper,0),p(b,radius*taper,0),p(b,radius,height),p(a,radius,height),color,
               (math.cos((a+b)/2),0,math.sin((a+b)/2)))
        m.quad(p(a,radius*.86,height),p(b,radius*.86,height),p(b,radius*taper*.86,.06),p(a,radius*taper*.86,.06),"wood_dark",
               (-math.cos((a+b)/2),0,-math.sin((a+b)/2)))
        m.quad(p(a,radius,height),p(b,radius,height),p(b,radius*.86,height),p(a,radius*.86,height),color,(0,1,0))
        m.tri((0,.06,0),p(b,radius*taper*.86,.06),p(a,radius*taper*.86,.06),"wood_dark")
        m.tri((0,0,0),p(a,radius*taper,0),p(b,radius*taper,0),color)


def wheel(m, center, radius, water=False, broken=False):
    x,y,z=center
    if water:
        for dx in (-.18,.18): ring(m,(x+dx,y,z),radius,.085,"wood_dark","x",16,5.5 if broken else math.tau)
    else: ring(m,center,radius,.045,"wood_dark","x",16)
    m.tube((x-.2,y,z),(x+.2,y,z),.13 if water else .07,"wood",8,True)
    for i in range(12 if water else 8):
        if broken and i in (1,2): continue
        a=i*math.tau/(12 if water else 8)
        end=(x,y+math.cos(a)*radius,z+math.sin(a)*radius)
        m.beam(center,end,.11 if water else .045,"wood",True)
        if water:
            # Tangential paddle planks, with visible thickness.
            tangent=(0,-math.sin(a)*.2,math.cos(a)*.2)
            m.beam(tuple(end[j]-tangent[j] for j in range(3)),tuple(end[j]+tangent[j] for j in range(3)),.48,"wood_light",True,.15)


def props():
    m=model("bench","Дерев’яна лавка","Табір","Товсті дошки, похилі ніжки та низька спинка.")
    for z in (-.15,.15): m.box((0,.47,z),(1.55,.1,.26),"wood")
    for x in (-.52,.52):
        for z in (-.19,.19): m.beam((x,.03,z*1.35),(x,.45,z),.1,"wood_dark",True)
        m.beam((x,.15,.23),(x,.95,.34),.075,"wood_dark",True)
    m.box((0,.81,.31),(1.5,.17,.07),"wood_light")
    m=model("picnic_table","Столик для пікніка","Табір","Дощатий стіл із двома лавками на спільній основі.")
    for z in (-.24,0,.24): m.box((0,.78,z),(1.65,.09,.22),"wood")
    for x in (-.53,.53):
        for s in (-1,1): m.beam((x,.02,s*.62),(x,.75,s*.2),.1,"wood_dark",True)
        m.beam((x,.43,-.75),(x,.43,.75),.08,"wood_dark",True)
    for z in (-.67,.67): m.box((0,.48,z),(1.65,.09,.25),"wood_light")
    m=model("lantern","Похідний ліхтар","Табір","Гранований ліхтар зі склом, захисними стійками та ручкою.")
    m.cylinder((0,.04,0),.16,.08,"steel_dark",8)
    m.cylinder((0,.19,0),.115,.24,"wheat_light",8)
    m.cylinder((0,.34,0),.16,.055,"steel_dark",8,.09)
    for a in range(4):
        x,z=math.cos(a*math.pi/2)*.125,math.sin(a*math.pi/2)*.125
        m.beam((x,.06,z),(x,.32,z),.026,"steel_dark",True)
    ring(m,(0,.4,0),.105,.016,"steel","z",10,math.pi)
    for s in (-1,1): m.tube((s*.1,.345,0),(s*.105,.4,0),.015,"steel_dark",6,True)
    m=model("cooking_pot","Казанок","Табір","Відкритий казанок з товстим обідком і дужкою.")
    vessel(m,.24,.25,"steel_dark",10)
    ring(m,(0,.31,0),.26,.018,"steel","z",12,math.pi)
    for s in (-1,1): m.tube((s*.23,.225,0),(s*.26,.31,0),.018,"steel_dark",6,True)
    m=model("mug","Кухоль","Табір","Простий синій кухоль з порожниною та ручкою.")
    vessel(m,.09,.19,"blue",8,.94)
    ring(m,(.11,.11,0),.06,.018,"blue_dark","z",8)
    m=model("backpack","Рюкзак","Табір","Оливковий рюкзак, накладна кишеня, ремені й верхній клапан.")
    m.box((0,.3,0),(.42,.56,.24),"leaf_dark")
    m.box((0,.17,.16),(.3,.23,.1),"leaf")
    m.box((0,.59,0),(.46,.1,.27),"wood_dark")
    for x in (-.13,.13):
        m.beam((x,.1,-.15),(x,.54,-.16),.04,"wicker",True)
        m.box((x,.28,.225),(.03,.08,.02),"steel")
    m=model("barrel","Діжка","Сільський двір","Дерев’яні клепки й темні металеві обручі.")
    m.cylinder((0,.35,0),.31,.7,"wood",12,.28)
    for y in (.12,.57): ring(m,(0,y,0),.31,.025,"steel_dark",sides=12)
    m.cylinder((0,.71,0),.278,.03,"wood_light",12)
    m=model("bucket","Відро","Сільський двір","Відкрите оцинковане відро з дужкою.")
    vessel(m,.19,.36,"steel",10,.77)
    ring(m,(0,.42,0),.2,.016,"steel_dark","z",10,math.pi)
    for s in (-1,1): m.tube((s*.18,.32,0),(s*.2,.42,0),.014,"steel_dark",6,True)
    m=model("wheelbarrow","Тачка","Сільський двір","Дерев’яна тачка з відкритим коритом, колесом та опорними ніжками.")
    m.box((0,.4,0),(.58,.08,.73),"wood")
    for x in (-.33,.33): m.box((x,.55,0),(.08,.3,.77),"wood_light")
    for z in (-.38,.38): m.box((0,.55,z),(.7,.3,.08),"wood")
    m.tube((-.13,.22,-.58),(.13,.22,-.58),.21,"wood_dark",10,True)
    for x in (-.22,.22):
        m.beam((x,.08,.31),(x,.38,.2),.07,"wood_dark",True)
        m.beam((x,.34,-.6),(x,.52,.93),.065,"wood_dark",True)
    m=model("apple_basket","Кошик яблук","Сільський двір","Плетений кошик із кількома великими читабельними яблуками.")
    vessel(m,.28,.27,"wicker",10,.82)
    for i in range(6):
        a=i*math.tau/6
        m.crown((.16*math.cos(a),.27,.16*math.sin(a)),.09,"apple",6,.17)
    m.crown((0,.34,0),.11,"apple",6,.2)
    for y in (.09,.19): ring(m,(0,y,0),.25+y*.07,.012,"wicker_dark",sides=10)
    m=model("haystack","Невеликий стіг сіна","Сільський двір","Приземкуватий стіг із широкою основою та нерівним верхом.")
    m.cylinder((0,.38,0),.64,.76,"wheat",9,.46)
    m.crown((.03,.78,0),.48,"wheat_light",9,.55)
    for i in range(9):
        a=i*math.tau/9
        m.beam((.61*math.cos(a),.06,.61*math.sin(a)),(.43*math.cos(a),.76,.43*math.sin(a)),.022,"stalk",True)
    m=model("garden_ladder","Садова драбина","Сільський двір","Складна драбина з поперечинами, що стоїть на власних опорах.")
    for x in (-.25,.25):
        for z in (-1,1): m.beam((x,.02,z*.45),(x,1.75,0),.055,"wood",True)
    for y in (.25,.55,.85,1.15,1.45): m.beam((-.25,y,-.45*(1-y/1.75)),(.25,y,-.45*(1-y/1.75)),.05,"wood_light",True)
    m=model("rowboat","Старий човен","Берег","Відкритий дерев’яний човен з товстим бортом, сидіннями та кілем.",water=True)
    outline=[(0,-1.25),(.4,-.75),(.46,.6),(.25,1.05),(-.25,1.05),(-.46,.6),(-.4,-.75)]
    for i in range(len(outline)):
        ax,az=outline[i]; bx,bz=outline[(i+1)%len(outline)]
        m.quad((ax*.65,.06,az*.85),(bx*.65,.06,bz*.85),(bx,.43,bz),(ax,.43,az),"wood",((ax+bx)/2,0,(az+bz)/2))
        m.quad((ax*.91,.43,az*.94),(bx*.91,.43,bz*.94),(bx*.65,.12,bz*.85),(ax*.65,.12,az*.85),"wood_dark",(-(ax+bx)/2,0,-(az+bz)/2))
        m.quad((ax,.43,az),(bx,.43,bz),(bx*.91,.43,bz*.94),(ax*.91,.43,az*.94),"wood_light",(0,1,0))
        m.tri((0,.12,0),(bx*.65,.12,bz*.85),(ax*.65,.12,az*.85),"wood_dark")
        m.tri((0,.06,0),(ax*.65,.06,az*.85),(bx*.65,.06,bz*.85),"wood_dark")
    for z in (-.4,.45): m.box((0,.32,z),(.73,.07,.2),"wood_light")
    m=model("oar","Весло","Берег","Широка лопать та товсте древко; лежить плазом.")
    m.beam((0,.045,-.62),(0,.045,.58),.045,"wood_light",True)
    m.box((0,.045,.68),(.2,.06,.5),"wood")
    m=model("rope_coil","Моток мотузки","Берег","Кілька виразних витків без надмірної деталізації.")
    for r in (.14,.19,.24,.29): ring(m,(0,.03,0),r,.026,"wicker",sides=14)
    m.beam((.29,.03,0),(.51,.03,.09),.043,"wicker_dark",True)
    m=model("fish_trap","Плетена пастка для риби","Берег","Відкрита плетена пастка з кільцями й поздовжніми прутами.")
    for y,r in ((.05,.24),(.28,.2),(.56,.08)):
        ring(m,(0,y,0),r,.018,"wicker_dark",sides=10)
    for i in range(10):
        a=i*math.tau/10
        m.tube((math.cos(a)*.24,.05,math.sin(a)*.24),(math.cos(a)*.08,.56,math.sin(a)*.08),.014,"wicker",5,True)
    m=model("fishing_basket","Рибальський кошик","Берег","Низький кошик із кришкою та ручкою.")
    vessel(m,.25,.27,"wicker",10,.92)
    m.cylinder((0,.29,0),.25,.04,"wicker_dark",10)
    ring(m,(0,.39,0),.12,.026,"wicker","z",10,math.pi)
    m=model("dock_section","Секція причалу","Берег","Настил на чотирьох опорах; висота задається від води.",water=True)
    for z in (-.72,-.36,0,.36,.72): m.box((0,.58,z),(1.3,.09,.33),"wood_light")
    for x in (-.48,.48):
        m.beam((x,.48,-.9),(x,.48,.9),.12,"wood_dark",True)
        for z in (-.65,.65): m.cylinder((x,.33,z),.085,.66,"wood",7)
    m=model("grain_sack","Мішок зерна","Старі споруди","Гранований полотняний мішок зі зв’язаною горловиною.")
    m.cylinder((0,.26,0),.25,.5,"wicker",8,.21)
    m.crown((0,.53,0),.21,"wood_light",8,.19)
    m.cylinder((0,.64,0),.075,.12,"wicker_dark",6)
    m=model("millstone","Жорно","Старі споруди","Кам’яне жорно з отвором і виразною товщиною.")
    # A broad rectangular toroidal cross section makes the stone read as a disc.
    for i in range(16):
        a,b=i*math.tau/16,(i+1)*math.tau/16
        def p(t,r,y): return (math.cos(t)*r,y,math.sin(t)*r)
        m.quad(p(a,.5,.02),p(b,.5,.02),p(b,.5,.19),p(a,.5,.19),"stone",(math.cos(a),0,math.sin(a)))
        m.quad(p(a,.09,.19),p(b,.09,.19),p(b,.09,.02),p(a,.09,.02),"concrete_dark",(-math.cos(a),0,-math.sin(a)))
        m.quad(p(a,.5,.19),p(b,.5,.19),p(b,.09,.19),p(a,.09,.19),"stone",(0,1,0))
        m.quad(p(a,.09,.02),p(b,.09,.02),p(b,.5,.02),p(a,.5,.02),"stone",(0,-1,0))
    m=model("wagon_wheel","Колесо воза","Старі споруди","Дерев’яний обід, вісім спиць та маточина.")
    wheel(m,(0,.5,0),.45)
    m=model("broken_beam","Пошкоджена балка","Старі споруди","Балка з нерівним, але закритим торцем.")
    m.box((0,.1,0),(1.35,.2,.18),"wood_dark")
    for z in (-.05,.04): m.beam((.63,.1,z),(.82,.06,z+.02),.065,"wood",True)
    m=model("stone_wall_fragment","Фрагмент кам’яної стіни","Старі споруди","Нерівна кладка із великих каменів, без дрібного шуму.")
    for row in range(4):
        for col in range(4-row//2):
            x=(col-1.5)*.36+(.15 if row%2 else 0)
            m.box((x,.12+row*.235,0),(.34,.24,.3 if row<2 else .25),"stone" if (row+col)%3 else "concrete_dark")
    m=model("bent_sheet","Погнутий лист металу","Старі споруди","Три складки металу з товщиною та локальним слідом іржі.")
    for i in range(3):
        x0,x1=-.45+i*.3,-.15+i*.3
        y0,y1=.04+(i%2)*.08,.04+((i+1)%2)*.08
        top=[(x0,y0,-.35),(x1,y1,-.35),(x1,y1,.35),(x0,y0,.35)]
        low=[(x,y-.025,z) for x,y,z in top]
        color="rust" if i==1 else "steel"
        m.quad(*top,color,(0,1,0));m.quad(*low,"steel_dark",(0,-1,0))
        for j in range(4):
            k=(j+1)%4
            m.quad(low[j],low[k],top[k],top[j],color,(-1,0,0) if j==3 else (1,0,0) if j==1 else (0,0,-1) if j==0 else (0,0,1))


def mill_shell(m, water=False):
    width,depth,height=(2.5,2.4,2.2) if water else (1.65,1.6,3.45)
    for x in (-width*.39,width*.39):
        for z in (-depth*.38,depth*.38):
            m.box((x,.2,z),(.48,.4,.48),"stone")
            if water: m.beam((x,-.9,z),(x,.24,z),.2,"wood_dark",True)
    m.box((0,.44,0),(width,.14,depth),"wood_dark")
    for x in (-width*.43,width*.43):
        for z in (-depth*.43,depth*.43): m.beam((x,.48,z),(x,height,z),.15,"wood_dark",True)
    # Front entrance and an actual missing side; no intersecting fake dark opening.
    for y in [i*.18+.56 for i in range(int((height-.55)/.18))]:
        m.box((0,y,-depth*.45),(width,.16,.08),"wood" if int(y*10)%3 else "wood_light")
        m.box((-width*.45,y,0),(.08,.16,depth),"wood")
        if y>1.55: m.box((0,y,depth*.45),(width,.16,.08),"wood_light")
        else:
            for x in (-width*.36,width*.36): m.box((x,y,depth*.45),(width*.25,.16,.08),"wood")
        if y<1.15: m.box((width*.45,y,-depth*.2),(.08,.16,depth*.48),"wood_dark")
    m.beam((-width*.43,.6,-depth*.43),(-width*.43,height,depth*.43),.08,"wood_dark",True)
    for x in (-width*.43,width*.43):
        m.beam((x,height,-depth*.48),(x,height+.7,0),.12,"wood_dark",True)
        m.beam((x,height+.7,0),(x,height,depth*.48),.12,"wood_dark",True)
    # Most of the rear roof remains; two front sections are absent, exposing rafters.
    for x in [i*width/7-width*.43 for i in range(7)]:
        m.beam((x,height-.02,-depth*.52),(x,height+.72,0),width/7*.93,"roof",True,.065)
        if x<width*.12:
            m.beam((x,height+.72,0),(x,height-.02,depth*.52),width/7*.93,"roof_light",True,.065)


def landmarks():
    rotor=model("windmill_rotor","Лопаті вітряка","Частини", "Три лопаті та залишок четвертої з видимою дерев’яною рамою.",part=True)
    rotor.tube((0,0,-.12),(0,0,.12),.14,"wood_dark",8,True)
    # Rotor lies in XY, facing +Z; structural frame stays visible between boards.
    for i in range(4):
        a=math.radians(53+i*90); r=1.75 if i!=2 else .67
        direction=(math.sin(a),math.cos(a),0); tangent=(math.cos(a),-math.sin(a),0)
        rotor.beam((0,0,0),tuple(v*r for v in direction),.09,"wood_dark",True)
        for j in range(4 if i!=2 else 1):
            d=.55+j*.34
            center=tuple(v*d for v in direction)
            rotor.beam(tuple(center[k]-.12*tangent[k] for k in range(3)),tuple(center[k]+.48*tangent[k] for k in range(3)),.24,"wood_light",True,.055)
    m=model("windmill_ruin","Старий вітряк","Орієнтири","Кам’яна основа, відкриті балки, пошкоджений дах і впізнавані лопаті.",large=True)
    mill_shell(m)
    merge(m,rotor,(0,3.45,1.05))
    m.beam((0,3.45,.3),(0,3.45,1.18),.16,"wood_dark",True)
    w=model("watermill_wheel","Водяне колесо","Частини","Товстий обід, спиці та лопатки; два сегменти відсутні.",part=True,water=True)
    wheel(w,(0,1.04,0),.95,True,True)
    m=model("watermill_ruin","Напівзруйнований водяний млин","Орієнтири","Відкритий береговий млин із пошкодженим колесом і залишками настилу.",large=True,water=True)
    mill_shell(m,True)
    merge(m,w,(1.58,-.43,-.1))
    m.beam((.8,.61,-.1),(1.85,.61,-.1),.14,"wood_dark",True)
    for x in (-.62,0,.62): m.box((x,.45,1.55),(.53,.09,.6),"wood_light")
    for x in (-.85,.85): m.beam((x,.01,1.6),(x,.42,1.6),.14,"wood_dark",True)
    # Donor vertices are normalized by each source section's own metric height.
    sources=json.loads((DONOR/"models.json").read_text(encoding="utf-8"))
    manifest=json.loads((DONOR/"manifest.json").read_text(encoding="utf-8"))
    parts=[]
    for suffix,label in (("fore","Передня частина літака"),("tail","Хвостова частина літака")):
        source=next(x for x in sources if x["id"]=="staging_aircraft_"+suffix)
        info=next(x for x in manifest["models"] if x["id"]==source["id"])
        m=model("aircraft_"+suffix,label,"Частини","Адаптована геометрія Poly by Google; приглушені матеріали й локальна іржа.",part=True)
        for i,rgb in enumerate(source["colors"]):
            # Preserve relative material contrast, remove pristine white/red livery.
            channels=[(rgb>>s)&255 for s in (16,8,0)]
            material="aircraft_body" if channels[2]-channels[0]>45 else "concrete" if sum(channels)>620 else "steel" if sum(channels)>320 else "steel_dark"
            points=[tuple(source["data"][i*18+j*6+k]*info["height"]*.28 for k in range(3)) for j in range(3)]
            m.tri(*points,material)
        lo,hi=m.bounds()
        aged=[]
        for a,b,c,n,color in m.tris:
            # A restrained band at the broken end and one contiguous weathered flank.
            z=(a[2]+b[2]+c[2])/3; x=(a[0]+b[0]+c[0])/3; y=(a[1]+b[1]+c[1])/3
            if color=="aircraft_body" and (z<lo[2]+.18 or (x>.5 and -.4<z<.2 and .3<y<.55)):
                color="rust"
            aged.append((a,b,c,n,color))
        m.tris=aged
        parts.append(m)
    wing=model("aircraft_wing","Відокремлене крило","Частини","Відламане крило з товстою кромкою, видимим торцем і двома ребрами.",part=True)
    # Closed prism tapered in plan; resting on its lowest edge.
    top=[(-1.1,.13,-.35),(1.4,.04,-.13),(1.38,.035,.2),(-1.1,.13,.65)]
    low=[(x,max(0,y-.06),z) for x,y,z in top]
    wing.quad(*top,"steel",(0,1,0));wing.quad(*low,"steel_dark",(0,-1,0))
    for i in range(4):
        j=(i+1)%4
        wing.quad(low[i],low[j],top[j],top[i],"rust",((top[i][0]+top[j][0])/2,0,(top[i][2]+top[j][2])/2))
    m=model("aircraft_ruin","Зарослий зруйнований літак","Орієнтири","Розділені частини цивільного літака та крило; зелень розставляється окремо.",large=True)
    merge(m,parts[0],(0,0,1.85),-5)
    merge(m,parts[1],(.45,0,-2.45),12)
    merge(m,wing,(2.1,0,.3),-32)


def packed(m):
    vertices=[];normals=[];colors=[]
    for a,b,c,n,color in m.tris:
        for p in (a,b,c):
            vertices.extend(round(v,6) or 0 for v in p)
            normals.extend(round(v,6) or 0 for v in n)
            colors.append(PALETTE[color])
    return dict(id=m.id,shading="solid",vertices=vertices,normals=normals,colors=colors)


def files():
    props();landmarks()
    data=[]
    for m,e in zip(MODELS,ENTRIES):
        lo,hi=m.bounds()
        e["boundsMin"]=list(lo);e["boundsMax"]=list(hi);e["triangles"]=len(m.tris)
        assert len(m.tris)<=m.budget,(m.id,len(m.tris),m.budget)
        for a,b,c,n,color in m.tris:
            assert all(math.isfinite(v) for p in (a,b,c,n) for v in p)
            assert abs(sum(v*v for v in n)-1)<1e-5
        data.append(packed(m))
    donor_hash=hashlib.sha256((DONOR/"models.json").read_bytes()).hexdigest()
    catalog=dict(version=1,donorGeometrySha256=donor_hash,entries=ENTRIES)
    result={DEST/"models.json":json.dumps(dict(models=data),separators=(",",":")),
            DEST/"catalog.json":json.dumps(catalog,ensure_ascii=False,indent=2)+"\n",
            DEST/"ATTRIBUTION.md":"# QuietCamp props\n\nProps, windmill, watermill and detached wing: original QuietCamp geometry.\n\nAircraft front/tail geometry adapted from **Airplane**, **Poly by Google**: https://poly.pizza/m/a3XrQkLNna9 — **CC BY 3.0**, https://creativecommons.org/licenses/by/3.0/. Original retained in StagingLandmarks/airplane-source.glb. Changes: coarse geometry, section separation, uniform metric scale, rotation, subdued colors; no livery text. Attribution must accompany redistribution.\n"}
    palette=[]
    for name,rgb in PALETTE.items():
        palette.extend(["newmtl "+name,"Kd "+" ".join(f"{((rgb>>s)&255)/255:.6f}" for s in (16,8,0)),"Ks 0 0 0","illum 1",""])
    result[DEST/"palette.mtl"]="\n".join(palette)
    for m in MODELS:
        obj=source_obj(m)
        if "aircraft" in m.id: obj=obj.replace("# Original Quiet Camp Ukrainian rural presentation asset","# QuietCamp aircraft adaptation; see ATTRIBUTION.md").replace("; no external geometry or textures","; no textures")
        result[DEST/(m.id+".obj")]=obj
    return result


def main():
    parser=argparse.ArgumentParser();parser.add_argument("--check",action="store_true");args=parser.parse_args()
    output=files()
    if args.check:
        differences=[str(p.relative_to(ROOT)) for p,t in output.items() if not p.exists() or p.read_text(encoding="utf-8")!=t]
        if differences: raise SystemExit("Outdated output: "+", ".join(differences))
    else:
        DEST.mkdir(parents=True,exist_ok=True)
        for p,t in output.items():
            p.write_text(t,encoding="utf-8",newline="\n")
            mp=Path(str(p)+".meta")
            if not mp.exists(): mp.write_text(meta(p),encoding="utf-8")
        foldermeta=Path(str(DEST)+".meta")
        if not foldermeta.exists():foldermeta.write_text(meta(DEST,True),encoding="utf-8")
    print(f"Props: 24; landmarks: 3; parts: {len(MODELS)-27}; triangles: {sum(len(m.tris) for m in MODELS)}; geometry and deterministic output valid.")


if __name__=="__main__":main()
