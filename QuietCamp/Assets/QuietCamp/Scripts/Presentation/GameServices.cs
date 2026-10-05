using System;
using Kruty1918.Audio;
using Kruty1918.Haptics;
using Kruty1918.InputRouting.API;
using Kruty1918.Localization;
using Kruty1918.Notifications.API;
using Kruty1918.SaveSystem;
using Kruty1918.UIActions.API;
using Kruty1918.UIActions.Runtime;
using Kruty1918.UiFoundation;
using QuietCamp.Application;
using QuietCamp.Infrastructure;
using QuietCamp.Domain;
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
        public HapticsService Haptics { get; }
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
        public TutorialDirector Tutorial { get; private set; }
        public UiHotkeyService HotkeyService { get; }
        /// <summary>Monetization boundary — offline stub until a real SDK adapter lands.</summary>
        public IAdService Ads { get; }
        public ComfortAnalytics Analytics { get; }
        public CozyRewardService Rewards { get; }
        public BonusCampAccessService BonusCamps { get; }
        public CampEconomy Economy { get; private set; }
        public AdLifeService AdLives { get; private set; }
        public JourneyCatalog Journeys { get; private set; }
        public JourneyAccessService JourneyAccess { get; private set; }
        public PurchaseService Purchases { get; private set; }
        public CampMemoryService Memories { get; private set; }
        public CampCompletionService Completion { get; private set; }
        public CampAttemptService Attempts { get; private set; }
        public bool MonetizationBusy => Purchases.Busy || AdLives.Busy || Rewards.Busy;
        public event Action MonetizationChanged;
        void OnMonetizationChanged() => MonetizationChanged?.Invoke();
        /// <summary>Optional verified entitlement adapter; no provider means no premium entitlement.</summary>
        public Func<string,bool> BonusEntitlement { get; set; }
        public GoogleServicesConfiguration GoogleConfiguration { get; }
        public LegalConfiguration Legal { get; }
        public bool PrivacyNoticePresented;

        /// <summary>Level chosen in the menu, consumed by the Camp scene host.</summary>
        public string PendingLevelId { get; set; }
        public float LevelMapScroll = -1f;
        public int AlbumIndex;
        /// <summary>Chosen once per application launch; returning from gameplay keeps the same backdrop.</summary>
        public LevelData MenuBackdrop { get; internal set; }
        public int EffectiveQuality = UnityEngine.Mathf.Clamp(UnityEngine.QualitySettings.GetQualityLevel(),0,2);


        /// <summary>Menu screen to land on after a navigation — e.g.
        /// "DemoComplete" after the final level. Consumed by MenuSceneHost.</summary>
        public string PendingMenuScreen { get; set; }

        /// <summary>
        /// The single persistent wind bed owned by QuietCampBootstrap. Scene
        /// hosts may scale its playback per phase weight but never restart it.
        /// </summary>
        public AudioHandle AmbientWindHandle { get; set; }
        public AudioHandle MusicBedHandle { get; set; }

        public bool ReducedMotion
        {
            get => Settings.reducedMotion;
            set { Settings.reducedMotion = value; }
        }

        /// <summary>Calm pace: tween durations stretch for a gentler feel.</summary>
        public bool CalmMode
        {
            get => Settings.calmMode;
            set { Settings.calmMode = value; }
        }

        /// <summary>Tween duration multiplier — 1.6 in calm mode, else 1.</summary>
        public float MotionScale => Settings.calmMode ? 1.6f : 1f;

        public bool HapticsEnabled
        {
            get => Settings.haptics;
            set { Settings.haptics = value; Haptics.Enabled = value; }
        }

        public void PlayHaptic(HapticCue cue)
        {
            Haptics.Enabled = Settings.haptics;
            Haptics.Play(cue);
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
            ProgressionService progression,
            IAdService ads = null,
            HapticsService haptics = null,
            IPurchaseProvider purchaseProvider = null)
        {
            Save = save;
            Localization = localization;
            AudioCatalog = audioCatalog;
            Audio = audio;
            Haptics = haptics ?? new HapticsService();
            Haptics.Enabled = save.Settings.haptics;
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
            Progression = progression ?? new ProgressionService();
            if (progression == null) Progression.Restore(save.Progress.completedIds, save.Progress.lastLevelId, save.Progress.cosmeticFlags);
            Legal = LegalConfiguration.Load();
            GoogleConfiguration = GoogleServicesConfiguration.Load(Legal);
            if (save.Settings.privacyAcknowledgementDraft || save.Settings.privacyAcknowledgementRevision != Legal.revision
                || string.IsNullOrEmpty(save.Settings.privacyAcknowledgementHash))
            { GoogleConfiguration.analyticsEnabled = false; GoogleConfiguration.rewardedAdsEnabled = false; }
            Ads = ads ?? OptionalGoogleServices.Ads(GoogleConfiguration);
            // Document acknowledgement never grants consent. Changed policy words also invalidate old optional choices.
            var consentPublication = Legal.Published ? Legal.revision + ":" + save.Settings.privacyAcknowledgementHash : Legal.revision;
            Analytics = new ComfortAnalytics(save.Settings, OptionalGoogleServices.Analytics(GoogleConfiguration), () => save.Save(), consentPublication);
            Rewards = new CozyRewardService(Ads, save.Settings, Progression, save.Save);
            BonusCamps = new BonusCampAccessService(Progression,LevelLoader.MvpLevelIds(),BonusCampCatalog.IsPublished,
                id=>BonusEntitlement?.Invoke(id)??false);
            Tutorial = CreateTutorial();
            ConfigureMonetization(purchaseProvider);
            save.BeforeSave += SyncProgress;
            Tutorial.RetryReward();
            Rewards.EarnFromPlay();
        }

        void ConfigureMonetization(IPurchaseProvider provider = null)
        {
            if (Economy != null) Economy.Changed -= OnMonetizationChanged;
            if (AdLives != null) AdLives.Changed -= OnMonetizationChanged;
            if (Purchases != null) Purchases.Changed -= OnMonetizationChanged;
            var config = MonetizationConfiguration.Load();
            Economy = new CampEconomy(Save.Economy, config.economy, Save.Save);
            Journeys = config.Catalog();
            JourneyAccess = new JourneyAccessService(Journeys, Progression, Save.Entitlements, () => Economy.IsPro);
            Memories = new CampMemoryService(Save.Memories);
            Completion = new CampCompletionService(Save, Progression, Memories, Journeys);
            Attempts = new CampAttemptService(Save, Economy, Completion);
            AdLives = new AdLifeService(Ads, Economy, () => Settings.optionalVideoBonuses && !Rewards.Busy && !Purchases.Busy);
            Purchases = new PurchaseService(provider ?? new UnavailablePurchaseProvider(), config.products, Save.Purchases, GrantProduct, Save.Save);
            Economy.Changed += OnMonetizationChanged; AdLives.Changed += OnMonetizationChanged; Purchases.Changed += OnMonetizationChanged;
        }
        EconomyResult GrantProduct(string receipt, PurchaseProduct product)
        {
            if (product.currencyAmount > 0) return Economy.CreditVerified(receipt, product.currencyAmount);
            if (product.pro) return Economy.GrantPro(receipt);
            if (string.IsNullOrEmpty(product.entitlementId)) return EconomyResult.Invalid;
            if (Save.Entitlements.Has(product.entitlementId)) return EconomyResult.AlreadyApplied;
            var before = Save.Entitlements.ownedIds;
            Save.Entitlements.ownedIds = new System.Collections.Generic.List<string>(before ?? Array.Empty<string>()) { product.entitlementId }.ToArray();
            if (Save.Save()) return EconomyResult.Applied;
            Save.Entitlements.ownedIds = before; return EconomyResult.SaveFailed;
        }
        public bool CanStart(string id)
        {
            id = CampContent.CanonicalId(id);
#if UNITY_EDITOR
            if (id == LevelLoader.TestLevelId()) return true;
#endif
            var bonus = BonusCampCatalog.ForLevel(id);
            return bonus != null ? BonusCampCatalog.IsPublished(bonus) && (Economy.IsPro || BonusCamps.Evaluate(bonus).CanPlay) : JourneyAccess.Evaluate(id).CanStart;
        }
        public string ContinueLevel()
        {
            var saved = CampContent.CanonicalId(Save.Session?.levelId);
            return !string.IsNullOrEmpty(saved) && CanStart(saved) ? saved : JourneyAccess.ContinueTarget("main");
        }
        public EconomyResult BuyJourney(string id)
        {
            var journey = Journeys.Find(id);
            if (journey == null || !journey.published || string.IsNullOrEmpty(journey.entitlementId)) return EconomyResult.Unavailable;
            if (Economy.IsPro || Save.Entitlements.Has(journey.entitlementId)) return EconomyResult.AlreadyApplied;
            var before = Save.Entitlements.ownedIds;
            Save.Entitlements.ownedIds = new System.Collections.Generic.List<string>(before ?? Array.Empty<string>()) { journey.entitlementId }.ToArray();
            var result = Economy.SpendCurrency(journey.currencyCost);
            if (result != EconomyResult.Applied) Save.Entitlements.ownedIds = before;
            return result;
        }

        void SyncProgress()
        {
            var ids = new string[Progression.CompletedCount];
            int i = 0; foreach (var id in Progression.CompletedIds) ids[i++] = id;
            if (Save.Progress == null) Save.Progress = new ProgressSaveData();
            Save.Progress.completedIds = ids;
            Save.Progress.lastLevelId = Progression.LastLevelId;
            Save.Progress.cosmeticFlags = Progression.CosmeticFlags;
            Save.Progress.tutorial = Tutorial?.Save;
        }

        TutorialDirector CreateTutorial()
        {
            if (Save.Progress == null) Save.Progress = new ProgressSaveData();
            var existing = Progression.CompletedCount > 0 || !string.IsNullOrEmpty(Progression.LastLevelId)
                || !string.IsNullOrEmpty(Save.Session?.levelId) || (Save.Album.entries?.Length ?? 0) > 0;
            var tutorial = new TutorialDirector(Save.Progress.tutorial, existing, Progression, Save.Save);
            Save.Progress.tutorial = tutorial.Save;
            return tutorial;
        }

        /// <summary>Main-menu operation, separate from the cosmetic-preserving progress reset.
        /// Keep the settings instance: analytics, rewards and motion hold references to it.</summary>
        public async System.Threading.Tasks.Task<bool> EraseLocalGameData()
        {
            if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().name != "MainMenu" || MonetizationBusy)
                return false;
            await Analytics.ClearLocalDataAndWithdraw();
            if (!SaveAdapter.TryEraseLocalFiles(UnityEngine.Application.persistentDataPath, out _)) return false;
            Save.ResetAfterErase();
            Progression.Restore(null, null, 0);
            Save.Progress = new ProgressSaveData();
            Save.Session = new SessionSaveData();
            Save.Album = new AlbumSaveData();
            Save.Economy = new EconomySaveData(); Save.Entitlements = new EntitlementSaveData();
            Save.Purchases = new PurchaseSaveData(); Save.Memories = new MemorySaveData();
            ConfigureMonetization();
            Tutorial = CreateTutorial();
            UnityEngine.JsonUtility.FromJsonOverwrite(UnityEngine.JsonUtility.ToJson(new SettingsSaveData()), Settings);
            PendingLevelId = null; PendingMenuScreen = null; LevelMapScroll = -1; AlbumIndex = 0;
            PrivacyNoticePresented = false;
            Localization.TrySetLanguage(Settings.language);
            UI.LocalizedLabel.TextScale = Settings.textScale;
            ScreenOrientationPolicy.Apply(Settings.orientation);
            Haptics.Enabled = Settings.haptics;
            Audio?.SetBusVolume(Kruty1918.Audio.AudioBus.Master, Settings.master);
            Audio?.SetBusVolume(Kruty1918.Audio.AudioBus.Music, Settings.music);
            Audio?.SetBusVolume(Kruty1918.Audio.AudioBus.Ambience, Settings.ambience);
            Audio?.SetBusVolume(Kruty1918.Audio.AudioBus.Ui, Settings.effects);
            Audio?.SetBusVolume(Kruty1918.Audio.AudioBus.Sfx, Settings.effects);
            return true;
        }

        public void Dispose()
        {
            Save.BeforeSave -= SyncProgress;
            Analytics.Dispose();
            (Ads as IDisposable)?.Dispose();
            Haptics.Dispose();
            Audio?.Dispose();
        }
    }
}
