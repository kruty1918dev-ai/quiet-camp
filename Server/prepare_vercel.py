"""Prepare a secret-free overlay for the EXISTING website. Does not deploy or change owner config."""
import argparse
import hashlib
import json
from pathlib import Path
import shutil


def prepare(output):
    source = Path(__file__).resolve().parent
    output = output.resolve()
    if output == source or source in output.parents:
        raise ValueError('Use an ignored Temp directory, not Server source')
    output.mkdir(parents=True, exist_ok=True)
    api, backend = output/'api', output/'quietcamp_backend'
    api.mkdir(exist_ok=True); backend.mkdir(exist_ok=True)
    for name in ('access_store.py', 'complimentary_access.py', 'service.py', 'vercel_handler.py'):
        shutil.copyfile(source/name, backend/name)
    (api/'quietcamp-access.py').write_text(
        'from pathlib import Path\nimport sys\nsys.path.insert(0, str(Path(__file__).resolve().parents[1] / "quietcamp_backend"))\nfrom vercel_handler import handler\n', encoding='utf-8')
    integration = dict(rewrite={'source':'/v1/access/:action', 'destination':'/api/quietcamp-access?action=:action'},
                       function={'api/quietcamp-access.py':{'maxDuration':15}},
                       environment=['QC_ANDROID_PACKAGE','QC_PRO_PRODUCT','QC_COMPLIMENTARY_USERNAME','QC_COMPLIMENTARY_PASSWORD_HASH',
                                    'QC_COMPLIMENTARY_EXPLORE_PUBLISHED','UPSTASH_REDIS_REST_URL','UPSTASH_REDIS_REST_TOKEN','CRON_SECRET'],
                       cron={'path':'/v1/access/maintenance','schedule':'0 5 * * *'},
                       requires=['Existing commercial-compatible Vercel plan with no added cost', 'Explicitly Free Upstash Redis database',
                                 'Production secrets installed by authorized owner', 'Public HTTPS and Android verification'])
    (output/'integration.json').write_text(json.dumps(integration,indent=2)+'\n', encoding='utf-8')
    files = {str(p.relative_to(output)).replace('\\','/'): hashlib.sha256(p.read_bytes()).hexdigest() for p in sorted(output.rglob('*.py'))}
    print(json.dumps(dict(prepared=str(output), files=files, deployed=False)))


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--output',type=Path,required=True)
    prepare(parser.parse_args().output)
