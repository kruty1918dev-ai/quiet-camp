using System.Collections;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Kruty1918.AgentVerify.Tests
{
    public class AgentVerifyTests
    {
        [Test]
        public void Commands_Parse_FromJson()
        {
            var json = "{\"commands\":[{\"cmd\":\"screenshot\",\"output\":\"t.png\",\"width\":64}," +
                       "{\"cmd\":\"expect\",\"check\":\"exists\",\"target\":\"X\"}]}";
            var cmds = AgentCommands.FromJson(json);
            Assert.AreEqual(2, cmds.Count);
            Assert.AreEqual("screenshot", cmds[0].cmd);
            Assert.AreEqual(64, cmds[0].width);
        }

        [Test]
        public void Probe_Finds_ByName_AndPath()
        {
            var root = new GameObject("Root");
            var child = new GameObject("Child");
            child.transform.SetParent(root.transform);
            try
            {
                Assert.IsTrue(AgentProbe.Exists("Child"));
                Assert.IsTrue(AgentProbe.Exists("Root/Child"));
                Assert.IsFalse(AgentProbe.Exists("Missing"));
                Assert.AreEqual("Root/Child", AgentProbe.PathOf(child.transform));
            }
            finally { Object.DestroyImmediate(root); }
        }

        [Test]
        public void Check_Exists_And_Missing()
        {
            var go = new GameObject("Thing");
            try
            {
                Assert.IsTrue(AgentCheck.Expect("exists", "Thing").ok);
                Assert.IsFalse(AgentCheck.Expect("missing", "Thing").ok);
                Assert.IsTrue(AgentCheck.Expect("active", "Thing").ok);
                go.SetActive(false);
                Assert.IsTrue(AgentCheck.Expect("inactive", "Thing").ok);
            }
            finally { Object.DestroyImmediate(go); }
        }

        [Test]
        public void Check_Unknown_IsFailure_NotThrow()
        {
            var r = AgentCheck.Expect("bogus", "X");
            Assert.IsFalse(r.ok);
            StringAssert.Contains("unknown", r.detail);
        }

        [Test]
        public void Log_Captures_Errors()
        {
            AgentLog.Install();
            AgentLog.Clear();
            var mark = AgentLog.Mark();
            UnityEngine.TestTools.LogAssert.ignoreFailingMessages = true;
            try
            {
                Debug.LogError("agentverify-test-error");
                Assert.GreaterOrEqual(AgentLog.ErrorCount(mark), 1);
                Assert.IsFalse(AgentLog.IsClean(mark));
            }
            finally
            {
                UnityEngine.TestTools.LogAssert.ignoreFailingMessages = false;
                AgentLog.Clear();
            }
        }

        [Test]
        public void Click_Triggers_Button_OnClick()
        {
            var es = new GameObject("EventSystem");
            es.AddComponent<EventSystem>();
            es.AddComponent<StandaloneInputModule>();
            var canvasGo = new GameObject("Canvas");
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvasGo.AddComponent<GraphicRaycaster>();
            var btnGo = new GameObject("Btn");
            btnGo.transform.SetParent(canvasGo.transform, false);
            var img = btnGo.AddComponent<Image>();
            var btn = btnGo.AddComponent<Button>();
            var clicked = 0;
            btn.onClick.AddListener(() => clicked++);
            try
            {
                Assert.IsTrue(AgentInput.Click("Btn"));
                Assert.AreEqual(1, clicked);
            }
            finally
            {
                Object.DestroyImmediate(canvasGo);
                Object.DestroyImmediate(es);
            }
        }

        [Test]
        public void Command_Expect_Produces_Result()
        {
            var go = new GameObject("Probe");
            try
            {
                var results = AgentCommands.RunAll(new List<AgentCommand>
                {
                    new AgentCommand { cmd = "expect", check = "exists", target = "Probe" },
                    new AgentCommand { cmd = "expect", check = "exists", target = "Nope" }
                });
                Assert.AreEqual(2, results.Count);
                Assert.IsTrue(results[0].ok);
                Assert.IsFalse(results[1].ok);
            }
            finally { Object.DestroyImmediate(go); }
        }

        [Test]
        public void Commands_Json_Roundtrip_Results()
        {
            var results = new List<AgentCommandResult>
            {
                new AgentCommandResult { cmd = "a", ok = true },
                new AgentCommandResult { cmd = "b", ok = false, detail = "x" }
            };
            var json = AgentCommands.ResultsToJson(results);
            StringAssert.Contains("\"allOk\":false", json.Replace(" ", ""));
            StringAssert.Contains("\"cmd\":\"b\"", json.Replace(" ", ""));
        }
    }
}
