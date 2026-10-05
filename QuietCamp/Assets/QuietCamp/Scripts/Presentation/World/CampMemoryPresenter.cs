using System;
using System.Collections;
using UnityEngine;

namespace QuietCamp.Presentation.World
{
    public sealed class CampMemoryPresenter : MonoBehaviour
    {
        Transform _world;
        Action _finished;
        Coroutine _playing;
        public bool Playing => _playing != null;
        public void Play(Transform world, Func<bool> reduced, Action finished)
        {
            Skip(); _world = world; _finished = finished;
            if (world != null && !reduced()) foreach (var story in world.GetComponentsInChildren<TentStoryVisual>()) story.ResetCare();
            if (world == null || reduced()) { Apply(); return; }
            _playing = StartCoroutine(Sequence(reduced));
        }
        IEnumerator Sequence(Func<bool> reduced)
        {
            float time = 0;
            while (time < 3.5f && !reduced()) { time += Time.unscaledDeltaTime; yield return null; }
            _playing = null; Apply();
        }
        public void Skip()
        {
            if (_playing != null) StopCoroutine(_playing);
            _playing = null; Apply();
        }
        void Apply()
        {
            if (_world != null)
            {
                foreach (var story in _world.GetComponentsInChildren<TentStoryVisual>()) story.ShowCare();
                foreach (var landmark in _world.GetComponentsInChildren<CampLandmarkVisual>()) landmark.ApplyCare();
            }
            var finished = _finished; _finished = null; finished?.Invoke();
        }
        void OnDisable() => Skip();
    }
}
