using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Kruty1918.AgentVerify
{
    /// <summary>
    /// Declarative verification script: a JSON list of commands an agent
    /// writes, Unity executes, and a machine-readable results file comes
    /// back. This is the primary headless interface — an agent edits the
    /// commands file, runs Unity once, then reads results + screenshots.
    ///
    /// Commands:
    ///   loadScene  {target:"SceneName"|path}   additive=false single load
    ///   wait       {seconds:1.0}               unscaled realtime wait
    ///   screenshot {output:"Temp/shots/a.png", camera?, width?, height?}
    ///   describe   {output?:path}              scene JSON dump (stdout if no output)
    ///   summary    {}                          one-screen text summary (into detail)
    ///   click      {target:"PlayButton"}       UI click by name/path
    ///   tap        {x:0.5,y:0.5}               normalized screen tap
    ///   drag       {x:0.2,y:0.5,x2:0.8,y2:0.5} normalized screen drag
    ///   invoke     {target:"Obj",method:"Foo"} SendMessage escape hatch
    ///   expect     {check:"exists|missing|active|inactive|visible|
    ///               interactable|text-contains|no-errors", target?, contains?}
    ///   log        {output?:path}              captured console entries (JSON)
    ///   quit       {}                          stop further commands (results still written)
    /// </summary>
    [Serializable]
    public class AgentCommand
    {
        public string cmd;
        public string target;    // object query / scene name / path
        public string output;    // file output path (screenshot/describe/log)
        public string method;    // invoke
        public string check;     // expect
        public string contains;  // expect text-contains
        public string camera;    // screenshot
        public float x, y, x2, y2; // tap/drag (normalized 0..1)
        public float seconds;    // wait
        public int width, height;// screenshot
    }

    [Serializable]
    public class AgentCommandResult
    {
        public string cmd;
        public bool ok;
        public string detail;
        public string output;   // payload for describe/log/summary
    }

    [Serializable] class CommandList { public AgentCommand[] commands; }
    [Serializable] class ResultList { public AgentCommandResult[] results; public bool allOk; }
    [Serializable] class LogDump { public AgentLog.Entry[] entries; }

    public static class AgentCommands
    {
        public static List<AgentCommand> FromJson(string json)
        {
            var list = JsonUtility.FromJson<CommandList>(json);
            return list != null && list.commands != null
                ? new List<AgentCommand>(list.commands)
                : new List<AgentCommand>();
        }

        public static string ResultsToJson(IReadOnlyList<AgentCommandResult> results)
        {
            var rl = new ResultList { results = ToArray(results), allOk = true };
            foreach (var r in rl.results) if (!r.ok) { rl.allOk = false; break; }
            return JsonUtility.ToJson(rl, true);
        }

        /// <summary>Synchronous run for EditMode — 'wait' uses real sleeps.</summary>
        public static List<AgentCommandResult> RunAll(IReadOnlyList<AgentCommand> commands)
        {
            var results = new List<AgentCommandResult>();
            foreach (var c in commands)
            {
                results.Add(Execute(c));
                if (c.cmd == "quit") break;
                if (c.cmd == "wait" && c.seconds > 0f)
                    System.Threading.Thread.Sleep(Mathf.CeilToInt(c.seconds * 1000f));
            }
            return results;
        }

        /// <summary>Coroutine run for PlayMode — 'wait' yields real seconds so anims/time advance.</summary>
        public static IEnumerator RunAllAsync(IReadOnlyList<AgentCommand> commands,
            List<AgentCommandResult> results)
        {
            foreach (var c in commands)
            {
                results.Add(Execute(c));
                if (c.cmd == "quit") yield break;
                if (c.cmd == "wait" && c.seconds > 0f)
                    yield return new WaitForSecondsRealtime(c.seconds);
                else
                    yield return null; // let a frame pass between commands — UI/layout settles
            }
        }

        /// <summary>Write results JSON file; returns path.</summary>
        public static string WriteResults(string path, IReadOnlyList<AgentCommandResult> results)
        {
            var dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            File.WriteAllText(path, ResultsToJson(results));
            return path;
        }

        static AgentCommandResult Execute(AgentCommand c)
        {
            var r = new AgentCommandResult { cmd = c.cmd ?? "?" };
            try
            {
                switch (c.cmd)
                {
                    case "loadScene": r.ok = LoadScene(c.target, out r.detail); break;
                    case "wait": r.ok = true; r.detail = $"{c.seconds:0.##}s"; break;
                    case "screenshot":
                        r.output = AgentScreenshot.Capture(c.output, c.camera, c.width, c.height);
                        r.ok = true; r.detail = "png"; break;
                    case "describe":
                        var dump = AgentProbe.DescribeJson();
                        if (!string.IsNullOrEmpty(c.output)) { WriteText(c.output, dump); r.output = c.output; }
                        else r.output = dump;
                        r.ok = true; r.detail = "json"; break;
                    case "summary": r.output = AgentProbe.Summary(); r.ok = true; break;
                    case "click":
                        r.ok = AgentInput.Click(c.target);
                        r.detail = r.ok ? "clicked" : "not found/blocked"; break;
                    case "tap":
                        r.ok = AgentInput.Tap(new Vector2(c.x * Screen.width, c.y * Screen.height));
                        r.detail = r.ok ? "tapped" : "no hit"; break;
                    case "drag":
                        r.ok = AgentInput.Drag(
                            new Vector2(c.x * Screen.width, c.y * Screen.height),
                            new Vector2(c.x2 * Screen.width, c.y2 * Screen.height));
                        r.detail = r.ok ? "dragged" : "no hit"; break;
                    case "invoke":
                        r.ok = AgentInput.Invoke(c.target, c.method);
                        r.detail = r.ok ? "sent" : "target missing"; break;
                    case "expect":
                        var check = AgentCheck.Expect(c.check, c.target, c.contains);
                        r.ok = check.ok; r.detail = check.detail; break;
                    case "log":
                        var json = JsonUtility.ToJson(
                            new LogDump { entries = AgentLog.Since().ToArray() }, true);
                        if (!string.IsNullOrEmpty(c.output)) { WriteText(c.output, json); r.output = c.output; }
                        else r.output = json;
                        r.ok = true; r.detail = $"{AgentLog.ErrorCount()} errors"; break;
                    case "quit": r.ok = true; r.detail = "stop"; break;
                    default:
                        r.ok = false; r.detail = $"unknown cmd '{c.cmd}'"; break;
                }
            }
            catch (Exception e)
            {
                r.ok = false; r.detail = e.Message;
            }
            return r;
        }

        static bool LoadScene(string target, out string detail)
        {
            if (string.IsNullOrEmpty(target)) { detail = "no scene"; return false; }
            try
            {
                SceneManager.LoadScene(target, LoadSceneMode.Single);
                detail = target; return true;
            }
            catch (Exception e) { detail = e.Message; return false; }
        }

        static string WriteText(string path, string content)
        {
            var dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            File.WriteAllText(path, content);
            return path;
        }

        static AgentCommandResult[] ToArray(IReadOnlyList<AgentCommandResult> l)
        {
            var a = new AgentCommandResult[l.Count];
            for (var i = 0; i < l.Count; i++) a[i] = l[i];
            return a;
        }
    }
}
