"""Original, metre-scale story landmarks. Shared construction parts are not whole-model variants.

The ordered specification is intentional art direction, independent of weather/seed.
Only primary objects are reserved; auxiliary objects remain reusable.
"""
import argparse
import hashlib
import json
import math
from pathlib import Path
from build_ukrainian_roadmap_models import Mesh, PALETTE, meta

ROOT = Path(__file__).resolve().parents[1]
DEST = ROOT/'QuietCamp/Assets/QuietCamp/Authoring/CampaignLandmarks'
# order | primary object | construction: structural features, not colour swaps
SPEC = '''
1|Вказівник першої галявини|existing:qc_kenney_survival_signpost
2|Спільний стіл|existing:qc_picnic_table
3|Криниця-журавель|existing:ua_well_sweep
4|Польова кухня з димарем|kitchen:chimney
5|Стара розлога яблуня|existing:ua_orchard_tree
6|Мозаїчна зупинка|existing:ua_bus_shelter_mosaic
7|Пошкоджена сільська огорожа|existing:ua_picket_fence_damaged
8|Шафа сільської пошти|cabinet:mail
9|Кам’яний колодязь із коловоротом|well:winch
10|Двобічний ліхтар роздоріжжя|lamp:double
11|Дерев’яний прес для яблук|press:fruit
12|Дім із втраченою покрівлею|existing:ua_abandoned_house
13|Шкільна парта надворі|bench:school
14|Парна хвіртка|existing:qc_kenney_nature_fence_gate
15|Арка для вишневого саду|trellis:arched
16|Зупинка з криволінійним навісом|shelter:curved
17|Кутова руїна з піччю|house:hearth
18|Стіна шкільного вікна|wall:window
19|Курник на палях|existing:ua_chicken_coop
20|Садова сушарка для фруктів|rack:fruit
21|Старий рамковий вулик|existing:ua_beehive
22|Хвіртка з перголою|gate:pergola
23|Ручна водорозбірна помпа|pump:hand
24|Сарай із новою дошкою|existing:ua_barn
25|Двосхила теплиця волонтерів|greenhouse:gable
26|Верстак із навісом|workshop:canopy
27|Берегова альтанка з живих гілок|trellis:dome
28|Іржава ґратчаста опора|existing:ua_power_pylon_rusted
29|Пасічницький павільйон|apiary:pavilion
30|Стіл для дорожніх пакунків|rack:parcels
31|Станційний годинник на опорі|clock:station
32|Довга лава з високою спинкою|bench:station
33|Багажний візок|cart:luggage
34|Колійний буфер|buffer:rail
35|Кам’яні сходи насипу|stairs:embankment
36|Підвісна кладка|bridge:suspension
37|Вежа ліхтаря перевізника|tower:beacon
38|Кладка на кам’яних устоях|bridge:masonry
39|Станційний водяний бак|tower:water
40|Старий диспетчерський кіоск|kiosk:signal
41|Польова вітряна помпа|wind:water
42|Розірвана носова секція літака|aircraft:qc_aircraft_fore
43|Пошкоджений хвіст літака|aircraft:qc_aircraft_tail
44|Відірване крило між секціями|aircraft:qc_crash_detached_wing
45|Камінь із меморіальною нішею|memorial:niche
46|Кругла лава пам’яті|bench:circle
47|Пошкоджений вітряк|existing:qc_windmill_ruin
48|Повалена сталева опора|existing:ua_power_pylon_fallen
49|Польовий навіс на триногах|shelter:tripod
50|Зерновий підйомник із ліхтарем|tower:grain
51|Старий човен на ребрах|existing:qc_rowboat
52|Вербовий поміст на відкритому дні|platform:willow
53|Сходи покинутого причалу|stairs:landing
54|Регулятор двох плес|sluice:double
55|Будівля водного поста|existing:ua_hydro_service_building
56|Пролом старої греблі|existing:ua_dam_breached
57|Кладка з трикутними фермами|bridge:truss
58|Рятувальний човен із відкритою рубкою|boat:rescue
59|Тераса саду над берегом|terrace:orchard
60|Напівзруйнований водяний млин|existing:qc_watermill_ruin
61|Цегляний павільйон зупинки|shelter:brick
62|Господарський двір із сінником|barn:hayloft
63|Накрита восьмигранна криниця|well:octagonal
64|Каркас відновлюваного будинку|house:frame
65|Кам’яна брама сусідського двору|gate:stone
66|Відкрита сцена будинку культури|stage:culture
67|Теплиця з похилим склінням|greenhouse:lean
68|Польовий ремонтний козел|workshop:gantry
69|Береговий аварійний устій|abutment:ruin
70|Будинок лісничого|existing:ua_forester_hut
71|Низькі ковані ворота|gate:iron
72|Комора для садових плодів|store:root
73|Лісове укриття з А-подібним дахом|shelter:aframe
74|Лісова сторожка з ґанком|hut:porch
75|Зимовий тамбур зупинки|shelter:vestibule
76|Відкритий дровник із розкосами|rack:woodshed
77|Дві сполучені лави біля стели|memorial:paired
78|Зрубна криниця з двома відрами|well:log
79|Станційний навіс на чавунних колонах|station:columns
80|Громадська зимова піч під дахом|kitchen:communal
81|Лісова хатина з мансардою|hut:loft
82|Причал з поворотним краном|crane:landing
83|Відкритий павільйон із лавою-дугою|shelter:fan
84|Прибережна спостережна платформа|platform:lookout
85|Джерело з довгим водопійним жолобом|well:trough
86|Підняті грядки старого саду|garden:raised
87|Відремонтований дерев’яний стенд|board:repaired
88|Низький арковий місток|bridge:arch
89|Майстерня з двостулковими дверима|workshop:doors
90|Берегова альтанка зі сходами|gazebo:shore
91|Розсадник молодих дерев|nursery:trees
92|Дім із відновленою верандою|house:veranda
93|Пасічницький віз із вуликами|apiary:wagon
94|Теплиця з центральним ліхтарем|greenhouse:lantern
95|Зупинка з дерев’яною вежею|shelter:bell
96|Кам’яна арка саду пам’яті|memorial:arch
97|Дорожня стела Гавані|waystone:haven
98|Джерело під кам’яним склепінням|spring:vault
99|Садова ротонда Гавані|gazebo:rotunda
100|Спільний дім Гавані|house:haven
'''


