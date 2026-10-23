using System;
using System.Linq;

namespace QuietCamp.Application
{
    [Serializable] public sealed class EconomyRules
    {
        public int initialLives = 5, initialHints = 3, lifeCost = 10, hintCost = 15, dailyAdLives = 10;
        public int lifeRecoveryMinutes = 30, dailyAdHints = 5;
    }
    [Serializable] public sealed class AttemptRecord
    {
        public string levelId;
        public int checks;
        public int consecutiveFailures;
        public bool recoveryHintPending;
    }
    [Serializable] public sealed class EconomySaveData
    {
        public int version = 1;
        public bool initialized, pro, proVerificationRequired;
        public string proValidUntilUtc;
        public string complimentarySessionToken, complimentaryValidUntilUtc, complimentaryEndpoint;
        public int lives, hints, currency, adLivesUsed, adHintsUsed;
        public string adDay;
        public string pendingAdLifeReceipt, pendingAdHintReceipt;
        public string lifeRecoveryAnchorUtc;
        public AttemptRecord[] attempts = Array.Empty<AttemptRecord>();
        public string[] appliedReceiptIds = Array.Empty<string>();

        public EconomySaveData Copy() => new EconomySaveData
        {
            version = version, initialized = initialized, pro = pro, proVerificationRequired = proVerificationRequired, proValidUntilUtc = proValidUntilUtc, lives = lives, hints = hints,
            currency = currency, adLivesUsed = adLivesUsed, adHintsUsed = adHintsUsed, adDay = adDay,
            pendingAdLifeReceipt = pendingAdLifeReceipt, pendingAdHintReceipt = pendingAdHintReceipt,
            lifeRecoveryAnchorUtc = lifeRecoveryAnchorUtc,
            complimentarySessionToken = complimentarySessionToken, complimentaryValidUntilUtc = complimentaryValidUntilUtc, complimentaryEndpoint = complimentaryEndpoint,
            attempts = (attempts ?? Array.Empty<AttemptRecord>()).Select(a => new AttemptRecord { levelId = a.levelId, checks = a.checks, consecutiveFailures=a.consecutiveFailures, recoveryHintPending=a.recoveryHintPending }).ToArray(),
            appliedReceiptIds = (string[])(appliedReceiptIds ?? Array.Empty<string>()).Clone()
        };
        public void Restore(EconomySaveData value)
        {
            version = value.version; initialized = value.initialized; pro = value.pro; proVerificationRequired = value.proVerificationRequired; proValidUntilUtc = value.proValidUntilUtc;
            lives = value.lives; hints = value.hints; currency = value.currency;
            adLivesUsed = value.adLivesUsed; adHintsUsed = value.adHintsUsed; adDay = value.adDay;
            pendingAdLifeReceipt = value.pendingAdLifeReceipt; pendingAdHintReceipt = value.pendingAdHintReceipt;
            lifeRecoveryAnchorUtc = value.lifeRecoveryAnchorUtc;
            complimentarySessionToken = value.complimentarySessionToken; complimentaryValidUntilUtc = value.complimentaryValidUntilUtc; complimentaryEndpoint = value.complimentaryEndpoint;
            attempts = value.attempts; appliedReceiptIds = value.appliedReceiptIds;
        }
    }
    public enum EconomyResult { Applied, AlreadyApplied, NoLives, NoHints, InsufficientCurrency, DailyLimit, SaveFailed, Invalid, Busy, Unavailable }

