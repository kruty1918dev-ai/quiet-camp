using System;
using System.Collections.Generic;
using System.IO;
using Kruty1918.Localization;
using QuietCamp.Application;
using QuietCamp.Infrastructure;
using TMPro;
using UnityEngine;
using UnityHTML.Runtime;
using UnityHTML.Runtime.Content;

namespace QuietCamp.Presentation.UI
{
    /// <summary>Runs before composition of optional providers. A placeholder is visibly a draft, never legal consent.</summary>
    public sealed class BootPrivacyPanel : MonoBehaviour
    {
        [Serializable, UnityEngine.Scripting.Preserve] public sealed class Publication
        {
            public bool draft = true;
            public ContentSource document;
            public Summary[] summaries;
        }
        [Serializable, UnityEngine.Scripting.Preserve] public sealed class Summary { public string language, revision, text; }
        readonly UnityHtmlHost _host = new UnityHtmlHost();
        readonly HtmlCallbacks _callbacks = new HtmlCallbacks();
        UnityHtmlContentBinding _content;
        SaveAdapter _save;
        ILocalizationService _locale;
        Publication _publication;
        bool _dirty = true, _full, _saveFailed;
        public bool Accepted { get; private set; }
        public bool Declined { get; private set; }
        public ContentDocument Document => _content?.Document;
        public string MountError { get; private set; }
        public static BootPrivacyPanel Current { get; private set; }

