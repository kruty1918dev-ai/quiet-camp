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
            const int side=12,patches=side*side;
            for(int i=0;i<patches*2;i++)
            {
                bool near=i>=patches;int k=i%patches;
                float u=(k%side+.28f+(float)random.NextDouble()*.44f)/side,v=(k/side+.28f+(float)random.NextDouble()*.44f)/side;
                float x=near?(u-.5f)*(width+10):art.landscape.originX+u*art.landscape.size;
                float z=near?(v-.5f)*(height+10):art.landscape.originZ+v*art.landscape.size;
                if(!Dry(x,z))continue;
                float span=near?(Math.Max(width,height)+10)/(float)side*1.12f:2.8f;
                float h=Math.Abs(x)<width*.5f&&Math.Abs(z)<height*.5f?.075f:.13f+(float)random.NextDouble()*.045f;
                if(art.atmosphere?.season=="autumn")h*=.55f;
                float yaw=(float)random.NextDouble()*Mathf.PI;
                var tint=Color.Lerp(CampArtDefinition.Color(art.groundLow),CampArtDefinition.Color(art.groundHigh),.35f+(float)random.NextDouble()*.3f);
                if(art.atmosphere?.season=="autumn")tint=Color.Lerp(tint,new Color(.57f,.42f,.19f),.55f);
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
                        points.AddRange(new[]{px,art.GroundHeight(px,pz)+.006f+(top?h:0),pz});
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
