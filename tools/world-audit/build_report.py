"""Build an offline, read-only Ukrainian evidence gallery. Never publish a Site.
Requires completed render metadata and explicit visual notes before final delivery.
"""
import collections,csv,hashlib,html,json,shutil,sys
from pathlib import Path
from PIL import Image
root=Path(sys.argv[1]);partial='--partial' in sys.argv;output=root/'gallery';output.mkdir(exist_ok=True);media=output/'media';media.mkdir(exist_ok=True);data=output/'data';data.mkdir(exist_ok=True)
notes=json.loads((root/'review-notes.json').read_text()) if (root/'review-notes.json').exists() else {};labels=json.loads((root/'analysis/labels.json').read_text());summaries=[];image_manifest={}
def esc(x):return html.escape(str(x))
def label(r):
 source=r.get('sourceModelId') or r.get('assetId') or r.get('modelId') or ''
 if source in labels:return labels[source]
 if r.get('state')=='solved':return 'Намет гостя · еталонне розміщення'
 role=r.get('seasonalRole');return {'deciduous':'Листяне дерево','conifer':'Хвойне дерево','flower':'Квіти','groundcover':'Трав’яний покрив'}.get(role,source or 'Ігровий об’єкт')
css='''*{box-sizing:border-box}html{color-scheme:light}body{margin:0;background:#eeeee7;color:#1f2b25;font:16px/1.6 system-ui,sans-serif}header,main,footer{max-width:1240px;margin:auto;padding:24px}header{border-bottom:1px solid #b3b9ad}h1{font-size:clamp(26px,4vw,44px);line-height:1.15;letter-spacing:-.03em}h2{font-size:24px;margin-top:2em}h3{font-size:18px}a{color:#315c43;text-underline-offset:3px}a:focus-visible,summary:focus-visible{outline:3px solid #a94e20;outline-offset:4px}.note{border-left:4px solid #9e7043;background:#fffdf4;padding:16px 20px;max-width:90ch}.tag{display:inline-block;background:#dce3d6;border-radius:3px;padding:2px 7px;font-size:13px}.grid{display:grid;grid-template-columns:repeat(auto-fill,minmax(260px,1fr));gap:18px}.card{background:#fbfaf2;border:1px solid #bac2b6;border-radius:6px;overflow:hidden}.card p,.card h2{padding:0 15px;margin:12px 0}.card h2{font-size:19px}.card img{width:100%;height:auto;display:block}.views{display:grid;grid-template-columns:repeat(2,minmax(0,1fr));gap:18px}figure{margin:0;background:#fbfaf2;padding:8px;border:1px solid #bcc4b9}figure img{width:100%;height:auto;display:block}figure.portrait img{max-height:720px;width:auto;max-width:100%;margin:auto}figcaption{padding:8px 3px;font-size:14px}.table{overflow:auto}table{border-collapse:collapse;width:100%;font-size:14px;background:#fbfaf2}th,td{padding:8px 10px;border-bottom:1px solid #d0d4c7;text-align:left;vertical-align:top}th{background:#dfe5d7}td code{overflow-wrap:anywhere;font-size:12px}details{margin:20px 0}summary{padding:12px;background:#e0e5da;cursor:pointer;min-height:48px}nav{display:flex;flex-wrap:wrap;gap:18px}footer{color:#465348;font-size:14px}code{overflow-wrap:anywhere}@media(max-width:700px){header,main,footer{padding:18px}.views{grid-template-columns:1fr}table{font-size:13px}h1{font-size:30px}}@media(prefers-reduced-motion:reduce){*{scroll-behavior:auto!important}}'''
(output/'style.css').write_text(css)
def page(title,body):return '<!doctype html><html lang="uk"><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><title>'+esc(title)+' · Quiet Camp</title><link rel="stylesheet" href="style.css"><header><nav><a href="index.html">Quiet Camp · усі рівні</a><a href="REPORT.md">Повний звіт</a><a href="data/level-summary.csv">Таблиця CSV</a></nav></header><main>'+body+'</main><footer>21 жовтня 2026 · Blender 4.3.2 / Workbench · Unity, player builds і публікація не запускалися.</footer></html>'
def image_file(folder,filename,prefix):
 source=folder/filename;name=prefix+'-'+Path(filename).stem+'.webp';target=media/name
 if not target.exists() or source.stat().st_mtime>target.stat().st_mtime:
  with Image.open(source) as im:im.convert('RGB').save(target,'WEBP',quality=84,method=5)
 image_manifest[str(target.relative_to(output))]={'sourcePng':str(source.relative_to(root)),'sourceSha256':hashlib.sha256(source.read_bytes()).hexdigest(),'webpSha256':hashlib.sha256(target.read_bytes()).hexdigest()}
 return 'media/'+name
