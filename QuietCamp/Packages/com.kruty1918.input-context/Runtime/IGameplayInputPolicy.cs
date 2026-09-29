using System;
using UnityEngine;

namespace Kruty1918.InputRouting.API
{
    [Flags]
    public enum GameplayInputKind
    {
        None = 0,
        PrimaryPointer = 1 << 0,
        SecondaryPointer = 1 << 1,
        PointerPan = 1 << 2,
        PointerRotate = 1 << 3,
        PointerZoom = 1 << 4,
        Placement = 1 << 5,
        KeyboardNavigation = 1 << 6,
        AllPointer = PrimaryPointer | SecondaryPointer | PointerPan | PointerRotate | PointerZoom | Placement,
        All = AllPointer | KeyboardNavigation,
    }

    public interface IGameplayInputPolicy
    {
        bool CanProcess(GameplayInputKind inputKind, Vector2 screenPosition, int pointerId = -1);
        bool IsPointerOverUi(Vector2 screenPosition, int pointerId = -1, bool interactiveOnly = true);
        bool TryBeginPointerCapture(GameplayInputKind inputKind, Vector2 screenPosition, int pointerId = -1);
        void EndPointerCapture(GameplayInputKind inputKind, int pointerId = -1);
        IDisposable AcquireBlock(GameplayInputKind inputMask, object owner);
    }
}
