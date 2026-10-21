using System;
using System.IO;
using QuietCamp.Application;
using UnityEngine;

namespace QuietCamp.Infrastructure
{
    // A local-only receipt marker, never sent as a user/device identifier.
    // Android noBackupFilesDir makes an OS-restored save require fresh choices.
    public static class PrivacyInstallScope
    {
        public static void Validate(SettingsSaveData settings, string directory = null)
        {
            try
            {
                directory = directory ?? DirectoryPath();
                Directory.CreateDirectory(directory);
                string path = Path.Combine(directory, "install-receipt");
                string marker = File.Exists(path) ? File.ReadAllText(path) : "";
                if (!Guid.TryParseExact(marker, "N", out _))
                { marker = Guid.NewGuid().ToString("N"); WriteMarker(path, marker); }
                if (settings.privacyInstallToken == marker) return;
                PrivacyEligibility.Reset(settings); settings.privacyInstallToken = marker;
            }
            catch { PrivacyEligibility.Reset(settings); settings.privacyInstallToken = ""; }
        }
        // Rotate first: a failed save cannot revive the preceding receipt on the next launch.
        public static bool Commit(SettingsSaveData settings, Func<bool> save, string directory = null)
        {
            try
            {
                directory = directory ?? DirectoryPath();
                Directory.CreateDirectory(directory);
                string marker = Guid.NewGuid().ToString("N");
                WriteMarker(Path.Combine(directory, "install-receipt"), marker);
                settings.privacyInstallToken = marker;
                if (save()) return true;
            }
            catch { }
            PrivacyEligibility.Reset(settings);
            return false;
        }
        static void WriteMarker(string path, string marker)
        {
            using (var file = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                var bytes = System.Text.Encoding.UTF8.GetBytes(marker);
                file.Write(bytes, 0, bytes.Length); file.Flush(true);
            }
        }
        static string DirectoryPath()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
                    using (var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                    using (var activity = player.GetStatic<AndroidJavaObject>("currentActivity"))
                    using (var folder = activity.Call<AndroidJavaObject>("getNoBackupFilesDir"))
                        return folder.Call<string>("getAbsolutePath");
#else
                    return Path.Combine(UnityEngine.Application.persistentDataPath, "qc_privacy_scope");
#endif
        }
    }
}
