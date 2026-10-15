"""U05: purposeful human traces placed through the existing native PWB boundary.

Each scene is authored against its inspected U04 frame. Shared placement only
resolves metre-scale contact and clearance; it never changes puzzle rules.
"""
import argparse,hashlib,json,math
from copy import deepcopy
from story_campaign_review import ROOT,QA,projected_bounds,terrain_height

TRACES='''
1|backpack,lantern,rope_coil|Зібрані речі першої безпечної зупинки біля вказівника.
2|cooking_pot,apple_basket,lantern|Спільний перекус біля столу, кухоль стоїть на стільниці.
3|bucket,rope_coil,barrel|Підготовлені ємності для води біля журавля.
4|barrel,grain_sack,cooking_pot|Зерно й казанок біля кухні для наступних гостей.
5|apple_basket,garden_ladder,bucket|Спільний збір плодів замість покинутих речей.
6|backpack,lantern,rope_coil|Зібраний багаж під краєм сільської зупинки.
7|apple_basket,bucket,garden_ladder|Речі тих, хто доглядає квіти за поламаною огорожею.
8|backpack,rope_coil,lantern|Невеликі пакунки та спорядження листоноші.
9|bucket,barrel,rope_coil|Черга порожніх ємностей біля спільної криниці.
10|backpack,lantern,cooking_pot|Речі для вечірньої зупинки на роздоріжжі.
11|apple_basket,barrel,bucket|Плоди зібрані біля старого преса, сік чекає в діжці.
12|grain_sack,backpack,lantern|Сухий склад речей біля відкритого дому.
13|backpack,apple_basket,lantern|Невелика спільна перерва біля шкільної лавки.
14|wheelbarrow,bucket,apple_basket|Сусіди залишили тачку з боку спільної хвіртки.
15|garden_ladder,apple_basket,bucket|Драбина й кошик пояснюють роботу у вишневому саду.
16|backpack,lantern,barrel|В очікуванні під мозаїчним навісом зібрані речі.
17|broken_beam,wheelbarrow,grain_sack|Придатні балки з двору зібрані для ремонту.
18|garden_ladder,bucket,apple_basket|Під шкільним вікном відновлюють маленький сад.
19|grain_sack,bucket,barrel|Корм та вода поруч із курником.
20|apple_basket,barrel,grain_sack|Зібрані плоди біля сушильного стелажа.
21|fishing_basket,bucket,barrel|Кошик для роботи пасічника й підготовлені ємності.
22|wheelbarrow,bucket,garden_ladder|Реманент перенесли до входу в город.
23|bucket,barrel,wheelbarrow|Воду від ручного насоса розвозять сусідам.
24|broken_beam,garden_ladder,wheelbarrow|Сухі балки та драбина для ремонту сараю.
25|bucket,wheelbarrow,apple_basket|Реманент волонтерської теплиці.
26|broken_beam,wagon_wheel,rope_coil|Колесо й матеріали на ремонті біля майстерні.
27|fishing_basket,rope_coil,lantern|Тиха зупинка рибалки під вербою.
28|bent_sheet,rope_coil,barrel|Зібрані старі деталі під безпечною ділянкою лінії.
29|fishing_basket,bucket,grain_sack|Доглядачі пасіки зібрали речі біля відкритого краю.
30|backpack,grain_sack,rope_coil|Пакунки, які вже підготували до дороги.
31|backpack,lantern,grain_sack|Речі тих, хто чекає повернення біля годинника.
32|backpack,apple_basket,lantern|Складений багаж і перекус поруч зі станційною лавкою.
33|backpack,grain_sack,rope_coil|Біля візка зібрані останні дорожні речі.
34|broken_beam,bent_sheet,rope_coil|Придатні залишки старої колії складені збоку.
35|backpack,lantern,bucket|Перепочинок перед сходами на насип.
36|rope_coil,lantern,backpack|Спорядження для переходу через підвісну переправу.
37|oar,rope_coil,fishing_basket|Весло й мотузка пояснюють місце перевізника.
38|broken_beam,wheelbarrow,rope_coil|Матеріали, що залишилися після ремонту підходу.
39|bucket,barrel,rope_coil|Порожні ємності біля водонапірної башти.
40|backpack,lantern,grain_sack|Багаж останньої зупинки перед полем.
41|bucket,barrel,rope_coil|Вітряний насос обслуговує невелику польову ємність.
42|backpack,rope_coil,lantern|Речі зібрані біля відкритого розриву носової секції.
43|backpack,grain_sack,lantern|Біля хвоста зібраний запас для прихистку.
44|backpack,rope_coil,cooking_pot|Табір використовує уламки як захист і сухий склад.
45|apple_basket,lantern,backpack|Скромна зупинка пам'яті, речі не перекривають нішу.
46|backpack,lantern,apple_basket|Люди сідають разом, залишивши середину лавки вільною.
47|grain_sack,garden_ladder,bucket|Речі тих, хто повернувся доглядати поле біля млина.
48|bent_sheet,rope_coil,broken_beam|Уцілілі деталі зібрані за межами проходу.
49|backpack,lantern,rope_coil|Невеликий прихисток перед відкритим полем.
50|grain_sack,lantern,barrel|Зерно збережене біля старої польової башти.
51|oar,rope_coil,fishing_basket|Старий човен залишився з веслом на колишньому березі.
52|fishing_basket,rope_coil,bucket|Речі біля настилу пояснюють давній рівень води.
53|oar,rope_coil,lantern|Сходи ведуть до спорядження на нижньому березі.
54|bucket,rope_coil,bent_sheet|Зібрані деталі біля двох шлюзових затворів.
55|bent_sheet,barrel,rope_coil|Невеликий запас ремонтних матеріалів біля служби греблі.
56|broken_beam,bent_sheet,rope_coil|Важкі рештки відсунуті від доступного берега.
57|broken_beam,rope_coil,wheelbarrow|Матеріали після влаштування нової кладки.
58|rope_coil,oar,lantern|Рятувальне спорядження зібране на березі біля човна.
59|wheelbarrow,bucket,apple_basket|Садівники працюють на колишній береговій терасі.
60|grain_sack,millstone,barrel|Старі жорна та мішки пояснюють роботу водяного млина.
61|backpack,lantern,bucket|На сухому краї навісу чекають дорожні речі.
62|haystack,grain_sack,wheelbarrow|Осінній запас складений поруч із входом на сінник.
63|bucket,barrel,rope_coil|Воду ще набирають після листопаду.
64|broken_beam,garden_ladder,wheelbarrow|Дошки та драбина для будинку, який лагодять разом.
65|wheelbarrow,apple_basket,bucket|Спільний двір за відчиненою сусідською брамою.
66|backpack,lantern,barrel|Речі невеликої спільної зустрічі біля старої сцени.
67|bucket,wheelbarrow,grain_sack|Реманент уже зібраний перед холодом.
68|wagon_wheel,broken_beam,rope_coil|Колесо й балки підготували до підйому на ремонт.
69|broken_beam,bent_sheet,rope_coil|Старі уламки та нове спорядження не заважають переправі.
70|grain_sack,lantern,barrel|Запас для тих, хто знайде лісничий будинок пізніше.
71|backpack,lantern,rope_coil|Речі чекають за огорожею перед холодним переходом.
72|grain_sack,barrel,bucket|Перший зимовий запас біля входу до льоху.
73|backpack,lantern,grain_sack|Сухі речі на захищеному краї хвойного прихистку.
74|grain_sack,lantern,barrel|Запас у сухому куті перед ґанком.
75|backpack,lantern,rope_coil|Багаж тих, хто зайшов у зимову зупинку.
76|broken_beam,rope_coil,lantern|Перев'язані сухі матеріали залишені наступним гостям.
77|backpack,lantern,grain_sack|Речі друзів зібрані разом біля тихого місця.
78|bucket,rope_coil,barrel|Підготовлені ємності біля засніженої криниці.
79|backpack,lantern,grain_sack|Дорожній запас на відкритому краї станційного навісу.
80|cooking_pot,grain_sack,barrel|Запас і спільний казанок для зимового табору.
81|bucket,barrel,lantern|Речі знову винесли на сухий край під час відлиги.
82|rope_coil,oar,bucket|На причалі готують спорядження до повернення води.
83|bucket,apple_basket,garden_ladder|Перший реманент уже чекає під навісом.
84|fishing_basket,rope_coil,lantern|Речі для спостереження за холодним плесом.
85|bucket,barrel,rope_coil|Відкритий доступ до корита в довший день.
86|bucket,apple_basket,garden_ladder|Зібрані речі для перших посадок у старі грядки.
87|broken_beam,rope_coil,lantern|Матеріали після ремонту вказівника складені поряд.
88|rope_coil,bucket,lantern|Спорядження після огляду переправи по льоду.
89|wagon_wheel,broken_beam,garden_ladder|Ремонт знову почався біля відкритих дверей майстерні.
90|fishing_basket,lantern,apple_basket|Спільна перерва біля світлого весняного берега.
91|wheelbarrow,bucket,garden_ladder|Інструменти для нового ряду молодих дерев.
92|apple_basket,lantern,barrel|Зібрані речі людей, які повернулися додому.
93|fishing_basket,bucket,barrel|Робочі речі біля пересувної пасіки.
94|bucket,wheelbarrow,garden_ladder|Реманент повернувся до теплиці нового сезону.
95|backpack,lantern,rope_coil|Зібраний багаж для тих, хто вирушає далі.
96|apple_basket,lantern,backpack|Спокійна квіткова зупинка біля арки пам'яті.
97|backpack,lantern,bucket|Дорожні речі біля останнього знака до Гавані.
98|bucket,barrel,rope_coil|Посудини біля доступного входу до джерела.
99|apple_basket,bucket,garden_ladder|Спільна робота й перепочинок у саду Гавані.
100|wheelbarrow,apple_basket,barrel|На місці довгої зупинки вже дбають про наступний врожай.
'''

