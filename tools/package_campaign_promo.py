"""Assemble only captured gameplay/audio into a validated ZIP below 100 decimal MB."""
import argparse
import hashlib
import html
import json
import math
from pathlib import Path
import re
import subprocess
import zipfile

from PIL import Image

ROOT = Path(__file__).resolve().parents[1]
QA = Path('C:/Users/Public/qcui-20261006/QuietCamp')
STAGE = QA/'UserSettings/QuietCampPromoCapture/2026-10-15-AudioVerified'
TOOLS = QA/'UserSettings/QuietCampPromoTools'
WORK = STAGE/'montage'
DELIVERY = ROOT/'Deliverables/QuietCamp-100-Levels'
DOC = ROOT/'Design/LevelAuthoring/StoryCampaign/Promo'
FF = str(TOOLS/'ffmpeg.exe')
PROBE = str(TOOLS/'ffprobe.exe')
SECONDS = 4.3
OVERLAP = .3
TOTAL = 100*SECONDS-99*OVERLAP


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def run(args, log):
    log.parent.mkdir(parents=True, exist_ok=True)
    with log.open('w', encoding='utf-8') as handle:
        result = subprocess.run(args, stdout=handle, stderr=subprocess.STDOUT)
    if result.returncode:
        raise RuntimeError(f'Command failed ({result.returncode}): {log}\n'+log.read_text(encoding='utf-8',errors='replace')[-2000:])


def probe(path):
    r = subprocess.run([PROBE, '-v', 'error', '-show_streams', '-show_format', '-of', 'json', str(path)], capture_output=True, text=True, encoding='utf-8', check=True)
    return json.loads(r.stdout)


def captures():
    rows = []
    content=Path('Assets/QuietCamp/Resources/QuietCamp')
    ids=json.loads((QA/content/'campaign.json').read_text(encoding='utf-8'))['campaignLevelIds']
    assert len(ids)==len(set(ids))==100
    assert ids==json.loads((ROOT/'QuietCamp'/content/'campaign.json').read_text(encoding='utf-8'))['campaignLevelIds']
    for order in range(1, 101):
        folder = STAGE/f'L{order:03}'
        data = json.loads((folder/'capture.json').read_text(encoding='utf-8'))
        assert data['status'] == 'captured' and data['order'] == order and data['completed'] and data['stillHasPlacementGrid'] and data['memoryOnly']
        assert sha(folder/'gameplay.mp4') == data['movieSha256'] and sha(folder/'gameplay.png') == data['imageSha256']
        assert data['playingAtmosphereVoices'] > 0
        assert data['media']['audioPackets'] >= 180 and data['media']['audioMeanDb'] > -60, 'Source audio must be decoded before editing'
        assert data['id']==ids[order-1]
        safe=data['id'].replace(':','_')
        for relative,key in [(content/('GeneratedLevels' if safe.startswith('gen_') else 'Levels')/(safe+'.json'),'levelSha256'),(content/'ArtCompositions'/(safe+'.json'),'artSha256')]:
            assert sha(QA/relative)==sha(ROOT/'QuietCamp'/relative)==data['inputs'][key],f'Published content changed after capture: {data["id"]}'
        if rows:assert data['inputs']['sources']==rows[0]['inputs']['sources'],'Mixed recorder source versions in one capture batch'
        rows.append(data)
    return rows


