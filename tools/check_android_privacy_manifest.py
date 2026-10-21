#!/usr/bin/env python3
"""Verify privacy rules in the APK itself, not just a source or stale Gradle manifest."""
import argparse
import hashlib
import json
import os
import subprocess
import xml.etree.ElementTree as ET
from datetime import datetime, timezone
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
ANDROID = '{http://schemas.android.com/apk/res/android}'
TOOLS = '{http://schemas.android.com/tools}'


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--apk', required=True, type=Path)
    parser.add_argument('--apkanalyzer', required=True, type=Path)
    parser.add_argument('--java-home', type=Path)
    parser.add_argument('--require-development', action='store_true')
    parser.add_argument('--output', type=Path)
    args = parser.parse_args()
    env = os.environ.copy()
    if args.java_home:
        env['JAVA_HOME'] = str(args.java_home)
    result = subprocess.run([str(args.apkanalyzer), 'manifest', 'print', str(args.apk)],
                            env=env, capture_output=True, text=True, encoding='utf8')
    if result.returncode:
        raise SystemExit('Cannot inspect packaged manifest: ' + result.stderr[-1000:])
    # SDK Windows launchers can echo their batch commands before/after XML.
    start = result.stdout.find('<?xml')
    end = result.stdout.rfind('</manifest>')
    if start < 0 or end < start:
        raise SystemExit('apkanalyzer did not return an XML manifest')
    manifest_xml = result.stdout[start:end+len('</manifest>')]
    manifest = ET.fromstring(manifest_xml)
    app = manifest.find('application')
    policy = ET.parse(ROOT/'QuietCamp/Assets/Plugins/Android/QuietCampPrivacy.androidlib/AndroidManifest.xml').getroot().find('application')
    errors = []
    if manifest.get('package') != 'com.quietcamp.game':
        errors.append('Unexpected Android package')
    if app is None:
        raise SystemExit('APK application manifest is missing')
    for rule in policy:
        name = rule.get(ANDROID+'name')
        matches = [entry for entry in app.findall(rule.tag) if entry.get(ANDROID+'name') == name]
        if rule.get(TOOLS+'node') == 'remove':
            if matches:
                errors.append('Forbidden startup component remains: ' + name)
        elif len(matches) != 1:
            errors.append('Missing or duplicate privacy rule: ' + name)
        else:
            for key, value in rule.attrib.items():
                if key.startswith(ANDROID) and key != ANDROID+'name':
                    expected = value.replace('${applicationId}', manifest.get('package'))
                    if matches[0].get(key) != expected:
                        errors.append('Incorrect privacy attribute: ' + name + ' / ' + key)
    if args.require_development and app.get(ANDROID+'debuggable') != 'true':
        errors.append('QA APK is not debuggable; development build was not applied')
    if errors:
        raise SystemExit('FAIL APK privacy manifest:\n' + '\n'.join(errors))
    with args.apk.open('rb') as artifact:
        digest = hashlib.file_digest(artifact, 'sha256').hexdigest()
    evidence = {
        'checkedAtUtc': datetime.now(timezone.utc).isoformat(),
        'apkSha256': digest, 'apkBytes': args.apk.stat().st_size,
        'package': manifest.get('package'),
        'debuggable': app.get(ANDROID+'debuggable', 'false'),
        'firebaseInitProviderRemoved': True,
        'defaultCollectionRulesMatch': True,
        'providers': [p.get(ANDROID+'name') for p in app.findall('provider')],
        'scope': 'Packaged manifest only; no assertion about network, native queues or Google account settings.'
    }
    if args.output:
        args.output.mkdir(parents=True, exist_ok=True)
        (args.output/'packaged-manifest.xml').write_text(manifest_xml, encoding='utf8')
        (args.output/'manifest-check.json').write_text(json.dumps(evidence, indent=2)+'\n', encoding='utf8')
    print('PASS APK privacy manifest: ' + evidence['package'] + ', Firebase startup provider removed, defaults denied; SHA-256 ' + digest)


if __name__ == '__main__':
    main()
