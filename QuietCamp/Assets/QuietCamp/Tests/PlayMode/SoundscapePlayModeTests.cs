using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using Kruty1918.Audio;
using NUnit.Framework;
using QuietCamp.Application;
using QuietCamp.Domain;
using QuietCamp.Infrastructure;
using QuietCamp.Presentation;
using QuietCamp.Presentation.World;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace QuietCamp.Tests
{
    public class SoundscapePlayModeTests
    {
        static IEnumerator Frames(int count) { while (count-- > 0) yield return null; }
        static IEnumerator Boot()
        {
            foreach (var old in Object.FindObjectsByType<QuietCampBootstrap>()) Object.Destroy(old.gameObject);
            yield return Frames(2);
            yield return SceneManager.LoadSceneAsync("Boot");
            yield return PrivacyBootTestSupport.EnterGame();
            var deadline = Time.realtimeSinceStartup + 25;
            while (!(Object.FindAnyObjectByType<QuietCampBootstrap>()?.StartupReady ?? false) && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.IsTrue(MenuSceneHost.Current?.UiReady ?? false);
            var services = QuietCampBootstrap.ServicesRef;
            services.Audio.SetBusVolume(AudioBus.Master, 1); services.Audio.SetBusVolume(AudioBus.Music, 1);
            services.Audio.SetBusVolume(AudioBus.Ambience, 1); services.Audio.SetBusVolume(AudioBus.Sfx, 1);
        }
        static void Tap(string id)
        {
            var button = Object.FindObjectsByType<Button>().FirstOrDefault(b => b.name == "<button #" + id + ">" && b.gameObject.activeInHierarchy);
            Assert.NotNull(button,id); Assert.IsTrue(button.interactable,id); button.onClick.Invoke();
        }

        [Test] public void ZeroInitialFadeCanBecomeAudibleAndPreservesAuthoredSpatialBlend()
        {
            var catalog = QuietCampAudioCatalog.Load(); var audio = new AudioService(catalog,catalog); audio.Initialize();
            try
            {
                var bed = audio.Play("ambience.wind",new AudioPlayOptions(initialPlaybackScale:0));
                Assert.IsTrue(bed.IsValid); Assert.AreEqual(0,bed.Source.volume);
                audio.SetPlaybackScale(bed,.8f); Assert.Greater(bed.Source.volume, .01f);
                audio.SetBusVolume(AudioBus.Master,0); Assert.AreEqual(0,bed.Source.volume);
                audio.SetBusVolume(AudioBus.Master,1); Assert.Greater(bed.Source.volume,.01f);
                var cloth = audio.PlayAt("sfx.tent.lift",Vector3.one);
                Assert.AreEqual(audio.GetSound("sfx.tent.lift").SpatialBlend,cloth.Source.spatialBlend,.001f);
            }
            finally { audio.Dispose(); }
        }

        [Test] public void LoopStopReleasesSlotWhilePausedAndOldHandleCannotStopItsReplacement()
        {
            var catalog = QuietCampAudioCatalog.Load(); var audio = new AudioService(catalog,catalog); audio.Initialize();
            bool oldPause=AudioListener.pause;
            try
            {
                var first=audio.Play("ambience.rain"); Assert.IsTrue(first.IsValid);
                Assert.IsFalse(audio.Play("ambience.rain").IsValid);
                AudioListener.pause=true; first.Stop(); Assert.IsFalse(first.IsValid);
                var second=audio.Play("ambience.rain"); Assert.IsTrue(second.IsValid,"Explicit stop must free the simultaneous slot immediately.");
                first.Stop(); Assert.IsTrue(second.IsValid);
                audio.SetPlaybackScale(second,.5f); Assert.Greater(second.Source.volume,0);
            }
            finally { AudioListener.pause=oldPause; audio.Dispose(); }
        }

        [Test] public void NewSurfaceDetailsHaveClipsAndMobileImportSettings()
        {
            var catalog=QuietCampAudioCatalog.Load();
            foreach(var key in new[]{"ambience.rain","ambience.rain.canvas","sfx.fire.quench","sfx.rain.drip","sfx.tent.gust","sfx.tent.drag","ambience.bird","level.complete"})
            {
                Assert.IsTrue(catalog.TryGet(key,out var sound),key);Assert.NotNull(sound.Clip,key);
                Assert.AreEqual(48000,sound.Clip.frequency,key);
                Assert.AreEqual(sound.Loop?AudioClipLoadType.Streaming:AudioClipLoadType.DecompressOnLoad,sound.Clip.loadType,key);
                if(sound.SpatialBlend>0)Assert.AreEqual(1,sound.Clip.channels,key);
                Assert.AreEqual(0,sound.DopplerLevel,key);
            }
        }

        [UnityTest] public IEnumerator DestroyingAParentedEmitterFreesItsPerKeySlot()
        {
            var catalog=QuietCampAudioCatalog.Load();var audio=new AudioService(catalog,catalog);audio.Initialize();
            var parent=new GameObject("disposable-sound-emitter");
            try
            {
                var old=audio.Play("ambience.rain.canvas",new AudioPlayOptions(parent:parent.transform));Assert.IsTrue(old.IsValid);
                Object.Destroy(parent);yield return null;audio.Tick();
                Assert.IsFalse(old.IsValid);
                Assert.IsTrue(audio.Play("ambience.rain.canvas").IsValid,"Destroyed emitters cannot permanently reserve a loop slot.");
            }
            finally { Object.Destroy(parent);audio.Dispose(); }
        }

        [UnityTest] public IEnumerator RejectedRequestsCannotAdvanceVariantHistory()
        {
            var catalog=Object.Instantiate(QuietCampAudioCatalog.Load());
            Assert.IsTrue(catalog.TryGet("sfx.tent.lift",out var definition));
            definition.Variants=definition.Variants.Take(2).ToArray();definition.Cooldown=.1f;
            var audio=new AudioService(catalog,catalog);audio.Initialize();
            try
            {
                var first=audio.Play("sfx.tent.lift");var firstClip=first.Source.clip;
                Assert.IsFalse(audio.Play("sfx.tent.lift").IsValid,"The second request is inside cooldown.");
                first.Stop();audio.Tick();yield return new WaitForSecondsRealtime(.13f);
                var second=audio.Play("sfx.tent.lift");Assert.IsTrue(second.IsValid);
                Assert.AreNotSame(firstClip,second.Source.clip,"A rejected request cannot consume the alternate Foley variant.");
            }
            finally { audio.Dispose();Object.Destroy(catalog); }
        }

        [UnityTest] public IEnumerator BootstrapBedsHaveNonzeroGainAfterTheFade()
        {
            yield return Boot(); yield return Frames(40);
            var services=QuietCampBootstrap.ServicesRef;
            Assert.IsTrue(services.MusicBedHandle.IsValid,"Music must retain its playback handle.");
            Assert.IsTrue(services.AmbientWindHandle.IsValid,"Wind must retain its playback handle.");
            // Rendered Editor runs can lose Game View focus between tests.
            // Test the foreground state explicitly, restoring the host state.
            bool paused=AudioListener.pause;
            try
            {
                AudioListener.pause=false;yield return Frames(3);
                Assert.IsTrue(services.MusicBedHandle.IsPlaying,"Music must resume in foreground.");Assert.Greater(services.MusicBedHandle.Source.volume,.001f);
                Assert.IsTrue(services.AmbientWindHandle.IsPlaying,"Wind must resume in foreground.");Assert.Greater(services.AmbientWindHandle.Source.volume,.001f);
            }
            finally { AudioListener.pause=paused; }
        }

        [UnityTest] public IEnumerator SurfaceRainQuenchAndSuspensionKeepOneOwnerAndNoLeakedLoops()
        {
            yield return Boot();var services=QuietCampBootstrap.ServicesRef;services.PendingLevelId="QC_TEST";
            yield return SceneManager.LoadSceneAsync("Camp");yield return Frames(30);
            var host=CampSceneHost.Current;Assert.IsTrue(host.IsReady);
            host.Session.Restore(Array.Empty<Placement>(),null);
            foreach(var placement in host.Session.Level.witness)
                Assert.IsTrue(host.Session.TryCommit(PlacementCommand.Place(placement.guestId,placement.x,placement.z,placement.rotation),out _));
            host.SetAtmospherePhase("evening");yield return Frames(3);
            var sound=host.Atmosphere.Soundscape;var before=Newtonsoft.Json.JsonConvert.SerializeObject(host.Session.State.Placements);
            sound.Advance(3);Assert.IsTrue(sound.FireVoice.IsValid);Assert.Greater(sound.FireVoice.Source.volume,0);
            host.Atmosphere.Weather.Advance(78,1,true,Vector2.right);sound.Advance(2);
            Assert.IsTrue(sound.RainVoice.IsValid);Assert.Greater(sound.RainVoice.Source.volume,0);
            Assert.IsTrue(sound.CanvasRainVoice.IsValid);Assert.Greater(sound.CanvasRainVoice.Source.volume,0,"Rain on tent fabric must be audible.");
            host.Atmosphere.RainShelter.Advance(5,.7f);sound.Advance(2);
            Assert.IsFalse(sound.FireVoice.IsValid,"An extinguished fire cannot keep a loop slot.");
            Assert.IsTrue(Object.FindObjectsByType<AudioSource>().Any(s=>s.clip!=null&&s.clip.name.StartsWith("fire_quench")));
            sound.gameObject.SetActive(false);Assert.AreEqual(0,sound.OwnedVoiceCount);
            Assert.IsTrue(services.MusicBedHandle.IsValid,"World suspension cannot stop persistent music.");
            sound.gameObject.SetActive(true);sound.Advance(2);
            Assert.IsTrue(sound.RainVoice.IsValid,"Rain must restart after suspension, without a leaked per-key slot.");
            Assert.AreEqual(before,Newtonsoft.Json.JsonConvert.SerializeObject(host.Session.State.Placements));
        }

        [UnityTest] public IEnumerator AlbumRetainsListenerAndOwnsItsFireAcrossSwitchAndBack()
        {
            yield return Boot();var services=QuietCampBootstrap.ServicesRef;
            var previous=services.Save.Album.entries;var level=LevelLoader.Load("QC_TEST");
            services.Save.Album.entries=new[]{new AlbumSaveData.Entry{levelId=level.id,levelSnapshot=CampContent.Snapshot(level),placements=level.witness,lighting="evening"},
                new AlbumSaveData.Entry{levelId=level.id,levelSnapshot=CampContent.Snapshot(level),placements=level.witness,lighting="night"}};
            services.ReducedMotion=false;services.AlbumIndex=0;
            try
            {
                Tap("album");yield return Frames(25);
                var album=Object.FindAnyObjectByType<AlbumDiorama>();Assert.NotNull(album.Atmosphere);
                Assert.AreEqual(1,Object.FindObjectsByType<AudioListener>().Count(l=>l.enabled));
                album.Atmosphere.Soundscape.Advance(3);var old=album.Atmosphere.Soundscape.FireVoice;Assert.IsTrue(old.IsValid);
                Assert.AreEqual(BoardMath.CellCenterWorld(level,new Cell(level.noise[0][0],level.noise[0][1]))+Vector3.up*.4f,old.Source.transform.position);
                Tap("album-1");yield return Frames(70);
                Assert.IsFalse(old.IsValid);Assert.AreEqual(1,album.DisplayedIndex);
                album.Atmosphere.Soundscape.Advance(3);Assert.IsTrue(album.Atmosphere.Soundscape.FireVoice.IsValid);
                Tap("back");yield return Frames(20);
                Assert.AreEqual(1,Object.FindObjectsByType<AudioListener>().Count(l=>l.enabled));
                Assert.IsNull(album.Atmosphere);Assert.AreEqual(1,Object.FindObjectsByType<CampSoundscape>().Length);
            }
            finally { services.Save.Album.entries=previous; }
        }

        [UnityTest] public IEnumerator OptionalSceneryFailureLeavesMenuNavigationUsable()
        {
            yield return Boot();var host=MenuSceneHost.Current;
            LogAssert.Expect(LogType.Warning,new Regex("Menu scenery unavailable; navigation remains usable"));
            typeof(MenuSceneHost).GetMethod("TryBuildWorld",BindingFlags.NonPublic|BindingFlags.Instance)
                .Invoke(host,new object[]{new Action(()=>throw new NullReferenceException("Simulated stripped decor component"))});
            yield return Frames(3);Assert.IsTrue(host.UiReady);
            Assert.AreEqual(1,Object.FindObjectsByType<AudioListener>().Count(l=>l.enabled));
            Tap("continue");yield return Frames(140);
            Assert.IsTrue(CampSceneHost.Current?.UiReady??false,"The player must still be able to start gameplay.");
        }
    }
}
