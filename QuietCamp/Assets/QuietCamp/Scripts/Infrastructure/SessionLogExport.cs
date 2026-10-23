using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;

namespace QuietCamp.Infrastructure
{
    public static class SessionLogExport
    {
        // Explicit user action only. No storage permission and no network upload.
        public static async Task<int> ToDownloads(LocalSessionJournal journal)
        {
            await journal.FlushAsync();
            if (!journal.LastWriteSucceeded) throw new IOException("Session checkpoint unavailable");
            var files = new DirectoryInfo(journal.DirectoryPath).GetFiles("session-*.json").OrderBy(f => f.Name).ToArray();
#if UNITY_ANDROID && !UNITY_EDITOR
            using var version = new AndroidJavaClass("android.os.Build$VERSION");
            if (version.GetStatic<int>("SDK_INT") < 29) throw new NotSupportedException("Use adb pull on Android 8/9");
            using var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
            using var activity = player.GetStatic<AndroidJavaObject>("currentActivity");
            using var resolver = activity.Call<AndroidJavaObject>("getContentResolver");
            using var downloads = new AndroidJavaClass("android.provider.MediaStore$Downloads");
            using var collection = downloads.GetStatic<AndroidJavaObject>("EXTERNAL_CONTENT_URI");
            foreach (var file in files) {
                using var values = new AndroidJavaObject("android.content.ContentValues");
                values.Call("put", "_display_name", file.Name);
                values.Call("put", "mime_type", "application/json");
                values.Call("put", "relative_path", "Download/QuietCamp");
                using var one = new AndroidJavaObject("java.lang.Integer", 1);
                values.Call("put", "is_pending", one);
                using var uri = resolver.Call<AndroidJavaObject>("insert", collection, values);
                if (uri == null) throw new IOException("Downloads unavailable");
                try {
                    using (var output = resolver.Call<AndroidJavaObject>("openOutputStream", uri, "w")) {
                        if (output == null) throw new IOException("Downloads unavailable");
                        try { output.Call("write", File.ReadAllBytes(file.FullName)); output.Call("flush"); }
                        finally { output.Call("close"); }
                    }
                    using var ready = new AndroidJavaObject("android.content.ContentValues");
                    using var zero = new AndroidJavaObject("java.lang.Integer", 0);
                    ready.Call("put", "is_pending", zero);
                    resolver.Call<int>("update", uri, ready, null, null);
                } catch { resolver.Call<int>("delete", uri, null, null); throw; }
            }
#else
            string destination = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads", "QuietCamp");
            Directory.CreateDirectory(destination);
            foreach (var file in files) File.Copy(file.FullName, Path.Combine(destination, file.Name), true);
#endif
            return files.Length;
        }
    }
}