    public sealed class CampEconomy
    {
        readonly EconomySaveData _data;
        readonly EconomyRules _rules;
        readonly Func<bool> _persist;
        readonly Func<DateTime> _clock;
        string _complimentaryEndpoint;
        bool _complimentaryRevoked;
        string _cachedProExpiry, _cachedComplimentaryExpiry;
        DateTime _proExpiry, _complimentaryExpiry;
        public event Action Changed;
        public EconomySaveData Data => _data;
        public bool IsPro => HasComplimentaryPro || (_data.pro && (!_data.proVerificationRequired
            || LeaseActive(_data.proValidUntilUtc, ref _cachedProExpiry, ref _proExpiry)));
        public bool HasComplimentaryPro => !_complimentaryRevoked && !string.IsNullOrEmpty(ComplimentarySessionToken)
            && LeaseActive(_data.complimentaryValidUntilUtc, ref _cachedComplimentaryExpiry, ref _complimentaryExpiry);
        bool LeaseActive(string value, ref string cachedValue, ref DateTime expiry)
        {
            if (value != cachedValue) {
                cachedValue = value;
                expiry = DateTime.TryParse(value, System.Globalization.CultureInfo.InvariantCulture,
                    System.Globalization.DateTimeStyles.RoundtripKind, out var until) ? until.ToUniversalTime() : DateTime.MinValue;
            }
            return expiry > UtcNow; // The clock is checked on every query; only parsing is cached.
        }
        public string ComplimentarySessionToken => !_complimentaryRevoked && !string.IsNullOrEmpty(_complimentaryEndpoint) && _complimentaryEndpoint == _data.complimentaryEndpoint
            ? _data.complimentarySessionToken : null;
        public void ConfigureComplimentaryAccess(string endpoint) { _complimentaryEndpoint = endpoint; }
        public EconomyResult GrantComplimentaryPro(string token, string validUntilUtc)
        {
            if (string.IsNullOrEmpty(_complimentaryEndpoint) || token == null || token.Length != 43
                || token.Any(c => !(c >= 'a' && c <= 'z' || c >= 'A' && c <= 'Z' || c >= '0' && c <= '9' || c == '-' || c == '_'))
                || !DateTime.TryParse(validUntilUtc, System.Globalization.CultureInfo.InvariantCulture,
                    System.Globalization.DateTimeStyles.RoundtripKind, out var until) || until.ToUniversalTime() <= UtcNow || until.ToUniversalTime() > UtcNow.AddDays(1).AddMinutes(1))
                return EconomyResult.Invalid;
            var result = Commit(() => { _complimentaryRevoked = false; _data.complimentarySessionToken = token;
                _data.complimentaryEndpoint = _complimentaryEndpoint; _data.complimentaryValidUntilUtc = validUntilUtc; });
            if (result != EconomyResult.Applied) _complimentaryRevoked = true;
            return result;
        }
        public EconomyResult RevokeComplimentaryPro()
        {
            _complimentaryRevoked = true;
            return Commit(() => { _data.complimentarySessionToken = _data.complimentaryValidUntilUtc = _data.complimentaryEndpoint = null; });
        }
        public int Lives => _data.lives;
        public int Hints => _data.hints;
        public int Currency => _data.currency;
        public int LifeCost => _rules.lifeCost;
        public int HintCost => _rules.hintCost;
        public bool CanCheck => IsPro || Lives > 0;
        public bool CanHint => IsPro || Hints > 0;
        public int AdLivesRemaining => Math.Max(0, _rules.dailyAdLives - (CurrentDay() == _data.adDay ? _data.adLivesUsed : 0));
        public int AdHintsRemaining => Math.Max(0, _rules.dailyAdHints - (CurrentDay() == _data.adDay ? _data.adHintsUsed : 0));
        public int LifeCapacity => _rules.initialLives;
        DateTime UtcNow => _clock().ToUniversalTime();
        static string Timestamp(DateTime value) => value.ToString("O", System.Globalization.CultureInfo.InvariantCulture);
        bool RecoveryAnchor(out DateTime anchor) => DateTime.TryParseExact(_data.lifeRecoveryAnchorUtc, "O",
            System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.RoundtripKind, out anchor)
            && anchor.Kind == DateTimeKind.Utc;
        public double SecondsUntilNextLife
        {
            get
            {
                if (IsPro || Lives >= LifeCapacity) return 0;
                double interval = _rules.lifeRecoveryMinutes * 60d;
                if (!RecoveryAnchor(out var anchor)) return interval;
                return Math.Max(0, Math.Min(interval, interval - (UtcNow - anchor).TotalSeconds));
            }
        }
        /// <summary>Slow local recovery, never an instant free refill or SDK operation.</summary>
        public EconomyResult RefreshLives()
        {
            if (IsPro || Lives >= LifeCapacity) return EconomyResult.Unavailable;
            var now = UtcNow;
            // Legacy saves start a timer; malformed/future clocks cannot grant a life.
            if (!RecoveryAnchor(out var anchor) || now < anchor)
                return Commit(() => _data.lifeRecoveryAnchorUtc = Timestamp(now));
            var elapsed = (now - anchor).TotalMinutes;
            int count = (int)Math.Min(LifeCapacity - Lives, Math.Floor(elapsed / _rules.lifeRecoveryMinutes));
            if (count < 1) return EconomyResult.AlreadyApplied;
            return Commit(() =>
            {
                _data.lives += count;
                _data.lifeRecoveryAnchorUtc = Timestamp(_data.lives >= LifeCapacity ? now : anchor.AddMinutes((double)count * _rules.lifeRecoveryMinutes));
            });
        }

