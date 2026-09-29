namespace Kruty1918.SaveSystem
{
    using System;

    /// <summary>
    /// Тип контролера, який керує діями гравця в матчі.
    /// </summary>
    public enum PlayerControllerType
    {
        /// <summary>Гравцем керує людина.</summary>
        Human = 0,
        /// <summary>Гравцем керує бот-опонент.</summary>
        Bot = 1
    }

    /// <summary>
    /// Сценарій запуску ігрової сесії, обраний перед входом у gameplay-сцену.
    /// </summary>
    public enum GameLaunchMode
    {
        /// <summary>Режим запуску не визначено.</summary>
        Unknown = 0,
        /// <summary>Прямий запуск gameplay-сцени для тестування.</summary>
        DirectGameplayTest = 1,
        /// <summary>Нова гра, створена з головного меню.</summary>
        MenuNewGame = 2,
        /// <summary>Завантаження збереження з головного меню.</summary>
        MenuLoadGame = 3,
        /// <summary>Приєднання до мережевої гри з головного меню.</summary>
        MenuJoinGame = 4,
        /// <summary>Мережевий матч, створений з головного меню.</summary>
        MenuMultiplayerGame = 5,
        /// <summary>Локальний матч проти бота з головного меню.</summary>
        MenuBotGame = 6,
    }

    /// <summary>
    /// Джерело, з якого ініційовано поточний запуск гри.
    /// </summary>
    public enum GameLaunchSource
    {
        /// <summary>Джерело запуску не визначено.</summary>
        Unknown = 0,
        /// <summary>Прямий тестовий запуск gameplay-сцени.</summary>
        DirectGameplayTest = 1,
        /// <summary>Запуск із головного меню.</summary>
        HomeMenu = 2,
        /// <summary>Запуск через завантаження збереження.</summary>
        SaveLoad = 3,
    }

    /// <summary>
    /// Міжсценовий контекст запуску, що вирівнює стартову поведінку гри.
    /// Дозволяє gameplay-сцені розрізняти прямий тестовий запуск і запуск із меню.
    /// </summary>
    public static class GameLaunchContext
    {
        private static readonly TimeSpan DefaultContextTtl = TimeSpan.FromMinutes(30);

        private static bool? _autoLoadOverride;
        private static bool? _autoSaveOverride;
        private static DateTime _configuredAtUtc;
        private static DateTime _expiresAtUtc;

        /// <summary>Поточний сценарій запуску ігрової сесії.</summary>
        public static GameLaunchMode Mode { get; private set; } = GameLaunchMode.Unknown;
        /// <summary>Джерело, з якого ініційовано запуск.</summary>
        public static GameLaunchSource Source { get; private set; } = GameLaunchSource.Unknown;
        /// <summary>Слот збереження, пов'язаний із запуском.</summary>
        public static int SaveSlot { get; private set; } = 0;
        /// <summary>Чи передано параметри генерації світу.</summary>
        public static bool HasWorldSettings { get; private set; }
        /// <summary>Назва світу для нової гри.</summary>
        public static string WorldName { get; private set; } = string.Empty;
        /// <summary>Seed генерації світу.</summary>
        public static int Seed { get; private set; }
        /// <summary>Пресет розміру карти.</summary>
        public static int Size { get; private set; }
        /// <summary>Явна ширина карти в клітинках.</summary>
        public static int Width { get; private set; }
        /// <summary>Явна висота карти в клітинках.</summary>
        public static int Height { get; private set; }
        /// <summary>Ідентифікатор типу карти.</summary>
        public static int MapType { get; private set; }
        /// <summary>Ідентифікатор складності.</summary>
        public static int Difficulty { get; private set; }
        /// <summary>Максимальна кількість гравців у матчі.</summary>
        public static int MaxPlayers { get; private set; }
        /// <summary>Чи є лобі приватним.</summary>
        public static bool IsPrivate { get; private set; }
        /// <summary>Чи відома роль локального гравця.</summary>
        public static bool HasLocalPlayerRole { get; private set; }
        /// <summary>Чи є локальний гравець хостом.</summary>
        public static bool IsLocalPlayerHost { get; private set; }
        /// <summary>Ідентифікатор локального гравця.</summary>
        public static string LocalPlayerId { get; private set; } = string.Empty;
        /// <summary>Ідентифікатор гравця-бота.</summary>
        public static string BotPlayerId { get; private set; } = string.Empty;
        /// <summary>Ідентифікатор складності бота.</summary>
        public static string BotDifficultyId { get; private set; } = string.Empty;
        /// <summary>
        /// Explicit per-match turn flow override chosen in the host lobby and
        /// propagated to clients through the session settings. -1 means
        /// "not set — resolve from the JSON turn-rules preset".
        /// Values map to Kruty1918.Moyva.Turns.API.TurnFlowMode ordinals.
        /// </summary>
        public static int TurnMode { get; private set; } = -1;
        /// <summary>
        /// Explicit per-match round timer override in seconds; -1 resolves
        /// from the JSON turn-rules preset, 0 disables the timer.
        /// </summary>
        public static int TurnTimerSeconds { get; private set; } = -1;
        /// <summary>Чи має матч бот-опонента.</summary>
        public static bool HasBotOpponent
            => (Mode == GameLaunchMode.MenuBotGame || Mode == GameLaunchMode.MenuLoadGame)
                && !string.IsNullOrEmpty(BotPlayerId);

        /// <summary>
        /// Налаштовує бот-опонента для локальної нової гри на два місця.
        /// </summary>
        public static void ConfigureBotOpponent(string playerId, string difficultyId = null)
        {
            if (Mode != GameLaunchMode.MenuNewGame || MaxPlayers != 2
                || string.IsNullOrWhiteSpace(playerId) || playerId == LocalPlayerId)
                throw new InvalidOperationException("A bot opponent requires a two-player local new game.");
            BotPlayerId = playerId.Trim();
            BotDifficultyId = string.IsNullOrWhiteSpace(difficultyId) ? string.Empty : difficultyId.Trim();
            Mode = GameLaunchMode.MenuBotGame;
            _autoLoadOverride = false;
        }

        /// <summary>
        /// Повертає тип контролера для вказаного гравця: бот або людина.
        /// </summary>
        public static PlayerControllerType GetPlayerController(string playerId)
            => HasBotOpponent && string.Equals(playerId, BotPlayerId, StringComparison.Ordinal)
                ? PlayerControllerType.Bot : PlayerControllerType.Human;
        /// <summary>UTC-момент останнього налаштування контексту.</summary>
        public static DateTime ConfiguredAtUtc => _configuredAtUtc;
        /// <summary>UTC-момент, після якого контекст вважається протермінованим.</summary>
        public static DateTime ExpiresAtUtc => _expiresAtUtc;
        /// <summary>Чи існує активний контекст запуску.</summary>
        public static bool HasActiveContext => Mode != GameLaunchMode.Unknown;
        /// <summary>Чи протерміновано активний контекст запуску.</summary>
        public static bool IsExpired => HasActiveContext && DateTime.UtcNow >= _expiresAtUtc;

        /// <summary>
        /// Налаштовує контекст прямого тестового запуску gameplay-сцени.
        /// </summary>
        public static void ConfigureDirectGameplayTest()
        {
            Mode = GameLaunchMode.DirectGameplayTest;
            Source = GameLaunchSource.DirectGameplayTest;
            SaveSlot = 0;
            ClearWorldSettings();
            ClearLocalPlayerRole();
            MaxPlayers = 2;
            _autoLoadOverride = false;
            _autoSaveOverride = false;
            MarkConfigured(DefaultContextTtl);
        }

        /// <summary>
        /// Відновлює відсутній контекст запуску для gameplay-сцени, стартованої напряму
        /// з редактора Unity або development-білду. Наявні контексти меню/збереження зберігаються.
        /// </summary>
        public static bool EnsureDirectGameplayTestFallback()
        {
            EnsureNotExpired();
            if (Mode != GameLaunchMode.Unknown)
                return false;

#if UNITY_EDITOR || UNITY_ENABLE_CHECKS
            ConfigureDirectGameplayTest();
            return true;
#else
            return false;
#endif
        }

        /// <summary>
        /// Налаштовує контекст нової гри, створеної з головного меню.
        /// </summary>
        public static void ConfigureMenuNewGame(int saveSlot = 0)
        {
            Mode = GameLaunchMode.MenuNewGame;
            Source = GameLaunchSource.HomeMenu;
            SaveSlot = ClampSlot(saveSlot);
            ClearWorldSettings();
            ClearLocalPlayerRole();
            _autoLoadOverride = false;
            _autoSaveOverride = true;
            MarkConfigured(DefaultContextTtl);
        }

        /// <summary>
        /// Налаштовує контекст нової гри з параметрами світу та роллю локального гравця.
        /// </summary>
        public static void ConfigureMenuNewGame(
            int saveSlot,
            string worldName,
            int seed,
            int size,
            int mapType,
            int difficulty,
            int maxPlayers,
            bool isPrivate,
            int width = 0,
            int height = 0,
            bool? isLocalPlayerHost = null,
            string localPlayerId = null,
            int turnMode = -1,
            int turnTimerSeconds = -1)
        {
            Mode = GameLaunchMode.MenuNewGame;
            Source = GameLaunchSource.HomeMenu;
            SaveSlot = ClampSlot(saveSlot);
            SetWorldSettings(worldName, seed, size, mapType, difficulty, maxPlayers, isPrivate, width, height,
                turnMode, turnTimerSeconds);
            SetLocalPlayerRole(isLocalPlayerHost, localPlayerId);
            _autoLoadOverride = false;
            _autoSaveOverride = true;
            MarkConfigured(DefaultContextTtl);
        }

        /// <summary>
        /// Налаштовує контекст завантаження збереженої гри з меню.
        /// </summary>
        public static void ConfigureMenuLoadGame(int saveSlot)
        {
            Mode = GameLaunchMode.MenuLoadGame;
            Source = GameLaunchSource.SaveLoad;
            SaveSlot = ClampSlot(saveSlot);
            ClearWorldSettings();
            ClearLocalPlayerRole();
            _autoLoadOverride = true;
            _autoSaveOverride = true;
            MarkConfigured(DefaultContextTtl);
        }

        /// <summary>
        /// Повторно активує ідентичність бота після завантаження збереженого матчу з ботом.
        /// Викликається модулем збереження бот-опонента; ігнорується для інших контекстів і порожніх id.
        /// </summary>
        public static void RestoreLoadedBotOpponent(string playerId, string difficultyId)
        {
            if (Mode != GameLaunchMode.MenuLoadGame || string.IsNullOrWhiteSpace(playerId))
                return;
            BotPlayerId = playerId.Trim();
            BotDifficultyId = string.IsNullOrWhiteSpace(difficultyId) ? string.Empty : difficultyId.Trim();
        }

        /// <summary>
        /// Налаштовує контекст приєднання до мережевої гри з меню.
        /// </summary>
        public static void ConfigureMenuJoinGame()
        {
            Mode = GameLaunchMode.MenuJoinGame;
            Source = GameLaunchSource.HomeMenu;
            SaveSlot = 0;
            ClearWorldSettings();
            ClearLocalPlayerRole();
            _autoLoadOverride = false;
            _autoSaveOverride = false;
            MarkConfigured(DefaultContextTtl);
        }

        /// <summary>
        /// Налаштовує контекст мережевого матчу з параметрами світу та роллю локального гравця.
        /// </summary>
        public static void ConfigureMenuMultiplayerGame(
            string worldName,
            int seed,
            int size,
            int mapType,
            int difficulty,
            int maxPlayers,
            bool isPrivate,
            int width = 0,
            int height = 0,
            bool? isLocalPlayerHost = null,
            string localPlayerId = null,
            int turnMode = -1,
            int turnTimerSeconds = -1)
        {
            Mode = GameLaunchMode.MenuMultiplayerGame;
            Source = GameLaunchSource.HomeMenu;
            SaveSlot = 0;
            SetWorldSettings(worldName, seed, size, mapType, difficulty, maxPlayers, isPrivate, width, height,
                turnMode, turnTimerSeconds);
            SetLocalPlayerRole(isLocalPlayerHost, localPlayerId);
            _autoLoadOverride = false;
            _autoSaveOverride = false;
            MarkConfigured(DefaultContextTtl);
        }

        /// <summary>
        /// Повністю очищає контекст запуску та перевизначення прапорців.
        /// </summary>
        public static void Reset()
        {
            Mode = GameLaunchMode.Unknown;
            Source = GameLaunchSource.Unknown;
            SaveSlot = 0;
            ClearWorldSettings();
            ClearLocalPlayerRole();
            _autoLoadOverride = null;
            _autoSaveOverride = null;
            _configuredAtUtc = DateTime.MinValue;
            _expiresAtUtc = DateTime.MinValue;
        }

        /// <summary>
        /// Перевіряє актуальність контексту; за протермінування скидає його.
        /// </summary>
        public static bool EnsureNotExpired()
        {
            if (!IsExpired)
                return true;

            Reset();
            return false;
        }

        /// <summary>
        /// Подовжує час життя активного контексту запуску.
        /// </summary>
        public static void RefreshTtl(TimeSpan? ttl = null)
        {
            if (!HasActiveContext)
                return;

            MarkConfigured(ttl ?? DefaultContextTtl);
        }

        /// <summary>
        /// Повертає чинне значення прапорця автозавантаження з урахуванням override контексту.
        /// </summary>
        public static bool IsAutoLoadEnabled()
        {
            EnsureNotExpired();
            return _autoLoadOverride ?? SavePlayModeOptions.AutoLoadEnabled;
        }

        /// <summary>
        /// Повертає чинне значення прапорця автозбереження з урахуванням override контексту.
        /// </summary>
        public static bool IsAutoSaveEnabled()
        {
            EnsureNotExpired();
            return _autoSaveOverride ?? SavePlayModeOptions.AutoSaveEnabled;
        }

        /// <summary>
        /// Намагається отримати розміри світу з параметрів запуску.
        /// </summary>
        public static bool TryGetWorldDimensions(out int width, out int height)
        {
            EnsureNotExpired();

            width = 0;
            height = 0;

            if (!HasWorldSettings)
                return false;

            if (Width > 0 && Height > 0)
            {
                width = Width;
                height = Height;
                return true;
            }

            int side = Size switch
            {
                0 => 32,
                1 => 64,
                2 => 128,
                _ => 64,
            };

            width = side;
            height = side;
            return true;
        }

        /// <summary>
        /// Намагається отримати seed генерації світу з параметрів запуску.
        /// </summary>
        public static bool TryGetSeed(out int seed)
        {
            EnsureNotExpired();
            seed = HasWorldSettings ? Seed : 0;
            return HasWorldSettings && seed != 0;
        }

        private static void MarkConfigured(TimeSpan ttl)
        {
            if (ttl <= TimeSpan.Zero)
                ttl = DefaultContextTtl;

            _configuredAtUtc = DateTime.UtcNow;
            _expiresAtUtc = _configuredAtUtc.Add(ttl);
        }

        /// <summary>
        /// Applies synchronized turn rules on clients after session settings
        /// arrive from the host. Safe no-op for contexts without world settings.
        /// </summary>
        public static void ApplySyncedTurnRules(int turnMode, int turnTimerSeconds)
        {
            if (!HasActiveContext)
                return;

            TurnMode = turnMode;
            TurnTimerSeconds = turnTimerSeconds;
        }

        private static void SetWorldSettings(
            string worldName,
            int seed,
            int size,
            int mapType,
            int difficulty,
            int maxPlayers,
            bool isPrivate,
            int width,
            int height,
            int turnMode = -1,
            int turnTimerSeconds = -1)
        {
            HasWorldSettings = true;
            WorldName = string.IsNullOrWhiteSpace(worldName)
                ? "Новий світ"
                : worldName.Trim();
            Seed = seed;
            Size = size;
            Width = width > 0 ? width : 0;
            Height = height > 0 ? height : 0;
            MapType = mapType;
            Difficulty = difficulty;
            MaxPlayers = maxPlayers < 1 ? 1 : maxPlayers;
            IsPrivate = isPrivate;
            TurnMode = turnMode;
            TurnTimerSeconds = turnTimerSeconds;
        }

        private static void ClearWorldSettings()
        {
            BotPlayerId = string.Empty;
            BotDifficultyId = string.Empty;
            HasWorldSettings = false;
            WorldName = string.Empty;
            Seed = 0;
            Size = 0;
            Width = 0;
            Height = 0;
            MapType = 0;
            Difficulty = 0;
            MaxPlayers = 0;
            IsPrivate = false;
            TurnMode = -1;
            TurnTimerSeconds = -1;
        }

        private static void SetLocalPlayerRole(bool? isLocalPlayerHost, string localPlayerId)
        {
            BotPlayerId = string.Empty;
            BotDifficultyId = string.Empty;
            HasLocalPlayerRole = isLocalPlayerHost.HasValue || !string.IsNullOrWhiteSpace(localPlayerId);
            IsLocalPlayerHost = isLocalPlayerHost ?? false;
            LocalPlayerId = string.IsNullOrWhiteSpace(localPlayerId) ? string.Empty : localPlayerId.Trim();
        }

        private static void ClearLocalPlayerRole()
        {
            HasLocalPlayerRole = false;
            IsLocalPlayerHost = false;
            LocalPlayerId = string.Empty;
        }

        private static int ClampSlot(int slot)
        {
            if (slot < 0) return 0;
            if (slot > 99) return 99;
            return slot;
        }
    }

    /// <summary>
    /// Прапорці режиму гри, доступні в runtime та керовані з інструментів редактора.
    /// У player-білдах обидва прапорці завжди ввімкнені.
    /// </summary>
    public static class SavePlayModeOptions
    {
        private const string AutoLoadKey = "Moyva.Save.PlayMode.AutoLoad";
        private const string AutoSaveKey = "Moyva.Save.PlayMode.AutoSave";

        /// <summary>Чи ввімкнено автозавантаження під час запуску з редактора.</summary>
        public static bool AutoLoadEnabled
        {
            get => GetBool(AutoLoadKey, true);
            set => SetBool(AutoLoadKey, value);
        }

        /// <summary>Чи ввімкнено автозбереження під час запуску з редактора.</summary>
        public static bool AutoSaveEnabled
        {
            get => GetBool(AutoSaveKey, true);
            set => SetBool(AutoSaveKey, value);
        }

        private static bool GetBool(string key, bool defaultValue)
        {
#if UNITY_EDITOR
            return UnityEditor.EditorPrefs.GetBool(key, defaultValue);
#else
            return true;
#endif
        }

        private static void SetBool(string key, bool value)
        {
#if UNITY_EDITOR
            UnityEditor.EditorPrefs.SetBool(key, value);
#endif
        }
    }
}
