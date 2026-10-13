"""Capture tutorial pages through the existing Unity authoring request boundary."""
import argparse
import hashlib
import json
import os
from pathlib import Path
import subprocess
import sys
import time

ROOT = Path(__file__).resolve().parents[1]
parser = argparse.ArgumentParser(description=__doc__)
parser.add_argument('--pid', type=int, required=True)
parser.add_argument('--project', type=Path, required=True)
parser.add_argument('--pages', nargs='+', required=True, choices=['levels', 'field', 'objects', 'grass', 'fence', 'river', 'terrain', 'guests', 'checks'])
args = parser.parse_args()
mapping = {'levels': 0, 'field': 1, 'objects': 2, 'grass': 2, 'fence': 2, 'river': 3, 'terrain': 3, 'guests': 4, 'checks': 5}
request = args.project / 'Temp/ai/qc007-art-request.json'
result = args.project / 'Temp/ai/qc007-art-result.json'
for name in args.pages:
    assert not request.exists(), 'Wait for the current native operation before capturing.'
    started = time.time()
    payload = {'mode': 'authoring-page', 'seed': mapping[name]}
    if name in ['grass', 'fence']:
        payload['profile'] = name
    pending = request.with_suffix('.pending')
    pending.write_text(json.dumps(payload), encoding='utf-8'); os.replace(pending, request)
    while time.time() - started < 25:
        if not request.exists() and result.exists() and result.stat().st_mtime >= started:
            receipt = json.loads(result.read_text(encoding='utf-8'))
            if receipt.get('status') == 'error':
                raise RuntimeError(receipt.get('error'))
            if receipt.get('status') == 'authoring-page' and receipt.get('page') == mapping[name]:
                break
        time.sleep(.2)
    else:
        raise TimeoutError('Unity did not acknowledge the page selection.')
    time.sleep(.5)
    target = ROOT / 'Design/LevelAuthoring/images' / (name + '.png')
    subprocess.run([sys.executable, str(ROOT / 'tools/capture_authoring_window.py'), '--pid', str(args.pid), '--output', str(target)], check=True)
    print(name, hashlib.sha256(target.read_bytes()).hexdigest()[:16])
