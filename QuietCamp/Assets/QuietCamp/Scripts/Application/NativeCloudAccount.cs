using System;
using System.Threading;
using System.Threading.Tasks;

namespace QuietCamp.Application
{
    [Serializable] public sealed class CloudAccountProfile
    {
        public string accountId, displayName, email;
        public string[] providers;
        public bool hasCloudSave;
        public long? cloudUpdated;
    }
    // Only the adapter creates this proof, after a new interactive Firebase sign-in.
    // It is short lived and never persisted with game progress or credentials.
    public sealed class CloudIdentityProof
    {
        public string FirebaseUid, FirebaseIdToken;
    }
    public sealed class CloudIdentityException : Exception
    {
        public readonly bool NoCredentials;
        public CloudIdentityException(bool noCredentials = false) : base("Native identity unavailable") { NoCredentials=noCredentials; }
    }
    public interface ICloudNativeIdentity
    {
        bool Available(string provider);
        Task<CloudIdentityProof> Authenticate(string provider, CancellationToken cancel);
        Task SignOut();
    }

    public sealed partial class CloudSaveService
    {
        readonly ICloudNativeIdentity _nativeIdentity;
        public bool DeletionNeedsRetry => _credential?.deletionPending==true;
        // A guest returning from gameplay gets an explicit conflict instead of a silent replacement.
        bool MayRestoreOnSignIn => _settings.accountChoiceVersion!=ChoiceVersion || NeedsReauthentication;
        public bool CanSignInNatively(string provider) => NativeSignInSupported && _nativeIdentity?.Available(provider)==true;
        public bool CanDeleteNatively => NativeSignInSupported && _nativeIdentity?.Available(Provider)==true
            && (HasAccount || DeletionNeedsRetry);
        async Task ClearNativeState() { try { if(_nativeIdentity!=null) await _nativeIdentity.SignOut(); } catch { } }
        Task Revoke(CloudReply reply) => Token(reply?.sessionToken) ? Call("sign-out",new {},reply.sessionToken) : Task.CompletedTask;

        public async Task SignInNative(string provider, bool link, CancellationToken cancel=default)
        {
            if(DeletionNeedsRetry) { Notify(CloudSaveStatus.DeletionRetry); return; }
            if(!Allowed || HasAccount || _busy || !CanSignInNatively(provider) || Array.IndexOf(Providers,provider)<0) return;
            int generation=_generation; _busy=true;
            using var cancellation=CancellationTokenSource.CreateLinkedTokenSource(cancel);
            cancellation.CancelAfter(TimeSpan.FromMinutes(6)); _loginCancellation=cancellation;
            CloudReply issued=null; bool remembered=false;
            try {
                Notify(CloudSaveStatus.Working);
                string localHash=Fingerprint(_capture());
                var proof=await _nativeIdentity.Authenticate(provider,cancellation.Token);
                cancellation.Token.ThrowIfCancellationRequested();
                if(!Current(generation)) return;
                if(string.IsNullOrWhiteSpace(proof?.FirebaseUid) || string.IsNullOrWhiteSpace(proof.FirebaseIdToken))
                    throw new CloudIdentityException();
                // Raw Google/Facebook tokens never cross this application boundary.
                issued=await Call("native/sign-in",new {provider,mode=link ? "link" : "signin",idToken=proof.FirebaseIdToken});
                proof.FirebaseIdToken=null;
                cancellation.Token.ThrowIfCancellationRequested();
                if(!Current(generation)) return;
                if(!issued.Ok) {
                    if(issued.error=="deletion_pending" || issued.error=="deletion_retry") {
                        Notify(CloudSaveStatus.DeletionPending); return;
                    }
                    if(issued.status==409) Notify(CloudSaveStatus.LinkedElsewhere);
                    else Notify(issued.status==401 ? CloudSaveStatus.SignedOut : CloudSaveStatus.Offline);
                    return;
                }
                if(issued.provider!=provider) { Notify(CloudSaveStatus.InvalidData); return; }
                remembered=Remember(issued,!link && MayRestoreOnSignIn,localHash,proof.FirebaseUid);
                if(remembered) Notify(CloudSaveStatus.Ready);
            } catch(OperationCanceledException) { if(Current(generation)) Notify(cancellation.IsCancellationRequested && !cancel.IsCancellationRequested ? CloudSaveStatus.TimedOut : CloudSaveStatus.Cancelled); }
            catch(CloudIdentityException e) { if(Current(generation)) Notify(e.NoCredentials ? CloudSaveStatus.NoCredentials : CloudSaveStatus.Unavailable); }
            catch { if(Current(generation)) Notify(CloudSaveStatus.Offline); }
            finally {
                if(!remembered) await Revoke(issued);
                await ClearNativeState();
                _loginCancellation=null; _busy=false; Changed?.Invoke();
            }
        }

