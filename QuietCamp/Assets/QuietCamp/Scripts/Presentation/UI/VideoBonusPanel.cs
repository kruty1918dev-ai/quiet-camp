using Kruty1918.Audio;
using Kruty1918.InputRouting.API;
using Kruty1918.UIActions.API;
using QuietCamp.Application;
using UnityEngine;

namespace QuietCamp.Presentation.UI
{
    public static class VideoBonusPanel
    {
        public static string Render(GameServices services, HtmlSurface surface, string placement)
        {
            string T(string key) => services.Localization.T(key);
            // No placeholder ad button in a build with no provider configured.
            // Settings still explains where the optional bonus will live.
            if (services.Rewards.Owned) return HtmlUi.Text(T("bonus.owned"), "s-sub");
            if (!services.Settings.optionalVideoBonuses) return "";
            if (!services.Rewards.Available)
                return placement == "album.lantern" ? HtmlUi.Text(T("bonus.freePath"), "s-sub") : "";
            return "<view class=\"bonus-card\">" + HtmlUi.Text(T("bonus.title"), "s-label")
                + HtmlUi.Text(T("bonus.explanation"), "s-sub")
                + HtmlUi.Button(surface, "video-bonus", T("bonus.watch"), async () =>
                {
                    if (services.Rewards.Busy || !services.Rewards.Available) return;
                    var block = services.InputPolicy.AcquireBlock(GameplayInputKind.AllPointer, surface);
                    var context = services.ContextStack.Push(new UiContextRegistration("OptionalVideo", UiContextLayer.Modal, 200,
                        () => services.Rewards.Busy, new UiActionId("qc.back"), blocksLowerHotkeys: true));
                    var gate = VideoBonusGate.Create(services, T("bonus.preparing"));
                    services.Audio?.SetBusVolume(AudioBus.Master, 0);
                    try
                    {
                        var result = await services.Rewards.Request(placement);
                        services.Notifications?.Show(T(result == CozyRewardResult.Granted ? "bonus.granted" : result == CozyRewardResult.Skipped ? "bonus.skipped" : "bonus.unavailable"), Kruty1918.Notifications.API.GameplayNotificationKind.Info);
                    }
                    finally
                    {
                        block.Dispose(); context.Dispose();
                        services.Audio?.SetBusVolume(AudioBus.Master, services.Settings.master);
                        if (gate != null) Object.Destroy(gate);
                        if (surface != null) surface.Refresh();
                    }
                }, "quiet") + "</view>";
        }
    }
    sealed class VideoBonusGate : MonoBehaviour
    {
        IAdRequestCancellation _cancel;
        UnityEngine.UI.Button _button;
        public static GameObject Create(GameServices services, string title)
        {
            var go = new GameObject("OptionalVideoGate", typeof(RectTransform));
            Object.DontDestroyOnLoad(go);
            var canvas = go.AddComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 90;
            go.AddComponent<UnityEngine.UI.GraphicRaycaster>();
            var scaler = go.AddComponent<UnityEngine.UI.CanvasScaler>(); scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 1920); scaler.matchWidthOrHeight = .5f;
            var dim = QcUi.Stretch(go.transform, "Dim").gameObject.AddComponent<UnityEngine.UI.Image>(); dim.color = new Color(.09f, .18f, .13f, .85f);
            var text = QcUi.Stretch(go.transform, "Preparing").gameObject.AddComponent<TMPro.TextMeshProUGUI>();
            text.font = Resources.Load<TMPro.TMP_FontAsset>("Fonts/DejaVuSans SDF");
            text.text = title; text.fontSize = 34; text.color = MenuArt.Cream; text.alignment = TMPro.TextAlignmentOptions.Center; text.raycastTarget = false;
            var gate = go.AddComponent<VideoBonusGate>(); gate._cancel = services.Ads as IAdRequestCancellation;
            if (gate._cancel != null)
            {
                var rect = QcUi.Root(go.transform, "CancelVideoPreparation"); rect.anchorMin = rect.anchorMax = new Vector2(.5f, .35f); rect.sizeDelta = new Vector2(420, 100);
                gate._button = QcUi.Button(rect, "action.cancel", () => gate._cancel.CancelPending(), QcUi.Brown);
            }
            return go;
        }
        void Update() { if (_button != null) _button.interactable = _cancel.CanCancelPending; }
    }
}
