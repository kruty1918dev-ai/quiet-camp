"""Recorded corrections following inspection; inputs/outputs stay separate."""
import argparse,copy,json,math,hashlib
from pathlib import Path
ROOT=Path(__file__).resolve().parents[1]
QA=Path('C:/Users/Public/qcui-20261006/QuietCamp')
BASE=QA/'Temp/ai/OpeningCampaign'
p=argparse.ArgumentParser();p.add_argument('--from-round',required=True);p.add_argument('--to-round',required=True);a=p.parse_args()
target=BASE/a.to_round;target.mkdir(parents=True,exist_ok=True)
review=[]
def projected_bounds(instance,model,art):
 v=model['vertices'];lo=[min(v[k::3]) for k in range(3)];hi=[max(v[k::3]) for k in range(3)]
 pitch,yaw=map(math.radians,art['cameraEuler'][:2]);angle=math.radians(instance.get('yaw',0));scale=instance.get('scaleXYZ') or [instance['scale']]*3
 points=[]
 for mask in range(8):
  x,y,z=[(hi[k] if mask&(1<<k) else lo[k])*scale[k] for k in range(3)]
  x,z=x*math.cos(angle)+z*math.sin(angle),-x*math.sin(angle)+z*math.cos(angle)
  x+=instance['x'];y+=instance['y'];z+=instance['z']
  points.append([x*math.cos(yaw)-z*math.sin(yaw),x*math.sin(yaw)*math.sin(pitch)+y*math.cos(pitch)+z*math.cos(yaw)*math.sin(pitch),x*math.sin(yaw)*math.cos(pitch)-y*math.sin(pitch)+z*math.cos(yaw)*math.cos(pitch)])
 return [min(p[k] for p in points) for k in range(3)],[max(p[k] for p in points) for k in range(3)]
def clear_landmark_silhouettes(doc):
 art=doc['art'];models={m['id']:m for m in art['models']};heroes=[i for i in art['instances'] if i['id'].startswith('opening/')];protect=[projected_bounds(i,models[i['modelId']],art) for i in heroes]
 removed=[];kept=[]
 for instance in art['instances']:
  model=models[instance['modelId']]
  if not instance['id'].startswith('opening/') and model.get('seasonalRole') in ['deciduous','conifer'] and (max(model['vertices'][1::3])-min(model['vertices'][1::3]))*instance['scale']>2:
   lo,hi=projected_bounds(instance,model,art)
   if any(lo[2]<a[2] and lo[0]<b[0]+.35 and hi[0]>a[0]-.35 and lo[1]<b[1]+.35 and hi[1]>a[1]-.35 for a,b in protect):removed.append(instance['id']);continue
  kept.append(instance)
 art['instances']=kept
 return len(removed)
def terrain_height(art,x,z):
 land=art['landscape'];n=land['resolution'];size=land['size'];px=max(0,min(n-1,(x-land['originX'])/size*(n-1)));pz=max(0,min(n-1,(z-land['originZ'])/size*(n-1)));ix=int(px);iz=int(pz);fx=px-ix;fz=pz-iz;h=land['heights']
 return sum(h[min(n-1,iz+dz)*n+min(n-1,ix+dx)]*(fx if dx else 1-fx)*(fz if dz else 1-fz) for dx in [0,1] for dz in [0,1])
def seat_height(model):
 # Ray through the center finds the actual tabletop/seat, not a guessed world height.
 v=model['vertices'];hits=[]
 for k in range(0,len(v),9):
  a,b,c=[v[k+j:k+j+3] for j in [0,3,6]];det=(b[2]-c[2])*(a[0]-c[0])+(c[0]-b[0])*(a[2]-c[2])
  if abs(det)<1e-6:continue
  u=((b[2]-c[2])*(-c[0])+(c[0]-b[0])*(-c[2]))/det;w=((c[2]-a[2])*(-c[0])+(a[0]-c[0])*(-c[2]))/det
  if u>=0 and w>=0 and u+w<=1:hits.append(u*a[1]+w*b[1]+(1-u-w)*c[1])
 return max(hits) if hits else .55
