using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace QuietCamp.Application
{
    public enum PurchaseState { Idle, Purchasing, Pending, Verifying, Owned, Cancelled, Failed, Retry, Unavailable, Busy, Revoked }
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
        public string productId, receiptId, validUntilUtc;
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
    public sealed class PurchaseService : IDisposable
    {
        readonly IPurchaseProvider _provider;
        readonly PurchaseProduct[] _products;
        readonly PurchaseSaveData _data;
        readonly Func<string, PurchaseProduct, EconomyResult> _grant;
        readonly Func<bool> _persist;
        readonly Func<PurchaseProduct, EconomyResult> _revoke;
        readonly Func<PurchaseReceipt, EconomyResult> _verifiedPro;
        bool _busy, _disposed;
        public event Action Changed;
        public event Action<PurchaseState> StateObserved;
        public PurchaseState State { get; private set; }
        public bool Busy => _busy;
        public IReadOnlyList<PurchaseProduct> Products => _products;
        public PurchaseService(IPurchaseProvider provider, IEnumerable<PurchaseProduct> products, PurchaseSaveData data,
            Func<string, PurchaseProduct, EconomyResult> grant, Func<bool> persist, Func<PurchaseProduct, EconomyResult> revoke = null, Func<PurchaseReceipt, EconomyResult> verifiedPro = null)
        {
            _provider = provider ?? throw new ArgumentNullException(nameof(provider));
            _products = products?.ToArray() ?? Array.Empty<PurchaseProduct>(); _data = data; _grant = grant; _persist = persist; _revoke = revoke; _verifiedPro = verifiedPro;
            if (data.version != 1 || _products.Any(p => p == null || string.IsNullOrWhiteSpace(p.id) || p.currencyAmount < 0)
                || _products.Select(p => p.id).Distinct().Count() != _products.Length) throw new ArgumentException("Invalid purchase catalog");
            State = string.IsNullOrEmpty(data.pendingProductId) ? PurchaseState.Idle : PurchaseState.Retry;
        }
        public Task<PurchaseDetails> Details(string id) => !_disposed && _products.Any(p => p.id == id && p.published)
            ? _provider.Details(id) : Task.FromResult(new PurchaseDetails { productId = id });
        public async Task<PurchaseState> Buy(string id)
        {
            if (_disposed) return PurchaseState.Unavailable;
            if (_busy) return PurchaseState.Busy;
            var product = _products.FirstOrDefault(p => p.id == id && p.published);
            if (product == null) return Set(PurchaseState.Unavailable);
            if (!string.IsNullOrEmpty(_data.pendingProductId)) return Set(PurchaseState.Pending);
            if (!product.Consumable && Array.IndexOf(_data.fulfilledProductIds ?? Array.Empty<string>(), id) >= 0) return await Restore();
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
            if (_disposed) return PurchaseState.Unavailable;
            if (_busy) return PurchaseState.Busy;
            _busy = true;
            try
            {
                Set(PurchaseState.Verifying);
                var receipts = (await _provider.Query() ?? Array.Empty<PurchaseReceipt>()).ToArray();
                bool found = false, revoked = false; var incomplete = PurchaseState.Idle;
                foreach (var receipt in receipts ?? Array.Empty<PurchaseReceipt>())
                {
                    if (receipt != null && receipt.state == PurchaseState.Revoked && receipts.Any(other => other != null && other.productId == receipt.productId
                        && other.state == PurchaseState.Owned && other.verified)) continue;
                    var result = Apply(receipt);
                    if (result == PurchaseState.Retry || result == PurchaseState.Pending || result == PurchaseState.Failed) incomplete = result;
                    found |= result == PurchaseState.Owned; revoked |= result == PurchaseState.Revoked;
                }
                return Set(incomplete != PurchaseState.Idle ? incomplete : !string.IsNullOrEmpty(_data.pendingProductId) ? PurchaseState.Retry : found ? PurchaseState.Owned : revoked ? PurchaseState.Revoked : PurchaseState.Unavailable);
            }
            catch { return Set(PurchaseState.Retry); }
            finally { _busy = false; Changed?.Invoke(); }
        }
        PurchaseState Apply(PurchaseReceipt receipt)
        {
            if (_disposed) return PurchaseState.Unavailable;
            var product = _products.FirstOrDefault(p => p.id == receipt?.productId && p.published);
            if (product == null) return Set(PurchaseState.Failed);
            if (receipt.state == PurchaseState.Revoked)
            {
                if (!receipt.verified || _revoke == null) return Set(PurchaseState.Failed);
                var result = _revoke(product);
                if (result != EconomyResult.Applied && result != EconomyResult.AlreadyApplied) return Set(PurchaseState.Retry);
                var before = _data.fulfilledProductIds;
                var pendingProduct = _data.pendingProductId; var pendingReceipt = _data.pendingReceiptId;
                _data.fulfilledProductIds = (before ?? Array.Empty<string>()).Where(id => id != product.id).ToArray();
                if (_data.pendingReceiptId == receipt.receiptId) { _data.pendingProductId = null; _data.pendingReceiptId = null; }
                if (!_persist()) { _data.fulfilledProductIds = before; _data.pendingProductId = pendingProduct; _data.pendingReceiptId = pendingReceipt; return Set(PurchaseState.Retry); }
                return Set(PurchaseState.Revoked);
            }
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
            var wasFulfilled = Array.IndexOf(_data.fulfilledProductIds ?? Array.Empty<string>(), product.id) >= 0;
            var applied = product.pro && _verifiedPro != null ? _verifiedPro(receipt) : _grant(receipt.receiptId, product);
            if (applied != EconomyResult.Applied && applied != EconomyResult.AlreadyApplied) return Set(PurchaseState.Retry);
            var fulfilled = _data.fulfilledProductIds;
            if (!product.Consumable) _data.fulfilledProductIds = (fulfilled ?? Array.Empty<string>()).Append(product.id).Distinct().ToArray();
            if (current) { _data.pendingProductId = null; _data.pendingReceiptId = null; }
            if (_persist()) {
                if (applied == EconomyResult.Applied && !wasFulfilled) { try { StateObserved?.Invoke(PurchaseState.Owned); } catch { } }
                return Set(PurchaseState.Owned);
            }
            _data.fulfilledProductIds = fulfilled;
            if (current) { _data.pendingProductId = product.id; _data.pendingReceiptId = receipt.receiptId; }
            return Set(PurchaseState.Retry);
        }
        public void Dispose() { _disposed = true; Changed = null; StateObserved = null; (_provider as IDisposable)?.Dispose(); }
        PurchaseState Set(PurchaseState state) { if (state == PurchaseState.Purchasing && State != state) { try { StateObserved?.Invoke(state); } catch { } } State = state; Changed?.Invoke(); return state; }
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
