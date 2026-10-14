#!/usr/bin/env python3
"""Install owner-provided authoring tools; keep paid sources local and preserve GUIDs."""
import argparse
import io
import json
import re
from pathlib import Path, PurePosixPath
import subprocess
import tarfile
import zipfile

MODULES = {'com.jbooth.microverse', 'com.jbooth.microverse.splines',
           'com.jbooth.microverse.objects', 'com.jbooth.microverse.vegetation',
           'com.jbooth.microverse.ambiance'}
REFERENCES = {
    '21d1eb854b91ade49bc69a263d12bee2': 'Unity.Splines',
    'd04eb9c554ad44ceab303cecf0c0cf82': 'Unity.Burst',
    '4bdfb2239705740718731d0b4d54061d': 'Unity.Collections',
    'e43142fc3fec6554980dde6126631f1d': 'Unity.Splines.Editor',
    '1826c0224c0d048b88112c79bbb0cd85': 'Unity.VisualEffectGraph.Runtime',
    '0f9e8f73c69964f1e8176f9a0b86f9b2': 'Unity.VisualEffectGraph.Editor',
    'd8b63aba1907145bea998dd612889d6b': 'Unity.Mathematics',
}
MATH_GUID = 'd8b63aba1907145bea998dd612889d6b'
MICROSPLAT_GUID = '90ab0119788ed4ec6b8fe54dd44f01fa'


def safe_path(value):
    path = PurePosixPath(value.replace('\\', '/'))
    if path.is_absolute() or '..' in path.parts or ':' in value:
        raise ValueError('Unsafe archive path: ' + value)
    return path


def compatibility(name, data):
    """Small, reproducible Unity 6.6 metadata patch; original .meta GUIDs stay intact."""
    if name.endswith('/RiverModeler.MicroVerse.cs'):
        text = data.decode('utf-8-sig').replace('\r', '')
        # The supplied MicroVerse 1.7.0 exposes the trench API, but not the later curve API.
        pattern = r'splinePath\.useTrenchCurve = true;\s*splinePath\.trenchCurve = new AnimationCurve\(heightCurveKeyframes\);\s*splinePath\.ClearCachedSplineTrenchCurve\(\);'
        text, count = re.subn(pattern, 'splinePath.trench = microVerseSettings.bedDepth; // QuietCamp: MicroVerse 1.7.0 compatibility', text)
        if count != 1:
            raise ValueError('Unexpected River Modeler integration source; compatibility patch needs review')
        return text.encode('utf-8')
    if name.endswith('/package.json'):
        obj = json.loads(data)
        deps = obj.get('dependencies', {})
        deps.pop('com.unity.mathematics', None)  # Built into Unity since 6.5.
        if 'com.unity.splines' in deps:
            deps['com.unity.splines'] = '2.8.4'
        if 'com.unity.burst' in deps:
            deps['com.unity.burst'] = '2.0.0'
        for package in ['com.unity.shadergraph', 'com.unity.visualeffectgraph']:
            if package in deps:
                deps[package] = '17.6.0'
        return (json.dumps(obj, indent=2) + '\n').encode()
    if name.endswith('.asmdef'):
        obj = json.loads(data)
        refs = []
        for ref in obj.get('references', []):
            guid = ref.removeprefix('GUID:')
            if guid == MICROSPLAT_GUID:
                continue
            refs.append(REFERENCES.get(guid, ref))
        obj['references'] = refs
        # Old packages detect mathematics through a UPM package that no longer exists.
        if any(v.get('define', '').startswith('MATHEMATICS') for v in obj.get('versionDefines', [])):
            obj['versionDefines'] += [
                {'name': 'Unity', 'expression': '6000.5.0', 'define': 'MATHEMATICS'},
                {'name': 'Unity', 'expression': '6000.5.0', 'define': 'MATHEMATICS_1_3_1'}]
        return (json.dumps(obj, indent=2) + '\n').encode()
    return data


def river_files(archive):
    names = subprocess.check_output(['tar', '-tf', str(archive)], text=True).splitlines()
    packages = [n for n in names if n.endswith('.unitypackage')]
    if len(packages) != 1:
        raise ValueError('Expected one River Modeler unitypackage')
    raw = subprocess.check_output(['tar', '-xOf', str(archive), packages[0]])
    result = {}
    with tarfile.open(fileobj=io.BytesIO(raw), mode='r:gz') as package:
        members = {m.name: m for m in package.getmembers() if m.isfile()}
        for key, member in members.items():
            if not key.endswith('/pathname'):
                continue
            name = str(safe_path(package.extractfile(member).read().decode('utf-8-sig').splitlines()[0]))
            if not name.startswith('Assets/WaterGeometryTools'):
                raise ValueError('Unexpected river asset root: ' + name)
            if '/Demo/' in name or name.endswith('/Demo'):
                continue
            guid = key.rsplit('/', 1)[0]
            for part, suffix in [('asset', ''), ('asset.meta', '.meta')]:
                source = members.get(guid + '/' + part)
                if source:
                    target = name + suffix
                    if target in result:
                        raise ValueError('Duplicate river path: ' + target)
                    result[target] = compatibility(target, package.extractfile(source).read())
    return result


