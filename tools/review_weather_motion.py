"""Review real Unity playback sequences and encode offline clips without altering frames."""
import argparse
import hashlib
import html
import json
from pathlib import Path
import subprocess
import imageio_ffmpeg
from playwright.sync_api import sync_playwright
ROOT=Path(__file__).resolve().parents[1]
BASE=ROOT/'Design/LevelAuthoring/SeasonAtmosphere/VerificationV2/Motion'
OUTPUT=Path(r'C:/Users/Public/qcui-20261006/QuietCamp/Temp/ai/WeatherMotionSheets')
OUTPUT.mkdir(parents=True,exist_ok=True)
p=argparse.ArgumentParser(description=__doc__);p.add_argument('--only');a=p.parse_args()
proofs=[]
with sync_playwright() as engine:
 browser=engine.chromium.launch(executable_path=r'C:/Program Files (x86)/Microsoft/Edge/Application/msedge.exe',headless=True)
 for folder in sorted(BASE.iterdir()) if BASE.exists() else []:
  if a.only and folder.name!=a.only:continue
  receipt=folder/'motion.json'
  if not receipt.exists():continue
  d=json.loads(receipt.read_text(encoding='utf-8'));assert d['status']=='weather-motion-captured',d
  frames=d['frames'];assert len(frames)==24
  assert frames[-1]['clock']>frames[0]['clock']+.5,'Weather did not advance'
  for f in frames:assert hashlib.sha256((folder/f['image']).read_bytes()).hexdigest()==f['imageSha256']
  for stage in ('stationary','pan','zoom'):
   selected=[f for f in frames if f['stage']==stage]
   assert any(sum(layer['visible'] for layer in f['visibleLayers'] if any(n in layer['name'] for n in ('Дощ','дощ','Сніг','сніг','Листя'))) for f in selected),stage+' has no visible precipitation or leaves'
  assert frames[0]['cameraPosition']!=frames[8]['cameraPosition']
  assert frames[16]['orthographicSize']>frames[0]['orthographicSize']
  for index,motion in enumerate(frames[0]['motions']):
   poses=[(tuple(f['motions'][index]['position']),tuple(f['motions'][index]['rotation'])) for f in frames]
   assert len(set(poses))>1,'Static motion: '+motion['name']
  # Preserve measured time intervals; no fabricated interpolation or simulated particles.
  concat=OUTPUT/(folder.name+'-concat.txt')
  lines=[]
  for index,f in enumerate(frames):
   lines.append("file '"+(folder/f['image']).as_posix()+"'")
   if index<len(frames)-1:lines.append('duration '+str(frames[index+1]['editorTime']-f['editorTime']))
  lines.append("file '"+(folder/frames[-1]['image']).as_posix()+"'")
  concat.write_text('\n'.join(lines)+'\n',encoding='utf-8')
  result=subprocess.run([imageio_ffmpeg.get_ffmpeg_exe(),'-y','-loglevel','error','-f','concat','-safe','0','-i',str(concat),'-fps_mode','vfr','-c:v','libx264','-crf','23','-pix_fmt','yuv420p','-movflags','+faststart',str(folder/'motion.mp4')],capture_output=True,text=True)
  assert result.returncode==0,result.stderr
  cards=''
  for index in (0,7,8,15,16,23):
   f=frames[index];cards+=f'<article><div>{index:02} · {html.escape(f["stage"])} · clock {f["clock"]:.2f} · particles {f["particles"]}</div><img src="{(folder/f["image"]).as_uri()}"></article>'
  page=browser.new_page(viewport={'width':1536,'height':1200},device_scale_factor=1)
  document=OUTPUT/(folder.name+'.html');document.write_text('<meta charset="utf-8"><style>body{margin:0;background:#ddd;font:15px sans-serif}.grid{display:grid;grid-template-columns:repeat(3,512px)}article div{height:35px;padding:8px}img{width:512px;height:550px;display:block;object-fit:contain;background:#202622}</style><main class="grid">'+cards+'</main>',encoding='utf-8')
  page.goto(document.as_uri());page.wait_for_function('Array.from(document.images).every(i=>i.complete&&i.naturalWidth>0)');page.screenshot(path=str(OUTPUT/(folder.name+'.png')));page.close()
  proofs.append({'preset':folder.name,'frames':24,'weatherAdvanced':True,'visibleAtAllCameraStages':True,'wheelAndBoatMoved':True,'videoSha256':hashlib.sha256((folder/'motion.mp4').read_bytes()).hexdigest(),'nativeSequenceSeconds':frames[-1]['editorTime']-frames[0]['editorTime']})
 browser.close()
(BASE/'verification.json').write_text(json.dumps(proofs,indent=2),encoding='utf-8')
print('Verified and encoded',len(proofs),'actual Unity sequences; images remain unchanged')
