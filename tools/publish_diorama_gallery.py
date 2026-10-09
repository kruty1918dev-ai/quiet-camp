"""Publish offline study gallery and contact sheet after explicit visual review."""
import html,json,sys
from pathlib import Path
from PIL import Image,ImageDraw,ImageFont

OUT=Path('Design/Roadmap/DioramaStudies/2026-10-10')
SOURCE=Path('QuietCamp/Assets/QuietCamp/Authoring/Roadmap/Composition/DioramaStudies')
styles=json.loads((SOURCE/'presentation.json').read_text())
sheets_only='--sheets-only' in sys.argv
reviews={} if sheets_only else {r['id']:r for r in json.loads((OUT/'visual-review.json').read_text())['reviews']}
font=ImageFont.truetype('/usr/share/fonts/truetype/dejavu/DejaVuSans.ttf',17)
sheet=Image.new('RGB',(1800,1980),'#eeeeE4');draw=ImageDraw.Draw(sheet)
cards=[]
for i,style in enumerate(styles):
    sid=style['id'];r=json.loads((OUT/(sid+'.json')).read_text())
    x,y=(i%3)*600,(i//3)*495
    with Image.open(OUT/r['image']) as image:sheet.paste(image.convert('RGB').resize((600,450)),(x,y))
    draw.text((x+10,y+455),sid+' · '+style['titleUk'],font=font,fill='#263b2b')
    if sheets_only:continue
    review=reviews[sid]
    e=html.escape
    cards.append(f'''<article data-season="{e(style['season'])}"><h2>{e(sid+' · '+style['titleUk'])}</h2>
<a href="{e(r['image'])}"><img loading="lazy" src="{e(r['image'])}" alt="{e(style['titleUk'])}"></a>
<p>{e(style['descriptionUk'])}</p><p class="note">{e(review['decisionUk'])}</p>
<details><summary>Моделі та оцінка</summary><p>{e(review['observationUk'])}</p>
<p>Donor: {e(', '.join(r['donorCatalogueIds']))}. Native preview: {r['nativePreviewTriangles']:,} triangles,
{r['nativePreviewRenderers']} renderers, {r['uniqueMaterials']} materials. Це не runtime budgets.</p>
<a href="{sid}.json">Квитанція та source hashes</a></details></article>''')
sheet.save(OUT/'overview.jpg',quality=94)
if sheets_only:
    print('Created contact sheet for visual inspection; no review accepted.');raise SystemExit(0)
page='''<!doctype html><html lang="uk"><meta charset="utf-8"><meta name="viewport" content="width=device-width">
<title>Quiet Camp · 12 українських діорам</title><style>
body{margin:0;background:#f0efe5;color:#25382b;font:17px/1.55 system-ui}header,main{max-width:1500px;margin:auto;padding:24px}
h1{font-size:32px}h2{font-size:22px}a{color:#476449}nav{display:flex;gap:10px;flex-wrap:wrap}button{font:inherit;background:#fffdf3;border:1px solid #bcc5ae;padding:8px 16px;border-radius:8px;cursor:pointer}
main{display:grid;grid-template-columns:repeat(auto-fit,minmax(360px,1fr));gap:22px}article{padding:18px;background:#fffdf5;border-radius:16px}img{width:100%;height:auto;border-radius:8px}details{font-size:14px}summary{cursor:pointer}.note{font-size:15px;color:#586849}
@media(max-width:440px){main{grid-template-columns:1fr;padding:10px}}</style><header><h1>12 українських діорам Quiet Camp</h1>
<p>Native Unity Editor · 10.10.2026 · композиційні досліди стилю. Чинні рівні та активна мапа збережені.</p>
<p><a href="../../DIORAMA-DESIGN-UA.md">Дизайн-план</a> · <a href="../../ModelCatalogue/index.html">Каталог 1 210 моделей</a> · <a href="overview.jpg">Увесь набір</a></p>
<nav><button data-filter="all">Усі 12</button><button data-filter="spring">Весна</button><button data-filter="summer">Літо</button><button data-filter="autumn">Осінь</button><button data-filter="winter">Зима</button></nav></header><main>'''+''.join(cards)+'''</main>
<script>document.querySelectorAll('button').forEach(b=>b.onclick=()=>document.querySelectorAll('article').forEach(a=>a.hidden=b.dataset.filter!=='all'&&a.dataset.season!==b.dataset.filter));</script></html>'''
(OUT/'index.html').write_text(page)
print('Published 12-card offline gallery and overview; explicit reviews retained.')
