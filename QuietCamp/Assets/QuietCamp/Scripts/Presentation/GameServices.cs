using System;
using System.Collections.Generic;
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
        public CrashDiagnostics Crashes { get; }
        public CozyRewardService Rewards { get; }
        public BonusCampAccessService BonusCamps { get; }
        public CampEconomy Economy { get; private set; }
        public AdLifeService AdLives { get; private set; }
        public AdHintService AdHints { get; private set; }
        public JourneyCatalog Journeys { get; private set; }
        public JourneyAccessService JourneyAccess { get; private set; }
        public PurchaseService Purchases { get; private set; }
        public ComplimentaryAccessService ComplimentaryAccess { get; private set; }
        public InterstitialService Interstitials { get; private set; }
        public OwnerClientConfiguration OwnerConfiguration { get; private set; }
        public OperationalMetrics OperationalMetrics { get; private set; }
        DateTime _nextPurchaseRefresh;
        public void RefreshPurchasesIfDue()
        {
            if (OwnerConfiguration == null || MonetizationBusy || DateTime.UtcNow < _nextPurchaseRefresh) return;
            _nextPurchaseRefresh = DateTime.UtcNow.AddMinutes(15);
            if (OwnerConfiguration.BillingConfigured(Legal)) _ = Purchases.Restore();
            _ = ComplimentaryAccess.Refresh();
        }
        public CampMemoryService Memories { get; private set; }
        public CampCompletionService Completion { get; private set; }
        public CampAttemptService Attempts { get; private set; }
        public bool MonetizationBusy => (ComplimentaryAccess?.Busy ?? false) || (Interstitials?.Busy ?? false) || Purchases.Busy || AdLives.Busy || AdHints.Busy || Rewards.Busy;
        float _economyTickSeconds;
        int _recoveryMinute = -1;
        public void TickEconomy(float seconds)
        {
            if (Economy == null || seconds < 0 || float.IsNaN(seconds) || float.IsInfinity(seconds)) return;
            _economyTickSeconds += seconds;
            if (_economyTickSeconds < 1) return;
            _economyTickSeconds = 0;
            Interstitials = new InterstitialService(Ads,
                () => !Economy.IsPro && !Purchases.Busy && !AdLives.Busy && !AdHints.Busy && !Rewards.Busy && GoogleConfiguration.interstitialsEnabled,
                Progression.CompletedCount, OwnerConfiguration.firstInterstitialAfterCompletedLevels,
                OwnerConfiguration.completedLevelsBetweenInterstitials, OwnerConfiguration.minimumInterstitialIntervalSeconds,
                OwnerConfiguration.interstitialCooldownAfterRewardSeconds, OwnerConfiguration.maximumInterstitialsPerSession);
            GoogleConfiguration.RewardEarned = placement => {
                if (placement == "camp.life" || placement == "camp.hint") Economy.RememberEarnedAdReward(placement == "camp.hint");
                Interstitials.Rewarded();
            };
            GoogleConfiguration.AdObserved = (placement, stage) => Analytics.AdEvent(stage,
                placement == "camp.life" ? 0 : placement == "camp.hint" ? 1 : placement == "camp.interstitial" ? 3 : 2);
            Purchases.StateObserved += state => Analytics.PurchaseEvent(state == PurchaseState.Owned ? 1 : 0);
            Economy.RefreshLives();
            _ = Purchases.Restore();
            _ = ComplimentaryAccess.Refresh();
            int minute = (int)Math.Ceiling(Economy.SecondsUntilNextLife / 60d);
            if (minute != _recoveryMinute) { _recoveryMinute = minute; OnMonetizationChanged(); }
        }
        #if UNITY_EDITOR
        public readonly Dictionary<string,int> RoadmapSeenFrontiers=new Dictionary<string,int>(StringComparer.Ordinal);
        #endif
        public event Action MonetizationChanged;
        void OnMonetizationChanged() { GoogleConfiguration?.Permissions.Refresh(); MonetizationChanged?.Invoke(); }
        /// <summary>Optional verified entitlement adapter; no provider means no premium entitlement.</summary>
        public Func<string,bool> BonusEntitlement { get; set; }
        #if UNITY_EDITOR
        public IRoadmapBranchUnlockProvider BranchUnlockProvider { get; set; }
        #endif
        public Func<string,bool> SubscriptionEntitlement { get; set; }
        public GoogleServicesConfiguration GoogleConfiguration { get; }
        public LegalConfiguration Legal { get; }
        readonly PrivacyConsentPersistence _privacyPersistence;
        public bool PrivacyNoticePresented;

        /// <summary>Level chosen in the menu, consumed by the Camp scene host.</summary>
        public string PendingLevelId { get; set; }
        #if UNITY_EDITOR
        public float LevelMapScroll = -1f;
        public int RoadmapAdvanceFrom = -1;
        public Domain.RoadmapAnchor LevelMapAnchor;
        public string LevelMapJourney="main";
        public readonly Dictionary<string,Domain.RoadmapAnchor> LevelMapAnchors=new Dictionary<string,Domain.RoadmapAnchor>();
        #endif
        public int AlbumIndex;
        /// <summary>Chosen once per application launch; returning from gameplay keeps the same backdrop.</summary>
        public LevelData MenuBackdrop { get; internal set; }
