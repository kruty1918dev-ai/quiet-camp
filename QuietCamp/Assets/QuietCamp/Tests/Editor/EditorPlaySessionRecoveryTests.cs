using System;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using QuietCamp.Application;
using QuietCamp.Editor;
using QuietCamp.Infrastructure;

namespace QuietCamp.Tests
{
    public sealed class EditorPlaySessionRecoveryTests
    {
        [Test] public void RamSnapshotRestoresSessionSettingsAndAllSaveModulesWithoutADiskSave()
        {
            var original=new SaveAdapter(memoryOnly:true);
            original.Session.levelId="QC001";original.Settings.textScale=1.3f;
            original.Progress.tutorial=new TutorialSaveData{initialized=true,explainedSigns=new[]{"shade","quiet"}};
            var json=EditorPlaySessionRecovery.Capture(original,"Camp","QC001",true,"Assets/QuietCamp/Scenes/Boot.unity");
            var restored=EditorPlaySessionRecovery.RestoreMemory(json);
            Assert.IsTrue(restored.MemoryOnly);Assert.IsFalse(restored.HasSave);Assert.IsTrue(restored.Save());
            Assert.AreEqual("QC001",restored.Session.levelId);Assert.AreEqual(1.3f,restored.Settings.textScale);
            CollectionAssert.AreEqual(new[]{"shade","quiet"},restored.Progress.tutorial.explainedSigns);
            var after=EditorPlaySessionRecovery.Capture(restored,"Camp","QC001",true,"Assets/QuietCamp/Scenes/Boot.unity");
            Assert.IsTrue(JToken.DeepEquals(JObject.Parse(json)["data"],JObject.Parse(after)["data"]));
            Assert.AreEqual(8,((JObject)JObject.Parse(json)["data"]).Count);
        }
        [Test] public void ReloadSnapshotDoesNotShareMutableSettingsWithTheOldGraph()
        {
            var original=new SaveAdapter(memoryOnly:true);original.Settings.textScale=1.3f;
            var json=EditorPlaySessionRecovery.Capture(original,"MainMenu",null,true,null);
            var restored=EditorPlaySessionRecovery.RestoreMemory(json);restored.Settings.textScale=1;
            Assert.AreEqual(1.3f,original.Settings.textScale);
        }
        [Test] public void OrdinarySavesCannotBeSilentlyConvertedToRamSnapshots()
        {
            var json=EditorPlaySessionRecovery.Capture(new SaveAdapter(),"MainMenu",null,false,null);
            Assert.Throws<InvalidOperationException>(()=>EditorPlaySessionRecovery.RestoreMemory(json));
        }
    }
}