def box(m, x,y,z, w,h,d, color='wood'):
    m.box((x,y,z),(w,h,d),color)


def post(m,x,z,h=2.5,color='wood_dark', y=0):
    box(m,x,y+h/2,z,.16,h,.16,color)


def roof(m,w,d,h=2.5,rise=1,color='roof'):
    for side in (-1,1):
        # Thick individual slopes; beam width is the roof depth.
        m.beam((0,h+rise,0),(side*w/2,h,0),d,color,True,.12)


def frame(m,w=3.6,d=2.5,h=2.6,roof_type='gable'):
    for x in (-w/2,w/2):
        for z in (-d/2,d/2):post(m,x,z,h)
    for z in (-d/2,d/2):m.beam((-w/2,h,z),(w/2,h,z),.16,'wood',True)
    if roof_type=='gable':roof(m,w+.4,d+.5,h,.9)
    elif roof_type=='flat':box(m,0,h+.08,0,w+.4,.16,d+.4,'roof')
    elif roof_type=='lean':m.beam((-w/2,h+.7,0),(w/2,h,0),d+.4,'roof',True,.16)


def ring(m, r, y, thickness=.1, color='wood', sides=12):
    for i in range(sides):
        a,b=i*math.tau/sides,(i+1)*math.tau/sides
        m.beam((math.cos(a)*r,y,math.sin(a)*r),(math.cos(b)*r,y,math.sin(b)*r),thickness,color,True)


def seat(m,x=0,z=0,w=2.8,back=False):
    box(m,x,.49,z,w,.14,.55,'wood_light')
    for dx in (-w*.38,w*.38):box(m,x+dx,.22,z,.16,.44,.48,'wood_dark')
    if back:
        for dx in (-w*.42,w*.42):box(m,x+dx,.8,z+.23,.1,.65,.1)
        box(m,x,.96,z+.23,w,.3,.08)


