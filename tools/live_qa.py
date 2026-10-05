#!/usr/bin/env python3
"""Sync the working game into the warm QA project without touching player saves."""
import argparse, json, pathlib, subprocess
root = pathlib.Path(__file__).resolve().parents[1]
parser = argparse.ArgumentParser(description=__doc__)
parser.add_argument('--target', type=pathlib.Path, default=pathlib.Path('/tmp/quietcamp-qa-20261002/QuietCamp'))
parser.add_argument('--warm-library', type=pathlib.Path)
parser.add_argument('--package-candidate', action='store_true')
parser.add_argument('--virtual-touch', action='store_true', help='Route virtual touch devices to GameView regardless of desktop focus, only in the isolated QA editor.')
parser.add_argument('--test-progress', action='store_true', help='Log individual test results in the isolated Editor, including interrupted runs.')
args = parser.parse_args()
target = args.target.expanduser().resolve()
target.mkdir(parents=True, exist_ok=True)
for folder in ['Assets','ProjectSettings','Packages']:
    (target/folder).mkdir(exist_ok=True)
    subprocess.run(['rsync','-a','--delete',str(root/'QuietCamp'/folder)+'/',str(target/folder)+'/'],check=True)
if args.warm_library and not (target/'Library').exists():
    subprocess.run(['rsync','-a','--exclude=SourceAssetDB-lock','--exclude=ArtifactDB-lock',str(args.warm_library.resolve())+'/',str(target/'Library')+'/'],check=True)
p = target/'Packages/manifest.json'
d = json.loads(p.read_text())
for name, value in list(d['dependencies'].items()):
    if value.startswith('file:../../../'):
        source = (root/'QuietCamp/Packages'/value[5:]).resolve()
        # Unity writes importer metadata even while running Editor tests. Copy
        # sibling packages too, so a QA refresh never modifies another checkout.
        isolated = target.parent/'ExternalPackages'/name
        isolated.mkdir(parents=True, exist_ok=True)
        subprocess.run(['rsync','-a','--delete','--exclude=.git','--exclude=Library',
                        '--exclude=bin','--exclude=obj',str(source)+'/',str(isolated)+'/'],check=True)
        # Ignored UPM folders do not have Unity metadata. Old orphan metadata
        # in sibling workspaces can otherwise emit errors during test cleanup.
        for meta in isolated.rglob('*~.meta'):
            meta.unlink()
        d['dependencies'][name] = 'file:' + str(isolated)
# Local candidate is verified here before the game pins the published commit.
if args.package_candidate:
    d['dependencies']['com.kruty1918.moyva.unityhtml'] = 'file:' + str(root.parent/'unityhtml')
d['testables'] = list(dict.fromkeys(d.get('testables', []) + ['com.kruty1918.moyva.unityhtml']))
p.write_text(json.dumps(d,indent=2)+'\n')
p = target/'ProjectSettings/ProjectSettings.asset'
s=p.read_text(); import re
s=re.sub(r'(?m)^  productName:.*$', '  productName: QuietCampLiveQA20261002', s)
p.write_text(s)
if args.virtual_touch:
    editor = target/'Assets/Editor'
    editor.mkdir(exist_ok=True)
    (editor/'TouchQaFocus.cs').write_text("""using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
[InitializeOnLoad] static class TouchQaFocus {
 static TouchQaFocus(){ EditorApplication.delayCall += () => {
  InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
  Debug.Log("[TouchQA fixture] Virtual device input goes to GameView independently of desktop focus");
 }; }
}
""")
print(target)
if args.test_progress:
    editor = target/'Assets/Editor'
    editor.mkdir(exist_ok=True)
    (editor/'CozyQaTestProgress.cs').write_text('''using UnityEditor;
using UnityEngine;
using UnityEditor.TestTools.TestRunner.Api;
[InitializeOnLoad] static class CozyQaTestProgress {
 static TestRunnerApi api;
 static CozyQaTestProgress(){ EditorApplication.delayCall += () => {
  api=ScriptableObject.CreateInstance<TestRunnerApi>(); api.RegisterCallbacks(new Progress());
 }; }
 sealed class Progress:ICallbacks {
  public void RunStarted(ITestAdaptor test){Debug.Log("[Cozy QA] Run started "+test.FullName);}
  public void RunFinished(ITestResultAdaptor result){Debug.Log("[Cozy QA] Run finished "+result.ResultState);}
  public void TestStarted(ITestAdaptor test){if(!test.IsSuite)Debug.Log("[Cozy QA] START "+test.FullName);}
  public void TestFinished(ITestResultAdaptor result){if(!result.Test.IsSuite)Debug.Log("[Cozy QA] END "+result.Test.FullName+" => "+result.ResultState+" "+result.Message);}
 }
}''')