        public static BootPrivacyPanel Create(Transform parent, SaveAdapter save, ILocalizationService locale)
        {
            var root = QcUi.Stretch(parent, "BootPrivacy");
            var panel = root.gameObject.AddComponent<BootPrivacyPanel>();
            Current = panel; panel._save = save; panel._locale = locale;
            panel._host.NativeEventResolver = panel._callbacks.ResolveNativeEvent;
            panel._host.BackRequested += panel.Refuse;
            var asset = Resources.Load<TextAsset>("QuietCamp/privacy_content");
            panel._publication = asset != null ? JsonUtility.FromJson<Publication>(asset.text) : null;
            if (panel._publication?.document == null) throw new InvalidOperationException("Bundled privacy preview is missing.");
            var legal = LegalConfiguration.Load();
            // Draft resources never fetch a remote URL or create a network consent receipt.
            if (panel._publication.draft)
                foreach (var variant in panel._publication.document.variants) { variant.url = ""; variant.sourceUrl = ""; }
            else
            {
                if (!legal.Published) throw new InvalidOperationException("Publish legal metadata before enabling the real Boot policy.");
                foreach (var variant in panel._publication.document.variants)
                    if (variant.revision != legal.revision || !panel._publication.document.Allows(variant.url))
                        throw new InvalidOperationException("Policy variants must match the published revision and approved HTTPS origins.");
            }
            panel._content = root.gameObject.AddComponent<UnityHtmlContentBinding>();
            panel._content.Changed += panel.DocumentChanged;
            var cache = Path.Combine(UnityEngine.Application.persistentDataPath, "qc_policy_cache");
            panel._content.Load(panel._publication.document, locale.CurrentLanguageId, new UnityHtmlContentLoader(cache));
            return panel;
        }
        void DocumentChanged()
        {
            _dirty = true;
            if (Document != null && PrivacyAcknowledgement.IsCurrent(_save.Settings, Document.Revision, Document.ContentHash, _publication.draft))
                Accepted = true;
        }
        string T(string key) => UnityHtmlContentReader.Escape(_locale.T(key));
        string Button(string id, string label, Action click, string classes = "", bool enabled = true)
        {
            _callbacks.Bind(id, click);
            return "<button id=\"" + id + "\" class=\"" + classes + "\" " + (enabled ? "onClick=\"Globals.campUi.Click('" + id + "')\"" : "disabled=\"true\"") + "><text>" + label + "</text></button>";
        }
        string Render()
        {
            _callbacks.Clear();
            string status = Document == null ? T(_content.Loading ? "boot.privacy.loading" : "boot.privacy.unavailable")
                : T(_publication.draft ? "boot.privacy.draft" : "boot.privacy.acknowledgement");
            string text = "";
            if (Document != null)
            {
                if (_full) text = _content.Render();
                else
                {
                    var summary = Array.Find(_publication.summaries ?? Array.Empty<Summary>(), s => s.language == Document.Language && s.revision == Document.Revision);
                    text = "<text class=\"policy-summary\">" + UnityHtmlContentReader.Escape(summary?.text ?? Document.PlainText) + "</text>";
                }
                if (Document.IsLanguageFallback(_locale.CurrentLanguageId))
                    status += " " + T("boot.privacy.sourceLanguage") + " " + UnityHtmlContentReader.Escape(Document.Language);
                if (Document.Origin == ContentOrigin.Cache) status += " " + T("boot.privacy.cached");
            }
            string links = Button("boot-policy-full", T(_full ? "boot.privacy.short" : "boot.privacy.full"), () => { _full = !_full; _dirty = true; }, "policy-link", Document != null)
                + Button("boot-policy-source", T("boot.privacy.website"), () => _content.OpenSource(_publication.document), "policy-link", _content.CanOpenSource(_publication.document));
            if (!_content.Loading && Document == null)
                links += Button("boot-policy-retry", T("boot.retry"), () => _content.Load(_publication.document, _locale.CurrentLanguageId, new UnityHtmlContentLoader(Path.Combine(UnityEngine.Application.persistentDataPath, "qc_policy_cache"))));
            return "<view class=\"policy-page\"><view class=\"policy-card\"><text class=\"policy-title\">" + T("boot.privacy.title")
                + "</text><text class=\"policy-status\">" + status + "</text><scroll id=\"boot-policy-scroll\" class=\"policy-scroll\"><view>" + text
                + "<text class=\"policy-note\">" + T("boot.privacy.separate") + "</text></view></scroll><view class=\"policy-links\">" + links + "</view>"
                + (_saveFailed ? "<text class=\"policy-status\">" + T("save.failed") + "</text>" : "")
                + "<view class=\"policy-actions\">" + Button("boot-policy-exit", T("boot.privacy.exit"), Refuse)
                + Button("boot-policy-ack", T(_publication.draft ? "boot.privacy.continueDraft" : "boot.privacy.continue"), Accept, "policy-primary", Document != null && !Declined)
                + "</view></view></view>";
        }
        void Refuse() { if (!Accepted) Declined = true; }
        void Accept()
        {
            if (Accepted || Declined || Document == null) return;
            string previous = JsonUtility.ToJson(_save.Settings);
            PrivacyAcknowledgement.Record(_save.Settings, Document.Revision, Document.ContentHash, Document.Language, _publication.draft);
            _save.Settings.language = _locale.CurrentLanguageId;
            if (_save.Save()) Accepted = true;
            else { JsonUtility.FromJsonOverwrite(previous, _save.Settings); _saveFailed = true; _dirty = true; }
        }
        void LateUpdate()
        {
            if (!_dirty || Accepted || Declined || ((RectTransform)transform).rect.width < 1) return;
            _dirty = false;
            try
            {
                var css = System.Text.RegularExpressions.Regex.Replace(Css, @"font-size:\s*([0-9.]+)px", match => "font-size:" + HtmlUi.Number(float.Parse(match.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture) * _save.Settings.textScale) + "px");
                _host.Motion.ReducedMotion = _save.Settings.reducedMotion;
                var result = _host.Mount((RectTransform)transform, new UnityHtmlDocument(Render(), css, "Boot privacy"), new Dictionary<string, object>
                { ["campFont"] = Resources.Load<TMP_FontAsset>("Fonts/DejaVuSans SDF"), ["moyvaFont"] = Resources.Load<TMP_FontAsset>("Fonts/DejaVuSans SDF") });
                if (!result.Succeeded) MountError = result.ErrorMessage;
            }
            catch (Exception exception) { MountError = exception.Message; }
        }
        void OnDestroy() { if (Current == this) Current = null; if (_content != null) _content.Changed -= DocumentChanged; _host.BackRequested -= Refuse; _host.Dispose(); }
        const string Css = @"
          .policy-page { width:100%; height:100%; padding:32px; justify-content:center; align-items:center; }
          .policy-card { width:100%; max-width:1120px; height:86%; max-height:1420px; padding:40px; background-color:#f5efdc; border-radius:32px; }
          text { color:#243e35; font-size:30px; flex-shrink:0; white-space:normal; }
          .policy-title { font-size:48px; margin-bottom:20px; }
          .policy-status { font-size:25px; color:#746447; margin-bottom:24px; }
          .policy-scroll { flex-grow:1; flex-shrink:1; min-height:60px; }
          .policy-summary, .remote-paragraph, .remote-list-item { margin-bottom:24px; }
          .remote-heading { font-size:36px; margin-bottom:18px; }
          .policy-note { color:#546c58; font-size:26px; margin-top:24px; }
          .policy-links { flex-direction:row; flex-wrap:wrap; gap:14px; margin-top:24px; }
          button { min-height:100px; border-radius:20px; background-color:#e6e0ce; padding:16px 24px; justify-content:center; align-items:center; }
          button:disabled { opacity:0.45; }
          .policy-link { min-height:76px; flex-grow:1; }
          .policy-link text { font-size:24px; }
          .policy-actions { flex-direction:row; gap:20px; margin-top:24px; }
          .policy-actions button { flex-basis:0; flex-grow:1; }
          .policy-actions text, .policy-links text { width:100%; white-space:normal; text-align:center; }
          .policy-primary { background-color:#2e5144; }
          .policy-primary text { color:#f5efdc; }
          @media (layout: wide) { .policy-card { height:94%; padding:28px 40px; } .policy-title { font-size:38px; margin-bottom:12px; } .policy-status { margin-bottom:12px; } .policy-links, .policy-actions { margin-top:14px; } button { min-height:80px; } }
        ";
    }
}
