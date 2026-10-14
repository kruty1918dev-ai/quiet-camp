#if QUIETCAMP_PWB
using System;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using QuietCamp.Infrastructure;
using UnityEngine.SceneManagement;
using PluginMaster;
using QuietCamp.Editor.LevelAuthoring;
using UnityEditor;
using UnityEngine;

// Deliberately in Assembly-CSharp-Editor: the vendor has no asmdef.
// This bridge references our named Editor assembly, never the reverse.
[InitializeOnLoad]
public sealed class PwbLevelAuthoringAdapter : IPlacementToolAdapter, IControlledPlacementAdapter
{
    static PwbLevelAuthoringAdapter() => PlacementToolIntegration.Register(new PwbLevelAuthoringAdapter());
    public void Activate(PlacementTool tool, GameObject prefab, Transform parent, GameObject surface,
        float radius, float density, float spacing, float variation)
    {
        Stop();
        var palette = PaletteManager.allPalettes.FirstOrDefault(p => p.name == "QuietCamp — декор");
        if (palette == null)
        {
            palette = new PaletteData("QuietCamp — декор", DateTime.UtcNow.Ticks);
            foreach (var entry in LevelAuthoringPalette.Entries)
            {
                var brush = MultibrushSettings.Create(entry.prefab, palette); brush.name = entry.id; palette.AddBrush(brush);
            }
            PaletteManager.AddPalette(palette, true);
        }
        PaletteManager.selectedPalette = palette;
        var marker = prefab.GetComponent<LevelAuthoringAsset>();
        int index = Array.FindIndex(palette.brushes, b => b.name == marker.stableId);
        if (index < 0) throw new InvalidOperationException("Model is missing from the QuietCamp PWB palette.");
        bool brushPopup=PWBCore.staticData.openBrushPropertiesWhenABrushIsSelected;
        bool toolPopup=PWBCore.staticData.openToolPropertiesWhenAToolIsSelected;
        PWBCore.staticData.openBrushPropertiesWhenABrushIsSelected=false;
        PWBCore.staticData.openToolPropertiesWhenAToolIsSelected=false;
        try
        {
        // SelectBrush depends on the optional native Palette window and its filters.
        // The public index API avoids depending on the native window's search filters.
        PaletteManager.ClearSelection(false); PaletteManager.selectedBrushIdx=index; PaletteManager.AddToSelection(index);
        Setup(BrushManager.settings, parent, variation); Setup(PinManager.settings, parent, variation); Setup(LineManager.settings, parent, 0);
        BrushManager.settings.radius = radius; BrushManager.settings.density = Mathf.RoundToInt(density);
        BrushManager.settings.spacingType = BrushToolBase.SpacingType.CUSTOM; BrushManager.settings.minSpacing = spacing;
        BrushManager.settings.paintOnSelectedOnly = PinManager.settings.paintOnSelectedOnly = LineManager.settings.paintOnSelectedOnly = true;
        BrushManager.settings.paintOnPalettePrefabs = PinManager.settings.paintOnPalettePrefabs = LineManager.settings.paintOnPalettePrefabs = false;
        BrushManager.settings.paintOnMeshesWithoutCollider = PinManager.settings.paintOnMeshesWithoutCollider = LineManager.settings.paintOnMeshesWithoutCollider = false;
        EraserManager.settings.radius = radius;
        EraserManager.settings.command = ISelectionBrushTool.Command.SELECT_PALETTE_PREFABS;
        EraserManager.settings.modifyAllButSelected = false;
        Selection.activeGameObject = surface;
        ToolController.current = tool == PlacementTool.Paint ? ToolController.Tool.BRUSH : tool == PlacementTool.Place ? ToolController.Tool.PIN : tool == PlacementTool.Line ? ToolController.Tool.LINE : ToolController.Tool.ERASER;
        // PWB opens its native palette when activating a tool; OnEnable clears
        // brush selection. Reapply our choice after that lifecycle completes.
        PaletteManager.selectedPalette=PaletteManager.allPalettes.First(p=>p.name=="QuietCamp — декор");
        index=Array.FindIndex(PaletteManager.selectedPalette.brushes,b=>b.name==marker.stableId);
        PaletteManager.ClearSelection(false); PaletteManager.selectedBrushIdx=index; PaletteManager.AddToSelection(index);
        SceneView.RepaintAll();
        }
        finally
        {
            PWBCore.staticData.openBrushPropertiesWhenABrushIsSelected=brushPopup;
            PWBCore.staticData.openToolPropertiesWhenAToolIsSelected=toolPopup;
        }
    }
    static void Setup(IPaintToolSettings settings, Transform parent, float variation)
    {
        settings.overwriteParentingSettings = true; settings.parent = parent;
        settings.autoCreateParent = settings.setSurfaceAsParent = settings.setLastSelectedAsParent = false;
        settings.createSubparentPerPalette = settings.createSubparentPerTool = settings.createSubparentPerBrush = settings.createSubparentPerPrefab = false;
        settings.overwriteBrushProperties = true;
        settings.brushSettings.addRandomRotation = variation > 0;
        settings.brushSettings.randomEulerOffset = new RandomUtils.Range3(Vector3.zero, new Vector3(0, 360, 0));
        settings.brushSettings.randomScaleMultiplier = variation > 0;
        settings.brushSettings.randomScaleMultiplierRange = new RandomUtils.Range3(Vector3.one * (1-variation), Vector3.one * (1+variation));
    }
    public string Backend => "Prefab World Builder 4.12 — native Paint";
    public GameObject[] PlaceBatch(GameObject prefab, Transform parent, GameObject surface, CampArtDefinition.Instance[] poses)
    {
        // Vendor 4.12 has no public scripted stroke API. Keep this version-checked
        // reflection at the vendor boundary, without copying or changing vendor code.
        var flags=BindingFlags.NonPublic|BindingFlags.Static;
        var strokeField=typeof(PWBIO).GetField("_paintStroke",flags);
        var itemType=typeof(PWBIO).GetNestedType("PaintStrokeItem",BindingFlags.NonPublic);
        var paint=typeof(PWBIO).GetMethod("Paint",flags);
        if(strokeField==null||itemType==null||paint==null||paint.GetParameters().Length!=5)
            throw new InvalidOperationException("PWB scripted placement contract changed; native 4.12 Paint API expected.");
        var previous=strokeField.GetValue(null);
        if(((IList)previous).Count!=0)throw new InvalidOperationException("Finish the current PWB stroke before composing an example.");
        var stroke=(IList)Activator.CreateInstance(strokeField.FieldType);
        string guid=AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(prefab));
        for(int k=0;k<poses.Length;k++)
        {
            var p=poses[k];var rotation=p.rotationEuler==null?Quaternion.Euler(0,p.yaw,0):Quaternion.Euler(CampArtDefinition.Vector(p.rotationEuler));
            stroke.Add(Activator.CreateInstance(itemType,new object[]{prefab,guid,parent.TransformPoint(new Vector3(p.x,p.y,p.z)),parent.rotation*rotation,
                p.scaleXYZ==null?Vector3.one*p.scale:CampArtDefinition.Vector(p.scaleXYZ),prefab.layer,parent,surface.transform,false,false,k}));
        }
        var settings=new PinSettings();Setup(settings,parent,0);
        var active=SceneManager.GetActiveScene();bool wasPainting=PWBIO.painting;
        try
        {
            SceneManager.SetActiveScene(parent.gameObject.scene);strokeField.SetValue(null,stroke);
            var result=(Dictionary<string,List<(GameObject,int)>>)paint.Invoke(null,new object[]{settings,"QuietCamp — композиція PWB",false,false,""});
            return result.Values.SelectMany(v=>v).OrderBy(v=>v.Item2).Select(v=>v.Item1).ToArray();
        }
        finally { strokeField.SetValue(null,previous);PWBIO.painting=wasPainting;if(active.IsValid()&&active.isLoaded)SceneManager.SetActiveScene(active); }
    }
    public void Stop() => ToolController.current = ToolController.Tool.NONE;
    public void OpenAdvanced()
    {
        EditorApplication.ExecuteMenuItem("Tools/Plugin Master/Prefab World Builder/Palette...");
        EditorApplication.ExecuteMenuItem("Tools/Plugin Master/Prefab World Builder/Tool Properties...");
    }
}
#endif
