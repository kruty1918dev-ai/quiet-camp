#!/usr/bin/env python3
"""Install the owner's PWB package locally, preserving Unity GUIDs. No Unity launch."""
import argparse
import io
from pathlib import Path, PurePosixPath
import tarfile
import zipfile


def unpack(archive):
    raw = archive.read_bytes()
    if zipfile.is_zipfile(archive):
        with zipfile.ZipFile(archive) as outer:
            packages = [n for n in outer.namelist() if n.endswith('.unitypackage')]
            if len(packages) != 1:
                raise ValueError('Expected exactly one .unitypackage in ZIP')
            raw = outer.read(packages[0])
    with tarfile.open(fileobj=io.BytesIO(raw), mode='r:gz') as package:
        members = {m.name: m for m in package.getmembers() if m.isfile()}
        assets = {}
        for name, member in members.items():
            if not name.endswith('/pathname'):
                continue
            text = package.extractfile(member).read().decode('utf-8-sig').splitlines()[0].strip('\x00')
            path = PurePosixPath(text.replace('\\', '/'))
            if path.is_absolute() or '..' in path.parts or path.parts[:2] != ('Assets', 'PluginMaster'):
                raise ValueError('Unexpected vendor asset path: ' + text)
            base = name.rsplit('/', 1)[0]
            for part, suffix in [('asset', ''), ('asset.meta', '.meta')]:
                source = members.get(base + '/' + part)
                if source:
                    key = str(path) + suffix
                    if key in assets:
                        raise ValueError('Duplicate asset path: ' + key)
                    assets[key] = package.extractfile(source).read()
        version_files = [b for n, b in assets.items() if n.endswith('/PWBData.cs')]
        if not version_files or b'4.12.0' not in version_files[0]:
            raise ValueError('The approved integration expects PWB 4.12.0')
        if any(n.endswith('.asmdef') for n in assets):
            raise ValueError('This package layout requires review: unexpected asmdef')
        return assets


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--archive', type=Path, required=True)
    parser.add_argument('--project', type=Path, action='append', required=True)
    parser.add_argument('--dry-run', action='store_true')
    args = parser.parse_args()
    assets = unpack(args.archive)
    planned = []
    for project in args.project:
        root = project.resolve()
        if not (root / 'ProjectSettings/ProjectVersion.txt').is_file():
            raise ValueError('Not a Unity project: ' + str(root))
        missing = []
        for name, content in assets.items():
            target = (root / name).resolve()
            if not target.is_relative_to(root / 'Assets/PluginMaster') and target != root / 'Assets/PluginMaster.meta':
                raise ValueError('Asset path escaped vendor folder')
            if target.exists():
                if target.read_bytes() != content:
                    raise ValueError('Existing local asset differs; not overwritten: ' + str(target))
            else:
                missing.append((target, content))
        planned.append((root, missing))
    # All projects are preflighted before writing either copy.
    for root, missing in planned:
        if not args.dry_run:
            for target, content in missing:
                target.parent.mkdir(parents=True, exist_ok=True)
                target.write_bytes(content)
        print(f'{root}: PWB 4.12.0, {len(assets)} files, {len(missing)} new; ' + ('dry run' if args.dry_run else 'installed'))
    print('Refresh the existing Unity Editor. The QuietCamp bridge is enabled after compilation.')


if __name__ == '__main__':
    main()
