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
            shutil.copytree(src, dst, dirs_exist_ok=True)
        else:
            shutil.copy2(src, dst)
    print('Teaser sources synchronized; local package mappings preserved. Refresh the existing Editor.')


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--project', required=True, type=Path)
    install(parser.parse_args().project.resolve())
