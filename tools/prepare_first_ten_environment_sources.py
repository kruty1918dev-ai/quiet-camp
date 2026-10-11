#!/usr/bin/env python3
"""Compact composition briefs with world-space routes/relief. No Resources publication."""
import json,hashlib
from pathlib import Path
ROOT=Path(__file__).resolve().parents[1]
DEST=ROOT/'Design/Campaign100/2026-10-11/FirstTenRefinement'

def main():
    levels=json.loads((DEST/'Baseline/published-first-ten.json').read_text())
    # Model IDs refer only to current gameplay libraries. Variants are separate outputs.
    briefs=[
      ('Старий маршрут відпочинку','Лавка стоїть біля розширення колишнього пішохідного маршруту; знак веде до поля. Ромашки повертаються вздовж краю проходу.',
       [('camp_bench',[-5.2,0,2.4],.68,95),('sign',[-3.5,0,4.2],.72,18)],
       [[[-7,0,-4],[-4.1,0,-1],[-4.1,0,3.8],[-1,0,3.5],[0,0,2.5]]],
       [('ridge',[4.5,4],.5,3),('hollow',[-4,1.5],-.13,2)],['daisies','broadleaf-understory'],'Лавка має чотири опорні контакти; знак низький, без яскравого нового матеріалу.'),
      ('Дві стежки, один прихисток','Два різні підходи сходяться біля входу. Старий навіс стоїть на сухому плечі схилу; стежки пояснюють правило проходів.',
       [('story_trail_shelter',[-4.8,0,3],1.65,20),('log',[-4.4,0,-1.8],.18,35)],
       [[[-7,0,4],[-4,0,4],[0,0,3]],[[5.5,0,-5],[4,0,-1.5],[3.5,0,3.4],[0,0,3]]],
       [('ridge',[-5,4],.65,3),('hollow',[4,-1],-.28,2.8)],['grass-patches','young-broadleaf'],'Навіс не отримує випадкового прогину опор; дві стежки читаються одночасно.'),
      ('Джерело під кроною','Вода збирається в кам’яному заглибленні під великим деревом. Низька огорожа завершує край джерела; це природне джерело, не друга криниця.',
       [('tree_default',[-4.1,0,-1.8],3.5,12),('stone_largeA',[-4.6,0,1],.48,25)],
       [[[0,0,-2.5],[-1,0,-3.8],[-3.7,0,-3.7],[-4.4,0,-.3]]],
       [('hollow',[-4.5,-.5],-.4,2.4),('ridge',[4.8,3.8],.55,3.5)],['moist-understory','ferns'],'Окремий authored basin і rock group; crown shadow має відповідати оновленій масці пазла, до publication.'),
      ('Тиха сторона вогнища','Вогнище займає неглибоку улоговину з одного боку. Інший край відгороджений рельєфом та підліском; тиша має просторове пояснення.',
       [('campfire_stones',[4.7,0,-1],.28,0),('log',[4.5,0,-3.7],.2,70)],
       [[[6,0,2],[4.2,0,1.3],[3,0,.5]]],
       [('hollow',[4.8,-1.2],-.38,2.3),('ridge',[-4,3],.85,3)],['quiet-grove','coarse-grass'],'Без повторного навісу; багаття і noise cells прив’язуються до одного джерела.'),
      ('Простір між друзями','Нерегулярні залишки саду оточують широке світле місце. Старі ряди ще можна впізнати, але молодий підлісок порушив їхню геометрію.',
       [('ua_valley_orchard',[-4.6,0,3.4],2.4,20),('ua_valley_orchard',[4.7,0,-3.8],2.1,-14)],
       [[[-6.5,0,5.2],[-3.8,0,4.8],[0,0,3.5]]],
       [('ridge',[-5,4.4],.35,4.5),('ridge',[5,-4],.5,3.8)],['old-orchard','wildflowers'],'Три нерегулярні групи саду; зберегти достатню відстань між гостьовими зонами.'),
      ('Зупинка без розкладу','Табір стоїть на траві біля старого сільського маршруту. Зупинка має невеликий заїзд, а кілька простих опор пояснюють напрям електромережі.',
       [('ua_bus_shelter_mosaic',[-6,0,2.5],2.1,90)],
       [[[-5.1,0,-8],[-5.4,0,-3],[-5.1,0,2.4],[-4.5,0,7.5]],[[-5.2,0,2],[-4.2,0,1.2],[-3.5,0,.5]]],
       [('ridge',[5,-2],.8,3.8),('hollow',[-5.2,-3],-.18,2)],['roadside-grass','scrub'],'Наметні клітини не перетинають дорогу; power route копіює road route з боковим offset.'),
      ('Ромашки за парканом','Низькі стіни та поріг пояснюють колишній двір. Хвіртка з’єднує шлях з входом; квіти заповнюють старі грядки, але лишають вільний прохід.',
       [('story_foundation',[-5,0,1.8],.35,22)],
       [[[0,0,-4],[0,0,-5],[-3.5,0,-4.8],[-5,0,-.3]]],
       [('ridge',[-5.5,3.4],.5,3),('hollow',[4,-2],-.25,2.8)],['daisies','abandoned-yard'],'Фундамент потребує геометрії залишків стін/дверного отвору; fence ensemble замкнений з явними пошкодженими openings.'),
      ('Шлях листоноші','Вузька заглиблена стежка проходить між нерегулярними групами тополь. Рельєф і вертикальні крони дають інший силует, без повторної лавки та знака.',
       [('ua_valley_poplar',[5.2,0,-3.8],3.6,9),('ua_valley_poplar',[5.7,0,4.5],3.2,-7)],
       [[[7,0,-6],[5.6,0,-2],[5,0,.5],[4,0,.5]],[[5.5,0,1],[5.6,0,4.5],[7,0,7]]],
       [('hollow',[5.5,.5],-.55,2.1),('ridge',[-5,4],.7,3.8)],['poplar-groups','edge-understory'],'Порівняти поплар із gameplay reference: below-reference-detail не вважати готовою гармонізацією.'),
      ('Спільна криниця','Проста криниця стоїть на невеликому сухому підвищенні. Два протоптані підходи та зношена дошка показують спільне користування водою.',
       [('ua_valley_well',[-5.2,0,2.9],1.15,30),('log',[-5.2,0,4.4],.14,85)],
       [[[0,0,3.5],[-1,0,4.5],[-4.6,0,3.5]],[[-7,0,-2],[-6.4,0,.5],[-5.7,0,2.6]]],
       [('ridge',[-5.3,2.8],.28,2.5),('hollow',[4,-3],-.3,3)],['trampled-grass','small-bushes'],'Криниця простіша за великі дерева; без повторної огорожі джерела QC003.'),
      ('Світло на роздоріжжі','Роздоріжжя підсумовує відновлений шлях. Невеликий ремонтований покажчик і теплий ліхтар говорять про турботу людей; великої зупинки тут немає.',
       [('sign',[-5.5,0,1.5],.85,35),('camp_lantern',[-5,0,.4],.3,0)],
       [[[-7,0,-6],[-4.6,0,.5],[-3.5,0,.5]],[[-4.6,0,.5],[-7,0,6.5]],[[-4.6,0,.5],[.2,0,5.2],[5,0,6]]],
       [('ridge',[4,4],.65,3.8),('hollow',[-5,-3],-.25,2.8)],['warm-edge','flower-patches'],'Ліхтар гармонується за фактичним screen size; geometry protection зберігає ручку, але не дозволяє надмірну деталізацію.'),
    ]
    places=[]
    for index,(title,story,props,routes,features,planting,note) in enumerate(briefs):
        level=levels[index];id=level['id'];w,h=level['width'],level['height']
        ex,ez=level['entry'];entry=[ex+.5-w/2,0,ez+.5-h/2]
        if ex==0:entry[0]=-w/2
        elif ex==w-1:entry[0]=w/2
        elif ez==0:entry[2]=-h/2
        else:entry[2]=h/2
        connection={1:(0,-1),2:(1,-1),3:(0,0),4:(0,-1),5:(0,-1),6:(1,-1),7:(0,0),8:(0,-1),9:(0,0),10:(0,-1)}[index+1]
        routes[connection[0]][connection[1]]=entry
        places.append({'id':id,'sourceLevelHash':level['contentHash'],'width':w,'height':h,'seed':level['decorSeed'],'titleUk':title,'storyUk':story,
          'status':'authoring-draft-not-baked','coordinates':'local Y-up; XZ ground; 1 unit = one board cell',
          'terrain':{'extent':[-12,12,-12,12],'groundY':-.012,'sampleStep':.4,'flatPlayableBounds':[-w/2,w/2,-h/2,h/2],'transitionWidth':1.6,'features':[{'kind':kind,'centerXZ':center,'height':height,'radius':radius} for kind,center,height,radius in features]},
          'props':[{'id':id+'/prop-'+str(i+1),'owner':id+'/place','sourceModel':model,'target':target,'height':height,'yaw':yaw,'supportProfile':'explicit model contacts required before bake','weatheringIntensity':.5 if model not in ('tree_default','ua_valley_orchard','ua_valley_poplar') else 0} for i,(model,target,height,yaw) in enumerate(props)],
          'entryConnection':{'point':entry,'routeId':id+'/route-'+str(connection[0]+1),'end':'first' if connection[1]==0 else 'last'},
          'routes':[{'id':id+'/route-'+str(i+1),'points':points,'width':.65 if id!='QC006' else 1.5 if i==0 else .65,'surface':'worn-asphalt' if id=='QC006' and i==0 else 'dirt','reserveVegetation':True} for i,points in enumerate(routes)],
          'planting':{'profiles':planting,'sourceModels':['tree_default','tree_pineRoundA','plant_bushSmall','grass','grass_leafsLarge','flower_yellowA'],'distribution':'separate patches and tiers; authored sightlines and route reservations','daisyVariant':{'source':'flower_yellowA','petalColor':[.91,.90,.79],'centerColor':[.81,.63,.25],'maskStatus':'requires geometry review'} if 'daisies' in planting else None},
          'acceptanceUk':note,'nativeAccepted':False,'puzzleRevisionPublished':False})
    folder=DEST/'Authoring';folder.mkdir(exist_ok=True)
    payload={'schemaVersion':1,'baseCampaignCount':100,'roadmapDeferred':True,'runtimePublication':False,'places':places}
    path=folder/'places.json';path.write_text(json.dumps(payload,ensure_ascii=False,indent=2)+'\n')
    print('Prepared 10 source composition briefs; gameplay/catalog unchanged; source SHA256 '+hashlib.sha256(path.read_bytes()).hexdigest())

if __name__=='__main__':main()
