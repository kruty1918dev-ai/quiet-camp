using System.IO;
using System.Linq;
using System.Xml.Linq;
using UnityEditor.Build;
#if UNITY_ANDROID
using UnityEditor.Android;
#endif

namespace QuietCamp.Editor
{
    // The app manifest has priority over every dependency. A removal marker in
    // an androidlib alone can lose to Firebase's transitive Maven dependency.
    public sealed class AndroidPrivacyBuild
#if UNITY_ANDROID
        : IPostGenerateGradleAndroidProject
#endif
    {
        public int callbackOrder => 1000;

        public void OnPostGenerateGradleAndroidProject(string unityLibraryPath)
        {
            var launcher = Path.GetFullPath(Path.Combine(unityLibraryPath,
                "../launcher/src/main/AndroidManifest.xml"));
            var policy = Path.Combine(UnityEngine.Application.dataPath,
                "Plugins/Android/QuietCampPrivacy.androidlib/AndroidManifest.xml");
            ApplyPolicy(launcher, policy);
        }

        public static void ApplyPolicy(string launcherPath, string policyPath)
        {
            if (!File.Exists(launcherPath) || !File.Exists(policyPath))
                throw new BuildFailedException("Android privacy manifests are missing; refusing an unguarded build.");
            XNamespace android = "http://schemas.android.com/apk/res/android";
            XNamespace tools = "http://schemas.android.com/tools";
            var document = XDocument.Load(launcherPath);
            var policy = XDocument.Load(policyPath);
            var app = document.Root?.Element("application");
            var rules = policy.Root?.Element("application");
            if (app == null || rules == null)
                throw new BuildFailedException("Android privacy manifest has no application element.");
            document.Root.SetAttributeValue(XNamespace.Xmlns + "android", android.NamespaceName);
            document.Root.SetAttributeValue(XNamespace.Xmlns + "tools", tools.NamespaceName);
            foreach (var rule in rules.Elements())
            {
                var name = (string)rule.Attribute(android + "name");
                if (string.IsNullOrEmpty(name))
                    throw new BuildFailedException("Android privacy rule has no component name.");
                app.Elements(rule.Name).Where(e => (string)e.Attribute(android + "name") == name).Remove();
                app.Add(new XElement(rule));
            }
            document.Save(launcherPath);
        }
    }
}
