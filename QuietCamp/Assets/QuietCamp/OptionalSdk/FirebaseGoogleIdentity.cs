#if QC_FIREBASE_AUTH && UNITY_ANDROID && !UNITY_EDITOR
using System.Threading;
using System.Threading.Tasks;
using Firebase.Auth;
using QuietCamp.Application;
using QuietCamp.Infrastructure;
using UnityEngine;

// Credential Manager supplies Google proof; Firebase supplies the only token sent to the Worker.
sealed class FirebaseGoogleIdentity : ICloudNativeIdentity
{
    readonly SemaphoreSlim _gate=new SemaphoreSlim(1,1);
    public bool Available(string provider) => provider=="google";
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Register() => NativeIdentityProviders.Factory=()=>new FirebaseGoogleIdentity();
    public async Task<CloudIdentityProof> Authenticate(string provider,CancellationToken cancel)
    {
        if(!Available(provider)) throw new CloudIdentityException();
        await _gate.WaitAsync(cancel);
        FirebaseAuth auth=null;
        try {
            // Each attempt is interactive: a refreshed old Firebase session is not fresh auth_time.
            string googleToken=await AndroidGoogleCredential.Choose(cancel);
            cancel.ThrowIfCancellationRequested();
            if(!await FirebaseRuntime.Initialize()) throw new CloudIdentityException();
            cancel.ThrowIfCancellationRequested();
            auth=FirebaseAuth.DefaultInstance;
            auth.SignOut();
            using var credential=GoogleAuthProvider.GetCredential(googleToken,null);
            googleToken=null;
            var user=await auth.SignInWithCredentialAsync(credential);
            cancel.ThrowIfCancellationRequested();
            string token=await user.TokenAsync(true);
            cancel.ThrowIfCancellationRequested();
            return new CloudIdentityProof {FirebaseUid=user.UserId,FirebaseIdToken=token};
        } finally {
            // The game retains only its Keystore-backed Worker session, never Firebase's refresh session.
            auth?.SignOut(); _gate.Release();
        }
    }
    public async Task SignOut()
    {
        await _gate.WaitAsync();
        try { if(FirebaseRuntime.Ready) FirebaseAuth.DefaultInstance.SignOut(); }
        finally { _gate.Release(); }
        await AndroidGoogleCredential.Clear();
    }
}
#endif