for index in range(1,11):
 ident=f'QC{index:03}';source=BASE/a.from_round/(ident+'.draft.json');doc=json.loads(source.read_text(encoding='utf-8'));art=doc['art']
 before=hashlib.sha256(source.read_bytes()).hexdigest()
 if a.to_round=='C01':
  sizes=[8.7,9.8,10.2,9.6,10.2,10.2,10.5,10.4,10.8,11.1]
  art['orthographicSize']=sizes[index-1];art['cameraTarget']=[0,.45,.5]
  issue='Табір і побутові предмети надто дрібні; кадр переважно займає периферійний ліс.'
  change=f'Наблизив ігрову камеру до ортографічного розміру {sizes[index-1]} та підняв ціль на 0.45 м.'
 elif a.to_round=='C02':
  removed=clear_landmark_silhouettes(doc)
  issue=['Лавку закриває крона праворуч.','Стовбури маскують місце відпочинку й торбину.','Колодязний журавель губиться за кронами.','Дрова й лавка перекриті ближнім лісом.','Садові дерева та спільна лавка не читаються як окрема група.','Мозаїчний навіс прихований за деревом.','Паркан і хвіртку майже не видно.','Вказівник і лавку маскує висока хвоя.','Криницю затискає передній ряд дерев.','Зупинку видно, але вказівник губиться.'][index-1]
  change=f'Прибрав {removed} декоративних дерев, чиї проєкції ближчі за характерні предмети та перекривають їхні силуети. Логічні дерева поля збережені.'
 elif a.to_round=='C03':
  original=json.loads((BASE/'C01'/(ident+'.draft.json')).read_text(encoding='utf-8'))
  current={i['id'] for i in art['instances']}
  art['instances'] += [i for i in original['art']['instances'] if i['id'].startswith('opening/') and i['id'] not in current]
  models={m['id']:m for m in art['models']};flowers=0;kept=[]
  for pose in art['instances']:
   if models[pose['modelId']].get('seasonalRole')=='flower' and not pose['id'].startswith('opening/'):
    # Keep uneven patches rather than an evenly distributed white confetti layer.
    patch=math.sin(pose['x']*.75+index)+math.cos(pose['z']*.6-index)
    if patch<.7:flowers+=1;continue
   kept.append(pose)
  art['instances']=kept
  if index in [6,10]:
   hero=next(i for i in art['instances'] if i['id']==f'opening/{ident}/landmark-0');hero['yaw']=45;hero['rotationEuler']=None
  issue='Ромашки рівномірно змагаються з табором; характерні предмети читаються слабко.'+(' Одне садове дерево помилково прибране під час захисту лавки.' if index==5 else '')
  change=f'Прибрав {flowers} розсіяних квіткових екземплярів, зберіг нерівномірні групи. Відновив авторські герої та повернув зупинки відкритим фасадом до камери.'
 elif a.to_round=='C04':
  models={m['id']:m for m in art['models']};removed=0;kept=[]
  for pose in art['instances']:
   role=models[pose['modelId']].get('seasonalRole')
   if not pose['id'].startswith('opening/') and role=='conifer' and index in [1,3,5,6,7,9,10] and int(hashlib.sha256(pose['id'].encode()).hexdigest()[:6],16)%3!=0:removed+=1;continue
   kept.append(pose)
  art['instances']=kept
  if index in [6,10]:
   hero=next(i for i in art['instances'] if i['id']==f'opening/{ident}/landmark-0');hero['yaw']=225
   art['cameraEuler'][0]=46
  atmosphere=art['atmosphere']
  if index not in [6,9]:atmosphere['sunIntensity']*=1.1;atmosphere['fog']*=.8;atmosphere['contrast']+=2
  issue='Місце все ще виглядає як той самий хвойний ліс, а не весняна галявина/двір.' if index in [1,3,5,6,7,9,10] else 'Густий верхній план зливається, світло не відокремлює побутові акценти.'
  change=f'Прорідив {removed} хвойних дерев у локаціях відкритої дороги, зберіг листяні групи. У ясних кадрах уточнив світло й серпанок; зупинки повернув протилежним боком і знизив кут камери для читання фасаду.'
 elif a.to_round=='C06':
  sizes=[7.4,7.8,8.5,8,8.5,8.7,8.7,8.8,9,9.2];art['orthographicSize']=sizes[index-1]
  models={m['id']:m for m in art['models']};seats=[i for i in art['instances'] if models[i['modelId']].get('sourceModelId') in ['qc_bench','qc_picnic_table']]
  cups=[i for i in art['instances'] if models[i['modelId']].get('sourceModelId')=='qc_mug']
  for cup in cups:
   if seats:
    seat=min(seats,key=lambda s:(s['x']-cup['x'])**2+(s['z']-cup['z'])**2)
    cup['x']=seat['x'];cup['z']=seat['z'];cup['surfaceOffset']=seat_height(models[seat['modelId']])*seat['scale']-min(models[cup['modelId']]['vertices'][1::3])
    cup['y']=terrain_height(art,cup['x'],cup['z'])+cup['surfaceOffset']
  if index==8:
   roll=next(i for i in art['instances'] if i['id']==f'opening/{ident}/detail-0');roll['x']+=.6;roll['z']-=.6;roll['y']=terrain_height(art,roll['x'],roll['z'])
  story=json.loads((ROOT/'QuietCamp/Assets/QuietCamp/Resources/QuietCamp/opening_campaign_story.json').read_text(encoding='utf-8'))['entries'][index-1]
  doc['level']['environmentalStory']['beats'][0]['intendedInference']=story['finding']
  issue='На мобільному масштабі дрібні сліди користування залишаються надто малими; висоту кухлів треба звірити з реальною геометрією опори.'
  change=f'Уточнив масштаб камери до {sizes[index-1]}; {len(cups)} кухлів поставив на фактичні верхні грані лавки/столу через геометричне семплювання. На стежці листоноші розсунув торбу й спальник.'
 elif a.to_round=='C07':
  preset=art['atmosphere']
  changes=[dict(fog=.1,lowFog=.08,contrast=9,temperature=-2),dict(fog=.2,lowFog=.1,sunIntensity=1.08,contrast=8),dict(fog=.12,lowFog=.08,rays=.75,contrast=9),dict(sunColor=0xffd4a0,temperature=8,fog=.14,sunIntensity=1.08),dict(sunColor=0xffeac0,temperature=2,fog=.1,saturation=5),dict(fog=.25,lowFog=.14,exposure=.18),dict(fog=.16,lowFog=.08,contrast=10),dict(fog=.11,lowFog=.07,sunIntensity=1.35,contrast=9),dict(fog=.24,lowFog=.13,exposure=.2),dict(sunEuler=[22,108,0],sunColor=0xffce9a,ambientColor=0x8e9eb1,skyColor=0xb4afb2,sunIntensity=.92,temperature=7,fog=.16,lowFog=.09,rays=.48,exposure=.2)]
  preset.update(changes[index-1])
  issue=['Тепла галявина має зайву сіру вуаль.','Ранковий туман знижує контраст столу й проходів.','Криниця потребує яснішого світла для читання опор.','Тихий край багаття не відрізняється настроєм від сонячних місць.','Молодий сад має надто приглушену весняну палітру.','Під мрякою речі під навісом занадто темні.','Стики хвіртки й фундаменту губляться в серпанку.','На стежці листоноші потрібно краще читати вказівник і лавку.','Біля спільної криниці бракує світла за похмурої погоди.','Назва «Світло на роздоріжжі» поки не підтримана вечірнім настроєм.'][index-1]
  change='Узгодив світло, температуру, серпанок і постобробку цієї локації: '+str(changes[index-1])+'. Спільна погода, вітер, звук і правила збережені.'
 else:raise ValueError('This correction has not been defined/reviewed.')
 dest=target/(ident+'.draft.json');dest.write_text(json.dumps(doc,ensure_ascii=False,separators=(',',':')),encoding='utf-8')
 review.append(dict(id=ident,issue=issue,correction=change,sourceSha256=before,draftSha256=hashlib.sha256(dest.read_bytes()).hexdigest(),nativeFrame=f'Review/{a.to_round}/{ident}.png'))
out=ROOT/'Design/LevelAuthoring/OpeningCampaign/Review'/a.to_round;out.mkdir(parents=True,exist_ok=True)
(out/'corrections.json').write_text(json.dumps(review,ensure_ascii=False,indent=2),encoding='utf-8')
print('Prepared',a.to_round,'corrections for 10 inspected compositions; native review still required.')
