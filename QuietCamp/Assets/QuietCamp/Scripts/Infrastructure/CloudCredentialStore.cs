using System;
using System.IO;
using Newtonsoft.Json;
using QuietCamp.Application;
using UnityEngine;

namespace QuietCamp.Infrastructure
{
    /// <summary>Android Keystore AES-GCM + noBackupFilesDir; no credentials in game-save/OS backup.</summary>
    public sealed class CloudCredentialStore : ICloudCredentialStore
    {
        readonly bool _memoryOnly;
        CloudCredential _memory;
        public CloudCredentialStore(bool memoryOnly=false) { _memoryOnly=memoryOnly; }
        public CloudCredential Read()
        {
            if (_memoryOnly) return _memory;
#if UNITY_ANDROID && !UNITY_EDITOR
            using var player=new AndroidJavaClass("com.unity3d.player.UnityPlayer");
            using var activity=player.GetStatic<AndroidJavaObject>("currentActivity");
            using var storage=new AndroidJavaClass("com.quietcamp.game.privacy.CloudCredentialStorage");
            string json=storage.CallStatic<string>("read",activity);
            return string.IsNullOrEmpty(json) ? null : JsonConvert.DeserializeObject<CloudCredential>(json);
#else
            // Editor does not store real account sessions on disk.
            return _memory;
#endif
        }
        public bool Write(CloudCredential credential)
        {
            if (_memoryOnly) { _memory=credential; return true; }
            try {
#if UNITY_ANDROID && !UNITY_EDITOR
                using var player=new AndroidJavaClass("com.unity3d.player.UnityPlayer");
                using var activity=player.GetStatic<AndroidJavaObject>("currentActivity");
                using var storage=new AndroidJavaClass("com.quietcamp.game.privacy.CloudCredentialStorage");
                return storage.CallStatic<bool>("write",activity,credential==null ? "" : JsonConvert.SerializeObject(credential));
#else
                _memory=credential; return true;
#endif
            } catch { return false; }
        }
    }
}
