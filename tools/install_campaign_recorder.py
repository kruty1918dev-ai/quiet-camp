"""Prepare the official Unity Recorder; repair one verified missing local package .meta."""
import argparse
import hashlib
import json
from pathlib import Path
import uuid

parser = argparse.ArgumentParser(description=__doc__)
parser.add_argument('--project', type=Path, required=True)
parser.add_argument('--repair-vfx-metadata', action='store_true')
args = parser.parse_args()
root = args.project.resolve()
if args.repair_vfx_metadata:
    source = Path('Runtime/Utilities/Playables/VisualEffectActivationDeprecated/VisualEffectActivationTrackMigration.cs')
    repaired = 0
    for folder in (root/'Library/PackageCache').glob('com.unity.visualeffectgraph@*'):
        definition = json.loads((folder/'package.json').read_text(encoding='utf-8'))
        if definition['version'] != '17.6.0':
            continue
        script = folder/source
        metadata = Path(str(script)+'.meta')
        if metadata.exists():
            print('VFX migration metadata already present.'); continue
        if hashlib.sha256(script.read_bytes()).hexdigest() != '2c72de8fd592d9fae4401bc4d9ad7e05fe542dc116210b8f8b159ce2b336d5c9':
            raise SystemExit('Unknown vendor source: refusing to apply the missing-metadata repair.')
        guid = uuid.uuid5(uuid.NAMESPACE_URL, 'https://unity.com/com.unity.visualeffectgraph/17.6.0/VisualEffectActivationTrackMigration.cs').hex
        metadata.write_text('fileFormatVersion: 2\nguid: '+guid+'\nMonoImporter:\n  externalObjects: {}\n  serializedVersion: 2\n  defaultReferences: []\n  executionOrder: 0\n  icon: {instanceID: 0}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n', encoding='utf-8')
        repaired += 1
    print('Repaired missing import metadata in local package cache:', repaired, '(no vendor C# or Unity installation changes).')
else:
    manifest = root/'Packages/manifest.json'
    data = json.loads(manifest.read_text(encoding='utf-8'))
    data['dependencies']['com.unity.recorder'] = '5.1.7'
    manifest.write_text(json.dumps(data, indent=2)+'\n', encoding='utf-8')
    print('Unity Recorder 5.1.7 requested. Refresh the existing Editor; if VFX migration fails, run --repair-vfx-metadata and Refresh again.')
