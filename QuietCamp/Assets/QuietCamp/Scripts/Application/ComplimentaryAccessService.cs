using System;
using System.Threading.Tasks;

namespace QuietCamp.Application
{
    public enum ComplimentaryAccessState { Unavailable, SigningIn, Granted, Denied, Retry, SaveFailed, SignedOut }
    [Serializable] public sealed class ComplimentaryGrant
    {
        public string packageName, productId, scope, state, sessionToken, validUntilUtc;
    }
    public interface IComplimentaryAccessTransport
    {
        Task<ComplimentaryGrant> SignIn(string username, string password);
        Task<ComplimentaryGrant> Refresh(string token);
    }
    // Complimentary entitlements do not impersonate a Play purchase or change privacy choices.
    public sealed class ComplimentaryAccessService : IDisposable
    {
        readonly CampEconomy _economy;
        readonly IComplimentaryAccessTransport _transport;
        readonly string _package, _product;
        int _generation;
        bool _disposed;
        public bool Configured => !_disposed && _transport != null;
        public bool Busy { get; private set; }
        public ComplimentaryAccessState State { get; private set; } = ComplimentaryAccessState.Unavailable;
        public event Action Changed;
        public ComplimentaryAccessService(CampEconomy economy, IComplimentaryAccessTransport transport, string package, string product)
        { _economy = economy; _transport = transport; _package = package; _product = product; }
        public Task<ComplimentaryAccessState> SignIn(string username, string password)
        {
            if (string.IsNullOrWhiteSpace(username) || username.Length > 100 || string.IsNullOrEmpty(password) || password.Length > 128)
                return Task.FromResult(Set(ComplimentaryAccessState.Denied));
            return Request(() => _transport.SignIn(username, password), null);
        }
        public Task<ComplimentaryAccessState> Refresh()
        {
            string token = _economy.ComplimentarySessionToken;
            return string.IsNullOrEmpty(token) ? Task.FromResult(State) : Request(() => _transport.Refresh(token), token);
        }
        async Task<ComplimentaryAccessState> Request(Func<Task<ComplimentaryGrant>> request, string expectedToken)
        {
            if (!Configured || Busy) return State;
            Busy = true; int generation = _generation; Set(ComplimentaryAccessState.SigningIn);
            try {
                var grant = await request();
                if (generation != _generation) return State;
                if (grant == null) return Set(ComplimentaryAccessState.Retry);
                if (grant.state == "Denied") { _economy.RevokeComplimentaryPro(); return Set(ComplimentaryAccessState.Denied); }
                if (grant.state != "Granted" || grant.packageName != _package || grant.productId != _product || grant.scope != "pro"
                    || (expectedToken != null && expectedToken != grant.sessionToken)) return Set(ComplimentaryAccessState.Denied);
                var result = _economy.GrantComplimentaryPro(grant.sessionToken, grant.validUntilUtc);
                return Set(result == EconomyResult.Applied ? ComplimentaryAccessState.Granted
                    : result == EconomyResult.SaveFailed ? ComplimentaryAccessState.SaveFailed : ComplimentaryAccessState.Denied);
            }
            catch { return generation == _generation ? Set(ComplimentaryAccessState.Retry) : State; }
            finally { Busy = false; Changed?.Invoke(); }
        }
        public void SignOut() { _generation++; Set(_economy.RevokeComplimentaryPro() == EconomyResult.SaveFailed ? ComplimentaryAccessState.SaveFailed : ComplimentaryAccessState.SignedOut); }
        public void Dispose() { _disposed = true; _generation++; }
        ComplimentaryAccessState Set(ComplimentaryAccessState state) { State = state; Changed?.Invoke(); return state; }
    }
}
