# Browser checks with mocked Firebase modules and API responses. Never deletes real accounts.
import asyncio,json,subprocess,time,os,tempfile
from pathlib import Path
from playwright.async_api import async_playwright
ROOT=Path(os.environ.get('QC_UI_ARTIFACTS',tempfile.mkdtemp(prefix='quietcamp-ui-')))
ROOT.mkdir(parents=True,exist_ok=True)
FIXTURE=Path(__file__).with_name('ui-fixture-server.mjs')
APP="export const initializeApp=c=>c;"
AUTH="""
export const getAuth=()=>({}),inMemoryPersistence={};
export async function setPersistence(){}
export class GoogleAuthProvider{setCustomParameters(v){window.providerParams=v;}}
export class FacebookAuthProvider{}
export async function signOut(){window.signedOut=true;}
export async function signInWithPopup(){
 window.popupCalls=(window.popupCalls||0)+1;
 await new Promise(r=>setTimeout(r,80));
 if(window.mockError)throw {code:window.mockError};
 return {user:{email:'camper@example.test',getIdToken:async()=> 'synthetic-token-only'}};
}
"""
async def run():
 results=[]
 async with async_playwright() as p:
  browser=await p.chromium.launch(executable_path='/usr/bin/chromium',headless=True,args=['--no-sandbox','--disable-dev-shm-usage'])
  for mobile in (False,True):
   context=await browser.new_context(viewport={'width':390 if mobile else 1440,'height':844 if mobile else 1000},device_scale_factor=1)
   errors=[];calls=[];fail_delete=False
   context.on('page',lambda page:page.on('pageerror',lambda error:errors.append(str(error))))
   context.on('page',lambda page:page.on('console',lambda msg:errors.append(msg.text) if 'Content Security Policy' in msg.text else None))
   await context.route('https://www.gstatic.com/**',lambda route:route.fulfill(status=200,content_type='text/javascript',body=AUTH if 'firebase-auth' in route.request.url else APP))
   async def api(route):
    calls.append({'path':route.request.url.split('/v1/cloud/')[-1],'body':route.request.post_data_json})
    await asyncio.sleep(.1)
    await route.fulfill(status=500 if fail_delete else 200,content_type='application/json',body='{}')
   await context.route('**/v1/cloud/delete-federated',api)
   await context.route('**/v1/cloud/login-complete',api)
   await context.route('**/v1/cloud/login-cancel',api)
   page=await context.new_page()
   async def fresh(path='/delete-account'):
    await page.goto('about:blank')
    await page.goto('http://127.0.0.1:4196/v1/cloud'+path)
    await page.wait_for_function("() => document.getElementById('status').textContent!=='Preparing secure sign-in…'")
   await fresh()
   assert await page.locator('#delete').is_hidden()
   assert await page.locator('#delete').is_disabled()
   assert await page.locator('.provider').count()==2
   assert await page.evaluate('document.documentElement.scrollWidth<=innerWidth')
   await page.screenshot(path=str(ROOT/('delete-account-mobile.png' if mobile else 'delete-account-desktop.png')),full_page=True)
   await page.evaluate("window.mockError='auth/popup-blocked'")
   await page.get_by_role('button',name='Continue with Google').click()
   await page.wait_for_function("() => document.getElementById('status').dataset.tone==='error'")
   assert await page.locator('#confirmation').is_hidden()
   await page.evaluate('window.mockError=null')
   await page.evaluate("() => {const b=document.querySelector('.provider');b.click();b.click();}")
   await page.wait_for_selector('#confirmation',state='visible')
   assert await page.locator('#identity').inner_text()=='camper@example.test'
   assert await page.evaluate('window.popupCalls')==2 # failed attempt + one successful popup, despite double-click
   assert await page.evaluate('window.providerParams.prompt')=='select_account'
   assert not calls
   await page.screenshot(path=str(ROOT/('confirm-mobile.png' if mobile else 'confirm-desktop.png')),full_page=True)
   page.once('dialog',lambda dialog:dialog.dismiss())
   await page.locator('#delete').click()
   assert not calls
   await page.get_by_role('button',name='Use a different account').click()
   assert await page.locator('#delete').is_hidden()
   assert await page.locator('#delete').is_disabled()
   await page.get_by_role('button',name='Continue with Facebook').click()
   await page.wait_for_selector('#confirmation',state='visible')
   fail_delete=True
   page.once('dialog',lambda dialog:dialog.accept())
   await page.locator('#delete').click()
   await page.wait_for_function("() => document.getElementById('status').dataset.tone==='error'")
   assert await page.locator('#delete').is_hidden()
   assert await page.locator('#delete').is_disabled()
   fail_delete=False
   await page.get_by_role('button',name='Continue with Google').click()
   await page.wait_for_selector('#confirmation',state='visible')
   page.once('dialog',lambda dialog:dialog.accept())
   await page.locator('#delete').click()
   await page.wait_for_selector('#complete',state='visible')
   assert 'Your account is deleted.' in await page.locator('#complete-title').inner_text()
   assert len(calls)==2
   assert calls[-1]=={'path':'delete-federated','body':{'idToken':'synthetic-token-only','confirm':True}}
   assert await page.locator('#delete').is_hidden()
   await page.screenshot(path=str(ROOT/('success-mobile.png' if mobile else 'success-desktop.png')),full_page=True)
   await fresh('/login')
   assert await page.locator('.provider').count()==0
   ticket='a'*64
   await fresh('/login#'+ticket+':google')
   assert await page.locator('.provider').count()==1
   assert 'AAAAAAAA' in await page.locator('#status').inner_text()
   assert await page.evaluate('location.hash')==''
   await page.get_by_role('button',name='Continue with Google').click()
   await page.wait_for_selector('#complete',state='visible')
   assert calls[-1]['path']=='login-complete'
   assert calls[-1]['body']['ticket']==ticket
   await fresh('/login#'+ticket+':facebook')
   await page.evaluate("window.mockError='auth/popup-closed-by-user'")
   await page.get_by_role('button',name='Continue with Facebook').click()
   await page.wait_for_function("() => document.getElementById('status').textContent.startsWith('Sign-in cancelled')")
   assert calls[-1]['path']=='login-cancel'
   assert await page.locator('.provider').count()==0
   assert not errors,errors
   results.append({'viewport':'mobile' if mobile else 'desktop','passed':True,'checks':['hidden and disabled before auth','responsive layout','popup failure','Google account chooser','concurrent sign-in blocked','identity displayed as text','confirmation cancelled sends no POST','switch account resets identity','deletion failure requires reauth','confirmed deletion success','handoff success and cancellation','no page errors']})
   await context.close()
  # SDK failure must produce a usable message instead of leaving the page loading.
  context=await browser.new_context()
  await context.route('https://www.gstatic.com/**',lambda route:route.abort())
  page=await context.new_page();await page.goto('http://127.0.0.1:4196/v1/cloud/delete-account')
  await page.wait_for_function("() => document.getElementById('status').dataset.tone==='error'")
  assert await page.locator('#delete').is_hidden()
  results.append({'sdk_load_failure':'passed'})
  await context.close();await browser.close()
 (ROOT/'browser-results.json').write_text(json.dumps(results,indent=2))
 print(json.dumps(results,indent=2))
server=subprocess.Popen(['node',str(FIXTURE)],stdout=subprocess.DEVNULL,stderr=subprocess.PIPE)
try:
 time.sleep(.6)
 asyncio.run(run())
finally:
 server.terminate();server.wait(timeout=10)
