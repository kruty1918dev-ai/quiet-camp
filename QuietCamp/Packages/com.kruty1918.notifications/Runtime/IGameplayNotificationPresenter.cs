using System;
using Kruty1918.Notifications.API;

namespace Kruty1918.Notifications.Runtime
{
    public interface IGameplayNotificationPresenter
    {
        void Present(
            GameplayNotificationRequest request,
            float holdDuration,
            Action completed);

        void ResetPresentation();
    }
}
