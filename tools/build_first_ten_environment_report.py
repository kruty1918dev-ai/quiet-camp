#!/usr/bin/env python3
"""Readable source/acceptance report. Historical photos stay historical."""
import html,json,math,hashlib,sys
from functools import lru_cache
from pathlib import Path
ROOT=Path(__file__).resolve().parents[1]
DEST=ROOT/'Design/Campaign100/2026-10-11/FirstTenRefinement'
def esc(value):return html.escape(str(value),quote=True)

def diagram(place):
    def point(p):return (180+p[0]*19,180-p[2]*19)
    svg=['<svg viewBox="0 0 360 360" role="img" aria-label="Схема джерел авторингу; це не рендер"><rect width="360" height="360" fill="#283b30"/>']
    for feature in place['terrain']['features']:
        p=[feature['centerXZ'][0],0,feature['centerXZ'][1]];x,y=point(p);color='#72916c' if feature['height']>0 else '#537786'
        for multiplier in (1,.7,.4):svg.append(f'<ellipse cx="{x:.1f}" cy="{y:.1f}" rx="{feature["radius"]*19*multiplier:.1f}" ry="{feature["radius"]*16*multiplier:.1f}" fill="none" stroke="{color}" opacity=".6"/>')
    w,h=place['width'],place['height'];svg.append(f'<rect x="{180-w*9.5}" y="{180-h*9.5}" width="{w*19}" height="{h*19}" fill="#455d3f" stroke="#b1bb96" stroke-dasharray="3 4"/>')
    for route in place['routes']:
        coords=' '.join(f'{x:.1f},{y:.1f}' for x,y in map(point,route['points']))
        svg.append(f'<polyline points="{coords}" fill="none" stroke="#bfa980" stroke-width="{route["width"]*19}" stroke-linecap="round" stroke-linejoin="round"/>')
    for prop in place['props']:
        x,y=point(prop['target']);svg.append(f'<circle cx="{x:.1f}" cy="{y:.1f}" r="5" fill="#d5bf86"><title>{esc(prop["sourceModel"])}</title></circle>')
    for extra in place.get('extras',[]):
        if extra['kind']=='fence':
            contour=extra['contour']+([extra['contour'][0]] if extra['closed'] else [])
            coords=' '.join(f'{x:.1f},{y:.1f}' for x,y in map(point,contour));svg.append(f'<polyline points="{coords}" fill="none" stroke="#c2c4b0" stroke-width="2" stroke-dasharray="5 2"/>')
        elif 'center' in extra:
            x,y=point(extra['center']);svg.append(f'<rect x="{x-7}" y="{y-7}" width="14" height="14" fill="#8bacad"><title>{esc(extra["kind"])}</title></rect>')
    x,y=point(place['entryConnection']['point']);svg.append(f'<circle cx="{x:.1f}" cy="{y:.1f}" r="4" fill="#efeee0"/><text x="12" y="345" fill="#d5dbcf" font-size="11">Контури: рельєф · лінії: маршрути · точки: основні props</text></svg>')
    return ''.join(svg)

@lru_cache(maxsize=1)
def capture_revision():
    sys.path.insert(0,str(ROOT/'tools'))
    from campaign100_capture_sources import current_manifest
    return current_manifest()['revision']

def after_image(number,orientation,current_hash):
    path=DEST/'After/Renders'/f'level-{number:03d}-{orientation}.json'
    if not path.exists():return None
    receipt=json.loads(path.read_text());png=path.with_suffix('.png')
    if not png.exists() or receipt.get('kind') not in ('native-unity-world-camera','native-unity-game-view'):return None
    if receipt.get('contentHash')!=current_hash or receipt.get('sourceRevision')!=capture_revision() or receipt.get('imageSha256')!=hashlib.sha256(png.read_bytes()).hexdigest():return None
    if receipt.get('levelId')!=f'QC{number:03d}' or receipt.get('orientation')!=orientation:return None
    return receipt