def figure(folder,frame,prefix):
 href=image_file(folder,frame['path'],prefix);caption={'landscape':'Ландшафтний формат · напрямок камери гравця','portrait':'Портретний формат · напрямок камери гравця','saved-reference-layout':'Еталонне розміщення справжніх наметів','diagnostic-full-landscape':'Діагностика всього оточення · інше збільшення','diagnostic-detail-same-direction':'Деталь · той самий напрямок, інша точка огляду'}.get(frame.get('state'),frame.get('profile') or frame.get('state') or '')
 if '-landscape.png' in frame['path']:caption='Ландшафтний формат · напрямок камери гравця'
 if '-portrait.png' in frame['path']:caption='Портретний формат · напрямок камери гравця'
 return '<figure'+(' class="portrait"' if '-portrait.png' in frame['path'] else '')+'><a href="'+href+'"><img loading="lazy" src="'+href+'" alt="'+esc(caption)+' · рівень '+str(int(frame['path'][:3]))+'"></a><figcaption>'+esc(caption)+'</figcaption></figure>'
for order in range(1,101):
 path=root/'review-v2'/f'{order:03}-inventory.json'
 if not path.exists():
  if partial:continue
  raise SystemExit('Missing final render '+str(order))
 d=json.loads(path.read_text());facts=json.loads((root/'analysis'/f'{order:03}-facts.json').read_text());review=notes.get(str(order));
 if not review and not partial:raise SystemExit('Missing explicit visual notes for '+str(order))
 review=review or {'status':'not-yet-visually-reviewed','notes':'Візуальний огляд цього кадру ще не завершено.'}
 primary=next((r for r in d['objects'] if r.get('id')==facts.get('primaryInstanceId')),None);title=label(primary) if primary else d['id'];groups=collections.defaultdict(lambda:[0,0,0]);rows=[];visible=[]
 for r in d['objects']:
  pixels=r.get('visiblePixels',{});land=pixels.get('landscape',0);portrait=pixels.get('portrait',0);kind=r.get('sourceModelId') or r.get('assetId') or r.get('modelId') or 'unresolved';groups[(label(r),kind)][0]+=1;groups[(label(r),kind)][1]+=int(land>0);groups[(label(r),kind)][2]+=int(portrait>0)
  row={'id':r['id'],'name':label(r),'sourceModelId':kind,'source':r.get('source','authored'),'role':r.get('seasonalRole'),'x':r.get('position',[None]*3)[0],'y':r.get('position',[None]*3)[1],'z':r.get('position',[None]*3)[2],'rendered':r.get('rendered'),'reason':r.get('reason'),'landscapePixels':land,'portraitPixels':portrait,'state':r.get('state','baseline')};rows.append(row)
  if land or portrait:visible.append(row)
 with (data/f'{order:03}-all-instances.csv').open('w',newline='',encoding='utf-8-sig') as f:
  writer=csv.DictWriter(f,fieldnames=list(rows[0]));writer.writeheader();writer.writerows(rows)
 (data/f'{order:03}-facts.json').write_text(json.dumps(facts,ensure_ascii=False,indent=2)+'\n');(data/f'{order:03}-review.json').write_text(json.dumps(review,ensure_ascii=False,indent=2)+'\n')
 shadow=sum(s['substantialCasterFraction'] for s in d.get('shade',[]))/len(d['shade']) if d.get('shade') else None
 summary={'order':order,'id':d['id'],'primary':title,'authoredInstances':facts['authoredInstances'],'visibleLandscape':sum(r['landscapePixels']>0 for r in rows),'visiblePortrait':sum(r['portraitPixels']>0 for r in rows),'shadeCoverageFraction':shadow,'reviewStatus':review['status'],'notes':review['notes']};summaries.append(summary)
 body=f'<p class="tag">Рівень {order:03} · {esc(facts.get("season"))}</p><h1>{esc(title)}</h1><p><code>{esc(d["id"])}</code></p><p class="note">{esc(review["notes"])}</p>'
 body+='<p>Це зовнішній геометричний огляд. Без HUD; розмір зарезервованої ділянки UI приблизний. URP, перебудова річки, процедурний луг, вогонь, рух тканини та предмети мешканців не відтворені повністю. Ці кадри не є нативним художнім прийманням.</p><div class="views">'+''.join(figure(root/'review-v2',fr,'before') for fr in d['frames'])+'</div>'
 if d.get('shade'):
  body+='<h2>Фізична тінь у клітинках правила</h2><p>По дев’ять променів до справжнього сонця на клітинку. Враховано дерева й тверді об’єкти заввишки від 0,8 м; травинка не зараховується як затишний прихисток.</p><div class="table"><table><tr><th>Клітинка</th><th>Затінені проби</th><th>Центр</th></tr>'
  body+=''.join(f'<tr><td>{s["cell"]}</td><td>{round(s["substantialCasterFraction"]*9)}/9</td><td>{esc(s.get("caster") or "немає фізичного затінювача")}</td></tr>' for s in d['shade'])+'</table></div>'
 for folder,heading,prefix in [('contact-renders','Кандидат: криниці / мости','contact'),('candidate-renders','Дослідний навіс: НЕ художнє приймання','candidate')]:
  record=root/folder/f'{order:03}-inventory.json'
  if record.exists():
   after=json.loads(record.read_text());body+='<h2>'+heading+'</h2><p>Окрема пропозиція WorldOps. Авторські JSON у Resources не опубліковано. Новий навіс може перекривати головний об’єкт: див. висновок.</p><div class="views">'+''.join(figure(root/folder,fr,prefix) for fr in after['frames'] if not fr['path'].endswith('-portrait.png'))+'</div>'
 body+='<h2>Об’єкти й видимість</h2><p>Числа у форматах — кількість екземплярів із хоча б одним видимим пікселем у растровій ID-пробі. CSV містить також невидимі й сезонно прибрані екземпляри. Наметам еталонного стану не приписана виміряна видимість початкового кадру.</p><p><a href="data/'+f'{order:03}-all-instances.csv'+'">Усі екземпляри: координати, роль, джерело, видимість · CSV</a> · <a href="data/'+f'{order:03}-facts.json'+'">Географія та контакт · JSON</a></p><div class="table"><table><tr><th>Об’єкт</th><th>Усього</th><th>Ландшафт</th><th>Портрет</th></tr>'
 body+=''.join(f'<tr><td>{esc(name)}<br><code>{esc(kind)}</code></td><td>{counts[0]}</td><td>{counts[1]}</td><td>{counts[2]}</td></tr>' for (name,kind),counts in sorted(groups.items()))+'</table></div>'
 body+='<details><summary>Кожен видимий екземпляр ('+str(len(visible))+')</summary><div class="table"><table><tr><th>ID / назва</th><th>Положення X Y Z</th><th>Пікселі L / P</th></tr>'+''.join(f'<tr><td>{esc(r["name"])}<br><code>{esc(r["id"])}</code></td><td>{r["x"]:.3f}, {r["y"]:.3f}, {r["z"]:.3f}</td><td>{r["landscapePixels"]} / {r["portraitPixels"]}</td></tr>' for r in visible)+'</table></div></details>'
 body+='<h2>Походження доказів</h2><p>Source art SHA-256: <code>'+esc(d['source']['artSha256'])+'</code><br>Ресурси: <code>'+esc(d['source']['artPath'])+'</code><br>Renderer SHA-256: <code>'+esc(d['rendererSourceSha256'])+'</code></p><nav>'+(f'<a href="level-{order-1:03}.html">← Попередній</a>' if order>1 else '')+(f'<a href="level-{order+1:03}.html">Наступний →</a>' if order<100 else '')+'</nav>'
 (output/f'level-{order:03}.html').write_text(page(f'{order:03} · {title}',body))
 body=None
