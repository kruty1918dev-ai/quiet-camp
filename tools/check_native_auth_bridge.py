"""Compile the Credential Manager bridge against real Android/Google APIs. No APK or Unity launch."""
import argparse
import hashlib
import json
import os
from pathlib import Path
import subprocess
import urllib.request
import zipfile

ROOT = Path(__file__).resolve().parents[1]


def main():
    cli = argparse.ArgumentParser(description=__doc__)
    cli.add_argument('--android-player', type=Path, required=True, help='Unity AndroidPlayer directory containing SDK and OpenJDK')
    args = cli.parse_args()
    cache = ROOT/'QuietCamp/Temp/ai/native-auth/java'
    cache.mkdir(parents=True, exist_ok=True)
    artifacts = [
        ('https://dl.google.com/dl/android/maven2', 'androidx.credentials:credentials:1.6.0', 'aar'),
        ('https://dl.google.com/dl/android/maven2', 'com.google.android.libraries.identity.googleid:googleid:1.2.1', 'aar'),
        ('https://repo.maven.apache.org/maven2', 'org.jetbrains.kotlin:kotlin-stdlib:2.1.20', 'jar'),
        ('https://dl.google.com/dl/android/maven2', 'androidx.annotation:annotation-jvm:1.9.1', 'jar'),
    ]
    jars = [args.android_player/'SDK/platforms/android-35/android.jar']
    hashes = {}
    for base, coordinate, extension in artifacts:
        group, name, version = coordinate.split(':')
        archive = cache/f'{name}-{version}.{extension}'
        url = base+'/'+group.replace('.', '/')+'/'+name+'/'+version+'/'+archive.name
        if not archive.exists():
            with urllib.request.urlopen(url, timeout=30) as response:
                archive.write_bytes(response.read())
        hashes[coordinate] = hashlib.sha256(archive.read_bytes()).hexdigest()
        if extension == 'aar':
            jar = cache/f'{name}-{version}-classes.jar'
            with zipfile.ZipFile(archive) as package:
                jar.write_bytes(package.read('classes.jar'))
            jars.append(jar)
        else:
            jars.append(archive)
    compiler = args.android_player/('OpenJDK/bin/javac.exe' if os.name == 'nt' else 'OpenJDK/bin/javac')
    source = ROOT/'QuietCamp/Assets/Plugins/Android/QuietCampPrivacy.androidlib/src/main/java/com/quietcamp/game/privacy/GoogleCredentialBridge.java'
    output = cache/'classes'
    output.mkdir(exist_ok=True)
    subprocess.run([str(compiler), '-proc:none', '-source', '17', '-target', '17', '-classpath', os.pathsep.join(map(str,jars)),
                    '-d', str(output), str(source)], check=True)
    receipt = dict(sourceSha256=hashlib.sha256(source.read_bytes()).hexdigest(), artifacts=hashes,
                   compiled=True, unityLaunched=False, playerBuild=False, nativeRuntimeVerified=False)
    (cache/'compile-receipt.json').write_text(json.dumps(receipt,indent=2)+'\n')
    print('PASS Java 17 bridge API compile: Android 35, Credential Manager 1.6.0, googleid 1.2.1. No player build/runtime check.')


if __name__ == '__main__':
    main()
