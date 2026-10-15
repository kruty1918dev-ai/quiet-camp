"""Real Game View check of campaign controls and the optional story finding."""
import hashlib,json,subprocess,sys,time
from pathlib import Path

ROOT=Path(__file__).resolve().parents[1]
QA=Path('C:/Users/Public/qcui-20261006/QuietCamp')
OUT=ROOT/'Design/LevelAuthoring/StoryCampaign/Verification/HudSmoke/After'


def request(mode,**values):
    command=[sys.executable,str(ROOT/'tools/request_authoring_props.py'),'--project',str(QA),'--mode',mode]
    for key,value in values.items():command+=['--'+key,str(value)]
    result=subprocess.run(command,capture_output=True,text=True,encoding='utf-8')
    if result.returncode:raise RuntimeError(result.stdout+result.stderr)
    return {} if mode=='authoring-stop' else json.loads((QA/'Temp/ai/qc007-art-result.json').read_text(encoding='utf-8'))


def frame(name):
    target=OUT/(name+'.png');target.unlink(missing_ok=True)
    request('opening-gameplay',name='frame',output=target)
    deadline=time.monotonic()+30
    while not target.exists() or target.stat().st_size<1000:
        if time.monotonic()>deadline:raise RuntimeError('Game View screenshot was not written.')
        time.sleep(.2)
    return dict(path=target.name,sha256=hashlib.sha256(target.read_bytes()).hexdigest())


def main():
    OUT.mkdir(parents=True,exist_ok=True);before=request('authoring-workspace-status');proof={}
    try:
        request('opening-gameplay',name='start');deadline=time.monotonic()+180
        while True:
            status=request('opening-gameplay',name='status')
            if status.get('ready') and status.get('id')=='QC001':break
            if time.monotonic()>deadline:raise RuntimeError('First camp did not become ready.')
            time.sleep(.3)
        assert status['memoryOnly'] and status['frozenArtMatches'] and status['story'],status
        request('opening-gameplay',name='pro');request('authoring-focus-game');request('authoring-place-witness');time.sleep(1)
        hud=request('opening-gameplay',name='hud');proof['layout']=hud
        (OUT/'layout.json').write_text(json.dumps(hud,indent=2)+'\n',encoding='utf-8')
        proof['gameplay']=frame('QC001')
        for node in hud['nodes']:
            if node['id'] not in ('pause','opening-finding'):continue
            assert node['exists'] and node['active'] and node['alpha']>.99,node
            assert all(-2<=x<=hud['screen'][0]+2 and -2<=y<=hud['screen'][1]+2 for x,y in node['corners']),node
        request('opening-gameplay',name='finding');time.sleep(.7);proof['finding']=frame('finding')
        proof['status']='passed'
    finally:
        if request('authoring-status').get('playing'):request('authoring-stop')
        deadline=time.monotonic()+120
        while request('authoring-status').get('playing'):
            if time.monotonic()>deadline:raise RuntimeError('Editor did not leave the isolated preview.')
            time.sleep(.3)
        after=request('authoring-workspace-status')
        assert before.get('artSha256')==after.get('artSha256') and before.get('objects')==after.get('objects') and before['status']==after['status']
        (OUT/'verification.json').write_text(json.dumps(proof,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
    print('Game View controls and story-finding smoke passed; native screenshots require visual inspection.')


if __name__=='__main__':main()
