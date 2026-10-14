using System;
using Newtonsoft.Json;
using UnityEngine;

namespace QuietCamp.Presentation.World
{
    [Serializable] public sealed class CampSurfaceUseSettings
    {
        public int resolution = 128;
        public float margin = 9, stride = .36f, walkingSpeed = .68f, wearPasses = 26,
            snowRecoverySeconds = 180, grassRecoverySeconds = 180, moistureSeconds = 45;
        public void Validate()
        {
            foreach(var v in new[]{margin,stride,walkingSpeed,wearPasses,snowRecoverySeconds,grassRecoverySeconds,moistureSeconds})
                if(float.IsNaN(v)||float.IsInfinity(v)||v<=0)throw new ArgumentException("Invalid camp surface settings.");
            if(resolution<32||resolution>256||margin>24||stride>1||walkingSpeed>2)throw new ArgumentException("Invalid surface budget.");
        }
        public static CampSurfaceUseSettings Load()
        {
            var asset=Resources.Load<TextAsset>("QuietCamp/camp_surface_use");
            if(asset==null)throw new InvalidOperationException("Missing camp_surface_use.json");
            var result=JsonConvert.DeserializeObject<CampSurfaceUseSettings>(asset.text);result.Validate();return result;
        }
    }

    /// <summary>Scene-local visual state. A step is reported by actual movement, never inferred from a route.
    /// Does not modify puzzle cells, placements, content hashes or player progress.</summary>
    public sealed class CampSurfaceUseField
    {
        public readonly CampSurfaceUseSettings Settings;
        public readonly Rect Bounds;
        readonly float[] _passes,_pressure,_snow,_occupied;
        public float Moisture {get;private set;}
        public int Steps {get;private set;}
        public bool Dirty {get;private set;}=true;
        public int Count=>_passes.Length;
        public CampSurfaceUseField(int width,int height,CampSurfaceUseSettings settings,float initialMoisture=0)
        {
            settings.Validate();Settings=settings;
            if(float.IsNaN(initialMoisture)||float.IsInfinity(initialMoisture))throw new ArgumentException("Invalid initial moisture.");
            Moisture=Mathf.Clamp01(initialMoisture);
            Bounds=new Rect(-width*.5f-settings.margin,-height*.5f-settings.margin,width+settings.margin*2,height+settings.margin*2);
            int count=settings.resolution*settings.resolution;
            _passes=new float[count];_pressure=new float[count];_snow=new float[count];_occupied=new float[count];
        }
        public Vector2 Point(int index)=>new Vector2(Bounds.xMin+(index%Settings.resolution+.5f)*Bounds.width/Settings.resolution,Bounds.yMin+(index/Settings.resolution+.5f)*Bounds.height/Settings.resolution);
        public int Index(Vector2 p)
        {
            if(!Bounds.Contains(p))return -1;
            int n=Settings.resolution;return Mathf.Clamp((int)((p.y-Bounds.yMin)/Bounds.height*n),0,n-1)*n+Mathf.Clamp((int)((p.x-Bounds.xMin)/Bounds.width*n),0,n-1);
        }
        public void ClearOccupancy(){Array.Clear(_occupied,0,_occupied.Length);Dirty=true;}
        public void Tent(Rect footprint,int seed)
        {
            Visit(footprint.center,footprint.size.magnitude*.5f+.25f,(i,p)=>
            {
                float edge=Mathf.Max(Mathf.Abs(p.x-footprint.center.x)-footprint.width*.5f,Mathf.Abs(p.y-footprint.center.y)-footprint.height*.5f);
                float irregular=(Mathf.PerlinNoise(p.x*3+seed*.13f,p.y*3)-.5f)*.09f;
                float pressure=1-Mathf.SmoothStep(0,1,Mathf.Clamp01((edge+irregular+.025f)/.17f));
                _occupied[i]=Mathf.Max(_occupied[i],pressure);_pressure[i]=Mathf.Max(_pressure[i],pressure*.85f);
            });Dirty=true;
        }
        public void Step(Vector2 p,float strength=1)
        {
            if(Index(p)<0||strength<=0)return;
            Steps++;
            Visit(p,.42f,(i,q)=>
            {
                float distance=Vector2.Distance(q,p),inner=1-Mathf.SmoothStep(0,1,Mathf.Clamp01(distance/.19f));
                float outer=1-Mathf.SmoothStep(0,1,Mathf.Clamp01(distance/.42f));
                _passes[i]=Mathf.Min(Settings.wearPasses*3,_passes[i]+strength*(inner*.8f+outer*.2f));
                _pressure[i]=Mathf.Min(1,_pressure[i]+strength*(inner*.28f+outer*.05f));
                _snow[i]=Mathf.Min(1,_snow[i]+strength*(inner*.24f+outer*.10f)*Mathf.Lerp(1,2,Mathf.Clamp01(_passes[i]/Settings.wearPasses)));
            });Dirty=true;
        }
        void Visit(Vector2 p,float radius,Action<int,Vector2> action)
        {
            int n=Settings.resolution;
            int x0=Mathf.Clamp(Mathf.FloorToInt((p.x-radius-Bounds.xMin)/Bounds.width*n),0,n-1),x1=Mathf.Clamp(Mathf.CeilToInt((p.x+radius-Bounds.xMin)/Bounds.width*n),0,n-1);
            int z0=Mathf.Clamp(Mathf.FloorToInt((p.y-radius-Bounds.yMin)/Bounds.height*n),0,n-1),z1=Mathf.Clamp(Mathf.CeilToInt((p.y+radius-Bounds.yMin)/Bounds.height*n),0,n-1);
            for(int z=z0;z<=z1;z++)for(int x=x0;x<=x1;x++){int i=z*n+x;action(i,Point(i));}
        }
        public void Advance(float seconds,float rain,float snowfall)
        {
            if(seconds<=0)return;
            Moisture=Mathf.MoveTowards(Moisture,Mathf.Clamp01(rain),seconds/Settings.moistureSeconds);
            for(int i=0;i<Count;i++)
            {
                _pressure[i]=Mathf.Max(_occupied[i],Mathf.MoveTowards(_pressure[i],0,seconds/Settings.grassRecoverySeconds));
                _snow[i]=Mathf.MoveTowards(_snow[i],0,seconds*Mathf.Clamp01(snowfall)/Settings.snowRecoverySeconds);
            }
            Dirty=true;
        }
        public Color Sample(int i)
        {
            float wear=Mathf.SmoothStep(0,1,Mathf.Clamp01((_passes[i]-1.5f)/Settings.wearPasses));
            return new Color(wear,Mathf.Max(Mathf.Max(_pressure[i],_occupied[i]),wear*.85f),_snow[i],wear*wear*Moisture);
        }
        public Color Sample(Vector2 p){int i=Index(p);return i<0?Color.clear:Sample(i);}
        public void CopyPixels(Color32[] pixels){if(pixels.Length!=Count)throw new ArgumentException();for(int i=0;i<Count;i++)pixels[i]=Sample(i);Dirty=false;}
    }
}
