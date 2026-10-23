using System;
using System.IO;
using Newtonsoft.Json;
using NUnitLite;
using QuietCamp.Domain;

// Executes the real focused EditMode test sources on .NET with file-backed
// platform doubles. It does not instantiate Unity, native Firebase or a player.
class Program
{
    static int Main(string[] args) => Array.IndexOf(args,"--complimentary-http-probe") >= 0
        ? ComplimentaryHttpProbe.Run().GetAwaiter().GetResult() : new AutoRun().Execute(args);
}
namespace UnityEngine
{
    public enum RuntimeInitializeLoadType { SubsystemRegistration }
    public sealed class RuntimeInitializeOnLoadMethodAttribute : Attribute
    { public RuntimeInitializeOnLoadMethodAttribute(RuntimeInitializeLoadType type) { } }
    public enum RuntimePlatform { Android, IPhonePlayer }
    public static class Application
    {
        public static string persistentDataPath = Path.GetTempPath();
        public static string dataPath = Path.GetFullPath("QuietCamp/Assets");
        public static bool isEditor = true;
        public static RuntimePlatform platform = RuntimePlatform.Android;
    }
    public static class Debug
    {
        public static bool isDebugBuild = true;
        public static void Log(string message) { }
    }
    public sealed class TextAsset { public string text; }
    public static class Resources
    {
        public static T Load<T>(string path) where T : class
        {
            string file = Path.Combine("QuietCamp/Assets/QuietCamp/Resources", path + ".json");
            return typeof(T) == typeof(TextAsset) && File.Exists(file) ? new TextAsset { text = File.ReadAllText(file) } as T : null;
        }
    }
    public static class JsonUtility
    { public static T FromJson<T>(string json) => JsonConvert.DeserializeObject<T>(json); }
}
namespace UnityEditor.Build
{
    public sealed class BuildFailedException : Exception
    { public BuildFailedException(string message) : base(message) { } }
}
namespace QuietCamp.Application
{
    [Serializable] public sealed class TutorialSaveData { }
}
namespace QuietCamp.Infrastructure
{
    public static class LevelLoader
    {
        public static LevelData Load(string id) => JsonConvert.DeserializeObject<LevelData>(
            File.ReadAllText("QuietCamp/Assets/QuietCamp/Resources/QuietCamp/Levels/" + id + ".json"));
    }
}
