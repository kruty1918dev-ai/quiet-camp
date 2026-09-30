using System.Threading.Tasks;
using Kruty1918.Notifications.API;
using UnityEngine;
using UnityEngine.SceneManagement;
namespace QuietCamp.Presentation
{
    /// <summary>
    /// Cover -> load -> reveal scene transitions through the foliage dive:
    /// the camera sinks into near leaves, an opaque forest cover hides the
    /// scene swap and the new clearing is revealed. Input is blocked for the
    /// whole operation by the transition overlay; repeated taps are ignored
    /// and the same gentle dive serves every direction.
    /// </summary>
    public sealed class ScreenRouter
    {
        readonly GameServices _services;
        FoliageDiveTransition _dive;
        bool _busy;

        public ScreenRouter(GameServices services) => _services = services;

        public bool IsBusy => _busy;
        public FoliageDiveTransition Dive
            => _dive != null ? _dive : _dive = FoliageDiveTransition.Ensure(_services);

        public async void GoToMenu()
            => await Transition("MainMenu", null);

        public async void GoToCamp(string levelId)
            => await Transition("Camp", levelId);

        public async void GoToNextCamp(string levelId)
            => await Transition("Camp", levelId);

        async Task Transition(string sceneName, string levelId)
        {
            if (_busy) return;
            _busy = true;
            var dive = Dive;
            try
            {
                await dive.CoverAsync();
                _services.PendingLevelId = levelId;
                var op = SceneManager.LoadSceneAsync(sceneName);
                var wait = 0f;
                while (!op.isDone)
                {
                    await Task.Yield();
                    wait += Time.unscaledDeltaTime;
                    if (wait > 1.5f) dive.ShowSlowHint();
                    if (wait > 15f)
                    {
                        // Unity can't cancel a load — recover the screen, tell
                        // the player and let them retry instead of hanging.
                        Debug.LogError($"[QuietCamp] Scene load '{sceneName}' exceeded 15 s.");
                        _services.Notifications?.Show(
                            _services.Localization.T("transition.failed"),
                            GameplayNotificationKind.Error);
                        dive.Recover();
                        return;
                    }
                }
                // Scene host Start + atmosphere apply + one rendered frame.
                await Task.Yield();
                await Task.Yield();
                dive.BeginReveal();
                await dive.RevealAsync();
            }
            catch (System.Exception e)
            {
                Debug.LogError("[QuietCamp] Transition failed: " + e.Message);
                dive.Recover();
            }
            finally
            {
                _busy = false;
            }
        }
    }
}
