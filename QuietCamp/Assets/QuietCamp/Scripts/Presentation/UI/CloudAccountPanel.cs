using System;
using System.Text;
using System.Threading.Tasks;
using QuietCamp.Application;
using UnityEngine;

namespace QuietCamp.Presentation.UI
{
    /// <summary>Shown after privacy setup; the same view is reachable later from Settings.</summary>
    public sealed class CloudAccountPanel : MonoBehaviour
    {
        public static CloudAccountPanel Current { get; private set; }
        public bool Accepted { get; private set; }
        public string MountError => _surface?.LastMountError;
        HtmlSurface _surface;
        public static CloudAccountPanel Create(Transform parent, GameServices services)
        {
            var root=QcUi.Stretch(parent,"CloudAccount");
            var panel=root.gameObject.AddComponent<CloudAccountPanel>(); Current=panel;
            CloudAccountView view=null;
            panel._surface=HtmlSurface.Create(root,"CloudAccountHtml",services,()=>"<scroll class=\"settings-root-scroll\">"+(view?.Render() ?? "<view />")+"</scroll>");
            panel._surface.ExtraCss=Resources.Load<TextAsset>("QuietCamp/Html/CampSettings.css")?.text ?? "";
            view=new CloudAccountView(services,panel._surface,()=>panel.Accepted=true);
            panel._surface.Refresh(); return panel;
        }
        void OnDestroy() { if(Current==this) Current=null; }
    }

