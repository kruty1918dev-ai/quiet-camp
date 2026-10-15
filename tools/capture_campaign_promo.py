"""Record the 100 published levels in the already running Editor. No player build."""
import argparse
import hashlib
import json
import os
from pathlib import Path
import subprocess
import sys
import time
import re

ROOT = Path(__file__).resolve().parents[1]
QA = Path('C:/Users/Public/qcui-20261006/QuietCamp')
STAGE = QA / 'UserSettings/QuietCampPromoCapture/2026-10-15-AudioVerified'
TOOLS = QA / 'UserSettings/QuietCampPromoTools'


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def request(mode, **values):
    args = [sys.executable, str(ROOT / 'tools/request_authoring_props.py'), '--project', str(QA), '--mode', mode]
    for key, value in values.items():
        args += ['--' + key, str(value)]
    result = subprocess.run(args, capture_output=True, text=True, encoding='utf-8')
    if result.returncode:
        raise RuntimeError(result.stdout + result.stderr)
    return {} if mode == 'authoring-stop' else json.loads((QA / 'Temp/ai/qc007-art-result.json').read_text(encoding='utf-8'))


def persisted():
    base = Path(os.environ['USERPROFILE']) / 'AppData/LocalLow/kruty1918'
    return {str(p): sha(p) for name in ['Quiet Camp', 'Quiet Camp QA']
            for p in (base / name).rglob('*') if p.is_file() and p.suffix in ['.mvs', '.bak', '.txt', '.json']}


def ready(ident):
    limit = time.monotonic() + 180
    while time.monotonic() < limit:
        status = request('opening-gameplay', name='status')
        if status.get('status') == 'opening-game-ready' and status['id'] == ident:
            return status
        time.sleep(.4)
    raise RuntimeError('Camp did not become ready: ' + ident)


def verify_movie(path):
    result = subprocess.run([str(TOOLS/'ffprobe.exe'), '-v', 'error', '-count_packets', '-show_streams', '-show_format', '-of', 'json', str(path)], capture_output=True, text=True, encoding='utf-8', check=True)
    data = json.loads(result.stdout)
    video = next(s for s in data['streams'] if s['codec_type'] == 'video')
    audio = next(s for s in data['streams'] if s['codec_type'] == 'audio')
    assert video['width'] == 1440 and video['height'] == 1080
    assert 4 <= float(data['format']['duration']) <= 4.8, data['format']
    assert audio['sample_rate'] in ['44100', '48000'] and int(audio['channels']) == 2
    assert int(audio.get('nb_read_packets', 0)) >= 180, 'Audio track has no complete recording: '+str(path)
    decoded = subprocess.run([str(TOOLS/'ffmpeg.exe'), '-hide_banner', '-v', 'info', '-xerror', '-threads', '1', '-i', str(path), '-vn', '-af', 'volumedetect', '-f', 'null', '-'], capture_output=True, text=True, encoding='utf-8', check=True)
    mean = re.search(r'mean_volume: ([-\d.]+) dB', decoded.stderr)
    assert mean and float(mean[1]) > -60, 'Recorded audio is empty or silent: '+str(path)
    return dict(duration=float(data['format']['duration']), video=video['codec_name'], audio=audio['codec_name'],
                width=video['width'], height=video['height'], frames=video.get('nb_frames'), sampleRate=audio['sample_rate'], audioPackets=int(audio['nb_read_packets']), audioMeanDb=float(mean[1]))