def prepare():
    rows = captures()
    DELIVERY.mkdir(parents=True, exist_ok=True)
    (DELIVERY/'renders').mkdir(exist_ok=True)
    WORK.mkdir(parents=True, exist_ok=True)
    chapters = [';FFMETADATA1', 'title=Quiet Camp — 100 рівнів', 'comment=Actual Unity gameplay, UI, placement grid and in-game audio.']
    cards = []
    for row in rows:
        order = row['order']; safe = row['id'].replace(':', '_')
        content = QA/'Assets/QuietCamp/Resources/QuietCamp'
        level = json.loads((content/('GeneratedLevels' if safe.startswith('gen_') else 'Levels')/(safe+'.json')).read_text(encoding='utf-8'))
        title = level.get('title', row['id'])
        if not isinstance(title, str):
            title = row['id']
        # Titles are authoring/story data; avoid changing the gameplay capture itself.
        profile = json.loads((ROOT/'QuietCamp/Assets/QuietCamp/Authoring/CampaignLevels'/(safe+'.draft.json')).read_text(encoding='utf-8'))
        title = profile.get('title') or title
        image = DELIVERY/'renders'/f'{order:03}_{safe}.jpg'
        with Image.open(STAGE/f'L{order:03}/gameplay.png') as source:
            source.convert('RGB').save(image, quality=88, optimize=True, subsampling=0)
        start = (order-1)*4
        end = min(TOTAL, start+SECONDS)
        chapter_end = order*4 if order < 100 else TOTAL
        chapters += ['[CHAPTER]', 'TIMEBASE=1/1000', f'START={round(start*1000)}', f'END={round(chapter_end*1000)}', 'title='+f'{order:03} — {title}'.replace('=', '\\=').replace(';', '\\;').replace('#', '\\#').replace('\n',' ')]
        cards.append(dict(order=order, id=row['id'], title=title, start=start, end=end, image=image.relative_to(DELIVERY).as_posix(), imageSha256=sha(image), sourceImageSha256=row['imageSha256'], sourceMovieSha256=row['movieSha256']))
    (WORK/'chapters.txt').write_text('\n'.join(chapters)+'\n', encoding='utf-8')
    (DELIVERY/'levels.json').write_text(json.dumps(cards, ensure_ascii=False, indent=2)+'\n', encoding='utf-8')
    sections=''.join(f'<article><a href="{c["image"]}" target="_blank"><img loading="lazy" src="{c["image"]}" alt="Рівень {c["order"]}: {html.escape(c["title"])}"></a><h2>{c["order"]:03} · {html.escape(c["title"])}</h2><button data-time="{c["start"]}">▶ {int(c["start"]//60):02}:{int(c["start"]%60):02}</button></article>' for c in cards)
    page='''<!doctype html><html lang="uk"><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><title>Quiet Camp · 100 рівнів</title><style>body{margin:auto;max-width:1440px;padding:24px;background:#172d25;color:#f5eedc;font:17px system-ui}video{width:100%;max-height:75vh;background:#000}a{color:inherit}.grid{display:grid;grid-template-columns:repeat(auto-fit,minmax(300px,1fr));gap:20px}article{background:#244234;border-radius:16px;padding:12px}img{width:100%;border-radius:8px}h2{font-size:18px}button{padding:10px 20px;border:0;border-radius:8px;background:#e7dba9;cursor:pointer}header{padding-bottom:20px}input{padding:12px;margin:20px 0;min-width:280px;border-radius:8px}</style><header><h1>Quiet Camp · 100 рівнів</h1><p>Справжній процес гри: український інтерфейс, сітка під час розміщення, погода й звук самої гри. Кожен рівень — 4,3 секунди, переходи — 0,3 секунди. Завантаження з інтернету не потрібне.</p><video id="promo" controls preload="metadata" src="QuietCamp-100-Levels.mp4"></video><input id="search" placeholder="Знайти рівень або назву"><p>Натисніть на кадр, щоб відкрити його повністю, або на час, щоб перейти до рівня у відео.</p></header><main class="grid">'''+sections+'''</main><script>const v=document.querySelector('video');document.querySelectorAll('[data-time]').forEach(b=>b.onclick=()=>{v.currentTime=Number(b.dataset.time);v.play();v.scrollIntoView({behavior:'smooth',block:'center'})});document.querySelector('#search').oninput=e=>document.querySelectorAll('article').forEach(a=>a.hidden=!a.textContent.toLocaleLowerCase().includes(e.target.value.toLocaleLowerCase()));</script></html>'''
    page=page.replace('src="QuietCamp-100-Levels.mp4"', 'src="QuietCamp-100-Levels.mp4" poster="'+html.escape(cards[43]['image'])+'"')
    page=page.replace('</main>', '</main><p><a href="ATTRIBUTION.txt">Авторство моделей</a> · <a href="README-UA.txt">Про матеріали</a></p>')
    (DELIVERY/'index.html').write_text(page, encoding='utf-8')
    (DELIVERY/'README-UA.txt').write_text('Quiet Camp — 100 ігрових рівнів\n\nВідкрийте index.html або QuietCamp-100-Levels.mp4. Кадри — у renders.\nУсі зображення знято з Game View, з UI й активною сіткою звичайного розміщення. У відео показано автоматичне відтворення реальних перетягувань наметів і завершення рівня.\nЗвук походить виключно з активної гри: атмосфера рівня, погода та ігрові дії. Під час монтажу виконано тільки переходи й корекцію гучності. Зовнішньої музики немає.\nВідео: H.264, 30 кадрів/с, 1280×960, AAC stereo; 4,3 с на рівень з 0,3 с перекриття на стиках, загалом 6 хв 40,3 с. MP4 містить 100 розділів.\nОригінали записано в Unity Editor у 1440×1080. Швидкість відео не є вимірюванням FPS на телефоні. Тестова гра використовувала прогрес у пам’яті.\nКонтрольні суми: SHA256SUMS.txt.\n', encoding='utf-8')
    (DELIVERY/'ATTRIBUTION.txt').write_text('Quiet Camp — власні моделі та ресурси проєкту.\nAirplane — Poly by Google, CC BY 3.0, adapted for Quiet Camp.\nSource: https://poly.pizza/m/a3XrQkLNna9\nLicense: https://creativecommons.org/licenses/by/3.0/\nЗастосовано розділення, руйнування, старіння, матеріали, масштабування та компонування моделі.\n', encoding='utf-8')
    print('Prepared 100 full-size gameplay JPGs and offline gallery; JPG bytes:', sum(p.stat().st_size for p in (DELIVERY/'renders').glob('*.jpg')), flush=True)


