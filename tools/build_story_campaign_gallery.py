"""Offline campaign review. Missing captures and unfinished review remain explicit."""
import hashlib,html,json
from pathlib import Path

ROOT=Path(__file__).resolve().parents[1];DEST=ROOT/'Design/LevelAuthoring/StoryCampaign'
profiles=json.loads((DEST/'unique-profiles.json').read_text(encoding='utf-8'))
entries=json.loads((ROOT/'QuietCamp/Assets/QuietCamp/Authoring/CampaignLandmarks/catalog.json').read_text(encoding='utf-8'))['entries']
progress_path=DEST/'Verification/gameplay.json'
progress=json.loads(progress_path.read_text(encoding='utf-8')) if progress_path.exists() else {}
played={row['id']:row for row in progress.get('levels',[]) if row.get('completion',{}).get('solved')}
inspections={};cards=[];captured=0;accepted=0;game_verified=0
for number,(p,e) in enumerate(zip(profiles,entries),1):
    s=p['story'];name=f'L{number:03}'
    frames=sorted((DEST/'Review').glob('U[0-9][0-9]/'+name+'.png'))
    frame=frames[-1] if frames else None
    captured+=bool(frame)
    reviewed=False
    if frame:
        if frame.parent.name not in inspections:
            path=frame.parent/'inspection.json'
            inspections[frame.parent.name]={r['order']:r for r in json.loads(path.read_text(encoding='utf-8'))['levels']} if path.exists() else {}
        row=inspections[frame.parent.name].get(number,{})
        reviewed=row.get('status')=='accepted' and row.get('imageSha256')==hashlib.sha256(frame.read_bytes()).hexdigest()
    accepted+=reviewed
    status=('Композицію перевірено · проходження очікує перевірки' if reviewed else 'Художнє приймання не завершене') if frame else 'Композиція очікує складання'
    game='';proof=played.get(s['id']);content=ROOT/'QuietCamp/Assets/QuietCamp/Resources/QuietCamp';safe=s['id'].replace(':','_')
    if proof:
        level_path=content/('GeneratedLevels' if s['id'].startswith('gen:') else 'Levels')/(safe+'.json')
        art_path=content/'ArtCompositions'/(safe+'.json');shot=DEST/proof['screenshot']['path']
        if shot.exists() and proof.get('publishedLevelSha256')==hashlib.sha256(level_path.read_bytes()).hexdigest() and proof.get('publishedArtSha256')==hashlib.sha256(art_path.read_bytes()).hexdigest() and proof['screenshot']['sha256']==hashlib.sha256(shot.read_bytes()).hexdigest():
            game_verified+=1;status='Композицію та автоматичне проходження перевірено'
            game='<button class="game-evidence" data-image="'+html.escape(shot.relative_to(DEST).as_posix(),quote=True)+'">Кадр гри з інтерфейсом</button>'
    image='<img loading="lazy" src="'+frame.relative_to(DEST).as_posix()+'" alt="Нативний кадр '+html.escape(s['title'],quote=True)+'">' if frame else '<div class="pending">Нативний кадр ще не знято</div>'
    text=' '.join([str(number),s['id'],s['title'],e['label'],s['weather']]).lower()
    cards.append('<article data-search="'+html.escape(text,quote=True)+'"><button class="preview" '+('data-image="'+frame.relative_to(DEST).as_posix()+'"' if frame else 'disabled')+'>'+image+'</button><div class="copy"><small>'+str(number)+' / '+html.escape(s['id'])+'</small><h2>'+html.escape(s['title'])+'</h2><p class="hero">'+html.escape(e['label'])+'</p><p class="status">'+html.escape(status)+'</p>'+game+'<details><summary>Історія, правила й задум</summary><p>'+html.escape(s['canonicalBefore'])+'</p><p>'+html.escape(s['canonicalBeat'])+'</p><p><b>Мета:</b> '+html.escape(s['objective'])+'</p><p><b>Погода:</b> '+html.escape(s['weather'])+'</p><p><b>Новий акцент:</b> '+html.escape(e['label'])+' — авторське доповнення до канонічного місця.</p><p><b>Конструкція:</b> <code>'+html.escape(e['construction'])+'</code></p></details></div></article>')
