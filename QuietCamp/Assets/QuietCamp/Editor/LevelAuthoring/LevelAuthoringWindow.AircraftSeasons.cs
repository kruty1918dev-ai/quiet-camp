using System.IO;
using UnityEditor;

namespace QuietCamp.Editor.LevelAuthoring
{
    public sealed partial class LevelAuthoringWindow
    {
        const string AircraftSeasonFolder = "Assets/QuietCamp/Authoring/WorldStudies/AircraftSeasons";
        [MenuItem("QuietCamp/Приклади/Табір між уламками/Весна")]
        static void OpenSpringAircraft() => OpenAircraftSeason("spring");
        [MenuItem("QuietCamp/Приклади/Табір між уламками/Літо")]
        static void OpenSummerAircraft() => OpenAircraftSeason("summer");
        [MenuItem("QuietCamp/Приклади/Табір між уламками/Осінь")]
        static void OpenAutumnAircraft() => OpenAircraftSeason("autumn");
        [MenuItem("QuietCamp/Приклади/Табір між уламками/Зима")]
        static void OpenWinterAircraft() => OpenAircraftSeason("winter");

        static void OpenAircraftSeason(string season)
        {
            string path = Path.Combine(AircraftSeasonFolder, season + ".draft.json");
            var window = OpenWindow();
            window.Try(() =>
            {
                if (!window.ConfirmReplace()) return;
                // Use a new draft so saving edits does not overwrite the supplied example.
                window.SetDocument(LevelAuthoringDocument.Open(path));
                window._path = null; window._dirty = true;
                window._message = "Приклад відкрито. Змініть сцену та збережіть власну чернетку.";
            });
        }
    }
}
