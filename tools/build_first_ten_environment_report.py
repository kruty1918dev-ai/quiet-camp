#!/usr/bin/env python3
"""Readable source/acceptance report. Historical photos stay historical."""
import html,json,math
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
    x,y=point(place['entryConnection']['point']);svg.append(f'<circle cx="{x:.1f}" cy="{y:.1f}" r="4" fill="#efeee0"/><text x="12" y="345" fill="#d5dbcf" font-size="11">Контури: рельєф · лінії: маршрути · точки: основні props</text></svg>')
    return ''.join(svg)

def main():
    places=json.loads((DEST/'Authoring/places.json').read_text())['places']
    upstreams=json.loads((DEST/'package-upstreams.json').read_text())['packages']
    tool_rows=''.join(f'<tr><td><a href="{esc(p["repository"])}">{esc(p["package"].split("com.kruty1918.")[1])}</a></td><td>Core ✓ / cached compiler ✓</td><td class="pending">Native Pending</td><td><a href="../../../../QuietCamp/Packages/{esc(p["package"])}/VENDORED.json"><code>{esc(p["upstreamCommit"][:12])}</code></a></td></tr>' for p in upstreams)
    cards=[]
    for i,place in enumerate(places,1):
        photos=[]
        for orientation,label in [('landscape','Landscape'),('portrait','Portrait')]:
            receipt=json.loads((DEST/'Baseline'/f'level-{i:03d}-{orientation}.json').read_text())
            src=f'Baseline/level-{i:03d}-{orientation}.png'
            photos.append(f'<figure class="photo {orientation}"><a href="{src}"><img loading="lazy" src="{src}" alt="{place["id"]}: вихідний native кадр, {label}"/></a><figcaption>{label} · {esc(receipt["capturedUtc"])} · <a href="Baseline/level-{i:03d}-{orientation}.json">receipt</a></figcaption></figure>')
        models=', '.join(f'<code>{esc(p["sourceModel"])}</code>' for p in place['props'])
        cards.append(f'''<article id="{place['id']}"><h2>{place['id']} — {esc(place['titleUk'])}</h2><p>{esc(place['storyUk'])}</p><div class="comparison"><section><h3>До: збережені native кадри</h3>{''.join(photos)}</section><section><h3>Джерело нової композиції</h3>{diagram(place)}<p class="pending">Схема авторингу, не рендер. Native bake і кадри «після» ще Pending.</p></section></div><p><strong>Основні моделі:</strong> {models}.</p><p><strong>Приймання:</strong> {esc(place['acceptanceUk'])}</p><p>Поле {place['width']} × {place['height']} · {len(place['routes'])} маршрути · {len(place['terrain']['features'])} форми рельєфу · canonical baseline <code>{place['sourceLevelHash'][:12]}</code></p><p class="pending">Пазл/тіні/опора/перетини/матеріали/рух/HUD/replay — native та gameplay приймання Pending.</p></article>''')
    page='''<!doctype html><html lang="uk"><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><title>Quiet Camp — інструменти та перші десять місць</title><style>
:root{color-scheme:dark;--text:#e3e9de;--muted:#aebcad;--line:#435047}*{box-sizing:border-box}body{margin:0;background:#18221d;color:var(--text);font:16px/1.6 system-ui,sans-serif}main{max-width:1280px;margin:auto;padding:30px 24px}a{color:#c7d6aa}h1{font-size:clamp(25px,4vw,40px);line-height:1.2}h2{font-size:24px}h3{font-size:17px}p{max-width:1000px}code{font-size:12px;overflow-wrap:anywhere}article{padding:24px;margin:28px 0;background:#243129;border:1px solid var(--line);border-radius:14px}.comparison{display:grid;grid-template-columns:1.3fr 1fr;gap:24px}.photo{margin:0 0 12px}.photo img{display:block;max-width:100%;height:auto;max-height:700px;object-fit:contain;background:#101914}.portrait img{max-height:420px}.portrait{display:none}.show-portrait .landscape{display:none}.show-portrait .portrait{display:block}figcaption{font-size:12px;color:var(--muted);margin:5px 0}.pending{color:#e8c391}svg{display:block;width:100%;max-width:420px}table{width:100%;border-collapse:collapse;font-size:14px}td,th{text-align:left;border-bottom:1px solid var(--line);padding:9px 12px}nav{display:flex;flex-wrap:wrap;gap:12px;position:sticky;top:0;background:#18221df2;padding:14px 0;z-index:1}button{font:inherit;color:var(--text);border:1px solid #819a70;border-radius:6px;background:#344631;padding:7px 14px;cursor:pointer}details{margin:20px 0}summary{cursor:pointer}@media(max-width:760px){.comparison{grid-template-columns:1fr}main{padding:18px 12px}article{padding:16px}table{display:block;overflow-x:auto}}
</style><main><a href="../index.html">← Кампанія 100</a><h1>Інструменти оточення та QC001–QC010</h1><p>Підготовка без Unity. Вісім приватних пакетів опубліковано; Core і compiler checks пройшли. Gameplay першої десятки ще не замінено. Нових native кадрів «після» немає.</p><p>287 Core assertions · 3 синтетичні інтеграції · 32 cached-reference assemblies без warnings. Ці перевірки не доводять native import, художню якість або mobile FPS.</p><nav><button id="orientation" type="button" aria-pressed="false">Показати portrait</button>'''+''.join(f'<a href="#{p["id"]}">{p["id"]}</a>' for p in places)+'''</nav><p><a href="PLAN-UA.md">План і відкриті перевірки</a> · <a href="TOOLS-UA.md">Детальні контракти інструментів</a> · <a href="Authoring/places.json">Джерела 10 композицій</a> · <a href="Baseline/published-content-receipt.json">Canonical baseline</a></p><details open><summary>Пакети й точні upstream commits</summary><table><thead><tr><th>Пакет, private repo</th><th>Перевірено</th><th>Відкрите приймання</th><th>Commit</th></tr></thead><tbody>'''+tool_rows+'''</tbody></table></details><p>Усі двадцять кадрів нижче — історичний baseline 11.10.2026, близько 10:59 UTC. Це native world-camera evidence, без HUD. Авторські draft hashes і фінальні canonical hashes збережено окремо; PNG відповідають фінальним опублікованим версіям.</p>'''+''.join(cards)+'''<p>Завершення вимагає native import у чистих проєктах, bake QC001/QC003/QC006, художніх ітерацій, решти семи місць, validator/witness/solver, старих saves, Game View/HUD, reduced motion та перевірок ресурсів. Роадмапа відкладена; player builds не виконувалися.</p></main><script>document.getElementById('orientation').addEventListener('click',function(){const portrait=document.body.classList.toggle('show-portrait');this.setAttribute('aria-pressed',String(portrait));this.textContent=portrait?'Показати landscape':'Показати portrait';});</script></html>'''
    (DEST/'index.html').write_text(page)
    print('Built source/acceptance report: 20 historical captures, 10 source diagrams, 8 private repos; after-renders pending.')

if __name__=='__main__':main()
