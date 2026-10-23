using System;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using QuietCamp.Domain;

namespace QuietCamp.Application
{
    [Serializable] public sealed class CloudSnapshot
    {
        public int schemaVersion = 2;
        public ProgressSaveData progress;
        public SessionSaveData session;
        public AlbumSaveData album;
        public MemorySaveData memories;
        public CloudResources resources = new CloudResources();
    }
    [Serializable] public sealed class CloudResources
    {
        public bool initialized;
        public int lives, hints, currency, adLivesUsed, adHintsUsed;
        public string adDay, lifeRecoveryAnchorUtc;
        public AttemptRecord[] attempts = Array.Empty<AttemptRecord>();
        public static CloudResources Capture(EconomySaveData data) => new CloudResources {
            initialized=data.initialized,lives=data.lives,hints=data.hints,currency=data.currency,
            adLivesUsed=data.adLivesUsed,adHintsUsed=data.adHintsUsed,adDay=data.adDay,
            lifeRecoveryAnchorUtc=data.lifeRecoveryAnchorUtc,attempts=data.attempts ?? Array.Empty<AttemptRecord>()};
        public void Apply(EconomySaveData data) {
            data.initialized=initialized; data.lives=lives; data.hints=hints; data.currency=currency;
            data.adLivesUsed=adLivesUsed; data.adHintsUsed=adHintsUsed; data.adDay=adDay;
            data.lifeRecoveryAnchorUtc=lifeRecoveryAnchorUtc; data.attempts=attempts;
        }
    }
    // Device-only credential; never a save module and never uploaded as progress.
    [Serializable] public sealed class CloudCredential
    {
        public string endpoint, accountId, sessionToken, provider;
        public long revision;
        public string syncedHash;
        public bool restorePending;
        public string restoreLocalHash;
    }
    public interface ICloudCredentialStore
    {
        CloudCredential Read();
        bool Write(CloudCredential credential);
    }
    public sealed class CloudReply
    {
        public int status;
        public string error, accountId, sessionToken, provider, ticket, pollSecret, url;
        public string[] providers;
        public bool created;
        public long revision;
        public CloudSnapshot snapshot;
        public bool Ok => status >= 200 && status < 300;
    }
    public interface ICloudTransport
    {
        Task<CloudReply> Call(string route, string token, object data = null);
    }
    public enum CloudSaveStatus { Local, Working, Ready, Saved, Offline, Conflict, Unavailable, SignedOut, StorageError, LinkedElsewhere, InvalidData }

    /// <summary>Explicit opt-in. CAS conflicts always require a player choice, never clock-based overwrites.</summary>
    public sealed class CloudSaveService : IDisposable
    {
        public const int ChoiceVersion = 2;
        readonly SettingsSaveData _settings;
        readonly ICloudCredentialStore _store;
        readonly ICloudTransport _transport;
        readonly Func<CloudSnapshot> _capture;
        readonly Func<CloudSnapshot, bool> _apply;
        readonly Func<bool> _persist, _allowed;
        readonly string _endpoint;
        readonly bool _configured;
        CloudCredential _credential;
        CloudReply _pendingCloud;
        bool _busy, _disposed, _remindedThisSession;
        int _reminderCompleted, _reminderRewards;
        int _generation;
        DateTime _nextSync;
        public event Action Changed;
        public CloudSaveStatus Status { get; private set; }
        public bool Configured => _configured;
        public bool Allowed => Configured && !_disposed && _allowed();
        public bool Busy => _busy;
        public bool OptedIn => Linked && _settings.accountChoiceVersion == ChoiceVersion;
        public bool HasAccount => !string.IsNullOrEmpty(_credential?.sessionToken);
        public bool Linked => HasAccount;
        public bool LinkReminderDue { get; private set; }
        public string Provider => _credential?.provider ?? "anonymous";
        public string PairCode { get; private set; }
        public string LoginUrl { get; private set; }
        public string[] Providers { get; private set; } = Array.Empty<string>();
        public int CloudCompletedCount => _pendingCloud?.snapshot?.progress?.completedIds?.Length ?? 0;

