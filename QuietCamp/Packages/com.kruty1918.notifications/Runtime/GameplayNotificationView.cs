using Kruty1918.Notifications.API;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Kruty1918.Notifications.Runtime
{
    public sealed class GameplayNotificationView : MonoBehaviour
    {
        [SerializeField] private CanvasGroup _canvasGroup;
        [SerializeField] private RectTransform _rectTransform;
        [SerializeField] private TMP_Text _messageText;

        public CanvasGroup CanvasGroup => _canvasGroup;
        public RectTransform RectTransform => _rectTransform;
        public TMP_Text MessageText => _messageText;

        public static GameplayNotificationView Create(Transform parent)
        {
            var root = new GameObject(
                "GameplayNotificationRoot",
                typeof(RectTransform),
                typeof(CanvasGroup),
                typeof(GameplayNotificationView));

            root.transform.SetParent(parent, false);

            var view = root.GetComponent<GameplayNotificationView>();
            view.BindReferences();
            view.ConfigureRoot();
            view.CreateText();
            view.SetHidden();
            return view;
        }

        public void SetMessage(
            string message,
            GameplayNotificationKind kind)
        {
            BindReferences();
            _messageText.text = message;
            _messageText.color = ResolveTextColor(kind);
        }

        internal void ApplyState(GameplayNotificationVisualState state)
        {
            BindReferences();
            gameObject.SetActive(state.IsActive);
            _canvasGroup.alpha = state.Alpha;
            Vector2 position = _rectTransform.anchoredPosition;
            position.y = state.AnchoredPositionY;
            _rectTransform.anchoredPosition = position;
        }

        public void SetHidden()
        {
            BindReferences();
            _canvasGroup.alpha = 0f;
            Vector2 position = _rectTransform.anchoredPosition;
            position.y = GameplayNotificationSettings.DefaultHiddenOffsetY;
            _rectTransform.anchoredPosition = position;
            gameObject.SetActive(false);
        }

        public void BindReferences()
        {
            if (_rectTransform == null)
                _rectTransform = GetComponent<RectTransform>();

            if (_canvasGroup == null)
                _canvasGroup = GetComponent<CanvasGroup>();

            if (_messageText == null)
                _messageText = GetComponentInChildren<TMP_Text>(true);

            if (_canvasGroup != null)
            {
                _canvasGroup.blocksRaycasts = false;
                _canvasGroup.interactable = false;
            }

            if (_messageText != null)
                _messageText.raycastTarget = false;
        }

        private void ConfigureRoot()
        {
            _rectTransform.anchorMin = new Vector2(0.5f, 1f);
            _rectTransform.anchorMax = new Vector2(0.5f, 1f);
            _rectTransform.pivot = new Vector2(0.5f, 1f);
            _rectTransform.sizeDelta = new Vector2(760f, 72f);
            _rectTransform.anchoredPosition = new Vector2(0f, GameplayNotificationSettings.DefaultHiddenOffsetY);
        }

        private void CreateText()
        {
            var textObject = new GameObject(
                "Message",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(TextMeshProUGUI),
                typeof(Shadow));
            textObject.transform.SetParent(transform, false);

            var rect = textObject.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            _messageText = textObject.GetComponent<TMP_Text>();
            _messageText.alignment = TextAlignmentOptions.Center;
            _messageText.textWrappingMode = TextWrappingModes.Normal;
            _messageText.overflowMode = TextOverflowModes.Ellipsis;
            _messageText.fontSize = 32f;
            _messageText.fontStyle = FontStyles.Bold;
            _messageText.raycastTarget = false;

            var shadow = textObject.GetComponent<Shadow>();
            shadow.effectColor = new Color(0f, 0f, 0f, 0.72f);
            shadow.effectDistance = new Vector2(1.5f, -1.5f);
            shadow.useGraphicAlpha = true;
        }

        private static Color ResolveTextColor(GameplayNotificationKind kind)
        {
            return kind switch
            {
                GameplayNotificationKind.Success => new Color(0.55f, 0.93f, 0.66f, 1f),
                GameplayNotificationKind.Warning => new Color(1f, 0.78f, 0.34f, 1f),
                GameplayNotificationKind.Error => new Color(1f, 0.48f, 0.44f, 1f),
                _ => new Color(0.92f, 0.96f, 1f, 1f),
            };
        }
    }
}