    public sealed class CloudAccountView
    {
        readonly GameServices _services;
        readonly HtmlSurface _surface;
        readonly Action _continue;
        bool LinkLocal => _services.Settings.accountChoiceVersion==CloudSaveService.ChoiceVersion;
        bool _confirmCloud, _confirmLocal, _confirmDelete, _confirmSignOut;
        string _notice;
        CloudSaveService Cloud => _services.Cloud;
        string T(string key) => _services.Localization.T("account."+key);
        public CloudAccountView(GameServices services,HtmlSurface surface,Action onContinue=null)
        {
            _services=services; _surface=surface; _continue=onContinue;
            if(Cloud.Allowed && !Cloud.HasAccount) Run(()=>Cloud.LoadOptions());
        }
        string Button(string id,string label,Action action,bool enabled=true)
            => HtmlUi.Button(_surface,"account-"+id,label,action,"quiet",enabled);
        string ProviderButton(string provider, string label, Action action, bool enabled)
        {
            string id = "account-" + provider;
            _surface.Callbacks.Bind(id, action);
            string mark = provider == "google" ? "G" : provider == "facebook" ? "f" : "X";
            return "<button id=\"" + id + "\" class=\"quiet account-provider\" onClick=\"Globals.campUi.Click('" + id + "')\""
                + (enabled ? "" : " disabled=\"true\"") + "><view class=\"account-provider-icon account-provider-" + provider
                + "\"><text>" + mark + "</text></view><text>" + label + "</text></button>";
        }
        async void Run(Func<Task> task)
        {
            try { await task(); }
            catch { _notice=T("Offline"); }
            finally { _surface?.Refresh(); }
        }
        bool MainMenu => UnityEngine.SceneManagement.SceneManager.GetActiveScene().name != "Camp";
        public string Render()
        {
            bool choosing = _continue != null && !Cloud.HasAccount;
            var html=new StringBuilder("<view class=\"surface column account-panel\">");
            html.Append(HtmlUi.Text(T(choosing ? "chooseMethod" : "title"),"settings-group-title"));
            if (!choosing) html.Append(HtmlUi.Text(T("explain"),"s-sub"));
            if (!MainMenu) html.Append(HtmlUi.Text(T("menuOnly"),"s-sub"));
            if (!Cloud.Configured) html.Append(HtmlUi.Text(T("unavailable"),"s-sub"));
            else if (!Cloud.Allowed) html.Append(HtmlUi.Text(T("ageUnavailable"),"s-sub"));
            if (!choosing) html.Append(HtmlUi.Text(T("cloudNotice"),"s-sub"));
            if(Cloud.Linked) html.Append(HtmlUi.Text(T("linked")+" "+Cloud.Provider,"s-sub"));
            else {
                html.Append(HtmlUi.Text(T(choosing ? "localBrief" : "anonymousWarning"),"s-sub"));
                foreach(string provider in new[]{"google","facebook","x"}) {
                    if (choosing && (!Cloud.Allowed || Array.IndexOf(Cloud.Providers,provider)<0)) continue;
                    string captured=provider, label=provider=="google" ? "Google" : provider=="facebook" ? "Facebook" : "X";
                    html.Append(ProviderButton(provider,label,()=>Run(async()=>{
                        await Cloud.SignIn(captured,LinkLocal,_=>{});
                        if(Cloud.Linked) {
                            await Cloud.Sync();
                            if(_continue!=null && Cloud.Status!=CloudSaveStatus.Conflict && Cloud.ContinueAccount()) _continue();
                        }
                    }),MainMenu && Cloud.Allowed && !Cloud.Busy && Array.IndexOf(Cloud.Providers,provider)>=0));
                }
                if (!choosing || Cloud.Allowed && !Cloud.Busy && (Cloud.Providers.Length == 0 || Cloud.Status == CloudSaveStatus.Offline))
                    html.Append(Button("retry",T("retry"),()=>Run(()=>Cloud.LoadOptions()),Cloud.Allowed && !Cloud.Busy));
            }
            if(Cloud.PairCode!=null) html.Append(HtmlUi.Text(T("pairCode")+" "+Cloud.PairCode,"s-sub"));
            if(Cloud.LoginUrl!=null) html.Append(Button("open-browser",T("openBrowser"),()=>UnityEngine.Application.OpenURL(Cloud.LoginUrl)));
            if (!choosing || Cloud.Busy || Cloud.Status != CloudSaveStatus.Local && Cloud.Status != CloudSaveStatus.SignedOut)
                html.Append(HtmlUi.Text(T(Cloud.Status.ToString()),"s-sub"));
            if(_notice!=null) html.Append(HtmlUi.Text(_notice,"s-sub"));
            if(Cloud.Status==CloudSaveStatus.Conflict) {
                html.Append(HtmlUi.Text(T("conflictExplain")+" "+Cloud.CloudCompletedCount,"s-sub"));
                html.Append(Button("use-cloud",T("useCloud"),()=>{_confirmCloud=true;_surface.Refresh();},MainMenu && !Cloud.Busy));
                html.Append(Button("use-local",T("useLocal"),()=>{_confirmLocal=true;_surface.Refresh();},!Cloud.Busy));
            }
            if(_confirmCloud || _confirmLocal || _confirmDelete || _confirmSignOut) {
                html.Append(HtmlUi.Text(T(_confirmDelete ? "deleteConfirm" : _confirmSignOut ? "signOutConfirm" : "replaceConfirm"),"s-sub"));
                html.Append(Button("confirm",T("confirm"),()=>Run(async()=>{
                    if(_confirmDelete) {
                        var endpoint=new Uri(_services.OwnerConfiguration.cloudSaveEndpoint);
                        UnityEngine.Application.OpenURL(endpoint.GetLeftPart(UriPartial.Authority)+"/v1/cloud/delete-account");
                    } else if(_confirmSignOut) await Cloud.SignOut();
                    else await Cloud.ResolveConflict(_confirmCloud);
                    _confirmCloud=_confirmLocal=_confirmDelete=_confirmSignOut=false;
                }),!Cloud.Busy));
                html.Append(Button("cancel-confirm",T("cancel"),()=>{_confirmCloud=_confirmLocal=_confirmDelete=_confirmSignOut=false;_surface.Refresh();}));
            }
            if(Cloud.Busy) html.Append(Button("cancel",T("cancel"),()=>Cloud.Cancel()));
            if(Cloud.HasAccount) {
                html.Append(Button("sync-now",T("syncNow"),()=>Run(async()=>{if(Cloud.ContinueAccount()) await Cloud.Sync();}),Cloud.Allowed && !Cloud.Busy && Cloud.Status!=CloudSaveStatus.Conflict));
                html.Append(Button("sign-out",T("signOut"),()=>{_confirmSignOut=true;_surface.Refresh();},!Cloud.Busy));
                html.Append(Button("delete",T("delete"),()=>{_confirmDelete=true;_surface.Refresh();},Cloud.Allowed && !Cloud.Busy));
            }
            if(_continue!=null) {
                if(Cloud.HasAccount) html.Append(Button("continue",T("continue"),()=>{
                    if(Cloud.Status!=CloudSaveStatus.Conflict && Cloud.ContinueAccount()) _continue();
                },!Cloud.Busy && Cloud.Status!=CloudSaveStatus.Conflict));
            }
            if(!Cloud.HasAccount) html.Append(Button("local-only",T(choosing ? "guestBrief" : "anonymous"),()=>{
                if(Cloud.ContinueLocally()) { if(_continue!=null) _continue(); else _surface.Refresh(); }
            }));
            return html.Append("</view>").ToString();
        }
    }
}
