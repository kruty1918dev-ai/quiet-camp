"""Author the twenty season presets and stable audio/visual settings."""
import json
from pathlib import Path
ROOT=Path(__file__).resolve().parents[1]
TARGET=ROOT/'QuietCamp/Assets/QuietCamp/Resources/QuietCamp/season_atmosphere.json'
presets=[]
def add(season,id,label,description,**kw):
 d=dict(id=f'{season}_{id}',label=label,season=season,description=description,revision=1,seed=1918,sunEuler=[45,65,0],sunColor=0xffefd5,ambientColor=0x9fa78a,skyColor=0x879b93,sunIntensity=1.1,shadows=.8,exposure=0,contrast=3,saturation=-8,temperature=0,bloom=.12,fog=0,rays=0,rain=0,snow=0,snowCover=0,leaves=0,insects=0,wind=.2,audioKeys=['ambience.wind','ambience.bird'],audioGains=[.2,.3]);d.update(kw);presets.append(d)
add('spring','morning','Весняний світанок','Свіжа зелень, ранковий серпанок і спів птахів.',sunEuler=[28,120,0],temperature=9,fog=.28,rays=.3,insects=.12,sunIntensity=.95)
add('spring','sunny','Сонячна квітуча весна','М’яке сонце, квіти й кілька метеликів.',temperature=6,rays=.38,insects=.4,wind=.14,saturation=-3)
add('spring','drizzle','Весняна мряка','Прохолодне розсіяне світло й дрібний дощ.',sunIntensity=.48,shadows=.3,rain=.3,fog=.15,temperature=-7,audioKeys=['ambience.wind','ambience.rain'],audioGains=[.16,.4])
add('spring','after_rain','Весна після дощу','Теплі просвіти в хмарах і свіже листя.',sunEuler=[38,180,0],rays=.55,fog=.12,insects=.2,temperature=5,audioGains=[.12,.5])
add('spring','breeze','Вітряний весняний день','Рух трави, легкий пилок і виразні хмарні тіні.',wind=.7,rays=.15,insects=.12,sunIntensity=.85,audioGains=[.5,.2])
add('summer','day','Сонячний літній день','Ясне світло, жучки біля трави та сонячні промені.',sunEuler=[58,150,0],rays=.55,insects=.65,temperature=7,contrast=6,audioGains=[.15,.35])
add('summer','evening','Теплий літній вечір','Золоте бокове світло, спокій і цвіркуни.',sunEuler=[24,190,0],sunColor=0xffcf83,temperature=22,bloom=.2,rays=.45,insects=.5,audioKeys=['ambience.wind','ambience.crickets'],audioGains=[.12,.48])
add('summer','rain','Легкий літній дощ','М’яка прохолода, краплі та приглушений ліс.',sunIntensity=.58,shadows=.35,rain=.4,fog=.12,temperature=-4,insects=.06,audioKeys=['ambience.wind','ambience.rain'],audioGains=[.2,.55])
add('summer','forest','Літній лісовий ранок','Промені між кронами, серпанок і метелики.',sunEuler=[32,120,0],rays=.75,fog=.2,insects=.38,temperature=7,audioGains=[.18,.55])
add('summer','fireflies','Літня ніч зі світлячками','Синє місячне світло й теплі живі вогники.',sunEuler=[42,165,0],sunColor=0x98b5df,ambientColor=0x718597,skyColor=0x283e59,sunIntensity=.38,temperature=-10,exposure=.05,bloom=.4,fog=.08,insects=.8,audioKeys=['ambience.wind','ambience.crickets','ambience.owl'],audioGains=[.1,.5,.12])
add('autumn','golden','Золотий осінній день','Золоті крони, тепле сонце та повільний листопад.',sunEuler=[38,175,0],temperature=18,rays=.4,leaves=.45,wind=.28,audioKeys=['ambience.wind','ambience.leaves'],audioGains=[.24,.35])
add('autumn','morning','Туманний осінній ранок','Мідне листя крізь прохолодну ранкову імлу.',sunIntensity=.68,temperature=-3,fog=.42,leaves=.2,rays=.15,audioKeys=['ambience.wind','ambience.bird','ambience.leaves'],audioGains=[.18,.15,.2])
add('autumn','rain','Тихий осінній дощ','Темніші крони, мокрий берег і дрібні краплі.',sunIntensity=.42,shadows=.25,temperature=-8,rain=.48,fog=.16,leaves=.15,audioKeys=['ambience.wind','ambience.rain','ambience.leaves'],audioGains=[.28,.6,.12])
add('autumn','wind','Вітряний листопад','Помітні пориви й листя, що летить із крон.',sunIntensity=.8,temperature=5,leaves=.85,wind=.8,rays=.12,audioKeys=['ambience.wind','ambience.leaves'],audioGains=[.6,.55])
add('autumn','sunset','Осінній захід сонця','Мідний захід, довгі тіні та зріджені крони.',sunEuler=[23,195,0],sunColor=0xffbb77,temperature=24,leaves=.35,rays=.5,bloom=.18,audioKeys=['ambience.wind','ambience.leaves','ambience.owl'],audioGains=[.2,.25,.08])
add('winter','clear','Ясний морозний день','Снігові шапки, холодні тіні й чітке сонце.',sunEuler=[35,170,0],sunColor=0xe8f2ff,ambientColor=0xa8b9c4,skyColor=0x9db7c5,temperature=-12,snowCover=.82,rays=.3,exposure=-.12,wind=.18,audioKeys=['ambience.wind'],audioGains=[.25])
add('winter','snowfall','Тихий зимовий сніг','Розсіяне світло та великі повільні сніжинки.',sunIntensity=.55,shadows=.35,temperature=-8,snow=.48,snowCover=.88,fog=.16,audioKeys=['ambience.wind'],audioGains=[.3])
add('winter','blizzard','Зимова завірюха','Косий сніг, пориви вітру й молочна імла.',sunIntensity=.34,shadows=.2,temperature=-14,snow=1,snowCover=1,wind=1,fog=.48,contrast=-5,audioKeys=['ambience.wind','ambience.gust'],audioGains=[.68,.45])
add('winter','evening','Рожевий зимовий вечір','Рожеве сонце, синій сніг і легке мерехтіння.',sunEuler=[24,190,0],sunColor=0xffc6b0,ambientColor=0x9dadc4,skyColor=0x8395b5,temperature=6,snow=.15,snowCover=.85,rays=.38,bloom=.22,audioKeys=['ambience.wind'],audioGains=[.2])
add('winter','moon','Місячна зимова ніч','Холодна ніч, рідкі сніжинки й тихий вітер.',sunEuler=[42,160,0],sunColor=0xa1bddf,ambientColor=0x8497b1,skyColor=0x27394f,sunIntensity=.38,temperature=-18,snow=.2,snowCover=.9,bloom=.24,fog=.1,audioKeys=['ambience.wind','ambience.owl'],audioGains=[.25,.08])
for preset in presets:
 preset['audioKeys']=[key.replace('ambience.leaves','sfx.rustle') for key in preset['audioKeys']]
 preset['sunEuler'][0]=max(34,preset['sunEuler'][0])
 if 'night' in preset['id'] or 'fireflies' in preset['id'] or 'moon' in preset['id']:
  preset['sunIntensity']=.48;preset['exposure']=.12;preset['ambientColor']=0x8c9dae
 preset['exposure']+=.10
 if preset['fog']>0:preset['fog']*=.85
 preset['shadows']=max(.18,preset['shadows']-.08)
 preset['saturation']+=3
 preset['contrast']+=1
 preset['sunIntensity']=round(preset['sunIntensity']*1.03,4)
assert len(presets)==20
if TARGET.exists():
 existing=json.loads(TARGET.read_text(encoding='utf-8'))
 if any(p.get('revision',1)>1 for p in existing.get('presets',[])):
  raise SystemExit('Reviewed season_atmosphere.json is authoritative. Edit that JSON; refusing to overwrite revision 2.')
TARGET.write_text(json.dumps(dict(version=1,presets=presets),ensure_ascii=False,indent=2),encoding='utf-8')
print('Authored 20 presets, five per season')
