"""Record actual gameplay camera motion and quality tiers without touching saved progress."""
import argparse
import hashlib
import json
from pathlib import Path
import subprocess
import sys
import time
import imageio_ffmpeg

sys.stdout.reconfigure(encoding='utf-8')
ROOT=Path(__file__).resolve().parents[1]
QA=Path(r'C:\Users\Public\qcui-20261006\QuietCamp')
GALLERY=ROOT/'Design/LevelAuthoring/AircraftSeasons'
p=argparse.ArgumentParser(description=__doc__)
p.add_argument('--round',default='A13')
a=p.parse_args()
OUTPUT=GALLERY/'AirVerification';OUTPUT.mkdir(exist_ok=True)
MOVIES=GALLERY/'Motion';MOVIES.mkdir(exist_ok=True)


def request(mode,**values):
    args=[sys.executable,'-X','utf8',str(ROOT/'tools/request_authoring_props.py'),'--project',str(QA),'--mode',mode]
    for k,v in values.items():args+=['--'+k,str(v)]
    result=subprocess.run(args,capture_output=True,text=True,encoding='utf-8')
    if result.returncode:raise RuntimeError(result.stdout+result.stderr)
    return {} if mode=='authoring-stop' else json.loads((QA/'Temp/ai/qc007-art-result.json').read_text(encoding='utf-8'))


def snapshot(folder):
    return {p.relative_to(folder).as_posix():hashlib.sha256(p.read_bytes()).hexdigest() for p in folder.rglob('*') if p.is_file() and p.suffix in ('.mvs','.bak','.txt')}


first=GALLERY/'AirReview'/a.round/'spring/spring.draft.json'
progress=Path(request('props-game-status',draft=first)['persistentDataPath'])
before=snapshot(progress);workspace=request('authoring-workspace-status')
for season in ('spring','summer','autumn','winter'):
    draft=GALLERY/'AirReview'/a.round/season/(season+'.draft.json')
    request('props-play',draft=draft)
    try:
        for _ in range(60):
            game=request('props-game-status',draft=draft)
            if game.get('ready'):break
            time.sleep(.5)
        else:raise RuntimeError('Game not ready: '+season)
        assert game['memoryOnly'] and game['identical'] and game['forbiddenComponents']==[],game
        request('authoring-place-witness')
        request('authoring-focus-game')
        output=QA/'Temp/ai/AircraftAirMotion'/a.round/season
        result=request('forest-motion',output=output)
        frames=result['frames'];assert len(frames)==32
        assert all(right['clock']>left['clock'] for left,right in zip(frames,frames[1:]))
        assert len({f['imageSha256'] for f in frames[:16]})>8
        assert len({tuple(f['cameraPosition']) for f in frames})==1
        low=frames[18:24];high=frames[26:32]
        assert all(f['qualityTier']==0 for f in low) and all(f['qualityTier']==2 for f in high)
        assert sum(f['visibleRegrowthTriangles'] for f in low)<=sum(f['visibleRegrowthTriangles'] for f in high)
        assert all(f['visibleShafts']<=3 for f in low)
        assert any(layer['visible']>0 for f in frames[:16] for layer in f['visibleLayers']), 'Atmospheric particles invisible'
        names={name:max(layer['visible'] for f in frames[:16] for layer in f['visibleLayers'] if layer['name']==name)
               for name in {layer['name'] for f in frames[:16] for layer in f['visibleLayers']}}
        moving=len({json.dumps(f['motions'],sort_keys=True) for f in frames[:16]})>1
        assert moving, 'Cloth does not react to the shared wind'
        for frame in frames:assert hashlib.sha256((output/frame['image']).read_bytes()).hexdigest()==frame['imageSha256']
        movie=MOVIES/(season+'.mp4')
        duration=frames[-1]['editorTime']-frames[0]['editorTime'];fps=31/max(.1,duration)
        subprocess.run([imageio_ffmpeg.get_ffmpeg_exe(),'-hide_banner','-loglevel','error','-y','-framerate',str(fps),'-i',str(output/'frame-%02d.png'),'-c:v','libx264','-preset','fast','-crf','19','-pix_fmt','yuv420p','-an','-movflags','+faststart',str(movie)],check=True)
        game=request('props-game-status',draft=draft);assert game['playingVoices']>0,game
        complete=request('authoring-complete',name='placed');assert all(complete[k] for k in ('solved','completed','memoryOnly','progressUnchanged','albumUnchanged','frozenArtMatches')),complete
        (OUTPUT/(season+'-motion.json')).write_text(json.dumps(dict(capture=result,visibleParticleLayers=names,clothMoves=True,
            video=movie.relative_to(GALLERY).as_posix(),videoSha256=hashlib.sha256(movie.read_bytes()).hexdigest(),
            videoHasAudio=False,play=game,completion=complete),ensure_ascii=False,indent=2),encoding='utf-8')
        print('Motion, quality, audio, solution passed:',season,flush=True)
    finally:
        request('authoring-stop')
        for _ in range(60):
            if not request('authoring-status').get('playing'):
                restored=request('authoring-workspace-status')
                if not restored.get('busy'):break
            time.sleep(.5)
        else:raise RuntimeError('Workspace not restored')
        assert restored['artSha256']==workspace['artSha256'],'User workspace changed'
after=snapshot(progress);assert after==before,'Persistent progress changed'
(OUTPUT/'motion-preservation.json').write_text(json.dumps(dict(unchanged=True,before=before,after=after,workspaceBefore=workspace,workspaceAfter=restored),ensure_ascii=False,indent=2),encoding='utf-8')
print('All seasons verified through the ordinary gameplay camera; progress and workspace preserved.')
