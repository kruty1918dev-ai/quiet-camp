using System;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using NUnit.Framework;
using QuietCamp.Editor;
using UnityEditor.Build;

namespace QuietCamp.Tests.Editor
{
    public sealed class AndroidPrivacyBuildTests
    {
        [Test]
        public void PrivacyRulesReachApplicationManifestAndRemainIdempotent()
        {
            var folder = Path.Combine(Path.GetTempPath(), "quietcamp-manifest-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(folder);
            try
            {
                var manifest = Path.Combine(folder, "AndroidManifest.xml");
                var policy = Path.Combine(UnityEngine.Application.dataPath,
                    "Plugins/Android/QuietCampPrivacy.androidlib/AndroidManifest.xml");
                File.WriteAllText(manifest, "<manifest xmlns:android='http://schemas.android.com/apk/res/android'><application android:label='preserved'><provider android:name='unrelated.Provider'/><meta-data android:name='firebase_analytics_collection_enabled' android:value='true'/></application></manifest>");
                AndroidPrivacyBuild.ApplyPolicy(manifest, policy);
                AndroidPrivacyBuild.ApplyPolicy(manifest, policy);
                var app = XDocument.Load(manifest).Root.Element("application");
                XNamespace android = "http://schemas.android.com/apk/res/android";
                XNamespace tools = "http://schemas.android.com/tools";
                Assert.That((string)app.Attribute(android + "label"), Is.EqualTo("preserved"));
                Assert.That(app.Elements("provider").Any(e => (string)e.Attribute(android + "name") == "unrelated.Provider"), Is.True);
                var removal = app.Elements("provider").Single(e => (string)e.Attribute(android + "name") == "com.google.firebase.provider.FirebaseInitProvider");
                Assert.That((string)removal.Attribute(tools + "node"), Is.EqualTo("remove"));
                var analytics = app.Elements("meta-data").Single(e => (string)e.Attribute(android + "name") == "firebase_analytics_collection_enabled");
                Assert.That((string)analytics.Attribute(android + "value"), Is.EqualTo("false"));
            }
            finally { Directory.Delete(folder, true); }
        }

        [Test]
        public void MissingPrivacyPolicyStopsBuild()
        {
            Assert.Throws<BuildFailedException>(() => AndroidPrivacyBuild.ApplyPolicy("missing-launcher", "missing-policy"));
        }
    }
}
