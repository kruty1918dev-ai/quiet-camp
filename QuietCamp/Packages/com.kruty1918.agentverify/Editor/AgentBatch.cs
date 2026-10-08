using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Kruty1918.AgentVerify.EditorTools
{
    /// <summary>
    /// Headless entry points for `Unity.exe -executeMethod`:
    ///
    ///   AgentBatch.RunEditMode   — executes agentverify.commands.json in
    ///                              the open editor state (no play mode), writes
    ///                              agentverify.results.json, exits 0/2.
    ///
    ///   AgentBatch.RunPlayMode   — enters play mode; AgentAutoRun executes the
    ///                              commands against the live scene, writes the
    ///                              results file; the editor exits when it appears.
    ///
    /// Agent loop: write commands file → run one command → read results file.
    /// </summary>
    public static class AgentBatch
    {
        /// <summary>-executeMethod Kruty1918.AgentVerify.EditorTools.AgentBatch.RunEditMode</summary>
        public static void RunEditMode()
        {
            AgentLog.Install();
            var commandsPath = AgentAutoRun.CommandsPath();
            var resultsPath = AgentAutoRun.ResultsPath();
            List<AgentCommand> commands;
            try { commands = AgentCommands.FromJson(File.ReadAllText(commandsPath)); }
            catch (System.Exception e)
            {
                AgentCommands.WriteResults(resultsPath, new List<AgentCommandResult>
                {
                    new AgentCommandResult { cmd = "parse", ok = false, detail = e.Message }
                });
                EditorApplication.Exit(1);
                return;
            }
            var results = AgentCommands.RunAll(commands);
            AgentCommands.WriteResults(resultsPath, results);
            var ok = true;
            foreach (var r in results) if (!r.ok) { ok = false; break; }
            EditorApplication.Exit(ok ? 0 : 2);
        }

        /// <summary>-executeMethod Kruty1918.AgentVerify.EditorTools.AgentBatch.RunPlayMode</summary>
        public static void RunPlayMode()
        {
            if (!File.Exists(AgentAutoRun.CommandsPath()))
            {
                Debug.LogError($"[AgentVerify] no commands file at {AgentAutoRun.CommandsPath()}");
                EditorApplication.Exit(1);
                return;
            }
            var resultsPath = AgentAutoRun.ResultsPath();
            if (File.Exists(resultsPath)) File.Delete(resultsPath);
            _startedAt = EditorApplication.timeSinceStartup;
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
            EditorApplication.update += WatchResults;
            EditorApplication.EnterPlaymode();
        }

        /// <summary>Safety net: a broken scene/script must never hang a batch run forever.</summary>
        const double WatchdogSeconds = 300;
        static double _startedAt;

        static void OnPlayModeChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.ExitingPlayMode)
            {
                EditorApplication.update -= WatchResults;
                EditorApplication.playModeStateChanged -= OnPlayModeChanged;
                // Play ended without results — treat as failure.
                if (!File.Exists(AgentAutoRun.ResultsPath()))
                    EditorApplication.Exit(2);
            }
        }

        static double _lastPoll;
        static void WatchResults()
        {
            if (EditorApplication.timeSinceStartup - _startedAt > WatchdogSeconds)
            {
                EditorApplication.update -= WatchResults;
                EditorApplication.playModeStateChanged -= OnPlayModeChanged;
                Debug.LogError("[AgentVerify] watchdog: no results within 300s — aborting");
                if (EditorApplication.isPlaying) EditorApplication.isPlaying = false;
                EditorApplication.Exit(1);
                return;
            }
            var path = AgentAutoRun.ResultsPath();
            if (!File.Exists(path)) return;
            if (EditorApplication.timeSinceStartup - _lastPoll < 0.25) return;
            _lastPoll = EditorApplication.timeSinceStartup;
            var ok = false;
            try
            {
                var json = File.ReadAllText(path);
                ok = json.Contains("\"allOk\": true") || json.Contains("\"allOk\":true");
            }
            catch { return; }
            EditorApplication.update -= WatchResults;
            EditorApplication.playModeStateChanged -= OnPlayModeChanged;
            if (EditorApplication.isPlaying) EditorApplication.isPlaying = false;
            EditorApplication.Exit(ok ? 0 : 2);
        }
    }
}