page='''<!doctype html><html lang="uk"><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><title>QuietCamp · 100 окремих місць</title>
<style>:root{color-scheme:dark;font-family:system-ui,sans-serif;background:#18261f;color:#eee9d9}body{margin:0}header{max-width:1400px;margin:auto;padding:32px 24px 18px}h1{font-size:clamp(26px,4vw,46px);margin:0 0 16px}header p{max-width:900px;line-height:1.6}a{color:#d9c17f}.tools{position:sticky;top:0;background:#18261ff5;padding:14px 24px;z-index:2;display:flex;gap:18px;align-items:center}input{font:inherit;padding:12px;border:1px solid #78816c;border-radius:8px;background:#26392d;color:white;width:min(600px,70vw)}main{display:grid;grid-template-columns:repeat(auto-fit,minmax(300px,1fr));gap:20px;max-width:1400px;padding:10px 24px 40px;margin:auto}article{background:#283b2f;border:1px solid #405744;border-radius:14px;overflow:hidden}article[hidden]{display:none}.preview{display:block;border:0;padding:0;width:100%;background:#324735;cursor:zoom-in;aspect-ratio:1;overflow:hidden}.preview img{width:100%;height:100%;object-fit:cover}.preview:disabled{cursor:default}.pending{padding:28px;font-size:18px;color:#cad0bd}.copy{padding:18px}h2{font-size:20px;margin:8px 0}.hero{color:#f1d697}.status{font-size:13px;color:#d4c29b}small{color:#b6c9b2}details{border-top:1px solid #58705b;padding-top:12px;line-height:1.55}summary{cursor:pointer}dialog{border:1px solid #809277;border-radius:12px;padding:12px;background:#18261f;max-width:94vw;max-height:94vh}dialog img{display:block;max-width:88vw;max-height:82vh}dialog button{font:inherit;background:#d9c17f;color:#18261f;border:0;border-radius:6px;padding:8px 16px;margin-bottom:10px;cursor:pointer}dialog::backdrop{background:#000c}code{overflow-wrap:anywhere}</style>
<header><h1>QuietCamp · 100 окремих місць</h1><p>Спільна дорога до Гавані, різні історії та <b>100 зарезервованих головних моделей</b>. 79 конструкцій створено окремо; інші акценти використовують різні наявні моделі. Допоміжні дороги, ЛЕП і рослинність можуть повторюватися.</p><p><b>Це робочий перегляд, а не оголошення готовності кампанії.</b> Нативні кадри показують реальну Unity-сцену, але потребують семи циклів правок і перевірки проходження перед публікацією. Кількість підготовлених моделей не дорівнює кількості художньо прийнятих рівнів.</p><p>Початкові кадри: CAPTURED / 100. <a href="LANDMARKS-UA.md">Реєстр акцентів</a> · <a href="README-UA.md">Сюжетна таблиця</a> · <a href="../AircraftSeasons/surface.html">Еталон із літаком</a></p></header><div class="tools"><input id="search" type="search" placeholder="Номер, назва, модель або погода" aria-label="Пошук рівнів"><span id="count">100 рівнів</span></div><main>CARDS</main><dialog><button autofocus>Закрити</button><img alt="Збільшений нативний кадр"></dialog><script>
const cards=[...document.querySelectorAll('article')],q=document.querySelector('#search'),count=document.querySelector('#count'),dialog=document.querySelector('dialog');q.addEventListener('input',()=>{let n=0;for(const c of cards){c.hidden=!c.dataset.search.includes(q.value.toLowerCase().trim());if(!c.hidden)n++}count.textContent=n+' рівнів'});document.querySelectorAll('[data-image]').forEach(b=>b.onclick=()=>{dialog.querySelector('img').src=b.dataset.image;dialog.showModal()});dialog.querySelector('button').onclick=()=>dialog.close();dialog.addEventListener('click',e=>{if(e.target===dialog)dialog.close()});
</script></html>'''.replace('CAPTURED',str(captured)).replace('CARDS',''.join(cards))
page=page.replace('Початкові кадри:','Нативні кадри:').replace('</style>','.game-evidence{font:inherit;margin:0 0 16px;padding:10px 14px;border:1px solid #809277;border-radius:8px;background:#d9c17f;color:#18261f;cursor:zoom-in}</style>')
page=page.replace('Нативні кадри показують реальну Unity-сцену, але потребують семи циклів правок і перевірки проходження перед публікацією.',f'Художній перегляд після семи циклів правок: {accepted} / 100. Перевірено через ігрові дії «Перевірити» та «Далі»: {game_verified} / 100. Це автоматична перевірка еталонних розміщень; тестування з новачками сюди не входить.')
if game_verified==100 and progress.get('status')=='passed':page=page.replace('Це робочий перегляд, а не оголошення готовності кампанії.','Усі 100 композицій застосовано до кампанії та перевірено автоматичним проходженням.')
(DEST/'index.html').write_text(page,encoding='utf-8');print(f'Offline review: 100 cards; {accepted} accepted compositions; {game_verified} verified gameplay frames.')
