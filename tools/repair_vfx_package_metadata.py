"""Restore missing metadata for the shipped VFX Timeline migration source; no source patch."""
import argparse
from pathlib import Path
import uuid

p = argparse.ArgumentParser(description=__doc__)
p.add_argument('--project', type=Path, required=True)
p.add_argument('--apply', action='store_true')
a = p.parse_args()
relative = Path('Runtime/Utilities/Playables/VisualEffectActivationDeprecated/VisualEffectActivationTrackMigration.cs')
packages = list((a.project / 'Library/PackageCache').glob('com.unity.visualeffectgraph@*'))
repaired = 0
for package in packages:
    source = package / relative
    meta = Path(str(source) + '.meta')
    if not source.is_file() or meta.exists():
        continue
    if not a.apply:
        print('Missing VFX Timeline migration metadata:', meta)
        continue
    # Stable identity, local package cache only. Do not edit the Unity installation.
    guid = uuid.uuid5(uuid.NAMESPACE_URL, 'com.unity.visualeffectgraph/' + relative.as_posix()).hex
    with meta.open('x', encoding='utf-8', newline='\n') as out:
        out.write('fileFormatVersion: 2\nguid: ' + guid + '\nMonoImporter:\n  externalObjects: {}\n  serializedVersion: 2\n  defaultReferences: []\n  executionOrder: 0\n  icon: {instanceID: 0}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n')
    repaired += 1
print('VFX metadata restored:', repaired)