        public async Task RefreshProfile()
        {
            if(!Allowed || !HasAccount || _busy) return;
            int generation=_generation; _busy=true;
            try {
                var reply=await Call("status");
                if(!Current(generation)) return;
                if(!reply.Ok) { HandleFailure(reply); return; }
                if(reply.profile?.accountId==AccountId) Profile=reply.profile;
            } finally { _busy=false; Changed?.Invoke(); }
        }

        void BlockForDeletion(CloudSaveStatus status)
        {
            _generation++; _pendingCloud=null;
            if(_credential!=null) { _credential.sessionToken=null; _credential.deletionPending=true; _store.Write(_credential); }
            _settings.accountChoiceVersion=0; _settings.cloudSyncEnabled=false; _persist();
            Notify(status);
        }

        // Confirmation belongs to the existing account view; identity is checked again here.
        // Keep the identity marker across partial deletion, whose retry needs no old bearer.
        public async Task DeleteNative(bool confirm, CancellationToken cancel=default)
        {
            if(!confirm || !Allowed || _busy || !CanDeleteNatively) return;
            int generation=_generation; string expectedAccount=AccountId, provider=Provider, expectedUid=_credential.firebaseUid;
            _busy=true; Notify(CloudSaveStatus.Working);
            using var cancellation=CancellationTokenSource.CreateLinkedTokenSource(cancel);
            cancellation.CancelAfter(TimeSpan.FromMinutes(6)); _loginCancellation=cancellation;
            CloudReply temporary=null;
            try {
                var proof=await _nativeIdentity.Authenticate(provider,cancellation.Token);
                cancellation.Token.ThrowIfCancellationRequested();
                if(!Current(generation)) return;
                if(string.IsNullOrWhiteSpace(proof?.FirebaseUid) || string.IsNullOrWhiteSpace(proof.FirebaseIdToken)) throw new CloudIdentityException();
                if(expectedUid==null) {
                    // Legacy browser sessions know accountId but not Firebase UID. Verify without adopting the session or its save.
                    temporary=await Call("native/sign-in",new {provider,mode="signin",idToken=proof.FirebaseIdToken});
                    var verified=temporary;
                    await Revoke(temporary); temporary=null;
                    if(!Current(generation)) return;
                    cancellation.Token.ThrowIfCancellationRequested();
                    if(!verified.Ok) { Notify(CloudSaveStatus.Unavailable); return; }
                    if(verified.accountId!=expectedAccount || verified.provider!=provider || !Token(verified.sessionToken)) {
                        Notify(CloudSaveStatus.IdentityMismatch); return;
                    }
                    expectedUid=proof.FirebaseUid; _credential.firebaseUid=expectedUid;
                }
                if(proof.FirebaseUid!=expectedUid) { Notify(CloudSaveStatus.IdentityMismatch); return; }
                cancellation.Token.ThrowIfCancellationRequested();
                if(!Current(generation) || AccountId!=expectedAccount) return;
                // Persist the retry marker BEFORE sending a destructive request. Even a lost response may have deleted D1 data.
                var token=_credential.sessionToken;
                _credential.deletionPending=true;
                if(!_store.Write(_credential)) { _credential.deletionPending=false; Notify(CloudSaveStatus.StorageError); return; }
                var reply=await Call("native/delete",new {idToken=proof.FirebaseIdToken,confirm=true});
                proof.FirebaseIdToken=null;
                // A sent deletion must be reconciled even after cancel/dispose; never resurrect a revoked session.
                if(reply.Ok && reply.deleted) {
                    _generation++; _pendingCloud=null; Profile=null;
                    _settings.accountChoiceVersion=0; _settings.cloudSyncEnabled=false; _persist();
                    _credential.sessionToken=null;
                    if(!_store.Write(null)) { _store.Write(_credential); Notify(CloudSaveStatus.StorageError); return; }
                    _credential=null; Notify(CloudSaveStatus.Deleted);
                } else if(reply.error=="identity_rejected" || reply.error=="reauthenticate" || reply.error=="confirmation_required") {
                    _credential.deletionPending=false; _store.Write(_credential);
                    if(reply.status==401) HandleFailure(reply); else Notify(CloudSaveStatus.Unavailable);
                } else {
                    BlockForDeletion(reply.error=="deletion_pending" ? CloudSaveStatus.DeletionPending : CloudSaveStatus.DeletionRetry);
                    if(token!=null) await Call("sign-out",new {},token);
                }
            } catch(OperationCanceledException) { if(Current(generation)) Notify(CloudSaveStatus.Cancelled); }
            catch(CloudIdentityException e) { if(Current(generation)) Notify(e.NoCredentials ? CloudSaveStatus.NoCredentials : CloudSaveStatus.Unavailable); }
            catch { if(Current(generation)) Notify(CloudSaveStatus.Offline); }
            finally {
                await Revoke(temporary); await ClearNativeState();
                _loginCancellation=null; _busy=false; Changed?.Invoke();
            }
        }
    }
}
