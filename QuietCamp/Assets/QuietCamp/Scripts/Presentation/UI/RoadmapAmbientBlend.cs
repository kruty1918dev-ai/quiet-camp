using Kruty1918.Audio;
using UnityEngine;
namespace QuietCamp.Presentation.UI
{
    /// <summary>Two target biome beds plus bounded fading tails and quiet water/insect hints (six voices maximum). Existing audio catalog/pool only.</summary>
    public sealed class RoadmapAmbientBlend : MonoBehaviour
    {
        readonly string[] _keys={"ambience.biome.forest","ambience.biome.meadow","ambience.biome.autumn","ambience.biome.winter","ambience.biome.water","ambience.crickets"};
        readonly AudioHandle[] _handles=new AudioHandle[6];readonly float[] _weights=new float[6];
        RoadmapGraphic _map;AudioService _audio;
        public void Configure(RoadmapGraphic map,AudioService audio){_map=map;_audio=audio;}
        void LateUpdate()
        {
            if(_audio==null||_map?.Environment==null||_map.VisibleArea.height<=0)return;
            var sample=_map.VisualAt(_map.rectTransform.rect.yMax-_map.VisibleArea.center.y).Environment;
            for(int i=0;i<_keys.Length;i++)
            {
                float target=i==4?sample.Water*.12f:i==5?sample.Insects*.04f:
                    ((_keys[i]==sample.From.AudioBed?1-sample.Blend:0)+(_keys[i]==sample.To.AudioBed?sample.Blend:0))*.14f;
                _weights[i]=Mathf.MoveTowards(_weights[i],target,Time.unscaledDeltaTime*.08f);
                if(!_handles[i].IsValid&&target>.006f)_handles[i]=_audio.Play(_keys[i],new AudioPlayOptions(initialPlaybackScale:0));
                if(_handles[i].IsValid){_audio.SetPlaybackScale(_handles[i],_weights[i]);if(_weights[i]<=0&&target<=0){_handles[i].Stop();_handles[i]=default;}}
            }
        }
        void OnDisable(){for(int i=0;i<_handles.Length;i++){_handles[i].Stop();_handles[i]=default;_weights[i]=0;}}
    }
}