#if UNITY_EDITOR
        public void SelectMenuCapture()
        {
            if(!EditorAuthoringPreview.CampaignPreview||!EditorAuthoringPreview.ControlledCapture||!Save.MemoryOnly)throw new InvalidOperationException("Only the controlled memory preview can select a menu backdrop.");
            MenuBackdrop=EditorAuthoringPreview.Current.level;
        }
#endif
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
            if (!save.MemoryOnly) PrivacyInstallScope.Validate(save.Settings);
            GoogleConfiguration = GoogleServicesConfiguration.Load(Legal);
            if (save.Settings.privacyAcknowledgementDraft || save.Settings.privacyAcknowledgementRevision != Legal.revision
                || string.IsNullOrEmpty(save.Settings.privacyAcknowledgementHash))
            {
                GoogleConfiguration.analyticsEnabled = false;
                GoogleConfiguration.crashlyticsEnabled = false;
                if (!GoogleConfiguration.TestAdsActive) GoogleConfiguration.rewardedAdsEnabled = false;
            }
            // Document acknowledgement never grants consent. Changed policy words also invalidate old optional choices.
            var consentPublication = Legal.Published ? Legal.revision + ":" + save.Settings.privacyAcknowledgementHash : Legal.revision;
            GoogleConfiguration.BindPrivacy(save.Settings, consentPublication);
            GoogleConfiguration.HasNoAdsEntitlement = () => Economy?.IsPro ?? false;
            GoogleConfiguration.RememberPrivacyOptions = required =>
            { Settings.googlePrivacyOptionsRequired = required; Save.Save(); };
            _privacyPersistence = new PrivacyConsentPersistence(save.Settings,
                () => save.MemoryOnly ? save.Save() : PrivacyInstallScope.Commit(save.Settings, save.Save));
            Ads = ads == null ? OptionalGoogleServices.Ads(GoogleConfiguration) : new PrivacyAdService(
                () => PrivacyEligibility.AdvertisingAllowed(save.Settings, consentPublication) && save.Settings.optionalVideoBonuses && !save.Economy.pro,
                () => PrivacyEligibility.Allowed(save.Settings), () => ads);
            Analytics = new ComfortAnalytics(save.Settings, OptionalGoogleServices.Analytics(GoogleConfiguration), () =>
            { if (!PersistPrivacy()) throw new InvalidOperationException("Privacy choice could not be saved."); }, consentPublication);
            Crashes = new CrashDiagnostics(save.Settings, OptionalGoogleServices.Crashes(GoogleConfiguration), PersistPrivacy, consentPublication);
            _privacyPersistence.Attach(Analytics, Crashes, Ads);
            Save.SaveFailed += OnSaveFailed;
            Rewards = new CozyRewardService(Ads, save.Settings, Progression, save.Save);
            Rewards.RewardObserved += OnCosmeticReward;
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
            if (AdLives != null) AdLives.RewardObserved -= OnLifeReward;
            if (AdHints != null) AdHints.RewardObserved -= OnHintReward;
            if (Economy != null) Economy.Changed -= OnMonetizationChanged;
            if (AdLives != null) AdLives.Changed -= OnMonetizationChanged;
            if (AdHints != null) AdHints.Changed -= OnMonetizationChanged;
            if (Purchases != null) Purchases.Changed -= OnMonetizationChanged;
            if (ComplimentaryAccess != null) { ComplimentaryAccess.Changed -= OnMonetizationChanged; ComplimentaryAccess.Dispose(); }
            Purchases?.Dispose();
            var config = MonetizationConfiguration.Load();
            Economy = new CampEconomy(Save.Economy, config.economy, Save.Save);
            Journeys = config.Catalog();
            JourneyAccess = new JourneyAccessService(Journeys, Progression, Save.Entitlements, () => Economy.IsPro,id=>SubscriptionEntitlement?.Invoke(id)??false, () => Economy.CanExplorePublishedContent);
            Memories = new CampMemoryService(Save.Memories);
            Completion = new CampCompletionService(Save, Progression, Memories, Journeys);
            Attempts = new CampAttemptService(Save, Economy, Completion);
            AdLives = new AdLifeService(Ads, Economy, () => Settings.optionalVideoBonuses && !Rewards.Busy && !Purchases.Busy && !AdHints.Busy);
            AdHints = new AdHintService(Ads, Economy, () => Settings.optionalVideoBonuses && !Rewards.Busy && !Purchases.Busy && !AdLives.Busy);
            AdLives.RewardObserved += OnLifeReward;
            AdHints.RewardObserved += OnHintReward;
            OwnerConfiguration = OwnerClientConfiguration.Load();
            Economy.ConfigureComplimentaryAccess(OwnerConfiguration.ComplimentaryAccessConfigured ? OwnerConfiguration.complimentaryAccessEndpoint : "");
            ComplimentaryAccess = new ComplimentaryAccessService(Economy,
                OwnerConfiguration.ComplimentaryAccessConfigured ? new ComplimentaryAccessTransport(OwnerConfiguration.complimentaryAccessEndpoint, OwnerConfiguration.androidPackageName) : null,
                OwnerConfiguration.androidPackageName, OwnerConfiguration.proProductId);
            ComplimentaryAccess.Changed += OnMonetizationChanged;
            OperationalMetrics?.Clear();
            OperationalMetrics = new OperationalMetrics(
                () => OwnerConfiguration.operationalMetricsReviewed && Legal.Published && LegalConfiguration.PublishedUrl(OwnerConfiguration.operationalMetricsEndpoint) && PrivacyEligibility.Band(Settings) == PrivacyAgeBand.Under13,
                (name, bucket, count) => OperationalMetricsTransport.Send(OwnerConfiguration.operationalMetricsEndpoint, name, bucket, count));
            if (OwnerConfiguration.BillingConfigured(Legal)) Economy.RequireProVerification();
            GoogleConfiguration.PurchasesConfigured = () => OwnerConfiguration.BillingConfigured(Legal);
            var nativeProvider = provider ?? OptionalBilling.Create(OwnerConfiguration, Legal, () => GoogleConfiguration.Permissions.Decisions.PurchasesAllowed);
            foreach (var product in config.products)
                if (product.pro) { if (!string.IsNullOrWhiteSpace(OwnerConfiguration.proProductId)) product.id = OwnerConfiguration.proProductId; product.published = OwnerConfiguration.BillingConfigured(Legal); }
            Purchases = new PurchaseService(nativeProvider, config.products, Save.Purchases, GrantProduct, Save.Save, product => product.pro ? Economy.SetProEntitlement(false) : EconomyResult.Unavailable,
                receipt => Economy.GrantVerifiedPro(receipt.receiptId, receipt.validUntilUtc));
            Economy.Changed += OnMonetizationChanged; AdLives.Changed += OnMonetizationChanged; AdHints.Changed += OnMonetizationChanged; Purchases.Changed += OnMonetizationChanged;
            Interstitials = new InterstitialService(Ads,
                () => !Economy.IsPro && !Purchases.Busy && !AdLives.Busy && !AdHints.Busy && !Rewards.Busy && GoogleConfiguration.interstitialsEnabled,
                Progression.CompletedCount, OwnerConfiguration.firstInterstitialAfterCompletedLevels,
                OwnerConfiguration.completedLevelsBetweenInterstitials, OwnerConfiguration.minimumInterstitialIntervalSeconds,
                OwnerConfiguration.interstitialCooldownAfterRewardSeconds, OwnerConfiguration.maximumInterstitialsPerSession);
            GoogleConfiguration.RewardEarned = placement => {
                if (placement == "camp.life" || placement == "camp.hint") Economy.RememberEarnedAdReward(placement == "camp.hint");
                Interstitials.Rewarded();
            };
            GoogleConfiguration.AdObserved = (placement, stage) => Analytics.AdEvent(stage,
                placement == "camp.life" ? 0 : placement == "camp.hint" ? 1 : placement == "camp.interstitial" ? 3 : 2);
            Purchases.StateObserved += state => Analytics.PurchaseEvent(state == PurchaseState.Owned ? 1 : 0);
            Economy.RefreshLives();
            _nextPurchaseRefresh = DateTime.UtcNow.AddMinutes(15);
            _ = Purchases.Restore();
            _ = ComplimentaryAccess.Refresh();
        }
        /// <summary>Accept a downloaded/local, authenticated data manifest at the menu boundary.
        /// Delivery may later use Addressables; installation never changes an active puzzle.</summary>
        public IReadOnlyList<string> InstallContentPack(string manifestJson)
        {
            if (World.CampSceneHost.Current != null || MonetizationBusy) return new[] { "pack.session-busy" };
            var errors = ContentPackRuntime.Store.Install(manifestJson);
            if (errors.Count > 0) return errors;
            Journeys = MonetizationConfiguration.Load().Catalog();
            JourneyAccess = new JourneyAccessService(Journeys, Progression, Save.Entitlements,
                () => Economy.IsPro, id => SubscriptionEntitlement?.Invoke(id) ?? false, () => Economy.CanExplorePublishedContent);
            Completion = new CampCompletionService(Save, Progression, Memories, Journeys);
            Attempts = new CampAttemptService(Save, Economy, Completion);
            OnMonetizationChanged();
            return errors;
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
        #if UNITY_EDITOR
        public bool PilotCompleted(string id)=>RoadmapPilotPolicy.Completed(Progression,id);
        #endif
        public bool CanStart(string id)
        {
            id = CampContent.CanonicalId(id);
#if UNITY_EDITOR
            if (EditorAuthoringPreview.Matches(id)) return true;
            if (id == LevelLoader.TestLevelId()) return true;
#endif
            var bonus = BonusCampCatalog.ForLevel(id);
            if(bonus!=null)return BonusCampCatalog.IsPublished(bonus)&&(Economy.IsPro||BonusCamps.Evaluate(bonus).CanPlay);
            var access=JourneyAccess.Evaluate(id);
            return access.CanStart;
        }
        public string ContinueLevel()
        {
            var saved = CampContent.CanonicalId(Save.Session?.levelId);
            return DirectCampNavigation.ContinueTarget(Journeys, Progression, CanStart, saved);
        }
        public string NextLevel(string completedId)
            => DirectCampNavigation.NextTarget(Journeys, Progression, CanStart, CampContent.CanonicalId(completedId));
        public EconomyResult BuyJourney(string id)
        {
            var journey = Journeys.Find(id);
            if (journey == null || !journey.published || string.IsNullOrEmpty(journey.entitlementId)) return EconomyResult.Unavailable;
            if ((Economy.IsPro && journey.includedInPro) || Save.Entitlements.Has(journey.entitlementId)) return EconomyResult.AlreadyApplied;
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
            var curriculum=LearningCurriculum.Read(UnityEngine.Resources.Load<UnityEngine.TextAsset>("QuietCamp/learning_curriculum").text);
            var tutorial = new TutorialDirector(Save.Progress.tutorial, existing, Progression, Save.Save, curriculum);
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
            await Crashes.SetConsent(false);
            if (!Save.MemoryOnly && !(OptionalBilling.ClearLocalHistory?.Invoke() ?? true)) return false;
            if (!Save.MemoryOnly && !SaveAdapter.TryEraseLocalFiles(UnityEngine.Application.persistentDataPath, out _)) return false;
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
            PendingLevelId = null; PendingMenuScreen = null; AlbumIndex = 0;
#if UNITY_EDITOR
            LevelMapScroll = -1; LevelMapAnchor=null; LevelMapJourney="main"; LevelMapAnchors.Clear();
#endif
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
            Rewards.RewardObserved -= OnCosmeticReward;
            AdLives.RewardObserved -= OnLifeReward;
            AdHints.RewardObserved -= OnHintReward;
            Save.SaveFailed -= OnSaveFailed;
            Save.BeforeSave -= SyncProgress;
            OperationalMetrics?.Clear(); Purchases?.Dispose(); ComplimentaryAccess?.Dispose();
            Analytics.Dispose();
            Crashes.Dispose();
            (Ads as IDisposable)?.Dispose();
            Haptics.Dispose();
            Audio?.Dispose();
        }
        void OnSaveFailed(string ignoredMessage) => Crashes.Report(CrashArea.Save);
        bool PersistPrivacy()
        {
            bool saved = _privacyPersistence.Save();
            if (!saved) GoogleConfiguration.Permissions.InvalidateAdvertising();
            GoogleConfiguration.Permissions.Refresh();
            return saved;
        }
        public async System.Threading.Tasks.Task<bool> SetPrivacyAge(PrivacyAgeBand band)
        {
            GoogleConfiguration.Permissions.InvalidateAdvertising();
            if (PrivacyEligibility.Band(Settings) == band && Settings.privacyAgeVersion == PrivacyEligibility.AgeVersion) return true;
            OperationalMetrics?.Clear();
            bool saved = PrivacyEligibility.ChangeAge(Settings, band, PersistPrivacy);
            await Analytics.SetConsent(false);
            await Crashes.SetConsent(false);
            (Ads as IAdRequestCancellation)?.CancelPending();
            return saved && PrivacyEligibility.Band(Settings) == band && Settings.privacyAgeVersion == PrivacyEligibility.AgeVersion;
        }
        public bool SetAdvertisingConsent(bool enabled)
        {
            GoogleConfiguration.Permissions.InvalidateAdvertising();
            bool saved = PrivacyEligibility.SetAdvertising(Settings, enabled, GoogleConfiguration.ConsentRevision, PersistPrivacy);
            if (!GoogleConfiguration.AdvertisingAllowed) (Ads as IAdRequestCancellation)?.CancelPending();
            return saved;
        }
        public bool SetOptionalVideoBonuses(bool enabled)
            => PrivacyEligibility.SetVideoBonuses(Settings, enabled, PersistPrivacy);
        void OnLifeReward(RewardStage stage) => Analytics.Reward(stage, 0);
        void OnHintReward(RewardStage stage) => Analytics.Reward(stage, 2);
        void OnCosmeticReward(RewardStage stage) => Analytics.Reward(stage, 1);
    }
}
