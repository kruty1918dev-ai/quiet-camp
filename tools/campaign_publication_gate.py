"""Read-only acceptance gate for the 100 native campaign compositions."""
import hashlib,json
from campaign_capture_receipt import current
from pathlib import Path

ROOT=Path(__file__).resolve().parents[1]
QA=Path('C:/Users/Public/qcui-20261006/QuietCamp')
REVIEW=ROOT/'Design/LevelAuthoring/StoryCampaign/Review'

def sha(path):return hashlib.sha256(path.read_bytes()).hexdigest()

def require_review(final_round):
    count=int(final_round.removeprefix('U'))
    if count<7:raise ValueError('At least seven inspected correction cycles are required before publication.')
    compositions={n:set() for n in range(1,101)}
    for revision in range(count+1):
        name=f'U{revision:02}';folder=REVIEW/name
        path=folder/'inspection.json'
        if not path.exists():raise ValueError('Missing actual visual inspection: '+name)
        inspected=json.loads(path.read_text(encoding='utf-8'))['levels']
        if sorted(r['order'] for r in inspected)!=list(range(1,101)) or not all(r.get('inspected') for r in inspected):
            raise ValueError('Visual inspection must cover all 100 native frames: '+name)
        if revision:
            corrections=json.loads((folder/'corrections.json').read_text(encoding='utf-8'))
            if sorted(r['order'] for r in corrections)!=list(range(1,101)):
                raise ValueError('Missing individual correction evidence: '+name)
        for row in inspected:
            stem=f'L{row["order"]:03}';image=folder/(stem+'.png');receipt=folder/(stem+'.json')
            if not image.exists() or not receipt.exists():raise ValueError('Missing native capture: '+name+'/'+stem)
            native=json.loads(receipt.read_text(encoding='utf-8'))
            if native.get('kind')!='native-unity-production-world-components' or native.get('imageSha256')!=sha(image) or native.get('shaderErrors') or native.get('errors'):
                raise ValueError('Invalid native capture receipt: '+name+'/'+stem)
            if row.get('imageSha256')!=sha(image):raise ValueError('Inspection does not identify the current native image: '+name+'/'+stem)
            art_hash=native.get('artSha256')
            if not art_hash or art_hash in compositions[row['order']]:raise ValueError('Unchanged composition cannot count as another correction: '+name+'/'+stem)
            compositions[row['order']].add(art_hash)
            if revision==count:
                draft=QA/'UserSettings/QuietCampCampaignReview'/name/(stem+'.draft.json')
                if not current(QA,image,receipt,draft):raise ValueError('Final frame uses stale source files or assemblies: '+stem)
                if row.get('status')!='accepted' or row.get('residual') or row.get('imageSha256')!=sha(image) or row.get('draftSha256')!=sha(draft):
                    raise ValueError('Final composition has not been accepted at its current image and draft hashes: '+stem)
    return count

if __name__=='__main__':
    import argparse
    p=argparse.ArgumentParser();p.add_argument('--round',required=True);a=p.parse_args()
    print('Inspected correction cycles:',require_review(a.round))
