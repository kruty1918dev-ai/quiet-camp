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
        string _accessUsername = "", _accessPassword = "";
        Func<EconomyResult> _exchange;
        bool _disposed, _offerObserved;
        public EconomyPanel(GameServices services, HtmlSurface surface) { _services = services; _surface = surface; }
        string T(string key) => _services.Localization.T(key);
        public static string Status(GameServices services, string levelId = null)
            => services.Economy.IsPro ? "" : HtmlUi.Text(string.Format(services.Localization.T("economy.status"),
                services.Economy.Lives, services.Economy.Hints), "economy-status");
        public void ResetConfirmation() { _confirm = _confirmLabel = null; _exchange = null; _accessUsername = _accessPassword = ""; }
        public string Render()
        {
            var e = _services.Economy;
            if (!_offerObserved && !e.IsPro) { _offerObserved = true; _services.Analytics.ProOffer(); }
            var html = new StringBuilder("<view class=\"column economy-panel\">");
            html.Append(HtmlUi.Text(T("economy.explain"), "s-sub"));
            bool purchasesAvailable = false;
            if (e.IsPro) html.Append(HtmlUi.Text(T("economy.pro.owned"), "s-sub"));
            else
            {
                html.Append(Status(_services)).Append(HtmlUi.Text(string.Format(T("economy.balance"), e.Currency), "s-sub"));
                if (e.Lives < e.LifeCapacity)
                    html.Append(HtmlUi.Text(string.Format(T("economy.recovery"), Math.Max(1, (int)Math.Ceiling(e.SecondsUntilNextLife / 60d)), e.LifeCapacity), "s-sub"));
                html.Append(Exchange("life", T("economy.life"), e.LifeCost, () => e.BuyLives(1), e.Currency >= e.LifeCost));
                html.Append(Exchange("hint", T("action.hint"), e.HintCost, () => e.BuyHints(1), e.Currency >= e.HintCost));
                if (_services.AdLives.Available)
                {
                    html.Append(HtmlUi.Text(string.Format(T("economy.ad.remaining"), e.AdLivesRemaining), "s-sub"));
                    html.Append(HtmlUi.Button(_surface, "life-ad", T("economy.ad.life"), RunAd, "quiet", !_services.MonetizationBusy));
                }
                if (_services.AdHints.Available)
                {
                    html.Append(HtmlUi.Text(string.Format(T("economy.ad.hintsRemaining"), e.AdHintsRemaining), "s-sub"));
                    html.Append(HtmlUi.Button(_surface, "hint-ad", T("economy.ad.hint"), RunHintAd, "quiet", !_services.MonetizationBusy));
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
            html.Append(RenderComplimentaryAccess());
            return html.Append("</view>").ToString();
        }
        string RenderComplimentaryAccess()
        {
            var access = _services.ComplimentaryAccess;
            var html = new StringBuilder("<view class=\"surface column\">").Append(HtmlUi.Text(T("access.title"), "s-sub"));
            if (!access.Configured) return html.Append(HtmlUi.Text(T("access.Unavailable"), "s-sub")).Append("</view>").ToString();
            html.Append(HtmlUi.Text(T("access.explain"), "s-sub"));
            if (_services.Economy.HasComplimentaryPro) {
                html.Append(HtmlUi.Text(T("access.Granted"), "s-sub"));
                html.Append(HtmlUi.Button(_surface, "access-sign-out", T("access.signOut"), () => { access.SignOut(); _surface.Refresh(); }, "quiet", !access.Busy));
            } else {
                _surface.Callbacks.BindText("access-username", value => _accessUsername = (value ?? "").Length <= 100 ? value : "");
                _surface.Callbacks.BindText("access-password", value => _accessPassword = (value ?? "").Length <= 128 ? value : "");
                html.Append(HtmlUi.Text(T("access.username"), "s-sub"))
                    .Append("<input id=\"access-username\" characterLimit=\"100\" richText=\"false\" style=\"height:56px;min-height:56px;background-color:#edf2ec;color:#17342b;font-size:18px;\" onChange=\"Globals.campUi.Text('access-username', event)\" />")
                    .Append(HtmlUi.Text(T("access.password"), "s-sub"))
                    .Append("<input id=\"access-password\" contentType=\"Password\" characterLimit=\"128\" richText=\"false\" style=\"height:56px;min-height:56px;background-color:#edf2ec;color:#17342b;font-size:18px;\" onChange=\"Globals.campUi.Text('access-password', event)\" />")
                    .Append(HtmlUi.Button(_surface, "access-sign-in", T("access.signIn"), SignInComplimentary, "quiet", !_services.MonetizationBusy));
                if (access.State != ComplimentaryAccessState.Unavailable) html.Append(HtmlUi.Text(T("access." + access.State), "s-sub"));
            }
            return html.Append("</view>").ToString();
        }
        async void SignInComplimentary()
        {
            if (_services.MonetizationBusy) return;
            string username = _accessUsername, password = _accessPassword;
            _accessUsername = _accessPassword = "";
            foreach (string id in new[] { "access-username", "access-password" }) {
                var input = _surface.Element(id)?.GetComponent<TMPro.TMP_InputField>();
                if (input != null) input.SetTextWithoutNotify("");
            }
            try { var pending = _services.ComplimentaryAccess.SignIn(username, password); _surface.Refresh(); await pending; }
            finally { if (!_disposed) _surface.Refresh(); }
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
            if (_services.MonetizationBusy) return;
            try { var result = await RewardedVideoPresentation.Run(_services, _surface, _services.AdLives.Request); if (!_disposed) { _result = "economy.result." + result; _surface.Refresh(); } }
            catch { if (!_disposed) { _result = "purchase.Unavailable"; _surface.Refresh(); } }
        }
        async void RunHintAd()
        {
            if (_services.MonetizationBusy) return;
            try { var result = await RewardedVideoPresentation.Run(_services, _surface, _services.AdHints.Request); if (!_disposed) { _result = "economy.result." + result; _surface.Refresh(); } }
            catch { if (!_disposed) { _result = "purchase.Unavailable"; _surface.Refresh(); } }
        }
        public void Dispose() { _disposed = true; _accessUsername = _accessPassword = ""; }
    }
}
