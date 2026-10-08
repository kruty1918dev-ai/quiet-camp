using System.IO;
using UnityEditor;
using UnityEditor.ProjectWindowCallback;
using UnityEngine;

namespace Kruty1918.LevelKit.EditorTools
{
    /// <summary>Project-window scaffolders for level + profile JSON files.</summary>
    public static class LevelKitMenu
    {
        const string ProfileTemplate =
@"{
    ""gameId"": ""my-game"",
    ""idKey"": ""id"",
    ""orderKey"": ""order"",
    ""schemaVersionKey"": ""schemaVersion"",
    ""widthKey"": ""width"",
    ""heightKey"": ""height"",
    ""layersKey"": """",
    ""entitiesKey"": """",
    ""propsKey"": """",
    ""layers"": [
        { ""name"": ""blocked"", ""key"": ""blocked"", ""multiple"": true, ""color"": ""#8899aa"" },
        { ""name"": ""entry"", ""key"": ""entry"", ""multiple"": false, ""color"": ""#e3c04a"" }
    ],
    ""entityKinds"": [
        {
            ""kind"": ""spawn"",
            ""arrayKey"": ""spawns"",
            ""idField"": ""id"",
            ""writeFields"": [""id"", ""x"", ""z""],
            ""color"": ""#e3a04a""
        }
    ]
}
";

        const string LevelTemplate =
@"{
    ""schemaVersion"": 1,
    ""id"": ""level_001"",
    ""order"": 1,
    ""width"": 6,
    ""height"": 6,
    ""layers"": {
        ""blocked"": [],
        ""entry"": []
    },
    ""entities"": [],
    ""props"": {}
}
";

        [MenuItem("Assets/Create/Level Kit/Level Profile", false, 82)]
        static void CreateProfile()
            => CreateAsset("new_profile.levelprofile.json", ProfileTemplate);

        [MenuItem("Assets/Create/Level Kit/Level (canonical)", false, 83)]
        static void CreateLevel()
            => CreateAsset("new_level.json", LevelTemplate);

        static void CreateAsset(string name, string content)
        {
            var dir = "Assets";
            var selected = Selection.activeObject;
            if (selected != null)
            {
                var path = AssetDatabase.GetAssetPath(selected);
                if (!string.IsNullOrEmpty(path))
                    dir = Directory.Exists(path) ? path : Path.GetDirectoryName(path);
            }
            var target = AssetDatabase.GenerateUniqueAssetPath(Path.Combine(dir, name));
            File.WriteAllText(target, content);
            AssetDatabase.Refresh();
            Selection.activeObject = AssetDatabase.LoadAssetAtPath<TextAsset>(target);
        }
    }
}
