using System.Linq;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using QuietCamp.Presentation.UI;
using TMPro;
using UnityEditor;
using UnityEngine;

namespace QuietCamp.Tests
{
    public sealed class CampTypographyTests
    {
        [TestCase("uk")][TestCase("en")][TestCase("de")]
        public void EveryLocalizedLetterAlreadyExistsInBothWeights(string language)
        {
            var regular=CampTypography.Font;
            Assert.NotNull(regular);
            var bold=regular.fontWeightTable[7].regularTypeface;
            Assert.NotNull(bold,"Bold must have real letterforms, not synthetic thickening.");
            var entries=JObject.Parse(Resources.Load<TextAsset>("QuietCampLocales/"+language).text)["entries"];
            var letters=entries.Children<JProperty>().Select(p=>p.Value.Value<string>()).SelectMany(s=>s).Where(char.IsLetterOrDigit).Distinct().ToArray();
            foreach(var font in new[]{regular,bold})
            {
                Assert.AreEqual("Noto Sans",font.faceInfo.familyName);
                foreach(var letter in letters)
                    Assert.IsTrue(font.characterLookupTable.ContainsKey(letter),language+" "+font.name+" U+"+((int)letter).ToString("X4"));
            }
        }

        [Test]
        public void AtlasMaterialAndDefaultFontAgree()
        {
            var regular=CampTypography.Font;
            Assert.AreSame(regular,TMP_Settings.defaultFontAsset);
            foreach(var font in new[]{regular,regular.fontWeightTable[7].regularTypeface})
            {
                Assert.AreEqual(font.atlasWidth,font.material.GetFloat("_TextureWidth"));
                Assert.AreEqual(font.atlasHeight,font.material.GetFloat("_TextureHeight"));
                Assert.AreEqual(font.atlasPadding+1,font.material.GetFloat("_GradientScale"));
                Assert.AreEqual(font.atlasTextures[0],font.material.mainTexture);
                Assert.IsNotEmpty(font.fontFeatureTable.glyphPairAdjustmentRecords,"Kerning must be prepared for both scripts.");
                var glyphs=font.glyphTable.Select(g=>g.index).ToArray();
                Assert.IsTrue(font.fontFeatureTable.glyphPairAdjustmentRecords.All(r=>glyphs.Contains(r.firstAdjustmentRecord.glyphIndex)&&glyphs.Contains(r.secondAdjustmentRecord.glyphIndex)),"Only usable kerning records should be serialized.");
                Assert.IsTrue(font.fontFeatureTable.MarkToBaseAdjustmentRecords.All(r=>glyphs.Contains(r.baseGlyphID)&&glyphs.Contains(r.markGlyphID)));
                Assert.AreEqual(0,font.boldSpacing);
                Assert.IsFalse(new SerializedObject(font).FindProperty("m_ClearDynamicDataOnBuild").boolValue);
            }
        }

        [TestCase(FontStyles.Normal)][TestCase(FontStyles.Bold)]
        public void MixedScriptsRenderThroughOneFamilyWithoutFallback(FontStyles style)
        {
            var canvas=new GameObject("Typography test canvas",typeof(Canvas));
            var go=new GameObject("Typography test",typeof(RectTransform),typeof(CanvasRenderer),typeof(TextMeshProUGUI));
            go.transform.SetParent(canvas.transform,false);
            try
            {
                var label=go.GetComponent<TextMeshProUGUI>();
                label.font=CampTypography.Font;label.fontStyle=style;label.fontSize=32;
                label.rectTransform.sizeDelta=new Vector2(1600,160);
                label.text="Тихий кемпінг — Quiet Camp — Ruhiges Camp: Іі Її Єє Ґґ Ää Öö Üü ß";
                label.ForceMeshUpdate(true,true);
                Assert.Greater(label.textInfo.characterCount,50);
                foreach(var character in label.textInfo.characterInfo.Take(label.textInfo.characterCount).Where(c=>char.IsLetter(c.character)))
                {
                    Assert.AreEqual("Noto Sans",character.fontAsset.faceInfo.familyName,character.character.ToString());
                    Assert.AreEqual(style==FontStyles.Bold?"Bold":"Regular",character.fontAsset.faceInfo.styleName);
                }
            }
            finally { Object.DestroyImmediate(canvas); }
        }
    }
}
