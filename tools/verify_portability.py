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
    assemblies = {}
    for path in paths:
        if path.endswith('.asmdef'):
            definition = json.loads((root / path).read_text(encoding='utf-8'))
            name = definition['name']
            if name in assemblies:
                errors.append(f'Duplicate assembly: {name}')
            assemblies[name] = path
    for name, path in assemblies.items():
        if not path.startswith('QuietCamp/Assets/QuietCamp/'):
            continue
        definition = json.loads((root / path).read_text(encoding='utf-8'))
        for reference in definition.get('references', []):
            if reference.startswith('Kruty1918.') and reference not in assemblies:
                # Git packages are resolved by Unity; local packages must already exist.
                remote = any(key.startswith('com.kruty1918.') and not value.startswith('file:')
                             and re.sub(r'[^a-z0-9]', '', reference.split('.')[1].lower()) in re.sub(r'[^a-z0-9]', '', key)
                             for key, value in manifest['dependencies'].items())
                if not remote:
                    errors.append(f'{name}: missing custom assembly {reference}')
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
