namespace Kruty1918.Notifications.Runtime
{
    public readonly struct GameplayNotificationVisualState
    {
        public GameplayNotificationVisualState(
            bool isActive,
            float alpha,
            float anchoredPositionY)
        {
            IsActive = isActive;
            Alpha = alpha;
            AnchoredPositionY = anchoredPositionY;
        }

        public bool IsActive { get; }
        public float Alpha { get; }
        public float AnchoredPositionY { get; }
    }
}
