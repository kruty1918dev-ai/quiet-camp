#if QUIETCAMP_PWB
using System;
using System.Linq;
using PluginMaster;
using QuietCamp.Editor.LevelAuthoring;
using UnityEditor;
using UnityEngine;

// Deliberately in Assembly-CSharp-Editor: the vendor has no asmdef.
// This bridge references our named Editor assembly, never the reverse.
[InitializeOnLoad]
public sealed class PwbLevelAuthoringAdapter : IPlacementToolAdapter
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
    public void Stop() => ToolController.current = ToolController.Tool.NONE;
    public void OpenAdvanced()
    {
        EditorApplication.ExecuteMenuItem("Tools/Plugin Master/Prefab World Builder/Palette...");
        EditorApplication.ExecuteMenuItem("Tools/Plugin Master/Prefab World Builder/Tool Properties...");
    }
}
#endif
