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
        PrivacySetup _setup;
        bool _showDocument;
        bool _published;
        int _lastBackFrame = -1;
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
            panel._host.BackRequested += panel.Back;
            var asset = Resources.Load<TextAsset>("QuietCamp/privacy_content");
            panel._publication = asset != null ? JsonUtility.FromJson<Publication>(asset.text) : null;
            if (panel._publication?.document == null) throw new InvalidOperationException("Bundled privacy preview is missing.");
            var legal = LegalConfiguration.Load();
            panel._published = legal.Published;
            var google = GoogleServicesConfiguration.Load(legal);
            panel._setup = new PrivacySetup(
                google.analyticsEnabled && google.OnlineServicesApproved && OptionalGoogleServices.AnalyticsFactory != null,
                google.crashlyticsEnabled && google.OnlineServicesApproved && OptionalGoogleServices.CrashFactory != null,
                google.CanUseRewardedAds(UnityEngine.Application.isEditor || Debug.isDebugBuild)
                    && !string.IsNullOrWhiteSpace(google.CurrentRewardedUnitId) && OptionalGoogleServices.AdsFactory != null,
                google.LimitedAdsApproved && google.underAgeLimitedAdsEnabled && legal.underAgeAdvertisingReviewed && !google.useTestAds
                    && !string.IsNullOrWhiteSpace(google.CurrentRewardedUnitId) && OptionalGoogleServices.AdsFactory != null, legal.teenPrivacyReviewed);
            // Draft resources never fetch a remote URL or create a network consent receipt.
            if (panel._publication.draft)
                foreach (var variant in panel._publication.document.variants) variant.url = "";
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
            if (Document == null) return;
            if (PrivacySetup.IsComplete(_save.Settings, Document.Revision, Document.ContentHash, _publication.draft))
                Accepted = true;
            else PrivacyEligibility.Revoke(_save.Settings); // No restored optional grants before the new flow completes.
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
            string body = text;
            string actions;
            if (_showDocument)
            {
                body = _content.Render();
                actions = Button("boot-document-back", T("privacy.setup.back"), () => { _showDocument = false; _dirty = true; });
            }
            else
            {
                if (_setup.Step != PrivacySetupStep.Policy)
                {
                    body = StepBody();
                    status = T("privacy.setup.stage." + _setup.Step);
                    links = Button("boot-policy-open", T("boot.privacy.full"), () => { _showDocument = true; _dirty = true; }, "policy-link", Document != null)
                        + Button("boot-policy-source", T("boot.privacy.website"), () => _content.OpenSource(_publication.document), "policy-link", _content.CanOpenSource(_publication.document));
                }
                actions = StepActions();
            }
            return "<view class=\"policy-page\"><view class=\"policy-card\"><text class=\"policy-title\">" + T("boot.privacy.title")
                + "</text><text class=\"policy-status\">" + status + "</text><scroll id=\"boot-policy-scroll\" class=\"policy-scroll\"><view>" + body
                + "</view></scroll><view class=\"policy-links\">" + links + "</view>"
                + (_saveFailed ? "<text class=\"policy-status\">" + T("save.failed") + "</text>" : "")
                + "<view class=\"policy-actions\">" + actions + "</view></view></view>";
        }
        string Note(string key) => "<text class=\"policy-note\">" + T(key) + "</text>";
        string StepBody()
        {
            switch (_setup.Step)
            {
                case PrivacySetupStep.Age:
                    string choices = Note("privacy.setup.ageHelp") + "<view class=\"policy-age-choices\">";
                    foreach (PrivacyAgeBand band in Enum.GetValues(typeof(PrivacyAgeBand)))
                    {

                        var choice = band;
                        choices += Button("boot-age-" + (int)band, (_setup.HasAgeChoice && _setup.Age == band ? "✓ " : "") + T("privacy.age." + (int)band),
                            () => { _setup.SelectAge(choice); _dirty = true; }, _setup.HasAgeChoice && _setup.Age == band ? "policy-selected" : "");
                    }
                    return choices + "</view>";
                case PrivacySetupStep.Analytics: return Note("privacy.explanation") + Note("privacy.provider") + Note("privacy.setup.optionalHelp");
                case PrivacySetupStep.Crashes: return Note("privacy.crashes.explanation") + Note("privacy.setup.optionalHelp");
                case PrivacySetupStep.Advertising: return Note("privacy.advertising.help") + Note("privacy.setup.adsHelp") + Note("privacy.setup.optionalHelp");
                default:
                    string body = Note(_setup.Eligible ? "privacy.setup.review" : "privacy.setup.protected");
                    if (!_setup.HasOptionalServices) body += Note("privacy.setup.unavailable");
                    body += Note("privacy.setup.local");
                    foreach (var item in new[] { new KeyValuePair<string, bool>("privacy.setup.analytics", _setup.Analytics),
                        new KeyValuePair<string, bool>("privacy.setup.crashes", _setup.Crashes) })
                        body += "<text class=\"policy-note\">" + T(item.Key) + ": " + T(item.Value ? "privacy.setup.on" : "privacy.setup.off") + "</text>";
                    body += _setup.Age == PrivacyAgeBand.From13To15
                        ? Note(_setup.RestrictedVideosAvailable ? "privacy.setup.limitedAvailable" : "privacy.setup.limitedInactive")
                        : "<text class=\"policy-note\">" + T("privacy.setup.ads") + ": " + T(_setup.Advertising ? "privacy.setup.on" : "privacy.setup.off") + "</text>";
                    return body + Note("privacy.setup.withdraw");
            }
        }
        string StepActions()
        {
            switch (_setup.Step)
            {
                case PrivacySetupStep.Policy:
                    return Button("boot-policy-exit", T("boot.privacy.exit"), Refuse)
                        + Button("boot-policy-ack", T("privacy.setup.next"), () => { _setup.ReadPolicy(); _dirty = true; }, "", Document != null && !Declined);
                case PrivacySetupStep.Age:
                    return Button("boot-privacy-back", T("privacy.setup.back"), Back)
                        + Button("boot-age-next", T("privacy.setup.next"), () => { _setup.ConfirmAge(); _dirty = true; }, "", _setup.HasAgeChoice);
                case PrivacySetupStep.Review:
                    return Button("boot-privacy-back", T("privacy.setup.back"), Back)
                        + Button("boot-privacy-finish", T("privacy.setup.play"), Accept);
                default:
                    return Button("boot-privacy-no", T("privacy.setup.decline"), () => { _setup.Choose(false); _dirty = true; })
                        + Button("boot-privacy-yes", T("privacy.setup.allow"), () => { _setup.Choose(true); _dirty = true; })
                        + Button("boot-privacy-skip", T("privacy.setup.skip"), () => { _setup.ContinueWithoutOptional(); _dirty = true; })
                        + Button("boot-privacy-back", T("privacy.setup.back"), Back);
            }
        }
        void Refuse() { if (!Accepted) Declined = true; }
        void Back()
        {
            if (_lastBackFrame == Time.frameCount) return;
            _lastBackFrame = Time.frameCount;
            if (_showDocument) _showDocument = false;
            else if (_setup.Step == PrivacySetupStep.Policy) Refuse();
            else _setup.Back();
            _dirty = true;
        }
        public void RequestBack() => Back();
        void Accept()
        {
            if (Accepted || Declined || Document == null) return;
            Accepted = _setup.Commit(_save.Settings, Document.Revision, Document.ContentHash, _locale.CurrentLanguageId,
                _publication.draft, () => _save.MemoryOnly ? _save.Save() : PrivacyInstallScope.Commit(_save.Settings, _save.Save),
                _published ? Document.Revision + ":" + Document.ContentHash : Document.Revision);
            _saveFailed = !Accepted; _dirty = true;
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
                { ["campFont"] = CampTypography.Font, ["moyvaFont"] = CampTypography.Font });
                if (!result.Succeeded) MountError = result.ErrorMessage;
            }
            catch (Exception exception) { MountError = exception.Message; }
        }
        void OnDestroy() { if (Current == this) Current = null; if (_content != null) _content.Changed -= DocumentChanged; _host.BackRequested -= Back; _host.Dispose(); }
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
          .policy-age-choices { gap:14px; margin-top:24px; }
          .policy-selected { background-color:#cad8bf; }
          .policy-actions { flex-wrap:wrap; }
          .policy-primary { background-color:#2e5144; }
          .policy-primary text { color:#f5efdc; }
          @media (layout: wide) { .policy-card { height:94%; padding:28px 40px; } .policy-title { font-size:38px; margin-bottom:12px; } .policy-status { margin-bottom:12px; } .policy-links, .policy-actions { margin-top:14px; } button { min-height:80px; } }
        ";
    }
}