def transition_group(inputs, durations, output, tag):
    fingerprint=dict(inputs=[dict(path=str(p.resolve()),sha256=sha(p)) for p in inputs],durations=durations,overlap=OVERLAP,width=1280,height=960,fps=30,crf=16,audio='flac24')
    receipt=WORK/f'{tag}.json'
    if output.exists() and receipt.exists():
        previous=json.loads(receipt.read_text(encoding='utf-8'))
        if previous.get('inputs')==fingerprint and previous.get('outputSha256')==sha(output):
            print('Reused verified',tag,flush=True);return
    args=[FF, '-y', '-hide_banner', '-loglevel', 'warning', '-filter_complex_threads', '1']
    graph=[]
    for i, path in enumerate(inputs):
        args += ['-threads', '1', '-i', str(path)]
        graph += [f'[{i}:v]trim=duration={durations[i]:.6f},fps=30,settb=AVTB,setpts=PTS-STARTPTS,scale=1280:960:flags=lanczos,setsar=1,format=yuv420p[v{i}]',
                  f'[{i}:a]atrim=duration={durations[i]:.6f},asetpts=PTS-STARTPTS,aresample=48000[a{i}]']
    v='v0';a='a0';duration=durations[0]
    for i in range(1,len(inputs)):
        graph += [f'[{v}][v{i}]xfade=transition=fade:duration={OVERLAP}:offset={duration-OVERLAP:.6f}[x{i}]',
                  f'[{a}][a{i}]acrossfade=d={OVERLAP}:c1=qsin:c2=qsin[z{i}]']
        v=f'x{i}';a=f'z{i}';duration+=durations[i]-OVERLAP
    graph += [f'[{v}]format=yuv420p[vout]']
    script=WORK/f'{tag}.filters';script.write_text(';\n'.join(graph),encoding='utf-8')
    args += ['-filter_complex',';'.join(graph),'-map','[vout]','-map',f'[{a}]','-c:v','libx264','-preset','medium','-crf','16','-threads','2','-pix_fmt','yuv420p','-c:a','flac','-sample_fmt','s32','-ar','48000','-t',f'{duration:.6f}',str(output)]
    run(args,WORK/f'{tag}.log')
    data=probe(output);assert abs(float(data['format']['duration'])-duration)<.08
    audio=next(s for s in data['streams'] if s['codec_type']=='audio')
    parts=audio.get('tags',{}).get('DURATION','0:0:0').split(':')
    audio_duration=sum(float(part)*60**i for i,part in enumerate(reversed(parts)))
    assert abs(audio_duration-duration)<.08, f'Incomplete intermediate audio: {tag}, {audio_duration}s'
    receipt.write_text(json.dumps(dict(inputs=fingerprint,outputSha256=sha(output),duration=duration),indent=2)+'\n',encoding='utf-8')
    print('Rendered',tag,f'{duration:.2f}s',flush=True)


def groups():
    captures()
    WORK.mkdir(parents=True,exist_ok=True)
    for group in range(10):
        output=WORK/f'group-{group+1:02}.mkv'
        transition_group([STAGE/f'L{n:03}/gameplay.mp4' for n in range(group*10+1,group*10+11)],[SECONDS]*10,output,f'group-{group+1:02}')