def construct(m,family,form):
    if family=='bench':
        if form=='school':
            seat(m,0,-.5,2.1);box(m,0,.83,.5,2.4,.12,.75)
            for x in (-.9,.9):post(m,x,.5,.78)
        elif form=='circle':
            for i in range(8):
                a=i*math.tau/8;x,z=math.cos(a)*1.6,math.sin(a)*1.6
                m.beam((x-.45*math.sin(a),.5,z+.45*math.cos(a)),(x+.45*math.sin(a),.5,z-.45*math.cos(a)),.36,'wood_light',True,.15);post(m,x,z,.45)
        else:
            seat(m,0,0,4.6,True);box(m,-2.4,.7,0,.25,1.4,.8,'stone');box(m,2.4,.7,0,.25,1.4,.8,'stone')
    elif family=='well':
        if form=='winch':
            for i in range(10):
                a=i*math.tau/10;box(m,.85*math.cos(a),.4,.85*math.sin(a),.5,.8,.5,'stone')
            for x in (-1.1,1.1):post(m,x,0,2)
            m.tube((-1.25,1.65,0),(1.5,1.65,0),.13,'wood',8,True);m.beam((1.5,1.65,0),(1.5,1.2,0),.1,'steel',True)
        elif form=='octagonal':
            ring(m,1,.45,.5,'stone',8);frame(m,2.6,2.6,2.2,'flat');m.crown((0,2.7,0),1.65,'roof',8,.8)
        elif form=='log':
            for y in (.2,.45,.7):
                for z in (-.8,.8):box(m,0,y,z,2,.2,.2,'wood_dark')
                for x in (-.8,.8):box(m,x,y,0,.2,.2,2,'wood')
            post(m,-1,0,2.1);post(m,1,0,2.1);roof(m,2.7,2,2.1,.8)
        else:
            box(m,0,.48,0,3.8,.8,.85,'stone');box(m,0,.91,0,3.6,.06,.62,'window');post(m,-2.2,0,1.8,'concrete');m.tube((-2.2,1.6,0),(-1.5,1.6,0),.12,'steel',8,True)
    elif family in ('house','barn','hut','store','workshop','kitchen','kiosk','station'):
        if family=='kitchen':
            box(m,0,.65,0,2.2,1.3,1.2,'concrete_dark');box(m,0,1.34,0,2.4,.12,1.4,'steel_dark');box(m,-.95,2,0,.42,3.6,.48,'stone')
            for x in (-.45,.45):m.cylinder((x,1.45,0),.28,.15,'steel_dark',10)
            if form=='communal':frame(m,4.8,3.6,3.2,'lean');seat(m,2.8,0,1.6)
        elif family=='workshop' and form=='gantry':
            for x in (-1.8,1.8):
                for z in (-.9,.9):m.beam((x,.1,z),(x,2.8,0),.18,'wood_dark',True)
            box(m,0,2.85,0,4.2,.22,.22);m.tube((0,2.8,0),(0,1.2,0),.035,'steel',6,True);box(m,0,.5,0,2.4,.9,.6)
        elif family=='store':
            box(m,0,.6,0,3,1.2,2.6,'stone');roof(m,3.8,3.3,1.2,.8);box(m,0,.55,-1.35,.9,1.1,.14,'wood_dark')
        else:
            w,d,h={'house':(4.8,3.6,2.7),'barn':(4.6,3.3,3),'hut':(3.1,2.8,2.5),'workshop':(4.5,2.4,2.4),'kiosk':(2,1.6,2.5),'station':(5.8,2.8,2.9)}[family]
            frame(m,w,d,h,'flat' if family=='station' else 'lean' if form=='canopy' else 'gable')
            if form not in ('frame','canopy','columns'):
                box(m,-w/2,h*.45,0,.18,h*.9,d,'plaster_worn');box(m,w/2,h*.45,0,.18,h*.9,d,'wood' if family=='barn' else 'plaster')
                box(m,0,h*.45,d/2,w,h*.9,.18,'wood_dark')
            if form=='hearth':box(m,-1.4,1,-.7,.9,2,.8,'stone');box(m,-1.4,2.8,-.7,.35,1.6,.4,'concrete_dark')
            if form in ('porch','veranda','haven'):
                box(m,0,.15,-d/2-1,w+1,.3,2,'stone');frame_shift(m,(0,0,-d/2-1),w,1.8,h*.85,'lean')
            if form in ('loft','hayloft'):box(m,0,h*.72,0,w,.14,d);box(m,w/2+.35,1.2,0,.2,2.4,.3);ladder(m,w/2+.35,0,2.4)
            if form=='doors':
                for s in (-1,1):m.beam((s*.2,1.1,-d/2-.8),(s*1.6,1.1,-d/2),2.2,'wood',True,.12)
            if form=='haven':box(m,0,h+1.1,0,1.8,.8,1.5,'plaster');roof(m,2.2,1.9,h+1.5,.65)
    elif family in ('shelter','greenhouse','gazebo'):
        if family=='greenhouse':
            w,d,h=(4.6,3.8,2.3) if form=='gable' else (4.8,2.6,2) if form=='lean' else (4,4.6,2.5)
            for z in [(-d/2)+i*d/5 for i in range(6)]:
                post(m,-w/2,z,h);post(m,w/2,z,h)
                m.beam((-w/2,h,z),(0,h+.9,z),.09,'wood_light',True);m.beam((0,h+.9,z),(w/2,h,z),.09,'wood_light',True)
            for x in (-w/2,w/2):box(m,x,.4,0,.18,.8,d,'concrete')
            for z in (-d/2,0,d/2):box(m,0,.35,z,w-.4,.7,.6,'wood_dark')
            if form=='lean':box(m,-w/2,1.5,0,.2,3,d,'plaster_worn')
            if form=='lantern':frame_shift(m,(0,3.2,0),1.6,d*.65,.6,'gable')
        elif family=='gazebo':
            n=6 if form=='shore' else 8;r=2.2 if form=='shore' else 2.8
            ring(m,r,.15,.3,'stone',n)
            for i in range(n):a=i*math.tau/n;post(m,math.cos(a)*r,math.sin(a)*r,2.7)
            m.crown((0,3.2,0),r+.35,'roof',n,1)
            if form=='shore':stairs(m,1.8,4,0,-r-.2)
            else:ring(m,r*.8,.55,.18,'wood_light',n)
        elif form=='aframe':
            for z in (-1.7,1.7):
                for s in (-1,1):m.beam((s*2,0,z),(0,3.5,z),.24,'wood_dark',True)
            for s in (-1,1):m.beam((s*2,.15,0),(0,3.5,0),3.7,'roof',True,.14)
        elif form=='tripod':
            for x,z in ((-2,-1.6),(2,-1.6),(0,2)):m.beam((x,0,z),(0,3,0),.2,'wood_dark',True)
            for a,b in [((-2,2,-1.6),(2,2,-1.6)),((2,2,-1.6),(0,2.8,2)),((0,2.8,2),(-2,2,-1.6))]:m.beam(a,b,.18,'wood',True)
            m.extrude([(-2,2),(2,2),(0,3.1)],2.5,'roof')
        elif form=='curved':
            for x in (-2.1,2.1):post(m,x,1,2.6,'steel')
            for j in range(7):
                x=-2.5+j*.8;box(m,x,2.7+.7*(1-(x/2.6)**2),0,.86,.13,2.8,'roof_light')
            seat(m,0,.9,3.4)
        elif form=='fan':
            post(m,0,1.5,3.2)
            for i in range(7):
                a=math.pi+i*math.pi/6;end=(math.cos(a)*2.8,2.6,1.5+math.sin(a)*2.8);m.beam((0,3.2,1.5),end,.18,'roof_light',True)
            for x,z in ((-2.6,1.5),(2.6,1.5),(0,-1.2)):post(m,x,z,2.6)
            seat(m,0,1,2.8,True)
        else:
            frame(m,4.5,2.6,2.6,'lean' if form=='brick' else 'flat')
            if form=='brick':box(m,-2.25,1.2,0,.3,2.4,2.6,'stone');box(m,0,1.2,1.3,4.5,2.4,.3,'plaster_worn')
            if form=='vestibule':
                for x in (-2.25,2.25):box(m,x,1.3,0,.18,2.6,2.6,'wood')
                box(m,0,1.3,1.3,4.5,2.6,.18,'plaster');frame_shift(m,(0,0,-2),2,1.2,2.2,'gable')
            if form=='bell':frame_shift(m,(-1.7,0,.9),1,1,4.6,'gable');m.cylinder((-1.7,3.9,.9),.3,.4,'steel',8,.15)
    else:
        construct_special(m,family,form)


