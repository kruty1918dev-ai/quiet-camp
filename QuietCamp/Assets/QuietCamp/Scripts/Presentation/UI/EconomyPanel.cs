using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using QuietCamp.Application;

namespace QuietCamp.Presentation.UI
{
    public sealed class EconomyPanel : IDisposable
    {
        readonly GameServices _services;
        readonly HtmlSurface _surface;
        readonly Dictionary<string, PurchaseDetails> _details = new Dictionary<string, PurchaseDetails>();
        string _confirm, _confirmLabel, _result;
        Func<EconomyResult> _exchange;
        bool _disposed;
        public EconomyPanel(GameServices services, HtmlSurface surface) { _services = services; _surface = surface; }
        string T(string key) => _services.Localization.T(key);
        public static string Status(GameServices services, string levelId = null)
            => services.Economy.IsPro ? "" : HtmlUi.Text(string.Format(services.Localization.T("economy.status"),
                services.Economy.Lives, services.Economy.Hints), "economy-status");
        public void ResetConfirmation() { _confirm = _confirmLabel = null; _exchange = null; }
        public string Render()
        {
            var e = _services.Economy;
            var html = new StringBuilder("<view class=\"column economy-panel\">");
            html.Append(HtmlUi.Text(T("economy.explain"), "s-sub"));
            bool purchasesAvailable = false;
            if (e.IsPro) html.Append(HtmlUi.Text(T("economy.pro.owned"), "s-sub"));
            else
            {
                html.Append(Status(_services)).Append(HtmlUi.Text(string.Format(T("economy.balance"), e.Currency), "s-sub"));
                html.Append(Exchange("life", T("economy.life"), e.LifeCost, () => e.BuyLives(1), e.Currency >= e.LifeCost));
                html.Append(Exchange("hint", T("action.hint"), e.HintCost, () => e.BuyHints(1), e.Currency >= e.HintCost));
                if (_services.AdLives.Available)
                {
                    html.Append(HtmlUi.Text(string.Format(T("economy.ad.remaining"), e.AdLivesRemaining), "s-sub"));
                    html.Append(HtmlUi.Button(_surface, "life-ad", T("economy.ad.life"), RunAd, "quiet", !_services.MonetizationBusy));
                }
                foreach (var product in _services.Purchases.Products)
                {
                    if (!product.published) continue;
                    var details = Details(product.id);
                    if (details?.available != true || string.IsNullOrWhiteSpace(details.formattedPrice)) continue;
                    purchasesAvailable = true;
                    html.Append(HtmlUi.Button(_surface, "buy-" + product.id, T(product.titleKey) + " · " + details.formattedPrice,
                        () => Buy(product.id), "quiet", !_services.MonetizationBusy));
                }
                if (!purchasesAvailable && !_services.AdLives.Available)
                    html.Append(HtmlUi.Text(T("economy.unavailable"), "s-sub"));
            }
            if (purchasesAvailable || _services.Purchases.State == PurchaseState.Pending || _services.Purchases.State == PurchaseState.Retry)
                html.Append(HtmlUi.Button(_surface, "purchase-restore", T("purchase.restore"), Restore, "quiet", !_services.MonetizationBusy));
            if (_services.Purchases.Busy || _services.Purchases.State == PurchaseState.Pending || _services.Purchases.State == PurchaseState.Retry)
                html.Append(HtmlUi.Text(T("purchase." + _services.Purchases.State), "s-sub"));
            if (_confirm != null)
                html.Append("<view class=\"surface column\">").Append(HtmlUi.Text(_confirmLabel, "s-sub"))
                    .Append(HtmlUi.Text(T("economy.confirm"), "s-sub"))
                    .Append(HtmlUi.Button(_surface, "exchange-confirm", T("action.confirm"), ConfirmExchange, "primary", !_services.MonetizationBusy))
                    .Append(HtmlUi.Button(_surface, "exchange-cancel", T("action.cancel"), () => { ResetConfirmation(); _surface.Refresh(); }, "quiet"))
                    .Append("</view>");
            if (_result != null) html.Append(HtmlUi.Text(T(_result), "s-sub"));
            return html.Append("</view>").ToString();
        }
        string Exchange(string id, string name, int price, Func<EconomyResult> buy, bool allowed)
            => HtmlUi.Button(_surface, "exchange-" + id, string.Format(T("economy.exchange"), name, price), () =>
            {
                if (_services.MonetizationBusy) return;
                _confirm = id; _confirmLabel = string.Format(T("economy.exchange"), name, price); _exchange = buy;
                _result = null; _surface.Refresh();
            }, "quiet", allowed && !_services.MonetizationBusy);
        void ConfirmExchange()
        {
            if (_exchange == null || _services.MonetizationBusy) return;
            var operation = _exchange; ResetConfirmation();
            _result = "economy.result." + operation(); _surface.Refresh();
        }
        PurchaseDetails Details(string id)
        {
            if (_details.TryGetValue(id, out var details)) return details;
            _details[id] = null; LoadDetails(id); return null;
        }
        async void LoadDetails(string id)
        {
            try { var details = await _services.Purchases.Details(id); if (!_disposed) { _details[id] = details; _surface.Refresh(); } }
            catch { if (!_disposed) { _details[id] = new PurchaseDetails { productId = id }; _surface.Refresh(); } }
        }
        async void Buy(string id) => await Run(() => _services.Purchases.Buy(id));
        async void Restore() => await Run(_services.Purchases.Restore);
        async Task Run(Func<Task<PurchaseState>> operation)
        {
            try { var state = await operation(); if (!_disposed) { _result = "purchase." + state; _surface.Refresh(); } }
            catch { if (!_disposed) { _result = "purchase.Retry"; _surface.Refresh(); } }
        }
        async void RunAd()
        {
            try { var result = await _services.AdLives.Request(); if (!_disposed) { _result = "economy.result." + result; _surface.Refresh(); } }
            catch { if (!_disposed) { _result = "purchase.Unavailable"; _surface.Refresh(); } }
        }
        public void Dispose() => _disposed = true;
    }
}
