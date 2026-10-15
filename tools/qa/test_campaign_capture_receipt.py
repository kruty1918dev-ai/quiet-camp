"""A finished PNG cannot hide missing drafts or changed Unity sources."""
import hashlib,json,sys,tempfile,unittest
from pathlib import Path
sys.path.insert(0,str(Path(__file__).resolve().parents[1]))
from campaign_capture_receipt import current

class NativeCaptureCheckpoint(unittest.TestCase):
    def setUp(self):
        self.folder=tempfile.TemporaryDirectory();self.root=Path(self.folder.name)
        self.image=self.root/'frame.png';self.image.write_bytes(b'native-image')
        self.draft=self.root/'draft.json';self.draft.write_bytes(b'editable-draft')
        self.source=self.root/'Assets/World.cs';self.source.parent.mkdir();self.source.write_bytes(b'world-source')
        self.assembly=self.root/'Library/ScriptAssemblies/QuietCamp.Presentation.dll';self.assembly.parent.mkdir(parents=True);self.assembly.write_bytes(b'loaded-assembly')
        sha=lambda path:hashlib.sha256(path.read_bytes()).hexdigest()
        self.receipt=self.root/'receipt.json';self.receipt.write_text(json.dumps(dict(kind='native-unity-production-world-components',imageSha256=sha(self.image),draftSha256=sha(self.draft),sourceReceipt=dict(sources=[dict(path='Assets/World.cs',sha256=sha(self.source))],assemblies=[dict(name='QuietCamp.Presentation',sha256=sha(self.assembly))]))),encoding='utf-8')
    def tearDown(self):self.folder.cleanup()
    def valid(self):return current(self.root,self.image,self.receipt,self.draft)
    def test_unchanged_checkpoint_can_resume(self):self.assertTrue(self.valid())
    def test_missing_editable_draft_must_be_recovered(self):
        self.draft.unlink();self.assertFalse(self.valid())
    def test_runtime_source_change_invalidates_the_frame(self):
        self.assertTrue(self.valid());self.source.write_bytes(b'new-world-source');self.assertFalse(self.valid())
    def test_loaded_assembly_change_invalidates_the_frame(self):
        self.assertTrue(self.valid());self.assembly.write_bytes(b'recompiled-world');self.assertFalse(self.valid())
    def test_damaged_image_is_not_a_completed_frame(self):
        self.image.write_bytes(b'truncated');self.assertFalse(self.valid())
    def test_unity_virtual_package_path_resolves_only_the_locked_version(self):
        package='com.example.render';relative='Editor/AssetVersion.cs'
        cache=self.root/'Library/PackageCache'/(package+'@hash');(cache/'Editor').mkdir(parents=True)
        (cache/'package.json').write_text(json.dumps(dict(name=package,version='1.2.3')),encoding='utf-8')
        actual=cache/relative;actual.write_bytes(b'locked-render-source')
        packages=self.root/'Packages';packages.mkdir();lock=packages/'packages-lock.json'
        lock.write_text(json.dumps(dict(dependencies={package:dict(version='1.2.3')})),encoding='utf-8')
        receipt=json.loads(self.receipt.read_text(encoding='utf-8'));receipt['sourceReceipt']['sources']=[dict(path='Packages/'+package+'/'+relative,sha256=hashlib.sha256(actual.read_bytes()).hexdigest())]
        self.receipt.write_text(json.dumps(receipt),encoding='utf-8');self.assertTrue(self.valid())
        lock.write_text(json.dumps(dict(dependencies={package:dict(version='9.0.0')})),encoding='utf-8');self.assertFalse(self.valid())

if __name__=='__main__':unittest.main()
