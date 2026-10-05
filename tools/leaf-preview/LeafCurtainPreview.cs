// Copy into Assets/Editor of a disposable project copy, then run Unity with
// -batchmode -nographics -quit -executeMethod LeafCurtainPreview.Export.
using System;
using System.IO;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using QuietCamp.Infrastructure;
using QuietCamp.Presentation;
[InitializeOnLoad]
public static class LeafCurtainPreview
{
    static LeafCurtainPreview()
    {
        if (Environment.GetEnvironmentVariable("LEAF_PREVIEW_AUTO") == "1")
            EditorApplication.update += ExportWhenReady;
    }
    static void ExportWhenReady()
    {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating) return;
        EditorApplication.update -= ExportWhenReady;
        string output = Environment.GetEnvironmentVariable("LEAF_PREVIEW_DIR") ?? "/tmp/leaf-preview";
        if (!File.Exists(Path.Combine(output, "0000.json"))) Export();
    }
    [Serializable] sealed class Frame
    {
        public float time;
        public bool revealed;
        public Vector3[] vertices;
        public Color32[] colors;
        public int[] triangles;
    }
    public static void Export()
    {
        string output = Environment.GetEnvironmentVariable("LEAF_PREVIEW_DIR") ?? "/tmp/leaf-preview";
        Directory.CreateDirectory(output);
        var cfg = TransitionConfig.Load();
        var go = new GameObject("Preview", typeof(RectTransform), typeof(LeafCurtainGraphic));
        go.GetComponent<RectTransform>().sizeDelta = new Vector2(1080, 1920);
        var graphic = go.GetComponent<LeafCurtainGraphic>();
        bool render = Environment.GetEnvironmentVariable("LEAF_PREVIEW_RENDER") == "1";
        var target = render ? new RenderTexture(648, 1152, 0, RenderTextureFormat.ARGB32) : null;
        var pixels = render ? new Texture2D(648, 1152, TextureFormat.RGB24, false) : null;
        var material = render ? new Material(Shader.Find("UI/Default")) : null;
        var old = render ? new Texture2D(2, 2) : null;
        var next = render ? new Texture2D(2, 2) : null;
        if (render)
        {
            old.LoadImage(File.ReadAllBytes("Screenshots/01_main_menu.png"));
            next.LoadImage(File.ReadAllBytes("Screenshots/04_camp_day.png"));
            material.mainTexture = Texture2D.whiteTexture;
        }
        float revealAt = cfg.CoverDuration + cfg.CoveredHold;
        int count = Mathf.CeilToInt((revealAt + cfg.RevealDuration) * 50f) + 1;
        for (int i = 0; i < count; i++)
        {
            float time = i / 50f;
            float travel = time < cfg.CoverDuration ? time / cfg.CoverDuration
                : time < revealAt ? 1f : 1f + Mathf.Clamp01((time - revealAt) / cfg.RevealDuration);
            graphic.SetFrame(travel, cfg.LeafTint("morning"), time);
            using (var vh = new VertexHelper())
            {
                typeof(LeafCurtainGraphic).GetMethod("OnPopulateMesh", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly)
                    .Invoke(graphic, new object[] { vh });
                var mesh = new Mesh();
                vh.FillMesh(mesh);
                File.WriteAllText(Path.Combine(output, $"{i:D4}.json"), JsonUtility.ToJson(new Frame {
                    time = time, revealed = time >= cfg.CoverDuration + cfg.CoveredHold * .5f,
                    vertices = mesh.vertices, colors = mesh.colors32, triangles = mesh.triangles }));
                if (render)
                {
                    // CanvasRenderer converts vertex tint to linear by default.
                    // DrawMeshNow bypasses that step, so do it here as well.
                    if (QualitySettings.activeColorSpace == ColorSpace.Linear)
                    {
                        var linearColors = mesh.colors;
                        for (int c = 0; c < linearColors.Length; c++)
                            linearColors[c] = linearColors[c].linear;
                        mesh.colors = linearColors;
                    }
                    var previous = RenderTexture.active;
                    Graphics.Blit(time >= revealAt ? next : old, target);
                    RenderTexture.active = target;
                    GL.PushMatrix();
                    GL.LoadProjectionMatrix(Matrix4x4.Ortho(-540, 540, -960, 960, -1, 1));
                    GL.modelview = Matrix4x4.identity;
                    material.SetPass(0);
                    Graphics.DrawMeshNow(mesh, Matrix4x4.identity);
                    GL.PopMatrix();
                    pixels.ReadPixels(new Rect(0, 0, 648, 1152), 0, 0);
                    pixels.Apply();
                    File.WriteAllBytes(Path.Combine(output, $"{i:D4}.png"), pixels.EncodeToPNG());
                    RenderTexture.active = previous;
                }
                UnityEngine.Object.DestroyImmediate(mesh);
            }
        }
        UnityEngine.Object.DestroyImmediate(go);
        if (render)
        {
            target.Release();
            foreach (var asset in new UnityEngine.Object[] { target, pixels, material, old, next })
                UnityEngine.Object.DestroyImmediate(asset);
        }
    }
}