def main():
    global STAGE
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--count', type=int, default=100, choices=range(1, 101))
    parser.add_argument('--stage',type=Path,help='Separate durable capture folder, for isolated recorder verification.')
    args = parser.parse_args()
    if args.stage:STAGE=args.stage.resolve()
    STAGE.mkdir(parents=True, exist_ok=True)
    ids = json.loads((QA/'Assets/QuietCamp/Resources/QuietCamp/campaign.json').read_text(encoding='utf-8'))['campaignLevelIds'][:args.count]
    transition = json.loads((QA/'Assets/QuietCamp/Resources/QuietCamp/transition.json').read_text(encoding='utf-8'))
    reveal_settle = float(transition['revealDuration']) + float(transition['coveredHold']) + .8
    before = persisted()
    workspace = request('authoring-workspace-status')
    state = dict(status='recording', levels=[])
    source_files = [Path('Assets/QuietCamp/Editor/LevelAuthoring')/name for name in ['CampaignPromoRecorder.cs', 'OpeningCampaignGameplayProbe.cs']]
    source = {str(p): sha(QA/p) for p in source_files}
    (STAGE/'before.json').write_text(json.dumps(dict(progress=before, workspace=workspace, captureSources=source), indent=2), encoding='utf-8')
    try:
        request('opening-gameplay', name='start')
        for order, ident in enumerate(ids, 1):
            status = ready(ident)
            # UiReady precedes the normal scene reveal. Do not drag against its
            # moving camera or send Next while the router still owns navigation.
            time.sleep(reveal_settle)
            if order == 1:
                grant = request('opening-gameplay', name='pro')
                assert grant['pro'] and grant['memoryOnly']
            assert status['memoryOnly'] and status['frozenArtMatches'] and status['surfaceSteps'] == 0
            assert source == {str(p): sha(QA/p) for p in source_files}, 'Sources changed during recording'
            folder = STAGE/f'L{order:03}'
            receipt = folder/'capture.json'
            content = QA/'Assets/QuietCamp/Resources/QuietCamp'
            safe = ident.replace(':', '_')
            level = content/('GeneratedLevels' if ident.startswith('gen:') else 'Levels')/(safe+'.json')
            art = content/'ArtCompositions'/(safe+'.json')
            inputs = dict(levelSha256=sha(level), artSha256=sha(art), sources=source)
            prior = json.loads(receipt.read_text(encoding='utf-8')) if receipt.exists() else {}
            reuse = (prior.get('status') == 'captured' and prior.get('inputs') == inputs and
                     all((folder/name).exists() and sha(folder/name) == prior.get(key) for name, key in [('gameplay.mp4','movieSha256'), ('gameplay.png','imageSha256')]))
            if reuse:
                prior['media'] = verify_movie(folder/'gameplay.mp4')
                result = request('opening-gameplay', name='check')
                assert result['solved'] and result['recorded']
                captured = prior
            else:
                receipt.unlink(missing_ok=True)
                request('authoring-focus-game')
                subprocess.run([sys.executable,str(ROOT/'tools/focus_existing_game_view.py'),'--pid','18660'],check=True,capture_output=True)
                request('opening-gameplay', name='promo', output=folder)
                limit = time.monotonic() + 300
                while not receipt.exists() and time.monotonic() < limit:
                    time.sleep(.5)
                if not receipt.exists():
                    raise RuntimeError('Recording timed out: '+ident)
                captured = json.loads(receipt.read_text(encoding='utf-8'))
                assert captured['status'] == 'captured', captured
                assert captured['id'] == ident and captured['completed'] and captured['memoryOnly'] and captured['stillHasPlacementGrid']
                assert captured['playingAtmosphereVoices'] > 0, 'No live preset ambience: '+ident
                captured.update(order=order, inputs=inputs, movieSha256=sha(folder/'gameplay.mp4'), imageSha256=sha(folder/'gameplay.png'), media=verify_movie(folder/'gameplay.mp4'))
                receipt.write_text(json.dumps(captured, ensure_ascii=False, indent=2)+'\n', encoding='utf-8')
            state['levels'].append(captured)
            (STAGE/'session.json').write_text(json.dumps(state, ensure_ascii=False, indent=2), encoding='utf-8')
            print(f'{order:03}: recorded {ident}, grid + real placement + game audio, {captured["media"]["duration"]:.2f}s' + (' (reused)' if reuse else ''), flush=True)
            if order < len(ids):
                result = request('opening-gameplay', name='next')
                assert result['to'] == ids[order]
        state['status'] = 'captured'
    finally:
        if request('authoring-status').get('playing'):
            request('authoring-stop')
        limit = time.monotonic() + 120
        while request('authoring-status').get('playing') and time.monotonic() < limit:
            time.sleep(.5)
        restored = request('authoring-workspace-status')
        assert before == persisted(), 'Persisted progress changed'
        assert all(workspace.get(k) == restored.get(k) for k in ['status', 'artSha256', 'objects']), 'Workspace changed'
        state.update(progressUnchanged=True, workspaceUnchanged=True)
        (STAGE/'session.json').write_text(json.dumps(state, ensure_ascii=False, indent=2)+'\n', encoding='utf-8')
    print(f'Captured {len(state["levels"])} real gameplay clips. Progress and workspace unchanged.', flush=True)


if __name__ == '__main__':
    main()
