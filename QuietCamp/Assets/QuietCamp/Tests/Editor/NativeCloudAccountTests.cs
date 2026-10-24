using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using QuietCamp.Application;

namespace QuietCamp.Tests
{
    public sealed class NativeCloudAccountTests
    {
        const string Endpoint="https://cloud.quietcamp.test/v1/cloud";
        static readonly string Token=new string('a',64);
        sealed class Store : ICloudCredentialStore
        {
            public CloudCredential Value; public bool Fail;
            public CloudCredential Read()=>Copy(Value);
            public bool Write(CloudCredential value) { if(Fail)return false; Value=Copy(value); return true; }
            static CloudCredential Copy(CloudCredential value)=>value==null ? null : JsonConvert.DeserializeObject<CloudCredential>(JsonConvert.SerializeObject(value));
        }
        sealed class Identity : ICloudNativeIdentity
        {
            public string Uid="firebase-one";
            public int AuthCalls, ClearCalls;
            public Func<CancellationToken,Task<CloudIdentityProof>> Handler;
            public bool Available(string provider)=>provider=="google";
            public Task<CloudIdentityProof> Authenticate(string provider,CancellationToken cancel) {
                AuthCalls++; return Handler?.Invoke(cancel) ?? Task.FromResult(new CloudIdentityProof {FirebaseUid=Uid,FirebaseIdToken="fresh-firebase-token"});
            }
            public Task SignOut() { ClearCalls++; return Task.CompletedTask; }
        }
        sealed class Network : ICloudTransport
        {
            public readonly List<(string route,string token,JObject data)> Calls=new List<(string,string,JObject)>();
            public Func<string,CloudReply> Reply;
            public Func<string,Task<CloudReply>> Handler;
            public Task<CloudReply> Call(string route,string token,object data=null) {
                Calls.Add((route,token,data==null?null:JObject.FromObject(data)));
                return Handler?.Invoke(route) ?? Task.FromResult(Reply(route));
            }
        }
        Store _store; Identity _identity; Network _network; CloudSaveService _cloud;
        SettingsSaveData _settings; CloudSnapshot _local, _remote;
        bool _persist; int _writes;
        static CloudSnapshot Snapshot(params string[] ids)=>new CloudSnapshot {progress=new ProgressSaveData {completedIds=ids},
            session=new SessionSaveData(),album=new AlbumSaveData(),memories=new MemorySaveData()};
        CloudReply Reply(string route) {
            if(route=="options")return new CloudReply {status=200,providers=new[]{"google","facebook"},nativeSignIn=true,accountPortal=true};
            if(route=="native/sign-in")return new CloudReply {status=200,provider="google",accountId="shared-website-account",sessionToken=Token,
                created=false,profile=new CloudAccountProfile {accountId="shared-website-account",displayName="<Camper>",email=null,providers=new[]{"google"}}};
            if(route=="read")return new CloudReply {status=200,revision=_remote==null?0:5,snapshot=_remote};
            if(route=="write") { _writes++; return new CloudReply {status=200,revision=6}; }
            if(route=="native/delete")return new CloudReply {status=200,deleted=true};
            return new CloudReply {status=200};
        }
        void Create(bool returning=false) {
            _cloud?.Dispose();
            _settings=new SettingsSaveData {accountChoiceVersion=returning?CloudSaveService.ChoiceVersion:0};
            _cloud=new CloudSaveService(_settings,_store,_network,Endpoint,true,()=>true,()=>_local,
                snapshot=>{_local=snapshot;return true;},()=>_persist,nativeIdentity:_identity);
        }
        [SetUp] public void Setup() {
            _store=new Store();_identity=new Identity();_network=new Network {Reply=Reply};
            _persist=true;_local=Snapshot("QC001");_remote=null;_writes=0;Create();
        }
        [TearDown] public void TearDown()=>_cloud.Dispose();
        async Task Login() { await _cloud.LoadOptions();await _cloud.SignInNative("google",false); }
        [Test] public async Task NativeUsesFreshFirebaseProofAndSameWebsiteAccountIdWithoutTrackingConsent() {
            await Login();
            Assert.That(_cloud.AccountId,Is.EqualTo("shared-website-account"));
            var request=_network.Calls.Find(c=>c.route=="native/sign-in");
            Assert.That((string)request.data["idToken"],Is.EqualTo("fresh-firebase-token"));
            Assert.That((string)request.data["mode"],Is.EqualTo("signin"));
            Assert.That(_store.Value.firebaseUid,Is.EqualTo("firebase-one"));
            Assert.That(JsonConvert.SerializeObject(_store.Value),Does.Not.Contain("fresh-firebase-token"));
            Assert.That(_settings.analyticsConsent || _settings.crashReportingConsent || _settings.advertisingConsent,Is.False);
            Assert.That(_cloud.Profile.email,Is.Null);Assert.That(_cloud.Profile.displayName,Is.EqualTo("<Camper>"));
            Assert.That(_identity.ClearCalls,Is.EqualTo(1));
        }
        [Test] public async Task EmptyWebsiteProfileUsesReadThenCasZeroAndKeepsResources() {
            _local.resources.currency=73; await Login();await _cloud.Sync();
            var write=_network.Calls.Find(c=>c.route=="write");
            Assert.That((long)write.data["expectedRevision"],Is.EqualTo(0));
            Assert.That((int)write.data["snapshot"]["resources"]["currency"],Is.EqualTo(73));
            Assert.That(_cloud.Status,Is.EqualTo(CloudSaveStatus.Saved));
            Assert.That(_network.Calls.FindIndex(c=>c.route=="read"),Is.LessThan(_network.Calls.FindIndex(c=>c.route=="write")));
        }
        [Test] public async Task FirstEntryRestoresNonemptyCloudWithoutAddingBalances() {
            _remote=Snapshot("QC001","QC002");_remote.resources.currency=19;
            await Login();await _cloud.Sync();
            Assert.That(_local.progress.completedIds,Has.Length.EqualTo(2));Assert.That(_local.resources.currency,Is.EqualTo(19));Assert.That(_writes,Is.Zero);
        }
        [Test] public async Task ReturningGuestSigninProtectsDeviceProgressWithConflict() {
            Create(true);_remote=Snapshot("QC002");await Login();await _cloud.Sync();
            Assert.That(_cloud.Status,Is.EqualTo(CloudSaveStatus.Conflict));Assert.That(_local.progress.completedIds,Is.EqualTo(new[]{"QC001"}));
            Assert.That(_writes,Is.Zero);await _cloud.ResolveConflict(false);Assert.That(_writes,Is.EqualTo(1));
            Assert.That((long)_network.Calls.Find(c=>c.route=="write").data["expectedRevision"],Is.EqualTo(5));
        }
        [Test] public async Task OccupiedGuestLinkIsDistinctFromSigninEvenWhenWebsiteHasNoSave() {
            await _cloud.LoadOptions();
            _network.Reply=route=>route=="native/sign-in"?new CloudReply {status=409,error="identity_already_linked"}:Reply(route);
            await _cloud.SignInNative("google",true);
            Assert.That((string)_network.Calls.Find(c=>c.route=="native/sign-in").data["mode"],Is.EqualTo("link"));
            Assert.That(_cloud.Status,Is.EqualTo(CloudSaveStatus.LinkedElsewhere));Assert.That(_cloud.HasAccount,Is.False);Assert.That(_writes,Is.Zero);
        }
        [Test] public async Task OldServerAndFacebookLeaveBrowserFlowAvailableWithoutNativeAttempt() {
            _network.Reply=route=>route=="options"?new CloudReply {status=200,providers=new[]{"google","facebook"}}:Reply(route);
            await _cloud.LoadOptions();await _cloud.SignInNative("google",false);
            Assert.That(_cloud.CanSignInNatively("google"),Is.False);Assert.That(_cloud.CanSignInNatively("facebook"),Is.False);
            Assert.That(_identity.AuthCalls,Is.Zero);Assert.That(_cloud.Providers,Does.Contain("facebook"));
        }
        [TestCase(false)] [TestCase(true)] public async Task CancelOrDisposeRevokesLateServerSession(bool dispose) {
            await _cloud.LoadOptions();var pending=new TaskCompletionSource<CloudReply>();
            _network.Handler=route=>route=="native/sign-in"?pending.Task:Task.FromResult(Reply(route));
            var login=_cloud.SignInNative("google",false);
            if(dispose)_cloud.Dispose();else _cloud.Cancel();pending.SetResult(Reply("native/sign-in"));await login;
            Assert.That(_cloud.HasAccount,Is.False);Assert.That(_store.Value,Is.Null);
            Assert.That(_network.Calls.Exists(c=>c.route=="sign-out" && c.token==Token),Is.True);
        }
        [TestCase(false)] [TestCase(true)] public async Task FailedCredentialOrChoicePersistenceRevokesIssuedSession(bool choiceFailure) {
            await _cloud.LoadOptions();if(choiceFailure)_persist=false;else _store.Fail=true;
            await _cloud.SignInNative("google",false);
            Assert.That(_cloud.HasAccount,Is.False);Assert.That(_store.Value,Is.Null);
            Assert.That(_network.Calls.Exists(c=>c.route=="sign-out"),Is.True);Assert.That(_cloud.Status,Is.EqualTo(CloudSaveStatus.StorageError));
        }
        [Test] public async Task CancelDuringChooserNeverCallsServer() {
            await _cloud.LoadOptions();
            _identity.Handler=async cancel=>{await Task.Delay(Timeout.Infinite,cancel);return null;};
            var login=_cloud.SignInNative("google",false);_cloud.Cancel();await login;
            Assert.That(_network.Calls.Exists(c=>c.route=="native/sign-in"),Is.False);Assert.That(_cloud.Status,Is.EqualTo(CloudSaveStatus.Cancelled));
        }
        [Test] public async Task NativeChooserCancellationIsNotReportedAsTimeout() {
            await _cloud.LoadOptions();_identity.Handler=_=>Task.FromCanceled<CloudIdentityProof>(new CancellationToken(true));
            await _cloud.SignInNative("google",false);Assert.That(_cloud.Status,Is.EqualTo(CloudSaveStatus.Cancelled));
        }
        [Test] public async Task NoCredentialsIsRetryableAndDoesNotSilentlyOpenBrowser() {
            await _cloud.LoadOptions();_identity.Handler=_=>Task.FromException<CloudIdentityProof>(new CloudIdentityException(true));
            await _cloud.SignInNative("google",false);Assert.That(_cloud.Status,Is.EqualTo(CloudSaveStatus.NoCredentials));
            Assert.That(_network.Calls.Exists(c=>c.route=="login-start" || c.route=="native/sign-in"),Is.False);
        }
        [Test] public async Task SignoutAndDifferentUserRequireNewInteractiveProof() {
            await Login();await _cloud.SignOut();_identity.Uid="firebase-two";
            _network.Reply=route=>route=="native/sign-in"?new CloudReply {status=200,provider="google",accountId="second-account",sessionToken=Token,created=true}:Reply(route);
            await Login();Assert.That(_identity.AuthCalls,Is.EqualTo(2));Assert.That(_store.Value.firebaseUid,Is.EqualTo("firebase-two"));
            Assert.That(_cloud.AccountId,Is.EqualTo("second-account"));
        }
        [Test] public async Task OfflineSigninKeepsLocalProgressAndNeverStoresProviderProof() {
            _network.Reply=route=>route=="native/sign-in"?new CloudReply {status=503}:Reply(route);
            await Login();Assert.That(_cloud.Status,Is.EqualTo(CloudSaveStatus.Offline));Assert.That(_store.Value,Is.Null);
            Assert.That(_local.progress.completedIds,Is.EqualTo(new[]{"QC001"}));
        }
        [Test] public async Task DeletionRequiresExplicitConfirmationAndMatchingFreshUid() {
            await Login();int calls=_identity.AuthCalls;await _cloud.DeleteNative(false);
            Assert.That(_identity.AuthCalls,Is.EqualTo(calls));_identity.Uid="another-user";await _cloud.DeleteNative(true);
            Assert.That(_cloud.Status,Is.EqualTo(CloudSaveStatus.IdentityMismatch));Assert.That(_cloud.HasAccount,Is.True);
            Assert.That(_network.Calls.Exists(c=>c.route=="native/delete"),Is.False);
        }
        [Test] public async Task DeleteKeepsLocalProgressClearsCredentialsAndNeverAutomaticallyRecreatesAccount() {
            await Login();await _cloud.DeleteNative(true);int calls=_network.Calls.Count;
            _cloud.Tick();await _cloud.Sync();await _cloud.RefreshProfile();
            Assert.That(_cloud.Status,Is.EqualTo(CloudSaveStatus.Deleted));Assert.That(_store.Value,Is.Null);
            Assert.That(_network.Calls.Count,Is.EqualTo(calls));Assert.That(_local.progress.completedIds,Is.EqualTo(new[]{"QC001"}));
            Assert.That(_identity.AuthCalls,Is.EqualTo(2));
            Assert.That((bool)_network.Calls.Find(c=>c.route=="native/delete").data["confirm"],Is.True);
        }
        [TestCase("deletion_retry",503)] [TestCase("deletion_pending",409)] [TestCase(null,503)]
        public async Task PartialOrUncertainDeletionSurvivesRestartAndRetriesWithoutSession(string error,int status) {
            await Login();_network.Reply=route=>route=="native/delete"?new CloudReply {status=status,error=error}:Reply(route);
            await _cloud.DeleteNative(true);
            Assert.That(_cloud.DeletionNeedsRetry,Is.True);Assert.That(_cloud.HasAccount,Is.False);Assert.That(_store.Value.firebaseUid,Is.EqualTo("firebase-one"));
            Create();await _cloud.LoadOptions();int calls=_identity.AuthCalls;
            await _cloud.SignInNative("google",false);Assert.That(_identity.AuthCalls,Is.EqualTo(calls));
            _network.Reply=Reply;await _cloud.DeleteNative(true);
            Assert.That(_cloud.Status,Is.EqualTo(CloudSaveStatus.Deleted));Assert.That(_store.Value,Is.Null);
        }
        [Test] public async Task CancelAfterDestructiveDispatchStillReconcilesSuccess() {
            await Login();var pending=new TaskCompletionSource<CloudReply>();
            _network.Handler=route=>route=="native/delete"?pending.Task:Task.FromResult(Reply(route));
            var deletion=_cloud.DeleteNative(true);_cloud.Cancel();pending.SetResult(new CloudReply {status=200,deleted=true});await deletion;
            Assert.That(_cloud.HasAccount,Is.False);Assert.That(_cloud.Status,Is.EqualTo(CloudSaveStatus.Deleted));
        }
        [Test] public async Task CrashDuringDeletionCannotResumeSyncWithStoredOldBearer() {
            _store.Value=new CloudCredential {accountId="shared-website-account",endpoint=Endpoint,sessionToken=Token,provider="google",firebaseUid="firebase-one",deletionPending=true};
            Create(true);_cloud.Tick();await _cloud.Sync();
            Assert.That(_cloud.HasAccount,Is.False);Assert.That(_cloud.DeletionNeedsRetry,Is.True);Assert.That(_network.Calls,Is.Empty);
        }
        [Test] public async Task RevokedSessionStopsSyncAndPreservesOfflineWork() {
            await Login();_network.Reply=_=>new CloudReply {status=401};await _cloud.Sync();int calls=_network.Calls.Count;
            _cloud.Tick();await _cloud.Sync();Assert.That(_network.Calls.Count,Is.EqualTo(calls));
            Assert.That(_cloud.HasAccount,Is.False);Assert.That(_local.progress.completedIds,Is.EqualTo(new[]{"QC001"}));
        }
        [Test] public async Task ProfileRefreshRejectsDifferentAccountMetadata() {
            await Login();_network.Reply=_=>new CloudReply {status=200,profile=new CloudAccountProfile {accountId="other",displayName="Other"}};
            await _cloud.RefreshProfile();Assert.That(_cloud.Profile.accountId,Is.EqualTo("shared-website-account"));
        }
        [TestCase(false)] [TestCase(true)] public async Task LegacyBrowserDeletionVerifiesAccountIdAndRevokesTemporarySession(bool otherUser) {
            _store.Value=new CloudCredential {accountId="shared-website-account",endpoint=Endpoint,sessionToken=Token,provider="google"};Create();
            await _cloud.LoadOptions();
            if(otherUser)_network.Reply=route=>route=="native/sign-in"?new CloudReply {status=200,provider="google",accountId="wrong-account",sessionToken=Token}:Reply(route);
            await _cloud.DeleteNative(true);
            Assert.That(_network.Calls.Exists(c=>c.route=="sign-out"),Is.True);
            Assert.That(_network.Calls.Exists(c=>c.route=="native/delete"),Is.EqualTo(!otherUser));
            Assert.That(_cloud.Status,Is.EqualTo(otherUser?CloudSaveStatus.IdentityMismatch:CloudSaveStatus.Deleted));
        }
    }
}
