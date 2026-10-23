"""Convert a known generated review credential to protected Worker secrets; no password rotation."""
import argparse
import base64
import hashlib
import hmac
import json
import os
from pathlib import Path
import re
import secrets


def provision(credentials, output):
    root = Path(__file__).resolve().parents[2]
    credentials, output = credentials.resolve(), output.resolve()
    if any(p == root or root in p.parents for p in (credentials, output)):
        raise ValueError('Private inputs and secrets must remain outside the repository')
    account = json.loads(credentials.read_text(encoding='utf-8'))
    if account.get('credentialOrigin') != 'secrets-generated-alnum32-v1' or not re.fullmatch('[A-Za-z0-9]{32}', account['password']):
        raise ValueError('Only credentials issued by the random review-account provisioner are supported')
    if not re.fullmatch('[A-Za-z0-9_-]{1,100}', account['username']): raise ValueError('Invalid account name')
    previous = dict(line.split('=',1) for line in credentials.with_name('server-account.private.env').read_text(encoding='utf-8').splitlines() if line)
    parts = previous['QC_COMPLIMENTARY_PASSWORD_HASH'].split('$')
    if previous['QC_COMPLIMENTARY_USERNAME'] != account['username'] or parts[:2] != ['pbkdf2-sha256','600000']:
        raise ValueError('Original private account does not match')
    digest = hashlib.pbkdf2_hmac('sha256',account['password'].encode(),bytes.fromhex(parts[2]),600000).hex()
    if not hmac.compare_digest(digest,parts[3]): raise ValueError('Original password verification failed')
    existing = json.loads(output.read_text(encoding='utf-8')) if output.exists() else None
    pepper = existing['QC_ACCESS_PEPPER'] if existing else secrets.token_urlsafe(32)
    key = base64.urlsafe_b64decode(pepper+'=')
    verifier = hmac.new(key,('quietcamp-credential-v1\0'+account['username']+'\0'+account['password']).encode(),hashlib.sha256).hexdigest()
    value = dict(QC_ACCESS_PEPPER=pepper, QC_ACCESS_ACCOUNT=json.dumps(dict(schema=1,kind='generated-alnum32-hmac-sha256-v1',username=account['username'],verifier=verifier),separators=(',',':')))
    if existing and existing != value: raise ValueError('Refusing implicit account/secret rotation')
    if not existing:
        output.parent.mkdir(parents=True,exist_ok=True)
        with output.open('x',encoding='utf-8') as file: file.write(json.dumps(value,indent=2)+'\n')
        os.chmod(output,0o600)
    print('Worker secrets prepared outside Git; existing random username/password verified against PBKDF2 and preserved. No values printed.')


if __name__ == '__main__':
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--credentials',type=Path,required=True)
    parser.add_argument('--output',type=Path,required=True)
    args=parser.parse_args()
    provision(args.credentials,args.output)
