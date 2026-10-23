using System;
using System.Threading.Tasks;
using NUnit.Framework;
using QuietCamp.Application;

namespace QuietCamp.Tests.Editor
{
    public sealed class ComplimentaryAccessTests
    {
        const string Endpoint = "https://access.quietcamp.dev/v1/access";
        static readonly string Token = new string('a', 43);
        DateTime _now;
        bool _save;
        EconomySaveData _data;
        CampEconomy _economy;
        Transport _transport;
        ComplimentaryAccessService _access;
        sealed class Transport : IComplimentaryAccessTransport
        {
            public ComplimentaryGrant Grant; public int Calls; public TaskCompletionSource<ComplimentaryGrant> Pending;
            public Task<ComplimentaryGrant> SignIn(string username, string password) { Calls++; return Pending?.Task ?? Task.FromResult(Grant); }
            public Task<ComplimentaryGrant> Refresh(string token) { Calls++; return Pending?.Task ?? Task.FromResult(Grant); }
        }
        [SetUp] public void SetUp()
        {
            _now = new DateTime(2026,10,23,0,0,0,DateTimeKind.Utc); _save = true; _data = new EconomySaveData();
            _economy = new CampEconomy(_data,new EconomyRules(),()=>_save,()=>_now); _economy.ConfigureComplimentaryAccess(Endpoint);
            _transport = new Transport { Grant = new ComplimentaryGrant { packageName="com.quietcamp.game", productId="quietcamp_pro", scope="pro", state="Granted", sessionToken=Token, validUntilUtc=_now.AddDays(1).ToString("O") } };
            _access = new ComplimentaryAccessService(_economy,_transport,"com.quietcamp.game","quietcamp_pro");
        }
        [Test] public async Task ComplimentaryGrantUsesNormalProWithoutPurchasesOrChangingResources()
        {
            var permissions=new PrivacyPermissionService(()=>new PrivacyPermissionInputs { Context="adult", Adult=true,
                AdvertisingAvailable=true, AdvertisingChoice=true, AnalyticsAvailable=true, AnalyticsChoice=true, Pro=_economy.IsPro });
            permissions.AcceptUmp(true,"1114"); Assert.IsTrue(permissions.Decisions.CanRequestAds);
            int lives=_economy.Lives,hints=_economy.Hints;
            Assert.AreEqual(ComplimentaryAccessState.Granted,await _access.SignIn("play-review","private-test-password"));
            Assert.IsTrue(_economy.IsPro && _economy.CanCheck && _economy.CanHint); Assert.IsFalse(_data.pro);
            Assert.IsFalse(permissions.Decisions.CanStartAdConsent || permissions.Decisions.CanRequestAds);
            Assert.IsTrue(permissions.Decisions.AnalyticsAllowed,"Pro does not overwrite independent Analytics consent");
            Assert.AreEqual(lives,_economy.Lives); Assert.AreEqual(hints,_economy.Hints);
            _economy.SetProEntitlement(false); Assert.IsTrue(_economy.IsPro,"Play refund cannot revoke a separate complimentary grant");
            Assert.IsFalse(new CampEconomy(_data,new EconomyRules(),()=>true,()=>_now).IsPro,"No grant without matching owner configuration");
            var restored=new CampEconomy(_data,new EconomyRules(),()=>true,()=>_now); restored.ConfigureComplimentaryAccess(Endpoint);
            Assert.IsTrue(restored.IsPro); _now=_now.AddDays(2); Assert.IsFalse(restored.IsPro);
        }
        [Test] public async Task SignOutAndDenialDoNotRemoveAnIndependentPurchase()
        {
            _economy.GrantVerifiedPro("real-receipt",_now.AddDays(1).ToString("O"));
            await _access.SignIn("play-review","private-test-password"); _access.SignOut();
            Assert.IsTrue(_economy.IsPro); Assert.IsFalse(_economy.HasComplimentaryPro);
            await _access.SignIn("play-review","private-test-password"); _transport.Grant=new ComplimentaryGrant {state="Denied"};
            Assert.AreEqual(ComplimentaryAccessState.Denied,await _access.Refresh()); Assert.IsTrue(_economy.IsPro);
        }
        [TestCase("package")] [TestCase("product")] [TestCase("scope")] [TestCase("token")] [TestCase("expiry")]
        public async Task InvalidServerGrantCannotUnlockPro(string invalid)
        {
            if(invalid=="package")_transport.Grant.packageName="other";
            if(invalid=="product")_transport.Grant.productId="other";
            if(invalid=="scope")_transport.Grant.scope="all";
            if(invalid=="token")_transport.Grant.sessionToken="invalid";
            if(invalid=="expiry")_transport.Grant.validUntilUtc=_now.AddDays(3).ToString("O");
            Assert.AreEqual(ComplimentaryAccessState.Denied,await _access.SignIn("play-review","password")); Assert.IsFalse(_economy.IsPro);
        }
        [Test] public async Task SaveFailuresFailClosedAndNetworkFailurePreservesOnlyExistingLease()
        {
            _save=false; Assert.AreEqual(ComplimentaryAccessState.SaveFailed,await _access.SignIn("play-review","password")); Assert.IsFalse(_economy.IsPro);
            _save=true; await _access.SignIn("play-review","password"); _transport.Grant=null;
            Assert.AreEqual(ComplimentaryAccessState.Retry,await _access.Refresh()); Assert.IsTrue(_economy.IsPro);
            _save=false; _access.SignOut(); Assert.IsFalse(_economy.IsPro);
        }
        [Test] public async Task SignOutRejectsLateCallbackAndDisabledTransportMakesNoRequest()
        {
            _transport.Pending=new TaskCompletionSource<ComplimentaryGrant>();
            var pending=_access.SignIn("play-review","password"); _access.SignOut(); _transport.Pending.SetResult(_transport.Grant);
            await pending; Assert.IsFalse(_economy.IsPro);
            var disabled=new ComplimentaryAccessService(_economy,null,"com.quietcamp.game","quietcamp_pro");
            Assert.AreEqual(ComplimentaryAccessState.Unavailable,await disabled.SignIn("play-review","password"));
        }
        [Test] public async Task RepeatedClickAndRefreshWithDifferentTokenCannotCreateAnotherGrant()
        {
            _transport.Pending=new TaskCompletionSource<ComplimentaryGrant>();
            var pending=_access.SignIn("play-review","password"); await _access.SignIn("play-review","password");
            Assert.AreEqual(1,_transport.Calls); _transport.Pending.SetResult(_transport.Grant); await pending;
            _transport.Pending=null; _transport.Grant.sessionToken=new string('b',43);
            Assert.AreEqual(ComplimentaryAccessState.Denied,await _access.Refresh()); Assert.AreEqual(Token,_economy.ComplimentarySessionToken);
        }
        [Test] public async Task ComplimentaryProOpensIncludedJourneysWithoutUnlockingUnpublishedOrFutureDlc()
        {
            await _access.SignIn("play-review","password");
            var included=new JourneyDefinition {id="included",published=true,includedInPro=true,entitlementId="pro.included",levelIds=new[]{"included.1"}};
            var future=new JourneyDefinition {id="future",published=true,entitlementId="dlc.future",levelIds=new[]{"future.1"}};
            var access=new JourneyAccessService(new JourneyCatalog(new[]{included,future}),new ProgressionService(),new EntitlementSaveData(),()=>_economy.IsPro);
            Assert.IsTrue(access.Evaluate("included.1").CanStart);
            Assert.AreEqual(JourneyAccessState.PurchaseRequired,access.Evaluate("future.1").State);
        }
    }
}
