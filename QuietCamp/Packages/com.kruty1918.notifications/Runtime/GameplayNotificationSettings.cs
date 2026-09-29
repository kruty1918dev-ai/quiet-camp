using System;
using UnityEngine;

namespace Kruty1918.Notifications.Runtime
{
    [Serializable]
    public sealed class GameplayNotificationSettings
    {
        public const float DefaultEnterDuration = 0.25f;
        public const float DefaultHoldDuration = 1.8f;
        public const float DefaultExitDuration = 0.22f;
        public const float DefaultHiddenOffsetY = 70f;
        public const float DefaultVisibleOffsetY = -70f;
        public const int DefaultMaxQueueSize = 8;

        [Min(0.01f)] public float EnterDuration = DefaultEnterDuration;
        [Min(0.01f)] public float HoldDuration = DefaultHoldDuration;
        [Min(0.01f)] public float ExitDuration = DefaultExitDuration;
        public float HiddenOffsetY = DefaultHiddenOffsetY;
        public float VisibleOffsetY = DefaultVisibleOffsetY;
        [Min(1)] public int MaxQueueSize = DefaultMaxQueueSize;

        public float ResolveHoldDuration(float? overrideDuration)
            => Mathf.Max(0.01f, overrideDuration ?? HoldDuration);

        public void Normalize()
        {
            EnterDuration = Mathf.Max(0.01f, EnterDuration);
            HoldDuration = Mathf.Max(0.01f, HoldDuration);
            ExitDuration = Mathf.Max(0.01f, ExitDuration);
            MaxQueueSize = Mathf.Max(1, MaxQueueSize);
        }
    }
}