def microverse_files(archive):
    result = {}
    with zipfile.ZipFile(archive) as source:
        for entry in source.infolist():
            parts = safe_path(entry.filename).parts
            if entry.is_dir() or '__MACOSX' in parts or len(parts) < 3 or parts[1] not in MODULES:
                continue
            if any(p in {'Example', 'Examples', 'Demo', 'MicroSplatProxyRenderer'} for p in parts):
                continue
            if parts[-1].startswith('.') or parts[-1] in {'Example.meta', 'Examples.meta', 'Demo.meta', 'MicroSplatProxyRenderer.meta'}:
                continue
            target = 'Packages/' + '/'.join(parts[1:])
            result[target] = compatibility(target, source.read(entry))
    if not any('/com.jbooth.microverse.splines/package.json' in n for n in result):
        raise ValueError('MicroVerse archive does not contain Splines')
    return result


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--river', type=Path, required=True)
    parser.add_argument('--microverse', type=Path, required=True)
    parser.add_argument('--odin-source', type=Path, required=True)
    parser.add_argument('--project', type=Path, action='append', required=True)
    parser.add_argument('--dry-run', action='store_true')
    parser.add_argument('--refresh-compatibility', action='store_true', help='Refresh known Unity metadata and River integration patch; preserve GUIDs')
    args = parser.parse_args()
    files = river_files(args.river)
    files.update(microverse_files(args.microverse))
    for source in args.odin_source.rglob('*'):
        if source.is_file() and 'Demos' not in source.relative_to(args.odin_source).parts and source.name != 'Demos.meta':
            files['Assets/Plugins/Sirenix/' + source.relative_to(args.odin_source).as_posix()] = source.read_bytes()
    meta = args.odin_source.with_suffix('.meta')
    if meta.exists():
        files['Assets/Plugins/Sirenix.meta'] = meta.read_bytes()
    plans = []
    for project in args.project:
        root = project.resolve()
        if not (root / 'ProjectSettings/ProjectVersion.txt').exists():
            raise ValueError('Not a Unity project: ' + str(root))
        missing = []
        for name, content in files.items():
            target = (root / safe_path(name)).resolve()
            if not target.is_relative_to(root):
                raise ValueError('Target escaped project')
            if target.exists():
                if target.read_bytes() != content:
                    if args.refresh_compatibility and name.endswith('/RiverModeler.MicroVerse.cs'):
                        previous = target.read_text(encoding='utf-8-sig')
                        if 'ClearCachedSplineTrenchCurve' in previous or 'QuietCamp: MicroVerse 1.7.0 compatibility' in previous:
                            missing.append((target, content)); continue
                    if args.refresh_compatibility and (name.endswith('.asmdef') or name.endswith('/package.json')):
                        previous = json.loads(target.read_bytes()); current = json.loads(content)
                        if previous.get('name') == current.get('name') and previous.get('version') == current.get('version'):
                            missing.append((target, content)); continue
                    raise ValueError('Existing asset differs; not overwritten: ' + str(target))
            else:
                missing.append((target, content))
        plans.append((root, missing))
    for root, missing in plans:
        if not args.dry_run:
            for target, content in missing:
                target.parent.mkdir(parents=True, exist_ok=True)
                target.write_bytes(content)
            manifest = root / 'Packages/manifest.json'
            data = json.loads(manifest.read_text(encoding='utf-8-sig'))
            data['dependencies']['com.unity.splines'] = '2.8.4'
            # Unity 6.6 ships a forwarding package, not the obsolete pre-6.5 implementation.
            data['dependencies']['com.unity.mathematics'] = '1.4.0'
            data['dependencies']['com.unity.visualeffectgraph'] = '17.6.0'
            manifest.write_text(json.dumps(data, indent=2) + '\n', encoding='utf-8')
        print(f'{root}: {len(missing)} files to write; ' + ('dry run' if args.dry_run else 'installed'))


if __name__ == '__main__':
    main()
