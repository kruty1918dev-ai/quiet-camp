"""Read-only deployment smoke check. Does not authenticate, create or delete accounts."""
import argparse,json,urllib.request,urllib.error
p=argparse.ArgumentParser(description=__doc__)
p.add_argument('--base',default='https://quietcamp-access.oleksiy-havryliuk.workers.dev/v1/cloud')
a=p.parse_args();base=a.base.rstrip('/')
def read(path):
 try:
  with urllib.request.urlopen(base+path,timeout=20) as r:return r.status,r.headers,r.read().decode()
 except urllib.error.HTTPError as e:return e.code,e.headers,e.read().decode(errors='replace')
 except urllib.error.URLError:return 0,{},''
report=[]
s,h,b=read('/options')
try:options=json.loads(b)
except (json.JSONDecodeError,ValueError):options={}
report.append({'path':'/options','status':s,'accountPortal':options.get('accountPortal'),'nativeSignIn':options.get('nativeSignIn')})
ready=s==200 and options.get('accountPortal') is True and options.get('nativeSignIn') is True
if ready:
 for page in ['account','admin']:
  for lang in ['en','uk','de']:
   path='/'+page+'?lang='+lang;s,h,b=read(path)
   ok=s==200 and '<html lang="'+lang+'">' in b and h.get('Cache-Control')=='no-store' and 'noindex' in h.get('X-Robots-Tag','') and 'unsafe-' not in h.get('Content-Security-Policy','')
   report.append({'path':path,'status':s,'privateHeaders':ok});ready=ready and ok
print(json.dumps({'ready':bool(ready),'checks':report,'realAuthenticationTested':False,'deletionPerformed':False},indent=2))
raise SystemExit(0 if ready else 1)
