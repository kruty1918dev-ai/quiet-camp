using System;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

namespace QuietCamp.Editor
{
    /// <summary>Reproducible, language-independent SDF assets. No runtime font substitutions for letters.</summary>
    public static class CampTypographySetup
    {
        const string Root = "Assets/QuietCamp/Resources/Fonts/";

        [MenuItem("Tools/Quiet Camp/Prepare UI Fonts")]
        public static void Prepare()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Stop preview before preparing fonts.");
            var regular=Create("NotoSans-Regular.ttf","NotoSans SDF");
            var bold=Create("NotoSans-Bold.ttf","NotoSans Bold SDF");
            CompactFeatures(regular);CompactFeatures(bold);
            var weights=regular.fontWeightTable;
            weights[7].regularTypeface=bold;
            // Symbols may use the legacy font; all supported-language letters live in Noto itself.
            var symbols=AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(Root+"DejaVuSans SDF.asset");
            regular.fallbackFontAssetTable=new System.Collections.Generic.List<TMP_FontAsset>{symbols};
            bold.fallbackFontAssetTable=new System.Collections.Generic.List<TMP_FontAsset>{symbols};
            EditorUtility.SetDirty(regular);EditorUtility.SetDirty(bold);
            var settings=AssetDatabase.LoadAssetAtPath<TMP_Settings>("Assets/TextMesh Pro/Resources/TMP Settings.asset");
            if(settings==null)throw new InvalidOperationException("Install TMP essentials before preparing fonts.");
            var serialized=new SerializedObject(settings);
            serialized.FindProperty("m_defaultFontAsset").objectReferenceValue=regular;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.SaveAssets();
            Debug.Log("[QuietCamp] Noto Sans Regular/Bold prepared for every UI language.");
        }

        static TMP_FontAsset Create(string source,string name)
        {
            var path=Root+name+".asset";
            var existing=AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path);
            if(existing!=null)
            {
                if(existing.fontFeatureTable.glyphPairAdjustmentRecords.Count==0)
                {
                    existing.ClearFontAssetData();
                    Prewarm(existing,AssetDatabase.LoadAssetAtPath<Font>(Root+source));
                    EditorUtility.SetDirty(existing);
                }
                return existing;
            }
            var font=AssetDatabase.LoadAssetAtPath<Font>(Root+source);
            if(font==null)throw new InvalidOperationException("Missing font: "+source);
            var asset=TMP_FontAsset.CreateFontAsset(font,64,9,GlyphRenderMode.SDFAA,2048,2048,AtlasPopulationMode.Dynamic,true);
            asset.name=name;
            asset.normalStyle=0;asset.boldStyle=0;asset.boldSpacing=0;
            Prewarm(asset,font);
            asset.isMultiAtlasTexturesEnabled=true;
            var serialized=new SerializedObject(asset);
            serialized.FindProperty("m_ClearDynamicDataOnBuild").boolValue=false;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            var material=asset.material;
            material.name=name+" Material";
            material.SetFloat(ShaderUtilities.ID_GradientScale,asset.atlasPadding+1);
            material.SetFloat(ShaderUtilities.ID_TextureWidth,asset.atlasWidth);
            material.SetFloat(ShaderUtilities.ID_TextureHeight,asset.atlasHeight);
            material.SetFloat(ShaderUtilities.ID_WeightNormal,0);
            material.SetFloat(ShaderUtilities.ID_WeightBold,0);
            AssetDatabase.CreateAsset(asset,path);
            AssetDatabase.AddObjectToAsset(material,asset);
            foreach(var atlas in asset.atlasTextures)
            {
                atlas.name=name+" Atlas";atlas.filterMode=FilterMode.Bilinear;
                AssetDatabase.AddObjectToAsset(atlas,asset);
            }
            EditorUtility.SetDirty(asset);
            return asset;
        }

        static void Prewarm(TMP_FontAsset asset,Font font)
        {
            // Bake both scripts and kerning with identical sampling, before the first frame.
            FontEngine.LoadFontFace(font,64);
            var candidates=Enumerable.Range(32,352).Concat(Enumerable.Range(0x400,96))
                .Concat(new[]{0x490,0x491}).Concat(Enumerable.Range(0x2010,24)).Concat(new[]{0x20ac,0x2116,0x2122});
            var supported=candidates.Where(c=>FontEngine.TryGetGlyphWithUnicodeValue((uint)c,GlyphLoadFlags.LOAD_DEFAULT,out _)).Select(c=>(uint)c).ToArray();
            if(!asset.TryAddCharacters(supported,out var missing,true)||(missing?.Length??0)!=0)
                throw new InvalidOperationException("Font atlas could not fit the UI alphabet: "+font.name);
        }

        static void CompactFeatures(TMP_FontAsset asset)
        {
            // TextCore returns tables for the entire source face, including thousands of unbaked glyphs.
            // Keep every usable adjustment, without serializing unrelated scripts and accents.
            var glyphs=new System.Collections.Generic.HashSet<uint>(asset.glyphTable.Select(g=>g.index));
            var features=asset.fontFeatureTable;
            features.glyphPairAdjustmentRecords.RemoveAll(r=>!glyphs.Contains(r.firstAdjustmentRecord.glyphIndex)||!glyphs.Contains(r.secondAdjustmentRecord.glyphIndex));
            features.MarkToBaseAdjustmentRecords.RemoveAll(r=>!glyphs.Contains(r.baseGlyphID)||!glyphs.Contains(r.markGlyphID));
            features.MarkToMarkAdjustmentRecords.RemoveAll(r=>!glyphs.Contains(r.baseMarkGlyphID)||!glyphs.Contains(r.combiningMarkGlyphID));
            asset.ReadFontAssetDefinition();
        }
    }
}
