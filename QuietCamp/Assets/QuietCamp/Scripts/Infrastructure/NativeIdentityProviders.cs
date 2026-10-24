using System;
using System.Threading;
using System.Threading.Tasks;
using QuietCamp.Application;
using UnityEngine;

namespace QuietCamp.Infrastructure
{
    // SDK registration stays outside the core asmdefs. Editor always uses the browser.
    public static class NativeIdentityProviders
    {
        public const string GoogleServerClientId="327790204451-1t6rfqnmj82urke0c951cn28tqcb7t96.apps.googleusercontent.com";
        public static Func<ICloudNativeIdentity> Factory;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reset() => Factory=null;
        public static ICloudNativeIdentity Create()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            return Factory?.Invoke();
#else
            return null;
#endif
        }
    }

#if UNITY_ANDROID && !UNITY_EDITOR
    // Android callbacks carry only the provider token in memory. All Unity work resumes on Unity's context.
    public static class AndroidGoogleCredential
    {
        [UnityEngine.Scripting.Preserve]
        sealed class Callback : AndroidJavaProxy
        {
            public readonly TaskCompletionSource<string> Completion=new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
            public Callback() : base("com.quietcamp.game.privacy.GoogleCredentialBridge$Callback") { }
            [UnityEngine.Scripting.Preserve] public void success(string value) => Completion.TrySetResult(value);
            [UnityEngine.Scripting.Preserve] public void failure(string code) {
                if(code=="cancelled") Completion.TrySetCanceled();
                else Completion.TrySetException(new CloudIdentityException(code=="no_credentials"));
            }
        }
        public static async Task<string> Choose(CancellationToken cancel)
        {
            cancel.ThrowIfCancellationRequested();
            var callback=new Callback();
            using var player=new AndroidJavaClass("com.unity3d.player.UnityPlayer");
            using var activity=player.GetStatic<AndroidJavaObject>("currentActivity");
            using var bridge=new AndroidJavaClass("com.quietcamp.game.privacy.GoogleCredentialBridge");
            using var request=bridge.CallStatic<AndroidJavaObject>("choose",activity,NativeIdentityProviders.GoogleServerClientId,callback);
            using var registration=cancel.Register(()=>{
                // Cancellation may run on a pool thread. JNI cancellation happens in finally on Unity's context.
                callback.Completion.TrySetCanceled();
            });
            try { return await callback.Completion.Task; }
            finally { request.Call("cancel"); GC.KeepAlive(callback); }
        }
        public static async Task Clear()
        {
            var callback=new Callback();
            using var player=new AndroidJavaClass("com.unity3d.player.UnityPlayer");
            using var activity=player.GetStatic<AndroidJavaObject>("currentActivity");
            using var bridge=new AndroidJavaClass("com.quietcamp.game.privacy.GoogleCredentialBridge");
            bridge.CallStatic("clear",activity,callback);
            // Clear-state failure must not block local sign-out.
            await Task.WhenAny(callback.Completion.Task,Task.Delay(5000));
            if(callback.Completion.Task.IsCompleted) { try { await callback.Completion.Task; } catch { } }
            GC.KeepAlive(callback);
        }
    }
#endif
}
