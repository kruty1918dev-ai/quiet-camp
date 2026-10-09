using System;
using System.Collections;
using QuietCamp.Domain;
using QuietCamp.Infrastructure;
using QuietCamp.Presentation.UI;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace QuietCamp.Presentation.World
{
    public sealed class AlbumDiorama : MonoBehaviour
    {
        GameServices _services; MenuScreens _screens; Camera _camera; Transform _menuWorld;
        CampAtmosphere _menuAtmosphere; MenuCameraDrift _drift;
        GameObject _world; BoardRenderer _board; CampAtmosphere _atmosphere;
        LevelData _level; RectTransform _viewport; float _yaw; Vector2Int _size; bool _refit;
        Image _veil; Coroutine _swap; int _pending=-1,_displayed=-1; float _orbitOffset,_zoom=1;
        Color _swapTint = new Color(.24f,.34f,.27f,0);
        public bool IsSwitching=>_swap!=null;
        public int DisplayedIndex=>_displayed;
        public CampAtmosphere Atmosphere=>_atmosphere;
        public void Configure(GameServices services,MenuScreens screens,Camera camera,Transform menuWorld,CampAtmosphere atmosphere,MenuCameraDrift drift)
        {
            _services=services;_screens=screens;_camera=camera;_menuWorld=menuWorld;_menuAtmosphere=atmosphere;_drift=drift;
            screens.ScreenChanged+=OnScreen; screens.AlbumSelected+=Select; screens.OverlayMounted+=Bind; screens.MemoryReplay += ReplayMemory;
        }
        void OnScreen(string screen)
        {
            var album=(screen=="Album" || screen=="AlbumQuiet") && (_services.Save.Album.entries?.Length??0)>0;
            var preview=screen=="JourneyPreview" && (_screens.SelectedJourney?.levelIds.Length??0)>0;
            var map=screen=="Levels"||screen=="BonusPreview"||screen=="BranchPreview";
            if (_menuWorld != null) _menuWorld.gameObject.SetActive(!album&&!map&&!preview);
            if (_drift!=null)_drift.enabled=!album&&!map&&!preview;
            _menuAtmosphere?.SetSuspended(album||map||preview);
            if(map){StopSwap();Release();return;}
            try
            {
                if(album) Select(_services.AlbumIndex);
                else if(preview)
                {
                    StopSwap(); var journey = _screens.SelectedJourney;
                    BuildCamp(new QuietCamp.Application.AlbumSaveData.Entry { levelId = journey.previewLevelId ?? journey.levelIds[0] });
                }
                else { StopSwap();Release();RenderSettings.sun=_menuWorld?.Find("DirectionalLight")?.GetComponent<Light>(); _menuAtmosphere?.Apply(AtmosphereCatalog.Load().Get(_menuAtmosphere.PhaseId)); }
            }
            catch { StopSwap(); Release(); }
        }
        void Select(int index)
        {
            var entries=_services.Save.Album.entries;
            if((_screens.Current!="Album" && _screens.Current!="AlbumQuiet")||entries==null||entries.Length==0)return;
            index=Mathf.Clamp(index,0,entries.Length-1);
            if(index==_displayed&&_swap==null)return;
            _pending=index;
            if(_world==null||_services.ReducedMotion){StopSwap();Build(index);return;}
            if(_veil==null)
            {
                var rect=QcUi.Stretch(_screens.Overlay.transform.parent,"AlbumSwapVeil");
                rect.SetSiblingIndex(_screens.Overlay.transform.GetSiblingIndex());
                _veil=rect.gameObject.AddComponent<Image>();_veil.raycastTarget=false;
                _veil.color=_swapTint;
            }
            if(_swap==null)
            {
                // Match the actual scene air, so wide screens never flash cream-white.
                _swapTint=Color.Lerp(RenderSettings.fogColor,RenderSettings.ambientLight,.28f);
                _swapTint=Color.Lerp(_swapTint,SeasonPalette.For(_level).CanopyDark,.32f);_swapTint.a=0;
                _swap=StartCoroutine(Swap());
            }
        }
        IEnumerator Swap()
        {
            do
            {
                yield return Fade(true);
                int target=_pending;Build(target);
                // Let the replaced world's owned materials and volumes dispose first.
                yield return null;
                _atmosphere?.Apply(AtmosphereCatalog.Load().Get(_atmosphere.PhaseId));
                yield return Fade(false);
            }while(_screens.Current=="Album"&&_displayed!=_pending);
            _swap=null;_orbitOffset=0;_zoom=1;_refit=true;
        }
        IEnumerator Fade(bool covering)
        {
            float duration=.34f*_services.MotionScale;
            for(float time=0;time<duration;time+=Time.unscaledDeltaTime)
            {
                if(_services.ReducedMotion)break;
                float t=Mathf.SmoothStep(0,1,time/duration);float weight=covering?t:1-t;
                var tint=_swapTint;tint.a=weight;_veil.color=tint;
                _atmosphere?.Soundscape?.SetVisibility(1-weight);
                _orbitOffset=covering?-7*t:7*(1-t);_zoom=1+.035f*weight;_refit=true;
                yield return null;
            }
            var finalTint=_swapTint;finalTint.a=covering?1:0;_veil.color=finalTint;
            _atmosphere?.Soundscape?.SetVisibility(covering?0:1);
        }
        void StopSwap()
        {
            if(_swap!=null)StopCoroutine(_swap);_swap=null;
            _orbitOffset=0;_zoom=1;_displayed=-1;
            if(_veil!=null){var tint=_swapTint;tint.a=0;_veil.color=tint;}
        }
        void Build(int index)
        {
            var entries=_services.Save.Album.entries; _displayed=index;
            BuildCamp(entries[Mathf.Clamp(index,0,entries.Length-1)]);
        }
        void BuildCamp(QuietCamp.Application.AlbumSaveData.Entry entry)
        {
            Release(); _level=CampContent.AlbumLevel(entry);
            _world=new GameObject("AlbumDioramaWorld");
            Transform Child(string name){var g=new GameObject(name);g.transform.SetParent(_world.transform,false);return g.transform;}
            var baseRoot=Child("Base");var grid=Child("Grid");var obstacle=Child("Obstacle");var tent=Child("Tents");var overlay=Child("Overlay");var decor=Child("Decor");
            _board=new BoardRenderer(_level,_services.Assets,baseRoot,grid,obstacle,tent,overlay);
            _board.SyncPlacements(entry.placements??Array.Empty<Placement>(),_level,1,true);
            TentStoryVisual.CloseAll(_world.transform,true);
            DecorSpawner.Spawn(_level,_services.Assets,decor);
            if (entry.cared) foreach (var story in _world.GetComponentsInChildren<TentStoryVisual>()) story.ShowCare();
            _yaw=0;_refit=true;
            var profile=AtmosphereCatalog.Load().Get(entry.lighting??_level.lighting??"day");
            var sun=MenuDiorama.CreateSun(_world.transform);MenuDiorama.ApplySun(sun,profile);
            RenderSettings.sun=sun;
            _board.BuildCanopies();
            _atmosphere=_world.AddComponent<CampAtmosphere>();
            _atmosphere.Configure(_camera,_level,_viewport,profile,()=>_services.ReducedMotion,_services.EffectiveQuality,decor,()=>_services.EffectiveQuality);
            _board.BindViewport(_camera.GetComponent<Kruty1918.GameplayViewport.GameplayViewport>());
            _atmosphere.RegisterDecor(_world.transform);
            _atmosphere.BindRainWorld(_world.transform);
            if (entry.cared)
            {
                foreach (var landmark in _world.GetComponentsInChildren<CampLandmarkVisual>()) landmark.ApplyCare();
                foreach (var detail in _world.GetComponentsInChildren<EnvironmentalStoryVisual>()) detail.ApplyCare();
            }
            _atmosphere.Soundscape.AccentSuppressed=()=>IsSwitching;
            if (_level.noise.Length>0) _atmosphere.SetFire(BoardMath.CellCenterWorld(_level,new Cell(_level.noise[0][0],_level.noise[0][1])),true);
        }
        void Bind()
        {
            if(_screens.Current!="Album" && _screens.Current!="AlbumQuiet" && _screens.Current!="JourneyPreview")return;
            _viewport=_screens.Overlay.Element(_screens.Current=="JourneyPreview" ? "journey-viewport" : "album-viewport");
            if(_viewport!=null){var drag=_viewport.GetComponent<AlbumRotateInput>()??_viewport.gameObject.AddComponent<AlbumRotateInput>();drag.Owner=this;}
            var entries=_services.Save.Album.entries??Array.Empty<QuietCamp.Application.AlbumSaveData.Entry>();
            for(int i=0;i<entries.Length;i++)
            {
                var rect=_screens.Overlay.Element("album-thumb-"+i);
                if(rect==null)continue;
                var thumb=rect.GetComponentInChildren<CampThumbnailGraphic>();
                if(thumb==null)thumb=QcUi.Stretch(rect,"CampMiniature").gameObject.AddComponent<CampThumbnailGraphic>();
                thumb.Configure(CampContent.AlbumLevel(entries[i]),entries[i].placements);thumb.raycastTarget=false;
            }
            _refit=true;
        }
        void ReplayMemory()
        {
            if (_world == null || IsSwitching) return;
            var memory = GetComponent<CampMemoryPresenter>() ?? gameObject.AddComponent<CampMemoryPresenter>();
            memory.Play(_world.transform, () => _services.ReducedMotion, null);
        }
        public void Rotate(float pixels)
        {
            if(IsSwitching)return;
            _yaw+=pixels*180/Mathf.Max(1,Screen.width);_refit=true;
        }
        void LateUpdate()
        {
            if(_world==null||_level==null)return;
            var size=new Vector2Int(Screen.width,Screen.height);
            if(_refit||size!=_size){_refit=false;_size=size;CameraFitter.Configure(_camera,new Vector3(52,225+_yaw+_orbitOffset,0));CameraFitter.Fit(_camera,_level,_viewport);_camera.orthographicSize*=_zoom;}
        }
        void Release()
        {
            _board?.ClearAll();_board=null;
            if(_atmosphere!=null)_atmosphere.RestoreEnvironmentOnDestroy=false;
            if(_world!=null){var post=_world.GetComponent<PhasePostFx>();if(post!=null)post.RestoreCameraOnDestroy=false;}
            if(_world!=null){_world.SetActive(false);Destroy(_world);}_world=null;_atmosphere=null;
        }
        void OnDestroy(){StopSwap();if(_screens!=null){_screens.ScreenChanged-=OnScreen;_screens.AlbumSelected-=Select;_screens.OverlayMounted-=Bind;_screens.MemoryReplay-=ReplayMemory;}Release();if(_veil!=null)Destroy(_veil.gameObject);}
    }
    public sealed class AlbumRotateInput : MonoBehaviour, IDragHandler
    {
        public AlbumDiorama Owner;
        public void OnDrag(PointerEventData e)=>Owner?.Rotate(e.delta.x);
    }
}
