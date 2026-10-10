using System;

namespace QuietCamp.Composition
{
    [Serializable] public sealed class ValleyChannelPoint { public float x,z,halfWidth=2.5f; }
    [Serializable] public sealed class ValleyHill { public float x,z,radiusX=15,radiusZ=25,height=6; }
    [Serializable] public sealed class ValleyBasin { public float x,z,radiusX=25,radiusZ=48,oldWaterHeight=3.8f,bedHeight=.05f; }
    [Serializable] public sealed class ValleyPool { public float x,z,radiusX=4,radiusZ=6; }
    [Serializable] public sealed class ValleyDefinition
    {
        public string id="valley-landscape";
        public float waterHeight=-.7f,damZ=171,damHeight=3.9f;
        public ValleyChannelPoint[] channel=Array.Empty<ValleyChannelPoint>();
        public ValleyHill[] hills=Array.Empty<ValleyHill>();
        public ValleyPool[] pools=Array.Empty<ValleyPool>();
        public ValleyBasin basin=new ValleyBasin();
    }
    /// <summary>One portable world sampler for terrain, water, planting and preview.</summary>
    public sealed class ValleyLandscape
    {
        public readonly ValleyDefinition Source;
        public ValleyLandscape(ValleyDefinition source)
        {
            Source=source??throw new ArgumentNullException(nameof(source));
            bool Finite(float v)=>!float.IsNaN(v)&&!float.IsInfinity(v);
            if(!Finite(source.waterHeight)||!Finite(source.damZ)||!Finite(source.damHeight)||source.basin==null)
                throw new ArgumentException("Invalid valley heights");
            float previous=float.NegativeInfinity;
            foreach(var p in source.channel)
            {if(!Finite(p.x)||!Finite(p.z)||!Finite(p.halfWidth)||p.halfWidth<=0||p.z<=previous)throw new ArgumentException("Channel stations must be finite, positive-width and ordered");previous=p.z;}
            void Ellipse(float x,float z,float rx,float rz)
            {if(!Finite(x)||!Finite(z)||!Finite(rx)||!Finite(rz)||rx<=0||rz<=0)throw new ArgumentException("Invalid valley ellipse");}
            Ellipse(source.basin.x,source.basin.z,source.basin.radiusX,source.basin.radiusZ);
            if(!Finite(source.basin.bedHeight)||!Finite(source.basin.oldWaterHeight))throw new ArgumentException("Invalid basin heights");
            foreach(var p in source.pools)Ellipse(p.x,p.z,p.radiusX,p.radiusZ);
            foreach(var p in source.hills){Ellipse(p.x,p.z,p.radiusX,p.radiusZ);if(!Finite(p.height))throw new ArgumentException("Invalid hill height");}
        }
        static float Lerp(float a,float b,float t)=>a+(b-a)*t;
        static float Smooth(float a,float b,float value)
        {float t=Math.Max(0,Math.Min(1,(value-a)/(b-a)));return t*t*(3-2*t);}
        public float ChannelX(float z)=>Channel(z,false);
        public float ChannelWidth(float z)=>Channel(z,true);
        float Channel(float z,bool width)
        {
            var p=Source.channel;if(p.Length==0)return width?2.5f:14;
            for(int i=1;i<p.Length;i++)if(z<=p[i].z)
            {float t=Smooth(p[i-1].z,p[i].z,z);return width?Lerp(p[i-1].halfWidth,p[i].halfWidth,t):Lerp(p[i-1].x,p[i].x,t);}
            return width?p[p.Length-1].halfWidth:p[p.Length-1].x;
        }
        public float WaterDistance(float x,float z)
        {
            float d=Source.channel.Length==0||z<Source.channel[0].z?100:Math.Abs(x-ChannelX(z))-ChannelWidth(z);
            foreach(var p in Source.pools)
            {float dx=(x-p.x)/p.radiusX,dz=(z-p.z)/p.radiusZ;d=Math.Min(d,((float)Math.Sqrt(dx*dx+dz*dz)-1)*Math.Min(p.radiusX,p.radiusZ));}
            return d;
        }
        public float BasinRadius(float x,float z)
        {var b=Source.basin;float dx=(x-b.x)/b.radiusX,dz=(z-b.z)/b.radiusZ;return (float)Math.Sqrt(dx*dx+dz*dz);}
        public float Height(float x,float z)
        {
            float h=1.7f+.48f*(float)Math.Sin(x*.083+z*.031)+.42f*(float)Math.Cos(z*.047)
                +5.5f*Smooth(12,48,Math.Abs(x));
            foreach(var p in Source.hills)
            {float dx=(x-p.x)/p.radiusX,dz=(z-p.z)/p.radiusZ;h+=p.height*(float)Math.Exp(-1.5*(dx*dx+dz*dz));}
            float basin=BasinRadius(x,z),bed=Source.basin.bedHeight+.12f*(float)Math.Sin(x*.23+z*.07);
            float bowl=Lerp(bed,Source.basin.oldWaterHeight,Smooth(.67f,1.03f,basin));
            h=Lerp(h,bowl,1-Smooth(1.03f,1.22f,basin));
            // The former embankment is land, with an eroded open channel through it.
            float ridge=(float)Math.Exp(-Math.Pow((z-Source.damZ)/5.2f,2));
            float shoulder=Smooth(3.5f,8,Math.Abs(x-ChannelX(Source.damZ)))*(1-Smooth(20,34,Math.Abs(x-ChannelX(Source.damZ))));
            h=Math.Max(h,Lerp(h,Source.damHeight,ridge*shoulder));
            float wet=WaterDistance(x,z);
            if(wet<8)
            {
                float floodplain=Source.waterHeight+.9f+.22f*(float)Math.Sin(z*.12+x*.19);
                h=Lerp(floodplain,h,Smooth(2.8f,8,wet));
                h=Lerp(Source.waterHeight-.7f,h,Smooth(-.35f,2.2f,wet));
            }
            return h;
        }
    }
    /// <summary>Portable grading of building foundations, shared by CLI and native publication.</summary>
    public sealed class ValleyTerrain : ITerrainSample
    {
        readonly ValleyLandscape landscape;
        readonly (float x,float z,float yaw,float halfX,float halfZ)[] foundations;
        readonly float minX,maxX,minZ,maxZ;
        public ValleyTerrain(SceneCompositionDocument doc,System.Collections.Generic.IReadOnlyDictionary<string,VisualAssetDefinition> assets,
            System.Collections.Generic.IReadOnlyDictionary<string,EnsembleTemplate> templates,float minX=-62,float maxX=62,float minZ=-70,float maxZ=260)
        {
            landscape=new ValleyLandscape(doc.landscape);this.minX=minX;this.maxX=maxX;this.minZ=minZ;this.maxZ=maxZ;
            var pads=new System.Collections.Generic.List<(float,float,float,float,float)>();
            foreach(var e in doc.ensembles)foreach(var r in templates[e.template].roles)
            {
                if(r.id!="house"&&r.id!="shelter")continue;
                float a=e.placement.yaw*(float)Math.PI/180,c=(float)Math.Cos(a),s=(float)Math.Sin(a);
                var asset=assets[r.asset];float factor=r.height/asset.height*e.scale;
                pads.Add((e.placement.x+(r.x*c+r.z*s)*e.scale,e.placement.z+(-r.x*s+r.z*c)*e.scale,
                    e.placement.yaw+r.yaw,asset.width*factor*.52f,asset.depth*factor*.52f));
            }
            foundations=pads.ToArray();
        }
        public float Height(float x,float z)
        {
            float h=landscape.Height(x,z);
            foreach(var p in foundations)
            {
                float a=p.yaw*(float)Math.PI/180,c=(float)Math.Cos(a),s=(float)Math.Sin(a),dx=x-p.x,dz=z-p.z;
                float qx=dx*c-dz*s,qz=dx*s+dz*c;
                float d=Math.Max(Math.Abs(qx)-p.halfX,Math.Abs(qz)-p.halfZ),t=Math.Max(0,Math.Min(1,d/1.2f));
                float w=1-t*t*(3-2*t);h+=(landscape.Height(p.x,p.z)-h)*w;
            }
            return h;
        }
        public bool Supported(float x,float z,float radius)=>x-radius>minX&&x+radius<maxX&&z-radius>minZ&&z+radius<maxZ;
    }
}
