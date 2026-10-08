using System.Threading.Tasks;
using Kruty1918.Notifications.API;
using QuietCamp.Infrastructure;
using QuietCamp.Presentation.World;
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
    ///
    /// Readiness is an explicit contract, not a fixed number of yields:
    /// the scene host must report UiReady (world built, HUD laid out, camera
    /// fitted, phase applied) and at least one frame must render under the
    /// cover before Reveal begins. A pending load is serialized — a timed-out
    /// operation is still awaited so it cannot swap the scene underneath a
    /// later navigation.
    /// </summary>
    public sealed class ScreenRouter
    {
        readonly GameServices _services;
        FoliageDiveTransition _dive;
        AtmosphereCatalog _atmosphere;
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
            if (_busy || _services.MonetizationBusy) return;
            if (sceneName == "Camp" && !_services.CanStart(levelId))
            {
                _services.Notifications?.Show(_services.Localization.T("journey.access.denied"), GameplayNotificationKind.Info);
                return;
            }
            _busy = true;
            var dive = Dive;
            var cfg = dive.Config;
            try
            {
                dive.SetTargetPhase(ResolvePhase(sceneName, levelId));
                await dive.CoverAsync();
                // CoverAsync exits early after an external Recover — the
                // screen already shows a usable state, don't load on top.
                if (dive.Current != FoliageDiveTransition.State.CoveredLoading) return;
                _services.PendingLevelId = levelId;
                var op = SceneManager.LoadSceneAsync(sceneName);
                float wait = 0f;
                bool recovered = false;
                while (!op.isDone)
                {
                    await Task.Yield();
                    wait += Time.unscaledDeltaTime;
                    if (wait > cfg.SlowHintDelay) dive.ShowSlowHint();
                    if (wait > cfg.RecoveryThreshold && !recovered)
                    {
                        // Unity can't cancel a scene load: the overlay is
                        // recovered for the player, but this loop keeps
                        // _busy held until the op physically finishes so a
                        // retry can't collide with the late activation.
                        recovered = true;
                        Debug.LogError($"[QuietCamp] Scene load '{sceneName}' exceeded {cfg.RecoveryThreshold:0}s.");
                        _services.Notifications?.Show(
                            _services.Localization.T("transition.failed"),
                            GameplayNotificationKind.Error);
                        dive.Recover();
                    }
                }
                if (recovered || dive.Current == FoliageDiveTransition.State.Recovery)
                    return; // whatever eventually loaded now stands on its own
                if (!await WaitSceneReady(sceneName, dive))
                {
                    Debug.LogError($"[QuietCamp] Scene '{sceneName}' host never reported ready.");
                    _services.Notifications?.Show(
                        _services.Localization.T("transition.failed"),
                        GameplayNotificationKind.Error);
                    dive.Recover();
                    return;
                }
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

        /// <summary>Host-composed check: the target scene's host must finish
        /// Start (level loaded, world built, HUD applied, camera fitted,
        /// phase applied), then at least one rendered frame must pass while
        /// the cover is opaque. Bounded — a wedged host recovers instead of
        /// hanging under the cover.</summary>
        async Task<bool> WaitSceneReady(string sceneName, FoliageDiveTransition dive)
        {
            float wait = 0f;
            while (wait < 10f)
            {
                // Recover() finishes into Idle — bail instead of waiting out
                // the host-ready window for a scene nobody will reveal.
                if (dive.Current == FoliageDiveTransition.State.Idle) return false;
                bool ready = sceneName == "Camp"
                    ? CampSceneHost.Current != null && CampSceneHost.Current.UiReady
                    : MenuSceneHost.Current != null && MenuSceneHost.Current.UiReady;
                if (ready)
                {
                    int frame = Time.renderedFrameCount;
                    float guard = 0f;
                    while (Time.renderedFrameCount == frame && guard < .5f)
                    {
                        guard += Time.unscaledDeltaTime;
                        await Task.Yield();
                    }
                    return true;
                }
                wait += Time.unscaledDeltaTime;
                await Task.Yield();
            }
            return false;
        }

        /// <summary>Phase of the scene being dived into — the palette swaps
        /// under the opaque cover so the reveal already shows the new time.</summary>
        string ResolvePhase(string sceneName, string levelId)
        {
            if (sceneName != "Camp" || levelId == null) return null;
            try
            {
                if (_atmosphere == null) _atmosphere = AtmosphereCatalog.Load();
                return _atmosphere.Resolve(levelId, null).Id;
            }
            catch { return null; }
        }
    }
}
