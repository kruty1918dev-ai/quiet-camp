"""Package the five exported authoring drafts and their reviewed native renders."""
import html
import json
from pathlib import Path
import shutil
import zipfile

ROOT=Path(__file__).resolve().parents[1]
BASE=ROOT/'Design/LevelAuthoring/WorldImprovement'
OUT=BASE/'Levels'
profiles=json.loads((ROOT/'QuietCamp/Assets/QuietCamp/Authoring/WorldStudies/profiles.json').read_text(encoding='utf-8'))
cards=[]
for profile in profiles:
 folder=OUT/profile['id'];folder.mkdir(parents=True,exist_ok=True)
 image=BASE/'Review/R07'/(profile['id']+'.png');shutil.copy2(image,folder/'render.png')
 shutil.copy2(image.with_suffix('.json'),folder/'capture.json')
 draft=next(folder.glob('*.draft.json'));d=json.loads(draft.read_text(encoding='utf-8'))
 cards.append(f'''<article><button class="picture" data-image="{profile['id']}/render.png" aria-label="Збільшити: {html.escape(profile['title'])}"><img src="{profile['id']}/render.png" alt="{html.escape(profile['title'])}"></button><div class="body"><h2>{html.escape(profile['title'])}</h2><p>Поле {d['level']['width']}×{d['level']['height']} · гостей {len(d['level']['guests'])} · декор {len(d['art']['instances'])}</p><a download href="{profile['id']}/{draft.name}">Чернетка для майстерні</a> · <a href="{profile['id']}/capture.json">Квитанція кадру</a></div></article>''')
text='''<!doctype html><html lang="uk"><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><title>QuietCamp — п’ять різних рівнів</title><style>body{margin:0;background:#f3f1e7;color:#283b2b;font:17px/1.6 system-ui,sans-serif}main{max-width:1180px;margin:auto;padding:36px 22px}h1{font-size:clamp(28px,4vw,46px);line-height:1.15}h2{font-size:21px;margin:0}a{color:#376747}header{max-width:800px;margin-bottom:30px}.grid{display:grid;grid-template-columns:repeat(auto-fit,minmax(min(100%,440px),1fr));gap:24px}article{border:1px solid #ccd3be;border-radius:14px;overflow:hidden;background:#fffdf6}.body{padding:18px}.body p{font-size:14px;color:#61705a;margin:8px 0}.picture{display:block;padding:0;border:0;width:100%;cursor:zoom-in;background:#dfe5ce}.picture img{display:block;width:100%;aspect-ratio:3/2;object-fit:cover}dialog{padding:8px;border:0;background:#172117;max-width:96vw;max-height:95vh}dialog::backdrop{background:#081308e0}dialog img{max-width:92vw;max-height:85vh;display:block}dialog button{font-size:16px;padding:8px 15px;cursor:pointer}footer{margin-top:28px}code{background:#e5eadb;padding:3px 7px;border-radius:5px}</style><main><header><p>QUIETCAMP · МАЙСТЕРНЯ РІВНІВ</p><h1>П’ять різних місць для табору</h1><p>Справжні Unity-рендери після семи циклів перегляду та правок. Кожен рівень — окрема редагована чернетка з перевіреним пазлом і зафіксованою геометрією.</p><p>У Unity: <strong>Quiet Camp → Майстерня рівнів → Новий → П’ять нових рівнів</strong>. Для точного відтворення кадру відкрийте завантажену чернетку кнопкою <strong>Відкрити</strong>. Кнопка <strong>Грати</strong> запускає тест із прогресом у пам’яті.</p><p>Вкладка <strong>Модель</strong>: виберіть декор, перегляньте масштаб/старіння/деталізацію, порівняйте з оригіналом і збережіть варіант. ProBuilder та Polybrush редагують робочу копію. Ctrl+Z — скасування.</p><a download href="../quietcamp-five-levels.zip">Завантажити всі п’ять рівнів</a></header><div class="grid">'''+''.join(cards)+'''</div><footer><a href="../REVIEW-UA.md">Журнал семи циклів</a> · <a href="../README-UA.md">Інструменти та перевірки</a> · <a href="../../index.html">Основна HTML-довідка</a></footer><dialog><button id="close">Закрити · Esc</button><img alt="Збільшений Unity-рендер"></dialog></main><script>const dialog=document.querySelector('dialog');document.querySelectorAll('[data-image]').forEach(b=>b.onclick=()=>{dialog.querySelector('img').src=b.dataset.image;dialog.showModal()});document.querySelector('#close').onclick=()=>dialog.close();dialog.onclick=e=>{if(e.target===dialog)dialog.close()}</script></html>'''
(OUT/'index.html').write_text(text,encoding='utf-8')
archive=BASE/'quietcamp-five-levels.zip'
with zipfile.ZipFile(archive,'w',compression=zipfile.ZIP_DEFLATED,compresslevel=6) as z:
 for f in OUT.rglob('*'):
  if f.is_file():z.write(f,Path('Levels')/f.relative_to(OUT))
 for name in ('README-UA.md','REVIEW-UA.md'):
  if (BASE/name).exists():z.write(BASE/name,name)
 for f in (BASE/'Verification').glob('*.json'):z.write(f,Path('Verification')/f.name)
print('Packaged',len(cards),'levels; ZIP bytes',archive.stat().st_size)
