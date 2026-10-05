"""Assemble Unity-rendered frames, or rasterize exported meshes; requires Pillow."""
import argparse
import array
import json
import shutil
import subprocess
import wave
from pathlib import Path
from PIL import Image, ImageDraw

parser = argparse.ArgumentParser(description=__doc__)
parser.add_argument('--meshes', type=Path, default=Path('/tmp/leaf-preview'))
parser.add_argument('--video', action='store_true', help='Build an MP4 with the transition cues (requires Unity PNG frames and ffmpeg)')
parser.add_argument('--ffmpeg', default=shutil.which('ffmpeg'))
args = parser.parse_args()
root = Path(__file__).resolve().parents[2]
size = (648, 1152)
old = Image.open(root / 'QuietCamp/Screenshots/01_main_menu.png').convert('RGB').resize(size)
new = Image.open(root / 'QuietCamp/Screenshots/04_camp_day.png').convert('RGB').resize(size)
frames = []
for path in sorted(args.meshes.glob('*.json')):
    rendered = path.with_suffix('.png')
    if rendered.exists():
        frames.append(Image.open(rendered).convert('RGB').resize((324, 576), Image.Resampling.LANCZOS))
        continue
    data = json.loads(path.read_text())
    frame = (new if data['revealed'] else old).copy()
    draw = ImageDraw.Draw(frame)
    vertices, colors, triangles = data['vertices'], data['colors'], data['triangles']
    points = [((v['x'] + 540) * .6, (960 - v['y']) * .6) for v in vertices]
    for i in range(0, len(triangles), 3):
        ids = triangles[i:i + 3]
        color = colors[ids[0]]
        draw.polygon([points[j] for j in ids], fill=tuple(color[k] for k in ('r', 'g', 'b')))
    frames.append(frame.resize((324, 576), Image.Resampling.LANCZOS))
if not frames:
    raise SystemExit('No exported meshes found')
out = root / 'Design/Transitions'
# Endpoint pauses are GIF-only, to make the looping review easier to follow.
durations = [20] * len(frames)
durations[0] = 450
durations[-1] = 650
frames[0].save(out / 'leaf-curtain-preview.gif', save_all=True, append_images=frames[1:],
               duration=durations, loop=0, disposal=2)
sheet = Image.new('RGB', (324 * 4, 576))
for j, fraction in enumerate((.20, .35, .53, .79)):
    sheet.paste(frames[round((len(frames) - 1) * fraction)], (j * 324, 0))
sheet.save(out / 'leaf-curtain-frames.png')
print(f'Rendered {len(frames)} independent Unity mesh frames')

if args.video:
    if not args.ffmpeg or not (args.meshes / '0000.png').exists():
        raise SystemExit('--video requires --ffmpeg and Unity-rendered PNG frames')
    cfg = json.loads((root / 'QuietCamp/Assets/QuietCamp/Resources/QuietCamp/transition.json').read_text())
    rate = 44100
    stereo = [0.0] * (round(len(frames) / 50 * rate) * 2)
    # Match the runtime guards at the 50 Hz preview frame rate. Entry uses
    # eased progress; reveal uses linear progress, as in FoliageDiveTransition.
    for name, marker, gain, pan, reveal in (
        ('foliage_enter', cfg['cueInMarker'], cfg['cueInGain'], cfg['cueInPan'], False),
        ('foliage_exit', cfg['cueOutMarker'], cfg['cueOutGain'], cfg['cueOutPan'], True),
    ):
        cue = 0
        for i in range(len(frames)):
            t = i / 50
            progress = ((t - cfg['coverDuration'] - cfg['coveredHold']) / cfg['revealDuration']
                        if reveal else min(1, t / cfg['coverDuration']))
            trigger = progress if reveal else progress * progress * (3 - 2 * progress)
            if trigger > marker:
                cue = round(t * rate)
                break
        with wave.open(str(root / f'QuietCamp/Assets/QuietCamp/Audio/Generated/{name}.wav'), 'rb') as sound:
            assert sound.getframerate() == rate and sound.getnchannels() == 1
            samples = array.array('h', sound.readframes(sound.getnframes()))
        for i, sample in enumerate(samples):
            target = (cue + i) * 2
            if target + 1 >= len(stereo):
                break
            stereo[target] += sample * .30 * gain * (1 - max(0, pan))
            stereo[target + 1] += sample * .30 * gain * (1 + min(0, pan))
    audio_path = out / 'leaf-curtain-sound.wav'
    with wave.open(str(audio_path), 'wb') as sound:
        sound.setparams((2, 2, rate, 0, 'NONE', 'not compressed'))
        sound.writeframes(array.array('h', (round(max(-32768, min(32767, v))) for v in stereo)).tobytes())
    subprocess.run([
        args.ffmpeg, '-y', '-loglevel', 'error', '-framerate', '50',
        '-i', str(args.meshes / '%04d.png'), '-i', str(audio_path),
        '-vf', 'tpad=start_duration=0.45:stop_duration=0.65:start_mode=clone:stop_mode=clone',
        '-af', 'adelay=450|450,apad=pad_dur=0.65', '-shortest',
        '-c:v', 'libx264', '-pix_fmt', 'yuv420p', '-crf', '20',
        '-c:a', 'aac', '-b:a', '160k', '-movflags', '+faststart',
        str(out / 'leaf-curtain-preview.mp4'),
    ], check=True)