def bounds(pose,model):
    # World-axis bounds reuse the exact serialized pose semantics.
    lo,hi=projected_bounds(pose,model,dict(cameraEuler=[0,0,0]),exact=True)
    return lo[0],lo[2],hi[0],hi[2]

def intersects(a,b,gap=.12):return a[0]<b[2]+gap and a[2]>b[0]-gap and a[1]<b[3]+gap and a[3]>b[1]-gap

def main():
    p=argparse.ArgumentParser();p.add_argument('--start',type=int,default=1);p.add_argument('--end',type=int,default=100);a=p.parse_args()
    stage=QA/'UserSettings/QuietCampCampaignReview';review=ROOT/'Design/LevelAuthoring/StoryCampaign/Review'
    profiles=json.loads((ROOT/'Design/LevelAuthoring/StoryCampaign/unique-profiles.json').read_text(encoding='utf-8'))
    inspected={r['order']:r for r in json.loads((review/'U04/inspection.json').read_text(encoding='utf-8'))['levels']}
    library=json.loads((ROOT/'QuietCamp/Assets/QuietCamp/Authoring/LevelProps/models.json').read_text(encoding='utf-8'))
    props={m['id']:m for m in library['models']}
    labels={m['id']:m['label'] for m in json.loads((ROOT/'QuietCamp/Assets/QuietCamp/Authoring/LevelProps/catalog.json').read_text(encoding='utf-8'))['entries']}
    traces={int(line.split('|')[0]):line.split('|')[1:] for line in TRACES.strip().splitlines()}
    target=stage/'U05';target.mkdir(parents=True,exist_ok=True);out=review/'U05';out.mkdir(parents=True,exist_ok=True)
    record=out/'corrections.json';records=json.loads(record.read_text(encoding='utf-8')) if record.exists() else []
    for n in range(a.start,a.end+1):
        stem=f'L{n:03}';destination=target/(stem+'.draft.json')
        if destination.exists():raise RuntimeError('Refusing to overwrite '+stem)
        row=inspected[n];assert row['inspected'] and row['imageSha256']==hashlib.sha256((review/'U04'/(stem+'.png')).read_bytes()).hexdigest()
        source=stage/'U04'/(stem+'.draft.json');d=json.loads(source.read_text(encoding='utf-8'));art=d['art'];models={m['id']:m for m in art['models']}
        hero=next(i for i in art['instances'] if i['id']==art['primaryLandmarkInstanceId']);removed=set()
        reposition=[]
        if n==47:
            hero['yaw']=45
            if hero.get('rotationEuler'):hero['rotationEuler'][1]=45
            reposition.append(hero)
        if n==44:
            # The U04 overview reveals the wing resting over the old drying
            # place. Keep the wing/support fixed and move the human belongings.
            locations={'crash/drying':(-5.9,-4.8),'crash/seat-a':(5.2,-2.9),'crash/seat-b':(6.4,-3.8),'crash/boots':(-4.8,-4.3),'crash/coat':(-6.4,-4.4)}
            for pose in art['instances']:
                if pose['id'] not in locations:continue
                x,z=locations[pose['id']];offset=pose['y']-terrain_height(art,pose['x'],pose['z'])
                pose.update(x=x,z=z,y=terrain_height(art,x,z)+offset,surfaceOffset=offset)
                reposition.append(pose)
                if pose['id']=='crash/drying':
                    for cloth in art['instances']:
                        if cloth['id'] in ('crash/drying/cloth0','crash/drying/cloth1'):cloth.update(x=x,z=z,y=pose['y'])
        if n not in (15,34,51,76):
            for pose in art['instances']:
                m=models[pose['modelId']]
                if pose['id']!=hero['id'] and (m.get('sourceModelId') or m['id']) in ('qc_kenney_nature_log','qc_kenney_nature_log_large'):removed.add(pose['id'])
        art['instances']=[i for i in art['instances'] if i['id'] not in removed and not any(i['id'].startswith('overgrowth/'+owner+'/') for owner in removed)]
        occupied=[bounds(i,models[i['modelId']]) for i in art['instances'] if models[i['modelId']].get('shading')=='solid']
        board=(-d['level']['width']/2-.4,-d['level']['height']/2-.4,d['level']['width']/2+.4,d['level']['height']/2+.4)
        anchors=[]
        for pose in reposition:
            model=models[pose['modelId']]
            anchors.append(dict(instanceId=pose['id'],model=model.get('sourceModelId') or model['id'],label=pose['id'],x=pose['x'],z=pose['z'],y=pose.get('surfaceOffset',0),yaw=pose['yaw'],scale=pose['scale'],age='dirt',ageAmount=0,preserveGeometry=True))
        trace=traces[n];camera=art['cameraEuler'][1];yaw=math.radians(camera);front=(-math.sin(yaw),-math.cos(yaw));right=(math.cos(yaw),-math.sin(yaw))
        hero_box=bounds(hero,models[hero['modelId']]);radius=max(hero_box[2]-hero_box[0],hero_box[3]-hero_box[1])*.5
        # Prefer the camp-facing edge of the structure; keep related objects in
        # one deliberately uneven cluster, with a walkable gap between them.
        centre=(hero['x']+front[0]*(radius+.7),hero['z']+front[1]*(radius+.7))
        chosen=trace[0].split(',')
        def footprint(short):
            v=props['qc_'+short]['vertices'];return (max(v[::3])-min(v[::3]))*(max(v[2::3])-min(v[2::3]))
        chosen.sort(key=footprint,reverse=True)
        for j,short in enumerate(chosen):
            ident='qc_'+short;model=props[ident];angle=camera+[-13,18,-7][j]
            candidates=[]
            for ring in range(20):
                for direction in range(24):
                    theta=direction*math.pi/12;distance=.4+ring*.4
                    x=centre[0]+math.cos(theta)*distance
                    z=centre[1]+math.sin(theta)*distance
                    height=terrain_height(art,x,z)
                    pose=dict(x=x,y=height,z=z,scale=1,yaw=angle)
                    box=bounds(pose,model)
                    if height<.025 or intersects(box,board) or any(intersects(box,b) for b in occupied):continue
                    lo,hi=projected_bounds(pose,model,art,True);cp=projected_bounds(dict(x=art['cameraTarget'][0],y=art['cameraTarget'][1],z=art['cameraTarget'][2],scale=1),dict(vertices=[0,0,0]),art)[0]
                    if any(abs(q[k]-cp[k])>art['orthographicSize']*.92 for q in (lo,hi) for k in (0,1)):continue
                    human=[anchor for anchor in anchors if '/human-trace-' in anchor['instanceId']]
                    if human and math.hypot(x-human[0]['x'],z-human[0]['z'])>4.8:continue
                    candidates.append((ring+direction*.002,x,z,box))
            if not candidates:raise RuntimeError('No supported visible place for '+stem+'/'+short)
            _,x,z,box=min(candidates);occupied.append(box)
            offset=-min(model['vertices'][1::3])+.008
            anchors.append(dict(instanceId=f'opening/{d["sourceLevelId"]}/human-trace-{j}',model=ident,label=labels[ident],x=x,z=z,y=offset,yaw=angle,scale=1,age='dirt',ageAmount=.18,weathering=dict(material='wood' if short not in ('bucket','lantern','cooking_pot','bent_sheet') else 'steel',patchSize=.35,distribution='lower')))
        profile=deepcopy(profiles[n-1]);profile['world']['anchors']=anchors;profile['world']['refineExistingPlants']=False
        destination.write_text(json.dumps(d,ensure_ascii=False,separators=(',',':'))+'\n',encoding='utf-8')
        (target/(stem+'.details.json')).write_text(json.dumps(profile,ensure_ascii=False),encoding='utf-8')
        records.append(dict(order=n,baseline='U04',correction='U05',issues=row['issues'],change=dict(humanTrace=trace[1],props=anchors,repeatedLogsRemoved=len(removed)),sourceSha256=hashlib.sha256(source.read_bytes()).hexdigest(),residual='Native placement, capture and contact inspection required; proposed anonymous human traces do not change canonical story facts.'))
        record.write_text(json.dumps(sorted(records,key=lambda r:r['order']),ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
    record.write_text(json.dumps(sorted(records,key=lambda r:r['order']),ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
    print('Individual human-trace corrections staged:',len(records))

if __name__=='__main__':main()
