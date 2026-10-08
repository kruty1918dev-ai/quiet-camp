#!/usr/bin/env python3
"""Check a fresh checkout without Unity, Library, sibling repos or network."""
import json
from pathlib import Path
import re
import subprocess
import sys

ROOT = Path(__file__).resolve().parent.parent


def verify(root):
    project = root / 'QuietCamp'
    packages = project / 'Packages'
    manifest = json.loads((packages / 'manifest.json').read_text(encoding='utf-8'))
    lock = json.loads((packages / 'packages-lock.json').read_text(encoding='utf-8'))['dependencies']
    errors = []
    for name, version in manifest['dependencies'].items():
        entry = lock.get(name, {})
        if entry.get('version') != version:
            errors.append(f'{name}: manifest/lock mismatch')
        if version.startswith('file:'):
            target = (packages / version[5:]).resolve()
            if not target.is_relative_to(packages.resolve()):
                errors.append(f'{name}: dependency outside project')
                continue
            definition = target / 'package.json'
            if not definition.is_file():
                errors.append(f'{name}: missing package.json')
                continue
            metadata = json.loads(definition.read_text(encoding='utf-8'))
            if metadata.get('name') != name:
                errors.append(f'{name}: package identity mismatch')
            if entry.get('dependencies', {}) != metadata.get('dependencies', {}):
                errors.append(f'{name}: stale dependency lock')
            for dependency in metadata.get('dependencies', {}):
                if dependency not in lock:
                    errors.append(f'{name}: unlocked dependency {dependency}')
        elif 'github.com' in version and not re.search(r'#[0-9a-f]{40}$', version):
            errors.append(f'{name}: Git dependency must pin an exact commit')
    paths = subprocess.check_output(['git', '-C', str(root), 'ls-files', '-z'], text=True).split('\0')
    folded = {}
    for path in filter(None, paths):
        key = path.casefold()
        if key in folded and folded[key] != path:
            errors.append(f'Case-insensitive path collision: {folded[key]} / {path}')
        folded[key] = path
    if errors:
        print('\n'.join(errors), file=sys.stderr)
        return 1
    print(f'PASS checkout portability: {len(manifest["dependencies"])} direct dependencies; no external local packages or path case collisions.')
    print('Static check only; Unity first import and platform runtime are not tested.')
    return 0


if __name__ == '__main__':
    sys.exit(verify(ROOT))
