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
        string _confirm, _result;
        bool _disposed;
        public EconomyPanel(GameServices services, HtmlSurface surface) { _services = services; _surface = surface; }
        string T(string key) => _services.Localization.T(key);
        public static string Status(GameServices services, string levelId = null)
        {
            if (services.Economy.IsPro) return "";
            var economy = services.Economy;
            var text = string.Format(services.Localization.T("economy.status"), economy.Lives, economy.Hints);
            if (levelId != null) text += " · " + string.Format(services.Localization.T("economy.attempts"), economy.Attempts(levelId));
            return HtmlUi.Text(text, "economy-status");
        }
        public string Render()
        {
            var e = _services.Economy;
            var html = new StringBuilder("<view class=\"column economy-panel\">");
            html.Append(HtmlUi.Text(T("economy.explain"), "s-sub"));
            if (e.IsPro) html.Append(HtmlUi.Text(T("economy.pro.owned"), "s-sub"));
            else
            {
                html.Append(Status(_services)).Append(HtmlUi.Text(string.Format(T("economy.balance"), e.Currency), "s-sub"));
                html.Append(Exchange("life", T("economy.life"), e.LifeCost, () => e.BuyLives(1), e.Currency >= e.LifeCost));
                foreach (var count in new[] { 5, 10 })
                    if ((long)e.LifeCost * count <= int.MaxValue)
                    {
                        var quantity = count; var cost = e.LifeCost * quantity;
                        html.Append(Exchange("lives-" + quantity, string.Format(T("economy.lifePack"), quantity), cost, () => e.BuyLives(quantity), e.Currency >= cost));
                    }
                html.Append(Exchange("hint", T("action.hint"), e.HintCost, () => e.BuyHints(1), e.Currency >= e.HintCost));
                html.Append(HtmlUi.Text(string.Format(T("economy.ad.remaining"), e.AdLivesRemaining), "s-sub"));
                html.Append(HtmlUi.Button(_surface, "life-ad", T("economy.ad.life"), () => RunAd(), "quiet", _services.AdLives.Available && !_services.MonetizationBusy));
                foreach (var product in _services.Purchases.Products)
                {
                    var details = Details(product.id);
                    var ready = product.published && details?.available == true && !_services.MonetizationBusy;
                    var label = T(product.titleKey) + " · " + (ready ? details.formattedPrice : T("purchase.Unavailable"));
                    html.Append(HtmlUi.Button(_surface, "buy-" + product.id, label, () => Buy(product.id), "quiet", ready));
                }
                html.Append(HtmlUi.Text(T("economy.pro.description"), "s-sub"));
            }
            html.Append(HtmlUi.Button(_surface, "purchase-restore", T("purchase.restore"), Restore, "quiet", !_services.MonetizationBusy));
            html.Append(HtmlUi.Text(T("purchase." + _services.Purchases.State), "s-sub"));
            if (_confirm != null) html.Append(HtmlUi.Text(T("economy.confirm"), "s-sub"));
            if (_result != null) html.Append(HtmlUi.Text(T(_result), "s-sub"));
            return html.Append("</view>").ToString();
        }
        string Exchange(string id, string name, int price, Func<EconomyResult> buy, bool allowed)
            => HtmlUi.Button(_surface, "exchange-" + id, string.Format(T("economy.exchange"), name, price), () =>
            {
                if (_services.MonetizationBusy) return;
                if (_confirm != id) { _confirm = id; _surface.Refresh(); return; }
                _confirm = null; _result = "economy.result." + buy(); _surface.Refresh();
            }, "quiet", allowed && !_services.MonetizationBusy);
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
