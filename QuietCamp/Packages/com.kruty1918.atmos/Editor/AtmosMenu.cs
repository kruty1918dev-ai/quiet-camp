using UnityEditor;
using UnityEngine;

namespace Kruty1918.Atmos.Editor
{
    /// <summary>Tools → Atmos convenience menu: spawn helper objects in the scene.</summary>
    public static class AtmosMenu
    {
        [MenuItem("Tools/Atmos/Add Sky Controller")]
        public static void AddSkyController()
        {
            var go = new GameObject("SkyController");
            Undo.RegisterCreatedObjectUndo(go, "Add Sky Controller");
            var ctrl = go.AddComponent<SkyController>();
            ctrl.skies.Add(new SkyController.NamedSky { name = "day", spec = SkySpec.Day });
            ctrl.skies.Add(new SkyController.NamedSky { name = "evening", spec = SkySpec.Evening });
            ctrl.skies.Add(new SkyController.NamedSky { name = "night", spec = SkySpec.Night });
            Selection.activeGameObject = go;
        }

        [MenuItem("Tools/Atmos/Add Campfire")]
        public static void AddCampfire()
        {
            var parent = Selection.activeTransform;
            var visual = Campfire.Create(parent);
            Undo.RegisterCreatedObjectUndo(visual.gameObject, "Add Campfire");
            Selection.activeGameObject = visual.gameObject;
        }
    }
}
