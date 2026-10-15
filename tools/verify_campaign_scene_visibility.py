"""Check published scenery through Boot -> Menu -> Continue/Next in the existing QA Editor.

RAM-only saves, no staged art or player builds. Private frames are regression
evidence, not newly accepted artwork. Run one serial batch at a time.
"""
import argparse
import json
import time
from pathlib import Path
from PIL import Image, ImageStat

from capture_campaign_promo import QA, persisted, ready, request, sha


def menu_ready():
    deadline = time.monotonic() + 120
    while time.monotonic() < deadline:
        status = request('opening-gameplay', name='menu-status')
        if status.get('ready'):
            return status
        time.sleep(.5)
    raise RuntimeError('The ordinary menu did not become ready.')


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--first', type=int, default=1)
    parser.add_argument('--last', type=int, default=100)
    parser.add_argument('--simulator', action='store_true', help='Use the already open Device Simulator; never open another window.')
    parser.add_argument('--output', type=Path, required=True)
    args = parser.parse_args()
    content = QA / 'Assets/QuietCamp/Resources/QuietCamp'
    ids = json.loads((content/'campaign.json').read_text(encoding='utf-8'))['campaignLevelIds']
    assert 1 <= args.first <= args.last <= len(ids)
    args.output.mkdir(parents=True, exist_ok=True)
    before = persisted()
    source_files = [QA/'Assets/QuietCamp'/name for name in [
        'Scripts/Presentation/World/BoardRenderer.cs', 'Scripts/Presentation/World/CampArtView.cs',
        'Scripts/Presentation/World/CameraFitter.cs', 'Scripts/Presentation/World/CampAtmosphere.cs',
        'Scripts/Presentation/UI/CampHud.cs', 'Resources/QuietCamp/Html/CampGame.css.txt',
        'Scripts/Presentation/World/PlacementTargetProjector.cs',
        'Scripts/Presentation/World/MenuSceneHost.cs', 'Scripts/Presentation/World/CampSceneHost.cs',
        'Scripts/Presentation/World/RiverReflectionView.cs', 'Scripts/Presentation/FoliageDiveTransition.cs',
        'Editor/LevelAuthoring/OpeningCampaignGameplayProbe.cs', 'Editor/LevelAuthoring/LevelAuthoringPlayDriver.cs']]
    sources = {str(p.relative_to(QA)): sha(p) for p in source_files}
    results = []
    report = {'status': 'running', 'artOverride': False, 'playerBuild': False, 'existingSimulator': args.simulator, 'sources': sources, 'levels': results}
    try:
        request('opening-gameplay', name='start-menu', level=ids[args.first-1])
        menu_ready()
        if args.simulator:
            request('opening-gameplay', name='simulator')
            time.sleep(.5)
        request('opening-gameplay', name='menu-continue')
        for order in range(args.first, args.last+1):
            ident = ids[order-1]
            status = ready(ident)
            assert status['memoryOnly'] and not status['previewArtOverride'], status
            assert status['sceneryActive'] and status['artModels'] > 0 and status['artInstances'] > 0, status
            assert status['cameraMask'] != 0 and status['inFrustumSceneryRenderers'] > 0, status
            assert status['visibleGridCells'] > 0 and status['residents'] == 0, status
            proof=status['cameraProof']
            assert proof['playableFieldContained'], 'The field leaves the gameplay viewport: '+ident
            assert max(abs(v) for v in proof['playableCentreErrorPixels'])<2, 'The field is not centred in the usable viewport: '+ident
            assert max(abs(v) for v in proof['frameCentreErrorPixels'])<2, 'The field is not centred in the actual frame: '+ident
            assert abs(status['cameraEuler'][0]-58)<.01, 'Historical gameplay camera pitch was overridden: '+ident
            image = args.output/f'{order:03}.png'
            image.unlink(missing_ok=True)
            request('opening-gameplay', name='frame', output=image.resolve())
            deadline = time.monotonic()+10
            while not image.exists() and time.monotonic() < deadline:
                time.sleep(.1)
            assert image.exists(), image
            with Image.open(image) as frame:
                x,y,w,h=status['cameraProof']['gameplay']
                region=frame.crop((int((x+w*.05)*frame.width),int((1-y-h*.95)*frame.height),int((x+w*.95)*frame.width),int((1-y-h*.05)*frame.height)))
                status['gameplayPixelStdDev']=ImageStat.Stat(region.convert('RGB').resize((64,64))).stddev
                assert max(status['gameplayPixelStdDev'])>2, 'The gameplay area is nearly a solid background: '+ident
            status['frameSha256'] = sha(image)
            safe=ident.replace(':','_')+'.json'
            status['publishedArtSha256']=sha(content/'ArtCompositions'/safe)
            status['puzzleSha256']=sha(content/('GeneratedLevels' if ':' in ident else 'Levels')/safe)
            results.append(status)
            (args.output/'visibility.json').write_text(json.dumps(report, indent=2), encoding='utf-8')
            print(json.dumps({'order': order, 'id': ident, 'scenery': status['inFrustumSceneryRenderers'], 'grid': status['visibleGridCells'], 'mask': status['cameraMask']}), flush=True)
            if order < args.last:
                checked = request('opening-gameplay', name='check')
                assert checked['solved'] and checked['recorded'], checked
                request('opening-gameplay', name='next')
        report['status'] = 'passed'
    except Exception as error:
        report.update(status='failed', error=str(error))
        raise
    finally:
        request('authoring-stop')
        report['persistentPlayerDataUnchanged'] = persisted() == before
        report['sourcesUnchanged'] = sources == {str(p.relative_to(QA)): sha(p) for p in source_files}
        (args.output/'visibility.json').write_text(json.dumps(report, indent=2), encoding='utf-8')
        assert report['persistentPlayerDataUnchanged'], 'Persistent player data changed.'
        assert report['sourcesUnchanged'], 'Captured sources changed during the run.'


if __name__ == '__main__':
    main()
