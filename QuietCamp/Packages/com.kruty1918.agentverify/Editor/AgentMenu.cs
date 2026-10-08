using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Kruty1918.AgentVerify.EditorTools
{
    /// <summary>Interactive helpers so a human can use the same tools an agent uses.</summary>
    public static class AgentMenu
    {
        [MenuItem("Tools/Agent Verify/Describe Open Scene → Console")]
        public static void DescribeOpenScene()
        {
            Debug.Log(AgentProbe.DescribeJson());
        }

        [MenuItem("Tools/Agent Verify/Screenshot Active Scene Camera")]
        public static void ScreenshotCamera()
        {
            var path = Path.Combine("Temp", "agentverify.scene.png");
            Debug.Log($"[AgentVerify] wrote {AgentScreenshot.Capture(path)}");
        }

        [MenuItem("Tools/Agent Verify/Run Command File (EditMode)")]
        public static void RunCommands()
        {
            var commandsPath = AgentAutoRun.CommandsPath();
            if (!File.Exists(commandsPath))
            {
                Debug.LogError($"[AgentVerify] no commands file at {commandsPath}");
                return;
            }
            var commands = AgentCommands.FromJson(File.ReadAllText(commandsPath));
            var results = AgentCommands.RunAll(commands);
            var path = AgentCommands.WriteResults(AgentAutoRun.ResultsPath(), results);
            Debug.Log($"[AgentVerify] {results.Count} commands → {path}");
        }

        [MenuItem("Tools/Agent Verify/Write Sample Commands File")]
        public static void WriteSampleCommands()
        {
            var path = AgentAutoRun.CommandsPath();
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path,
                "{\n" +
                "  \"commands\": [\n" +
                "    {\"cmd\":\"summary\"},\n" +
                "    {\"cmd\":\"screenshot\",\"output\":\"Temp/shots/menu.png\"},\n" +
                "    {\"cmd\":\"describe\",\"output\":\"Temp/describe.json\"},\n" +
                "    {\"cmd\":\"expect\",\"check\":\"no-errors\"}\n" +
                "  ]\n" +
                "}\n");
            Debug.Log($"[AgentVerify] sample commands → {path}");
        }
    }
}
