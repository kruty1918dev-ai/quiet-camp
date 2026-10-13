"""Install reviewed native previews and build the offline Ukrainian gallery."""
from pathlib import Path
import hashlib
import html
import json
import shutil
import uuid

ROOT = Path(__file__).resolve().parents[1]
PROJECT = ROOT / 'QuietCamp'
BASE = ROOT / 'Design/LevelAuthoring/SeasonAtmosphere'
presets = json.loads((PROJECT / 'Assets/QuietCamp/Resources/QuietCamp/season_atmosphere.json').read_text(encoding='utf-8'))['presets']
review = BASE / 'Review/R07'
previews = PROJECT / 'Assets/QuietCamp/Editor/LevelAuthoring/AtmospherePreviews'
previews.mkdir(parents=True, exist_ok=True)
folder_meta = Path(str(previews) + '.meta')
if not folder_meta.exists():
    folder_meta.write_text(f'fileFormatVersion: 2\nguid: {uuid.uuid4().hex}\nfolderAsset: yes\nDefaultImporter:\n  externalObjects: {{}}\n  userData:\n  assetBundleName:\n  assetBundleVariant:\n', encoding='utf-8')
manifest = []
cards = []
seasons = {'spring': 'Весна', 'summer': 'Літо', 'autumn': 'Осінь', 'winter': 'Зима'}
sounds = {'ambience.wind': 'вітер', 'ambience.bird': 'птахи', 'ambience.rain': 'дощ', 'ambience.crickets': 'цвіркуни', 'ambience.owl': 'сова', 'ambience.gust': 'пориви вітру', 'sfx.rustle': 'шурхіт листя'}
for p in presets:
    key = p['id']
    source = review / f'{key}.png'
    if not source.exists():
        raise SystemExit(f'Missing native preview: {source}')
    target = previews / source.name
    shutil.copy2(source, target)
    meta = Path(str(target) + '.meta')
    if not meta.exists():
        meta.write_text(f'''fileFormatVersion: 2
guid: {uuid.uuid4().hex}
TextureImporter:
  serializedVersion: 13
  mipmaps:
    enableMipMap: 0
  isReadable: 0
  sRGBTexture: 1
  textureType: 0
  maxTextureSize: 512
  textureSettings:
    serializedVersion: 2
    filterMode: 1
    aniso: 1
    mipBias: 0
    wrapU: 1
    wrapV: 1
    wrapW: 1
  textureCompression: 1
  userData:
  assetBundleName:
  assetBundleVariant:
''', encoding='utf-8')
    effects = json.loads((review / f'{key}.effects.json').read_text(encoding='utf-8'))
    manifest.append({'preset': key, 'round': 'R07', 'sha256': hashlib.sha256(source.read_bytes()).hexdigest(), 'effects': effects})
    audio = ', '.join(sounds.get(s, s) for s in p['audioKeys'])
    cards.append(f'''<article data-season="{p['season']}"><button class="preview" aria-label="Збільшити: {html.escape(p['label'])}"><img loading="lazy" src="Review/R07/{key}.png" alt="{html.escape(p['label'])} — кадр Unity"></button><div class="body"><small>{seasons[p['season']]}</small><h2>{html.escape(p['label'])}</h2><p>{html.escape(p['description'])}</p><p class="sound">Звуки: {html.escape(audio)}.</p><a href="Review/R07/{key}.effects.json">Дані ефектів</a></div></article>''')
