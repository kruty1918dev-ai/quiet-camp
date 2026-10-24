using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Newtonsoft.Json;
using NUnit.Framework;
using QuietCamp.Application;

namespace QuietCamp.Tests
{
    public sealed class CloudSaveTests
    {
        const string Endpoint="https://cloud.quietcamp.test/v1/cloud";
        static readonly string Token=new string('a',64);
        sealed class Store : ICloudCredentialStore
        {
            public CloudCredential Value; public bool Fail;
            public CloudCredential Read()=>Value==null ? null : JsonConvert.DeserializeObject<CloudCredential>(JsonConvert.SerializeObject(Value));
            public bool Write(CloudCredential value) { if(Fail)return false;Value=value==null?null:JsonConvert.DeserializeObject<CloudCredential>(JsonConvert.SerializeObject(value));return true; }
        }
        sealed class Transport : ICloudTransport
        {
            public readonly List<string> Calls=new List<string>();
            public Func<string,string,object,Task<CloudReply>> Handler;
            public Task<CloudReply> Call(string route,string token,object data=null) {
                Calls.Add(route);return Handler?.Invoke(route,token,data) ?? Task.FromResult(new CloudReply {status=503});
            }
        }
        static CloudSnapshot Snapshot(params string[] ids)=>new CloudSnapshot {progress=new ProgressSaveData {completedIds=ids},
            session=new SessionSaveData(),album=new AlbumSaveData(),memories=new MemorySaveData()};
        static CloudCredential Credential()=>new CloudCredential {endpoint=Endpoint,accountId="account-one",provider="google",sessionToken=Token};
        SettingsSaveData _settings; Store _store; Transport _network; CloudSaveService _service;
        CloudSnapshot _local; bool _eligible,_persist,_apply;
        [SetUp] public void Setup() {
            _settings=new SettingsSaveData {accountChoiceVersion=CloudSaveService.ChoiceVersion};_store=new Store {Value=Credential()};_network=new Transport();
            _local=Snapshot("QC001");_eligible=_persist=_apply=true;
            _service=new CloudSaveService(_settings,_store,_network,Endpoint,true,()=>_eligible,()=>_local,
                snapshot=>{if(!_apply)return false;_local=snapshot;return true;},()=>_persist);
        }
        [TearDown] public void TearDown()=>_service.Dispose();
        void Enable()=>Assert.That(_service.ContinueAccount(),Is.True);
        void Guest() {
            _service.Dispose();_store.Value=null;_settings.accountChoiceVersion=0;
            _service=new CloudSaveService(_settings,_store,_network,Endpoint,true,()=>_eligible,()=>_local,
                snapshot=>{if(!_apply)return false;_local=snapshot;return true;},()=>_persist);
        }
        void PendingLogin() {
            Guest();
            _network.Handler=(route,t,data)=>Task.FromResult(route=="options"
                ? new CloudReply {status=200,providers=new[]{"google","facebook","x","unsupported"}}
                : route=="login-start" ? new CloudReply {status=200,ticket=Token,pollSecret=Token,url=Endpoint+"/login#"+Token+":google"}
                : new CloudReply {status=202});
        }
        [Test] public async Task UnsupportedProvidersAndLegacyXCredentialsNeverAuthorizeCloud() {
            PendingLogin();await _service.LoadOptions();
            Assert.That(_service.Providers,Is.EqualTo(new[]{"google","facebook"}));
            await _service.SignIn("x",false,_=>Assert.Fail("X must never open a browser"));
            Assert.That(_network.Calls,Does.Not.Contain("login-start"));
            _store.Value=Credential();_store.Value.provider="x";
            _service.Dispose();
            _service=new CloudSaveService(_settings,_store,_network,Endpoint,true,()=>true,()=>_local,_=>true,()=>true);
            Assert.That(_service.HasAccount,Is.False);
            Assert.That(_service.NeedsReauthentication,Is.True);
            Assert.That(_service.ContinueLocally(),Is.True);Assert.That(_store.Value,Is.Null);
            Assert.That(_service.NeedsReauthentication,Is.False);
        }
        [Test] public async Task CancelImmediatelyStopsLoginAndWithdrawsTheTicketWithItsPollSecret() {
            PendingLogin();await _service.LoadOptions();
            var login=_service.SignIn("google",false,_=>{});
            _service.Cancel();
            Assert.That(await Task.WhenAny(login,Task.Delay(1000)),Is.SameAs(login));await login;
            Assert.That(_service.Busy,Is.False);Assert.That(_service.Status,Is.EqualTo(CloudSaveStatus.Cancelled));
            Assert.That(_service.LoginUrl,Is.Null);Assert.That(_network.Calls,Does.Contain("login-cancel"));
            Assert.That(_service.HasAccount,Is.False);Assert.That(_local.progress.completedIds,Is.EqualTo(new[]{"QC001"}));
        }
        [Test] public async Task ForegroundReturnImmediatelyPollsAndRecognizesBrowserCancellation() {
            PendingLogin();await _service.LoadOptions();
            var previous=_network.Handler;
            _network.Handler=(route,t,data)=>route=="login-poll" ? Task.FromResult(new CloudReply {status=410}) : previous(route,t,data);
            var login=_service.SignIn("google",false,_=>{});_service.ConnectionResumed();
            Assert.That(await Task.WhenAny(login,Task.Delay(1000)),Is.SameAs(login));await login;
            Assert.That(_service.Status,Is.EqualTo(CloudSaveStatus.Cancelled));Assert.That(_service.HasAccount,Is.False);
        }
        [Test] public async Task LoginDeadlineWithdrawsAttemptWithoutChangingLocalProgress() {
            PendingLogin();_service.Dispose();var now=DateTime.UtcNow;
            _service=new CloudSaveService(_settings,_store,_network,Endpoint,true,()=>true,()=>_local,_=>true,()=>true,()=>now);
            await _service.LoadOptions();await _service.SignIn("google",false,_=>now=now.AddMinutes(7));
            Assert.That(_service.Status,Is.EqualTo(CloudSaveStatus.TimedOut));Assert.That(_service.Busy,Is.False);
            Assert.That(_network.Calls,Does.Contain("login-cancel"));Assert.That(_network.Calls,Does.Not.Contain("login-poll"));
            Assert.That(_local.progress.completedIds,Is.EqualTo(new[]{"QC001"}));
        }
        [Test] public async Task CancelledInFlightPollRevokesItsLateSessionInsteadOfRememberingIt() {
            PendingLogin();await _service.LoadOptions();var previous=_network.Handler;
            var pending=new TaskCompletionSource<CloudReply>();
            var requested=new TaskCompletionSource<bool>();
            _network.Handler=(route,t,data)=> {if(route=="login-poll"){requested.TrySetResult(true);return pending.Task;}return previous(route,t,data);};
            var login=_service.SignIn("google",false,_=>{});_service.ConnectionResumed();await requested.Task;
            _service.Cancel();pending.SetResult(new CloudReply {status=200,sessionToken=Token,accountId="late",provider="google",created=true});
            await login;Assert.That(_service.HasAccount,Is.False);Assert.That(_store.Value,Is.Null);
            Assert.That(_network.Calls,Does.Contain("sign-out"));
        }
        [Test] public async Task LocalGuestAndDefaultOffNeverContactServer() {
            Guest();Assert.That(_service.ContinueLocally(),Is.True);await _service.Sync();_service.Tick();
            Assert.That(_network.Calls,Is.Empty);Assert.That(_settings.accountChoiceVersion,Is.EqualTo(CloudSaveService.ChoiceVersion));
            Assert.That(_settings.analyticsConsent||_settings.advertisingConsent||_settings.crashReportingConsent,Is.False);
        }
        [Test] public async Task FailedOptInPersistenceNeverStartsNetworking() {
            _settings.accountChoiceVersion=0;_persist=false;Assert.That(_service.ContinueAccount(),Is.False);await _service.Sync();
            Assert.That(_service.OptedIn,Is.False);Assert.That(_network.Calls,Is.Empty);
        }
        [Test] public async Task EligibilityBlocksEveryOnlineAction() {
            Enable();_eligible=false;_service.EligibilityChanged();await _service.Sync();await _service.LoadOptions();_service.Tick();
            Assert.That(_network.Calls,Is.Empty);
        }
        [Test] public async Task DifferentCloudProgressRequiresExplicitChoice() {
            Enable();_network.Handler=(r,t,d)=>Task.FromResult(new CloudReply {status=200,revision=8,snapshot=Snapshot("QC002")});
            await _service.Sync();Assert.That(_service.Status,Is.EqualTo(CloudSaveStatus.Conflict));
            Assert.That(_local.progress.completedIds,Is.EqualTo(new[]{"QC001"}));Assert.That(_network.Calls,Is.EqualTo(new[]{"read"}));
        }
        [Test] public async Task ChoosingCloudRestoresOnlyAfterSuccessfulLocalCommit() {
            Enable();_network.Handler=(r,t,d)=>Task.FromResult(new CloudReply {status=200,revision=8,snapshot=Snapshot("QC002")});
            await _service.Sync();_apply=false;await _service.ResolveConflict(true);
            Assert.That(_local.progress.completedIds,Is.EqualTo(new[]{"QC001"}));Assert.That(_service.Status,Is.EqualTo(CloudSaveStatus.StorageError));
            _apply=true;await _service.ResolveConflict(true);Assert.That(_local.progress.completedIds,Is.EqualTo(new[]{"QC002"}));
            Assert.That(_store.Value.revision,Is.EqualTo(8));
        }
        [Test] public async Task ExplicitLocalChoiceUsesCloudRevisionNotWallClock() {
            Enable();long expected=-1;
            _network.Handler=(route,t,data)=>{
                if(route=="read")return Task.FromResult(new CloudReply {status=200,revision=7,snapshot=Snapshot("QC002")});
                expected=Newtonsoft.Json.Linq.JObject.FromObject(data).Value<long>("expectedRevision");
                return Task.FromResult(new CloudReply {status=200,revision=8});
            };
            await _service.Sync();await _service.ResolveConflict(false);Assert.That(expected,Is.EqualTo(7));
            Assert.That(_local.progress.completedIds,Is.EqualTo(new[]{"QC001"}));Assert.That(_store.Value.revision,Is.EqualTo(8));
        }
        [Test] public async Task EligibilityRevocationDuringReadDoesNotUploadOrApply() {
            Enable();var pending=new TaskCompletionSource<CloudReply>();_network.Handler=(r,t,d)=>pending.Task;
            var sync=_service.Sync();_eligible=false;_service.EligibilityChanged();
            pending.SetResult(new CloudReply {status=200,revision=0});await sync;
            Assert.That(_network.Calls,Is.EqualTo(new[]{"read"}));Assert.That(_local.progress.completedIds,Is.EqualTo(new[]{"QC001"}));
        }
        [Test] public async Task OfflineChangesOnBothDevicesRequireChoiceInsteadOfSilentOverwrite() {
            Enable();_network.Handler=(r,t,d)=>Task.FromResult(new CloudReply {status=200,revision=3,snapshot=_local});
            await _service.Sync();_local=Snapshot("QC002");
            _network.Handler=(r,t,d)=>Task.FromResult(new CloudReply {status=200,revision=4,snapshot=Snapshot("QC003")});
            await _service.Sync();Assert.That(_service.Status,Is.EqualTo(CloudSaveStatus.Conflict));
            Assert.That(_local.progress.completedIds,Is.EqualTo(new[]{"QC002"}));
            Assert.That(_network.Calls,Does.Not.Contain("write"));
        }
        [Test] public async Task RevokedSessionStopsAutomaticSyncAndKeepsLocalProgress() {
            Enable();_network.Handler=(r,t,d)=>Task.FromResult(new CloudReply {status=401});await _service.Sync();
            Assert.That(_service.HasAccount,Is.False);Assert.That(_service.OptedIn,Is.False);
            Assert.That(_store.Value.sessionToken,Is.Null);Assert.That(_local.progress.completedIds,Is.EqualTo(new[]{"QC001"}));
        }
        [Test] public async Task OfflineFailureDoesNotAdvanceRevisionOrRemoveProgress() {
            Enable();await _service.Sync();Assert.That(_service.Status,Is.EqualTo(CloudSaveStatus.Offline));
            Assert.That(_store.Value.revision,Is.EqualTo(0));Assert.That(_local.progress.completedIds.Length,Is.EqualTo(1));
        }
        [Test] public async Task CredentialWriteFailureDoesNotClaimSuccessfulSync() {
            Enable();_store.Fail=true;
            _network.Handler=(r,t,d)=>Task.FromResult(new CloudReply {status=200,revision=8,snapshot=_local});
            await _service.Sync();Assert.That(_service.Status,Is.EqualTo(CloudSaveStatus.StorageError));
            Assert.That(_store.Value.revision,Is.Zero);
        }
        [Test] public async Task LateLinkCreatesProfileFromLocalProgressWithoutAlteringPrivacy() {
            Guest();_settings.accountChoiceVersion=CloudSaveService.ChoiceVersion;_settings.advertisingConsent=false;string mode=null;
            _network.Handler=(route,t,data)=>{
                if(route=="options")return Task.FromResult(new CloudReply {status=200,providers=new[]{"google"}});
                if(route=="login-start") {
                    mode=Newtonsoft.Json.Linq.JObject.FromObject(data).Value<string>("mode");
                    return Task.FromResult(new CloudReply {status=200,ticket=Token,pollSecret=Token,url=Endpoint+"/login#"+Token+":google"});
                }
                return Task.FromResult(new CloudReply {status=200,accountId="account-one",sessionToken=new string('b',64),provider="google",created=true});
            };
            await _service.LoadOptions();await _service.SignIn("google",true,_=>{});
            Assert.That(mode,Is.EqualTo("link"));Assert.That(_store.Value.accountId,Is.EqualTo("account-one"));
            Assert.That(_service.Linked,Is.True);Assert.That(_settings.advertisingConsent,Is.False);
            Assert.That(_local.progress.completedIds,Is.EqualTo(new[]{"QC001"}));
        }
        [Test] public async Task BrowserHandoffOpensValidatedLoginBeforeWaitingForAuthorization() {
            Guest();string opened=null;
            string url=Endpoint+"/login#"+Token+":google";
            _network.Handler=(route,t,data)=>Task.FromResult(route=="options"
                ? new CloudReply {status=200,providers=new[]{"google"}}
                : new CloudReply {status=200,ticket=Token,pollSecret=Token,url=url});
            await _service.LoadOptions();
            using var cancel=new System.Threading.CancellationTokenSource();
            var signIn=_service.SignIn("google",true,value=>opened=value,cancel.Token);
            try {
                Assert.That(opened,Is.EqualTo(url));
                Assert.That(_service.LoginUrl,Is.EqualTo(url));
                Assert.That(_service.Busy,Is.True);
                Assert.That(_network.Calls,Does.Not.Contain("login-poll"));
            } finally { cancel.Cancel();await signIn; }
            Assert.That(_service.Busy,Is.False);
            Assert.That(_service.HasAccount,Is.False,"Opening a browser alone does not authorize an account.");
        }
        [Test] public async Task BrowserHandoffRejectsForeignOriginsWithoutOpeningThem() {
            Guest();bool opened=false;
            _network.Handler=(route,t,data)=>Task.FromResult(route=="options"
                ? new CloudReply {status=200,providers=new[]{"google"}}
                : new CloudReply {status=200,ticket=Token,pollSecret=Token,url="https://attacker.test/v1/cloud/login#"+Token+":google"});
            await _service.LoadOptions();await _service.SignIn("google",true,_=>opened=true);
            Assert.That(opened,Is.False);Assert.That(_service.Status,Is.EqualTo(CloudSaveStatus.Unavailable));
        }
        [Test] public async Task RestartKeepsAccountAndAcknowledgedCloudRevisionWithoutUploadingAgain() {
            Enable();_network.Handler=(route,t,data)=>Task.FromResult(new CloudReply {status=200,revision=9,snapshot=_local});
            await _service.Sync();_service.Dispose();_network.Calls.Clear();
            _service=new CloudSaveService(_settings,_store,_network,Endpoint,true,()=>true,()=>_local,_=>true,()=>true);
            await _service.Sync();Assert.That(_store.Value.revision,Is.EqualTo(9));
            Assert.That(_network.Calls,Is.EqualTo(new[]{"read"}));Assert.That(_service.Status,Is.EqualTo(CloudSaveStatus.Saved));
        }
        [Test] public async Task UnknownCloudSchemaCannotReplaceCurrentProgress() {
            Enable();var snapshot=Snapshot();snapshot.schemaVersion=999;
            _network.Handler=(r,t,d)=>Task.FromResult(new CloudReply {status=200,revision=4,snapshot=snapshot});
            await _service.Sync();Assert.That(_service.Status,Is.EqualTo(CloudSaveStatus.InvalidData));
            Assert.That(_local.progress.completedIds.Length,Is.EqualTo(1));
        }
        [Test] public void InvalidCloudBoardsAndResourcePathsAreRejectedBeforeRestore() {
            var payload=Snapshot("../arbitrary");Assert.That(CloudSaveService.Valid(payload),Is.False);
            payload=Snapshot("QC001");payload.session.levelSnapshot=new QuietCamp.Domain.LevelData {width=int.MaxValue,height=int.MaxValue};
            Assert.That(CloudSaveService.Valid(payload),Is.False);
            payload=Snapshot("QC001");payload.album.entries=new AlbumSaveData.Entry[]{null};
            Assert.That(CloudSaveService.Valid(payload),Is.False);
        }
        [Test] public void CloudSnapshotCannotSerializeDeviceConsentPurchaseOrEntitlementFields() {
            string json=JsonConvert.SerializeObject(_local);
            foreach(string forbidden in new[]{"sessionToken","purchases","entitlements","analyticsConsent","cloudSyncEnabled","advertisingConsent","pro","complimentarySessionToken"})
                Assert.That(json.Contains("\""+forbidden+"\":"),Is.False,forbidden);
        }
        [Test] public async Task SignOutRevokesServerSessionButRetainsLocalProgress() {
            Enable();await _service.SignOut();Assert.That(_service.HasAccount,Is.False);
            Assert.That(_store.Value,Is.Null);Assert.That(_network.Calls,Is.EqualTo(new[]{"sign-out"}));
            Assert.That(_local.progress.completedIds,Is.EqualTo(new[]{"QC001"}));
        }
        [Test] public async Task ConcurrentRemoteWriteReturnsConflictInsteadOfBlindRetry() {
            Enable();_network.Handler=(r,t,d)=>Task.FromResult(r=="write" ? new CloudReply {status=409} :
                new CloudReply {status=200,revision=_network.Calls.Count>1?2:0,snapshot=_network.Calls.Count>1?Snapshot("QC002"):null});
            await _service.Sync();Assert.That(_service.Status,Is.EqualTo(CloudSaveStatus.Conflict));
            Assert.That(_network.Calls,Is.EqualTo(new[]{"read","write","read"}));
        }
        [Test] public void RestoredPrivacyReceiptRequiresFreshCloudChoice() {
            _settings.cloudSyncEnabled=true;_settings.cloudConsentVersion=1;_settings.accountChoiceVersion=1;
            PrivacyEligibility.Reset(_settings);
            Assert.That(_settings.cloudSyncEnabled,Is.False);Assert.That(_settings.cloudConsentVersion,Is.Zero);
            Assert.That(_settings.accountChoiceVersion,Is.Zero);
        }
        [Test] public async Task ExistingAccountSignInRestoresAutomaticallyBeforeContinuing() {
            Guest();string mode=null;
            _network.Handler=(route,t,data)=>{
                if(route=="options")return Task.FromResult(new CloudReply {status=200,providers=new[]{"google"}});
                if(route=="login-start") {
                    mode=Newtonsoft.Json.Linq.JObject.FromObject(data).Value<string>("mode");
                    return Task.FromResult(new CloudReply {status=200,ticket=Token,pollSecret=Token,url=Endpoint+"/login#"+Token+":google"});
                }
                if(route=="login-poll")return Task.FromResult(new CloudReply {status=200,accountId="restored",sessionToken=Token,provider="google",created=false});
                return Task.FromResult(new CloudReply {status=200,revision=5,snapshot=Snapshot("QC001","QC002","QC003")});
            };
            await _service.LoadOptions();await _service.SignIn("google",false,_=>{});await _service.Sync();
            Assert.That(mode,Is.EqualTo("signin"));Assert.That(_local.progress.completedIds.Length,Is.EqualTo(3));
            Assert.That(_service.Status,Is.EqualTo(CloudSaveStatus.Saved));Assert.That(_network.Calls,Does.Not.Contain("write"));
        }
        [Test] public async Task LateLinkToOccupiedAccountLeavesGuestDataAndHasNoSwitchFallback() {
            Guest();_network.Handler=(route,t,data)=>Task.FromResult(route=="options"
                ? new CloudReply {status=200,providers=new[]{"google"}} : route=="login-start"
                ? new CloudReply {status=200,ticket=Token,pollSecret=Token,url=Endpoint+"/login#"+Token+":google"} : new CloudReply {status=409});
            await _service.LoadOptions();await _service.SignIn("google",true,_=>{});
            Assert.That(_service.Status,Is.EqualTo(CloudSaveStatus.LinkedElsewhere));Assert.That(_service.HasAccount,Is.False);
            Assert.That(_local.progress.completedIds,Is.EqualTo(new[]{"QC001"}));
            Assert.That(_network.Calls,Does.Not.Contain("write"));Assert.That(_network.Calls,Does.Not.Contain("read"));
        }
        [Test] public async Task UnchangedLocalCopyAutomaticallyPullsNewerCloudOnNextLaunch() {
            Enable();_network.Handler=(r,t,d)=>Task.FromResult(new CloudReply {status=200,revision=1,snapshot=_local});await _service.Sync();
            _service.Dispose();_network.Calls.Clear();
            _service=new CloudSaveService(_settings,_store,_network,Endpoint,true,()=>true,()=>_local,v=>{_local=v;return true;},()=>true);
            _network.Handler=(r,t,d)=>Task.FromResult(new CloudReply {status=200,revision=2,snapshot=Snapshot("QC001","QC002")});
            await _service.Sync();Assert.That(_local.progress.completedIds.Length,Is.EqualTo(2));
            Assert.That(_service.Status,Is.EqualTo(CloudSaveStatus.Saved));Assert.That(_network.Calls,Is.EqualTo(new[]{"read"}));
        }
        [Test] public void GuestRemindersFollowMilestonesAndRespectDailyAndSessionCooldown() {
            Guest();var now=new DateTime(2026,10,23,12,0,0,DateTimeKind.Utc);
            _service.ObserveMilestones(2,0,now);Assert.That(_service.LinkReminderDue,Is.False);
            _service.ObserveMilestones(3,0,now);Assert.That(_service.LinkReminderDue,Is.True);
            _service.DismissLinkReminder(now);_service.ObserveMilestones(20,5,now.AddDays(1));
            Assert.That(_service.LinkReminderDue,Is.False); // At most once per launch as well as per day.
            _service.Dispose();_service=new CloudSaveService(_settings,_store,_network,Endpoint,true,()=>true,()=>_local,_=>true,()=>true);
            _service.ObserveMilestones(8,1,now.AddHours(2));Assert.That(_service.LinkReminderDue,Is.False);
            _service.ObserveMilestones(8,1,now.AddDays(1));Assert.That(_service.LinkReminderDue,Is.True);
        }
        [Test] public void SignificantRewardCanSuggestLinkingBeforeThreeLevelsButLinkedPlayersAreNeverNagged() {
            _service.ObserveMilestones(10,4);Assert.That(_service.LinkReminderDue,Is.False);
            Guest();_service.ObserveMilestones(1,1);Assert.That(_service.LinkReminderDue,Is.True);
        }
        [Test] public void ResourcesCarryEarnedBalancesAndAttemptsWithoutGrantingPro() {
            var original=new EconomySaveData {initialized=true,lives=2,hints=7,currency=99,pro=true,complimentarySessionToken="private",
                attempts=new[]{new AttemptRecord {levelId="QC001",checks=3}}};
            var copy=CloudResources.Capture(original);string json=JsonConvert.SerializeObject(copy);
            Assert.That(json.Contains("private"),Is.False);Assert.That(json.Contains("pro"),Is.False);
            var destination=new EconomySaveData {pro=false,complimentarySessionToken="device-only"};copy.Apply(destination);
            Assert.That(destination.currency,Is.EqualTo(99));Assert.That(destination.hints,Is.EqualTo(7));
            Assert.That(destination.attempts[0].checks,Is.EqualTo(3));Assert.That(destination.pro,Is.False);
            Assert.That(destination.complimentarySessionToken,Is.EqualTo("device-only"));
        }
        [Test] public async Task OfflineLocalChangesUploadOnceConnectionReturnsAndRemoteIsUnchanged() {
            Enable();_network.Handler=(r,t,d)=>Task.FromResult(new CloudReply {status=200,revision=1,snapshot=_local});await _service.Sync();
            var cloud=Snapshot("QC001");_local=Snapshot("QC001","QC002");_local.resources.currency=80;
            _network.Handler=(r,t,d)=>Task.FromResult(new CloudReply {status=503});await _service.Sync();
            Assert.That(_service.Status,Is.EqualTo(CloudSaveStatus.Offline));
            CloudSnapshot uploaded=null;
            _network.Handler=(route,t,data)=>{
                if(route=="read")return Task.FromResult(new CloudReply {status=200,revision=1,snapshot=cloud});
                uploaded=Newtonsoft.Json.Linq.JObject.FromObject(data)["snapshot"].ToObject<CloudSnapshot>();
                return Task.FromResult(new CloudReply {status=200,revision=2});
            };
            _service.ConnectionResumed();await _service.Sync();
            Assert.That(uploaded.progress.completedIds.Length,Is.EqualTo(2));Assert.That(uploaded.resources.currency,Is.EqualTo(80));
            Assert.That(_service.Status,Is.EqualTo(CloudSaveStatus.Saved));
        }
        [Test] public async Task LegacyAnonymousCloudCredentialsDoNotAuthorizeAnyGuestUpload() {
            Guest();_service.Dispose();_store.Value=Credential();_store.Value.provider="anonymous";
            _service=new CloudSaveService(_settings,_store,_network,Endpoint,true,()=>true,()=>_local,_=>true,()=>true);
            Assert.That(_service.HasAccount,Is.False);Assert.That(_service.ContinueLocally(),Is.True);
            await _service.Sync();_service.Tick();Assert.That(_network.Calls,Is.Empty);
        }
        [Test] public async Task ExpiredSessionReauthenticationPreservesOfflineWorkForTheSameAccount() {
            Enable();var previous=Snapshot("QC001");
            _network.Handler=(r,t,d)=>Task.FromResult(new CloudReply {status=200,revision=1,snapshot=previous});await _service.Sync();
            _local=Snapshot("QC001","QC002");_network.Handler=(r,t,d)=>Task.FromResult(new CloudReply {status=401});await _service.Sync();
            Assert.That(_service.HasAccount,Is.False);
            _network.Handler=(route,t,data)=>{
                if(route=="options")return Task.FromResult(new CloudReply {status=200,providers=new[]{"google"}});
                if(route=="login-start")return Task.FromResult(new CloudReply {status=200,ticket=Token,pollSecret=Token,url=Endpoint+"/login#"+Token+":google"});
                if(route=="login-poll")return Task.FromResult(new CloudReply {status=200,accountId="account-one",sessionToken=Token,provider="google",created=false});
                if(route=="read")return Task.FromResult(new CloudReply {status=200,revision=1,snapshot=previous});
                return Task.FromResult(new CloudReply {status=200,revision=2});
            };
            await _service.LoadOptions();await _service.SignIn("google",false,_=>{});await _service.Sync();
            Assert.That(_local.progress.completedIds.Length,Is.EqualTo(2));Assert.That(_network.Calls,Does.Contain("write"));
            Assert.That(_store.Value.revision,Is.EqualTo(2));
        }
    }
}