def frame_shift(m,offset,*args):
    s=Mesh('part',10000,'part');frame(s,*args)
    for a,b,c,n,color in s.tris:m.tri(tuple(a[i]+offset[i] for i in range(3)),tuple(b[i]+offset[i] for i in range(3)),tuple(c[i]+offset[i] for i in range(3)),color)


def ladder(m,x,z,h):
    for dx in (-.3,.3):post(m,x+dx,z,h)
    for y in [.25+i*.3 for i in range(int(h/.3))]:box(m,x,y,z,.7,.08,.1,'wood_light')


def stairs(m,w,n,x=0,z=0):
    for i in range(n):box(m,x,.12*(i+1),z+i*.38,w,.24*(i+1),.4,'stone')


def construct_special(m,family,form):
    if family=='cabinet':
        box(m,0,1.25,0,2.1,2.5,.5,'wood_dark');roof(m,2.5,.85,2.5,.35)
        for x in (-.65,0,.65):
            for y in (.5,1.2,1.9):box(m,x,y,-.29,.53,.56,.08,'wood_light');box(m,x,y+.12,-.35,.3,.035,.04,'steel_dark')
    elif family=='lamp':
        post(m,0,0,3.9,'wood_dark');box(m,0,3.6,0,2.3,.18,.18)
        for x in (-.95,.95):box(m,x,3.2,0,.42,.65,.42,'insulator');roof_lamp(m,x,3.6,0)
    elif family=='press':
        for x in (-1,1):post(m,x,0,2.6)
        box(m,0,2.5,0,2.4,.25,.35);m.cylinder((0,1,0),.7,1.2,'wood',12);m.tube((0,1.5,0),(0,2.8,0),.09,'steel',8,True);box(m,0,2.9,0,1.3,.12,.15)
    elif family in ('trellis','gate','memorial','spring','wall','board','waystone'):
        if form=='dome':
            for i in range(8):
                a=i*math.tau/8;m.beam((math.cos(a)*2,0,math.sin(a)*2),(math.cos(a)*1.2,2.6,math.sin(a)*1.2),.12,'wood_dark',True);m.beam((math.cos(a)*1.2,2.6,math.sin(a)*1.2),(0,3.2,0),.1,'wood',True)
            ring(m,1.2,2.6)
        elif form in ('window','repaired'):
            for x in (-1.5,1.5):post(m,x,0,2.8,'stone' if form=='window' else 'wood')
            for y in (.6,2.7):box(m,0,y,0,3.3,.35,.25,'plaster_worn' if form=='window' else 'wood_light')
            if form=='window':box(m,0,1.65,0,.14,1.8,.2,'wood');box(m,0,1.7,0,3,.12,.2,'wood')
            else:box(m,0,1.7,0,2.8,1.6,.12,'wood_light');m.beam((-1.3,.9,-.1),(1.2,2.5,-.1),.13,'wood_dark',True)
        elif form in ('niche','paired','haven'):
            box(m,0,.2,0,2.8,.4,1.6,'stone');m.extrude([(-.65,.4),(.65,.4),(.55,2.6),(-.2,3)],.55,'stone');box(m,0,1.6,-.31,.65,.6,.08,'steel_dark')
            if form=='niche':box(m,0,.9,-.5,.7,.12,.7,'stone');roof_lamp(m,0,1.2,-.5)
            if form=='paired':seat(m,-2,0,1.8,True);seat(m,2,0,1.8,True)
            if form=='haven':box(m,0,2.2,-.4,3.6,.65,.12,'wood_light')
        else:
            stone=form in ('stone','arch','vault');height=2.7 if form!='iron' else 1.4
            for x in (-1.7,1.7):box(m,x,height/2,0,.42,height,.55,'stone' if stone else 'wood_dark')
            if form in ('arched','arch','vault'):
                for i in range(10):
                    a,b=i*math.pi/10,(i+1)*math.pi/10
                    m.beam((math.cos(a)*1.7,height+math.sin(a)*1.1,0),(math.cos(b)*1.7,height+math.sin(b)*1.1,0),.28,'stone' if stone else 'wood',True)
            else:box(m,0,height,0,3.8,.22,.4,'stone' if stone else 'wood')
            if form=='pergola':
                for z in (-.8,.8):
                    for x in (-1.7,1.7):post(m,x,z,2.7)
                for x in (-1.7,-.8,0,.8,1.7):box(m,x,2.85,0,.12,.16,2.2)
            if form=='iron':
                for x in (-1.3,-.9,-.5,0,.5,.9,1.3):post(m,x,0,1.3,'steel_dark')
            if form=='vault':box(m,0,.4,0,2.7,.8,1.4,'concrete_dark');box(m,0,.81,-.8,2.2,.06,.8,'window')
            if form=='arch':
                for x in (-1.7,1.7):box(m,x,.25,0,.9,.5,1.2,'stone');box(m,x,1.3,.35,.65,2.6,.7,'stone')
    elif family in ('rack','garden','nursery'):
        if family=='rack':
            w,d,h=(3.2,1.6,1.8) if form=='fruit' else (3.6,2,1.2) if form=='parcels' else (4.8,2.4,2.5)
            frame(m,w,d,h,'lean' if form=='woodshed' else 'flat')
            if form=='woodshed':
                for x in (-w/2,w/2):m.beam((x,0,-d/2),(x,h,d/2),.15,'wood',True)
                for y in (.25,.6,.95):
                    for x in (-1.8,-1.1,-.4,.3,1,1.7):m.tube((x,y,-.8),(x,y,.8),.14,'wood_dark',7,True)
            else:
                for y in ([.4,.9,1.4] if form=='fruit' else [.6]):box(m,0,y,0,w,.1,d,'wood_light')
        elif family=='garden':
            for x in (-1.3,1.3):
                for z in (-1.4,1.4):box(m,x,.4,z,1.9,.8,.14)
                for dx in (-.88,.88):box(m,x+dx,.4,0,.14,.8,2.8)
        else:
            for x in (-1.5,0,1.5):
                post(m,x,0,2);m.beam((x,1,0),(x+.45,2.2,.2),.1,'wood_dark',True)
                for z in (-.8,.8):post(m,x,z,1,'wood_light')
            box(m,0,.08,0,4.8,.16,2.2,'wood_dark')
    elif family in ('apiary','cart','boat'):
        if family=='boat':
            # Open hull, closed thick ribs, shallow draft. No solid box hull.
            for s in (-1,1):
                for i in range(8):
                    a=-2.8+i*.7;b=a+.7;wa=1*(1-(abs(a)/3.3)**2);wb=1*(1-(abs(b)/3.3)**2)
                    m.beam((a,.6,s*wa),(b,.6,s*wb),.18,'blue_dark',True,.6)
            box(m,0,.12,0,4.6,.2,1.4,'wood_dark');frame_shift(m,(-.6,.1,0),1.5,1.4,1.5,'flat')
        else:
            wagon=family=='cart' or form=='wagon';box(m,0,.65,0,3.4,.2,1.8)
            if wagon:
                for x in (-1.2,1.2):
                    for z in (-1,1):m.tube((x,.45,z-.08),(x,.45,z+.08),.45,'wood_dark',12,True)
                for z in (-.65,.65):m.beam((1.5,.7,z),(3.4,.6,z),.14,'wood',True)
            if family=='apiary':
                for x in (-1,0,1):
                    box(m,x,1.2,0,.8,.9,1,'wood_light');box(m,x,1.7,0,.95,.1,1.15,'roof');box(m,x,.85,-.55,.5,.08,.2)
                if form=='pavilion':frame(m,4.3,2.7,2.5,'gable')
            else:
                for x in (-1.1,0,1):box(m,x,1.05,0,.8,.6,1.2,'wicker_dark')
    elif family in ('bridge','platform','terrace','stairs','stage','abutment'):
        w,d=(3.4,5.8) if family=='bridge' else (4.8,3.2)
        if family=='stairs':stairs(m,2.4,8 if form=='embankment' else 5);return
        if family=='abutment':
            for x,h in ((-1.4,1.5),(0,.65),(1.2,1)):box(m,x,h/2,0,1.1,h,2.2,'concrete_dark')
            return
        box(m,0,.35,0,w,.22,d,'wood_light' if family not in ('terrace','stage') else 'stone')
        for x in (-w/2+.2,w/2-.2):
            for z in (-d/2+.2,d/2-.2):post(m,x,z,.3)
        if family=='bridge':
            for x in (-w/2,w/2):
                if form=='suspension':
                    for z in (-d/2,d/2):post(m,x,z,2.5)
                    for j in range(8):
                        a=-d/2+j*d/8;b=a+d/8;ha=.9+1.6*(a/(d/2))**2;hb=.9+1.6*(b/(d/2))**2
                        m.tube((x,ha,a),(x,hb,b),.055,'steel',6,True);m.tube((x,.5,a),(x,ha,a),.025,'steel',6,True)
                elif form=='truss':
                    for z in (-d/2,0,d/2):post(m,x,z,1.6)
                    m.beam((x,1.6,-d/2),(x,1.6,d/2),.16,'wood_dark',True)
                    for z in (-d/2,0):m.beam((x,.5,z),(x,1.6,z+d/4),.16,'wood',True);m.beam((x,1.6,z+d/4),(x,.5,z+d/2),.16,'wood',True)
                elif form=='arch':
                    for j in range(8):
                        z=-d/2+j*d/8;zn=z+d/8;m.beam((x,.6+.6*(1-(z/(d/2))**2),z),(x,.6+.6*(1-(zn/(d/2))**2),zn),.24,'stone',True)
                else:
                    for z in (-d/2,d/2):box(m,x,.3,z,.6,.6,1.4,'stone')
                    m.beam((x,1.3,-d/2),(x,1.3,d/2),.14,'wood',True)
        elif family=='platform' and form=='lookout':frame(m,3.6,2.3,3.2,'flat');ladder(m,2.2,0,3.2)
        elif family=='stage':frame(m,5.5,2.7,3.1,'lean');box(m,0,1.7,1.3,5.5,2.8,.2,'plaster_worn')
        elif family=='terrace':stairs(m,2,4,3,0);box(m,0,.8,1.5,4.8,1,.3,'stone')
        else:stairs(m,1.6,3,0,-2)
    elif family in ('tower','wind','clock','crane','sluice','pump','buffer'):
        if family=='pump':
            m.cylinder((0,.6,0),.2,1.2,'steel',8);m.tube((0,1,0),(.6,1,0),.11,'steel',8,True);m.beam((0,1.5,0),(-.7,1.1,0),.1,'steel_dark',True);box(m,0,.1,0,1.1,.2,1.1,'concrete')
        elif family=='buffer':
            for x in (-1,1):m.beam((x,.1,-1.5),(x,1.1,0),.24,'steel',True);box(m,x,.1,0,.16,.16,3.6,'steel_dark')
            box(m,0,1.05,0,3,.4,.3,'wood_dark')
        elif family=='sluice':
            for x in (-1.7,0,1.7):post(m,x,0,2.3,'concrete')
            for x in (-.85,.85):box(m,x,.65,0,1.5,1.3,.15,'steel_dark');m.tube((x,1.2,0),(x,2.9,0),.06,'steel',8,True);ring_shift(m,x,2.9,0,.35)
            box(m,0,2.3,0,3.7,.25,.35,'concrete')
        elif family=='clock':
            post(m,0,0,3.5,'steel_dark');m.tube((0,3.7,-.12),(0,3.7,.12),.65,'insulator',12,True);m.beam((0,3.7,-.14),(.32,3.7,-.14),.045,'steel_dark',True);box(m,0,3.85,-.14,.04,.4,.05,'steel_dark')
        else:
            h=4.5 if form!='grain' else 5.6;w=1.9
            for x in (-w/2,w/2):
                for z in (-w/2,w/2):post(m,x,z,h)
                m.beam((x,0,-w/2),(x,h,w/2),.13,'wood',True)
            box(m,0,h,0,2.4,.2,2.4)
            if family=='crane':m.beam((0,h,0),(3,h+.5,0),.2,'steel',True);m.tube((3,h+.5,0),(3,1,0),.04,'steel_dark',6,True)
            elif family=='wind':
                for i in range(10):
                    a=i*math.tau/10;m.beam((0,h+1,0),(math.cos(a)*1.7,h+1+math.sin(a)*1.7,0),.18,'steel',True,.4)
            elif form=='water':m.cylinder((0,h+.9,0),1.3,1.6,'steel',10);ladder(m,1.4,0,h)
            elif form=='grain':box(m,0,h-.8,0,1.6,2.2,1.6,'wood_light');m.beam((.8,h-.4,0),(2,2.8,0),.4,'wood_dark',True)
            else:box(m,0,h+.5,0,.7,1,.7,'insulator');roof(m,1.3,1.3,h+1,.45)
    else:raise ValueError((family,form))


