"""Review or install the prepared function into the existing static website; never deploys."""
import argparse
from pathlib import Path
import shutil
import tempfile

from prepare_vercel import prepare

ROUTE_MARKER = "const rewrites=routes.filter(r=>r.path!=='/').map(r=>({source:r.path,destination:'/'+r.file}));"
ROUTE = "rewrites.unshift({source:'/v1/access/:action',destination:'/api/quietcamp-access?action=:action'});"
FUNCTION_MARKER = 'framework:null,rewrites,'
FUNCTION_VALUE = "functions:{'api/quietcamp-access.py':{maxDuration:15}},"
FUNCTION = 'framework:null,'+FUNCTION_VALUE+'rewrites,'
CRON = "crons:[{path:'/v1/access/maintenance',schedule:'0 5 * * *'}],"


def install(site, apply=False):
    site = site.resolve()
    path = site/'scripts/build.mjs'
    source = path.read_text(encoding='utf-8')
    if ROUTE_MARKER not in source or (FUNCTION_MARKER not in source and FUNCTION_VALUE not in source):
        raise ValueError('Website routing generator changed; review integration.json instead of overwriting it')
    updated = source if ROUTE in source else source.replace(ROUTE_MARKER, ROUTE_MARKER+'\n'+ROUTE, 1)
    updated = updated.replace(FUNCTION_MARKER,FUNCTION,1)
    if CRON not in updated:
        if 'crons:' in updated:
            raise ValueError('Website already has cron configuration; merge the maintenance schedule explicitly')
        updated = updated.replace('framework:null,', 'framework:null,'+CRON, 1)
    with tempfile.TemporaryDirectory() as directory:
        overlay = Path(directory)
        prepare(overlay)
        paths = [p for folder in ('api','quietcamp_backend') for p in (overlay/folder).rglob('*.py')]
        for file in paths:
            target = site/file.relative_to(overlay)
            if target.exists() and target.read_bytes() != file.read_bytes():
                raise ValueError('Existing backend file differs: '+str(target.relative_to(site)))
        print('REVIEW: add /v1/access/:action rewrite, one Python function and daily authenticated maintenance; preserve existing routes and headers.')
        print('Files: scripts/build.mjs, vercel.json (npm run routes), '+', '.join(str(p.relative_to(overlay)) for p in paths))
        if not apply:
            print('Dry run only. No website files changed; no deployment or owner activation.'); return
        for file in paths:
            target = site/file.relative_to(overlay)
            target.parent.mkdir(parents=True,exist_ok=True)
            shutil.copyfile(file,target)
        path.write_text(updated,encoding='utf-8',newline='\n')
        print('Installed source only. In the website run npm run routes, npm test, npm run build, review diff, then commit/push when hosting conditions are verified.')


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--site-root',type=Path,required=True)
    parser.add_argument('--apply',action='store_true')
    args = parser.parse_args()
    install(args.site_root,args.apply)
