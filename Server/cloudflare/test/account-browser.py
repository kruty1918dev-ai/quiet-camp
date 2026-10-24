"""Real portal HTML, mocked Firebase/API only. Never authenticates or deletes production accounts."""
import asyncio,json,subprocess,time,os
from pathlib import Path
from playwright.async_api import async_playwright
OUT=Path(os.environ.get('QC_ACCOUNT_ARTIFACTS','/workspace/artifacts/quiet-camp-player-accounts-20261024'))
APP='export const initializeApp=c=>c;'
AUTH='''
export const getAuth=()=>({}),inMemoryPersistence={};export async function setPersistence(){}
export class GoogleAuthProvider{setCustomParameters(v){}}export class FacebookAuthProvider{}
export async function signOut(){}
export async function signInWithPopup(){if(window.mockAuthError)throw {code:window.mockAuthError};return {user:{getIdToken:async()=> 'synthetic',getIdTokenResult:async()=>({token:'synthetic',claims:{firebase:{sign_in_provider:'google.com'}}})}};}
export async function linkWithPopup(){window.mockLinked=true;}
'''
async def main():
 OUT.mkdir(parents=True,exist_ok=True);results=[]
 async with async_playwright() as p:
  browser=await p.chromium.launch(executable_path='/usr/bin/chromium',headless=True,args=['--no-sandbox','--disable-dev-shm-usage'])
  for lang in ['en','uk','de']:
   for width,height in [(1440,1000),(390,844),(320,740)]:
    context=await browser.new_context(viewport={'width':width,'height':height});page=await context.new_page();errors=[];requests=[]
    page.on('pageerror',lambda e:errors.append(str(e)))
    page.on('console',lambda m:errors.append(m.text) if m.type=='error' and 'Failed to load resource: the server responded with a status of 401' not in m.text and 'Failed to load resource: the server responded with a status of 503' not in m.text else None)
    current=None;owner=False;fail=False;deleted=0
    async def sdk(route):await route.fulfill(content_type='text/javascript',body=AUTH if 'firebase-auth' in route.request.url else APP)
    await context.route('https://www.gstatic.com/**',sdk)
    async def api(route):
     nonlocal current,deleted
     path=route.request.url.split('/v1/cloud')[-1];data=route.request.post_data_json;requests.append(path)
     status=200;body={}
     if path=='/web/session':
      status=200 if current else 401;body={'profile':current} if current else {'error':'signed_out'}
     elif path=='/web/sign-in':
      current={'accountId':'00000000-0000-4000-8000-000000000001','displayName':'Оксана ⛺','email':'camper@example.test','created':1792830000,'lastActivity':1792830000,'providers':['google','facebook'] if await page.evaluate('!!window.mockLinked') else ['google'],'hasCloudSave':False,'cloudUpdated':None,'isAdmin':owner}
      body={'profile':current}
     elif path=='/web/profile':current['displayName']=data['displayName'];body={'profile':current}
     elif path=='/web/sign-out':current=None;body={'signedOut':True}
     elif path=='/web/delete':
      if fail:status=503;body={'error':'deletion_retry'}
      else:deleted+=1;current=None;body={'deleted':True}
     elif path=='/admin/accounts':
      body={'accounts':[{'id':'00000000-0000-4000-8000-000000000001','firebase_uid':'synthetic-firebase-uid','display_name':'Оксана ⛺','email':'camper@example.test','provider':'google','providers':['google'],'created':1792830000,'touched':1792830000,'cloud_updated':None,'has_cloud_save':False,'device_sessions':2}],'nextCursor':None}
     else:status=404;body={'error':'not_found'}
     await route.fulfill(status=status,content_type='application/json',body=json.dumps(body))
    await context.route('**/v1/cloud/web/**',api);await context.route('**/v1/cloud/admin/accounts',api)
    await page.goto('http://127.0.0.1:4196/v1/cloud/account?lang='+lang)
    await page.wait_for_function("() => !document.querySelector('#status').textContent.includes('…')")
    assert await page.locator('#profile').is_hidden();assert await page.locator('#signin').is_visible()
    assert '/web/delete' not in requests
    assert not await page.evaluate('document.documentElement.scrollWidth>innerWidth')
    await page.screenshot(path=str(OUT/f'account-{lang}-{width}-signed-out.png'),full_page=True)
    await page.locator('[data-provider=google]').click();await page.wait_for_function("() => !document.querySelector('#profile').hidden")
    await page.locator('#nickname').fill('<img src=x onerror=alert(1)>');await page.locator('#name-form button').click()
    await page.wait_for_function("() => document.querySelector('#status').dataset.tone==='success'")
    assert await page.locator('#profile img').count()==0
    await page.locator('#nickname').fill('Оксана ⛺');await page.locator('#name-form button').click()
    await page.wait_for_function("() => document.querySelector('#status').dataset.tone==='success'")
    await page.locator('[data-link=facebook]').click();await page.wait_for_function("() => document.querySelector('#providers').textContent.includes('Facebook')")
    await page.screenshot(path=str(OUT/f'account-{lang}-{width}-profile.png'),full_page=True)
    await page.locator('#remove').click();assert await page.locator('#delete-dialog').is_visible()
    await page.locator('#keep').click();assert deleted==0
    await page.locator('#remove').click();await page.locator('#confirm-delete').click()
    await page.wait_for_function("() => document.querySelector('#profile').hidden");assert deleted==1
    # Popup cancellation leaves deletion unrequested.
    await page.evaluate("window.mockAuthError='auth/popup-closed-by-user'")
    before=len(requests);await page.locator('[data-provider=google]').click()
    await page.wait_for_function("() => document.querySelector('#status').dataset.tone==='error'")
    assert '/web/delete' not in requests[before:]
    await page.evaluate('window.mockAuthError=null');await page.locator('[data-provider=google]').click()
    await page.wait_for_function("() => !document.querySelector('#profile').hidden")
    fail=True;await page.locator('#remove').click();await page.locator('#confirm-delete').click()
    await page.wait_for_function("() => !document.querySelector('#retry-deletion').hidden")
    # Private directory layout, no public directory on regular profile.
    owner=True;current=None;await page.goto('http://127.0.0.1:4196/v1/cloud/admin?lang='+lang)
    await page.locator('[data-provider=google]').click();await page.wait_for_selector('#accounts tr')
    assert not await page.evaluate('document.documentElement.scrollWidth>innerWidth')
    await page.screenshot(path=str(OUT/f'admin-{lang}-{width}.png'),full_page=True)
    assert not errors,errors
    results.append({'lang':lang,'width':width,'passed':True,'mockedProviders':True,'realAuthentication':False,'productionDeletion':False})
    await context.close()
  await browser.close()
 (OUT/'browser-results.json').write_text(json.dumps(results,indent=2));print(json.dumps(results))
if __name__=='__main__':
 fixture=subprocess.Popen(['node',str(Path(__file__).with_name('ui-fixture-server.mjs'))],stdout=subprocess.DEVNULL)
 try:
  time.sleep(.5);asyncio.run(main())
 finally:fixture.terminate();fixture.wait(timeout=5)