def ring_shift(m,x,y,z,r):
    for j in range(10):
        a,b=j*math.tau/10,(j+1)*math.tau/10
        m.beam((x+math.cos(a)*r,y,z+math.sin(a)*r),(x+math.cos(b)*r,y,z+math.sin(b)*r),.04,'steel',True)


def roof_lamp(m,x,y,z):box(m,x,y,z,.65,.13,.65,'steel_dark')


def fingerprint(vertices):
    # Translation, uniform size, vertex order, materials and new IDs cannot disguise a clone.
    points=list(zip(vertices[::3],vertices[1::3],vertices[2::3]));lo=[min(p[k] for p in points) for k in range(3)];hi=[max(p[k] for p in points) for k in range(3)];scale=max(hi[k]-lo[k] for k in range(3))
    # Ground-preserving quarter turns and reflections also remain the same object.
    representations=[]
    for turn in range(4):
        for mirror in (-1,1):
            transformed=[]
            for x,y,z in points:
                for _ in range(turn):x,z=-z,x
                transformed.append((mirror*x,y,z))
            low=[min(p[k] for p in transformed) for k in range(3)]
            canonical=sorted(set(tuple(round((p[k]-low[k])/scale,5) for k in range(3)) for p in transformed))
            representations.append(json.dumps(canonical,separators=(',',':')))
    return hashlib.sha256(min(representations).encode()).hexdigest()


