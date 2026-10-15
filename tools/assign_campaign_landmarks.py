"""Reserve a physically distinct hero for every scene; does not publish."""
import json
from pathlib import Path

ROOT=Path(__file__).resolve().parents[1]
DEST=ROOT/'Design/LevelAuthoring/StoryCampaign'
catalog=json.loads((ROOT/'QuietCamp/Assets/QuietCamp/Authoring/CampaignLandmarks/catalog.json').read_text(encoding='utf-8'))['entries']
opening=json.loads((ROOT/'Design/LevelAuthoring/OpeningCampaign/profiles.json').read_text(encoding='utf-8'))
remaining=json.loads((DEST/'profiles.json').read_text(encoding='utf-8'))
rows=[r.split('|') for r in (ROOT/'Design/Campaign100/2026-10-11/Authoring/places.tsv').read_text(encoding='utf-8').splitlines()]
profiles=opening+remaining
for i,(p,entry) in enumerate(zip(profiles,catalog),1):
    p['kind']=rows[i-1][1]
    p['primaryLandmark']=entry['modelId'];p['primaryLabel']=entry['label'];p['reviewRevision']=0
    if p['kind'].startswith('aircraft'):continue
    anchor=p['world']['anchors'][0]
    anchor.update(model=entry['modelId'],label=entry['label'],metric=1,scale=1,ageAmount=.38)
    if p['kind'] in ('well','house','barn','shelter','bus','station','greenhouse','hut','haven'):
        anchor.update(x=-6.5 if i%2 else 5.8,z=5.3,yaw=35 if i%2 else -30)
    if entry['construction'].split(':')[0] in ('bridge','stairs','platform','sluice','crane','boat'):
        anchor.update(x=-9.5,z=3.2,water=False,y=0)
    if entry['construction'].startswith('bridge:'):
        anchor.update(x=-10,z=1.5,water=True,y=.12,yaw=90)
    if entry['construction'].startswith('boat:'):
        anchor.update(x=-9.7,z=3.2,water=True,y=-.18,yaw=90,motion='boat')
    if i==9:anchor.update(x=-6,z=4,yaw=15)
    if i==10:anchor.update(x=-5.8,z=4.2,yaw=15)
    p['world']['liveTerrain']=True
(DEST/'unique-profiles.json').write_text(json.dumps(profiles,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
lines=['# Головні об’єкти 100 рівнів','',
 'Вимога користувача від 2026-10-14: кожна головна модель використовується лише в одній локації. Дороги, ЛЕП, рослинність і допоміжний декор можуть повторюватися. Новий колір, сезон, масштаб, поворот або ID не роблять ту саму модель новою.', '',
 f'{sum("geometryHash" in e for e in catalog)} нових конструктивних моделей створені з метричних деталей у спільній палітрі проєкту; {sum("geometryHash" not in e for e in catalog)} акцент використовує різні наявні моделі. Геометрія запікається в чернетку, PWB розміщує її як редагований об’єкт. Каталог резервує головний об’єкт, а не забороняє його дрібні складові в оточенні.', '',
 'Літак: №42 — носова секція, №43 — хвіст, №44 — відірване крило; у третьому кадрі обидві секції мають виконувати роль обрамлення табору. Це необхідно ще перевірити через ігрову камеру.', '',
 '**Статус:** реєстр і геометрія підготовлені. Це не приймання всіх сцен: потрібні нативні кадри, сім циклів виправлень, перевірки проходження та публікація.', '',
 '| № | Локація | Єдиний головний об’єкт | Конструкція |','|---|---|---|---|']
for p,e in zip(profiles,catalog):lines.append(f'| {e["order"]} | {p["story"]["title"]} | {e["label"]} | `{e["construction"]}` |')
(DEST/'LANDMARKS-UA.md').write_text('\n'.join(lines)+'\n',encoding='utf-8')
print('100 unique primary-object assignments prepared; campaign publication unchanged.')
