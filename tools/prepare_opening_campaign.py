"""Prepare authored story/profile inputs; never publish campaign content."""
from pathlib import Path
import json

ROOT = Path(__file__).resolve().parents[1]
PROJECT = ROOT / 'QuietCamp'
DEST = ROOT / 'Design/LevelAuthoring/OpeningCampaign'
DEST.mkdir(parents=True, exist_ok=True)
rows = [line.split('|') for line in (ROOT/'Design/Campaign100/2026-10-11/Authoring/places.tsv').read_text(encoding='utf-8').splitlines()[:10]]
additions = [
 ('На лавці залишено кухоль і складений дощовик. Вказівник обіцяє воду далі дорогою.', 'Два намети, вибір гостя, розміщення й поворот', 'spring_sunny'),
 ('Дві різні торбини лежать біля спільного навісу: тут чекали одне одного, не перегороджуючи шлях.', 'Пересування намету, двері й доступний прохід', 'spring_morning'),
 ('Відро під цямриною й стара мотузка показують, де набирали воду. Кухоль із першої зупинки нагадує про спільну дорогу.', 'Тінь для всіх чотирьох клітин намету', 'spring_sunny'),
 ('Біля дров — казанок; віддалік під деревом — порожня лавка для того, кому потрібен спокій.', 'Тиша: понад дві клітини від багаття', 'spring_evening'),
 ('Два кошики біля садової лавки належать друзям. Вони залишили стрічку на хвіртці як знак наступним мандрівникам.', 'Друзі: шлях між дверима не довший за три кроки', 'spring_sunny'),
 ('Складений плащ і рюкзак під мозаїчним навісом показують, що зупинка тепер прихисток для пішої дороги.', 'Самостійне розміщення трьох гостей', 'spring_rain'),
 ('Біля розірваного паркана стоїть кошик із квітами; хвіртку підперли дошкою й зберегли прохід.', 'Поєднання тіні, друзів і виходу', 'spring_morning'),
 ('Поштова торба лежить на лавці, поруч порожня скринька. Вказівник веде до спільної криниці.', 'Проходи, поворот і зовнішня доступна стежка', 'spring_sunny'),
 ('Нову дошку видно на старому колодязі; два відра й тачка залишені для спільного користування.', 'Чотири гості, тиша й наскрізний маршрут', 'spring_rain'),
 ('Ліхтар і виправлений знак продовжують мотив першого вказівника. На лавці — запасний кухоль для тих, хто прийде після нас.', 'Самостійне поєднання проходів, тіні й тиші', 'spring_evening'),
]
additions = [(a,b,{'spring_rain':'spring_drizzle','spring_evening':'spring_breeze'}.get(c,c)) for a,b,c in additions]
# Existing stable models, metric normalization, deliberately different landmark groups.
anchors = [
 [('qc_kenney_survival_signpost',-6,2,20),('qc_bench',-5,5,110),('qc_mug',-4.7,5,0)],
 [('qc_picnic_table',5,6,-25),('qc_backpack',5.2,3.8,25),('qc_kenney_nature_log_large',-5,3,70)],
 [('ua_well_sweep',-6,4,-15),('qc_bucket',-5,2.7,10),('qc_rope_coil',-6.7,2.5,20)],
 [('qc_bench',-6,5,80),('qc_cooking_pot',6,4,10),('qc_kenney_nature_log_large',6,6,75)],
 [('ua_orchard_tree',-6,5,0),('ua_orchard_tree',5,6,20),('qc_bench',-5,2,110),('qc_apple_basket',-4.5,1,30)],
 [('ua_bus_shelter_mosaic',-6,4,120),('ua_village_wood_pole',-7,7,-10),('qc_backpack',-5,2.2,20)],
 [('ua_picket_fence_damaged',5,5,0),('qc_kenney_nature_fence_gate',-5,5,35),('qc_stone_wall_fragment',6,7,75),('qc_apple_basket',-5.5,3.2,20)],
 [('qc_kenney_survival_signpost',6,3,-35),('qc_bench',5,6,170),('qc_backpack',5.5,4.5,20),('ua_valley_poplar',-6,7,0)],
 [('ua_well_sweep',6,6,20),('qc_bucket',5.5,3.6,0),('qc_wheelbarrow',-6,5,35),('qc_barrel',6.8,3.8,10)],
 [('ua_bus_shelter_mosaic',6,6,-20),('qc_kenney_survival_signpost',-6,3,10),('qc_lantern',-5.5,4,0),('qc_bench',-6,6,50)],
]
profiles=[]; stories=[]
base=json.loads((PROJECT/'Assets/QuietCamp/Authoring/WorldStudies/profiles.json').read_text(encoding='utf-8-sig'))[4]
for index,(row,extra,group) in enumerate(zip(rows,additions,anchors),1):
 title,kind,before,beat=row
 finding,mechanic,mood=extra
 entry={'id':f'QC{index:03}','title':title,'arrival':beat,'objective':'Облаштуйте табір для всіх гостей: виконайте їхні побажання й залиште доступ до кожних дверей.','finding':finding,'after':'Табір приймає людей; спільні маршрути лишаються відкритими. '+('Попереду — сад і наступна частина дороги.' if index==10 else 'Наступна зупинка: '+rows[index][0]+'.'),'nextId':f'QC{index+1:03}' if index<10 else 'gen:qc_camp:1','canonicalBefore':before,'canonicalBeat':beat,'mechanic':mechanic,'weather':mood,'proposedAdditions':finding}
 stories.append(entry)
 p=json.loads(json.dumps(base));p.update(id=f'opening-{index:03}',title=title,sourceLevel=entry['id'],biome="opening/"+kind,seed=714000+index*7919,trees=48,plants=180,extent=42,cameraPitch=52,cameraYaw=225,cameraSize=13.8,riverCentre=-13 if index in (3,9) else 25,riverWidth=1.8,riverBend=.9 if index==3 else .45,relief=.35,innerForest=8,outerForest=23,treeMin=.8,treeMax=1.3,liveTerrain=True,age=.42)
 p['anchors']=[dict(model=model,label=title+' — деталь '+str(i+1),x=x,z=z,yaw=yaw,age='moss' if 'well' in model or 'wall' in model else 'dirt',ageAmount=.45) for i,(model,x,z,yaw) in enumerate(group)]
 # A shared family of trees, with variation rather than copied environments.
 if index in (2,4,8):p['treeModels']=['qc_kenney_nature_tree_oak','qc_kenney_nature_tree_thin','qc_kenney_nature_tree_pineTallA']
 p['bushModels']=['qc_kenney_nature_grass_large','qc_kenney_nature_grass_leafsLarge','qc_kenney_nature_plant_bushSmall']
 profiles.append(dict(story=entry,world=p,reviewRevision=0))