        public CloudSaveService(SettingsSaveData settings, ICloudCredentialStore store, ICloudTransport transport,
            string endpoint, bool configured, Func<bool> allowed, Func<CloudSnapshot> capture,
            Func<CloudSnapshot, bool> apply, Func<bool> persist)
        {
            _settings=settings; _store=store; _transport=transport; _endpoint=endpoint;
            _configured=configured; _allowed=allowed; _capture=capture; _apply=apply; _persist=persist;
            try { _credential=store.Read(); } catch { }
            if (_credential?.endpoint != endpoint || !Social(_credential?.provider)
                || string.IsNullOrEmpty(_credential?.accountId) || _credential.sessionToken!=null && !Token(_credential.sessionToken)) _credential=null;
            Status=CloudSaveStatus.Local;
        }
        static bool Social(string value) => value=="google" || value=="facebook" || value=="x";
        static bool Token(string value) => value != null && value.Length == 64 && Array.TrueForAll(value.ToCharArray(), c => c >= '0' && c <= '9' || c >= 'a' && c <= 'f');
        public static string Fingerprint(CloudSnapshot snapshot)
        {
            using var sha=SHA256.Create();
            return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(snapshot)))).Replace("-", "");
        }
        static bool Id(string id,bool optional=false) => optional && string.IsNullOrEmpty(id) || !string.IsNullOrEmpty(id)
            && id.Length<=160 && !id.Contains("..") && Array.TrueForAll(id.ToCharArray(),c=>char.IsLetterOrDigit(c)||c==':'||c=='_'||c=='-'||c=='.');
        static bool Placements(Placement[] values) => values==null || values.Length<=64 && Array.TrueForAll(values,
            p=>p!=null && Id(p.guestId) && p.x>=0 && p.x<8 && p.z>=0 && p.z<8 && p.rotation>=0 && p.rotation<=3);
        static bool Level(LevelData level) => level==null || level.width>=4 && level.width<=8 && level.height>=4 && level.height<=8
            && (level.guests?.Length ?? 0)<=64 && LevelContentValidator.Validate(level).Count==0;
        public static bool Valid(CloudSnapshot snapshot)
        {
            try {
                return snapshot?.schemaVersion == 2 && snapshot.progress?.completedIds != null && snapshot.session != null
                    && snapshot.album?.entries != null && snapshot.memories?.version==1 && snapshot.memories.rewardIds!=null
                    && snapshot.resources!=null && snapshot.resources.lives>=0 && snapshot.resources.hints>=0 && snapshot.resources.currency>=0
                    && snapshot.resources.adLivesUsed>=0 && snapshot.resources.adHintsUsed>=0 && snapshot.resources.attempts!=null
                    && snapshot.resources.attempts.Length<=10000 && Array.TrueForAll(snapshot.resources.attempts,a=>a!=null && Id(a.levelId) && a.checks>=0 && a.consecutiveFailures>=0)
                    && snapshot.progress.completedIds.Length<=10000 && snapshot.album.entries.Length<=2000
                    && Array.TrueForAll(snapshot.progress.completedIds,id=>Id(id)) && Id(snapshot.progress.lastLevelId,true)
                    && Id(snapshot.session.levelId,true) && Placements(snapshot.session.placements) && Level(snapshot.session.levelSnapshot)
                    && Array.TrueForAll(snapshot.album.entries,e=>e!=null && Id(e.levelId) && Placements(e.placements) && Level(e.levelSnapshot))
                    && Encoding.UTF8.GetByteCount(JsonConvert.SerializeObject(snapshot))<=768*1024;
            } catch { return false; }
        }
        void Notify(CloudSaveStatus status) { Status=status; Changed?.Invoke(); }
        bool Current(int generation) => !_disposed && generation == _generation && Allowed;
        async Task<CloudReply> Call(string route, object data = null, string token = null)
        {
            try { return await _transport.Call(route, token ?? _credential?.sessionToken, data) ?? new CloudReply {status=503}; }
            catch { return new CloudReply {status=503}; } // Never expose raw network/provider errors or credentials.
        }
        public bool ContinueLocally()
        {
            if(HasAccount) return false; // Sign out explicitly before changing to a local guest.
            _generation++; _pendingCloud=null; PairCode=LoginUrl=null;
            int old=_settings.accountChoiceVersion;
            _settings.accountChoiceVersion=ChoiceVersion;
            _settings.cloudSyncEnabled=false; _settings.cloudConsentVersion=0;
            if (_persist()) { Notify(CloudSaveStatus.Local); return true; }
            _settings.accountChoiceVersion=old; Notify(CloudSaveStatus.StorageError); return false;
        }
        public bool ContinueAccount()
        {
            if(!HasAccount || !Allowed) return false;
            int old=_settings.accountChoiceVersion; _settings.accountChoiceVersion=ChoiceVersion;
            if(_persist()) return true;
            _settings.accountChoiceVersion=old; Notify(CloudSaveStatus.StorageError); return false;
        }
        public void ObserveMilestones(int completed,int rewards,DateTime? utc=null)
        {
            if(HasAccount || !Allowed || _remindedThisSession || LinkReminderDue) return;
            var now=utc ?? DateTime.UtcNow;
            if(DateTime.TryParse(_settings.accountReminderUtc,out var last) && now-last.ToUniversalTime()<TimeSpan.FromDays(1)) return;
            bool milestone=completed>=3 && completed>=(_settings.accountReminderCompleted==0 ? 3 : _settings.accountReminderCompleted+5);
            if(!milestone && rewards<=_settings.accountReminderRewards) return;
            _reminderCompleted=completed; _reminderRewards=rewards; LinkReminderDue=true; Changed?.Invoke();
        }
        public void DismissLinkReminder(DateTime? utc=null)
        {
            LinkReminderDue=false; _remindedThisSession=true;
            _settings.accountReminderCompleted=_reminderCompleted; _settings.accountReminderRewards=_reminderRewards;
            _settings.accountReminderUtc=(utc ?? DateTime.UtcNow).ToString("O"); _persist(); Changed?.Invoke();
        }
        public void EligibilityChanged()
        {
            if (!Allowed) { _generation++; _pendingCloud=null; PairCode=LoginUrl=null; Notify(CloudSaveStatus.Local); }
        }
        public void MarkDirty() {
            if(_nextSync>DateTime.UtcNow.AddSeconds(45)) _nextSync=DateTime.UtcNow.AddSeconds(45);
            if(Status==CloudSaveStatus.Saved) Notify(CloudSaveStatus.Ready);
        }
        public void ConnectionResumed() { _nextSync=DateTime.MinValue; }
        public void Tick()
        {
            if (Allowed && OptedIn && HasAccount && !_busy && _pendingCloud == null && DateTime.UtcNow >= _nextSync)
                _ = Sync();
        }
        public async Task LoadOptions()
        {
            if (!Allowed || _busy) return;
            int generation=_generation; _busy=true;
            try {
                var reply=await Call("options"); if (!Current(generation)) return;
                if (!reply.Ok) { Notify(CloudSaveStatus.Offline); return; }
                Providers=reply.providers ?? Array.Empty<string>();
                Notify(CloudSaveStatus.Ready);
            } finally { _busy=false; Changed?.Invoke(); }
        }
        bool Remember(CloudReply reply, bool restoreExisting, string localHash)
        {
            if (!reply.Ok || !Token(reply.sessionToken) || string.IsNullOrWhiteSpace(reply.accountId)
                || !Social(reply.provider)) { Notify(CloudSaveStatus.Offline); return false; }
            var credential=new CloudCredential {endpoint=_endpoint,accountId=reply.accountId,sessionToken=reply.sessionToken,
                provider=reply.provider,revision=0,syncedHash=null,restorePending=restoreExisting && !reply.created,restoreLocalHash=localHash};
            if (_credential?.accountId == credential.accountId && _credential.syncedHash!=null) {
                credential.revision=_credential.revision; credential.syncedHash=_credential.syncedHash;
                credential.restorePending=false; credential.restoreLocalHash=null;
            }
            if (!_store.Write(credential)) { Notify(CloudSaveStatus.StorageError); return false; }
            _credential=credential; _pendingCloud=null; _nextSync=DateTime.MinValue; LinkReminderDue=false; return ContinueAccount();
        }
        public async Task SignIn(string provider, bool link, Action<string> openBrowser, CancellationToken cancel=default)
        {
            if (!Allowed || HasAccount || _busy || Array.IndexOf(Providers,provider)<0) return;
            int generation=_generation; _busy=true; Notify(CloudSaveStatus.Working);
            try {
                string localHash=Fingerprint(_capture());
                var reply=await Call("login-start",new {provider,mode=link ? "link" : "signin"});
                if (!Current(generation)) return;
                if (!reply.Ok || !Token(reply.ticket) || !Token(reply.pollSecret)
                    || !Uri.TryCreate(reply.url,UriKind.Absolute,out var uri) || !Uri.TryCreate(_endpoint,UriKind.Absolute,out var endpoint)
                    || uri.Scheme != "https" || uri.Authority != endpoint.Authority || uri.AbsolutePath != endpoint.AbsolutePath.TrimEnd('/')+"/login"
                    || !string.IsNullOrEmpty(uri.UserInfo) || !string.IsNullOrEmpty(uri.Query) || uri.Fragment!="#"+reply.ticket+":"+provider)
                { Notify(CloudSaveStatus.Unavailable); return; }
                PairCode=reply.ticket.Substring(0,8).ToUpperInvariant(); LoginUrl=reply.url; Changed?.Invoke(); openBrowser(reply.url);
                string ticket=reply.ticket, pollSecret=reply.pollSecret;
                for (int i=0; i<120 && Current(generation); i++) {
                    await Task.Delay(3000,cancel);
                    if (!Current(generation)) return;
                    var result=await Call("login-poll",new {ticket,pollSecret});
                    if (!Current(generation)) return;
                    if (result.status == 202) continue;
                    if (result.status == 409) { Notify(CloudSaveStatus.LinkedElsewhere); return; }
                    if (!Remember(result,!link,localHash)) return;
                    PairCode=null; Notify(CloudSaveStatus.Ready); return;
                }
                if (Current(generation)) Notify(CloudSaveStatus.Offline);
            } catch (OperationCanceledException) { if (Current(generation)) Notify(CloudSaveStatus.Ready); }
            catch { if (Current(generation)) Notify(CloudSaveStatus.Offline); }
            finally { _busy=false; PairCode=LoginUrl=null; Changed?.Invoke(); }
        }
        public async Task Sync()
        {
            if (!Allowed || !OptedIn || !HasAccount || _busy || _pendingCloud != null) return;
            int generation=_generation; _busy=true; _nextSync=DateTime.UtcNow.AddSeconds(45);
            try {
                var remote=await Call("read"); if (!Current(generation)) return;
                if (!remote.Ok) { HandleFailure(remote); return; }
                var local=_capture(); if (!Valid(local)) { Notify(CloudSaveStatus.InvalidData); return; }
                string localHash=Fingerprint(local);
                if (remote.snapshot != null && !Valid(remote.snapshot)) { Notify(CloudSaveStatus.InvalidData); return; }
                if (remote.snapshot != null && Fingerprint(remote.snapshot) == localHash) {
                    _credential.revision=remote.revision; _credential.syncedHash=localHash; _credential.restorePending=false; _credential.restoreLocalHash=null;
                    if (!_store.Write(_credential)) { Notify(CloudSaveStatus.StorageError); return; }
                    _nextSync=DateTime.UtcNow.AddMinutes(3); Notify(CloudSaveStatus.Saved); return;
                }
                if (remote.snapshot != null && (remote.revision != _credential.revision || _credential.syncedHash == null)) {
                    bool restore=_credential.restorePending && localHash==_credential.restoreLocalHash
                        || !_credential.restorePending && _credential.syncedHash!=null && localHash==_credential.syncedHash;
                    if(restore) {
                        if(!_apply(remote.snapshot)) { Notify(CloudSaveStatus.StorageError); return; }
                        _credential.revision=remote.revision; _credential.syncedHash=Fingerprint(remote.snapshot);
                        _credential.restorePending=false; _credential.restoreLocalHash=null;
                        if(!_store.Write(_credential)) { Notify(CloudSaveStatus.StorageError); return; }
                        _nextSync=DateTime.UtcNow.AddMinutes(3); Notify(CloudSaveStatus.Saved); return;
                    }
                    _pendingCloud=remote; Notify(CloudSaveStatus.Conflict); return;
                }
                await Upload(local,remote.revision,generation);
            } finally { _busy=false; Changed?.Invoke(); }
        }
        async Task Upload(CloudSnapshot local,long expectedRevision,int generation)
        {
            string hash=Fingerprint(local);
            var result=await Call("write",new {expectedRevision,snapshot=local});
            if (!Current(generation)) return;
            if (result.status==409) {
                var remote=await Call("read");
                if (Current(generation) && remote.Ok && Valid(remote.snapshot)) { _pendingCloud=remote; Notify(CloudSaveStatus.Conflict); }
                return;
            }
            if (!result.Ok) { HandleFailure(result); return; }
            _credential.revision=result.revision; _credential.syncedHash=hash; _credential.restorePending=false; _credential.restoreLocalHash=null;
            if (!_store.Write(_credential)) { Notify(CloudSaveStatus.StorageError); return; }
            // A local save during the HTTP request must remain queued.
            _nextSync=DateTime.UtcNow.AddSeconds(Fingerprint(_capture()) != hash ? 45 : 180); Notify(CloudSaveStatus.Saved);
        }
        public async Task ResolveConflict(bool useCloud)
        {
            if (!Allowed || !OptedIn || _busy || _pendingCloud==null) return;
            int generation=_generation; var remote=_pendingCloud; _busy=true;
            try {
                if (useCloud) {
                    if (!Valid(remote.snapshot) || !_apply(remote.snapshot)) { Notify(CloudSaveStatus.StorageError); return; }
                    _credential.revision=remote.revision; _credential.syncedHash=Fingerprint(remote.snapshot); _credential.restorePending=false; _credential.restoreLocalHash=null;
                    if (!_store.Write(_credential)) { Notify(CloudSaveStatus.StorageError); return; }
                    _nextSync=DateTime.UtcNow.AddMinutes(3); _pendingCloud=null; Notify(CloudSaveStatus.Saved);
                } else {
                    var local=_capture(); if (!Valid(local)) { Notify(CloudSaveStatus.InvalidData); return; }
                    _pendingCloud=null; await Upload(local,remote.revision,generation);
                }
            } finally { _busy=false; Changed?.Invoke(); }
        }
        void HandleFailure(CloudReply reply)
        {
            if (reply.status==401) {
                // Keep only the sync baseline so reauthentication of the same account cannot erase offline work.
                _credential.sessionToken=null; _store.Write(_credential); _settings.accountChoiceVersion=0;
                _settings.cloudSyncEnabled=false; _persist(); Notify(CloudSaveStatus.SignedOut);
            }
            else Notify(reply.status==413 || reply.status==400 ? CloudSaveStatus.InvalidData : CloudSaveStatus.Offline);
        }
        public async Task SignOut()
        {
            if (_busy) return;
            _generation++; _pendingCloud=null;
            var token=_credential?.sessionToken;
            if (!_store.Write(null)) { Notify(CloudSaveStatus.StorageError); return; }
            _credential=null; _settings.accountChoiceVersion=0; _settings.cloudSyncEnabled=false; _settings.cloudConsentVersion=0; _persist();
            Notify(CloudSaveStatus.Local);
            if (token != null && Configured && _allowed()) await Call("sign-out",new {},token);
        }
        public void Cancel() { _generation++; PairCode=LoginUrl=null; Notify(OptedIn ? CloudSaveStatus.Ready : CloudSaveStatus.Local); }
        public void Dispose() { _disposed=true; _generation++; Changed=null; }
    }
}
