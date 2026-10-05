using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace QuietCamp.Application
{
    public enum PurchaseState { Idle, Purchasing, Pending, Verifying, Owned, Cancelled, Failed, Retry, Unavailable, Busy }
    [Serializable] public sealed class PurchaseProduct
    {
        public string id, titleKey, entitlementId;
        public int currencyAmount;
        public bool pro, published;
        public bool Consumable => currencyAmount > 0;
    }
    [Serializable] public sealed class PurchaseSaveData
    {
        public int version = 1;
        public string pendingProductId, pendingReceiptId;
        public string[] fulfilledProductIds = Array.Empty<string>();
    }
    public sealed class PurchaseDetails
    {
        public string productId, formattedPrice;
        public bool available;
    }
    public sealed class PurchaseReceipt
    {
        public string productId, receiptId;
        public PurchaseState state;
        public bool verified;
    }
    public interface IPurchaseProvider
    {
        Task<PurchaseDetails> Details(string productId);
        Task<PurchaseReceipt> Buy(string productId);
        Task<IReadOnlyList<PurchaseReceipt>> Query();
    }
    public sealed class UnavailablePurchaseProvider : IPurchaseProvider
    {
        public Task<PurchaseDetails> Details(string productId) => Task.FromResult(new PurchaseDetails { productId = productId });
        public Task<PurchaseReceipt> Buy(string productId) => Task.FromResult(new PurchaseReceipt { productId = productId, state = PurchaseState.Unavailable });
        public Task<IReadOnlyList<PurchaseReceipt>> Query() => Task.FromResult<IReadOnlyList<PurchaseReceipt>>(Array.Empty<PurchaseReceipt>());
    }
    public sealed class PurchaseService
    {
        readonly IPurchaseProvider _provider;
        readonly PurchaseProduct[] _products;
        readonly PurchaseSaveData _data;
        readonly Func<string, PurchaseProduct, EconomyResult> _grant;
        readonly Func<bool> _persist;
        bool _busy;
        public event Action Changed;
        public PurchaseState State { get; private set; }
        public bool Busy => _busy;
        public IReadOnlyList<PurchaseProduct> Products => _products;
        public PurchaseService(IPurchaseProvider provider, IEnumerable<PurchaseProduct> products, PurchaseSaveData data,
            Func<string, PurchaseProduct, EconomyResult> grant, Func<bool> persist)
        {
            _provider = provider ?? throw new ArgumentNullException(nameof(provider));
            _products = products?.ToArray() ?? Array.Empty<PurchaseProduct>(); _data = data; _grant = grant; _persist = persist;
            if (data.version != 1 || _products.Any(p => p == null || string.IsNullOrWhiteSpace(p.id) || p.currencyAmount < 0)
                || _products.Select(p => p.id).Distinct().Count() != _products.Length) throw new ArgumentException("Invalid purchase catalog");
            State = string.IsNullOrEmpty(data.pendingProductId) ? PurchaseState.Idle : PurchaseState.Retry;
        }
        public Task<PurchaseDetails> Details(string id) => _products.Any(p => p.id == id && p.published)
            ? _provider.Details(id) : Task.FromResult(new PurchaseDetails { productId = id });
        public async Task<PurchaseState> Buy(string id)
        {
            if (_busy) return PurchaseState.Busy;
            var product = _products.FirstOrDefault(p => p.id == id && p.published);
            if (product == null) return Set(PurchaseState.Unavailable);
            if (!string.IsNullOrEmpty(_data.pendingProductId)) return Set(PurchaseState.Pending);
            if (!product.Consumable && Array.IndexOf(_data.fulfilledProductIds ?? Array.Empty<string>(), id) >= 0) return Set(PurchaseState.Owned);
            _busy = true;
            try
            {
                var details = await Details(id);
                if (details == null || !details.available || details.productId != id || string.IsNullOrWhiteSpace(details.formattedPrice)) return Set(PurchaseState.Unavailable);
                Set(PurchaseState.Purchasing);
                var receipt = await _provider.Buy(id);
                if (receipt == null || receipt.productId != id) return Set(PurchaseState.Failed);
                return Apply(receipt);
            }
            catch { return Set(PurchaseState.Retry); }
            finally { _busy = false; Changed?.Invoke(); }
        }
        public async Task<PurchaseState> Restore()
        {
            if (_busy) return PurchaseState.Busy;
            _busy = true;
            try
            {
                Set(PurchaseState.Verifying);
                var receipts = await _provider.Query();
                bool found = false; var incomplete = PurchaseState.Idle;
                foreach (var receipt in receipts ?? Array.Empty<PurchaseReceipt>())
                {
                    var result = Apply(receipt);
                    if (result == PurchaseState.Retry || result == PurchaseState.Pending || result == PurchaseState.Failed) incomplete = result;
                    found |= result == PurchaseState.Owned;
                }
                return Set(incomplete != PurchaseState.Idle ? incomplete : !string.IsNullOrEmpty(_data.pendingProductId) ? PurchaseState.Retry : found ? PurchaseState.Owned : PurchaseState.Unavailable);
            }
            catch { return Set(PurchaseState.Retry); }
            finally { _busy = false; Changed?.Invoke(); }
        }
        PurchaseState Apply(PurchaseReceipt receipt)
        {
            var product = _products.FirstOrDefault(p => p.id == receipt?.productId && p.published);
            if (product == null) return Set(PurchaseState.Failed);
            if (receipt.state == PurchaseState.Cancelled || receipt.state == PurchaseState.Failed || receipt.state == PurchaseState.Unavailable)
            {
                if (_data.pendingProductId == product.id && _data.pendingReceiptId == receipt.receiptId)
                {
                    var pendingId = _data.pendingReceiptId;
                    _data.pendingProductId = null; _data.pendingReceiptId = null;
                    if (!_persist()) { _data.pendingProductId = product.id; _data.pendingReceiptId = pendingId; return Set(PurchaseState.Retry); }
                }
                return Set(receipt.state);
            }
            if (string.IsNullOrWhiteSpace(receipt.receiptId)) return Set(PurchaseState.Failed);
            if (receipt.state == PurchaseState.Pending)
            {
                _data.pendingProductId = product.id; _data.pendingReceiptId = receipt.receiptId;
                return Set(_persist() ? PurchaseState.Pending : PurchaseState.Retry);
            }
            if (receipt.state != PurchaseState.Owned || !receipt.verified) return Set(PurchaseState.Failed);
            var current = string.IsNullOrEmpty(_data.pendingProductId) || _data.pendingReceiptId == receipt.receiptId;
            if (current) { _data.pendingProductId = product.id; _data.pendingReceiptId = receipt.receiptId; }
            Set(PurchaseState.Verifying);
            var applied = _grant(receipt.receiptId, product);
            if (applied != EconomyResult.Applied && applied != EconomyResult.AlreadyApplied) return Set(PurchaseState.Retry);
            var fulfilled = _data.fulfilledProductIds;
            if (!product.Consumable) _data.fulfilledProductIds = (fulfilled ?? Array.Empty<string>()).Append(product.id).Distinct().ToArray();
            if (current) { _data.pendingProductId = null; _data.pendingReceiptId = null; }
            if (_persist()) return Set(PurchaseState.Owned);
            _data.fulfilledProductIds = fulfilled;
            if (current) { _data.pendingProductId = product.id; _data.pendingReceiptId = receipt.receiptId; }
            return Set(PurchaseState.Retry);
        }
        PurchaseState Set(PurchaseState state) { State = state; Changed?.Invoke(); return state; }
    }
#if UNITY_EDITOR || QC_MONETIZATION_PROBE
    public sealed class FakePurchaseProvider : IPurchaseProvider
    {
        readonly List<PurchaseReceipt> _receipts = new List<PurchaseReceipt>();
        public PurchaseState NextState = PurchaseState.Owned;
        public bool Verified = true;
        public Task<PurchaseDetails> Details(string productId) => Task.FromResult(new PurchaseDetails { productId = productId, formattedPrice = "TEST ONLY", available = true });
        public Task<PurchaseReceipt> Buy(string productId)
        {
            var receipt = new PurchaseReceipt { productId = productId, receiptId = "qa:" + Guid.NewGuid().ToString("N"), state = NextState, verified = Verified };
            if (receipt.state == PurchaseState.Owned || receipt.state == PurchaseState.Pending) _receipts.Add(receipt);
            return Task.FromResult(receipt);
        }
        public Task<IReadOnlyList<PurchaseReceipt>> Query()
        {
            foreach (var receipt in _receipts.Where(r => r.state == PurchaseState.Pending)) { receipt.state = NextState; receipt.verified = Verified; }
            return Task.FromResult<IReadOnlyList<PurchaseReceipt>>(_receipts.ToArray());
        }
    }
#endif
}
