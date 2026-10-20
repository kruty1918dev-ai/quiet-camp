using System;
using QuietCamp.Application;
using GoogleMobileAds.Api;
using GoogleMobileAds.Ump.Api;
using UnityEngine;
using System.Threading.Tasks;

static class Probe {
 static int checks;
 static void Check(bool ok,string message) { checks++;if(!ok)throw new Exception(message); }
 static SettingsSaveData Settings(PrivacyAgeBand band) { var s=new SettingsSaveData();PrivacyEligibility.RecordAge(s,band);return s; }
 static async Task Main() {
  foreach(PrivacyAgeBand band in Enum.GetValues<PrivacyAgeBand>()) {
   var s=Settings(band);bool eligible=band==PrivacyAgeBand.From16To17||band==PrivacyAgeBand.Adult;
   var a=new AnalyticsDouble();var c=new CrashDouble();int factories=0;
   using var analytics=new ComfortAnalytics(s,a);
   using var crashes=new CrashReportConsent(s,c);
   using var ads=new AgeRestrictedAdService(s,()=>{factories++;return new AdDouble();});
   Check(!analytics.Consented&&!crashes.Consented,"no implicit consent");
   await analytics.SetConsent(true);await crashes.SetConsent(true);
   analytics.BeginLevel(1,4,4,0);analytics.EndLevel(ComfortOutcome.Completed);
   Check(analytics.Collecting==eligible && crashes.Collecting==eligible,"age gate");
   Check((await ads.ShowRewarded("test")==AdResult.Completed)==eligible,"ad age gate");
   Check(eligible||a.Initializations+c.Initializations+factories==0,"no provider initialization under 16");
   await analytics.SetConsent(false);Check(!analytics.Collecting&&crashes.Collecting==eligible,"independent crash consent");
   await crashes.SetConsent(false);Check(c.Deletes>0&&!crashes.Collecting,"withdrawal purges pending");
  }
  {
   var s=Settings(PrivacyAgeBand.Adult);var t=new AnalyticsDouble { Pending=new() };using var a=new ComfortAnalytics(s,t);
   var start=a.SetConsent(true);PrivacyEligibility.RecordAge(s,PrivacyAgeBand.From13To15);await a.SetConsent(false);t.Pending.SetResult(true);await start;
   Check(!a.Collecting&&!t.Enabled,"age change wins initialization race");
  }
  {
   var s=Settings(PrivacyAgeBand.Adult);var t=new CrashDouble { Pending=new() };using var c=new CrashReportConsent(s,t);
   var start=c.SetConsent(true);await c.SetConsent(false);t.Pending.SetResult(true);await start;
   Check(!c.Collecting&&!t.Enabled,"crash withdrawal wins initialization race");
  }
  {
   var s=Settings(PrivacyAgeBand.Adult);s.analyticsConsent=true;s.analyticsPolicyVersion=ComfortAnalytics.PolicyVersion;s.analyticsPublicationRevision="old";
   s.crashReportingConsent=true;s.crashReportingPolicyVersion=CrashReportConsent.PolicyVersion;s.crashReportingPublicationRevision="old";
   var a=new AnalyticsDouble();var c=new CrashDouble();using var analytics=new ComfortAnalytics(s,a,publicationRevision:"new");using var crashes=new CrashReportConsent(s,c,publicationRevision:"new");
   await analytics.RestoreConsent();await crashes.RestoreConsent();Check(a.Initializations+c.Initializations==0,"stale policy cannot restore collection");
  }
  {
   var s=new SettingsSaveData { analyticsConsent=true, crashReportingConsent=true };var a=new AnalyticsDouble();var c=new CrashDouble();
   using var analytics=new ComfortAnalytics(s,a);using var crashes=new CrashReportConsent(s,c);
   await analytics.SetConsent(true);await crashes.SetConsent(true);Check(a.Initializations+c.Initializations==0,"legacy save without age stays off");
  }
  {
   var s=Settings(PrivacyAgeBand.Adult);s.analyticsConsent=true;s.crashReportingConsent=true;s.advertisingConsent=true;
   PrivacyEligibility.RecordAge(s,PrivacyAgeBand.Adult);Check(s.analyticsConsent&&s.crashReportingConsent&&s.advertisingConsent,"same age selection preserves choices");
   PrivacyEligibility.RecordAge(s,PrivacyAgeBand.From16To17);Check(!s.analyticsConsent&&!s.crashReportingConsent&&!s.advertisingConsent,"changed age clears all optional choices");
   using var provider=new GoogleRewardedAdapter("publisher-unit",false,s,"r");int updates=ConsentInformation.Updates;
   await provider.RefreshPrivacyAtLaunch();Check(ConsentInformation.Updates==updates,"no advertising permission means no UMP request");
  }
  {
   var settings=Settings(PrivacyAgeBand.From13To15);int factories=0;
   using var a=new ComfortAnalytics(settings,new DeferredComfortTransport(()=>PrivacyEligibility.OptionalServicesAllowed(settings),()=>{factories++;return new AnalyticsDouble();}));
   using var c=new CrashReportConsent(settings,new DeferredCrashReportTransport(()=>PrivacyEligibility.OptionalServicesAllowed(settings),()=>{factories++;return new CrashDouble();}));
   await a.SetConsent(true);await c.SetConsent(true);Check(factories==0,"no SDK factories for under16 even on forced choice");
   PrivacyEligibility.RecordAge(settings,PrivacyAgeBand.Adult);Check(a.Available&&c.Available&&factories==0,"availability inspection does not initialize SDKs");
   await a.SetConsent(true);Check(factories==1,"analytics factory only after opt-in");await c.SetConsent(true);Check(factories==2,"crash factory only after independent opt-in");
  }
  {
   var s=Settings(PrivacyAgeBand.Adult);var t=new AnalyticsDouble();var crash=new CrashDouble();
   using var a=new ComfortAnalytics(s,t,persist:()=>false);using var c=new CrashReportConsent(s,crash,persist:()=>false);
   await a.SetConsent(true);await c.SetConsent(true);
   Check(!a.ChoicePersisted&&!c.ChoicePersisted,"save failure is visible");Check(t.Initializations+crash.Initializations==0,"failed consent persistence cannot activate SDKs");
  }
  foreach(var band in new[]{PrivacyAgeBand.From16To17,PrivacyAgeBand.Adult}) {
   var s=Settings(band);s.advertisingConsent=true;s.advertisingPolicyVersion=1;s.advertisingPublicationRevision="r";
   var provider=new GoogleRewardedAdapter("publisher-unit",false,s,"r");
   int initializations=MobileAds.Initializations, loads=RewardedAd.Loads;
   await provider.RefreshPrivacyAtLaunch();Check(MobileAds.Initializations==initializations,"launch UMP does not initialize ads");
   var result=await provider.ShowRewarded("test");Check(result==AdResult.Completed,"reward callback plus close");
   Check(RewardedAd.Request.Extras["npa"]=="1","always NPA");
   Check(MobileAds.Configuration.AgeRestrictedTreatment==(band==PrivacyAgeBand.From16To17?AgeRestrictedTreatment.Teen:AgeRestrictedTreatment.Unspecified),"correct age treatment");
   AndroidJavaObject.Purposes="0";Check(await provider.ShowRewarded("test")==AdResult.Unavailable&&RewardedAd.Loads==loads+1,"refusing storage blocks ad load");AndroidJavaObject.Purposes="1";
   AndroidJavaObject.Vendors="";Check(await provider.ShowRewarded("test")==AdResult.Unavailable&&RewardedAd.Loads==loads+1,"missing Google vendor consent blocks ad load");AndroidJavaObject.Vendors=new string('1',755);
   s.advertisingConsent=false;Check(!provider.IsReady,"ad permission withdraws availability");provider.Dispose();
  }
  {
   using var crash=new FirebaseCrashReportTransport();Check(Firebase.FirebaseApp.Initializations==0,"crash constructor does not initialize Firebase");
   Check(await crash.Initialize(),"conditional crash adapter initializes");crash.SetCollection(true);Check(Firebase.Crashlytics.Crashlytics.IsCrashlyticsCollectionEnabled&&!Firebase.Analytics.FirebaseAnalytics.Enabled,"crash-only choice does not enable analytics");
   crash.SetCollection(false);crash.DeletePendingReports();
   using var analytics=new FirebaseComfortTransport();await analytics.Initialize();analytics.SetConsent(true);
   Check(Firebase.Analytics.FirebaseAnalytics.Enabled&&!Firebase.Crashlytics.Crashlytics.IsCrashlyticsCollectionEnabled,"analytics-only choice does not enable crash reporting");
  }
  Console.WriteLine($"PASS {checks} privacy contracts. SDK doubles; not native/runtime or player-build evidence.");
 }
 sealed class AnalyticsDouble:IComfortTransport {
  public int Initializations;public bool Enabled;public TaskCompletionSource<bool> Pending;public bool Available=>true;
  public Task<bool> Initialize(){Initializations++;return Pending?.Task??Task.FromResult(true);}public void SetConsent(bool v)=>Enabled=v;
  public void ResetLocalData(){}public void Send(ComfortRecord r){}public void Dispose(){}
 }
 sealed class CrashDouble:ICrashReportTransport {
  public int Initializations,Deletes;public bool Enabled;public TaskCompletionSource<bool> Pending;public bool Available=>true;
  public Task<bool> Initialize(){Initializations++;return Pending?.Task??Task.FromResult(true);}public void SetCollection(bool v)=>Enabled=v;
  public void DeletePendingReports()=>Deletes++;public void Dispose(){}
 }
 sealed class AdDouble:IAdService { public bool IsReady=>true;public void ShowBanner(string p){}public void HideBanner(){}public Task<AdResult> ShowRewarded(string p)=>Task.FromResult(AdResult.Completed); }
}
