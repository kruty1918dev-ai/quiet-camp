using System;
using System.IO;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using QuietCamp.Application;

// A remote probe requires an exact enabled owner-configured HTTPS endpoint.
// This uses HttpClient, not UnityWebRequest, and is not Android/UI evidence.
static class ComplimentaryHttpProbe
{
    static void VerifyPublishedContent(CampEconomy economy)
    {
        using var campaign=JsonDocument.Parse(File.ReadAllText("QuietCamp/Assets/QuietCamp/Resources/QuietCamp/campaign.json"));
        using var monetization=JsonDocument.Parse(File.ReadAllText("QuietCamp/Assets/QuietCamp/Resources/QuietCamp/monetization.json"));
        var ids=new List<string>();
        foreach(string field in new[]{"mvpLevelIds","generatedLevelIds"})
            if(campaign.RootElement.TryGetProperty(field,out var values))foreach(var id in values.EnumerateArray())ids.Add(id.GetString());
        var definitions=new List<JourneyDefinition>{new JourneyDefinition{id="main",published=true,includedInPro=true,levelIds=ids.ToArray()}};
        var optional=JsonSerializer.Deserialize<JourneyDefinition[]>(monetization.RootElement.GetProperty("journeys").GetRawText(),new JsonSerializerOptions{IncludeFields=true});
        foreach(var journey in optional){if(campaign.RootElement.GetProperty("bundledLevelLimit").GetInt32()==100)journey.published=false; definitions.Add(journey);}
        var catalog=new JourneyCatalog(definitions); var progress=new ProgressionService();
        var access=new JourneyAccessService(catalog,progress,new EntitlementSaveData(),()=>economy.IsPro,explorePublished:()=>economy.CanExplorePublishedContent);
        int levels=0,journeys=0;
        foreach(var journey in catalog.Journeys) {
            if(journey.published){journeys++;foreach(string id in journey.levelIds){if(!access.Evaluate(id).CanStart)throw new Exception("Published content is still progress/paywall gated"); levels++;}}
            else foreach(string id in journey.levelIds)if(catalog.ForLevel(id)==journey&&access.Evaluate(id).CanStart)throw new Exception("Unpublished content opened");
        }
        if(levels==0||progress.CompletedCount!=0||progress.CosmeticFlags!=0)throw new Exception("Content access altered earned progression or no published levels found");
        Console.WriteLine("PASS real grant -> current resource catalog: "+levels+" published levels, "+journeys+" published journey(s), zero forged progress; draft journeys excluded. No Android/UI verification.");
    }
    sealed class Transport : IComplimentaryAccessTransport, IDisposable
    {
        readonly HttpClient _client = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false }) { Timeout = TimeSpan.FromSeconds(15) };
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
        var uri=new Uri(endpoint); bool local=uri.Host=="127.0.0.1"&&uri.Scheme=="http";
        if(!local) {
            using var owner=JsonDocument.Parse(File.ReadAllText(Environment.GetEnvironmentVariable("QC_REVIEW_PROBE_OWNER_CONFIG")));
            var config=owner.RootElement.GetProperty("complimentaryAccess");
            if(uri.Scheme!="https"||!string.IsNullOrEmpty(uri.UserInfo)||!string.IsNullOrEmpty(uri.Query)||!string.IsNullOrEmpty(uri.Fragment)
                ||!config.GetProperty("enabled").GetBoolean()||config.GetProperty("endpoint").GetString()!=endpoint)
                throw new InvalidOperationException("An exact enabled owner-configured HTTPS endpoint is required");
        }
        using var credentials=JsonDocument.Parse(File.ReadAllText(Environment.GetEnvironmentVariable("QC_REVIEW_PROBE_CREDENTIALS")));
        string username=credentials.RootElement.GetProperty("username").GetString(),password=credentials.RootElement.GetProperty("password").GetString();
        string phase=Environment.GetEnvironmentVariable("QC_REVIEW_PROBE_PHASE"), stateFile=Environment.GetEnvironmentVariable("QC_REVIEW_PROBE_STATE");
        var options=new JsonSerializerOptions {IncludeFields=true};
        if(phase=="database-unavailable"||phase=="restore") {
            var stored=JsonSerializer.Deserialize<EconomySaveData>(File.ReadAllText(stateFile),options);
            DateTime clock=DateTime.UtcNow;
            var cached=new CampEconomy(stored,new EconomyRules(),()=>true,()=>clock); cached.ConfigureComplimentaryAccess(endpoint);
            using var http=new Transport(endpoint);
            using var resumed=new ComplimentaryAccessService(cached,http,"com.quietcamp.game","quietcamp_pro");
            var result=await resumed.Refresh();
            if(phase=="restore") {
                if(result!=ComplimentaryAccessState.Granted||!cached.CanExplorePublishedContent)throw new Exception("Public service did not restore saved content grant");
                VerifyPublishedContent(cached);
            } else {
                if(result!=ComplimentaryAccessState.Retry||!cached.HasComplimentaryPro)throw new Exception("Unavailable database did not preserve valid lease");
                clock=clock.AddDays(2);
                if(await resumed.Refresh()!=ComplimentaryAccessState.Retry||cached.IsPro||cached.CanExplorePublishedContent)throw new Exception("Unavailable database extended expired lease");
                var fresh=new CampEconomy(new EconomySaveData(),new EconomyRules(),()=>true); fresh.ConfigureComplimentaryAccess(endpoint);
                using var failed=new ComplimentaryAccessService(fresh,http,"com.quietcamp.game","quietcamp_pro");
                if(await failed.SignIn(username,password)!=ComplimentaryAccessState.Retry||fresh.IsPro)throw new Exception("Unavailable database granted a new lease");
            }
            Console.WriteLine("PASS owner-configured HTTPS "+phase+" against canonical C# lease authority. No Android/UI verification.");
            return 0;
        }
        var data=new EconomySaveData(); var economy=new CampEconomy(data,new EconomyRules(),()=>true); economy.ConfigureComplimentaryAccess(endpoint);
        using var transport=new Transport(endpoint); using var access=new ComplimentaryAccessService(economy,transport,"com.quietcamp.game","quietcamp_pro");
        if(await access.SignIn(username,password)!=ComplimentaryAccessState.Granted||!economy.IsPro)throw new Exception("Real server login did not grant Pro");
        if(await access.Refresh()!=ComplimentaryAccessState.Granted)throw new Exception("Real server session refresh failed");
        var saved=JsonSerializer.Deserialize<EconomySaveData>(JsonSerializer.Serialize(data,options),options);
        if(!string.IsNullOrEmpty(stateFile)) {
            string full=Path.GetFullPath(stateFile), repository=Path.GetFullPath(".")+Path.DirectorySeparatorChar;
            if(full.StartsWith(repository,StringComparison.OrdinalIgnoreCase))throw new InvalidOperationException("Probe bearer token must be saved outside the repository");
            File.WriteAllText(full,JsonSerializer.Serialize(saved,options));
        }
        var restarted=new CampEconomy(saved,new EconomyRules(),()=>true); restarted.ConfigureComplimentaryAccess(endpoint);
        if(!restarted.HasComplimentaryPro)throw new Exception("Saved grant was not restored after client restart");
        using(var resumed=new ComplimentaryAccessService(restarted,transport,"com.quietcamp.game","quietcamp_pro"))
            if(await resumed.Refresh()!=ComplimentaryAccessState.Granted)throw new Exception("Restored token could not refresh");
        if(!local&&!restarted.CanExplorePublishedContent)throw new Exception("Full published-content grant missing");
        if(local) {
            // The owned loopback fixture returns 503 at this route. No production outage is induced.
            using var offline=new Transport(endpoint+"-unavailable");
            DateTime now=DateTime.UtcNow;
            var cached=new CampEconomy(saved.Copy(),new EconomyRules(),()=>true,()=>now); cached.ConfigureComplimentaryAccess(endpoint);
            using var retry=new ComplimentaryAccessService(cached,offline,"com.quietcamp.game","quietcamp_pro");
            if(await retry.Refresh()!=ComplimentaryAccessState.Retry||!cached.HasComplimentaryPro)throw new Exception("HTTP 503 did not preserve a valid cached grant");
            now=now.AddDays(2);
            if(await retry.Refresh()!=ComplimentaryAccessState.Retry||cached.IsPro||cached.CanExplorePublishedContent)throw new Exception("HTTP 503 extended an expired grant");
            var fresh=new CampEconomy(new EconomySaveData(),new EconomyRules(),()=>true); fresh.ConfigureComplimentaryAccess(endpoint);
            using var failed=new ComplimentaryAccessService(fresh,offline,"com.quietcamp.game","quietcamp_pro");
            if(await failed.SignIn(username,password)!=ComplimentaryAccessState.Retry||fresh.IsPro)throw new Exception("HTTP 503 granted access on a fresh client");
        }
        if(await access.SignIn(username,"wrong-password")!=ComplimentaryAccessState.Denied||economy.IsPro)throw new Exception("Invalid credentials granted Pro");
        if(await access.SignIn(username,password)!=ComplimentaryAccessState.Granted)throw new Exception("Reusable credentials failed");
        access.SignOut(); if(economy.IsPro||data.pro)throw new Exception("Sign out or purchase separation failed");
        Console.WriteLine("PASS "+(local?"loopback HTTP":"owner-configured public HTTPS")+" authentication -> C# Pro authority, refresh, client restart/save restore, wrong password, reusable credentials and sign-out. No Android/UI verification.");
        return 0;
    }
}