if summaries:
 with (data/'level-summary.csv').open('w',newline='',encoding='utf-8-sig') as f:
  writer=csv.DictWriter(f,fieldnames=list(summaries[0]));writer.writeheader();writer.writerows(summaries)
body='<p class="tag">WorldOps · зовнішній огляд · '+str(len(summaries))+'/100</p><h1>Quiet Camp<br>Композиція кожного табору</h1><p class="note">Справжня геометрія з main 030e76d. Blender без запуску Unity та без player build. Кадри показують геометрію й контакти; шейдери, HUD та частина процедурних систем мають окремі обмеження. Позитивний числовий тест не є художнім прийманням.</p><p>Відкрий рівень: портрет, ландшафт, намети, усе оточення, деталі, склад об’єктів і затінення.</p><div class="grid">'
for s in summaries:
 thumb=image_file(root/'review-v2',f'{s["order"]:03}-landscape.png','before');body+=f'<article class="card"><a href="level-{s["order"]:03}.html"><img loading="lazy" src="{thumb}" alt="Рівень {s["order"]:03}"><h2>{s["order"]:03} · {esc(s["primary"])}</h2></a><p>{esc(s["notes"])}</p></article>'
body+='</div>';(output/'index.html').write_text(page('Огляд 100 композицій',body));(output/'image-manifest.json').write_text(json.dumps(image_manifest,ensure_ascii=False,indent=2)+'\n');(output/'data/review-summary.json').write_text(json.dumps(summaries,ensure_ascii=False,indent=2)+'\n')
print('GALLERY',len(summaries),'levels',len(image_manifest),'derived images; final=',not partial)
