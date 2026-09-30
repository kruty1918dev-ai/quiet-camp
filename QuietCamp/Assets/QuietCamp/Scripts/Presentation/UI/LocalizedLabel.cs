using Kruty1918.Localization;
using TMPro;
using UnityEngine;
namespace QuietCamp.Presentation.UI
{
    /// <summary>
    /// TMP label bound to a localization key; re-renders on LanguageChanged.
    /// Holds no translated text in scenes or prefabs.
    /// </summary>
    [RequireComponent(typeof(TMP_Text))]
    public sealed class LocalizedLabel : MonoBehaviour
    {
        static ILocalizationService _localization;
        public static ILocalizationService Localization
        {
            get => _localization;
            set
            {
                if (_localization == value) return;
                if (_localization != null) _localization.LanguageChanged -= RefreshAll;
                _localization = value;
                if (_localization != null) _localization.LanguageChanged += RefreshAll;
                RefreshAll();
            }
        }

        static readonly System.Collections.Generic.List<LocalizedLabel> _alive
            = new System.Collections.Generic.List<LocalizedLabel>();

        static void RefreshAll()
        {
            for (var i = _alive.Count - 1; i >= 0; i--) _alive[i].Refresh();
        }

        string _key;
        object[] _args;
        TMP_Text _text;
        float _baseSize;
        static float _textScale = 1f;

        public static float TextScale
        {
            get => _textScale;
            set { _textScale = Mathf.Clamp(value, 0.85f, 1.5f); RefreshAll(); }
        }

        public void Bind(string key, params object[] args)
        {
            _key = key;
            _args = args;
            Refresh();
        }

        void Awake()
        {
            _text = GetComponent<TMP_Text>();
            _baseSize = _text.fontSize;
        }

        void OnEnable() => _alive.Add(this);
        void OnDisable() => _alive.Remove(this);

        public void Refresh()
        {
            if (_text == null) _text = GetComponent<TMP_Text>();
            if (_text == null) return;
            // Awake is deferred on inactive objects — capture lazily so the
            // first Bind on an inactive label does not zero the font size.
            if (_baseSize <= 0f && _text.fontSize > 0f) _baseSize = _text.fontSize;
            if (_baseSize > 0f) _text.fontSize = _baseSize * _textScale;
            if (_localization == null || string.IsNullOrEmpty(_key)) return;
            _text.text = _args == null || _args.Length == 0
                ? _localization.T(_key)
                : _localization.TF(_key, _args);
        }
    }
}
