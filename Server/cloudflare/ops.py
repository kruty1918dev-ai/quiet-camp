"""Use the owner's Wrangler browser login for narrowly scoped Cloudflare operations. Never print tokens."""
import argparse
from datetime import datetime, timezone, timedelta
import json
import os
from pathlib import Path
import tomllib
from urllib.request import Request, urlopen
from urllib.error import HTTPError

ACCOUNT = '54bd1b2351d1bbd189427b2a4e43aa5b'
DATABASE = 'f8e41926-e58f-4306-8a89-9b0616b367fc'
WORKER = 'quietcamp-access'


def api(path, data=None, *, graphql=False):
    auth = Path(os.environ['APPDATA'])/'xdg.config/.wrangler/config/default.toml'
    token = tomllib.loads(auth.read_text(encoding='utf-8'))['oauth_token']
    url = 'https://api.cloudflare.com/client/v4/' + ('graphql' if graphql else 'accounts/'+ACCOUNT+path)
    request = Request(url, json.dumps(data).encode() if data is not None else None,
                      {'Authorization':'Bearer '+token,'Content-Type':'application/json'})
    try:
        with urlopen(request,timeout=30) as response: value=json.load(response)
    except HTTPError as error: raise RuntimeError('Cloudflare API HTTP '+str(error.code)) from None
    if value.get('errors'): raise RuntimeError('Cloudflare API rejected operation: '+json.dumps(value['errors']))
    return value.get('data') if graphql else value['result']


def preflight():
    database=api('/d1/database/'+DATABASE)
    if database['uuid'] != DATABASE or database['name'] != WORKER or database.get('jurisdiction') != 'eu' or database.get('read_replication',{}).get('mode') != 'disabled':
        raise RuntimeError('Existing D1 identity, EU jurisdiction or replication differs; no changes allowed')
    worker=api('/workers/scripts/'+WORKER+'/settings')
    print(json.dumps(dict(worker=WORKER,databaseId=DATABASE,jurisdiction=database['jurisdiction'],readReplication=database['read_replication'],tables=database.get('num_tables'),existingWorker=True)))
    return worker


def metrics(start):
    end=datetime.now(timezone.utc)
    query='''query Metrics($account: string, $start: string, $end: string) {
      viewer { accounts(filter:{accountTag:$account}) { workersInvocationsAdaptive(limit:100, filter:{scriptName:"quietcamp-access",datetime_geq:$start,datetime_leq:$end}) {
        sum {requests errors subrequests} quantiles {cpuTimeP50 cpuTimeP99} dimensions {status}
      } } } }'''
    result=api('',dict(query=query,variables=dict(account=ACCOUNT,start=start or (end-timedelta(hours=1)).isoformat(),end=end.isoformat())),graphql=True)
    print(json.dumps(result))


if __name__ == '__main__':
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument('command',choices=['preflight','metrics'])
    parser.add_argument('--start')
    args=parser.parse_args()
    try:
        if args.command=='preflight': preflight()
        else: metrics(args.start)
    except (RuntimeError,OSError,KeyError) as error:
        print('FAILED: '+str(error)); raise SystemExit(1)
