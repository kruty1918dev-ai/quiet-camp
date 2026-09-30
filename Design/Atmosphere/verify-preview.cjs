// Verify only this design package; does not run or change Unity.
const fs=require('fs'),path=require('path'),os=require('os'),cp=require('child_process'),{pathToFileURL,fileURLToPath}=require('url');
const base=__dirname,wait=ms=>new Promise(r=>setTimeout(r,ms)),assert=(c,m)=>{if(!c)throw Error(m)};
(async()=>{
 const files=fs.readdirSync(base).filter(n=>/\.(md|html)$/.test(n));let links=0;
 for(const name of files){const t=fs.readFileSync(path.join(base,name),'utf8');assert(!t.includes('\uFFFD'),'UTF8 '+name);const refs=name.endsWith('.md')?[...t.matchAll(/\]\(([^)]+)\)/g)].map(m=>m[1]):[...t.matchAll(/(?:src|href)="([^"]+)"/g)].map(m=>m[1]);for(const ref of refs){if(/^(https?:|data:|#)/.test(ref))continue;assert(fs.existsSync(path.resolve(base,decodeURIComponent(ref.split('#')[0]))),'Broken link '+name+' '+ref);links++;}}
 const chrome='C:/Program Files/Google/Chrome/Application/chrome.exe';assert(fs.existsSync(chrome),'Chrome missing');
 const profile=fs.mkdtempSync(path.join(os.tmpdir(),'quietcamp-atmosphere-preview-'));
 const child=cp.spawn(chrome,['--headless=new','--disable-gpu','--no-first-run','--no-default-browser-check','--remote-debugging-port=0','--user-data-dir='+profile,'about:blank'],{windowsHide:true,stdio:'ignore'});
 let ws;
 try{
  const portFile=path.join(profile,'DevToolsActivePort');for(let i=0;i<150&&!fs.existsSync(portFile);i++)await wait(100);
  assert(fs.existsSync(portFile),'Chrome did not start');const port=Number(fs.readFileSync(portFile,'utf8').split('\n')[0]);
  const targets=await(await fetch('http://127.0.0.1:'+port+'/json')).json(),target=targets.find(t=>t.type==='page');assert(target,'No Chrome page');
  ws=new WebSocket(target.webSocketDebuggerUrl);await new Promise((res,rej)=>{ws.onopen=res;ws.onerror=rej});
  let seq=0;const pending=new Map(),errors=[];
  ws.onmessage=e=>{const m=JSON.parse(e.data);if(m.id){const p=pending.get(m.id);pending.delete(m.id);if(m.error)p.reject(Error(JSON.stringify(m.error)));else p.resolve(m.result)}else if(m.method==='Runtime.exceptionThrown')errors.push(m.params.exceptionDetails.text)};
  const call=(method,params={})=>new Promise((resolve,reject)=>{const id=++seq;pending.set(id,{resolve,reject});ws.send(JSON.stringify({id,method,params}))});
  const evalJS=async expression=>{const r=await call('Runtime.evaluate',{expression,returnByValue:true,awaitPromise:true});assert(!r.exceptionDetails,'JS exception '+JSON.stringify(r.exceptionDetails));return r.result.value};
  await call('Runtime.enable');await call('Page.enable');await call('Emulation.setDeviceMetricsOverride',{width:1280,height:1100,deviceScaleFactor:1,mobile:false});
  await call('Page.navigate',{url:pathToFileURL(path.join(base,'preview.html')).href});
  for(let i=0;i<100;i++){if(await evalJS("document.readyState==='complete'&&!!window.previewState"))break;await wait(100);}
  assert(await evalJS("[...document.images].every(i=>i.complete&&i.naturalWidth>0)"),'Image load');
  for(const p of ['morning','noon','evening','night']){await evalJS("document.querySelector('[data-set=\""+p+"\"]').click()");assert((await evalJS('previewState()')).phase===p,'Phase '+p);}
  await evalJS("document.getElementById('phoneToggle').click();document.getElementById('rearToggle').click();document.getElementById('safeToggle').click();");
  assert(await evalJS("document.getElementById('stage').classList.contains('phone')&&!document.getElementById('rear').hidden&&!document.getElementById('protected').hidden"),'Toggles');
  await evalJS("document.getElementById('transition').click()");
  assert((await evalJS('previewState()')).busy,'Transition did not lock');
  await wait(750);assert(await evalJS("Number(getComputedStyle(document.getElementById('cover')).opacity)===1"),'Cover not opaque during hold');
  await wait(2500);assert(!(await evalJS('previewState()')).busy,'Transition did not release');
  await evalJS("document.getElementById('reducedToggle').checked=true;document.getElementById('reducedToggle').dispatchEvent(new Event('change'));document.getElementById('transition').click()");
  await wait(2400);assert(!(await evalJS('previewState()')).busy,'Reduced transition stuck');
  await evalJS("document.getElementById('phoneToggle').click();document.getElementById('rearToggle').click();document.getElementById('safeToggle').click();document.querySelector('[data-set=\"evening\"]').click()");
  await wait(1600);const shot=await call('Page.captureScreenshot',{format:'png',captureBeyondViewport:false});fs.writeFileSync(path.join(base,'preview-browser.png'),Buffer.from(shot.data,'base64'));
  for(const width of [360,390,768,1280]){await call('Emulation.setDeviceMetricsOverride',{width,height:1100,deviceScaleFactor:1,mobile:width<500});assert(await evalJS('document.documentElement.scrollWidth<=innerWidth'),'Horizontal overflow '+width);}
  await call('Page.navigate',{url:pathToFileURL(path.join(base,'audio-listening.html')).href});await wait(700);
  const urls=await evalJS("[...document.querySelectorAll('audio')].map(a=>a.src)");assert(urls.length===23,'Audio count');urls.forEach(u=>assert(fs.existsSync(fileURLToPath(u)),'Audio link '+u));
  assert(errors.length===0,'Browser exceptions '+errors.join(';'));
  console.log(JSON.stringify({result:'PASS',localLinks:links,images:7,phases:4,viewports:4,transitions:2,audioSources:urls.length,browserExceptions:errors.length}));
  await call('Browser.close').catch(()=>{});
 }finally{if(ws)ws.close();child.kill();}
})().catch(e=>{console.error(e.message);process.exitCode=1;});

