using System;
using System.Collections.Generic;
using Kruty1918.Audio;
using QuietCamp.Domain;
using QuietCamp.Infrastructure;
using UnityEngine;

namespace QuietCamp.Presentation.World
{
    /// <summary>Audio belonging to one visible camp. Its atmosphere advances it;
    /// pooled sources stay under AudioPool, never under a disposable diorama.
    /// Rain contacts remain visual: audio uses two bounded surface layers.</summary>
    public sealed class CampSoundscape : MonoBehaviour
    {
        struct Accent { public AudioHandle handle; public Transform follow; }
        readonly List<Accent> _accents = new List<Accent>(8);
        readonly List<TentCloth> _tents = new List<TentCloth>();
        readonly List<Vector3> _canopies = new List<Vector3>();
        AudioService _audio;
        CampAtmosphere _atmosphere;
        AtmosphereCatalog.Profile _profile;
        Transform _world;
        System.Random _random;
        AudioHandle _fire, _crickets, _rain, _canvas;
        AudioHandle _biomeBed, _water;
        CampBiomeProfile _biome;
        LevelData _level;
        Vector3? _waterPosition;
        float _biomeWeight, _waterWeight, _surfaceGap;
        Vector3 _firePosition;
        bool _hasFire, _hadFire, _quenched, _wasWet;
        float _fireWeight, _cricketWeight, _rainWeight, _canvasWeight, _windWeight;
        float _entryCalm = 12, _accentGap, _gustGap, _dragGap, _birdClock, _owlClock;
        float _tentRefresh, _dripWindow, _dripClock, _visibility = 1;
        int _lastCanopy = -1;
        public Func<bool> AccentSuppressed { get; set; }
        public AudioHandle FireVoice => _fire;
        public AudioHandle RainVoice => _rain;
        public AudioHandle CanvasRainVoice => _canvas;
        public int OwnedVoiceCount => (_fire.IsValid ? 1 : 0) + (_crickets.IsValid ? 1 : 0)
            + (_rain.IsValid ? 1 : 0) + (_canvas.IsValid ? 1 : 0)
            + (_biomeBed.IsValid ? 1 : 0) + (_water.IsValid ? 1 : 0) + _accents.Count;
        public void SetVisibility(float weight) => _visibility = Mathf.Clamp01(weight);

        public void Configure(CampAtmosphere atmosphere, LevelData level,
            AtmosphereCatalog.Profile profile, Transform decor, AudioService audio)
        {
            _audio = audio; _atmosphere = atmosphere; _random = new System.Random(level.decorSeed ^ 191819);
            _level = level; _biome = new CampBiomeProfile(level);
            var shore = EnvironmentCompositionData.For(level).shore;
            _waterPosition = shore == null ? (Vector3?)null : ShorelineGeometry.Point(shore,0,.5f);
            if (decor != null)
                foreach (Transform child in decor)
                    if (child.name.StartsWith("tree", StringComparison.OrdinalIgnoreCase))
                        _canopies.Add(child.position + Vector3.up * 2);
            if (_canopies.Count == 0)
            {
                _canopies.Add(new Vector3(-level.width * .5f - 1, 2, -level.height * .5f));
                _canopies.Add(new Vector3(level.width * .5f + 1, 2, level.height * .5f));
            }
            Apply(profile);
            atmosphere.GustStarted += OnGust;
        }

        public void BindWorld(Transform world) { _world = world; _tentRefresh = 0; }
        public void SetFire(Vector3 position, bool active)
        {
            _hasFire = active;
            if (active) { _firePosition = position + Vector3.up * .4f; _hadFire = true; }
        }
        public void Apply(AtmosphereCatalog.Profile profile)
        {
            _profile = profile;
            if (_random == null) return;
            _birdClock = Delay(profile.BirdMin, profile.BirdMax)/Mathf.Max(.1f,_biome.Birds);
            _owlClock = Delay(profile.OwlMin, profile.OwlMax);
        }
        float Delay(float min, float max) => max <= 0 ? float.PositiveInfinity
            : Mathf.Lerp(min, Mathf.Max(min, max), (float)_random.NextDouble());
        bool Quiet => _entryCalm > 0 || _accentGap > 0 || AudioListener.pause
            || (AccentSuppressed?.Invoke() ?? false) || _visibility < .8f;

