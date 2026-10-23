using System;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using QuietCamp.Application;

// Runs only against a loopback WSGI server. This is not UnityWebRequest/Android UI evidence.
static class ComplimentaryHttpProbe
{
    sealed class Transport : IComplimentaryAccessTransport, IDisposable
    {
        readonly HttpClient _client = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false });
        readonly string _endpoint;
        public Transport(string endpoint) { _endpoint = endpoint; }
        public Task<ComplimentaryGrant> SignIn(string username, string password)
            => Send("/sign-in", new {packageName="com.quietcamp.game",username,password});
        public Task<ComplimentaryGrant> Refresh(string token)
            => Send("/refresh", new {packageName="com.quietcamp.game",sessionToken=token});
        async Task<ComplimentaryGrant> Send(string route, object data)
        {
            using var response = await _client.PostAsync(_endpoint+route,new StringContent(JsonSerializer.Serialize(data),Encoding.UTF8,"application/json"));
            if(response.StatusCode==System.Net.HttpStatusCode.Unauthorized)return new ComplimentaryGrant {state="Denied"};
            response.EnsureSuccessStatusCode();
            return JsonSerializer.Deserialize<ComplimentaryGrant>(await response.Content.ReadAsStringAsync(),new JsonSerializerOptions {IncludeFields=true});
        }
        public void Dispose()=>_client.Dispose();
    }
    public static async Task<int> Run()
    {
        string endpoint=Environment.GetEnvironmentVariable("QC_REVIEW_PROBE_ENDPOINT");
        var uri=new Uri(endpoint); if(uri.Host!="127.0.0.1"||uri.Scheme!="http")throw new InvalidOperationException("Loopback endpoint required");
        using var credentials=JsonDocument.Parse(File.ReadAllText(Environment.GetEnvironmentVariable("QC_REVIEW_PROBE_CREDENTIALS")));
        string username=credentials.RootElement.GetProperty("username").GetString(),password=credentials.RootElement.GetProperty("password").GetString();
        var data=new EconomySaveData(); var economy=new CampEconomy(data,new EconomyRules(),()=>true); economy.ConfigureComplimentaryAccess(endpoint);
        using var transport=new Transport(endpoint); using var access=new ComplimentaryAccessService(economy,transport,"com.quietcamp.game","quietcamp_pro");
        if(await access.SignIn(username,password)!=ComplimentaryAccessState.Granted||!economy.IsPro)throw new Exception("Real server login did not grant Pro");
        if(await access.Refresh()!=ComplimentaryAccessState.Granted)throw new Exception("Real server session refresh failed");
        if(await access.SignIn(username,"wrong-password")!=ComplimentaryAccessState.Denied||economy.IsPro)throw new Exception("Invalid credentials granted Pro");
        if(await access.SignIn(username,password)!=ComplimentaryAccessState.Granted)throw new Exception("Reusable credentials failed");
        access.SignOut(); if(economy.IsPro||data.pro)throw new Exception("Sign out or purchase separation failed");
        Console.WriteLine("PASS real loopback HTTP authentication -> C# Pro authority, refresh, wrong password, reusable credentials and sign-out. No Android/UI verification.");
        return 0;
    }
}
