using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Kruty1918.AgentVerify
{
    /// <summary>
    /// Auto-runner for headless verification: if a commands file exists when
    /// play mode starts, it is executed automatically and a results file is
    /// written — the agent never has to touch the game itself.
    ///
    /// Command file resolution order:
    ///   1) env var AGENTVERIFY_COMMANDS (absolute path)
    ///   2) &lt;project&gt;/agentverify.commands.json (project root — Temp is wiped on startup)
    /// Results default to the same folder: agentverify.results.json, or the
    /// path in env var AGENTVERIFY_RESULTS.
    /// </summary>
    public static class AgentAutoRun
    {
        public const string CommandsFileName = "agentverify.commands.json";
        public const string ResultsFileName = "agentverify.results.json";

        public static string CommandsPath()
        {
            var env = System.Environment.GetEnvironmentVariable("AGENTVERIFY_COMMANDS");
            if (!string.IsNullOrEmpty(env)) return env;
            // Project root, NOT Temp/ — Unity wipes Temp on every startup.
            return Path.Combine(ProjectRoot(), CommandsFileName);
        }

        public static string ResultsPath()
        {
            var env = System.Environment.GetEnvironmentVariable("AGENTVERIFY_RESULTS");
            if (!string.IsNullOrEmpty(env)) return env;
            return Path.Combine(ProjectRoot(), ResultsFileName);
        }

        /// <summary>Project root = parent of Assets (Application.dataPath).</summary>
        public static string ProjectRoot()
            => Directory.GetParent(Application.dataPath).FullName;

        /// <summary>True when a commands file is staged for this run.</summary>
        public static bool HasCommands => File.Exists(CommandsPath());

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            if (!HasCommands) return;
            var go = new GameObject("[AgentVerify]");
            Object.DontDestroyOnLoad(go);
            go.AddComponent<Runner>();
        }

        sealed class Runner : MonoBehaviour
        {
            IEnumerator Start()
            {
                AgentLog.Install();
                List<AgentCommand> commands;
                try { commands = AgentCommands.FromJson(File.ReadAllText(CommandsPath())); }
                catch (System.Exception e)
                {
                    AgentCommands.WriteResults(ResultsPath(), new List<AgentCommandResult>
                    {
                        new AgentCommandResult { cmd = "parse", ok = false, detail = e.Message }
                    });
                    yield break;
                }
                var results = new List<AgentCommandResult>();
                yield return AgentCommands.RunAllAsync(commands, results);
                // Let async work (e.g. ScreenCapture writes) land — plain frame
                // yields, since WaitForEndOfFrame never fires without a device.
                yield return new WaitForSecondsRealtime(0.5f);
                AgentCommands.WriteResults(ResultsPath(), results);
            }

            static bool AllOk(List<AgentCommandResult> results)
            {
                foreach (var r in results) if (!r.ok) return false;
                return true;
            }
        }
    }
}
