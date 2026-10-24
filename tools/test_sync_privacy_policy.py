"""Exercise policy synchronization in temporary draft/published projects; no network."""
import json
from pathlib import Path
import shutil
import subprocess
import sys
import tempfile
import unittest
from urllib.parse import urlsplit

SOURCE = Path(__file__).with_name('sync_privacy_policy.py')
BASE = 'https://oleksiy-havryliuk.de/quiet-camp/privacy'

class BundledPublicationTests(unittest.TestCase):
    def test_boot_policy_matches_published_metadata_and_origins(self):
        resources = SOURCE.parents[1]/'QuietCamp/Assets/QuietCamp/Resources/QuietCamp'
        bundle = json.loads((resources/'privacy_content.json').read_text(encoding='utf8'))
        legal = json.loads((resources/'legal.json').read_text(encoding='utf8'))
        self.assertEqual(legal['policyIsDraft'], bundle['draft'])
        if bundle['draft']:
            return
        document = bundle['document']
        self.assertEqual({'en', 'uk', 'de'}, {v['language'] for v in document['variants']})
        for variant in document['variants']:
            with self.subTest(language=variant['language']):
                self.assertEqual(legal['revision'], variant['revision'], 'Boot rejects mismatched policy revisions')
                url = urlsplit(variant['url'])
                self.assertEqual('https', url.scheme)
                self.assertIsNone(url.username)
                self.assertIsNone(url.password)
                self.assertIn(f'{url.scheme}://{url.netloc}', document['allowedOrigins'])
                self.assertEqual(variant['sourceUrl'], variant['url'])
                summary = next(s for s in bundle['summaries'] if s['language'] == variant['language'])
                self.assertEqual(legal['revision'], summary['revision'])

class SyncPolicyTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory(prefix='qc-policy-sync-')
        self.addCleanup(self.temp.cleanup)
        self.root = Path(self.temp.name)
        self.game = self.root/'game'
        self.site = self.root/'site'
        (self.game/'tools').mkdir(parents=True)
        shutil.copyfile(SOURCE, self.game/'tools/sync_privacy_policy.py')
        self.resources = self.game/'QuietCamp/Assets/QuietCamp/Resources/QuietCamp'
        self.resources.mkdir(parents=True)
        (self.site/'legal/quiet-camp').mkdir(parents=True)
        (self.site/'legal/controller.json').write_text('{}')
        content = {}
        for lang in ['en','uk','de']:
            suffix = '' if lang == 'en' else '/'+lang
            sections = [{'id':'section-'+str(i),'title':'Section '+str(i),'paragraphs':['Fixture '+lang+' '+str(i),'Second paragraph '+lang+' '+str(i)]} for i in range(14)]
            content[lang] = dict(revision='new-revision',title='Fixture policy',updated='Updated',date='2026-10-24',status='',notice='',intro='Fixture only',sections=sections)
            page = self.site/('dist/quiet-camp/privacy'+suffix)/'index.html'
            page.parent.mkdir(parents=True)
            page.write_text('<link rel="canonical" href="'+BASE+suffix+'">'+''.join('<section id="'+s['id']+'"><p>'+'</p><p>'.join(s['paragraphs'])+'</p></section>' for s in sections))
        (self.site/'legal/quiet-camp/content.json').write_text(json.dumps(content))

    def setup_state(self, draft):
        variants = [dict(language=l,revision='old-revision',bundledBody='Old body',url='' if draft else BASE+('' if l=='en' else '/'+l)) for l in ['en','uk','de']]
        bundle = dict(draft=draft,document=dict(variants=variants),summaries=[dict(language=l,revision='old-revision',text='Old summary') for l in ['en','uk','de']])
        legal = dict(policyIsDraft=draft,revision='old-revision',analyticsReviewed=False,teenPrivacyReviewed=False,accountsEnabled=True)
        self.bundle_path = self.resources/'privacy_content.json'
        self.legal_path = self.resources/'legal.json'
        self.bundle_path.write_text(json.dumps(bundle))
        self.legal_path.write_text(json.dumps(legal))
        return legal

    def run_sync(self, *args):
        return subprocess.run([sys.executable,str(self.game/'tools/sync_privacy_policy.py'),'--site-root',str(self.site),*args],capture_output=True,text=True)

    def test_published_update_keeps_publication_and_service_flags(self):
        original = self.setup_state(False)
        result = self.run_sync('--write-published')
        self.assertEqual(0,result.returncode,result.stderr)
        legal = json.loads(self.legal_path.read_text())
        self.assertEqual({**original,'revision':'new-revision'},legal)
        bundle = json.loads(self.bundle_path.read_text())
        self.assertFalse(bundle['draft'])
        self.assertTrue(all(v['url'].startswith(BASE) and v['revision']=='new-revision' for v in bundle['document']['variants']))
        self.assertEqual(0,self.run_sync().returncode)

    def test_candidate_write_cannot_overwrite_published_policy(self):
        self.setup_state(False)
        original = self.bundle_path.read_bytes()
        result = self.run_sync('--write')
        self.assertNotEqual(0,result.returncode)
        self.assertIn('Candidate sync refuses',result.stderr)
        self.assertEqual(original,self.bundle_path.read_bytes())

    def test_published_mode_cannot_activate_draft(self):
        self.setup_state(True)
        original = self.legal_path.read_bytes()
        result = self.run_sync('--write-published')
        self.assertNotEqual(0,result.returncode)
        self.assertIn('requires an already published policy',result.stderr)
        self.assertEqual(original,self.legal_path.read_bytes())

    def test_draft_write_preserves_draft_and_remote_fetch_disabled(self):
        original = self.setup_state(True)
        result = self.run_sync('--write')
        self.assertEqual(0,result.returncode,result.stderr)
        bundle = json.loads(self.bundle_path.read_text())
        self.assertTrue(bundle['draft'])
        self.assertTrue(all(v['url']=='' for v in bundle['document']['variants']))
        self.assertEqual({**original,'revision':'new-revision'},json.loads(self.legal_path.read_text()))

    def test_conflicting_publication_flags_fail_before_writes(self):
        self.setup_state(False)
        self.legal_path.write_text(json.dumps(dict(policyIsDraft=True,revision='old-revision')))
        original = self.bundle_path.read_bytes()
        result = self.run_sync('--write-published')
        self.assertNotEqual(0,result.returncode)
        self.assertIn('publication states differ',result.stderr)
        self.assertEqual(original,self.bundle_path.read_bytes())

if __name__ == '__main__':
    unittest.main()