        public void Advance(float seconds)
        {
            if (_audio == null || _profile == null || !isActiveAndEnabled) return;
            float dt = Mathf.Max(0, seconds);
            _entryCalm -= dt; _accentGap -= dt; _gustGap -= dt; _dragGap -= dt; _tentRefresh -= dt; _surfaceGap -= dt;
            for (int i = _accents.Count - 1; i >= 0; i--)
            {
                var accent = _accents[i];
                if (!accent.handle.IsValid) { _accents.RemoveAt(i); continue; }
                if (accent.follow != null) accent.handle.Source.transform.position = accent.follow.position;
                _audio.SetPlaybackScale(accent.handle, _visibility);
            }
            if (_tentRefresh <= 0)
            {
                _tentRefresh = 1;
                _tents.Clear();
                if (_world != null) _world.GetComponentsInChildren(false, _tents);
            }
            float wet = _atmosphere.Weather?.RainAmount ?? 0;
            float burn = _atmosphere.RainShelter?.FireStrength ?? 1;
            bool extinguished = _atmosphere.RainShelter?.Extinguished ?? false;
            if (extinguished && !_quenched)
            {
                _quenched = true;
                // A physical consequence stays audible even when accents rest.
                if (_hadFire) PlayAccent("sfx.fire.quench", _firePosition, .65f);
            }
            TickLoop(ref _fire, ref _fireWeight, "ambience.fire", _hasFire && !extinguished ? burn : 0, 1.4f, dt, _firePosition);
            TickLoop(ref _crickets, ref _cricketWeight, "ambience.crickets", _profile.Crickets * _biome.Crickets * (1 - wet * .8f), 4, dt);
            TickLoop(ref _biomeBed, ref _biomeWeight, _biome.BedKey,
                _biome.BedWeight*(.7f+.3f*_atmosphere.Wind.Strength)*(1-wet*.35f),4,dt);
            TickLoop(ref _water, ref _waterWeight, "ambience.biome.water", _biome.WaterWeight,4,dt,_waterPosition);
            TickLoop(ref _rain, ref _rainWeight, "ambience.rain", wet * .8f, 2, dt);
            Vector3 roof = _tents.Count > 0 && _tents[0] != null ? _tents[0].transform.position + Vector3.up * .6f : Vector3.zero;
            TickLoop(ref _canvas, ref _canvasWeight, "ambience.rain.canvas", _tents.Count > 0 ? wet * .65f : 0, 2, dt, roof);
            _windWeight = Mathf.MoveTowards(_windWeight, _profile.WindAudio, dt / 3);
            var services = QuietCampBootstrap.ServicesRef;
            if (services?.Audio == _audio)
                _audio.SetPlaybackScale(services.AmbientWindHandle,
                    _windWeight * (.78f + .22f * _atmosphere.Wind.Strength) * _visibility);

            // No showers of per-particle sounds. Only a few residual canopy drips.
            if (wet > .15f) _wasWet = true;
            if (_wasWet && wet < .08f) { _wasWet = false; _dripWindow = 12; _dripClock = 2; }
            _dripWindow -= dt; _dripClock -= dt;
            if (_dripWindow > 0 && _dripClock <= 0)
            {
                _dripClock = Delay(3, 5);
                if (!Quiet) PlayAccent("sfx.rain.drip", NextCanopy() - Vector3.up * 1.6f, .45f);
            }
            _birdClock -= dt; _owlClock -= dt;
            if (_birdClock <= 0)
            {
                _birdClock = Delay(_profile.BirdMin, _profile.BirdMax)/Mathf.Max(.1f,_biome.Birds);
                if (!Quiet && wet < .12f) PlayAccent("ambience.bird", NextCanopy(), (_profile.Id == "evening" ? .5f : .75f)*_biome.Birds);
            }
            if (_owlClock <= 0)
            {
                _owlClock = Delay(_profile.OwlMin, _profile.OwlMax);
                if (!Quiet && wet < .15f) PlayAccent("ambience.owl", NextCanopy(), .7f);
            }
        }

