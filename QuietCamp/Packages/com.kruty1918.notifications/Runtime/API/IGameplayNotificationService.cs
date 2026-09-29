namespace Kruty1918.Notifications.API
{
    public interface IGameplayNotificationService
    {
        void Show(
            string message,
            GameplayNotificationKind kind = GameplayNotificationKind.Info,
            float? holdDuration = null,
            string dedupKey = null);

        void Show(GameplayNotificationRequest request);

        void Clear();
    }
}
