"""Assemble verified Unity Recorder takes. Audio comes exclusively from each in-engine take."""
import argparse
import hashlib
import json
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


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--project', type=Path, required=True)
    parser.add_argument('--verify-only', type=Path)
    args = parser.parse_args()
    project = args.project.resolve()
    tools = project / 'UserSettings/QuietCampPromoTools'
    ffmpeg, ffprobe = tools / 'ffmpeg.exe', tools / 'ffprobe.exe'
    if args.verify_only:
        data = probe(ffprobe, args.verify_only)
        print(json.dumps(data, indent=2))
        return
    output = project / 'Marketing/Exports'
    paths = [output / 'Shots' / name / 'gameplay.mp4' for name in ['01-hook','02-world','03-play','04-care','05-rain','06-traces','07-reveal']]
    counts = [150,180,180,180,180,180,210]
    receipts = []
    for path, frames in zip(paths, counts):
        receipt = json.loads((path.parent / 'capture.json').read_text(encoding='utf-8-sig'))
        if not receipt['memoryOnly'] or receipt['frames'] != frames or receipt['audioPeak'] <= 0:
            raise RuntimeError('Unverified capture: ' + str(path))
        verify(ffprobe, ffmpeg, path, frames)
        receipts.append(receipt)
    if len({x['sourceHash'] for x in receipts}) != 1:
        raise RuntimeError('Takes have different source revisions; record all seven again.')
    listing = output / 'takes.txt'
    listing.write_text('\n'.join("file '" + str(p).replace('\\','/').replace("'", "'\\''") + "'" for p in paths), encoding='utf-8')
    target = output / 'Teaser_v01_1920x1080_30fps.mp4'
    run([ffmpeg, '-y', '-v', 'error', '-f', 'concat', '-safe', '0', '-i', listing,
         '-map', '0:v:0', '-map', '0:a:0', '-c:v', 'libx264', '-preset', 'medium', '-crf', '18',
         '-threads', '2', '-pix_fmt', 'yuv420p', '-r', '30', '-frames:v', '1260', '-c:a', 'aac',
         '-b:a', '192k', '-ar', '48000', '-ac', '2', '-t', '42', '-movflags', '+faststart', target])
    data = verify(ffprobe, ffmpeg, target, 1260)
    if abs(float(data['format']['duration']) - 42) > .05:
        raise RuntimeError('Unexpected final duration')
    stats = subprocess.run([str(ffmpeg), '-v', 'info', '-i', str(target), '-vn', '-af', 'volumedetect', '-f', 'null', '-'], capture_output=True, text=True, encoding='utf-8')
    (output / 'audio-analysis.txt').write_text(stats.stderr, encoding='utf-8')
    (output / 'ATTRIBUTION.md').write_text('Aircraft geometry: Airplane — Poly by Google, CC BY 3.0.\nhttps://poly.pizza/m/a3XrQkLNna9\nhttps://creativecommons.org/licenses/by/3.0/\nAdaptations: section separation, metric scale, damage, muted colours, weathering.\nMusic: original procedurally synthesized QuietCamp score (tools/cozy_audio.py).\nGame sound remains part of the Unity recording. No external music overlay.\n', encoding='utf-8')
    result = dict(file=str(target), bytes=target.stat().st_size, sha256=hashlib.sha256(target.read_bytes()).hexdigest(),
                  frames=1260, seconds=float(data['format']['duration']), sourceHash=receipts[0]['sourceHash'],
                  codec='h264',width=1920,height=1080,fps=30,channels=2,sampleRate=48000,fullyDecoded=True)
    (output / 'export-verification.json').write_text(json.dumps(result, indent=2), encoding='utf-8')
    print(json.dumps(result, indent=2))


if __name__ == '__main__':
    main()
