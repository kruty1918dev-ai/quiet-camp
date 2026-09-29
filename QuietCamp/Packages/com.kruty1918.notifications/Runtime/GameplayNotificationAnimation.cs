using UnityEngine;

namespace Kruty1918.Notifications.Runtime
{
    public static class GameplayNotificationAnimation
    {
        public static GameplayNotificationVisualState Evaluate(
            GameplayNotificationSettings settings,
            float elapsed,
            float holdDuration)
        {
            settings ??= new GameplayNotificationSettings();
            settings.Normalize();

            float clampedHold = Mathf.Max(0.01f, holdDuration);
            float enterEnd = settings.EnterDuration;
            float holdEnd = enterEnd + clampedHold;
            float exitEnd = holdEnd + settings.ExitDuration;
            float t = Mathf.Max(0f, elapsed);

            if (t <= 0f)
            {
                return new GameplayNotificationVisualState(
                    true,
                    0f,
                    settings.HiddenOffsetY);
            }

            if (t < enterEnd)
            {
                float p = t / settings.EnterDuration;
                return new GameplayNotificationVisualState(
                    true,
                    p,
                    Mathf.Lerp(settings.HiddenOffsetY, settings.VisibleOffsetY, p));
            }

            if (t < holdEnd)
            {
                return new GameplayNotificationVisualState(
                    true,
                    1f,
                    settings.VisibleOffsetY);
            }

            if (t < exitEnd)
            {
                float p = (t - holdEnd) / settings.ExitDuration;
                return new GameplayNotificationVisualState(
                    true,
                    1f - p,
                    Mathf.Lerp(settings.VisibleOffsetY, settings.HiddenOffsetY, p));
            }

            return new GameplayNotificationVisualState(
                false,
                0f,
                settings.HiddenOffsetY);
        }
    }
}
