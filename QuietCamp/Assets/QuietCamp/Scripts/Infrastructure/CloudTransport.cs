using System;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using QuietCamp.Application;
using UnityEngine.Networking;

namespace QuietCamp.Infrastructure
{
    public sealed class CloudTransport : ICloudTransport
    {
        readonly string _endpoint;
        public CloudTransport(string endpoint) { _endpoint=(endpoint ?? "").TrimEnd('/'); }
        public static bool ValidEndpoint(string endpoint) => Uri.TryCreate(endpoint,UriKind.Absolute,out var uri)
            && uri.Scheme=="https" && string.IsNullOrEmpty(uri.UserInfo) && string.IsNullOrEmpty(uri.Query)
            && string.IsNullOrEmpty(uri.Fragment) && uri.AbsolutePath.TrimEnd('/')=="/v1/cloud";
        public async Task<CloudReply> Call(string route,string token,object data=null)
        {
            if (!ValidEndpoint(_endpoint)) return new CloudReply {status=503};
            byte[] bytes=null;
            try {
                using var request=new UnityWebRequest(_endpoint+"/"+route,route=="options" ? "GET" : "POST");
                request.redirectLimit=0; request.timeout=20;
                request.downloadHandler=new DownloadHandlerBuffer();
                if (token!=null) request.SetRequestHeader("Authorization","Bearer "+token);
                if (route!="options") {
                    bytes=Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(data ?? new {}));
                    request.uploadHandler=new UploadHandlerRaw(bytes); request.SetRequestHeader("Content-Type","application/json");
                }
                var operation=request.SendWebRequest();
                while (!operation.isDone) await Task.Yield();
                if ((request.downloadHandler.data?.Length ?? 0)>1024*1024) return new CloudReply {status=413};
                var result=JsonConvert.DeserializeObject<CloudReply>(request.downloadHandler.text) ?? new CloudReply();
                result.status=(int)request.responseCode; return result;
            } catch { return new CloudReply {status=503}; }
            finally { if(bytes!=null) Array.Clear(bytes,0,bytes.Length); }
        }
    }
}
