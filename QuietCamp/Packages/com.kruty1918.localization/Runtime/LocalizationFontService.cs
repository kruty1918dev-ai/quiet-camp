using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace Kruty1918.Localization
{
    /// <summary>
    /// Guarantees glyph coverage for every supported language.
    /// Creates one dynamic-atlas <see cref="TMP_FontAsset"/> from a raw font
    /// resource supplied via <see cref="LocalizationFontOptions"/> and wires
    /// it as a fallback both globally (<c>TMP_Settings.fallbackFontAssets</c>) and
    /// on every primary font asset registered via <see cref="RegisterPrimaryFont"/>.
    /// Fallback resolution in TMP keeps primary metrics identical and only routes
    /// missing codepoints through the dynamic asset — no artifacts for existing text.
    /// </summary>
    public sealed class LocalizationFontService
    {
        private readonly LocalizationFontOptions _options;
        private readonly HashSet<TMP_FontAsset> _registered = new HashSet<TMP_FontAsset>();
        private TMP_FontAsset _fallback;

        public LocalizationFontService(LocalizationFontOptions options)
        {
            _options = options ?? new LocalizationFontOptions();
        }

        /// <summary>
        /// Підключає primary font asset до multilingual fallback-ланцюга.
        /// Викликається presenter-ами для шрифту, який передається у UnityHtmlHost.
        /// The dynamic fallback must come FIRST: serialized fallback entries such as
        /// TMP's "LiberationSans SDF - Fallback" are persistent dynamic font assets —
        /// when TMP grows their atlas at runtime, the editor reimports the asset and
        /// destroys the live object, leaving MissingReferenceException in every text
        /// component that still references it. The non-persistent runtime asset is
        /// never reimported, so routing glyph lookups through it first is safe.
        /// </summary>
        public void RegisterPrimaryFont(TMP_FontAsset primary)
        {
            if (primary == null || !_registered.Add(primary)) return;
            TMP_FontAsset fallback = EnsureFallbackFont();
            if (fallback == null || primary == fallback) return;
            primary.fallbackFontAssetTable ??= new List<TMP_FontAsset>();
            // Drop stale runtime fallbacks left by earlier service instances:
            // their material may have been collected while the font asset
            // itself is still alive, and a glyph hit there throws
            // MissingReferenceException inside TMP_MaterialManager.
            primary.fallbackFontAssetTable.RemoveAll(a =>
                a == null ||
                (a.name == _options.FallbackAssetName && a.material == null));
            primary.fallbackFontAssetTable.Remove(fallback);
            primary.fallbackFontAssetTable.Insert(0, fallback);
        }

        /// <summary>
        /// Попередньо прогріває динамічний атлас символами активної мови,
        /// щоб перший рендер після перемикання вже мав усі гліфи.
        /// </summary>
        public void Warmup(LocalizationLanguage language)
        {
            TMP_FontAsset fallback = EnsureFallbackFont();
            if (fallback == null) return;
            string charset = CharsetFor(language?.Id);
            if (charset.Length == 0) return;
            fallback.TryAddCharacters(charset);
            ProtectSubAssets(fallback);
        }

        /// <summary>
        /// Ледаче створення dynamic font asset + реєстрація у глобальному
        /// fallback-листі TMP (покриває tooltips, dynamic UGUI, TMP_Settings.defaultFontAsset).
        /// </summary>
        private TMP_FontAsset EnsureFallbackFont()
        {
            if (_fallback != null && _fallback.material != null) return _fallback;
            _fallback = FindLiveGlobalFallback() ?? CreateFallbackFont();
            if (_fallback == null) return null;
            if (TMP_Settings.instance != null && TMP_Settings.fallbackFontAssets != null)
            {
                // First position here too: glyph searches must reach the
                // non-persistent fallback before any persistent dynamic asset.
                TMP_Settings.fallbackFontAssets.Remove(_fallback);
                TMP_Settings.fallbackFontAssets.Insert(0, _fallback);
            }
            if (TMP_Settings.defaultFontAsset != null)
                RegisterPrimaryFont(TMP_Settings.defaultFontAsset);
            return _fallback;
        }

        /// <summary>
        /// Повторно використовує fallback-асет, який залишився у глобальному
        /// списку TMP від попереднього екземпляра сервісу (перехід сцен).
        /// </summary>
        private TMP_FontAsset FindLiveGlobalFallback()
        {
            var globalList = TMP_Settings.instance != null ? TMP_Settings.fallbackFontAssets : null;
            if (globalList == null) return null;
            TMP_FontAsset live = null;
            for (int i = globalList.Count - 1; i >= 0; i--)
            {
                TMP_FontAsset asset = globalList[i];
                if (asset == null || asset.name != _options.FallbackAssetName)
                    continue;
                // A leftover asset with a dead material must not stay in the
                // global list: glyph lookups hit it first and throw.
                if (asset.material == null)
                    globalList.RemoveAt(i);
                else if (live == null)
                    live = asset;
            }
            return live;
        }

        private TMP_FontAsset CreateFallbackFont()
        {
            Font source = Resources.Load<Font>(_options.FontResourcePath);
            if (source == null)
            {
                Debug.LogError($"[Localization] Font '{_options.FontResourcePath}' not found in Resources; " +
                               "non-ASCII glyphs will be missing.");
                return null;
            }
            TMP_FontAsset fallback = TMP_FontAsset.CreateFontAsset(source);
            fallback.name = _options.FallbackAssetName;
            fallback.hideFlags = HideFlags.HideAndDontSave;
            ProtectSubAssets(fallback);
            return fallback;
        }

        /// <summary>
        /// The asset survives scene unloads via HideAndDontSave, but its
        /// sub-objects (material, atlas textures) keep default flags and would
        /// be collected by Resources.UnloadUnusedAssets between scenes —
        /// leaving a dead m_Material that throws MissingReferenceException
        /// in TMP_MaterialManager.GetFallbackMaterial. Atlas growth during
        /// warmup creates additional textures, so this must run again after
        /// TryAddCharacters.
        /// </summary>
        private static void ProtectSubAssets(TMP_FontAsset fallback)
        {
            if (fallback.material != null)
                fallback.material.hideFlags = HideFlags.HideAndDontSave;
            if (fallback.atlasTextures == null) return;
            foreach (Texture2D atlas in fallback.atlasTextures)
            {
                if (atlas != null)
                    atlas.hideFlags = HideFlags.HideAndDontSave;
            }
        }

        /// <summary>
        /// Повертає набір символів, який потрібен мові понад ASCII.
        /// Постачається хостом через <see cref="LocalizationFontOptions.CharsetForLanguage"/>.
        /// </summary>
        private string CharsetFor(string languageId)
        {
            return _options.CharsetForLanguage?.Invoke(languageId) ?? string.Empty;
        }
    }
}
