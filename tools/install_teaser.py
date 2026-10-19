"""Sync owned teaser sources into an existing QA project, preserving local UPM mappings."""
import argparse
import json
from pathlib import Path
import shutil

ROOT = Path(__file__).resolve().parents[1]


def install(project):
    source = ROOT / 'QuietCamp'
    packages = project / 'Packages'
    manifest = packages / 'manifest.json'
    data = json.loads(manifest.read_text(encoding='utf-8-sig'))
    lock = json.loads((packages / 'packages-lock.json').read_text(encoding='utf-8-sig'))['dependencies']
    for key, value in list(data['dependencies'].items()):
        if value.startswith('file:') and not (packages / value[5:]).exists():
            previous = lock.get(key, {}).get('version', '')
            if previous.startswith('file:') and (packages / previous[5:]).exists():
                data['dependencies'][key] = previous
            elif (source / 'Packages' / key / 'package.json').exists():
                data['dependencies'][key] = 'file:' + (source / 'Packages' / key).as_posix()
            else:
                raise SystemExit('Unresolved existing local package: ' + key)
    data['dependencies'].update({'com.unity.cinemachine': '3.1.7', 'com.unity.timeline': '6.6.0', 'com.unity.recorder': '5.1.7'})
    manifest.write_text(json.dumps(data, indent=2) + '\n', encoding='utf-8')
    for relative in ['Assets/Marketing/Teaser', 'Assets/QuietCamp/Editor/LevelAuthoring/WorldOps/WorldOpsBridge.cs']:
        src = source / relative
        dst = project / relative
        if src.is_dir():
            for file in src.rglob('*'):
                if not file.is_file():
                    continue
                relative_file = file.relative_to(src)
                target = dst / relative_file
                # Existing cameras, scene, curves and config are user-authored.
                # Installing code must never reset framing or Timeline edits.
                if target.exists() and relative_file.parts[0] not in ('Editor', 'Documentation'):
                    continue
                target.parent.mkdir(parents=True, exist_ok=True)
                shutil.copy2(file, target)
        else:
            shutil.copy2(src, dst)
    print('Teaser sources synchronized; local package mappings preserved. Refresh the existing Editor.')


def collect(project):
    source = project / 'Assets/Marketing'
    destination = ROOT / 'QuietCamp/Assets/Marketing'
    for file in source.rglob('*'):
        if file.is_file() and file.suffix in ('.asset', '.prefab', '.anim', '.unity', '.playable', '.meta'):
            target = destination / file.relative_to(source)
            target.parent.mkdir(parents=True, exist_ok=True)
            shutil.copy2(file, target)
    parent_meta = project / 'Assets/Marketing.meta'
    if parent_meta.exists():
        shutil.copy2(parent_meta, destination.with_suffix('.meta'))
    print('Collected authored teaser assets and GUIDs; code and documentation were preserved.')


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--project', required=True, type=Path)
    parser.add_argument('--collect', action='store_true', help='Collect edited Unity assets and generated metadata back to the repository')
    args = parser.parse_args()
    (collect if args.collect else install)(args.project.resolve())
