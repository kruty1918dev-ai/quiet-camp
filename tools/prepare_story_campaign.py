"""Canonical 100-place narrative/composition inputs, preserving the reviewed opening."""
import json,math
from pathlib import Path
ROOT=Path(__file__).resolve().parents[1];PROJECT=ROOT/'QuietCamp';CONTENT=PROJECT/'Assets/QuietCamp/Resources/QuietCamp';DEST=ROOT/'Design/LevelAuthoring/StoryCampaign';DEST.mkdir(parents=True,exist_ok=True)
rows=[r.split('|') for r in (ROOT/'Design/Campaign100/2026-10-11/Authoring/places.tsv').read_text(encoding='utf-8').splitlines()]
ids=json.loads((CONTENT/'campaign.json').read_text(encoding='utf-8-sig'))['campaignLevelIds'];stories=json.loads((CONTENT/'opening_campaign_story.json').read_text(encoding='utf-8'))['entries'][:10]
base=json.loads((PROJECT/'Assets/QuietCamp/Authoring/WorldStudies/profiles.json').read_text(encoding='utf-8-sig'))[4]
aliases={'camp_bench':'qc_bench','camp_lantern':'qc_lantern','camp_bag':'qc_backpack','camp_crate':'qc_barrel','log':'qc_kenney_nature_log','log_stack':'qc_kenney_nature_log','sign':'qc_kenney_survival_signpost','flower_yellowA':'qc_kenney_nature_flower_yellowB','story_workboat':'qc_rowboat','ua_valley_orchard':'ua_orchard_tree','ua_valley_fence_broken_0':'ua_picket_fence_damaged','ua_valley_fence_broken_1':'ua_picket_fence_damaged','ua_valley_fence_broken_2':'qc_stone_wall_fragment','ua_valley_gate':'qc_kenney_nature_fence_gate','story_foundation':'qc_stone_wall_fragment'}
moods={s:[p['id'] for p in json.loads((CONTENT/'season_atmosphere.json').read_text(encoding='utf-8'))['presets'] if p['season']==s] for s in ['spring','summer','autumn','winter']}
water_kinds={'willow','crossing','mooring','water','service','dam','stranded'};large_kinds={'house','barn','greenhouse','station','hut','haven'}
roles={'orchard':'садівники','apiary':'пасічники','station':'люди, що повертаються додому','crossing':'мешканці двох берегів','mooring':'перевізники','greenhouse':'волонтери','barn':'майстри','house':'колишні мешканці двору','hut':'лісничий','field':'люди, що доглядають поле','memorial':'ті, хто бережуть пам’ять'}
profiles=[]
for order in range(11,101):
 ident=ids[order-1];title,kind,before,beat=rows[order-1];level=json.loads((CONTENT/'GeneratedLevels'/(ident.replace(':','_')+'.json')).read_text(encoding='utf-8-sig'))
 season=level['environment']['seasonId'];mood=moods[season][(order-1)%5]
 person=roles.get(kind,'мандрівники й сусіди')
 mechanism=[]
 if any(g['shade'] for g in level['guests']):mechanism.append('тінь')
 if any(g['quiet'] for g in level['guests']):mechanism.append('тиша')
 if level['friends']:mechanism.append('близькість друзів')
 if level.get('accessPoints') or level.get('exteriorWalkable'):mechanism.append('входи, виходи й зовнішні проходи')
 mechanism=', '.join(mechanism) or 'розміщення й доступ до дверей'
 finding=before+' Тут могли зупинятися '+person+'. Залишені речі та вільний прохід допомагають продовжити дорогу.'
 entry=dict(id=ident,title=title,arrival=beat,objective=f'Прийміть {len(level["guests"])} гостей: {mechanism}. Збережіть доступ до кожного намету.',finding=finding,after=('Гавань приймає нас. Тепер тут можна залишитися й дбати про тих, хто прийде наступним.' if order==100 else 'Доглянута зупинка лишається іншим. Далі — '+rows[order][0]+'.'),nextId=ids[order] if order<100 else None,canonicalBefore=before,canonicalBeat=beat,mechanic=mechanism,weather=mood,proposedAdditions='Можливі користувачі місця: '+person+'; побутові речі й конкретний спосіб користування — художнє доповнення.')
 stories.append(entry)
 p=json.loads(json.dumps(base));p.update(id=f'place-{order:03}',title=title,sourceLevel=ident,biome='campaign/'+kind,seed=814000+order*7919,trees=44 if kind not in large_kinds else 34,plants=170,extent=42,cameraPitch=50+(order%3),cameraYaw=215+(order%5)*5,cameraSize=10 if kind in large_kinds else 9,riverCentre=-10 if kind in water_kinds else 27,riverWidth=2.6 if kind in water_kinds else 1.8,riverBend=.45+(order%4)*.25,relief=.28+(order%3)*.12,innerForest=8.5,outerForest=23,treeMin=.8,treeMax=1.3,liveTerrain=True,age=.52)
 if season=='winter':p['treeModels']=['qc_kenney_nature_tree_pineTallA','qc_kenney_nature_tree_pineRoundA','qc_kenney_nature_tree_oak']
 elif kind in ['orchard','apiary','field','yard','haven']:p['treeModels']=['ua_orchard_tree','qc_kenney_nature_tree_oak','qc_kenney_nature_tree_thin']
 else:p['treeModels']=['qc_kenney_nature_tree_oak','qc_kenney_nature_tree_thin','qc_kenney_nature_tree_pineTallA']
 p['bushModels']=['qc_kenney_nature_grass_large','qc_kenney_nature_grass_leafsLarge','qc_kenney_nature_plant_bushSmall']
 anchors=[]
 props=level['environmentalStory']['beats'][0]['props']
 for prop in props:
  view=prop.get('level')
  if not view:continue
  model=aliases.get(view['assetId'],view['assetId']);water=view.get('spansWater',False)
  anchors.append(dict(model=model,label=title+' — '+prop['id'],x=view['x'],z=view['z'],yaw=view['yaw'],scale=1,metric=1,y=.02 if water else 0,water=water,age='moss' if any(s in model for s in ['well','stone','house']) else 'dirt',ageAmount=.5,sourceHeight=view['height']))
 if kind in water_kinds:
  # One coherent bank; footbridges cross the actual generated channel.
  for anchor in anchors:
   if 'bridge' in anchor['model']:
    anchor.update(x=-10,z=1.5,yaw=0,water=True,y=.04)
  p['riverCentre']=-10
 if kind=='haven':
  anchors += [dict(model='ua_well_sweep',label='Спільна вода Гавані',x=-7,z=6,yaw=15,age='moss'),dict(model='qc_picnic_table',label='Спільний стіл Гавані',x=6,z=7,yaw=50,age='dirt')]
 if kind.startswith('aircraft'):
  # The accepted crash geometry is reused, with distinct fore/tail/pair layouts.
  anchors=[];p['cameraSize']=18.7;p['extent']=25;p['riverCentre']=27
 p['anchors']=anchors
 profiles.append(dict(story=entry,world=p,kind=kind,reviewRevision=0))
