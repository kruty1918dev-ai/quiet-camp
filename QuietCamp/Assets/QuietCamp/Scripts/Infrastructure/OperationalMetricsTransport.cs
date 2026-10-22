using System;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

namespace QuietCamp.Infrastructure
{
    public static class OperationalMetricsTransport
    {
        [Serializable] sealed class Payload { public string name; public int bucket, count; }
        public static async Task<bool> Send(string endpoint, string name, int bucket, int count)
        {
            if (!LegalConfiguration.PublishedUrl(endpoint)) return false;
            using (var request = new UnityWebRequest(endpoint, "POST"))
            {
                request.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(JsonUtility.ToJson(new Payload { name = name, bucket = bucket, count = count })));
                request.downloadHandler = new DownloadHandlerBuffer(); request.timeout = 10; request.redirectLimit = 0;
                request.SetRequestHeader("Content-Type", "application/json");
                var operation = request.SendWebRequest(); while (!operation.isDone) await Task.Yield();
                return request.result == UnityWebRequest.Result.Success;
            }
        }
    }
}
