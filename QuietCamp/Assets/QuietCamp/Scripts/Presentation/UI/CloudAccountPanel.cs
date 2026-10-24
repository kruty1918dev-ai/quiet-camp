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
            if(Cloud.Allowed) Run(async()=>{ await Cloud.LoadOptions(); if(Cloud.HasAccount) await Cloud.RefreshProfile(); });
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
        async Task SignIn(string provider, string label, bool link=false, bool browser=false)
        {
            if (!Cloud.Configured) { _notice=string.Format(T("setupRequired"),label); return; }
            if (!Cloud.Allowed) { _notice=T("unavailable"); return; }
            if (Array.IndexOf(Cloud.Providers,provider)<0) await Cloud.LoadOptions();
            if (Array.IndexOf(Cloud.Providers,provider)<0) { _notice=string.Format(T("providerUnavailable"),label); return; }
            if(!browser && Cloud.CanSignInNatively(provider)) await Cloud.SignInNative(provider,link);
            else await Cloud.SignIn(provider,link,UnityEngine.Application.OpenURL);
            if(Cloud.Linked) {
                await Cloud.Sync();
                if(_continue!=null && Cloud.Status!=CloudSaveStatus.Conflict && Cloud.ContinueAccount()) _continue();
            }
            else _notice=T(Cloud.Status.ToString());
        }
        void OpenAccountPage(bool deletion=false)
        {
            var endpoint=_services.OwnerConfiguration.cloudSaveEndpoint.TrimEnd('/');
            if(!QuietCamp.Infrastructure.CloudTransport.ValidEndpoint(endpoint)) return;
            string language=_services.Localization.CurrentLanguageId;
            if(language!="uk" && language!="de") language="en";
            UnityEngine.Application.OpenURL(endpoint+(deletion?"/delete-account":"/account?lang="+language));
        }
        async Task DeleteAccount()
        {
            if(Cloud.CanDeleteNatively) await Cloud.DeleteNative(true);
            else OpenAccountPage(true);
            _confirmDelete=false;
        }
        string RenderChoice()
        {
            var html = new StringBuilder("<view class=\"account-entry\"><view class=\"account-choice\">");
            html.Append(HtmlUi.Text(T("chooseMethod"),"account-choice-title"));
            html.Append("<view class=\"account-choice-icons\">");
            foreach (string provider in new[]{"google","facebook"}) {
                string captured=provider, label=provider=="google" ? "Google" : "Facebook";
                html.Append(ProviderButton(provider,label,()=>Run(()=>SignIn(captured,label)),!Cloud.Busy && !Cloud.DeletionNeedsRetry,true));
            }
            _surface.Callbacks.Bind("account-local-only",()=> { if(Cloud.ContinueLocally()) _continue(); });
            html.Append("<view class=\"account-choice-item\"><button id=\"account-local-only\" class=\"quiet account-provider\" onClick=\"Globals.campUi.Click('account-local-only')\"><img class=\"account-brand-image\" src=\"res:QuietCamp/UI/Icons/guests\"/></button>"
                + HtmlUi.Text(T("guestBrief"),"account-choice-label") + "</view></view>");
            if (Cloud.Busy) html.Append(HtmlUi.Text(T(Cloud.Status==CloudSaveStatus.Offline ? "Offline" : "Working"),"account-choice-status") + Button("cancel",T("cancel"),()=>Cloud.Cancel()));
            if (Cloud.PairCode!=null) html.Append(HtmlUi.Text(T("pairCode")+" "+Cloud.PairCode,"account-choice-status"));
            if (_notice!=null) html.Append(HtmlUi.Text(_notice,"account-choice-status"));
            if (Cloud.LoginUrl!=null) html.Append(Button("open-browser",T("openBrowser"),()=>UnityEngine.Application.OpenURL(Cloud.LoginUrl)));
            if(!Cloud.Busy && Cloud.DeletionNeedsRetry) {
                html.Append(Button("delete-retry",T("retryDeletion"),()=>{_confirmDelete=true;_surface.Refresh();}));
                html.Append(Button("delete-help",T("deletionHelp"),()=>OpenAccountPage(true)));
            } else if(!Cloud.Busy && _notice!=null && Cloud.CanSignInNatively("google"))
                html.Append(Button("browser-fallback",T("browserGoogle"),()=>Run(()=>SignIn("google","Google",browser:true))));
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
            if (_continue != null && !Cloud.HasAccount && !_confirmDelete) return RenderChoice();
            var html=new StringBuilder("<view class=\"surface column account-panel\">");
            html.Append(HtmlUi.Text(T("title"),"settings-group-title"));
            html.Append(HtmlUi.Text(T("explain"),"s-sub"));
            if (!MainMenu) html.Append(HtmlUi.Text(T("menuOnly"),"s-sub"));
            if (!Cloud.Configured) html.Append(HtmlUi.Text(T("unavailable"),"s-sub"));
            else if (!Cloud.Allowed) html.Append(HtmlUi.Text(T("unavailable"),"s-sub"));
            html.Append(HtmlUi.Text(T("cloudNotice"),"s-sub"));
            if(Cloud.Linked) {
                html.Append(HtmlUi.Text(T("linked")+" "+Cloud.Provider,"s-sub"));
                if(Cloud.Profile!=null) {
                    html.Append(HtmlUi.Text(Cloud.Profile.displayName,"s-sub"));
                    if(!string.IsNullOrEmpty(Cloud.Profile.email)) html.Append(HtmlUi.Text(Cloud.Profile.email,"s-sub"));
                    html.Append(HtmlUi.Text(string.Join(", ",Cloud.Profile.providers ?? Array.Empty<string>()),"s-sub"));
                }
                if(Cloud.AccountPortalSupported) html.Append(Button("portal",T("portal"),()=>OpenAccountPage()));
            }
            else {
                html.Append(HtmlUi.Text(T("anonymousWarning"),"s-sub"));
                foreach(string provider in new[]{"google","facebook"}) {
                    string captured=provider, label=provider=="google" ? "Google" : "Facebook";
                    bool available=MainMenu && Cloud.Allowed && !Cloud.Busy && !Cloud.DeletionNeedsRetry && Array.IndexOf(Cloud.Providers,provider)>=0;
                    html.Append(ProviderButton(provider,label,()=>Run(()=>SignIn(captured,label)),available));
                    if(LinkLocal) html.Append(Button("link-"+provider,string.Format(T("linkNew"),label),()=>Run(()=>SignIn(captured,label,link:true)),available));
                }
                if(Cloud.CanSignInNatively("google") && !Cloud.DeletionNeedsRetry)
                    html.Append(Button("browser-fallback",T("browserGoogle"),()=>Run(()=>SignIn("google","Google",browser:true)),MainMenu && !Cloud.Busy));
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
                html.Append(HtmlUi.Text(T(_confirmDelete ? (Cloud.CanDeleteNatively ? "nativeDeleteConfirm" : "deleteConfirm") : _confirmSignOut ? "signOutConfirm" : "replaceConfirm"),"s-sub"));
                html.Append(Button("confirm",T("confirm"),()=>Run(async()=>{
                    if(_confirmDelete) {
                        await DeleteAccount();
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
            if(Cloud.DeletionNeedsRetry) {
                html.Append(Button("delete-retry",T("retryDeletion"),()=>{_confirmDelete=true;_surface.Refresh();},Cloud.Allowed && !Cloud.Busy));
                html.Append(Button("delete-help",T("deletionHelp"),()=>OpenAccountPage(true)));
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
