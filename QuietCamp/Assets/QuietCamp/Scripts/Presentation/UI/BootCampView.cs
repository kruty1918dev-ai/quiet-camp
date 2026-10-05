using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace QuietCamp.Presentation.UI
{
    /// <summary>Native startup UI renders before audio/assets load, so startup
    /// never depends on the HTML runtime it is preparing. All art bleeds; text
    /// and recovery actions stay inside the device safe area.</summary>
    public sealed class BootCampView : MonoBehaviour
    {
        CanvasGroup _group;
        TMP_Text _title, _stage;
        Image _fill;
        RectTransform _track, _safe, _brand, _stageRect;
        Vector2 _lastSize;
        GameObject _retry;
        GameObject _recoveryInput;
        Func<string, string> _translate;
        string _stageKey = "boot.save";
        float _progress, _shown;
        public float Progress => _progress;
        public bool Failed { get; private set; }
        public RectTransform PolicyParent => _safe;
        public void ShowPolicy(bool visible)
        {
            _brand.gameObject.SetActive(!visible); _stageRect.gameObject.SetActive(!visible);
            _track.gameObject.SetActive(!visible); _retry.SetActive(false);
        }

        public static BootCampView Create(Transform owner, Action retry)
        {
            var root = new GameObject("BootCamp", typeof(RectTransform));
            root.transform.SetParent(owner, false);
            var canvas = root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 75;
            root.AddComponent<GraphicRaycaster>();
            var scaler = root.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 1920); scaler.matchWidthOrHeight = .5f;
            var view = root.AddComponent<BootCampView>();
            view._group = root.AddComponent<CanvasGroup>();
            MenuArt.BuildBackground(root.transform);
            var veil = QcUi.Stretch(root.transform, "ForestVeil").gameObject.AddComponent<Image>();
            veil.color = new Color(.09f, .19f, .14f, .70f);
            var safe = QcUi.Stretch(root.transform, "SafeArea"); safe.gameObject.AddComponent<SafeAreaFitter>();
            view._safe = safe;
            var brand = QcUi.Root(safe, "CampBrand"); view._brand = brand;
            brand.anchorMin = brand.anchorMax = new Vector2(.5f, .57f); brand.sizeDelta = new Vector2(760, 300);
            view._title = Text(brand, "Title", 74, MenuArt.Cream);
            view._title.text = "Тихий\nкемпінг";
            var stage = QcUi.Root(safe, "Preparation"); view._stageRect = stage;
            stage.anchorMin = stage.anchorMax = new Vector2(.5f, .18f); stage.sizeDelta = new Vector2(780, 120);
            view._stage = Text(stage, "Stage", 29, MenuArt.Cream);
            view._stage.text = "Готуємо вашу галявину…";
            view._track = QcUi.Root(safe, "TrailProgress");
            view._track.anchorMin = view._track.anchorMax = new Vector2(.5f, .13f); view._track.sizeDelta = new Vector2(340, 5);
            var background = view._track.gameObject.AddComponent<Image>(); background.color = new Color(1, 1, 1, .15f);
            var fill = QcUi.Stretch(view._track, "WarmProgress");
            view._fill = fill.gameObject.AddComponent<Image>(); view._fill.color = new Color(.94f, .73f, .38f);
            var retryRoot = QcUi.Root(safe, "Retry");
            retryRoot.anchorMin = retryRoot.anchorMax = new Vector2(.5f, .09f); retryRoot.sizeDelta = new Vector2(440, 100);
            QcUi.Button(retryRoot, "boot.retry", retry, QcUi.GreenDark);
            retryRoot.GetComponentInChildren<TMP_Text>().text = "Спробувати ще раз";
            view._retry = retryRoot.gameObject; view._retry.SetActive(false);
            return view;
        }
        static TMP_Text Text(RectTransform parent, string name, float size, Color color)
        {
            var text = QcUi.Stretch(parent, name).gameObject.AddComponent<TextMeshProUGUI>();
            text.font = Resources.Load<TMP_FontAsset>("Fonts/DejaVuSans SDF");
            text.fontSize = size; text.alignment = TextAlignmentOptions.Center; text.color = color; text.raycastTarget = false;
            return text;
        }
        public void Localize(Func<string, string> translate)
        { _translate = translate; _title.text = translate("menu.title.line1") + "\n" + translate("menu.title.line2"); _stage.text = translate(_stageKey); _retry.GetComponentInChildren<TMP_Text>(true).text = translate("boot.retry"); }
        public void Stage(string key, float completed)
        { _stageKey = key; _progress = Mathf.Clamp01(completed); if (_translate != null) _stage.text = _translate(key); }
        public void Fail()
        {
            Failed = true; Stage("boot.failed", _progress); _retry.SetActive(true);
            if (UnityEngine.EventSystems.EventSystem.current == null)
            {
                _recoveryInput = new GameObject("BootRecoveryInput", typeof(UnityEngine.EventSystems.EventSystem), typeof(UnityEngine.InputSystem.UI.InputSystemUIInputModule));
                _recoveryInput.transform.SetParent(transform, false);
                _recoveryInput.GetComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>().AssignDefaultActions();
            }
        }
        public void Retry()
        {
            Failed = false; _retry.SetActive(false); _group.alpha = 1;
            if (_recoveryInput != null) { _recoveryInput.SetActive(false); Destroy(_recoveryInput); _recoveryInput = null; }
        }
        public void Fade(float opacity) => _group.alpha = opacity;
        void Update()
        {
            if (_safe != null && _lastSize != _safe.rect.size)
            {
                _lastSize = _safe.rect.size;
                var wide = _lastSize.x > _lastSize.y;
                _brand.anchorMin = _brand.anchorMax = new Vector2(.5f, wide ? .64f : .57f);
                _brand.sizeDelta = new Vector2(Mathf.Min(760, _lastSize.x - 64), wide ? 220 : 300);
                _stageRect.anchorMin = _stageRect.anchorMax = new Vector2(.5f, wide ? .28f : .18f);
                _stageRect.sizeDelta = new Vector2(Mathf.Min(780, _lastSize.x - 64), wide ? 90 : 120);
                _track.anchorMin = _track.anchorMax = new Vector2(.5f, wide ? .18f : .13f);
            }
            _shown = Mathf.MoveTowards(_shown, _progress, Time.unscaledDeltaTime * 2);
            _fill.rectTransform.anchorMax = new Vector2(_shown, 1);
        }
    }
}
