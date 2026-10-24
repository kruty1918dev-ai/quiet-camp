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
            panel._surface=HtmlSurface.Create(root,"CloudAccountHtml",services,()=>services.Cloud.HasAccount
                ? "<scroll class=\"settings-root-scroll\">"+(view?.Render() ?? "<view />")+"</scroll>" : view?.Render() ?? "<view />");
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
        bool LinkLocal => _services.Settings.accountChoiceVersion==CloudSaveService.ChoiceVersion && !Cloud.NeedsReauthentication;
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
        string ProviderButton(string provider, string label, Action action, bool enabled, bool compact = false)
        {
            string id = "account-" + provider;
            _surface.Callbacks.Bind(id, action);
            string mark = provider == "google" ? "<img class=\"account-brand-image\" src=\"res:QuietCamp/UI/Icons/google-sign-in\"/>"
                : "<text>f</text>";
            string button = "<button id=\"" + id + "\" class=\"quiet account-provider\" data-tooltip=\"" + label + "\" onClick=\"Globals.campUi.Click('" + id + "')\""
                + (enabled ? "" : " disabled=\"true\"") + "><view class=\"account-provider-icon account-provider-" + provider
                + "\">" + mark + "</view>" + (compact ? "" : "<text>" + label + "</text>") + "</button>";
            return compact ? "<view class=\"account-choice-item\">" + button + HtmlUi.Text(label,"account-choice-label") + "</view>" : button;
        }
        async Task SignIn(string provider, string label)
        {
            if (!Cloud.Configured) { _notice=string.Format(T("setupRequired"),label); return; }
            if (!Cloud.Allowed) { _notice=T("ageUnavailable"); return; }
            if (Array.IndexOf(Cloud.Providers,provider)<0) await Cloud.LoadOptions();
            if (Array.IndexOf(Cloud.Providers,provider)<0) { _notice=string.Format(T("providerUnavailable"),label); return; }
            await Cloud.SignIn(provider,LinkLocal,UnityEngine.Application.OpenURL);
            if(Cloud.Linked) {
                await Cloud.Sync();
                if(_continue!=null && Cloud.Status!=CloudSaveStatus.Conflict && Cloud.ContinueAccount()) _continue();
            }
            else _notice=T(Cloud.Status.ToString());
        }
        string RenderChoice()
        {
            var html = new StringBuilder("<view class=\"account-entry\"><view class=\"account-choice\">");
            html.Append(HtmlUi.Text(T("chooseMethod"),"account-choice-title"));
            html.Append("<view class=\"account-choice-icons\">");
            foreach (string provider in new[]{"google","facebook"}) {
                string captured=provider, label=provider=="google" ? "Google" : "Facebook";
                html.Append(ProviderButton(provider,label,()=>Run(()=>SignIn(captured,label)),!Cloud.Busy,true));
            }
            _surface.Callbacks.Bind("account-local-only",()=> { if(Cloud.ContinueLocally()) _continue(); });
            html.Append("<view class=\"account-choice-item\"><button id=\"account-local-only\" class=\"quiet account-provider\" onClick=\"Globals.campUi.Click('account-local-only')\"><img class=\"account-brand-image\" src=\"res:QuietCamp/UI/Icons/guests\"/></button>"
                + HtmlUi.Text(T("guestBrief"),"account-choice-label") + "</view></view>");
            if (Cloud.Busy) html.Append(HtmlUi.Text(T(Cloud.Status==CloudSaveStatus.Offline ? "Offline" : "Working"),"account-choice-status") + Button("cancel",T("cancel"),()=>Cloud.Cancel()));
            if (Cloud.PairCode!=null) html.Append(HtmlUi.Text(T("pairCode")+" "+Cloud.PairCode,"account-choice-status"));
            if (_notice!=null) html.Append(HtmlUi.Text(_notice,"account-choice-status"));
            if (Cloud.LoginUrl!=null) html.Append(Button("open-browser",T("openBrowser"),()=>UnityEngine.Application.OpenURL(Cloud.LoginUrl)));
            return html.Append("</view></view>").ToString();
        }
        async void Run(Func<Task> task)
        {
            _notice=null;
            try { await task(); }
            catch { _notice=T("Offline"); }
            finally { _surface?.Refresh(); }
        }
        bool MainMenu => UnityEngine.SceneManagement.SceneManager.GetActiveScene().name != "Camp";
        public string Render()
        {
            if (_continue != null && !Cloud.HasAccount) return RenderChoice();
            var html=new StringBuilder("<view class=\"surface column account-panel\">");
            html.Append(HtmlUi.Text(T("title"),"settings-group-title"));
            html.Append(HtmlUi.Text(T("explain"),"s-sub"));
            if (!MainMenu) html.Append(HtmlUi.Text(T("menuOnly"),"s-sub"));
            if (!Cloud.Configured) html.Append(HtmlUi.Text(T("unavailable"),"s-sub"));
            else if (!Cloud.Allowed) html.Append(HtmlUi.Text(T("ageUnavailable"),"s-sub"));
            html.Append(HtmlUi.Text(T("cloudNotice"),"s-sub"));
            if(Cloud.Linked) html.Append(HtmlUi.Text(T("linked")+" "+Cloud.Provider,"s-sub"));
            else {
                html.Append(HtmlUi.Text(T("anonymousWarning"),"s-sub"));
                foreach(string provider in new[]{"google","facebook"}) {
                    string captured=provider, label=provider=="google" ? "Google" : "Facebook";
                    html.Append(ProviderButton(provider,label,()=>Run(()=>SignIn(captured,label)),MainMenu && Cloud.Allowed && !Cloud.Busy && Array.IndexOf(Cloud.Providers,provider)>=0));
                }
                html.Append(Button("retry",T("retry"),()=>Run(()=>Cloud.LoadOptions()),Cloud.Allowed && !Cloud.Busy));
            }
            if(Cloud.PairCode!=null) html.Append(HtmlUi.Text(T("pairCode")+" "+Cloud.PairCode,"s-sub"));
            if(Cloud.LoginUrl!=null) html.Append(Button("open-browser",T("openBrowser"),()=>UnityEngine.Application.OpenURL(Cloud.LoginUrl)));
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
            if(!Cloud.HasAccount) html.Append(Button("local-only",T("anonymous"),()=>{
                if(Cloud.ContinueLocally()) { if(_continue!=null) _continue(); else _surface.Refresh(); }
            }));
            return html.Append("</view>").ToString();
        }
    }
}
