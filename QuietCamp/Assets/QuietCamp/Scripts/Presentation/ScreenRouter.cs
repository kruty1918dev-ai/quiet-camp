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
    /// </summary>
    public sealed class ScreenRouter
    {
        readonly GameServices _services;
        bool _busy;

        public ScreenRouter(GameServices services) => _services = services;

        public bool IsBusy => _busy;

        public async void GoToMenu()
            => await Transition("MainMenu", null);

        public async void GoToCamp(string levelId)
            => await Transition("Camp", levelId);

        async Task Transition(string sceneName, string levelId)
        {
            if (_busy) return;
            _busy = true;
            ISceneTransitionLease lease = _services.Transitions.TryBeginTransition();
            try
            {
                if (lease != null) await lease.CoverAsync();
                else
                {
                    await _services.Transitions.WaitForActiveTransitionAsync();
                    await _services.Transitions.CoverAsync();
                }
                _services.PendingLevelId = levelId;
                var op = SceneManager.LoadSceneAsync(sceneName);
                while (!op.isDone) await Task.Yield();
                // One frame for scene hosts to finish Awake/Start before reveal.
                await Task.Yield();
                if (lease != null) await lease.RevealAsync();
                else await _services.Transitions.RevealAsync();
            }
            finally
            {
                lease?.Dispose();
                _busy = false;
            }
        }
    }
}
