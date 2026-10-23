"""Generate private, reusable access credentials outside Git. Does not deploy or enable access."""
import argparse
import json
import os
from pathlib import Path
import secrets
import string
from complimentary_access import password_hash

TITLE = 'Quiet Camp - full Pro access'
INSTRUCTIONS = ('In Settings, choose English, then open Complimentary Pro access. Scroll to the sign-in form. '
                'Enter the supplied username and password and tap Sign in. No payment, registration or developer contact is required. '
                'This grants standard Pro: no ads, unlimited lives/hints and included journeys. Normal progression and privacy choices still apply.')


def provision(output):
    output = output.resolve()
    root = Path(__file__).resolve().parents[1]
    if output == root or root in output.parents:
        raise ValueError('Credentials must be outside the repository')
    output.mkdir(parents=True, exist_ok=True)
    private = output/'play-console-access.private.json'
    environment = output/'server-account.private.env'
    if private.exists() or environment.exists(): raise ValueError('Refusing to overwrite an existing account')
    username, password = 'play-review', ''.join(secrets.choice(string.ascii_letters + string.digits) for _ in range(32))
    assert len(TITLE) <= 60 and len(INSTRUCTIONS) <= 500
    data = dict(readyForPlayConsole=False, status='NOT CONNECTED: deploy and verify the endpoint and Android UI before submission',
                title=TITLE, username=username, password=password, instructions=INSTRUCTIONS,
                grants=['No advertising requests', 'Unlimited lives and hints', 'Journeys explicitly included in Pro'],
                doesNotGrant=['Unpublished content', 'Unspecified future DLC', 'Privacy consent or purchase simulation'])
    with private.open('x', encoding='utf-8') as file: file.write(json.dumps(data, indent=2)+'\n')
    with environment.open('x', encoding='utf-8') as file:
        file.write('QC_COMPLIMENTARY_USERNAME='+username+'\nQC_COMPLIMENTARY_PASSWORD_HASH='+password_hash(password)+'\n')
    for path in (private, environment):
        os.chmod(path, 0o600)
    return private


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--output', type=Path, required=True)
    args = parser.parse_args()
    print('Private account prepared at '+str(provision(args.output))+'. Not deployed or enabled; credentials were not printed.')
