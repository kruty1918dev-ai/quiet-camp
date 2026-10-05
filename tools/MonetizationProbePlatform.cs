using System;
using System.IO;
using Newtonsoft.Json;
using QuietCamp.Domain;

namespace UnityEngine
{
    public static class Application { public static string persistentDataPath; }
    public static class Debug
    {
        public static void Log(string value) { }
        public static void LogError(string value) { }
        public static void LogWarning(string value) { }
    }
    public sealed class TextAsset { public string text; }
    public static class Resources
    {
        public static string Root = Path.Combine(Environment.CurrentDirectory, "QuietCamp/Assets/QuietCamp/Resources");
        public static T Load<T>(string path) where T : class
        {
            var filename = Path.Combine(Root, path + ".json");
            return typeof(T) == typeof(TextAsset) && File.Exists(filename) ? new TextAsset { text = File.ReadAllText(filename) } as T : null;
        }
    }
}
namespace QuietCamp.Application
{
    public enum ComfortAction { Place, InvalidDrop, Move, CancelDrag, Rotate, Undo, Redo, Remove, Check, Hint, TutorialStep }
    [Serializable] public sealed class TutorialSaveData { public int fixtureMarker; }
}
namespace QuietCamp.Infrastructure
{
    public static class LevelLoader
    {
        public static LevelData Load(string id)
        {
            var path = id.StartsWith("gen:", StringComparison.Ordinal) ? "QuietCamp/GeneratedLevels/" + id.Replace(':', '_') : "QuietCamp/Levels/" + id;
            return JsonConvert.DeserializeObject<LevelData>(UnityEngine.Resources.Load<UnityEngine.TextAsset>(path).text);
        }
    }
}
