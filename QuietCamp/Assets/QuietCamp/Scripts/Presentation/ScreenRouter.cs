using System.Threading;
using System.Threading.Tasks;
using Kruty1918.UiFoundation;
using UnityEngine;
using UnityEngine.SceneManagement;
namespace QuietCamp.Presentation
{
    /// <summary>
    /// Cover -> load -> reveal scene transitions through the shared transition
    /// service lease. Input is blocked for the whole operation by the cover.
    /// Each direction uses a distinct visual style: Doors on the way back to
    /// the menu, Iris zoom-in when entering a camp, Curtain for next-level.
    /// </summary>
    public sealed class ScreenRouter
    {
        readonly GameServices _services;
        bool _busy;

        public ScreenRouter(GameServices services) => _services = services;

        public bool IsBusy => _busy;

        /// <summary>Doors close on camp, open onto the menu.</summary>
        public async void GoToMenu()
            => await Transition("MainMenu", null, SceneTransitionStyle.Doors);

        /// <summary>Iris expands over the diorama — entering the campsite.</summary>
        public async void GoToCamp(string levelId)
            => await Transition("Camp", levelId, SceneTransitionStyle.Iris);

        /// <summary>Gentle bottom-up curtain for the next level in the chain.</summary>
        public async void GoToNextCamp(string levelId)
            => await Transition("Camp", levelId, SceneTransitionStyle.Curtain);

        async Task Transition(string sceneName, string levelId, SceneTransitionStyle style)
        {
            if (_busy) return;
            _busy = true;
            ISceneTransitionLease lease = _services.Transitions.TryBeginTransition();
            try
            {
                if (lease != null) await lease.CoverAsync(style);
                else
                {
                    await _services.Transitions.WaitForActiveTransitionAsync();
                    await _services.Transitions.CoverAsync(style);
                }
                _services.PendingLevelId = levelId;
                var op = SceneManager.LoadSceneAsync(sceneName);
                while (!op.isDone) await Task.Yield();
                // One frame for scene hosts to finish Awake/Start before reveal.
                await Task.Yield();
                if (lease != null) await lease.RevealAsync(style);
                else await _services.Transitions.RevealAsync(style);
            }
            finally
            {
                lease?.Dispose();
                _busy = false;
            }
        }
    }
}