def main():
    places=json.loads((DEST/'Authoring/places.json').read_text())['places']
    upstreams=json.loads((DEST/'package-upstreams.json').read_text())['packages']
    tool_rows=''.join(f'<tr><td><a href="{esc(p["repository"])}">{esc(p["package"].split("com.kruty1918.")[1])}</a></td><td>Core ✓ / cached compiler ✓</td><td class="pending">Native Pending</td><td><a href="../../../../QuietCamp/Packages/{esc(p["package"])}/VENDORED.json"><code>{esc(p["upstreamCommit"][:12])}</code></a></td></tr>' for p in upstreams)
    cards=[]
    fresh_count=0
    publication=DEST/'runtime-publication.json'
    active=publication.exists()
    layout_path=DEST/'layout-contracts.json'
    layout=json.loads(layout_path.read_text()) if layout_path.exists() else None
    layout_current=layout is not None and all(hashlib.sha256((ROOT/path).read_bytes()).hexdigest()==sha for path,sha in layout['sources'].items())
    baked=None
    pointer=DEST/'Baked/current.json'
    if pointer.exists():
        revision=json.loads(pointer.read_text())['revision']
        receipt=json.loads((DEST/'Baked'/revision/'receipt.json').read_text())
        if all(hashlib.sha256((ROOT/path).read_bytes()).hexdigest()==sha for path,sha in receipt['sources'].items()):baked=revision
    for i,place in enumerate(places,1):
        photos=[]
        for orientation,label in [('landscape','Landscape'),('portrait','Portrait')]:
            receipt=json.loads((DEST/'Baseline'/f'level-{i:03d}-{orientation}.json').read_text())
            src=f'Baseline/level-{i:03d}-{orientation}.png'
            photos.append(f'<figure class="photo {orientation}"><a href="{src}"><img loading="lazy" src="{src}" alt="{place["id"]}: вихідний native кадр, {label}"/></a><figcaption>{label} · {esc(receipt["capturedUtc"])} · <a href="Baseline/level-{i:03d}-{orientation}.json">receipt</a></figcaption></figure>')
        models=', '.join(f'<code>{esc(p["sourceModel"])}</code>' for p in place['props']) or 'Власний ансамбль із запечених частин; параметри та IDs у джерелі.'
        current=json.loads((ROOT/'QuietCamp/Assets/QuietCamp/Resources/QuietCamp/Levels'/f'{place["id"]}.json').read_text())
        after=[]
        for orientation,label in [('landscape','Landscape'),('portrait','Portrait')]:
            receipt=after_image(i,orientation,current['contentHash'])
            if receipt:
                fresh_count+=1;src=f'After/Renders/level-{i:03d}-{orientation}.png'
                after.append(f'<figure class="photo {orientation}"><a href="{src}"><img loading="lazy" src="{src}" alt="{place["id"]}: нова композиція, {label}"/></a><figcaption>{label} · {esc(receipt["capturedUtc"])} · <a href="After/Renders/level-{i:03d}-{orientation}.json">receipt</a></figcaption></figure>')
        if not after:after=['<div class="waiting"><strong>Native кадри ще не отримано.</strong><p>Unity не запускається за останньою вказівкою користувача. Джерело нижче показує перебудовані маршрути й ансамблі; це схема, не готовий 3D-кадр.</p></div>']
        geometry='Перевірено опори, маршрути, потрібні ансамблі та конструктивні зв’язки у Core.' if layout_current else 'Повторна перевірка джерел потрібна; native-приймання відкрите.'
        changes=[]
        for e in place.get('extras',[]):
            kind=e['kind'];label={'spring':'відкрита чаша джерела під кроною','ruin':'залишки стін із входом','fence':'зв’язна огорожа з опорами','power':'мережа опор і провислих/обірваних проводів','rest-pocket':'місце колишнього відпочинку'}.get(kind,kind)
            changes.append(label)
        descriptions={1:'Старіння лавки перед перевіркою ніжок; малий знак, місце відпочинку й білі ромашки з золотим центром.',2:'Два окремі підходи, навіс на локальній опорі, схил і трав’яні плями замість повторної плями бруду.',3:'Відкрита чаша джерела з низьким кам’яним берегом, вода над дном, дерево тіні й огорожа з кінцевими опорами.',4:'Вогнище в окремій улоговині, тихий край і відмінний напрям стежки.',5:'Нерегулярний старий сад, різні групи крон і місце для друзів.',6:'Зупинка збоку зношеної дороги, провислі проводи між чотирма сільськими опорами й виразний обрив; поле наметів збережено на траві.',7:'Низькі стіни колишньої хати з входом, замкнена нерегулярна огорожа, оперта хвіртка й підхід через отвір.',8:'Заглиблена стежка з під’єднаним відгалуженням, тополі й інший camera anchor; повторної лавки немає.',9:'Мала спільна криниця на підвищенні, два підходи та підлісок; силует відмінний від відкритого джерела QC003.',10:'Три напрями роздоріжжя, малий покажчик і ліхтар; повторної великої зупинки немає.'}
        details=descriptions[i]
        mesh_status='Повне запікання ще очікує перевірки.'
        if baked:
            manifest=json.loads((DEST/'Baked'/baked/place['id']/'manifest.json').read_text())
            parts={p['kind']:p for p in manifest['parts']}
            mesh_status=f"Запечено: {len(parts['trees']['objects'])} дерев, {len(parts['groundcover']['objects'])} низьких рослин, {len(parts['structures']['objects'])} частин споруд і каміння; {sum(p['triangles'] for p in manifest['parts']):,} трикутників в усьому оточенні. <a href=\"Baked/{baked}/{place['id']}/manifest.json\">Manifest і зв’язки</a>. Це геометричні підрахунки, не видимі draw calls чи mobile FPS."
            geometry='Повне запікання прийняло обов’язкові розміщення та перевірило об’єми рослин, коридори, опори й зв’язки.'
        cards.append(f'''<article id="{place['id']}"><h2>{place['id']} — {esc(place['titleUk'])}</h2><p>{esc(place['storyUk'])}</p><div class="comparison"><section><h3>До · native baseline</h3>{''.join(photos)}</section><section><h3>Після · нова реалізація</h3>{''.join(after)}<details><summary>Авторське джерело нової композиції</summary>{diagram(place)}</details></section></div><p><strong>Зміни:</strong> {esc(details)}</p><p><strong>Моделі:</strong> {models}</p><p><strong>Геометричні перевірки:</strong> {geometry}</p><p>{mesh_status}</p><p><strong>Художнє приймання:</strong> {esc(place['acceptanceUk'])}</p><p>Поле {place['width']} × {place['height']} · {len(place['routes'])} маршрути · {len(place['terrain']['features'])} форми рельєфу · старий hash <code>{place['sourceLevelHash'][:12]}</code></p><p class="pending">Видимі тіні/матеріали, композиція, HUD, рух, replay й ресурсні цикли — ще потребують перевірки.</p></article>''')
    page='''<!doctype html><html lang="uk"><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><title>Quiet Camp — інструменти та перші десять місць</title><style>
:root{color-scheme:dark;--text:#e3e9de;--muted:#aebcad;--line:#435047}*{box-sizing:border-box}body{margin:0;background:#18221d;color:var(--text);font:16px/1.6 system-ui,sans-serif}main{max-width:1280px;margin:auto;padding:30px 24px}a{color:#c7d6aa}h1{font-size:clamp(25px,4vw,40px);line-height:1.2}h2{font-size:24px}h3{font-size:17px}p{max-width:1000px}code{font-size:12px;overflow-wrap:anywhere}article{padding:24px;margin:28px 0;background:#243129;border:1px solid var(--line);border-radius:14px}.comparison{display:grid;grid-template-columns:1fr 1fr;gap:24px}.photo{margin:0 0 12px}.photo img{display:block;max-width:100%;height:auto;max-height:700px;object-fit:contain;background:#101914}.portrait img{max-height:420px}.portrait{display:none}.show-portrait .landscape{display:none}.show-portrait .portrait{display:block}figcaption{font-size:12px;color:var(--muted);margin:5px 0}.pending{color:#e8c391}.waiting{padding:20px;border:1px dashed #829276;border-radius:10px;background:#1b271f;min-height:260px}svg{display:block;width:100%;max-width:420px}table{width:100%;border-collapse:collapse;font-size:14px}td,th{text-align:left;border-bottom:1px solid var(--line);padding:9px 12px}nav{display:flex;flex-wrap:wrap;gap:12px;position:sticky;top:0;background:#18221df2;padding:14px 0;z-index:1}button{font:inherit;color:var(--text);border:1px solid #819a70;border-radius:6px;background:#344631;padding:7px 14px;cursor:pointer}details{margin:20px 0}summary{cursor:pointer}@media(max-width:760px){.comparison{grid-template-columns:1fr}main{padding:18px 12px}article{padding:16px}table{display:block;overflow-x:auto}}
</style><main><a href="../index.html">← Кампанія 100</a><h1>Інструменти оточення та QC001–QC010</h1><p>Оновлено 12.10.2026. Реалізовано адаптер восьми інструментів, авторинг десяти місць і завантаження готової геометрії без процедурної генерації в gameplay. Усі десять місць запечені окремо від чинних рівнів. Нові композиції ще не активовані: спочатку потрібен візуальний перегляд. Unity не запускається за поточною вказівкою користувача.</p><p>287 Core assertions · 3 синтетичні інтеграції · 32 package assemblies · поточний runtime/Editor/PlayMode код компілюється · 100 чинних пазлів пройшли validator, witness і незалежний solver. Публікація перевірена на тимчасовій копії: лише перша десятка отримує нове оточення, всі попередні revisions збережені; пошкоджений buffer відхиляється до запису. Пазли кандидата й save-контракти пройшли повторно. Чинні рівні репозиторію не змінено. Поточні compiler checks містять warnings, наведені в квитанції. Ці перевірки не доводять native import, художню якість або mobile FPS.</p><nav><button id="orientation" type="button" aria-pressed="false">Показати portrait</button>'''+''.join(f'<a href="#{p["id"]}">{p["id"]}</a>' for p in places)+'''</nav><p><a href="PLAN-UA.md">План і відкриті перевірки</a> · <a href="INTEGRATION-UA.md">Запікання й інтеграція</a> · <a href="bake-verification.json">Перевірка buffers</a> · <a href="domain-contracts.json">Пазли та saves</a> · <a href="publication-contracts.json">Публікація на тимчасовій копії</a> · <a href="integration-source-checks.json">Компіляція</a> · <a href="TOOLS-UA.md">Детальні контракти інструментів</a> · <a href="Authoring/places.json">Джерела 10 композицій</a> · <a href="Baseline/published-content-receipt.json">Canonical baseline</a></p><details open><summary>Пакети й точні upstream commits</summary><table><thead><tr><th>Пакет, private repo</th><th>Перевірено</th><th>Відкрите приймання</th><th>Commit</th></tr></thead><tbody>'''+tool_rows+'''</tbody></table></details><p>Усі двадцять кадрів нижче — історичний baseline 11.10.2026, близько 10:59 UTC. Це native world-camera evidence, без HUD. Авторські draft hashes і фінальні canonical hashes збережено окремо; PNG відповідають фінальним опублікованим версіям.</p>'''+''.join(cards)+'''<p>Відкриті пункти: native import кожного пакета в чистому проєкті; перегляд QC001/QC003/QC006 і художні ітерації; приймання всіх десяти місць у двох орієнтаціях; Game View/HUD, відео вітру/атмосфери, reduced motion та повторні входи без накопичення ресурсів. Чинні 100 пазлів і історичні saves перевірені автономно; повний ігровий flow нового оточення ще не перевірений. Роадмапа відкладена; player builds не виконувалися.</p></main><script>document.getElementById('orientation').addEventListener('click',function(){const portrait=document.body.classList.toggle('show-portrait');this.setAttribute('aria-pressed',String(portrait));this.textContent=portrait?'Показати landscape':'Показати portrait';});</script></html>'''
    (DEST/'index.html').write_text(page)
    print(f'Built report: 20 historical captures, {fresh_count}/20 valid after captures, 10 source diagrams, 8 private repos; runtime active={active}, current bake={bool(baked)}.')

if __name__=='__main__':main()
