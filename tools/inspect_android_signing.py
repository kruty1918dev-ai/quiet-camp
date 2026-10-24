"""Read an existing APK's signing fingerprints. Never generates keys or builds a player."""
import argparse
import base64
import hashlib
import json
import os
from pathlib import Path
import re
import subprocess


def main():
    cli=argparse.ArgumentParser(description=__doc__)
    cli.add_argument('--apk',type=Path,required=True)
    cli.add_argument('--android-player',type=Path,required=True)
    cli.add_argument('--build-tools',default='36.0.0')
    cli.add_argument('--output',type=Path)
    args=cli.parse_args()
    base=args.android_player
    java=base/('OpenJDK/bin/java.exe' if os.name=='nt' else 'OpenJDK/bin/java')
    tools=base/'SDK/build-tools'/args.build_tools
    signed=subprocess.run([str(java),'-jar',str(tools/'lib/apksigner.jar'),'verify','--print-certs',str(args.apk)],
                          check=True,capture_output=True,text=True).stdout
    certs=re.findall(r'Signer #\d+ certificate SHA-1 digest: ([0-9a-fA-F]{40})',signed)
    sha256=re.findall(r'Signer #\d+ certificate SHA-256 digest: ([0-9a-fA-F]{64})',signed)
    if len(certs)!=1 or len(sha256)!=1:raise SystemExit('Expected one verified APK signer; inspect multi-signer APK manually.')
    aapt=tools/('aapt2.exe' if os.name=='nt' else 'aapt2')
    manifest=subprocess.run([str(aapt),'dump','badging',str(args.apk)],check=True,capture_output=True,text=True,encoding='utf8').stdout
    package=re.search(r"^package: name='([^']+)'",manifest,re.M)
    activity=re.search(r"^launchable-activity: name='([^']+)'",manifest,re.M)
    if not package or package[1]!='com.quietcamp.game':raise SystemExit('APK package is not the configured com.quietcamp.game; no Firebase registration recommended.')
    def fingerprint(value):return ':'.join(value[i:i+2].upper() for i in range(0,len(value),2))
    result=dict(apkName=args.apk.name,apkSha256=hashlib.sha256(args.apk.read_bytes()).hexdigest(),
                package=package[1],activity=activity[1] if activity else None,certificateSha1=fingerprint(certs[0]),
                certificateSha256=fingerprint(sha256[0]),metaKeyHash=base64.b64encode(bytes.fromhex(certs[0])).decode(),
                existingApk=True,signatureVerified=True,playerBuild=False)
    output=json.dumps(result,indent=2)+'\n'
    if args.output:
        args.output.parent.mkdir(parents=True,exist_ok=True);args.output.write_text(output,encoding='utf8')
    print(output,end='')


if __name__=='__main__':main()