(CONTENT/'opening_campaign_story.json').write_text(json.dumps(dict(schemaVersion=1,entries=stories),ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
(DEST/'profiles.json').write_text(json.dumps(profiles,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
lines=['# Кампанія зі 100 історій','', 'Продовження перевіреної весняної дороги QC001–QC010. Канон: `Design/Campaign100/2026-10-11/STORY-UA.md`, `MECHANICS-UA.md`, `Authoring/places.tsv` та чинні JSON рівнів. Пазли, ID, порядок і історичні ревізії зберігаються. Нові деталі про анонімних користувачів локацій — пропозиції автора, а не встановлені факти чи документальні реконструкції.','', 'Сезони беруться з чинного рівня, а не з повторення п’яти шаблонів: весна, літо, осінь, зима, відлига й наступна весна. Літак послідовно відкривається у №42–44; осушений берег і водоскид — у №51–60; подорож завершується в Гавані.','', '| № / ID | Локація | Сюжетний крок | Мета та правила | Деталі | Погода | Завершення |','|---|---|---|---|---|---|---|']
for n,s in enumerate(stories,1):lines.append(f'| {n} / {s["id"]} | {s["title"]} | {s["canonicalBeat"]} | {s["mechanic"]} | {s["canonicalBefore"]} | {s["weather"]} | Усі гості, правильні побажання та проходи; далі {s["nextId"] or "меню після Гавані"} |')
lines += ['', '## Реалізація', '', 'Система спільна з першими десятьма: редаговані JSON чернетки, PWB для характерних груп, MicroVerse/River Modeler для землі й води, наявні старіння, обростання, сезонні ефекти та жива поверхня. Новий декор не змінює логіку тіні, шуму або прохідності. Правила стилю — `../SCENE-STORY-RULES-UA.md` та `../AircraftSeasons/surface.html`.', '', 'Перед публікацією кожна нова композиція проходить щонайменше сім фактичних циклів огляду, виправлення й нативного кадру. Таблиця — план; свіжі скриншоти, перевірки та підтвердження проходження зберігаються окремо.']
(DEST/'README-UA.md').write_text('\n'.join(lines)+'\n',encoding='utf-8')
print('Prepared 100 canonical stories and 90 additional composition profiles.')
