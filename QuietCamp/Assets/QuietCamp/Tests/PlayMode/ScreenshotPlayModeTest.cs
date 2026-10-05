using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using QuietCamp.Presentation;
using QuietCamp.Presentation.World;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace QuietCamp.Tests
{
    /// <summary>
    /// Captures real gameplay screenshots into QuietCamp/Screenshots/.
    /// Runs in a rendered Game View; batch mode cannot capture composed UI frames.
    /// </summary>
    public class ScreenshotPlayModeTest
    {
        InputSettings.BackgroundBehavior _background;
        InputSettings.EditorInputBehaviorInPlayMode _editorInput;
        [SetUp] public void SyntheticInput()
        {
            _background = InputSystem.settings.backgroundBehavior;
            _editorInput = InputSystem.settings.editorInputBehaviorInPlayMode;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
        }
        [TearDown] public void RestoreInput()
        {
            InputSystem.settings.backgroundBehavior = _background;
            InputSystem.settings.editorInputBehaviorInPlayMode = _editorInput;
        }
        const int W = 1080, H = 1920;
        static int _expectedWidth, _expectedHeight;

        [UnityTest]
        public IEnumerator LeafPaletteMatchesTheActualMenuAndDaylightUnderFullCover()
        {
            if(UnityEngine.Application.isBatchMode)Assert.Ignore("Leaf visual capture requires a rendered Game View.");
            SetGameViewSize(720,1600);
            yield return SceneManager.LoadSceneAsync("Boot");yield return PrivacyBootTestSupport.EnterGame();
            var services=QuietCampBootstrap.ServicesRef;services.Tutorial.Skip();
            bool previousMotion=services.ReducedMotion;services.ReducedMotion=false;
            var dive=FoliageDiveTransition.Ensure(services);
            var dir=Path.Combine(Directory.GetCurrentDirectory(),"Screenshots/LeafLighting");
            IEnumerator Wait(System.Threading.Tasks.Task task)
            {
                float deadline=Time.realtimeSinceStartup+15;
                while(!task.IsCompleted&&Time.realtimeSinceStartup<deadline)yield return null;
                Assert.IsTrue(task.IsCompleted,"Leaf transition did not finish");
                if(task.IsFaulted)Assert.Fail(task.Exception.GetBaseException().Message);
            }
            try
            {
                if(SceneManager.GetActiveScene().name!="MainMenu")yield return SceneManager.LoadSceneAsync("MainMenu");
                yield return Settle(25);
                var menuTint=FoliageDiveTransition.CaptureLighting();
                yield return Wait(dive.CoverAsync());
                Assert.That(Vector4.Distance(menuTint,dive.LeafTint),Is.LessThan(.001f));
                Assert.AreEqual(FoliageDiveTransition.State.CoveredLoading,dive.Current);
                yield return Shot(dir,"01-menu-lighting-folded-leaves");
                services.PendingLevelId="QC001";yield return SceneManager.LoadSceneAsync("Camp");yield return Settle(30);
                Assert.IsTrue(CampSceneHost.Current.IsReady);
                var daylight=FoliageDiveTransition.CaptureLighting();
                Assert.Greater(Vector4.Distance(menuTint,daylight),.025f,"The palette must react to the real destination lighting");
                dive.BeginReveal();yield return new WaitForSecondsRealtime(.3f);
                Assert.AreEqual(FoliageDiveTransition.State.Preparing,dive.Current,"Palette changes while the scene remains covered");
                Assert.That(Vector4.Distance(daylight,dive.LeafTint),Is.LessThan(.001f));
                yield return Shot(dir,"02-daylight-under-full-cover");
                yield return Wait(dive.RevealAsync());yield return Settle(3);
                Assert.IsTrue(dive.IsIdle);
                Assert.IsTrue(services.InputPolicy.CanProcess(Kruty1918.InputRouting.API.GameplayInputKind.Placement,new Vector2(-100,-100)));
                yield return Shot(dir,"03-daylight-revealed");
                yield return Wait(dive.CoverAsync());yield return SceneManager.LoadSceneAsync("MainMenu");yield return Settle(25);
                var returning=FoliageDiveTransition.CaptureLighting();dive.BeginReveal();yield return new WaitForSecondsRealtime(.3f);
                Assert.That(Vector4.Distance(returning,dive.LeafTint),Is.LessThan(.001f));
                yield return Shot(dir,"04-menu-return-under-full-cover");yield return Wait(dive.RevealAsync());
            }
            finally{dive.Recover();services.ReducedMotion=previousMotion;}
        }

        [UnityTest]
        public IEnumerator SeasonalCampsAcrossEveryQualityProfile()
        {
            if (UnityEngine.Application.isBatchMode) Assert.Ignore("Seasonal visual capture requires a rendered Game View.");
#if UNITY_EDITOR
            SetGameViewSize(1080,1920);
#endif
            yield return SceneManager.LoadSceneAsync("Boot");
            yield return PrivacyBootTestSupport.EnterGame();
            var services=QuietCampBootstrap.ServicesRef;
            services.Tutorial.Skip();
            yield return WaitForActiveScene("MainMenu",20);
            yield return Settle(60);
            int previousQuality=services.Settings.quality;
            int previousEffects=services.EffectiveQuality;
            var adaptive=Object.FindAnyObjectByType<AdaptiveCampQuality>();
            bool wasAdaptive=adaptive!=null&&adaptive.enabled;
            // Capture each requested tier deterministically. The timing policy
            // is tested separately; slow Editor shader imports must not turn
            // a High screenshot into an automatically degraded Low scene.
            if(adaptive!=null)adaptive.enabled=false;
            var dir=Path.Combine(Directory.GetCurrentDirectory(),"Screenshots","SeasonalCamps");
            try
            {
                foreach(var id in new[]{"QC001","QC007","gen:qc_camp:3","gen:qc_camp:9","gen:qc_camp:14","gen:qc_camp:19"})
                for(int tier=0;tier<3;tier++)
                {
                    services.Settings.quality=tier+1;
                    services.EffectiveQuality=tier;
                    QualitySettings.SetQualityLevel(tier,true);
                    services.PendingLevelId=id;
                    yield return SceneManager.LoadSceneAsync("Camp");
                    yield return WaitForActiveScene("Camp",20);
                    yield return Settle(45);
                    var host=CampSceneHost.Current;Assert.NotNull(host);Assert.AreEqual(id,host.Session.Level.id);
                    host.Session.DebugApplyWitness();yield return Settle(35);
                    var atmosphere=Object.FindAnyObjectByType<CampAtmosphere>();Assert.NotNull(atmosphere);Assert.NotNull(atmosphere.Environment);
                    Assert.AreEqual(host.Session.Level.environment.seasonId,atmosphere.Environment.Descriptor.seasonId);
                    Assert.Greater(atmosphere.Environment.VisibleClusterCount,0);
                    Assert.AreEqual(tier,atmosphere.QualityTier);
                    Assert.LessOrEqual(Object.FindObjectsByType<Camera>(FindObjectsSortMode.None).Count(c=>c.enabled),1,"Seasonal water must not allocate a reflection camera");
                    if(host.Session.Level.environment.shore!=null)
                        Assert.NotNull(atmosphere.Environment.GetComponentInChildren<CampWaterVisuals>(),id+": authored shore is missing or unsupported");
                    yield return Shot(dir,id.Replace(':','_')+"_"+(tier==0?"Low":tier==1?"Balanced":"High"));
                }
            }
            finally
            {
                services.Settings.quality=previousQuality;
                services.EffectiveQuality=previousEffects;
                if(previousQuality>0)QualitySettings.SetQualityLevel(previousQuality-1,true);
                if(adaptive!=null){adaptive.Configure(services);adaptive.enabled=wasAdaptive;}
            }
        }

        [UnityTest]
        public IEnumerator CaptureProjectShowcase()
        {
            if (UnityEngine.Application.isBatchMode) Assert.Ignore("Showcase capture requires a rendered Game View.");
#if UNITY_EDITOR
            SetGameViewSize(1080,1920);
#endif
            yield return SceneManager.LoadSceneAsync("Boot");
            yield return PrivacyBootTestSupport.EnterGame();
            var services = QuietCampBootstrap.ServicesRef;
            services.Tutorial.Skip();
            services.Progression.Restore(QuietCamp.Infrastructure.LevelLoader.MvpLevelIds(), "QC001", services.Progression.CosmeticFlags);
            services.Localization.TrySetLanguage("en"); services.Settings.language = "en";
            services.Settings.master = 0; services.Audio?.SetBusVolume(Kruty1918.Audio.AudioBus.Master,0);
            services.Settings.quality = 3; services.EffectiveQuality = 2;
            QualitySettings.SetQualityLevel(2,true);
            var adaptive = Object.FindAnyObjectByType<AdaptiveCampQuality>();
            if (adaptive != null) adaptive.enabled = false;
            var directory = Path.Combine(Directory.GetCurrentDirectory(),"Screenshots","Showcase");
            yield return SceneManager.LoadSceneAsync("MainMenu"); yield return Settle(90);
            yield return Shot(directory,"01-main-menu");
            ClickButton("<button #levels>"); yield return Settle(45);
            yield return Shot(directory,"02-campaign-map");
#if UNITY_EDITOR
            SetGameViewSize(1920,1080);
#endif
            var ids = new[] { "QC007", "QC001", "gen:qc_camp:9", "gen:qc_camp:14" };
            var names = new[] { "03-summer-camp", "04-spring-camp", "05-autumn-camp", "06-winter-camp" };
            for (int i=0;i<ids.Length;i++)
            {
                services.PendingLevelId = ids[i];
                yield return SceneManager.LoadSceneAsync("Camp"); yield return WaitForActiveScene("Camp",30);
                yield return Settle(80);
                var host = CampSceneHost.Current; Assert.NotNull(host); Assert.AreEqual(ids[i],host.Session.Level.id);
                host.Session.DebugApplyWitness(); yield return Settle(45);
                Assert.IsTrue(QuietCamp.Domain.RuleEvaluator.Evaluate(host.Session.Level,host.Session.State.Placements).IsSolved);
                yield return Shot(directory,names[i]);
                if (i==0) { host.Session.Check(); Assert.IsTrue(host.Session.IsCompleted); }
            }
            yield return SceneManager.LoadSceneAsync("MainMenu"); yield return Settle(60);
            ClickButton("<button #album>"); yield return Settle(60);
            ClickButton("<button #album-stay>"); yield return Settle(45);
            yield return Shot(directory,"07-own-camp");
            Assert.LessOrEqual(Object.FindObjectsByType<Camera>().Count(camera=>camera.enabled),1);
        }

        [UnityTest]
        public IEnumerator CaptureMenuAndCamp()
        {
#if UNITY_EDITOR
            if (UnityEngine.Application.isBatchMode) Assert.Ignore("Visual capture requires a rendered Game View; run without -batchmode.");
            SetGameViewSize(W, H);
            yield return Settle(5);
#endif
            var outDir = System.IO.Path.Combine(Directory.GetCurrentDirectory(), "Screenshots");
            Directory.CreateDirectory(outDir);

            yield return SceneManager.LoadSceneAsync("Boot", LoadSceneMode.Single);
            yield return PrivacyBootTestSupport.EnterGame();
            QuietCampBootstrap.ServicesRef.Tutorial.Skip(); // Full-control visual QA, separate from first-minute onboarding.
            // Bootstrap redirects Boot→MainMenu only on its first Start;
            // if it already exists (shared play session), go directly.
            yield return Settle(10);
            if (SceneManager.GetActiveScene().name != "MainMenu")
                yield return SceneManager.LoadSceneAsync("MainMenu", LoadSceneMode.Single);
            yield return WaitForActiveScene("MainMenu", 20f);
            yield return Settle(60);
            yield return Shot(outDir, "01_main_menu");

            // Navigate to the Settings screen via the real button — exercises
            // the same action path as a user tap.
            ClickButton("<button #settings>");
            yield return Settle(30);
            yield return Shot(outDir, "02_menu_settings");

            // Category nav: one level in, then ‹ steps back to the list.
            ClickModalButton("<view #sheet>", "<button #set-cat-sound>");
            yield return Settle(25);
            yield return Shot(outDir, "02b_menu_settings_sound");
            ClickModalButton("<view #sheet>", "<button #back>"); // category → list
            yield return Settle(20);

            ClickModalButton("<view #sheet>", "<button #back>"); // Settings → Main
            yield return Settle(20);
            ClickButton("<button #levels>");
            yield return Settle(30);
            yield return Shot(outDir, "03_menu_levels");

            yield return SceneManager.LoadSceneAsync("Camp", LoadSceneMode.Single);
            yield return WaitForActiveScene("Camp", 20f);
            yield return new WaitUntil(() => CampSceneHost.Current != null);
            yield return Settle(60);
            var centerPointer = new UnityEngine.EventSystems.PointerEventData(UnityEngine.EventSystems.EventSystem.current)
                { position = new Vector2(Screen.width * .5f, Screen.height * .5f) };
            var centerHits = new System.Collections.Generic.List<UnityEngine.EventSystems.RaycastResult>();
            UnityEngine.EventSystems.EventSystem.current.RaycastAll(centerPointer, centerHits);
            Assert.IsFalse(centerHits.Any(hit => hit.gameObject.GetComponentInParent<QuietCamp.Presentation.UI.HtmlSurface>() != null),
                "Transparent HTML canvas blocks the board");
            // Commit the witness layout so the shot shows tents like the
            // reference render (door markers, footprints, rotations).
            CampSceneHost.Current.Session.DebugApplyWitness();
            yield return Settle(40);
            yield return Shot(outDir, "04_camp_day");

            // Pause modal → shot; then in-game settings modal → shot.
            ClickButton("<button #pause>");
            yield return Settle(30);
            yield return Shot(outDir, "05_camp_pause");

            ClickModalButton("<view #sheet>", "<button #settings>");
            yield return Settle(30);
            yield return Shot(outDir, "06_camp_settings");

            // Solving the level flips the scene to the evening preset —
            // burning campfire, dusk light, completion panel.
            ClickModalButton("<view #sheet>", "<button #back>");
            yield return Settle(20);
            // Resume closes the pause sheet so completion opens on a clean HUD.
            ClickModalButton("<view #sheet>", "<button #resume>");
            yield return Settle(30);
            CampSceneHost.Current.Session.Check();
            // The completed camp remains until the player chooses Next.
            yield return Settle(2);
            yield return Shot(outDir, "07_camp_evening");

            // The player explicitly starts the next foliage transition.
            var next = FindButton(null, "<button #next>");
            if (next != null) TryTapButton(next);
            FoliageDiveTransition dive = null;
            for (var i = 0; i < 240 && dive == null; i++)
            {
                dive = Object.FindObjectsByType<FoliageDiveTransition>(FindObjectsSortMode.None)
                    .FirstOrDefault();
                yield return null;
            }
            Assert.IsNotNull(dive, "Transition overlay never appeared");
            var coverFrame = 0;
            var coveredShot = false;
            for (var i = 0; i < 1800 && dive != null && !dive.IsIdle; i++)
            {
                if (dive.Current == FoliageDiveTransition.State.Covering)
                {
                    // Overwrite every few frames — the last write lands at
                    // the deepest cover progress before the state flips.
                    if (coverFrame++ % 4 == 0) yield return Shot(outDir, "08_transition_cover");
                }
                else if (!coveredShot
                    && dive.Current == FoliageDiveTransition.State.CoveredLoading)
                {
                    yield return Shot(outDir, "09_transition_covered");
                    coveredShot = true;
                }
                yield return null;
            }
            // Let navigation finish so teardown starts from a clean state.
            for (var i = 0; i < 3600 && dive != null && !dive.IsIdle; i++)
                yield return null;

            foreach (var n in new[] { "01_main_menu", "02_menu_settings", "03_menu_levels",
                "04_camp_day", "05_camp_pause", "06_camp_settings", "07_camp_evening" })
                Assert.IsTrue(File.Exists(Path.Combine(outDir, n + ".png")), "Missing screenshot " + n);
        }

        [UnityTest]
        public IEnumerator NarrowScreenPlacementUndoRedoCheck()
        {
            if (UnityEngine.Application.isBatchMode) Assert.Ignore("Visual capture requires a rendered Game View.");
            SetGameViewSize(720, 1600);
            yield return SceneManager.LoadSceneAsync("Boot");
            yield return PrivacyBootTestSupport.EnterGame();
            QuietCampBootstrap.ServicesRef.Tutorial.Skip(); // Full-control visual QA, separate from first-minute onboarding.
            yield return Settle(20);
            yield return SceneManager.LoadSceneAsync("MainMenu");
            yield return Settle(60);
            ClickButton("<button #continue>");
            yield return WaitForActiveScene("Camp", 25f);
            yield return Settle(100);
            var host = CampSceneHost.Current;
            Assert.IsNotNull(host);
            var dir = Path.Combine(Directory.GetCurrentDirectory(), "Screenshots");
            yield return Shot(dir, "10_narrow_before");
            yield return Settle(5);
            var tutorial = Object.FindObjectsByType<RectTransform>(FindObjectsSortMode.None)
                .FirstOrDefault(t => t.name == "<view #tutorial>");
            if (!string.IsNullOrEmpty(host.Session.Level.tutorialKey))
                Assert.IsNotNull(tutorial, "Tutorial must remain visible until the player acts");
            if (tutorial != null)
            {
                var pause = FindButton(null, "<button #pause>");
                Assert.IsFalse(ScreenRect(tutorial).Overlaps(ScreenRect((RectTransform)pause.transform)),
                    "Tutorial overlaps pause");
            }
            host.Session.Restore(new QuietCamp.Domain.Placement[0], null);
            yield return Settle(10);
            var before = host.Session.State.Count;
            yield return PlaceFromCard(host);
            yield return Settle(20);
            Assert.AreEqual(before + 1, host.Session.State.Count);
            ClickButton("<button #undo>");
            yield return Settle(20);
            Assert.AreEqual(before, host.Session.State.Count);
            ClickButton("<button #redo>");
            yield return Settle(20);
            Assert.AreEqual(before + 1, host.Session.State.Count);
            var guestName = host.Session.Level.guests.First(g => g.id == host.Session.SelectedGuestId).nameKey;
            var text = QuietCamp.Presentation.UI.LocalizedLabel.Localization.T(guestName);
            var labels = Object.FindObjectsByType<TMPro.TMP_Text>(FindObjectsSortMode.None)
                .Where(t => t.text == text && t.GetComponentsInParent<Transform>().Any(p => p.name.Contains("tent-chip")));
            Assert.AreEqual(1, labels.Count(), "Selected guest has duplicate anchored labels");
            var selected = host.Session.SelectedGuestId;
            var original = host.Session.State.Find(selected);
            ClickButton("<button #rotate>");
            yield return Settle(20);
            Assert.AreEqual((original.rotation + 1) % 4, host.Session.State.Find(selected).rotation,
                "Rotate did not update a legal placement");
            ClickButton("<button #undo>"); yield return Settle(20);
            Assert.AreEqual(original.rotation, host.Session.State.Find(selected).rotation);
            ClickButton("<button #remove>"); yield return Settle(20);
            Assert.AreEqual(before, host.Session.State.Count);
            ClickButton("<button #undo>"); yield return Settle(20);
            Assert.AreEqual(before + 1, host.Session.State.Count);
            yield return Shot(dir, "11_narrow_placed");
            yield return Settle(5);
            for (int i=0; i<10 && host.Session.State.Count < host.Session.Level.guests.Length; i++)
            {
                yield return PlaceFromCard(host);
                yield return Settle(20);
            }
            yield return Shot(dir, "12_narrow_allplaced");
            yield return Settle(5);
            ClickButton("<button #check>");
            yield return Settle(30);
            Assert.IsTrue(host.Session.IsCompleted, "Check did not complete the manually placed level");
            yield return Shot(dir, "13_narrow_checked");
            yield return Settle(10);
        }

        static IEnumerator PlaceFromCard(CampSceneHost host)
        {
            var before = host.Session.State.Count;
            ClickButton("<button #guest-card>");
            yield return Settle(5);
            Assert.AreEqual(before, host.Session.State.Count, "The guest card should select without solving the puzzle");
            var pose = host.Session.Level.witness.First(p => p.guestId == host.Session.SelectedGuestId);
            var mouse = InputSystem.AddDevice<Mouse>();
            try
            {
                var point = (Vector2)Camera.main.WorldToScreenPoint(BoardMath.CellCenter(host.Session.Level, pose.x, pose.z));
                InputSystem.QueueStateEvent(mouse, new MouseState { position = point }.WithButton(MouseButton.Left));
                yield return null;
                InputSystem.QueueStateEvent(mouse, new MouseState { position = point });
                yield return Settle(20);
                Assert.AreEqual(before + 1, host.Session.State.Count, "Board tap failed to place the selected guest");
                for (var q = 0; q < pose.rotation; q++)
                {
                    ClickButton("<button #rotate>");
                    yield return Settle(10);
                }
            }
            finally { InputSystem.RemoveDevice(mouse); }
        }

        [UnityTest]
        public IEnumerator GameplayLanguagesAndLargeText()
        {
            if (UnityEngine.Application.isBatchMode) Assert.Ignore("Visual capture requires a rendered Game View.");
            SetGameViewSize(720, 1600);
            yield return SceneManager.LoadSceneAsync("Boot");
            yield return PrivacyBootTestSupport.EnterGame();
            QuietCampBootstrap.ServicesRef.Tutorial.Skip(); // Full-control visual QA, separate from first-minute onboarding.
            yield return Settle(20);
            var bootstrap = Object.FindAnyObjectByType<QuietCampBootstrap>();
            var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
            var services = (GameServices)typeof(QuietCampBootstrap).GetField("_services", flags).GetValue(bootstrap);
            var previousScale = services.Settings.textScale;
            var previousLanguage = services.Localization.CurrentLanguageId;
            var dir = Path.Combine(Directory.GetCurrentDirectory(), "Screenshots");
            try
            {
                foreach (var language in new[] { "uk", "en", "de" })
                {
                    services.Settings.textScale = 1.3f;
                    services.Localization.TrySetLanguage(language);
                    services.PendingLevelId = "QC010";
                    yield return SceneManager.LoadSceneAsync("Camp");
                    yield return Settle(60);
                    var host = CampSceneHost.Current;
                    Assert.AreEqual("QC010", host.Session.Level.id);
                    // Choose the shade guest through the real guest sheet.
                    ClickButton("<button #guests>"); yield return Settle(20);
                    ClickModalButton("<view #sheet>", "<button #guest-2>"); yield return Settle(30);
                    var card = FindButton(null, "<button #guest-card>");
                    Assert.IsNotNull(card);
                    var bounds = ScreenRect((RectTransform)card.transform);
                    foreach (var label in card.GetComponentsInChildren<TMPro.TMP_Text>())
                    {
                        label.ForceMeshUpdate();
                        var corners = new[] { label.transform.TransformPoint(label.textBounds.min),
                            label.transform.TransformPoint(label.textBounds.max) };
                        Assert.GreaterOrEqual(corners[0].x, bounds.xMin - 2f, "Guest text escapes the left edge: " + label.text);
                        Assert.LessOrEqual(corners[1].x, bounds.xMax + 2f, "Guest text escapes the right edge: " + label.text);
                    }
                    yield return Shot(dir, "16_gameplay_" + language + "_130");
                    host.Session.DebugApplyWitness();
                    host.Session.Select(host.Session.Level.guests[2].id);
                    yield return Settle(30);
                    foreach (var id in new[] { "pause", "undo", "rotate", "remove" })
                    {
                        var control = FindButton(null, "<button #" + id + ">");
                        Assert.IsNotNull(control);
                        var area = ScreenRect((RectTransform)control.transform);
                        Assert.GreaterOrEqual(area.width, 96f - 1f, "Touch target is too narrow on a 720px phone: " + id);
                        Assert.GreaterOrEqual(area.height, 96f - 1f, "Touch target is too short on a 720px phone: " + id);
                        Assert.GreaterOrEqual(area.xMin, 0f, "Control clips off the left edge: " + id);
                        Assert.LessOrEqual(area.xMax, Screen.width, "Control clips off the right edge: " + id);
                    }
                    yield return Shot(dir, "17_gameplay_selected_" + language + "_130");
                }
            }
            finally
            {
                services.Settings.textScale = previousScale;
                services.Localization.TrySetLanguage(previousLanguage);
                foreach (var surface in Object.FindObjectsByType<QuietCamp.Presentation.UI.HtmlSurface>()) surface.Refresh();
            }
        }

        [UnityTest]
        public IEnumerator SettingsLanguagesAndLargeText()
        {
            if (UnityEngine.Application.isBatchMode) Assert.Ignore("Visual capture requires a rendered Game View.");
            SetGameViewSize(720, 1600);
            yield return SceneManager.LoadSceneAsync("Boot");
            yield return PrivacyBootTestSupport.EnterGame();
            QuietCampBootstrap.ServicesRef.Tutorial.Skip(); // Full-control visual QA, separate from first-minute onboarding.
            yield return Settle(20);
            yield return SceneManager.LoadSceneAsync("MainMenu");
            yield return Settle(40);
            var dir = Path.Combine(Directory.GetCurrentDirectory(), "Screenshots");
            foreach (var language in new[] { "uk", "en", "de" })
            {
                ClickButton("<button #settings>");
                yield return Settle(20);
                // Language is on the settings overview; readability now lives
                // in Comfort rather than the graphics category.
                ClickModalButton("<view #sheet>", "<button #language-" + language + ">");
                yield return Settle(15);
                Assert.AreEqual(language, QuietCamp.Presentation.UI.LocalizedLabel.Localization.CurrentLanguageId);
                ClickModalButton("<view #sheet>", "<button #set-cat-comfort>");
                yield return Settle(20);
                var slider = Object.FindObjectsByType<UnityEngine.UI.Slider>(FindObjectsSortMode.None)
                    .First(t => t.name.Contains("settings.textSize"));
                slider.value = 1.3f;
                slider.GetComponentInParent<QuietCamp.Presentation.UI.HtmlSurface>().Callbacks.Click("settings.textSize.end");
                yield return Settle(20);
                // Changing font CSS remounts the document in UnityHTML. Check
                // the live restored control rather than the retired instance.
                slider = Object.FindObjectsByType<UnityEngine.UI.Slider>(FindObjectsSortMode.None)
                    .First(t => t.name.Contains("settings.textSize"));
                Assert.AreEqual(1.3f, slider.value, .001f, "Reconciliation clamped the text-size slider");
                var valueLabel = slider.GetComponentInChildren<TMPro.TMP_Text>();
                valueLabel.ForceMeshUpdate();
                Assert.Less(ScreenRect(slider.handleRect).xMax,
                    valueLabel.transform.TransformPoint(valueLabel.textBounds.min).x,
                    "Slider handle overlaps its percentage at 130% text size");
                var saved = new QuietCamp.Infrastructure.SaveAdapter();
                Assert.IsTrue(saved.Load(out var saveError), saveError);
                Assert.AreEqual(1.3f, saved.Settings.textScale, .001f, "Text size was not persisted");
                yield return Shot(dir, "14_settings_" + language + "_130");
                ClickModalButton("<view #sheet>", "<button #back>");
                yield return Settle(20);
                ClickModalButton("<view #sheet>", "<button #back>");
                yield return Settle(20);
                yield return Shot(dir, "15_menu_" + language + "_130");
                Assert.IsNotNull(FindButton(null, "<button #continue>"));
                foreach (var id in new[] { "levels", "album" })
                {
                    var card = FindButton(null, "<button #" + id + ">");
                    var cardRect = ScreenRect((RectTransform)card.transform);
                    foreach (var label in card.GetComponentsInChildren<TMPro.TMP_Text>())
                    {
                        label.ForceMeshUpdate();
                        var edge = label.transform.TransformPoint(label.textBounds.max);
                        Assert.LessOrEqual(edge.x, cardRect.xMax + 1f, "Menu card label overflows: " + label.text);
                    }
                }
            }
            // Restore this test profile's ordinary language and scale.
            ClickButton("<button #settings>"); yield return Settle(20);
            ClickModalButton("<view #sheet>", "<button #language-uk>"); yield return Settle(15);
            ClickModalButton("<view #sheet>", "<button #set-cat-comfort>"); yield return Settle(20);
            Object.FindObjectsByType<UnityEngine.UI.Slider>(FindObjectsSortMode.None)
                .First(t => t.name.Contains("settings.textSize")).value = 1f;
            yield return Settle(10);
        }

        [UnityTest]
        public IEnumerator MenuAllPhasesAndAlbumNavigation()
        {
            if (UnityEngine.Application.isBatchMode) Assert.Ignore("Visual capture requires a rendered Game View.");
            var dir = Path.Combine(Directory.GetCurrentDirectory(), "Screenshots/menu");
            SetGameViewSize(1080, 1920);
            yield return SceneManager.LoadSceneAsync("Boot");
            yield return PrivacyBootTestSupport.EnterGame();
            QuietCampBootstrap.ServicesRef.Tutorial.Skip(); // Full-control visual QA, separate from first-minute onboarding.
            yield return Settle(20);
            yield return SceneManager.LoadSceneAsync("MainMenu");
            yield return Settle(60);
            var atmosphere = Object.FindAnyObjectByType<CampAtmosphere>();
            Assert.IsNotNull(atmosphere);
            var catalog = QuietCamp.Infrastructure.AtmosphereCatalog.Load();
            foreach (var phase in new[] { "morning", "noon", "evening", "night" })
            {
                var profile = catalog.Get(phase);
                MenuDiorama.ApplySun(RenderSettings.sun, profile);
                atmosphere.Apply(profile);
                yield return new WaitForSecondsRealtime(4.5f);
                yield return Shot(dir, "menu-" + phase);
                AssertMenuRegions();
            }
            SetGameViewSize(1200, 1600);
            yield return Settle(30);
            yield return Shot(dir, "menu-tablet-night");
            AssertMenuRegions();
            ClickButton("<button #album>");
            yield return Settle(30);
            yield return Shot(dir, "menu-album");
            ClickModalButton("<view #sheet>", "<button #back>");
            yield return Settle(30);
            AssertMenuRegions();
        }

        static void AssertMenuRegions()
        {
            var brand = Object.FindObjectsByType<RectTransform>(FindObjectsSortMode.None)
                .First(t => t.name == "<view #menu-brand>");
            var primary = FindButton(null, "<button #continue>");
            var settings = FindButton(null, "<button #settings>");
            Assert.IsNotNull(primary); Assert.IsNotNull(settings);
            var brandRect = ScreenRect(brand);
            Assert.IsFalse(brandRect.Overlaps(ScreenRect((RectTransform)primary.transform)),
                "Wordmark overlaps the primary menu action");
            Assert.IsFalse(brandRect.Overlaps(ScreenRect((RectTransform)settings.transform)),
                "Wordmark overlaps settings");
            foreach (var label in brand.GetComponentsInChildren<TMPro.TMP_Text>())
            {
                label.ForceMeshUpdate();
                Assert.LessOrEqual(label.transform.TransformPoint(label.textBounds.max).x,
                    brandRect.xMax + 1f, "Menu wordmark overflows: " + label.text);
            }
            // The campfire is framed above navigation; atmospheric shading
            // must never make the open scene intercept menu input.
            var point = (Vector2)Camera.main.WorldToScreenPoint(new Vector3(0f, 0f, -.15f));
            var pointer = new UnityEngine.EventSystems.PointerEventData(UnityEngine.EventSystems.EventSystem.current)
                { position = point };
            var hits = new System.Collections.Generic.List<UnityEngine.EventSystems.RaycastResult>();
            UnityEngine.EventSystems.EventSystem.current.RaycastAll(pointer, hits);
            Assert.IsFalse(hits.Any(hit => hit.gameObject.GetComponentInParent<QuietCamp.Presentation.UI.HtmlSurface>() != null),
                "The wordmark/navigation/shading covers the campfire");
        }

        static Rect ScreenRect(RectTransform transform)
        {
            var corners = new Vector3[4]; transform.GetWorldCorners(corners);
            return Rect.MinMaxRect(corners[0].x, corners[0].y, corners[2].x, corners[2].y);
        }

        static void ClickButton(string goName)
        {
            var btn = FindButton(null, goName);
            Assert.IsNotNull(btn, $"Button '{goName}' not found");
            TapButton(btn);
        }

        static void ClickModalButton(string panelName, string goName)
        {
            var panel = Object.FindObjectsByType<Transform>(FindObjectsSortMode.None)
                .FirstOrDefault(t => t.name == panelName && t.gameObject.activeInHierarchy);
            Assert.IsNotNull(panel, $"Modal panel '{panelName}' not active");
            var btn = FindButton(panel, goName);
            Assert.IsNotNull(btn, $"Button '{goName}' not found under {panelName}");
            TapButton(btn);
        }

        /// <summary>Tap only if the button is the top raycast hit — used for
        /// optional taps that may already be covered by a transition.</summary>
        static bool TryTapButton(UnityEngine.UI.Button button)
        {
            Canvas.ForceUpdateCanvases();
            var rect = (RectTransform)button.transform;
            var canvas = button.GetComponentInParent<Canvas>();
            var camera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
            var position = RectTransformUtility.WorldToScreenPoint(camera, rect.TransformPoint(rect.rect.center));
            var pointer = new UnityEngine.EventSystems.PointerEventData(UnityEngine.EventSystems.EventSystem.current) { position = position };
            var hits = new System.Collections.Generic.List<UnityEngine.EventSystems.RaycastResult>();
            UnityEngine.EventSystems.EventSystem.current.RaycastAll(pointer, hits);
            if (!(hits.Count > 0 && (hits[0].gameObject.transform == rect || hits[0].gameObject.transform.IsChildOf(rect))))
                return false;
            UnityEngine.EventSystems.ExecuteEvents.Execute(button.gameObject, pointer, UnityEngine.EventSystems.ExecuteEvents.pointerClickHandler);
            return true;
        }

        static void TapButton(UnityEngine.UI.Button button)
        {
            Canvas.ForceUpdateCanvases();
            var rect = (RectTransform)button.transform;
            var canvas = button.GetComponentInParent<Canvas>();
            var camera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
            var position = RectTransformUtility.WorldToScreenPoint(camera, rect.TransformPoint(rect.rect.center));
            var pointer = new UnityEngine.EventSystems.PointerEventData(UnityEngine.EventSystems.EventSystem.current) { position = position };
            var hits = new System.Collections.Generic.List<UnityEngine.EventSystems.RaycastResult>();
            UnityEngine.EventSystems.EventSystem.current.RaycastAll(pointer, hits);
            if (!(hits.Count > 0 && (hits[0].gameObject.transform == rect || hits[0].gameObject.transform.IsChildOf(rect))))
            {
                Debug.Log("[QC-UI] raycast at " + position + " for " + button.name + " → " +
                    string.Join(" | ", hits.ConvertAll(h => h.gameObject.name + "(" + TmpPath(h.gameObject.transform) + ")")));
            }
            Assert.IsTrue(hits.Count > 0 && (hits[0].gameObject.transform == rect || hits[0].gameObject.transform.IsChildOf(rect)),
                "HTML button is covered or outside its hit area: " + button.name);
            UnityEngine.EventSystems.ExecuteEvents.Execute(button.gameObject, pointer, UnityEngine.EventSystems.ExecuteEvents.pointerClickHandler);
        }

        static UnityEngine.UI.Button FindButton(Transform root, string name)
        {
            var buttons = root == null
                ? Object.FindObjectsByType<UnityEngine.UI.Button>(FindObjectsSortMode.None)
                : root.GetComponentsInChildren<UnityEngine.UI.Button>(true);
            return buttons.FirstOrDefault(b => b.name == name && b.gameObject.activeInHierarchy);
        }

#if UNITY_EDITOR
        static void SetGameViewSize(int width, int height)
        {
            Assert.IsTrue(UnityEngine.Application.productName.Contains("QA"),"Viewport capture must use the isolated QA Editor.");
            // An inherited Simulator window overrides Screen.width/safeArea
            // with its selected phone whenever focus changes on scene loads.
            // Only one PlayMode view may own the synthetic viewport.
            foreach(var window in Resources.FindObjectsOfTypeAll<UnityEditor.EditorWindow>())
                if(window.GetType().FullName=="UnityEditor.DeviceSimulation.SimulatorWindow")window.Close();
            var assembly = typeof(UnityEditor.Editor).Assembly;
            var sizesType = assembly.GetType("UnityEditor.GameViewSizes");
            var singleton = typeof(UnityEditor.ScriptableSingleton<>).MakeGenericType(sizesType);
            var sizes = singleton.GetProperty("instance").GetValue(null);
            var groupType = assembly.GetType("UnityEditor.GameViewSizeGroupType");
            var viewType = assembly.GetType("UnityEditor.GameView");
            var view = UnityEditor.EditorWindow.GetWindow(viewType);
            // A newly created floating Game View can inherit a tiny utility
            // window rectangle. Its fixed target dimensions alone do not
            // guarantee a fully composed camera/UI capture.
            if(!view.docked&&(view.position.width<640||view.position.height<480))
                view.position=new Rect(64,64,1024,768);
            var platform = UnityEditor.EditorUserBuildSettings.activeBuildTarget == UnityEditor.BuildTarget.Android
                ? "Android" : "Standalone";
            // The active profile and the currently displayed size group can
            // differ after importing settings into an isolated QA project.
            var displayedGroup = viewType.GetProperty("currentSizeGroupType", System.Reflection.BindingFlags.Static
                | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic)?.GetValue(null)
                ?? System.Enum.Parse(groupType, platform);
            var group = sizesType.GetMethod("GetGroup").Invoke(sizes, new[] { displayedGroup });
            var sizeType = assembly.GetType("UnityEditor.GameViewSize");
            var kindType = assembly.GetType("UnityEditor.GameViewSizeType");
            var size = System.Activator.CreateInstance(sizeType, new[] { System.Enum.Parse(kindType, "FixedResolution"), (object)width, height, "Quiet Camp HTML verification" });
            group.GetType().GetMethod("AddCustomSize").Invoke(group, new[] { size });
            var count = (int)group.GetType().GetMethod("GetTotalCount").Invoke(group, null);
            view.Focus();
            UnityEngine.Application.runInBackground = true;
            viewType.GetProperty("selectedSizeIndex", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic).SetValue(view, count - 1);
            view.Repaint();
            _expectedWidth = width; _expectedHeight = height;
        }
#endif

        static IEnumerator WaitForActiveScene(string name, float timeout)
        {
            var deadline = Time.realtimeSinceStartup + timeout;
            while (SceneManager.GetActiveScene().name != name)
            {
                if (Time.realtimeSinceStartup > deadline)
                    Assert.Fail($"Scene {name} did not become active.");
                yield return null;
            }
        }

        static IEnumerator Settle(int frames)
        {
            for (var i = 0; i < frames; i++) yield return null;
        }

        static string TmpPath(Transform t)
        {
            var s = t.name;
            while (t.parent != null) { t = t.parent; s = t.name + "/" + s; }
            return s;
        }

        static IEnumerator Shot(string dir, string name)
        {
            // Wait for the asynchronous composed-frame capture before navigating.
            // Retargeting overlay canvases changes occlusion; WaitForEndOfFrame
            // can stall when an editor window has focus instead of Game View.
            Directory.CreateDirectory(dir);
            var path = Path.Combine(dir, name + ".png");
            if (File.Exists(path)) File.Delete(path);
            ScreenCapture.CaptureScreenshot(path);
            var deadline = Time.realtimeSinceStartup + 15f;
            while (!File.Exists(path) && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.IsTrue(File.Exists(path), "Composed screenshot did not finish: " + name);
            var tex = new Texture2D(2, 2);
            try
            {
                Assert.IsTrue(tex.LoadImage(File.ReadAllBytes(path)), "Invalid screenshot: " + name);
                Assert.AreEqual(_expectedWidth, tex.width, "Game View did not apply the requested width");
                Assert.AreEqual(_expectedHeight, tex.height, "Game View did not apply the requested height");
                Assert.Less(MagentaFraction(tex), .02f, name + ": missing shader");
                var pixels=tex.GetPixels32();int visible=0,samples=0;
                for(int i=0;i<pixels.Length;i+=97)
                { var p=pixels[i];samples++;if(p.r>2||p.g>2||p.b>2)visible++; }
                Assert.Greater((float)visible/samples,.1f,name+": composed frame is almost empty; check the QA Game View layout");
            }
            finally { Object.DestroyImmediate(tex); }
        }

        static float MagentaFraction(Texture2D tex)
        {
            var px = tex.GetPixels32();
            var hits = 0;
            for (var i = 0; i < px.Length; i += 7) // sample every 7th pixel
            {
                var c = px[i];
                if (c.r > 180 && c.b > 180 && c.g < 90) hits++;
            }
            return (float)hits / (px.Length / 7f);
        }
    }
}