(PROJECT/'Assets/QuietCamp/Resources/QuietCamp/opening_campaign_story.json').write_text(json.dumps({'schemaVersion':1,'entries':stories},ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
(DEST/'profiles.json').write_text(json.dumps(profiles,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
lines=['# Перші десять зупинок — весняна дорога до Гавані','', '## Канон і межі доповнень','', 'Джерела: `Design/Campaign100/2026-10-11/STORY-UA.md`, `MECHANICS-UA.md`, `Authoring/places.tsv`, чинні `Levels/QC001–QC010.json`, `world_story.json`, українська локалізація та `TutorialDirector`. Мандрівники подорожують Україною після війни; гравець облаштовує табори, а Остап навчає на перших п’яти зупинках. Мета великої подорожі — Гавань. Літак належить пізнішій ділянці Sunfield: тут запозичуємо художній стандарт, а не переставляємо катастрофу на початок.', '', 'Сюжетні матеріали не встановлюють імен мешканців кожної стартової локації й не містять окремих записок для всіх десяти місць. Знахідки, повторюваний кухоль, торбини й конкретні побутові сліди нижче — **нові авторські доповнення**, не документальні факти. Архівні side journeys не переносяться в початок кампанії. Погода залишається весняною; сезонний варіант не створює новий рівень.', '', '## Таблиця рівнів','', '| ID / назва | Місце в сюжеті та історія | Мета / навчання | Ключові деталі та нова знахідка | Погода | Завершення |','|---|---|---|---|---|---|']
for s in stories:lines.append('| '+s['id']+' — '+s['title']+' | '+s['canonicalBeat']+' | '+s['mechanic']+' | '+s['canonicalBefore']+' **Доповнення:** '+s['finding']+' | '+s['weather']+' | Усі гості розміщені, `RuleEvaluator.IsSolved`; далі '+s['nextId']+' |')
lines+=['','## Спільна реалізація й приймання','', 'JSON — джерело історій і композицій; існуюча майстерня зберігає всі об’єкти як редаговані екземпляри. PWB розміщує групи характерних деталей. MicroVerse/River Modeler формують землю й воду, геометрія запікається без редакторських залежностей. Старіння й обростання використовують чинні авторські системи; погода, вітер, звук, туман, промені й бокове розмиття — спільний `SeasonAtmosphereView`.', '', 'Чинні навчальні пазли, їхні ID, witness, прохідність і правила зберігаються. Об’єкти декору не створюють прихованої тіні чи шуму. Нова поверхня лишається природною до розміщення наметів; приминання і стежки отримують тільки фактичні події від `CampGroundDetails`.', '', 'Перед публікацією: перевірити кожну композицію з ігрової камери, виконати сім реальних циклів виправлень, валідацію/solver, проходження й Next між усіма десятьма рівнями, незмінність інших дев’яноста рівнів, відтворення історичних збережень і збереження ручної чернетки. Статус та свіжі докази будуть записані у REVIEW-UA.md; ця таблиця сама по собі не є доказом приймання.']
(DEST/'README-UA.md').write_text('\n'.join(lines)+'\n',encoding='utf-8')
print('Prepared 10 story entries and composition inputs; campaign not published.')
