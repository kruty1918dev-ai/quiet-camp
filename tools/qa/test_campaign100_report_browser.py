#!/usr/bin/env python3
"""Inspect the offline campaign report using a real browser; no Unity or build."""
import argparse,json,datetime
from pathlib import Path
from playwright.sync_api import sync_playwright
ROOT=Path(__file__).resolve().parents[2]
REPORT=ROOT/'Design/Campaign100/2026-10-11/index.html'

def main():
 parser=argparse.ArgumentParser()
 parser.add_argument('--browser-executable')
 parser.add_argument('--output',type=Path,default=Path('/tmp/qc-campaign100-report-browser'))
 args=parser.parse_args();args.output.mkdir(parents=True,exist_ok=True)
 errors=[];checks=[]
 with sync_playwright() as p:
  browser=p.chromium.launch(headless=True,executable_path=args.browser_executable,args=['--disable-dev-shm-usage'])
  page=browser.new_page(viewport={'width':1440,'height':1000},locale='uk-UA')
  page.on('pageerror',lambda e:errors.append(str(e)))
  page.goto(REPORT.as_uri(),wait_until='load')
  assert page.locator('#level-grid .card').count()==100
  assert page.evaluate('DATA.counts.bothOrientations')==100
  assert page.evaluate('DATA.counts.modelCaptures===DATA.counts.models && DATA.counts.models===1342')
  # Load each section in small batches to avoid decoding 200 PNGs simultaneously.
  for orientation in ['landscape','portrait']:
   page.locator('#orientation').select_option(orientation)
   for chapter in range(1,11):
    page.locator('#chapter-filter').select_option(str(chapter))
    assert page.locator('#level-grid .card').count()==10
    page.evaluate('document.querySelectorAll("#level-grid img").forEach(i=>i.loading="eager")')
    page.wait_for_function('Array.from(document.querySelectorAll("#level-grid img")).every(i=>i.complete&&i.naturalWidth>0)')
  checks.append('All 100 scenes load in both orientations through chapter filters')
  page.locator('#chapter-filter').select_option('')
  page.locator('#level-search').fill('літак')
  assert 0<page.locator('#level-grid .card').count()<100
  page.locator('#level-search').fill('not-a-camp-xyz');assert page.locator('#level-grid .card').count()==0
  page.locator('#level-search').fill('')
  page.locator('[data-level="1"]').click()
  assert page.locator('#viewer').evaluate('(d)=>d.open')
  page.wait_for_function('Array.from(document.querySelectorAll("#viewer img")).every(i=>i.complete&&i.naturalWidth>0)')
  assert page.locator('#viewer a[href$=".json"]').count()==2
  page.keyboard.press('Escape');assert not page.locator('#viewer').evaluate('(d)=>d.open')
  checks.append('Search, no-match results, large native frames, provenance links and Escape')
  for tab in ['story','models','audit','checks','levels']:
   page.locator(f'[data-tab="{tab}"]').click();assert page.locator('#'+tab).is_visible()
  page.locator('[data-tab="models"]').click()
  assert page.locator('#model-grid .card').count()==40
  page.locator('#model-more').click();assert page.locator('#model-grid .card').count()==80
  for number in ['M0001','M1215','M1267','M1325','M1342']:
   page.locator('#model-search').fill(number)
   assert page.locator('#model-grid .card').count()==1
   page.wait_for_function('Array.from(document.querySelectorAll("#model-grid img")).every(i=>i.complete&&i.naturalWidth>0)')
   page.locator('#model-grid summary').click()
   page.wait_for_function('Array.from(document.querySelectorAll("#model-grid img")).every(i=>i.complete&&i.naturalWidth>0)')
  checks.append('Model pagination, stable IDs, local photographs and second angles')
  page.locator('[data-tab="audit"]').click();assert page.locator('#audit-body tr').count()==274
  page.locator('#audit-search').fill('QC001');assert page.locator('#audit-body tr').count()==1
  for width,height in [(320,900),(390,844),(768,1024),(1440,1000)]:
   page.set_viewport_size({'width':width,'height':height})
   for tab in ['levels','story','models','audit','checks']:
    page.locator(f'[data-tab="{tab}"]').click()
    assert page.evaluate('document.documentElement.scrollWidth<=innerWidth'),(width,tab)
   page.locator('[data-tab="levels"]').click()
   page.evaluate('window.scrollTo(0,0)')
   if width in [390,1440]:page.screenshot(path=str(args.output/f'report-{width}.png'))
  checks.append('274-row audit and no horizontal overflow at 320–1440 pixels')
  assert not errors,errors
  browser.close()
 receipt=dict(result='Passed',verifiedUtc=datetime.datetime.now(datetime.timezone.utc).isoformat(),checks=checks,pageErrors=errors)
 (args.output/'browser-results.json').write_text(json.dumps(receipt,ensure_ascii=False,indent=2)+'\n')
 print(json.dumps(receipt,ensure_ascii=False))
if __name__=='__main__':main()
