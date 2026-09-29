using System;

namespace Kruty1918.Notifications.API
{
    public readonly struct GameplayNotificationRequest
    {
        public GameplayNotificationRequest(
            string message,
            GameplayNotificationKind kind = GameplayNotificationKind.Info,
            float? holdDuration = null,
            string dedupKey = null)
        {
            Message = message;
            Kind = kind;
            HoldDuration = holdDuration;
            DedupKey = dedupKey;
        }

        public string Message { get; }
        public GameplayNotificationKind Kind { get; }
        public float? HoldDuration { get; }
        public string DedupKey { get; }

        public bool HasDedupKey
            => !string.IsNullOrWhiteSpace(DedupKey);

        public GameplayNotificationRequest Normalized()
        {
            return new GameplayNotificationRequest(
                Message?.Trim(),
                Enum.IsDefined(typeof(GameplayNotificationKind), Kind)
                    ? Kind
                    : GameplayNotificationKind.Info,
                HoldDuration,
                DedupKey?.Trim());
        }
    }

    public static class GameplayNotificationStream
    {
        public static event Action<GameplayNotificationRequest> Published;

        internal static void Publish(GameplayNotificationRequest request)
            => Published?.Invoke(request);
    }
}