def rigid_shape_profile(vertices):
    """Rotation/translation/scale invariant screen for baked, tilted aliases."""
    points=list(zip(vertices[::3],vertices[1::3],vertices[2::3]))
    centre=[sum(p[k] for p in points)/len(points) for k in range(3)]
    radii=[sum((p[k]-centre[k])**2 for k in range(3)) for p in points]
    radius=sum(radii)/len(radii)
    edges=[]
    for j in range(0,len(points),3):
        edges.extend(sum((points[j+x][k]-points[j+y][k])**2 for k in range(3))/radius for x,y in ((0,1),(1,2),(2,0)))
    return sorted(r/radius for r in radii),sorted(edges)


def is_rigid_clone(a,b,tolerance=2e-5):
    return len(a[0])==len(b[0]) and all(abs(x-y)<=tolerance for left,right in zip(a,b) for x,y in zip(left,right))


def build():
    DEST.mkdir(parents=True,exist_ok=True);models=[];entries=[];seen={}
    for line in SPEC.strip().splitlines():
        number,label,construction=line.split('|');order=int(number);family,form=construction.split(':');ident=form if family in ('existing','aircraft') else 'qc_campaign_landmark_'+number.zfill(3)
        entry=dict(order=order,modelId=ident,label=label,construction=construction,role='primary',placement='single',scaleMetres=1)
        if family not in ('existing','aircraft'):
            m=Mesh(ident,4500,label);construct(m,family,form)
            vertices=[round(v,5) for t in m.tris for p in t[:3] for v in p];normals=[round(v,6) for t in m.tris for _ in range(3) for v in t[3]];colors=[PALETTE[t[4]] if isinstance(t[4],str) else t[4] for t in m.tris for _ in range(3)]
            shape=fingerprint(vertices)
            if shape in seen:raise ValueError(f'Duplicate geometry {number} / {seen[shape]}')
            seen[shape]=order
            assert len(m.tris)<=m.budget
            models.append(dict(id=ident,shading='solid',vertices=vertices,normals=normals,colors=colors,authoringRecipe=json.dumps(dict(operation='external',backend='campaign-construction',label=label,category='Великі об’єкти'),ensure_ascii=False)))
            entry.update(geometryHash=shape,triangles=len(m.tris),boundsMin=m.bounds()[0],boundsMax=m.bounds()[1])
        entries.append(entry)
    assert len(entries)==100 and len({e['modelId'] for e in entries})==100
    return dict(version=1,policy='A primary model is reserved to one campaign level; cosmetic variants do not qualify.',entries=entries),dict(models=models)


def main():
    p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');a=p.parse_args();catalog,models=build()
    for name,data in [('catalog.json',catalog),('models.json',models)]:
        path=DEST/name;text=json.dumps(data,ensure_ascii=False,separators=(',',':'))+'\n'
        if a.check:assert path.read_text(encoding='utf-8')==text, f'Stale {path}'
        else:
            path.write_text(text,encoding='utf-8')
            metadata=Path(str(path)+'.meta')
            if not metadata.exists():metadata.write_text(meta(path),encoding='utf-8')
    if not a.check:
        folder_meta=Path(str(DEST)+'.meta')
        if not folder_meta.exists():folder_meta.write_text(meta(DEST,folder=True),encoding='utf-8')
    print(f'100 primary reservations; {len(models["models"])} original structural meshes; no duplicate geometry.')


if __name__=='__main__':main()
