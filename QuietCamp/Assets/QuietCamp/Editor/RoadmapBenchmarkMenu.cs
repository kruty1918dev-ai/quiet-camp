
using QuietCamp.Presentation.UI.Prepared;
using RoadmapGladeGraphic = QuietCamp.Presentation.UI.Prepared.RoadmapGladeGraphic;
using RoadmapGraphic = QuietCamp.Presentation.UI.Prepared.RoadmapGraphic;
using RoadmapLayout = QuietCamp.Presentation.UI.Prepared.RoadmapLayout;
using RoadmapSceneGenerator = QuietCamp.Presentation.UI.Prepared.RoadmapSceneGenerator;
using RoadmapWeatherGraphic = QuietCamp.Presentation.UI.Prepared.RoadmapWeatherGraphic;
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
            UnityEngine.Object.FindAnyObjectByType<QuietCamp.Presentation.UI.Prepared.RoadmapBenchmarkHarness>().autoRun=true;
        }
    }
}
#endif
