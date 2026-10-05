using System;
using System.Linq;

namespace QuietCamp.Application
{
    [Serializable] public sealed class EconomyRules
    {
        public int initialLives = 5, initialHints = 3, lifeCost = 10, hintCost = 15, dailyAdLives = 10;
    }
    [Serializable] public sealed class AttemptRecord
    {
        public string levelId;
        public int checks;
    }
    [Serializable] public sealed class EconomySaveData
    {
        public int version = 1;
        public bool initialized, pro;
        public int lives, hints, currency, adLivesUsed;
        public string adDay;
        public AttemptRecord[] attempts = Array.Empty<AttemptRecord>();
        public string[] appliedReceiptIds = Array.Empty<string>();

        public EconomySaveData Copy() => new EconomySaveData
        {
            version = version, initialized = initialized, pro = pro, lives = lives, hints = hints,
            currency = currency, adLivesUsed = adLivesUsed, adDay = adDay,
            attempts = (attempts ?? Array.Empty<AttemptRecord>()).Select(a => new AttemptRecord { levelId = a.levelId, checks = a.checks }).ToArray(),
            appliedReceiptIds = (string[])(appliedReceiptIds ?? Array.Empty<string>()).Clone()
        };
        public void Restore(EconomySaveData value)
        {
            version = value.version; initialized = value.initialized; pro = value.pro;
            lives = value.lives; hints = value.hints; currency = value.currency;
            adLivesUsed = value.adLivesUsed; adDay = value.adDay;
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
        public event Action Changed;
        public EconomySaveData Data => _data;
        public bool IsPro => _data.pro;
        public int Lives => _data.lives;
        public int Hints => _data.hints;
        public int Currency => _data.currency;
        public int LifeCost => _rules.lifeCost;
        public int HintCost => _rules.hintCost;
        public bool CanCheck => IsPro || Lives > 0;
        public bool CanHint => IsPro || Hints > 0;
        public int AdLivesRemaining => Math.Max(0, _rules.dailyAdLives - (CurrentDay() == _data.adDay ? _data.adLivesUsed : 0));

        public CampEconomy(EconomySaveData data, EconomyRules rules, Func<bool> persist, Func<DateTime> clock = null)
        {
            _data = data ?? throw new ArgumentNullException(nameof(data));
            _rules = rules ?? throw new ArgumentNullException(nameof(rules));
            _persist = persist ?? throw new ArgumentNullException(nameof(persist));
            _clock = clock ?? (() => DateTime.UtcNow);
            if (_data.version != 1 || data.lives < 0 || data.hints < 0 || data.currency < 0 || data.adLivesUsed < 0
                || rules.initialLives < 1 || rules.initialHints < 0 || rules.lifeCost < 1 || rules.hintCost < 1 || rules.dailyAdLives != 10)
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
        public EconomyResult RecordCheck(string levelId, bool solved)
        {
            if (string.IsNullOrWhiteSpace(levelId)) return EconomyResult.Invalid;
            if (!CanCheck) return EconomyResult.NoLives;
            if (IsPro) return EconomyResult.Applied;
            if (Attempts(levelId) == int.MaxValue) return EconomyResult.Invalid;
            return Commit(() =>
            {
                var record = _data.attempts.FirstOrDefault(a => a.levelId == levelId);
                if (record == null) { record = new AttemptRecord { levelId = levelId }; _data.attempts = _data.attempts.Append(record).ToArray(); }
                record.checks++;
                if (!solved) _data.lives--;
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
        public EconomyResult GrantAdLife(string rewardId)
        {
            if (IsPro || string.IsNullOrWhiteSpace(rewardId)) return EconomyResult.Invalid;
            if (Applied(rewardId)) return EconomyResult.AlreadyApplied;
            if (Lives == int.MaxValue) return EconomyResult.Invalid;
            if (AdLivesRemaining == 0) return EconomyResult.DailyLimit;
            return Commit(() =>
            {
                var day = CurrentDay();
                if (_data.adDay != day) { _data.adDay = day; _data.adLivesUsed = 0; }
                _data.adLivesUsed++; _data.lives++; Receipt(rewardId);
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
