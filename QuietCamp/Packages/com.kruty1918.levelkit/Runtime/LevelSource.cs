using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Kruty1918.LevelKit
{
    /// <summary>
    /// A read-only source of raw level JSON texts, keyed by a stable name
    /// (usually the file/level id). Parsing and validation happen in
    /// LevelRepository so any storage backend can be plugged in.
    /// </summary>
    public interface ILevelSource
    {
        IEnumerable<KeyValuePair<string, string>> ReadAll();
    }

    /// <summary>Levels shipped in a Resources folder (e.g. "MyGame/Levels").</summary>
    public sealed class ResourcesLevelSource : ILevelSource
    {
        readonly string _folder;
        public ResourcesLevelSource(string folder) { _folder = folder; }

        public IEnumerable<KeyValuePair<string, string>> ReadAll()
        {
            foreach (var asset in Resources.LoadAll<TextAsset>(_folder))
            {
                if (asset == null) continue;
                yield return new KeyValuePair<string, string>(asset.name, asset.text);
            }
        }
    }

    /// <summary>
    /// Levels from a directory on disk — StreamingAssets, persistentDataPath,
    /// mod folders, downloaded content. Recursive .json scan.
    /// </summary>
    public sealed class DirectoryLevelSource : ILevelSource
    {
        readonly string _path;
        readonly string _searchPattern;

        public DirectoryLevelSource(string path, string searchPattern = "*.json")
        {
            _path = path;
            _searchPattern = searchPattern;
        }

        public IEnumerable<KeyValuePair<string, string>> ReadAll()
        {
            if (!Directory.Exists(_path)) yield break;
            foreach (var file in Directory.EnumerateFiles(_path, _searchPattern,
                SearchOption.AllDirectories))
            {
                string text;
                try { text = File.ReadAllText(file); }
                catch { continue; }
                yield return new KeyValuePair<string, string>(
                    Path.GetFileNameWithoutExtension(file), text);
            }
        }
    }

    /// <summary>Levels supplied directly as strings — tests, tools, remote fetches.</summary>
    public sealed class InlineLevelSource : ILevelSource
    {
        readonly List<KeyValuePair<string, string>> _items
            = new List<KeyValuePair<string, string>>();

        public InlineLevelSource Add(string name, string json)
        {
            _items.Add(new KeyValuePair<string, string>(name, json));
            return this;
        }

        public IEnumerable<KeyValuePair<string, string>> ReadAll() => _items;
    }
}
