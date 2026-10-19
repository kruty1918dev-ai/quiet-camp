"""Assemble verified Unity Recorder takes. Audio comes exclusively from each in-engine take."""
import argparse
import hashlib
import json
import re
from pathlib import Path
import subprocess


def run(args):
    result = subprocess.run([str(x) for x in args], capture_output=True, text=True, encoding='utf-8')
    if result.returncode:
        raise RuntimeError(result.stderr[-6000:])
    return result.stdout


def probe(binary, path):
    return json.loads(run([binary, '-v', 'error', '-count_frames', '-show_streams', '-show_format', '-of', 'json', path]))


def verify(binary, ffmpeg, path, frames):
    data = probe(binary, path)
    video = next(s for s in data['streams'] if s['codec_type'] == 'video')
    audio = next(s for s in data['streams'] if s['codec_type'] == 'audio')
    assert (video['width'], video['height'], video['r_frame_rate'], int(video['nb_read_frames'])) == (1920, 1080, '30/1', frames), video
    assert audio['channels'] == 2 and int(audio['sample_rate']) == 48000, audio
    assert video['codec_name'] == 'h264', video
    run([ffmpeg, '-v', 'error', '-xerror', '-threads', '1', '-i', path, '-f', 'null', '-'])
    return data


def takes(project, version="v01"):
    job = json.loads((project / ('UserSettings/Teaser/job.json' if version=='v01' else 'UserSettings/Teaser/job-'+version+'.json')).read_text(encoding='utf-8-sig'))
    receipts = job.get('receipts', [])
    if job.get('status') != 'captured' or not job.get('record') or job.get('onlyShot') != -1 or len(receipts) != job.get('shotCount',7):
        raise RuntimeError('Finish one complete recording for this revision before assembling.')
    if sum(r['frames'] for r in receipts) != (job.get('totalFrames') or 1260) or len({r['shotId'] for r in receipts}) != len(receipts):
        raise RuntimeError('Invalid recorded sequence.')
    for receipt in receipts:
        path = Path(receipt['video'])
        stored = json.loads((path.parent / 'capture.json').read_text(encoding='utf-8-sig'))
        if stored != receipt or receipt['sourceHash'] != job['sourceHash'] or not receipt.get('recorded') or not receipt['memoryOnly'] or receipt['renderedFrames'] != receipt['frames']:
            raise RuntimeError('Capture evidence does not match the completed job: ' + str(path))
    return receipts


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--project', type=Path, required=True)
    parser.add_argument('--version',choices=['v01','v02','v03-a','v03-b','v03-c','v03-rough','v03'],default='v01')
    parser.add_argument('--verify-only', type=Path)
    parser.add_argument('--gain-db', type=float, default=6, help='Delivery master gain; source audio remains the Unity recording (0 keeps the raw game mix)')
    parser.add_argument('--finish-v03','--compress-native-mix',dest='finish_v03',action='store_true',help='V03 finishing: compress recorded audio and complete the final fade; no added content')
    args = parser.parse_args()
    if not -12 <= args.gain_db <= 12:
        raise SystemExit('Master gain must stay within -12..12 dB.')
    if args.finish_v03 and args.version!='v03':
        raise SystemExit('Native-mix finishing is isolated to V03; earlier exports stay unchanged.')
    project = args.project.resolve()
    tools = project / 'UserSettings/QuietCampPromoTools'
    ffmpeg, ffprobe = tools / 'ffmpeg.exe', tools / 'ffprobe.exe'
    if args.verify_only:
        data = probe(ffprobe, args.verify_only)
        print(json.dumps(data, indent=2))
        return
    job=json.loads((project/('UserSettings/Teaser/job.json' if args.version=='v01' else 'UserSettings/Teaser/job-'+args.version+'.json')).read_text(encoding='utf-8-sig'))
    output = Path(job['output'])
    expected=job.get('totalFrames') or 1260
    seconds=expected/30
    ordered = takes(project,args.version)
    paths = [Path(r['video']) for r in ordered]
    counts = [r['frames'] for r in ordered]
    receipts = []
    for path, frames in zip(paths, counts):
        receipt = json.loads((path.parent / 'capture.json').read_text(encoding='utf-8-sig'))
        if not receipt['memoryOnly'] or not receipt.get('recorded') or receipt.get('renderedFrames') != frames or receipt['frames'] != frames or receipt['audioPeak'] <= 0:
            raise RuntimeError('Unverified capture: ' + str(path))
        verify(ffprobe, ffmpeg, path, frames)
        meter = subprocess.run([str(ffmpeg), '-v', 'info', '-i', str(path), '-vn', '-af', 'volumedetect', '-f', 'null', '-'],capture_output=True,text=True,encoding='utf-8',check=True)
        peak = float(re.findall(r'max_volume:\s+([-\d.]+)',meter.stderr)[-1])
        if peak < -70 or peak + args.gain_db >= -.5:
            raise RuntimeError('Silent audio or insufficient mastering headroom: ' + str(path))
        receipts.append(receipt)
    if len({x['sourceHash'] for x in receipts}) != 1:
        raise RuntimeError('Takes have different source revisions; record all seven again.')
    listing = output / 'takes.txt'
    listing.write_text('\n'.join("file '" + str(p).replace('\\','/').replace("'", "'\\''") + "'" for p in paths), encoding='utf-8')
    target = output / ((job.get('videoName') or 'Teaser_'+args.version+'_1920x1080_30fps')+'.mp4')
    candidate = target.with_suffix('.pending.mp4')
    audio_filter=('acompressor=threshold=0.03:ratio=3:attack=12:release=180:makeup=2.5,' if args.finish_v03 else '')+'volume='+str(args.gain_db)+'dB'
    if args.finish_v03:audio_filter+=',alimiter=limit=0.89:level=false:latency=true'
    # The URP capture has a one-frame neutral-colour tail when its blackout pass
    # becomes active. Finishing closes the authored fade at the exact delivery frame.
    video_filter='fade=t=out:st='+str(seconds-.4)+':d=0.2' if args.finish_v03 else None
    run([ffmpeg, '-y', '-v', 'error', '-f', 'concat', '-safe', '0', '-i', listing,
         '-map', '0:v:0', '-map', '0:a:0', *(['-vf',video_filter] if video_filter else []), '-c:v', 'libx264', '-preset', 'medium', '-crf', '18',
         '-threads', '2', '-pix_fmt', 'yuv420p', '-r', '30', '-frames:v', str(expected), '-c:a', 'aac',
         '-b:a', '192k', '-ar', '48000', '-ac', '2', '-af', audio_filter, '-t', str(seconds), '-movflags', '+faststart', candidate])
    data = verify(ffprobe, ffmpeg, candidate, expected)
    if abs(float(data['format']['duration']) - seconds) > .05:
        raise RuntimeError('Unexpected final duration')
    candidate.replace(target)
    stats = subprocess.run([str(ffmpeg), '-v', 'info', '-i', str(target), '-vn', '-af', 'volumedetect', '-f', 'null', '-'], capture_output=True, text=True, encoding='utf-8')
    (output / 'audio-analysis.txt').write_text(stats.stderr, encoding='utf-8')
    score_tool='tools/teaser_v03_score.py' if args.version.startswith('v03') else 'tools/cozy_audio.py'
    (output / 'ATTRIBUTION.md').write_text('Aircraft geometry: Airplane - Poly by Google, CC BY 3.0.\nhttps://poly.pizza/m/a3XrQkLNna9\nhttps://creativecommons.org/licenses/by/3.0/\nAdaptations: section separation, metric scale, damage, muted colours, weathering.\nMusic: original procedurally synthesized QuietCamp score ('+score_tool+').\nGame sound remains part of the Unity recording. No external music overlay.\n', encoding='utf-8')
    result = dict(file=str(target), bytes=target.stat().st_size, sha256=hashlib.sha256(target.read_bytes()).hexdigest(),
                  frames=expected, seconds=float(data['format']['duration']), sourceHash=receipts[0]['sourceHash'],
                  codec='h264',width=1920,height=1080,fps=30,channels=2,sampleRate=48000,fullyDecoded=True,masterGainDb=args.gain_db,masterAudioFilter=audio_filter,videoFinishingFilter=video_filter)
    (output / 'export-verification.json').write_text(json.dumps(result, indent=2), encoding='utf-8')
    print(json.dumps(result, indent=2))


if __name__ == '__main__':
    main()
