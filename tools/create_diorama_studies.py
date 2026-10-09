"""Seed the first, editable art intentions. Never writes runtime resources or levels.

Run once; subsequent composition edits use inspect/patch with an expected hash.
"""
import hashlib
import json
from pathlib import Path

ROOT = Path('QuietCamp/Assets/QuietCamp/Authoring/Roadmap/Composition')
OUT = ROOT / 'DioramaStudies'
MODELS = json.loads(Path('Design/Roadmap/ModelCatalogue/catalogue.json').read_text())['models']


def write(name, value):
    (OUT / name).write_text(json.dumps(value, ensure_ascii=False, indent=2) + '\n')


def main():
    if (OUT / 'manifest.json').exists():
        raise SystemExit('Studies already exist: inspect and patch intentions instead of reseeding.')
    OUT.mkdir(parents=True, exist_ok=True)
    assets = json.loads((ROOT / 'assets.json').read_text())
    templates = [t for t in json.loads((ROOT / 'templates.json').read_text())
                 if t['id'] in ('ua.homestead', 'ua.orchard', 'ua.roadside-stop')]
    for template in templates:
        if template['id'] == 'ua.orchard':
            template['width'] = 9; template['depth'] = 9
    bindings = []
    choices = {'pine':187, 'birch':813, 'fruit':816, 'grass':760, 'rock':807,
               'flowers':821, 'lilies':982, 'reeds':987, 'mushrooms':874,
               'crate':834, 'cart':843, 'chime':915, 'sign':888,
               'canoe':1029, 'bench':571, 'cabin':742, 'sunflower':810}
    for key, number in choices.items():
        model = MODELS[number - 1]
        matches = sorted(Path('QuietCamp').glob(model['package'] + '/Prefabs/**/' + model['name'] + '.prefab'))
        if not matches:
            raise SystemExit('Missing prefab: ' + model['displayId'])
        w,h,d = model['bounds']
        aid = 'study.' + key
        assets.append(dict(id=aid, source=model['path'], license='purchased-local-donor',
                           sourceHash=model['sourceHash'], height=h, sourceHeight=h,
                           width=w, depth=d, radius=(w*w+d*d)**.5/2,
                           placementClass='canopy' if key in ('pine','birch','fruit') else
                           'groundcover' if key in ('grass','flowers','lilies','reeds','mushrooms','sunflower') else 'solid',
                           seasons=[], lod='preview-prefab-LOD1-when-present'))
        bindings.append(dict(asset=aid, catalogueId=model['displayId'], guid=model['id'][2:],
                             source=model['path'], sourceHash=model['sourceHash'],
                             prefab=matches[0].relative_to('QuietCamp').as_posix(),
                             prefabHash=hashlib.sha256(matches[0].read_bytes()).hexdigest()))

    def role(name, asset, x, z, height, yaw=0, parent=None):
        r=dict(id=name, asset=asset, x=x, z=z, height=height, yaw=yaw)
        if parent: r['parent']=parent
        return r

    features = {
        'apiary':[role('hive-'+str(i),'ua_beehive',-3+i*2,1.5,1) for i in range(4)]
                  +[role('tree-'+str(i),'ua_orchard_tree',-3+i*3,-2,2.8) for i in range(3)]
                  +[role('well','ua_well',3,3.7,.9)],
        'sunflowers':[role('crop-'+str(i),'ua_sunflower_patch',-3+i*3,-1,1.8) for i in range(3)]
                    +[role('hero','study.sunflower',-2,2.5,1.7),role('cart','study.cart',2,3,1.1)],
        'birches':[role('tree-'+str(i),'study.birch',x,z,h) for i,(x,z,h) in enumerate([(-3,-2,4),(0,-3,4.5),(3,-2,3.8),(-3,2,3.6)])]
                  +[role('bench','study.bench',1,2,1),role('mushrooms','study.mushrooms',2,3.5,.35)],
        'pines':[role('tree-'+str(i),'study.pine',x,z,h) for i,(x,z,h) in enumerate([(-3,-2,4.5),(0,-3,5),(3,-2,4),(-3,2,3.9)])]
                +[role('stack','log_stack',1,2,.8),role('crate','study.crate',3,2,.65)],
        'willows':[role('willow-'+str(i),'ua_young_willow',x,z,h) for i,(x,z,h) in enumerate([(-3,-2,3.5),(0,-3,3.1),(3,-2,3.4)])]
                  +[role('post','ua_mooring_post',-2.5,2.5,.9),role('canoe','study.canoe',2.5,3,.7,80)],
        'reedbank':[role('reed-'+str(i),'study.reeds',-3+i*1.5,-2,1.3) for i in range(5)]
                   +[role('willow','ua_young_willow',-3,2.5,3.5),role('bridge','ua_plank_bridge',1.5,1.8,.55,90)],
        'cabin':[role('cabin','study.cabin',-1,-1,2.9),role('stack','log_stack',2.8,1.6,.7),
                 role('cart','study.cart',-2.5,3,1),role('chime','study.chime',2.8,-2,.7)],
    }
    for name, roles in features.items():
        templates.append(dict(id='study.'+name,width=11,depth=11,entrance=False,roles=roles))

    specs=[
      ('D01','Тиха садиба','spring','ua.homestead','fruit','QC001','Білена хата, криниця та замкнений тин; світла галявина праворуч.','glades'),
      ('D02','Весняний сад','spring','ua.orchard','fruit','QC003','Ряди плодових дерев, польові квіти й ручний візок; відкрита межа саду.','farms'),
      ('D03','Соняшниковий край','summer','study.sunflowers','fruit','QC005','Соняшники читаються смугою поля, а не випадковими кущами; візок на межі.','farms'),
      ('D04','Пасіка під садом','summer','study.apiary','fruit','QC006','Чотири вулики й плодовий затінок; стежка проходить повз робочу ділянку.','farms'),
      ('D05','Березове узлісся','autumn','study.birches','birch','QC007','Жовті берези, грибний край і лавка; низька галявина з теплими наметами.','forest'),
      ('D06','Соснова стоянка','summer','study.pines','pine','QC008','Високий хвойний силует, дрова й ящик; відкритий передній план для табору.','forest'),
      ('D07','Вербовий ставок','summer','study.willows','fruit','QC009','Верби, дерев’яний човен і швартовний стовп; гладка вода з окремою береговою смугою.','shores'),
      ('D08','Очеретяний берег','summer','study.reedbank','fruit','QC010','Рогіз і місток на березі; вода не заповнює місце табору.','shores'),
      ('D09','Лісова хатина','autumn','study.cabin','pine','QC004','Кам’яно-дерев’яна хатина, склад дров і лісовий візок; камерне місце перепочинку.','forest'),
      ('D10','Осінній хутір','autumn','ua.homestead','birch','QC007','Білена хата за штахетником, жовте узлісся і невеликий край пшеничного поля.','villages'),
      ('D11','Зимова садиба','winter','ua.homestead','pine','QC002','Той самий зрозумілий двір у снігу; голий сад, хвойне тло й доступний підхід.','haven'),
      ('D12','Сільська зупинка','spring','ua.roadside-stop','fruit','QC003','Мозаїчна зупинка біля дороги, лавка й дерево; табір на окремій бічній галявині.','stations'),
    ]
    presentations=[]
    for index,(sid,title,season,template,tree,reference,description,district) in enumerate(specs):
        node=sid+'.clearing'; path=sid+'.path'; zone=sid+'.focal-zone'
        water=sid in ('D07','D08')
        doc=dict(schemaVersion=1,id=sid,seed=1918+index*101,season=season,
                 nodes=[dict(id=node,x=3.6,z=-3,radius=3.35)],
                 zones=[dict(id=zone,kind='parcel',x=-7,z=3,width=10,depth=10,density=0),
                        dict(id=sid+'.tree-edge',kind='forest',species='study.'+tree,x=3,z=6,width=15,depth=5,density=.27),
                        dict(id=sid+'.flower-edge',kind='meadow',species='study.grass',x=0,z=0,width=24,depth=17,density=.55)],
                 routes=[dict(id=path,kind='road' if sid=='D12' else 'path',width=.6 if sid!='D12' else 1.15,
                              points=[dict(x=-.6,z=-9),dict(x=-.6,z=1),dict(x=1.8,z=9)])],
                 ensembles=[dict(id=sid+'.focal',template=template,scale=.6,entranceConnectsTo=path,
                                 fenceVariant='picket' if sid in ('D10','D11') else 'wattle',state='maintained',
                                 placement=dict(zone=zone,x=-7,z=3,yaw=180,fixedPosition=True,faceRoute=False))],
                 landmarks=[dict(id=sid+'.tent-a',asset='tent_smallOpen',x=2.4,z=-3,height=1.18,yaw=12,revealOwner=node),
                            dict(id=sid+'.tent-b',asset='tent_smallOpen',x=4.8,z=-1.4,height=1.05,yaw=-25,revealOwner=node)],
                 surfaces=[],budgets=dict(instances=1000,materials=6,renderers=32,cacheBytes=67108864))
        if sid=='D02':doc['landmarks'].append(dict(id=sid+'.cart',asset='study.cart',x=-6,z=-2.5,height=.85,yaw=50,revealOwner=node))
        if sid=='D10':doc['zones'].append(dict(id=sid+'.field',kind='field',species='ua_wheat_patch',x=8,z=3,width=6,depth=4,density=.2))
        if sid=='D12':doc['landmarks'].append(dict(id=sid+'.bench',asset='study.bench',x=-5,z=-1,height=.8,yaw=180,revealOwner=node))
        write(sid+'.json',doc)
        presentations.append(dict(id=sid,titleUk=title,descriptionUk=description,season=season,
                                  intendedDistrict=district,visualReferenceLevelId=reference,
                                  referencePurpose='visual vocabulary only; no new puzzle or level',
                                  radiusX=14,radiusZ=11,cameraYaw=-25,cameraPitch=52,
                                  water=dict(x=-7,z=0,radiusX=4.8,radiusZ=3.1) if water else None))
    write('manifest.json',dict(schemaVersion=1,id='quiet-camp-diorama-studies',regions=[s[0]+'.json' for s in specs],
                              assets='assets.json',templates='templates.json',coordinateSpace='metres-y-up',
                              compilerRevision='semantic-composer-0.3.0'))
    write('assets.json',assets);write('templates.json',templates)
    write('bindings.json',bindings);write('presentation.json',presentations)
    print('Created 12 independent diorama intentions, 17 donor bindings; no runtime bake.')


if __name__=='__main__':main()
