using System.IO;
using System.Linq;
using System.Xml.Linq;
using System.Text.RegularExpressions;
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
            ApplyBillingDependencies(Path.Combine(unityLibraryPath, "QuietCampPrivacy.androidlib/build.gradle"),
                Path.Combine(UnityEngine.Application.dataPath, "QuietCamp/OptionalSdk/Editor/PlayBillingDependencies.xml"));
            ApplyDependencies(Path.Combine(unityLibraryPath, "QuietCampPrivacy.androidlib/build.gradle"),
                Path.Combine(UnityEngine.Application.dataPath, "QuietCamp/OptionalSdk/Editor/NativeAuthDependencies.xml"), "Native Auth");
        }

        // The androidlib compiles independently: unityLibrary's implementation
        // dependencies are not on this child module's Java compile classpath.
        public static void ApplyBillingDependencies(string gradlePath, string dependenciesPath)
            => ApplyDependencies(gradlePath, dependenciesPath, "Billing");

        public static void ApplyDependencies(string gradlePath, string dependenciesPath, string label)
        {
            if (!File.Exists(gradlePath) || !File.Exists(dependenciesPath))
                throw new BuildFailedException("Billing module or dependency declaration missing.");
            var packages = XDocument.Load(dependenciesPath).Descendants("androidPackage")
                .Select(e => (string)e.Attribute("spec")).Distinct().ToArray();
            if (packages.Length == 0 || packages.Any(p => p == null || !Regex.IsMatch(p, @"^[a-zA-Z0-9_.-]+:[a-zA-Z0-9_.-]+:[0-9][a-zA-Z0-9_.+\-]*$")))
                throw new BuildFailedException("Invalid Billing Maven dependency declaration.");
            string start = "// QuietCamp " + label + " dependencies start", end = "// QuietCamp " + label + " dependencies end";
            string text = File.ReadAllText(gradlePath);
            int first = text.IndexOf(start, System.StringComparison.Ordinal), last = text.IndexOf(end, System.StringComparison.Ordinal);
            if (first >= 0) {
                if (last < first) throw new BuildFailedException("Incomplete Billing dependency block.");
                text = text.Remove(first, last + end.Length - first);
            }
            File.WriteAllText(gradlePath, text.TrimEnd() + "\n" + start + "\ndependencies {\n"
                + string.Join("\n", packages.Select(p => "    implementation '" + p + "'")) + "\n}\n" + end + "\n");
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
            foreach(var attribute in rules.Attributes().Where(a=>a.Name.Namespace==android))
                app.SetAttributeValue(attribute.Name,attribute.Value);
            var replacements=((string)app.Attribute(tools+"replace") ?? "").Split(',')
                .Concat(((string)rules.Attribute(tools+"replace") ?? "").Split(','))
                .Select(value=>value.Trim()).Where(value=>value.Length>0).Distinct().ToArray();
            if(replacements.Length>0) app.SetAttributeValue(tools+"replace",string.Join(",",replacements));
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
