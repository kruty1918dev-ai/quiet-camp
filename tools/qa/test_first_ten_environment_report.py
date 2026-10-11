#!/usr/bin/env python3
"""Lightweight report integrity and orientation-control contracts; no browser or Unity."""
from html.parser import HTMLParser
from pathlib import Path
from urllib.parse import urlsplit,unquote
import json,hashlib,subprocess,tempfile
ROOT=Path(__file__).resolve().parents[2]
REPORT=ROOT/'Design/Campaign100/2026-10-11/FirstTenRefinement/index.html'
class Page(HTMLParser):
    def __init__(self):super().__init__();self.images=[];self.links=[];self.ids=[];self.scripts=[];self.in_script=False
    def handle_starttag(self,tag,attrs):
        d=dict(attrs)
        if 'id' in d:self.ids.append(d['id'])
        if tag=='img':self.images.append(d['src']);assert d.get('alt')
        if tag=='a':self.links.append(d['href'])
        if tag=='script':self.in_script=True;self.scripts.append('')
    def handle_endtag(self,tag):
        if tag=='script':self.in_script=False
    def handle_data(self,data):
        if self.in_script:self.scripts[-1]+=data
def main():
    text=REPORT.read_text();parsed=Page();parsed.feed(text)
    historical=[s for s in parsed.images if s.startswith('Baseline/')]
    assert len(historical)==20
    restored=(REPORT.parent/'rollback-receipt.json').exists() and not (REPORT.parent/'runtime-publication.json').exists()
    if restored:
        assert text.count('Відновлено · старий варіант')==10 and text.count('Архів · відкладений експеримент')==10
    else:assert text.count('До · native baseline')==10 and text.count('Після · нова реалізація')==10
    for src in parsed.images:
        path=REPORT.parent/src;receipt=json.loads(path.with_suffix('.json').read_text())
        assert hashlib.sha256(path.read_bytes()).hexdigest()==receipt['imageSha256']
        if src.startswith('After/'):
            import sys
            sys.path.insert(0,str(ROOT/'tools'))
            from build_first_ten_environment_report import after_image,archived_after_image
            level=json.loads((ROOT/'QuietCamp/Assets/QuietCamp/Resources/QuietCamp/Levels'/f'{receipt["levelId"]}.json').read_text())
            if restored:
                assert after_image(receipt['number'],receipt['orientation'],level['contentHash']) is None
                assert archived_after_image(receipt['number'],receipt['orientation']) is not None
            else:assert after_image(receipt['number'],receipt['orientation'],level['contentHash']) is not None
    assert len(parsed.ids)==len(set(parsed.ids));assert all(f'QC{i:03d}' in parsed.ids for i in range(1,11))
    for link in parsed.images+parsed.links:
        u=urlsplit(link)
        if u.scheme:continue
        if u.path:assert (REPORT.parent/unquote(u.path)).resolve().is_file(),link
        elif u.fragment:assert u.fragment in parsed.ids,link
    script='\n'.join(parsed.scripts)
    test='''const vm=require('vm');let callback;let portrait=false;const button={textContent:'Показати portrait',attributes:{},addEventListener:(event,handler)=>{if(event!=='click')throw Error('event');callback=handler;},setAttribute:function(k,v){this.attributes[k]=v;}};const document={getElementById:id=>{if(id!=='orientation')throw Error('id');return button;},body:{classList:{toggle:name=>{if(name!=='show-portrait')throw Error('class');portrait=!portrait;return portrait;}}}};vm.runInNewContext(SCRIPT,{document});callback.call(button);if(!portrait||button.attributes['aria-pressed']!=='true'||button.textContent!=='Показати landscape')throw Error('portrait toggle');callback.call(button);if(portrait||button.attributes['aria-pressed']!=='false'||button.textContent!=='Показати portrait')throw Error('landscape toggle');console.log('PASS orientation JS contract');'''.replace('SCRIPT',json.dumps(script))
    with tempfile.TemporaryDirectory(prefix='qc-first-ten-report-') as directory:
        file=Path(directory)/'report.js';file.write_text(test);subprocess.run(['node','--max-old-space-size=64',str(file)],check=True)
    print(f'PASS report integrity: 20 verified baseline images, {len(parsed.images)-20} experiment images (archived={restored}), 10 comparison sections, local links and orientation control. Browser layout/native acceptance not tested.')
if __name__=='__main__':main()