        public CampEconomy(EconomySaveData data, EconomyRules rules, Func<bool> persist, Func<DateTime> clock = null)
        {
            _data = data ?? throw new ArgumentNullException(nameof(data));
            _rules = rules ?? throw new ArgumentNullException(nameof(rules));
            _persist = persist ?? throw new ArgumentNullException(nameof(persist));
            _clock = clock ?? (() => DateTime.UtcNow);
            if (_data.version != 1 || data.lives < 0 || data.hints < 0 || data.currency < 0 || data.adLivesUsed < 0
                || data.adHintsUsed < 0 || rules.initialLives < 1 || rules.initialHints < 0 || rules.lifeCost < 1 || rules.hintCost < 1 || rules.dailyAdLives != 10
                || rules.lifeRecoveryMinutes < 1 || rules.lifeRecoveryMinutes > 1440 || rules.dailyAdHints < 1 || rules.dailyAdHints > 10)
                throw new ArgumentException("Unsupported economy data or rules");
            if (!data.initialized) { data.lives = rules.initialLives; data.hints = rules.initialHints; data.initialized = true; }
            data.attempts ??= Array.Empty<AttemptRecord>(); data.appliedReceiptIds ??= Array.Empty<string>();
        }
        public int Attempts(string levelId) => _data.attempts.FirstOrDefault(a => a.levelId == levelId)?.checks ?? 0;
        public EconomyResult UseHint()
        {
            if (!CanHint) return EconomyResult.NoHints;
            return IsPro ? EconomyResult.Applied : Commit(() => _data.hints--);
        }
        public bool RecoveryHintPending(string levelId) => _data.attempts.Any(a=>a.levelId==levelId&&a.recoveryHintPending);
        public EconomyResult UseRecoveryHint(string levelId)
        {
            var record=_data.attempts.FirstOrDefault(a=>a.levelId==levelId);
            if(record?.recoveryHintPending!=true)return EconomyResult.NoHints;
            return Commit(()=>record.recoveryHintPending=false);
        }
        public EconomyResult RecordCheck(string levelId, bool solved, bool learning=false)
        {
            if (string.IsNullOrWhiteSpace(levelId)) return EconomyResult.Invalid;
            if (!learning&&!CanCheck) return EconomyResult.NoLives;
            if (Attempts(levelId) == int.MaxValue) return EconomyResult.Invalid;
            return Commit(() =>
            {
                var record = _data.attempts.FirstOrDefault(a => a.levelId == levelId);
                if (record == null) { record = new AttemptRecord { levelId = levelId }; _data.attempts = _data.attempts.Append(record).ToArray(); }
                if(!IsPro&&!learning)record.checks++;
                if(solved){record.consecutiveFailures=0;record.recoveryHintPending=false;}
                else
                {
                    record.consecutiveFailures=Math.Min(3,record.consecutiveFailures+1);
                    if(record.consecutiveFailures==3){record.recoveryHintPending=true;record.consecutiveFailures=0;}
                    if(!IsPro&&!learning)
                    {
                        if (_data.lives >= LifeCapacity || !RecoveryAnchor(out _)) _data.lifeRecoveryAnchorUtc = Timestamp(UtcNow);
                        _data.lives--;
                    }
                }
            });
        }
        public EconomyResult SpendCurrency(int cost)
        {
            if (cost < 1) return EconomyResult.Invalid;
            if (Currency < cost) return EconomyResult.InsufficientCurrency;
            return Commit(() => _data.currency -= cost);
        }
        public EconomyResult BuyLives(int count) => Exchange(count, _rules.lifeCost, true);
        public EconomyResult BuyHints(int count) => Exchange(count, _rules.hintCost, false);
        EconomyResult Exchange(int count, int price, bool lives)
        {
            if (IsPro || count < 1 || (long)count * price > int.MaxValue || (long)(lives ? Lives : Hints) + count > int.MaxValue) return EconomyResult.Invalid;
            int cost = count * price;
            if (Currency < cost) return EconomyResult.InsufficientCurrency;
            return Commit(() => { _data.currency -= cost; if (lives) _data.lives += count; else _data.hints += count; });
        }
        public EconomyResult CreditVerified(string receiptId, int amount)
        {
            if (string.IsNullOrWhiteSpace(receiptId) || amount < 1) return EconomyResult.Invalid;
            if (Applied(receiptId)) return EconomyResult.AlreadyApplied;
            if ((long)Currency + amount > int.MaxValue) return EconomyResult.Invalid;
            return Commit(() => { _data.currency += amount; Receipt(receiptId); });
        }
        public EconomyResult GrantPro(string receiptId)
        {
            if (string.IsNullOrWhiteSpace(receiptId)) return EconomyResult.Invalid;
            if (Applied(receiptId)) return EconomyResult.AlreadyApplied;
            return Commit(() => { _data.pro = true; Receipt(receiptId); });
        }
        public string PendingAdReward(bool hint) => hint ? _data.pendingAdHintReceipt : _data.pendingAdLifeReceipt;
        // Called synchronously from the native earned callback, before dismissal.
        // A failed disk write keeps the receipt in memory for retry; never show a
        // second ad to repair a failed reward save.
        public string RememberEarnedAdReward(bool hint)
        {
            var receipt = PendingAdReward(hint);
            if (string.IsNullOrEmpty(receipt))
            {
                receipt = (hint ? "ad-hint:" : "ad:") + Guid.NewGuid().ToString("N");
                if (hint) _data.pendingAdHintReceipt = receipt; else _data.pendingAdLifeReceipt = receipt;
            }
            try { _persist(); } catch { }
            return receipt;
        }
        public void RequireProVerification() { _data.proVerificationRequired = true; }
        public EconomyResult GrantVerifiedPro(string receipt, string validUntilUtc)
        {
            if (string.IsNullOrWhiteSpace(receipt) || !DateTime.TryParse(validUntilUtc, System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.RoundtripKind, out var until) || until.ToUniversalTime() <= _clock().ToUniversalTime()
                || until.ToUniversalTime() > _clock().ToUniversalTime().AddDays(2)) return EconomyResult.Invalid;
            return Commit(() => { _data.pro = true; _data.proVerificationRequired = true; _data.proValidUntilUtc = validUntilUtc;
                if (!Applied(receipt)) Receipt(receipt); });
        }
        public EconomyResult SetProEntitlement(bool active) => _data.pro == active ? EconomyResult.AlreadyApplied : Commit(() => _data.pro = active);
        public EconomyResult GrantAdLife(string rewardId)
        {
            if (IsPro || string.IsNullOrWhiteSpace(rewardId)) return EconomyResult.Invalid;
            if (Applied(rewardId)) return EconomyResult.AlreadyApplied;
            if (Lives == int.MaxValue) return EconomyResult.Invalid;
            if (AdLivesRemaining == 0) return EconomyResult.DailyLimit;
            return Commit(() =>
            {
                var day = CurrentDay();
                if (_data.adDay != day) { _data.adDay = day; _data.adLivesUsed = _data.adHintsUsed = 0; }
                _data.adLivesUsed++; _data.lives++; Receipt(rewardId);
                if (_data.pendingAdLifeReceipt == rewardId) _data.pendingAdLifeReceipt = null;
            });
        }
        public EconomyResult GrantAdHint(string rewardId)
        {
            if (IsPro || string.IsNullOrWhiteSpace(rewardId)) return EconomyResult.Invalid;
            if (Applied(rewardId)) return EconomyResult.AlreadyApplied;
            if (Hints == int.MaxValue) return EconomyResult.Invalid;
            if (AdHintsRemaining == 0) return EconomyResult.DailyLimit;
            return Commit(() =>
            {
                var day = CurrentDay();
                if (_data.adDay != day) { _data.adDay = day; _data.adLivesUsed = _data.adHintsUsed = 0; }
                _data.adHintsUsed++; _data.hints++; Receipt(rewardId);
                if (_data.pendingAdHintReceipt == rewardId) _data.pendingAdHintReceipt = null;
            });
        }
        bool Applied(string id) => Array.IndexOf(_data.appliedReceiptIds, id) >= 0;
        void Receipt(string id) => _data.appliedReceiptIds = _data.appliedReceiptIds.Append(id).ToArray();
        string CurrentDay()
        {
            var today = _clock().ToUniversalTime().ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);
            return string.CompareOrdinal(today, _data.adDay) < 0 ? _data.adDay : today;
        }
        EconomyResult Commit(Action apply)
        {
            var previous = _data.Copy();
            try
            {
                apply();
                if (!_persist()) { _data.Restore(previous); return EconomyResult.SaveFailed; }
            }
            catch { _data.Restore(previous); return EconomyResult.SaveFailed; }
            Changed?.Invoke(); return EconomyResult.Applied;
        }
    }
}
