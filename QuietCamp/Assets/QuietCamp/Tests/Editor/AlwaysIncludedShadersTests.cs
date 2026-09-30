using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace QuietCamp.Tests.Editor
{
    /// <summary>
    /// Guards against URP shader stripping regressions: every shader the runtime
    /// resolves by name (or receives implicitly via GameObject.CreatePrimitive)
    /// must be present in GraphicsSettings "Always Included Shaders", otherwise
    /// device builds render magenta primitives.
    /// </summary>
    public class AlwaysIncludedShadersTests
    {
        static readonly string[] RequiredShaders =
        {
            "Universal Render Pipeline/Lit",            // CreatePrimitive default material
            "Universal Render Pipeline/Simple Lit",     // BoardRenderer lit props
            "Universal Render Pipeline/Unlit",          // grid, overlays, markers, ghost
            "Universal Render Pipeline/Particles/Unlit" // FireFx
        };

        static string GraphicsSettingsPath =>
            Path.Combine(Directory.GetCurrentDirectory(), "ProjectSettings", "GraphicsSettings.asset");

        [Test]
        public void GraphicsSettings_ContainsEveryRuntimeShader()
        {
            var text = File.ReadAllText(GraphicsSettingsPath);
            var includedGuids = Regex.Matches(text, @"guid: ([0-9a-f]{32})")
                .Select(m => m.Groups[1].Value).ToHashSet();

            foreach (var name in RequiredShaders)
            {
                var shader = Shader.Find(name);
                Assert.IsNotNull(shader, $"Shader '{name}' not found in editor.");
                var guid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(shader));
                Assert.IsTrue(includedGuids.Contains(guid),
                    $"Shader '{name}' (guid {guid}) missing from Always Included Shaders.");
            }
        }

        [Test]
        public void EveryShaderFindLiteral_IsCoveredByAlwaysIncluded()
        {
            var text = File.ReadAllText(GraphicsSettingsPath);
            var includedGuids = Regex.Matches(text, @"guid: ([0-9a-f]{32})")
                .Select(m => m.Groups[1].Value).ToHashSet();

            var scriptsDir = Path.Combine(Directory.GetCurrentDirectory(),
                "Assets", "QuietCamp", "Scripts");
            var literals = Directory.GetFiles(scriptsDir, "*.cs", SearchOption.AllDirectories)
                .SelectMany(f => Regex.Matches(File.ReadAllText(f),
                        @"Shader\.Find\(\s*""([^""]+)""")
                    .Select(m => m.Groups[1].Value))
                .Distinct()
                .ToList();

            Assert.IsNotEmpty(literals, "No Shader.Find literals found — test is stale.");
            var missing = new List<string>();
            foreach (var name in literals)
            {
                var shader = Shader.Find(name);
                if (shader == null) { missing.Add($"{name} (unresolved)"); continue; }
                var guid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(shader));
                if (!includedGuids.Contains(guid)) missing.Add($"{name} (guid {guid})");
            }
            Assert.IsEmpty(missing,
                "Shader.Find shaders missing from Always Included: " + string.Join(", ", missing));
        }

        [Test]
        public void ResourcesUiSprites_AllPresent()
        {
            var needed = new[]
            {
                "QuietCamp/UI/Green/Default/button_rectangle_depth_flat",
                "QuietCamp/UI/Grey/Default/button_rectangle_depth_flat",
                "QuietCamp/UI/Red/Default/button_rectangle_depth_flat",
                "QuietCamp/UI/Extra/Default/input_rectangle",
                "QuietCamp/UI/Green/Default/icon_checkmark",
                "QuietCamp/UI/Red/Default/icon_cross",
                "QuietCamp/UI/Extra/Default/icon_repeat_dark",
                "QuietCamp/UI/Extra/Default/icon_arrow_down_dark",
                "QuietCamp/UI/Grey/Default/slide_horizontal_grey",
                "QuietCamp/UI/Green/Default/slide_horizontal_color",
                "QuietCamp/UI/Grey/Default/slide_hangle",
                "QuietCamp/UI/Grey/Default/check_square_grey"
            };
            foreach (var path in needed)
            {
                var full = Path.Combine(Directory.GetCurrentDirectory(),
                    "Assets", "QuietCamp", "Resources", path + ".png");
                Assert.IsTrue(File.Exists(full), $"Missing mirrored sprite: {full}");
            }
        }
    }
}
