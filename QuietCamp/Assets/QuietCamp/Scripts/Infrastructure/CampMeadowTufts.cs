using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace QuietCamp.Infrastructure
{
    /// <summary>Compact three-dimensional tufts made of individual tapered leaves.
    /// No textured ribbons, crossed cards, alpha clipping or transparent ground layers.</summary>
    public static class CampMeadowTufts
    {
        public static Color SeasonTint(string season)=>season=="autumn"?new Color(.96f,.94f,.83f):new Color(.72f,1f,.78f);
        public static LandscapeMesh Build(CampArtDefinition art,int width,int height,int seed)
        {
            var points=new List<float>();var normals=new List<float>();var colors=new List<float>();var uv=new List<float>();var triangles=new List<int>();
            var result=new LandscapeMesh{name="Compact meadow grass tufts",vertices=Array.Empty<float>(),normals=Array.Empty<float>(),
                colors=Array.Empty<float>(),uv=Array.Empty<float>(),triangles=Array.Empty<int>()};
            if(art.atmosphere?.season=="winter")return result;
            var water=new RiverSurfaceField(art.landscape);var holes=WorldWellShafts.Find(art);var random=new System.Random(seed^6289);
            bool Dry(float x,float z)=>x>=art.landscape.originX&&x<=art.landscape.originX+art.landscape.size
                &&z>=art.landscape.originZ&&z<=art.landscape.originZ+art.landscape.size
                &&!holes.Any(h=>h.Contains(x,z,-.04f))&&(!water.TrySample(x,z,out var wet)||!wet.Wet);
            // Roots use the displayed terrain triangles, which may have been
            // resampled for the budget. The physical height field stays intact.
            var ground=art.landscape.meshes.Where(m=>!m.water&&!m.decoration).ToArray();
            float Ground(float x,float z)
            {
                foreach(var mesh in ground)for(int i=0;i<mesh.triangles.Length;i+=3)
                {
                    int a=mesh.triangles[i]*3,b=mesh.triangles[i+1]*3,c=mesh.triangles[i+2]*3;var v=mesh.vertices;
                    if(x<Math.Min(v[a],Math.Min(v[b],v[c]))-.00001f||x>Math.Max(v[a],Math.Max(v[b],v[c]))+.00001f
                        ||z<Math.Min(v[a+2],Math.Min(v[b+2],v[c+2]))-.00001f||z>Math.Max(v[a+2],Math.Max(v[b+2],v[c+2]))+.00001f)continue;
                    float determinant=(v[b+2]-v[c+2])*(v[a]-v[c])+(v[c]-v[b])*(v[a+2]-v[c+2]);
                    if(Math.Abs(determinant)<1e-10f)continue;
                    float u=((v[b+2]-v[c+2])*(x-v[c])+(v[c]-v[b])*(z-v[c+2]))/determinant;
                    float t=((v[c+2]-v[a+2])*(x-v[c])+(v[a]-v[c])*(z-v[c+2]))/determinant;
                    if(u>=-.00001f&&t>=-.00001f&&u+t<=1.00001f)return u*v[a+1]+t*v[b+1]+(1-u-t)*v[c+1];
                }
                return art.GroundHeight(x,z);
            }
            const int farSide=16,nearSide=23,farPatches=farSide*farSide;
            for(int i=0;i<farPatches+nearSide*nearSide;i++)
            {
                bool near=i>=farPatches;int k=near?i-farPatches:i,side=near?nearSide:farSide;
                float u=(k%side+.08f+(float)random.NextDouble()*.84f)/side,v=(k/side+.08f+(float)random.NextDouble()*.84f)/side;
                float x=near?(u-.5f)*(width+14):art.landscape.originX+u*art.landscape.size;
                float z=near?(v-.5f)*(height+14):art.landscape.originZ+v*art.landscape.size;
                if(!Dry(x,z))continue;
                bool onField=Math.Abs(x)<width*.5f+.3f&&Math.Abs(z)<height*.5f+.3f;
                float tuftHeight=onField?.09f:.19f+(float)random.NextDouble()*.13f;
                if(art.atmosphere?.season=="autumn")tuftHeight*=.72f;
                float yaw=(float)random.NextDouble()*Mathf.PI*2;
                var tint=Color.Lerp(CampArtDefinition.Color(art.groundLow),CampArtDefinition.Color(art.groundHigh),.22f+(float)random.NextDouble()*.42f)*SeasonTint(art.atmosphere?.season);
                for(int blade=0;blade<4;blade++)
                {
                    float angle=yaw+blade*Mathf.PI*.5f+(float)random.NextDouble()*.6f;
                    float dx=Mathf.Cos(angle),dz=Mathf.Sin(angle),spread=onField?.025f:.055f;
                    float rootX=x+dx*spread,rootZ=z+dz*spread;
                    float halfWidth=onField?.021f:.032f+(float)random.NextDouble()*.018f;
                    float lean=onField?.025f:.045f+(float)random.NextDouble()*.04f;
                    float tipX=rootX-dz*lean,tipZ=rootZ+dx*lean;
                    float leftX=rootX-dx*halfWidth,leftZ=rootZ-dz*halfWidth,rightX=rootX+dx*halfWidth,rightZ=rootZ+dz*halfWidth;
                    if(!Dry(leftX,leftZ)||!Dry(rightX,rightZ)||!Dry(tipX,tipZ))continue;
                    float y=Ground(rootX,rootZ)+.003f,h=tuftHeight*(.72f+(float)random.NextDouble()*.28f);
                    float leftY=Ground(leftX,leftZ)+.003f,rightY=Ground(rightX,rightZ)+.003f;
                    if(y+h-Math.Max(leftY,rightY)<.045f||y+h<Ground(tipX,tipZ)+.045f)continue;
                    points.AddRange(new[]{leftX,leftY,leftZ,rightX,rightY,rightZ,tipX,y+h,tipZ});
                    for(int corner=0;corner<3;corner++)
                    {
                        normals.AddRange(new[]{0f,1f,0f});var shade=tint*(corner==2?1.06f:.86f);
                        colors.AddRange(new[]{shade.r,shade.g,shade.b,1f});uv.AddRange(new[]{corner==0?0:corner==1?1:.5f,corner==2?1f:0});
                        triangles.Add(triangles.Count);
                    }
                }
            }
            if(triangles.Count/3>CampGeometryBudget.ProceduralGrassTriangles)throw new InvalidOperationException("Meadow tuft budget exceeded.");
            result.vertices=points.ToArray();result.normals=normals.ToArray();result.colors=colors.ToArray();result.uv=uv.ToArray();result.triangles=triangles.ToArray();return result;
        }
    }
}
