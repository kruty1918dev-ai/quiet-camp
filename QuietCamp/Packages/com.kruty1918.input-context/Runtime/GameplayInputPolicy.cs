using System;
using System.Collections.Generic;
using Kruty1918.InputRouting.API;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Kruty1918.InputRouting.Runtime
{
    public sealed class GameplayInputPolicy : IGameplayInputPolicy
    {
        private readonly EventSystem _eventSystemOverride;
        private readonly Dictionary<int, PointerRaycastCache> _raycastCaches = new();
        private readonly Dictionary<PointerCaptureKey, bool> _pointerCaptures = new();
        private readonly Dictionary<int, GameplayInputKind> _globalBlocks = new();

        private int _nextBlockId = 1;
        private GameplayInputKind _globalBlockMask;

        public GameplayInputPolicy(
            EventSystem eventSystem = null)
        {
            _eventSystemOverride = eventSystem;
        }

        public bool CanProcess(GameplayInputKind inputKind, Vector2 screenPosition, int pointerId = -1)
        {
            if (inputKind == GameplayInputKind.None)
                return true;

            if ((_globalBlockMask & inputKind) != 0)
                return false;

            if ((inputKind & GameplayInputKind.KeyboardNavigation) != 0)
                return !IsTextInputFocused();

            var captureKey = new PointerCaptureKey(pointerId, inputKind);
            if (_pointerCaptures.TryGetValue(captureKey, out bool captureAllowsInput))
                return captureAllowsInput;

            return !IsPointerBlocked(inputKind, screenPosition, pointerId);
        }

        public bool IsPointerOverUi(Vector2 screenPosition, int pointerId = -1, bool interactiveOnly = true)
        {
            List<RaycastResult> results = Raycast(screenPosition, pointerId);
            for (int index = 0; index < results.Count; index++)
            {
                GameObject hitObject = results[index].gameObject;
                if (hitObject == null)
                    continue;

                // interactiveOnly=false answers "is any UI under the pointer";
                // true restricts to regions that actually consume input.
                if (!interactiveOnly || IsBlockingUiHit(hitObject))
                    return true;
            }

            return false;
        }

        public bool TryBeginPointerCapture(
            GameplayInputKind inputKind,
            Vector2 screenPosition,
            int pointerId = -1)
        {
            var key = new PointerCaptureKey(pointerId, inputKind);
            bool canProcess = CanProcess(inputKind, screenPosition, pointerId);
            _pointerCaptures[key] = canProcess;
            return canProcess;
        }

        public void EndPointerCapture(GameplayInputKind inputKind, int pointerId = -1)
        {
            _pointerCaptures.Remove(new PointerCaptureKey(pointerId, inputKind));
        }

        public IDisposable AcquireBlock(GameplayInputKind inputMask, object owner)
        {
            if (inputMask == GameplayInputKind.None)
                return EmptyDisposable.Instance;

            int id = _nextBlockId++;
            _globalBlocks[id] = inputMask;
            RebuildGlobalBlockMask();
            return new BlockLease(this, id, owner);
        }

        private bool IsPointerBlocked(
            GameplayInputKind inputKind,
            Vector2 screenPosition,
            int pointerId)
        {
            List<RaycastResult> results = Raycast(screenPosition, pointerId);
            for (int index = 0; index < results.Count; index++)
            {
                GameObject hitObject = results[index].gameObject;
                if (hitObject == null)
                    continue;

                if (IsBlockingUiHit(hitObject, inputKind))
                    return true;
            }

            return false;
        }

        private static bool IsBlockingUiHit(GameObject hitObject)
        {
            if (hitObject == null)
                return false;

            return hitObject.GetComponentInParent<GameplayInputBlocker>() != null
                   || hitObject.GetComponentInParent<Selectable>() != null
                   || hitObject.GetComponentInParent<ScrollRect>() != null
                   || hitObject.GetComponentInParent<TMP_InputField>() != null;
        }

        private static bool IsBlockingUiHit(GameObject hitObject, GameplayInputKind inputKind)
        {
            if (hitObject == null)
                return false;

            GameplayInputBlocker explicitBlocker = hitObject.GetComponentInParent<GameplayInputBlocker>();
            if (explicitBlocker != null && (explicitBlocker.BlockedInput & inputKind) != 0)
                return true;

            return hitObject.GetComponentInParent<Selectable>() != null
                   || hitObject.GetComponentInParent<ScrollRect>() != null
                   || hitObject.GetComponentInParent<TMP_InputField>() != null;
        }

        private List<RaycastResult> Raycast(Vector2 screenPosition, int pointerId)
        {
            if (!_raycastCaches.TryGetValue(pointerId, out PointerRaycastCache cache))
            {
                cache = new PointerRaycastCache();
                _raycastCaches.Add(pointerId, cache);
            }

            int frame = Time.frameCount;
            if (cache.Frame == frame && cache.ScreenPosition == screenPosition)
                return cache.Results;

            cache.Frame = frame;
            cache.ScreenPosition = screenPosition;
            cache.Results.Clear();

            EventSystem eventSystem = ResolveEventSystem();
            if (eventSystem == null)
                return cache.Results;

            cache.EventData ??= new PointerEventData(eventSystem);
            cache.EventData.Reset();
            cache.EventData.pointerId = pointerId;
            cache.EventData.position = screenPosition;
            eventSystem.RaycastAll(cache.EventData, cache.Results);
            return cache.Results;
        }

        private bool IsTextInputFocused()
        {
            GameObject selected = ResolveEventSystem()?.currentSelectedGameObject;
            return selected != null && selected.GetComponentInParent<TMP_InputField>() != null;
        }

        private EventSystem ResolveEventSystem()
            => _eventSystemOverride != null
                ? _eventSystemOverride
                : EventSystem.current;

        private void ReleaseBlock(int id)
        {
            if (_globalBlocks.Remove(id))
                RebuildGlobalBlockMask();
        }

        private void RebuildGlobalBlockMask()
        {
            _globalBlockMask = GameplayInputKind.None;
            foreach (GameplayInputKind mask in _globalBlocks.Values)
                _globalBlockMask |= mask;
        }

        private sealed class PointerRaycastCache
        {
            public int Frame = -1;
            public Vector2 ScreenPosition;
            public PointerEventData EventData;
            public readonly List<RaycastResult> Results = new(8);
        }

        private readonly struct PointerCaptureKey : IEquatable<PointerCaptureKey>
        {
            private readonly int _pointerId;
            private readonly GameplayInputKind _inputKind;

            public PointerCaptureKey(int pointerId, GameplayInputKind inputKind)
            {
                _pointerId = pointerId;
                _inputKind = inputKind;
            }

            public bool Equals(PointerCaptureKey other)
                => _pointerId == other._pointerId && _inputKind == other._inputKind;

            public override bool Equals(object obj)
                => obj is PointerCaptureKey other && Equals(other);

            public override int GetHashCode()
                => HashCode.Combine(_pointerId, (int)_inputKind);
        }

        private sealed class BlockLease : IDisposable
        {
            private GameplayInputPolicy _owner;
            private readonly int _id;
            private readonly object _requestOwner;

            public BlockLease(GameplayInputPolicy owner, int id, object requestOwner)
            {
                _owner = owner;
                _id = id;
                _requestOwner = requestOwner;
            }

            public void Dispose()
            {
                GameplayInputPolicy owner = _owner;
                _owner = null;
                owner?.ReleaseBlock(_id);
                GC.KeepAlive(_requestOwner);
            }
        }

        private sealed class EmptyDisposable : IDisposable
        {
            public static readonly EmptyDisposable Instance = new();
            public void Dispose() { }
        }
    }
}
