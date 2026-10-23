#if UNITY_ANDROID && !UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using QuietCamp.Application;
using QuietCamp.Infrastructure;
using UnityEngine;
using UnityEngine.Networking;

// JNI boundary to the real Play Billing Library; never fabricates price or ownership.
sealed class PlayBillingProvider : IPurchaseProvider, IDisposable
{
    [Serializable] sealed class NativePurchase { public string productId, purchaseToken; }
    [Serializable] sealed class NativeResult
    { public string state, productId, price; public NativePurchase[] purchases; }
    [Serializable] sealed class VerificationRequest { public string packageName, productId, purchaseToken; }
    [Serializable] sealed class VerificationResponse
    { public string productId, receiptId, state, validUntilUtc; public bool verified; }
    readonly OwnerClientConfiguration _config;
    readonly Func<bool> _canBuy;
    AndroidJavaObject _bridge;
    bool _disposed;
    public PlayBillingProvider(OwnerClientConfiguration config, Func<bool> canBuy) { _config = config; _canBuy = canBuy; }
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Register()
    {
        OptionalBilling.Factory = (config, allowed) => new PlayBillingProvider(config, allowed);
        OptionalBilling.ClearLocalHistory = () => {
            try {
                using (var unity = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                using (var activity = unity.GetStatic<AndroidJavaObject>("currentActivity"))
                using (var bridge = new AndroidJavaClass("com.quietcamp.game.billing.PlayBillingBridge"))
                    return bridge.CallStatic<bool>("clearHistory", activity);
            } catch { return false; }
        };
    }
    AndroidJavaObject Bridge()
    {
        if (_disposed) throw new ObjectDisposedException(nameof(PlayBillingProvider));
        if (_bridge != null) return _bridge;
        using (var unity = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
        using (var activity = unity.GetStatic<AndroidJavaObject>("currentActivity"))
            return _bridge = new AndroidJavaObject("com.quietcamp.game.billing.PlayBillingBridge", activity);
    }
    async Task<NativeResult> Native(string operation, string product)
    {
        var bridge = Bridge();
        int id = bridge.Call<int>("request", operation, product);
        // A live Play purchase UI has no timer; do not permit a second buy while
        // the store UI may still be open. Connection/product queries time out.
        var deadline = operation == "buy" ? DateTime.MaxValue : DateTime.UtcNow.AddSeconds(30);
        while (!_disposed)
        {
            string response = bridge.Call<string>("poll", id);
            if (!string.IsNullOrEmpty(response)) return JsonUtility.FromJson<NativeResult>(response);
            if (DateTime.UtcNow >= deadline) { bridge.Call("abandon", id); throw new TimeoutException(); }
            await Task.Delay(100);
        }
        throw new ObjectDisposedException(nameof(PlayBillingProvider));
    }
    public async Task<PurchaseDetails> Details(string productId)
    {
        var details = new PurchaseDetails { productId = productId };
        if (!_canBuy() || productId != _config.proProductId) return details;
        var result = await Native("details", productId);
        details.available = result.state == "Available" && result.productId == productId && !string.IsNullOrWhiteSpace(result.price);
        details.formattedPrice = details.available ? result.price : null;
        return details;
    }
    public async Task<PurchaseReceipt> Buy(string productId)
    {
        if (!_canBuy() || productId != _config.proProductId) return new PurchaseReceipt { productId = productId, state = PurchaseState.Unavailable };
        var native = await Native("buy", productId);
        if (native.state == "Cancelled") return new PurchaseReceipt { productId = productId, state = PurchaseState.Cancelled };
        if (native.purchases == null || native.purchases.Length != 1) return new PurchaseReceipt { productId = productId, state = PurchaseState.Retry };
        return await Verify(native.purchases[0]);
    }
    public async Task<IReadOnlyList<PurchaseReceipt>> Query()
    {
        // Recovery fulfills a prior purchase, independent of tracking consent and
        // eligibility to start a new purchase. Unconfigured services stay offline.
        if (!LegalConfiguration.PublishedUrl(_config.verificationEndpoint)) return Array.Empty<PurchaseReceipt>();
        var native = await Native("query", _config.proProductId);
        if (native.state != "Available") throw new InvalidOperationException("Store unavailable");
        var result = new List<PurchaseReceipt>();
        foreach (var purchase in native.purchases ?? Array.Empty<NativePurchase>()) result.Add(await Verify(purchase));
        return result;
    }
    async Task<PurchaseReceipt> Verify(NativePurchase purchase)
    {
        if (purchase.productId != _config.proProductId || string.IsNullOrWhiteSpace(purchase.purchaseToken)) throw new InvalidOperationException("Unexpected store product");
        var payload = JsonUtility.ToJson(new VerificationRequest { packageName = _config.androidPackageName,
            productId = purchase.productId, purchaseToken = purchase.purchaseToken });
        using (var request = new UnityWebRequest(_config.verificationEndpoint, "POST"))
        {
            request.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(payload));
            request.downloadHandler = new DownloadHandlerBuffer(); request.timeout = 20; request.redirectLimit = 0;
            request.SetRequestHeader("Content-Type", "application/json");
            var sent = request.SendWebRequest();
            while (!sent.isDone) { if (_disposed) { request.Abort(); throw new ObjectDisposedException(nameof(PlayBillingProvider)); } await Task.Yield(); }
            if (request.result != UnityWebRequest.Result.Success) throw new InvalidOperationException("Verification unavailable");
            var verified = JsonUtility.FromJson<VerificationResponse>(request.downloadHandler.text);
            if (verified == null || verified.productId != purchase.productId || string.IsNullOrWhiteSpace(verified.receiptId)
                || !Enum.TryParse(verified.state, out PurchaseState state)
                || (state != PurchaseState.Owned && state != PurchaseState.Pending && state != PurchaseState.Revoked))
                throw new InvalidOperationException("Invalid verification response");
            return new PurchaseReceipt { productId = verified.productId, receiptId = verified.receiptId,
                state = state, verified = verified.verified, validUntilUtc = verified.validUntilUtc };
        }
    }
    public void Dispose() { _disposed = true; if (_bridge != null) { _bridge.Call("close"); _bridge.Dispose(); _bridge = null; } }
}
#endif