        void TickLoop(ref AudioHandle handle, ref float current, string key, float target,
            float fade, float dt, Vector3? position = null)
        {
            // A small hysteresis prevents repeated starts around the dry threshold.
            target = Mathf.Clamp01(target);
            if (!handle.IsValid && target > .025f)
                handle = _audio.Play(key, new AudioPlayOptions(position: position, initialPlaybackScale: 0));
            current = Mathf.MoveTowards(current, target < .01f ? 0 : target, dt / fade);
            if (!handle.IsValid) return;
            if (position.HasValue) handle.Source.transform.position = position.Value;
            _audio.SetPlaybackScale(handle, current * _visibility);
            if (target < .01f && current <= 0) { handle.Stop(); handle = default; }
        }

        public void BirdPassing(Transform bird)
        {
            if (_audio == null || bird == null || Quiet || (_atmosphere.Weather?.RainAmount ?? 0) > .12f) return;
            PlayAccent("ambience.bird", bird.position, .55f*_biome.Birds, bird);
            _birdClock = Mathf.Max(_birdClock, 8);
        }
        public void TentDragged(Vector3 position, float speed)
        {
            if (_dragGap > 0 || speed < .55f || _visibility < .8f || (AccentSuppressed?.Invoke() ?? false)) return;
            _dragGap = .75f;
            PlayAccent("sfx.tent.drag", position, Mathf.Lerp(.2f,.6f,Mathf.Clamp01(speed/6)));
        }
        public void GroundContact(Vector3 position)
        {
            if (_audio == null || _surfaceGap > 0 || AudioListener.pause || _visibility < .8f) return;
            _surfaceGap=.2f;
            var surface=new CampBiomeProfile(_level,_atmosphere.Weather?.RainAmount??0);
            PlayAccent(surface.SurfaceKey,position,.55f);
        }
        void OnGust()
        {
            if (_audio == null || Quiet || _gustGap > 0) return;
            _gustGap = 18;
            var wind = _atmosphere.Wind.DirectionXZ;
            PlayAccent("ambience.gust", new Vector3(-wind.x * 5, 1.2f, -wind.y * 5), .6f);
            if (_tents.Count > 0 && _tents[0] != null)
                PlayAccent("sfx.tent.gust", _tents[0].transform.position + Vector3.up * .5f, .55f);
            else PlayAccent("sfx.rustle", NextCanopy(), .45f);
        }
        Vector3 NextCanopy()
        {
            int index = _canopies.Count == 1 ? 0 : _random.Next(_canopies.Count - 1);
            if (_canopies.Count > 1 && index >= _lastCanopy) index++;
            index = Mathf.Clamp(index, 0, _canopies.Count - 1);
            _lastCanopy = index; return _canopies[index];
        }
        void PlayAccent(string key, Vector3 position, float volume, Transform follow = null)
        {
            if (_audio == null || _accents.Count >= 6 || AudioListener.pause) return;
            var handle = _audio.Play(key, new AudioPlayOptions(position: position, volumeScale: volume,
                pitchOffset: ((float)_random.NextDouble() * 2 - 1) * .015f, initialPlaybackScale: _visibility));
            if (!handle.IsValid) return;
            _accents.Add(new Accent { handle = handle, follow = follow }); _accentGap = 3;
        }
        void StopOwned()
        {
            _fire.Stop(); _crickets.Stop(); _rain.Stop(); _canvas.Stop();
            _biomeBed.Stop(); _water.Stop(); _biomeBed = _water = default;
            _biomeWeight = _waterWeight = 0;
            _fire = _crickets = _rain = _canvas = default;
            _fireWeight = _cricketWeight = _rainWeight = _canvasWeight = 0;
            foreach (var accent in _accents) { _audio?.SetPlaybackScale(accent.handle, 0); accent.handle.Stop(); }
            _accents.Clear();
        }
        void OnDisable() => StopOwned();
        void OnDestroy() { StopOwned(); if (_atmosphere != null) _atmosphere.GustStarted -= OnGust; }
    }
}
