"""Verify the offline seasonal gallery, including native image and video previews."""
from pathlib import Path
import json
from playwright.sync_api import sync_playwright
from check_level_authoring_guide import References
ROOT=Path(__file__).resolve().parents[1];pagefile=ROOT/'Design/LevelAuthoring/SeasonAtmosphere/index.html'
refs=References();refs.feed(pagefile.read_text(encoding='utf-8'))
for name in refs.paths:assert (pagefile.parent/name).is_file(),name
with sync_playwright() as engine:
 browser=engine.chromium.launch(executable_path=r'C:/Program Files (x86)/Microsoft/Edge/Application/msedge.exe',headless=True)
 page=browser.new_page(viewport={'width':1280,'height':900});errors=[];network=[]
 page.on('pageerror',lambda e:errors.append(str(e)));page.on('request',lambda r:network.append(r.url) if r.url.startswith(('http:','https:')) else None)
 page.goto(pagefile.as_uri());assert page.locator('article').count()==20
 page.locator('[data-filter="winter"]').click();assert page.locator('article:visible').count()==5
 page.locator('#search').fill('завірюха');assert page.locator('article:visible').count()==1
 page.locator('#search').fill('zzzz');assert page.locator('article:visible').count()==0
 page.locator('#search').fill('');page.locator('[data-filter="all"]').click();assert page.locator('article:visible').count()==20
 for image in page.locator('article img').all():
  image.scroll_into_view_if_needed();image.evaluate('(i)=>i.loading="eager"');page.wait_for_function('(i)=>i.complete&&i.naturalWidth>=512',arg=image.element_handle())
 page.locator('article .preview').first.click();assert page.locator('#zoom').evaluate('(e)=>e.open');page.keyboard.press('Escape');assert not page.locator('#zoom').evaluate('(e)=>e.open')
 assert page.locator('video').count()==4
 for video in page.locator('video').all():
  video.scroll_into_view_if_needed();page.wait_for_function('(v)=>v.readyState>=1&&v.videoWidth>0&&Number.isFinite(v.duration)',arg=video.element_handle())
 page.set_viewport_size({'width':390,'height':844});assert page.evaluate('document.documentElement.scrollWidth<=innerWidth')
 assert not errors,errors;assert not network,network;browser.close()
proof={'offline':True,'cards':20,'filtersAndSearch':True,'allImagesLoaded':True,'zoomAndEscape':True,'nativeVideosLoaded':4,'mobileWidth':390,'horizontalOverflow':False,'javascriptErrors':errors,'networkRequests':network}
(ROOT/'Design/LevelAuthoring/SeasonAtmosphere/VerificationV2/gallery-browser.json').write_text(json.dumps(proof,indent=2),encoding='utf-8')
print('PASS offline gallery: 20 images, filters/search/zoom, 4 native clips, mobile width; no JS errors or network')
