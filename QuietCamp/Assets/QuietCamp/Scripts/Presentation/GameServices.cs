using System;
using Kruty1918.Audio;
using Kruty1918.InputRouting.API;
using Kruty1918.Localization;
using Kruty1918.Notifications.API;
using Kruty1918.SaveSystem;
using Kruty1918.UIActions.API;
using Kruty1918.UIActions.Runtime;
using Kruty1918.UiFoundation;
using QuietCamp.Application;
using QuietCamp.Infrastructure;
namespace QuietCamp.Presentation
{
    /// <summary>
    /// Composition-root container: services created once by QuietCampBootstrap
    /// and passed down to scene hosts by constructor. Never searched for.
    /// </summary>
    public sealed class GameServices : IDisposable
    {
        public SaveAdapter Save { get; }
        public SettingsSaveData Settings => Save.Settings;
        public ILocalizationService Localization { get; }
        public AudioService Audio { get; }
        public QuietCampAudioCatalog AudioCatalog { get; }
        public AssetCatalog Assets { get; }
        public IGameplayInputPolicy InputPolicy { get; }
        public IUiContextStack ContextStack { get; }
        public IUiActionRouter Actions { get; }
        public QcActionHandler Dispatch { get; }
        public UiActionJournal Journal { get; }
        public UiEscapeRouter Escape { get; }
        public IUiMotionService Motion { get; }
        public IGameplayNotificationService Notifications { get; }
        public ISceneTransitionService Transitions { get; }
        public ProgressionService Progression { get; }
        public UiHotkeyService HotkeyService { get; }

        /// <summary>Level chosen in the menu, consumed by the Camp scene host.</summary>
        public string PendingLevelId { get; set; }

        public bool ReducedMotion
        {
            get => Settings.reducedMotion;
            set { Settings.reducedMotion = value; }
        }

        public GameServices(
            SaveAdapter save,
            ILocalizationService localization,
            QuietCampAudioCatalog audioCatalog,
            AudioService audio,
            AssetCatalog assets,
            IGameplayInputPolicy inputPolicy,
            IUiContextStack contextStack,
            IUiActionRouter actions,
            QcActionHandler dispatch,
            UiHotkeyService hotkeyService,
            UiActionJournal journal,
            UiEscapeRouter escape,
            IUiMotionService motion,
            IGameplayNotificationService notifications,
            ISceneTransitionService transitions,
            ProgressionService progression)
        {
            Save = save;
            Localization = localization;
            AudioCatalog = audioCatalog;
            Audio = audio;
            Assets = assets;
            InputPolicy = inputPolicy;
            ContextStack = contextStack;
            Actions = actions;
            Dispatch = dispatch;
            HotkeyService = hotkeyService;
            Journal = journal;
            Escape = escape;
            Motion = motion;
            Notifications = notifications;
            Transitions = transitions;
            Progression = progression;
        }

        public void Dispose() => Audio?.Dispose();
    }
}
