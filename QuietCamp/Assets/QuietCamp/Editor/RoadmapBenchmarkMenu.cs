#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;

namespace QuietCamp.Editor
{
    public static class RoadmapBenchmarkMenu
    {
        [MenuItem("QuietCamp/QA/Open Roadmap 360 Benchmark")]
        static void Open()
        {
            if(!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())return;
            EditorSceneManager.OpenScene("Assets/QuietCamp/Tests/Fixtures/Roadmap/RoadmapBenchmark.unity");
            UnityEngine.Object.FindAnyObjectByType<QuietCamp.Presentation.UI.RoadmapBenchmarkHarness>().autoRun=true;
        }
    }
}
#endif
