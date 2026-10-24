using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace QuietCamp.Infrastructure
{
    /// <summary>Each crossed pair displays hundreds of baked blades, using four triangles.
    /// The opaque ground detail supplies distant density; these cards only add silhouette.</summary>
    public static class CampMeadowImpostors
    {
        public static Color SeasonTint(string season)=>season=="autumn"?new Color(.96f,.94f,.83f):new Color(.72f,1f,.78f);
        public static LandscapeMesh Build(CampArtDefinition art,int width,int height,int seed)
        {
            var points=new List<float>();var normals=new List<float>();var colors=new List<float>();var uv=new List<float>();var triangles=new List<int>();
            var result=new LandscapeMesh{name="Dense meadow clump impostors",vertices=Array.Empty<float>(),normals=Array.Empty<float>(),
                colors=Array.Empty<float>(),uv=Array.Empty<float>(),triangles=Array.Empty<int>()};
            if(art.atmosphere?.season=="winter")return result;
            var water=new RiverSurfaceField(art.landscape);var holes=WorldWellShafts.Find(art);var random=new System.Random(seed^6289);
            bool Dry(float x,float z)=>x>=art.landscape.originX&&x<=art.landscape.originX+art.landscape.size
                &&z>=art.landscape.originZ&&z<=art.landscape.originZ+art.landscape.size
                &&!holes.Any(h=>h.Contains(x,z,-.03f))&&(!water.TrySample(x,z,out var wet)||!wet.Wet);
            const int farSide=16,nearSide=23,farPatches=farSide*farSide;
            for(int i=0;i<farPatches+nearSide*nearSide;i++)
            {
                bool near=i>=farPatches;int k=near?i-farPatches:i,side=near?nearSide:farSide;
                float u=(k%side+.08f+(float)random.NextDouble()*.84f)/side,v=(k/side+.08f+(float)random.NextDouble()*.84f)/side;
                float x=near?(u-.5f)*(width+14):art.landscape.originX+u*art.landscape.size;
                float z=near?(v-.5f)*(height+14):art.landscape.originZ+v*art.landscape.size;
                if(!Dry(x,z))continue;
                // Short, raised clumps instead of metre-long, almost flat ribbons.
                // The broad layer still spans all four distant terrain regions.
                float span=near?.65f+(float)random.NextDouble()*.48f:1.8f+(float)random.NextDouble()*.8f;
                bool onField=Math.Abs(x)<width*.5f+.3f&&Math.Abs(z)<height*.5f+.3f;
                float h=onField?.10f:.22f+(float)random.NextDouble()*.13f;
                if(art.atmosphere?.season=="autumn")h*=.72f;
                float yaw=(float)random.NextDouble()*Mathf.PI;
                var tint=Color.Lerp(CampArtDefinition.Color(art.groundLow),CampArtDefinition.Color(art.groundHigh),.25f+(float)random.NextDouble()*.4f)*SeasonTint(art.atmosphere?.season);
                for(int card=0;card<2;card++)
                {
                    float angle=yaw+card*Mathf.PI*.5f,dx=Mathf.Cos(angle)*span*.5f,dz=Mathf.Sin(angle)*span*.5f;
                    // Check the whole ribbon, not just its centre, against water and shafts.
                    bool dry=true;for(int check=0;check<=8;check++)if(!Dry(x-dx+dx*check*.25f,z-dz+dz*check*.25f)){dry=false;break;}
                    if(!dry)continue;
                    int first=points.Count/3;
                    for(int corner=0;corner<4;corner++)
                    {
                        bool right=corner>=2,top=corner==1||corner==2;float px=x+(right?dx:-dx),pz=z+(right?dz:-dz);
                        points.AddRange(new[]{px,art.GroundHeight(px,pz)+.003f+(top?h:0),pz});
                        normals.AddRange(new[]{0f,1f,0f});colors.AddRange(new[]{tint.r,tint.g,tint.b,1f});uv.AddRange(new[]{right?1f:0,top?1f:0});
                    }
                    triangles.AddRange(new[]{first,first+1,first+2,first,first+2,first+3});
                }
            }
            if(triangles.Count/3>CampGeometryBudget.ProceduralGrassTriangles)throw new InvalidOperationException("Meadow card budget exceeded.");
            result.vertices=points.ToArray();result.normals=normals.ToArray();result.colors=colors.ToArray();result.uv=uv.ToArray();result.triangles=triangles.ToArray();return result;
        }
    }
}