def assemble():
    master=WORK/'master.mkv'
    transition_group([WORK/f'group-{n:02}.mkv' for n in range(1,11)],[40.3]*10,master,'master')
    # Normalize only the captured game's mix; no extra sound sources are used.
    measure=WORK/'loudness.log'
    run([FF,'-hide_banner','-i',str(master),'-vn','-af','loudnorm=I=-20:TP=-1.5:LRA=11:print_format=json','-f','null','-'],measure)
    match=re.findall(r'\{\s*"input_i".*?\}',measure.read_text(encoding='utf-8',errors='replace'),re.S)
    loud=json.loads(match[-1]);(WORK/'loudness.json').write_text(json.dumps(loud,indent=2),encoding='utf-8')
    af='loudnorm=I=-20:TP=-1.5:LRA=11:linear=true:measured_I={input_i}:measured_TP={input_tp}:measured_LRA={input_lra}:measured_thresh={input_thresh}:offset={target_offset}'.format(**loud)
    other=sum(p.stat().st_size for p in DELIVERY.rglob('*') if p.is_file() and p.name not in ['QuietCamp-100-Levels.mp4','SHA256SUMS.txt','verification.json'])
    budget=98_000_000-other-1_000_000
    rate=math.floor((budget*8/TOTAL-128000)*.985/1000)
    assert rate>=900,f'Insufficient video budget: {rate} kb/s'
    output=DELIVERY/'QuietCamp-100-Levels.mp4'
    for attempt in range(2):
        common=[FF,'-y','-hide_banner','-loglevel','warning','-threads','2','-i',str(master),'-f','ffmetadata','-i',str(WORK/'chapters.txt'),'-map','0:v','-vf',f'fade=t=in:d=0.18,fade=t=out:st={TOTAL-.18:.3f}:d=0.18','-c:v','libx264','-preset','slow','-threads','2','-pix_fmt','yuv420p','-b:v',f'{rate}k','-passlogfile',str(WORK/'encode-pass'),'-map_metadata','1','-map_chapters','1']
        run(common+['-pass','1','-an','-f','null','NUL'],WORK/'encode-pass1.log')
        run(common+['-pass','2','-map','0:a','-af',af+f',afade=t=in:d=0.18,afade=t=out:st={TOTAL-.18:.3f}:d=0.18','-c:a','aac','-b:a','128k','-ar','48000','-ac','2','-movflags','+faststart',str(output)],WORK/'encode-pass2.log')
        if output.stat().st_size<budget:break
        rate=math.floor(rate*.94)
    assert output.stat().st_size<budget
    print('Final MP4 bytes:',output.stat().st_size,'target video kb/s:',rate,flush=True)


