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
import ctypes

ROOT = Path(__file__).resolve().parents[1]
QA = Path('C:/Users/Public/qcui-20261006/QuietCamp')
STAGE = QA / 'UserSettings/QuietCampPromoCapture/2026-10-15-AudioVerified'
TOOLS = QA / 'UserSettings/QuietCampPromoTools'


def available_memory_mb():
    if os.name!='nt':return 4096
    class Memory(ctypes.Structure):
        _fields_=[('length',ctypes.c_ulong),('load',ctypes.c_ulong)]+[(n,ctypes.c_ulonglong) for n in ['total','available','pageTotal','pageAvailable','virtualTotal','virtualAvailable','extended']]
    state=Memory();state.length=ctypes.sizeof(state);ctypes.windll.kernel32.GlobalMemoryStatusEx(ctypes.byref(state))
    return state.available//1048576


def trim_selected_editor(pid):
    """Release resident cache pages of the explicitly selected Editor, never other apps."""
    if os.name!='nt' or not pid:return
    from ctypes import wintypes
    kernel=ctypes.windll.kernel32;psapi=ctypes.windll.psapi
    kernel.OpenProcess.argtypes=[wintypes.DWORD,wintypes.BOOL,wintypes.DWORD];kernel.OpenProcess.restype=wintypes.HANDLE
    kernel.QueryFullProcessImageNameW.argtypes=[wintypes.HANDLE,wintypes.DWORD,wintypes.LPWSTR,ctypes.POINTER(wintypes.DWORD)]
    kernel.CloseHandle.argtypes=[wintypes.HANDLE];psapi.EmptyWorkingSet.argtypes=[wintypes.HANDLE]
    handle=kernel.OpenProcess(0x100|0x400,False,pid)
    if not handle:return
    try:
        path=ctypes.create_unicode_buffer(2048);length=wintypes.DWORD(2048)
        if kernel.QueryFullProcessImageNameW(handle,0,path,ctypes.byref(length)) and Path(path.value).name.lower()=='unity.exe':
            psapi.EmptyWorkingSet(handle);time.sleep(.5)
    finally:kernel.CloseHandle(handle)


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
        if status.get('status') == 'opening-game-ready' and status['id'] == ident and status.get('transitionIdle', True):
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
    parser.add_argument('--art-stage',type=Path,help='Editor-only staged art JSON directory; published puzzles and art remain untouched.')
    parser.add_argument('--start',type=int,default=1,choices=range(1,101),help='First campaign order; --count is the final order, inclusive.')
    parser.add_argument('--stills',action='store_true',help='Two actual Game View PNGs with the same input replay; no movie or recorded-audio claim.')
    parser.add_argument('--editor-pid',type=int,help='Explicit existing Editor for focus and low-memory working-set cleanup; no process is started or closed.')
    args = parser.parse_args()
    if args.stage:STAGE=args.stage.resolve()
    STAGE.mkdir(parents=True, exist_ok=True)
    ids = json.loads((QA/'Assets/QuietCamp/Resources/QuietCamp/campaign.json').read_text(encoding='utf-8'))['campaignLevelIds'][:args.count]
    assert args.start<=len(ids)
    art_stage=args.art_stage.resolve() if args.art_stage else None
    if art_stage:
        assert all((art_stage/(ident.replace(':','_')+'.json')).is_file() for ident in ids[args.start-1:]), 'Incomplete staged art'
    transition = json.loads((QA/'Assets/QuietCamp/Resources/QuietCamp/transition.json').read_text(encoding='utf-8'))
    reveal_settle = float(transition['revealDuration']) + float(transition['coveredHold']) + .8
    if available_memory_mb()<1500:
        if request('authoring-status').get('playing'):raise RuntimeError('Low memory; refusing to disturb active Play Mode.')
        request('authoring-trim')
        if available_memory_mb()<1000:trim_selected_editor(args.editor_pid)
        # Windows returns released cache pages asynchronously. Give that cleanup
        # time to settle before declaring a resumable low-memory failure.
        for _ in range(12):
            if available_memory_mb()>=750:break
            time.sleep(.5)
        if available_memory_mb()<750:raise RuntimeError('Low memory after Editor cleanup; capture can be resumed without losing verified frames.')
    before = persisted()
    workspace = request('authoring-workspace-status')
    state = dict(status='recording', levels=[])
    source_files = [Path('Assets/QuietCamp/Editor/LevelAuthoring')/name for name in ['CampaignPromoRecorder.cs', 'OpeningCampaignGameplayProbe.cs']]
    source_files += [Path('Assets/QuietCamp')/name for name in ['Scripts/Infrastructure/CampArtDefinition.cs','Scripts/Infrastructure/EditorAuthoringPreview.cs','Scripts/Presentation/World/CameraFitter.cs','Scripts/Presentation/World/CampArtView.cs','Editor/LevelAuthoring/LevelAuthoringPlayDriver.cs']]
    source_files += [Path('Assets/QuietCamp')/name for name in ['Scripts/Infrastructure/FrozenLandscape.cs','Scripts/Infrastructure/RiverEnvironmentProfile.cs','Scripts/Infrastructure/RiverSurfaceField.cs','Scripts/Infrastructure/RiverLandscapeGeometry.cs','Scripts/Infrastructure/RiverBankEcology.cs','Scripts/Presentation/World/RiverWeatherMaterial.cs','Scripts/Presentation/World/SeasonAtmosphereView.cs','Resources/QuietCamp/river_environment.json']]
    source_files += [Path('Assets/QuietCamp/Resources/QuietCamp/CampArtLit.shader')]
    source_files += [Path('Assets/QuietCamp/EditorBridge/Editor/MicroVerseRiverTerrainAdapter.cs')]
    source_files += [Path('Assets/QuietCamp')/name for name in ['Scripts/Presentation/World/RiverReflectionView.cs','Scripts/Presentation/QuietCamp.Presentation.asmdef']]
    source_files += [Path('Assets/QuietCamp/Scripts/Presentation/World')/name for name in ['CampGroundDetails.cs','MenuSceneHost.cs','MenuDiorama.cs','CampAtmosphere.cs','CampArtEdgeBlur.cs','SeasonVisibilityPass.cs']]
    source = {str(p): sha(QA/p) for p in source_files}
    (STAGE/'before.json').write_text(json.dumps(dict(progress=before, workspace=workspace, captureSources=source), indent=2), encoding='utf-8')
    try:
        start_values=dict(name='start',level=ids[args.start-1])
        if art_stage:start_values['output']=art_stage
        request('opening-gameplay', **start_values)
        for order, ident in list(enumerate(ids, 1))[args.start-1:]:
            status = ready(ident)
            # UiReady precedes the normal scene reveal. Do not drag against its
            # moving camera or send Next while the router still owns navigation.
            if 'transitionIdle' not in status:time.sleep(reveal_settle)
            if order == args.start:
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
            inputs = dict(levelSha256=sha(level), artSha256=sha(art_stage/(safe+'.json') if art_stage else art), sources=source)
            if art_stage:inputs['publishedArtSha256']=sha(art)
            prior = json.loads(receipt.read_text(encoding='utf-8')) if receipt.exists() else {}
            required=[('gameplay.png','imageSha256'),('complete.png','completeSha256')] if args.stills else [('gameplay.mp4','movieSha256'),('gameplay.png','imageSha256')]
            reuse = (prior.get('status') == 'captured' and prior.get('inputs') == inputs and
                     all((folder/name).exists() and sha(folder/name) == prior.get(key) for name, key in required))
            if reuse:
                if not args.stills:prior['media'] = verify_movie(folder/'gameplay.mp4')
                result = request('opening-gameplay', name='check')
                assert result['solved'] and result['recorded']
                captured = prior
            else:
                receipt.unlink(missing_ok=True)
                request('authoring-focus-game')
                if not args.stills:
                    focus=subprocess.run([sys.executable,str(ROOT/'tools/focus_existing_game_view.py'),'--pid',str(args.editor_pid or 18660),'--allow-docked'],capture_output=True,text=True,encoding='utf-8')
                    if focus.returncode:raise RuntimeError(focus.stdout+focus.stderr)
                request('opening-gameplay', name='composition-stills' if args.stills else 'promo', output=folder)
                limit = time.monotonic() + 300
                while not receipt.exists() and time.monotonic() < limit:
                    time.sleep(.5)
                if not receipt.exists():
                    raise RuntimeError('Recording timed out: '+ident)
                captured = json.loads(receipt.read_text(encoding='utf-8'))
                assert captured['status'] == 'captured', captured
                assert captured['id'] == ident and captured['completed'] and captured['memoryOnly'] and captured['stillHasPlacementGrid']
                if not args.stills:assert captured['playingAtmosphereVoices'] > 0, 'No live preset ambience: '+ident
                captured.update(order=order, inputs=inputs,imageSha256=sha(folder/'gameplay.png'))
                if args.stills:
                    from PIL import Image
                    for name in ['gameplay.png','complete.png']:
                        with Image.open(folder/name) as frame:assert frame.size==(1440,1080)
                    captured.update(completeSha256=sha(folder/'complete.png'),media=dict(kind='two-game-view-stills',width=1440,height=1080))
                else:captured.update(movieSha256=sha(folder/'gameplay.mp4'),media=verify_movie(folder/'gameplay.mp4'))
                captured['cameraBefore']=captured.get('cameraBefore') or status.get('cameraProof')
                captured['cameraAfter']=captured.get('cameraAfter') or request('opening-gameplay',name='status').get('cameraProof')
                if art_stage:
                    for proof in [captured['cameraBefore'],captured['cameraAfter']]:
                        assert proof and proof['boardFraming'] and proof['fieldContained'] and proof['groundContained'], 'Staged framing failed: '+ident
                        assert max(abs(v) for v in proof['centreErrorPixels'])<8, 'Field is not centred: '+ident
                receipt.write_text(json.dumps(captured, ensure_ascii=False, indent=2)+'\n', encoding='utf-8')
            state['levels'].append(captured)
            (STAGE/'session.json').write_text(json.dumps(state, ensure_ascii=False, indent=2), encoding='utf-8')
            label='placement + completed Game View PNGs' if args.stills else f'grid + real placement + game audio, {captured["media"]["duration"]:.2f}s'
            print(f'{order:03}: recorded {ident}, {label}' + (' (reused)' if reuse else ''), flush=True)
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
