using System;
using System.Text;
using System.Threading.Tasks;
using QuietCamp.Application;
using UnityEngine;
using UnityEngine.Networking;

namespace QuietCamp.Infrastructure
{
    public sealed class ComplimentaryAccessTransport : IComplimentaryAccessTransport
    {
        readonly string _endpoint, _package;
        [Serializable] sealed class Credentials { public string packageName, username, password; }
        [Serializable] sealed class Session { public string packageName, sessionToken; }
        public ComplimentaryAccessTransport(string endpoint, string package) { _endpoint = endpoint.TrimEnd('/'); _package = package; }
        public Task<ComplimentaryGrant> SignIn(string username, string password)
            => Send("/sign-in", JsonUtility.ToJson(new Credentials { packageName = _package, username = username, password = password }));
        public Task<ComplimentaryGrant> Refresh(string token)
            => Send("/refresh", JsonUtility.ToJson(new Session { packageName = _package, sessionToken = token }));
        async Task<ComplimentaryGrant> Send(string route, string json)
        {
            if (!LegalConfiguration.PublishedUrl(_endpoint)) return null;
            byte[] bytes = Encoding.UTF8.GetBytes(json);
            try {
                using (var request = new UnityWebRequest(_endpoint + route, "POST")) {
                    request.uploadHandler = new UploadHandlerRaw(bytes); request.downloadHandler = new DownloadHandlerBuffer();
                    request.timeout = 15; request.redirectLimit = 0;
                    request.SetRequestHeader("Content-Type", "application/json");
                    var operation = request.SendWebRequest(); while (!operation.isDone) await Task.Yield();
                    if (request.responseCode == 401) return new ComplimentaryGrant { state = "Denied" };
                    if (request.result != UnityWebRequest.Result.Success || request.downloadHandler.data.Length > 4096) return null;
                    return JsonUtility.FromJson<ComplimentaryGrant>(request.downloadHandler.text);
                }
            } finally { Array.Clear(bytes, 0, bytes.Length); }
        }
    }
}