def verify():
    rows=captures()
    video=DELIVERY/'QuietCamp-100-Levels.mp4';data=probe(video)
    v=next(s for s in data['streams'] if s['codec_type']=='video');a=next(s for s in data['streams'] if s['codec_type']=='audio')
    assert v['codec_name']=='h264' and v['width']==1280 and v['height']==960 and v['avg_frame_rate']=='30/1'
    assert a['codec_name']=='aac' and a['channels']==2 and a['sample_rate']=='48000'
    assert abs(float(data['format']['duration'])-TOTAL)<.08
    chapter_result=subprocess.run([PROBE,'-v','error','-show_chapters','-of','json',str(video)],capture_output=True,text=True,encoding='utf-8',check=True)
    chapters=json.loads(chapter_result.stdout)['chapters'];assert len(chapters)==100
    assert all(abs(float(c['start_time'])-i*4)<.01 for i,c in enumerate(chapters))
    run([FF,'-hide_banner','-v','error','-xerror','-threads','2','-i',str(video),'-map','0:v','-map','0:a','-f','null','-'],WORK/'decode-validation.log')
    run([FF,'-hide_banner','-i',str(video),'-vn','-af','astats=metadata=0:reset=0','-f','null','-'],WORK/'audio-validation.log')
    audio_log=(WORK/'audio-validation.log').read_text(encoding='utf-8',errors='replace')
    peaks=[float(x) for x in re.findall(r'Peak level dB: ([-\d.]+)',audio_log)]
    rms=[float(x) for x in re.findall(r'RMS level dB: ([-\d.]+)',audio_log)]
    assert peaks and max(peaks)<0 and rms and min(rms)>-60
    for term in ['Number of NaNs','Number of Infs']:
        values=re.findall(term+r': ([\d.]+)',audio_log)
        assert values and all(float(x)==0 for x in values)
    images=sorted((DELIVERY/'renders').glob('*.jpg'));assert len(images)==100
    for image in images:
        with Image.open(image) as im:
            im.load()
            assert im.format=='JPEG' and im.size==(1440,1080),image
    cards=json.loads((DELIVERY/'levels.json').read_text(encoding='utf-8'))
    assert [c['id'] for c in cards]==[r['id'] for r in rows]
    assert all(sha(DELIVERY/c['image'])==c['imageSha256'] for c in cards)
    page=(DELIVERY/'index.html').read_text(encoding='utf-8')
    assert page.count('<article>')==100 and '??' not in page and '\ufffd' not in page
    for reference in re.findall(r'(?:href|src|poster)="([^"]+)"',page):
        assert not reference.startswith(('http:','https:')), 'Gallery must work offline'
        assert (DELIVERY/reference).is_file(),reference
    # Detect a missing/silent level, not just a nonsilent overall soundtrack.
    per_level=[]
    for row in rows:
        start=(row['order']-1)*4+.4
        r=subprocess.run([FF,'-hide_banner','-ss',str(start),'-t','3.4','-i',str(video),'-vn','-af','volumedetect','-f','null','-'],capture_output=True,text=True,encoding='utf-8',check=True)
        mean=re.search(r'mean_volume: ([-\d.]+) dB',r.stderr);assert mean and float(mean[1])>-60,row['id']
        per_level.append(dict(order=row['order'],id=row['id'],audioMeanDb=float(mean[1]),sourceMovieSha256=row['movieSha256'],sourceImageSha256=row['imageSha256']))
    report=dict(status='decoded-and-validated',movieSha256=sha(video),levels=100,duration=TOTAL,video=v['codec_name'],resolution=[v['width'],v['height']],fps=30,audio=a['codec_name'],sampleRate=48000,channels=2,audioPeakDb=max(peaks),audioRmsDb=min(rms),fullyDecoded=True,imagesDecoded=100,source='Native Unity Game View, real placement input, UI and placement grid; audio recorded from the same running levels.',noExternalSoundtrack=True,levelChecks=per_level)
    report['offlineGalleryValidated']=True
    (DELIVERY/'verification.json').write_text(json.dumps(report,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
    DOC.mkdir(parents=True,exist_ok=True);(DOC/'media-verification.json').write_text(json.dumps(report,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
    print('Decoded full MP4 and 100 JPGs; all 100 audio segments nonsilent, no clipping or invalid samples.',flush=True)


def package():
    report=json.loads((DELIVERY/'verification.json').read_text(encoding='utf-8'));assert report['status']=='decoded-and-validated'
    movie_hash=sha(DELIVERY/'QuietCamp-100-Levels.mp4')
    assert report['movieSha256']==movie_hash,'Movie changed after decode verification'
    review=json.loads((DOC/'visual-review.json').read_text(encoding='utf-8'))
    assert review['finalEncodingReviewed'] and review['transitionsReviewed']
    assert review['movieSha256']==movie_hash,'Visual review belongs to another encode'
    assert review['motionReviewed']==list(range(1,101)) and len(review['sourceStills'])==100
    files=sorted(p for p in DELIVERY.rglob('*') if p.is_file() and p.name!='SHA256SUMS.txt')
    (DELIVERY/'SHA256SUMS.txt').write_text(''.join(f'{sha(p)}  {p.relative_to(DELIVERY).as_posix()}\n' for p in files),encoding='utf-8')
    output=DELIVERY.parent/'QuietCamp-100-Levels.zip'
    with zipfile.ZipFile(output,'w',compression=zipfile.ZIP_STORED,allowZip64=False) as z:
        for p in sorted(p for p in DELIVERY.rglob('*') if p.is_file()):z.write(p,p.relative_to(DELIVERY.parent).as_posix())
    assert output.stat().st_size<100_000_000,output.stat().st_size
    with zipfile.ZipFile(output) as z:
        assert z.testzip() is None
        for p in files:assert hashlib.sha256(z.read(p.relative_to(DELIVERY.parent).as_posix())).hexdigest()==sha(p)
    receipt=dict(status='verified',zip=output.name,bytes=output.stat().st_size,limitBytes=100_000_000,sha256=sha(output),crcPassed=True,fileHashesPassed=True,levels=100,duration=TOTAL)
    (DOC/'archive-verification.json').write_text(json.dumps(receipt,indent=2)+'\n',encoding='utf-8')
    print(json.dumps(receipt),flush=True)


if __name__=='__main__':
    p=argparse.ArgumentParser(description=__doc__);p.add_argument('step',choices=['prepare','groups','assemble','verify','package']);args=p.parse_args()
    globals()[args.step]()