assert len({m['sha256'] for m in manifest}) == 20, 'Repeated preview images'
(BASE / 'previews.json').write_text(json.dumps(manifest, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')
page = '''<!doctype html><html lang="uk"><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><title>Настрій рівня — QuietCamp</title>
<style>:root{color-scheme:dark}*{box-sizing:border-box}body{margin:0;background:#18231d;color:#eff2e8;font:17px/1.6 system-ui}header,main{max-width:1280px;margin:auto;padding:24px}a{color:#bddd9e}h1{font-size:38px;line-height:1.15}h2{font-size:20px;margin:5px 0}p{margin:10px 0}nav{display:flex;gap:10px;flex-wrap:wrap;position:sticky;top:0;background:#18231df5;padding:16px 0;z-index:2}button,input{font:inherit;background:#2c3b30;color:inherit;border:1px solid #6a8067;border-radius:8px;padding:8px 14px}button{cursor:pointer}button[aria-pressed=true]{background:#566d42}input{flex:1;min-width:180px}.grid{display:grid;grid-template-columns:repeat(auto-fill,minmax(265px,1fr));gap:20px}article{background:#27332b;border:1px solid #44523e;border-radius:12px;overflow:hidden}.body{padding:16px}small,.sound{color:#c8cfbf}.sound{font-size:14px}.preview{padding:0;border:0;border-radius:0;display:block;width:100%}.preview img{display:block;width:100%;aspect-ratio:1;object-fit:contain}article[hidden]{display:none}section{padding:20px 0}.steps{background:#27332b;padding:22px;border-radius:12px}dialog{padding:12px;max-width:96vw;background:#18231d;border:1px solid #8b9c7f;color:inherit}dialog::backdrop{background:#000c}dialog img{display:block;max-width:85vw;max-height:82vh}dialog button{float:right}</style>
<header><a href="../index.html">← Інструкція майстерні</a><h1>Один клік — інший настрій</h1><p>20 сезонних пресетів на одному демонстраційному рівні з водяним млином. Усі картинки — справжні квадратні кадри Unity після семи циклів перегляду й виправлень.</p><div class="steps"><strong>У Unity: Quiet Camp → Майстерня рівнів → Настрій</strong><ol><li>Відкрийте рівень у розділі «Рівні».</li><li>У «Настрій» виберіть сезон і натисніть велику картку.</li><li>Кнопка «Послухати звуки пресету» вмикає звукове прев’ю. У тестовій грі звуки працюють автоматично.</li><li>Угорі виберіть помічника зелені — ліс, луг, берег чи сільська галявина — та натисніть «Додати зелень».</li><li>Збережіть чернетку й натисніть «Грати». Скасування Ctrl+Z повертає попередній стан.</li></ol></div></header>
<main><details><summary>Показати справжній інтерфейс майстерні</summary><button class="preview" aria-label="Збільшити скриншот панелі"><img style="aspect-ratio:auto" src="workshop-final.png" alt="Чотири розділи майстерні, вибір зелені та квадратні картки пресетів"></button><p>Виберіть сезон, натисніть картку. Помічник зелені — окремий список і одна кнопка.</p></details><nav aria-label="Фільтр пресетів"><button data-filter="all" aria-pressed="true">Усі</button><button data-filter="spring">Весна</button><button data-filter="summer">Літо</button><button data-filter="autumn">Осінь</button><button data-filter="winter">Зима</button><input id="search" type="search" placeholder="Пошук: дощ, вечір, світлячки…" aria-label="Пошук пресету"></nav><div class="grid">CARDS</div>
<section><h2>Що змінює пресет</h2><p>Напрям, колір і сила світла, тіні, кольори рослин, сезонна видимість листя та квітів, вітер, опади, жучки, звуки, bloom і корекція кольору. Зимове покриття додається на землю та верхні поверхні. Перехід назад до літа відновлює вихідну геометрію.</p><p>Рослини хитаються через шейдер із нерухомим корінням; дощ змінює їхній нахил і вигляд матеріалу. Тверді споруди залишаються твердими. Це візуальна реакція, без тисяч фізичних Rigidbody. Промені й серпанок стилізовані; зимові кадри показують покриття кольором, без накопичення об’єму снігу.</p><p>Усі 100 рівнів кампанії мають окремі редаговані чернетки в <code>Assets/QuietCamp/Authoring/CampaignLevels</code>. Звичайне збереження змінює чернетку; публікація в кампанію виконується окремою перевіреною дією.</p><p><a href="REVIEW-UA.md">Журнал семи циклів</a> · <a href="previews.json">Походження зображень і SHA-256</a></p></section></main>
<dialog id="zoom"><button aria-label="Закрити">Закрити ×</button><img alt="Збільшений кадр Unity"></dialog><script>let season='all';const input=document.querySelector('#search');function filter(){const q=input.value.toLocaleLowerCase('uk');document.querySelectorAll('article').forEach(c=>c.hidden=(season!=='all'&&c.dataset.season!==season)||!c.textContent.toLocaleLowerCase('uk').includes(q))}document.querySelectorAll('[data-filter]').forEach(b=>b.onclick=()=>{season=b.dataset.filter;document.querySelectorAll('[data-filter]').forEach(x=>x.setAttribute('aria-pressed',String(x===b)));filter()});input.oninput=filter;const zoom=document.querySelector('#zoom');document.querySelectorAll('.preview').forEach(b=>b.onclick=()=>{const im=b.querySelector('img');zoom.querySelector('img').src=im.src;zoom.querySelector('img').alt=im.alt;zoom.showModal()});zoom.querySelector('button').onclick=()=>zoom.close();zoom.onclick=e=>{if(e.target===zoom)zoom.close()};</script></html>'''
(BASE / 'index.html').write_text(page.replace('CARDS', '\n'.join(cards)), encoding='utf-8')
print('Installed 20 distinct native previews and offline gallery')
