using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace QuietCamp.Infrastructure
{
    /// <summary>Shared WorldOps/runtime presentation policy. Source documents, puzzles,
    /// landmarks and gameplay meshes are never rewritten. Counts are instance counts,
    /// including off-screen scenery, not unique-mesh or visible-camera estimates.</summary>
    public static class CampGeometryBudget
    {
        public const int SceneTriangles=50000;
        public const int AuthoredTriangles=38000;
        public const int GrassAccentTriangles=600;
        public const int ProceduralGrassTriangles=3200;
        public static int WeatherParticleCapacity(int requested)=>requested<=0?0:Math.Max(1,Math.Min(400,requested/4));

        public static bool IsGrass(CampArtDefinition.Model model)
            =>model.seasonalRole=="groundcover";
        public static CampArtDefinition DenseMeadow(CampArtDefinition source)
        {
            if(source==null||source.mobileMeadow)return source;
            if(!source.mobileGeometryBudget)throw new InvalidOperationException("Dense meadow requires the scene budget stage first.");
            var result=source.EditingCopy();result.mobileMeadow=true;return result;
        }
        static uint Stable(string id)
        {uint h=2166136261;foreach(char c in id??"")h=unchecked((h^c)*16777619);return h;}

        public static CampArtDefinition Prepare(CampArtDefinition source,IEnumerable<string> selected=null)
        {
            if(source==null)return null;
            if(source.mobileGeometryBudget)
            {
                if(CountAuthored(source)>AuthoredTriangles)throw new InvalidDataException("A budgeted presentation exceeded its reserved geometry share.");
                return source;
            }
            if(source.landscape==null)throw new InvalidDataException("Mobile geometry budget requires a baked landscape.");
            var models=source.models.ToDictionary(m=>m.id);
            var eligible=selected==null?null:new HashSet<string>(selected,StringComparer.Ordinal);
            bool Candidate(CampArtDefinition.Instance i)=>i.modelId!=null&&models.TryGetValue(i.modelId,out var m)&&IsGrass(m)
                &&i.id!=source.primaryLandmarkInstanceId&&string.IsNullOrEmpty(i.motion)
                &&!i.id.StartsWith("overgrowth/",StringComparison.Ordinal)&&(eligible==null||eligible.Contains(i.id));
            var remove=new HashSet<string>(source.instances.Where(Candidate).Select(i=>i.id),StringComparer.Ordinal);
            // Interleave spatial tiles before the stable ID tie-breaker. A small
            // accent budget must not be spent entirely in the first meadow patch.
            var tiles=source.instances.Where(Candidate).GroupBy(i=>(Mathf.FloorToInt(i.x/8),Mathf.FloorToInt(i.z/8)))
                .OrderBy(g=>Stable(g.Key.Item1+":"+g.Key.Item2)).Select(g=>g.OrderBy(i=>Stable(i.id)).ThenBy(i=>i.id,StringComparer.Ordinal).ToArray()).ToArray();
            int spent=0;
            for(int round=0;tiles.Any(t=>round<t.Length);round++)foreach(var tile in tiles)
            {
                if(round>=tile.Length)continue;
                var item=tile[round];int triangles=models[item.modelId].vertices.Length/9;
                if(spent+triangles>GrassAccentTriangles)continue;
                spent+=triangles;remove.Remove(item.id);
            }
            var result=source.EditingCopy();
            result.instances=source.instances.Where(i=>!remove.Contains(i.id)).ToArray();
            // Legacy cover is another full-model scatter on top of authored plants.
            // The bounded procedural accent layer replaces it, without another carpet.
            result.cover=Array.Empty<CampArtDefinition.CoverPoint>();
            result.landscape=SimplifyLandscape(source.landscape);
            long total=CountAuthored(result);
            if(total>AuthoredTriangles)throw new InvalidDataException("Protected scenery exceeds mobile authored budget: "+total+" > "+AuthoredTriangles+". Simplify through WorldOps; do not hide gameplay objects.");
            result.mobileGeometryBudget=true;
            // Existing natural-ground shading supplies continuous meadow colour,
            // wear, wetness and snow. No heightfield/parallax textures are needed.
            result.blurPixels=0;
            result.fieldCover=new CampArtDefinition.FieldCoverSettings{density=2,continuousMeadow=true,softnessPixels=0};
            return result;
        }
        public static long CountAuthored(CampArtDefinition art)
        {
            var models=art.models.ToDictionary(m=>m.id,m=>(long)m.vertices.Length/9);
            return art.instances.Sum(i=>i.modelId!=null&&models.TryGetValue(i.modelId,out long n)?n:0)
                +(art.landscape?.meshes.Sum(m=>(long)m.triangles.Length/3)??0);
        }
        public static LandscapeMesh SparseCover(CampArtDefinition art,int width,int height,int seed)
        {
            var points=new List<float>();var normals=new List<float>();var colors=new List<float>();var triangles=new List<int>();
            var result=new LandscapeMesh{name="Sparse meadow accents",vertices=Array.Empty<float>(),normals=Array.Empty<float>(),
                colors=Array.Empty<float>(),uv=Array.Empty<float>(),triangles=Array.Empty<int>()};
            // Snow already covers the actual ground through CampArtSurface. A
            // second tessellated snow landscape adds no useful silhouette here.
            if(art.atmosphere?.season=="winter")return result;
            var water=new RiverSurfaceField(art.landscape);var random=new System.Random(seed^4157);
            var openings=WorldWellShafts.Find(art);
            for(int i=0;i<ProceduralGrassTriangles/3;i++)
            {
                bool far=i<256;int side=far?16:12,k=far?i:i-256;
                float u=(k%side+.15f+(float)random.NextDouble()*.7f)/side;
                float v=(k/side+.15f+(float)random.NextDouble()*.7f)/side;
                float x=far?art.landscape.originX+u*art.landscape.size:(u-.5f)*(width+12);
                float z=far?art.landscape.originZ+v*art.landscape.size:(v-.5f)*(height+12);
                if(x<art.landscape.originX||x>art.landscape.originX+art.landscape.size||z<art.landscape.originZ||z>art.landscape.originZ+art.landscape.size)continue;
                if(openings.Any(o=>o.Contains(x,z))||water.TrySample(x,z,out var wet)&&wet.Wet)continue;
                float y=art.GroundHeight(x,z)+.008f;
                float h=Mathf.Abs(x)<width*.5f&&Mathf.Abs(z)<height*.5f?.065f:.10f+(float)random.NextDouble()*.07f;
                if(art.atmosphere?.season=="autumn")h*=.5f;
                var tint=Color.Lerp(CampArtDefinition.Color(art.groundLow),CampArtDefinition.Color(art.groundHigh),.35f+(float)random.NextDouble()*.4f);
                float yaw=(float)random.NextDouble()*Mathf.PI*2;
                for(int blade=0;blade<3;blade++)
                {
                    float angle=yaw+blade*Mathf.PI*2/3,dx=Mathf.Cos(angle)*.025f,dz=Mathf.Sin(angle)*.025f;
                    points.AddRange(new[]{x-dx,y,z-dz,x+dz,y+h,z-dx,x+dx,y,z+dz});
                    for(int j=0;j<3;j++){normals.AddRange(new[]{0f,1f,0f});colors.AddRange(new[]{tint.r,tint.g,tint.b,1f});triangles.Add(triangles.Count);}
                }
            }
            result.vertices=points.ToArray();result.normals=normals.ToArray();result.colors=colors.ToArray();
            result.uv=new float[points.Count/3*2];result.triangles=triangles.ToArray();return result;
        }
        public static FrozenLandscape SimplifyLandscape(FrozenLandscape source)
        {
            var result=new FrozenLandscape{resolution=source.resolution,originX=source.originX,originZ=source.originZ,size=source.size,
                heights=source.heights,riverEnvironmentVersion=source.riverEnvironmentVersion,riverContacts=source.riverContacts,
                meshes=source.meshes.Select(m=>m.water||m.decoration?m:SimplifyGrid(m,4)).ToArray()};
            result.Validate();return result;
        }
        // Only a verified complete rectangular grid can be resampled. A cut well,
        // cave, authored detail or arbitrary mesh is retained with all its faces.
        public static LandscapeMesh SimplifyGrid(LandscapeMesh source,int stride)
        {
            if(stride<2)return source;
            int count=source.vertices.Length/3,columns=1;
            float z0=source.vertices[2];
            while(columns<count&&Mathf.Abs(source.vertices[columns*3+2]-z0)<.00001f)columns++;
            if(columns<3||count%columns!=0)return source;
            int rows=count/columns;
            if(rows<3||source.triangles.Length!=(rows-1)*(columns-1)*6)return source;
            for(int z=0;z<rows;z++)for(int x=0;x<columns;x++)
            {
                int i=z*columns+x;
                if(Mathf.Abs(source.vertices[i*3]-source.vertices[x*3])>.00001f
                    ||Mathf.Abs(source.vertices[i*3+2]-source.vertices[z*columns*3+2])>.00001f)return source;
                if(x>0&&source.vertices[i*3]<=source.vertices[(i-1)*3]||z>0&&source.vertices[i*3+2]<=source.vertices[(i-columns)*3+2])return source;
            }
            // Validate topology, including winding, rather than guessing from a vertex count.
            var faces=new HashSet<(int,int,int)>();
            (int,int,int) Key(int a,int b,int c)=>a<b&&a<c?(a,b,c):b<c?(b,c,a):(c,a,b);
            for(int i=0;i<source.triangles.Length;i+=3)
                if(!faces.Add(Key(source.triangles[i],source.triangles[i+1],source.triangles[i+2])))return source;
            bool alternate=faces.Contains(Key(0,columns,1))||faces.Contains(Key(0,1,columns));
            bool up=alternate?faces.Contains(Key(0,columns,1))&&faces.Contains(Key(1,columns,columns+1)):
                faces.Contains(Key(0,columns,columns+1))&&faces.Contains(Key(0,columns+1,1));
            bool down=alternate?faces.Contains(Key(0,1,columns))&&faces.Contains(Key(1,columns+1,columns)):
                faces.Contains(Key(0,columns+1,columns))&&faces.Contains(Key(0,1,columns+1));
            if(!up&&!down)return source;
            for(int z=0;z<rows-1;z++)for(int x=0;x<columns-1;x++)
            {
                int a=z*columns+x,b=a+columns,c=b+1,d=a+1;
                var first=alternate?(up?Key(a,b,d):Key(a,d,b)):(up?Key(a,b,c):Key(a,c,b));
                var second=alternate?(up?Key(d,b,c):Key(d,c,b)):(up?Key(a,c,d):Key(a,d,c));
                if(!faces.Contains(first)||!faces.Contains(second))return source;
            }
            int[] Axis(int n)=>Enumerable.Range(0,(n-2)/stride+1).Select(i=>i*stride).Concat(new[]{n-1}).ToArray();
            var xs=Axis(columns);var zs=Axis(rows);var indices=zs.SelectMany(z=>xs.Select(x=>z*columns+x)).ToArray();
            float[] Channel(float[] input,int width)
            {
                if(input==null||input.Length==0)return input;
                var output=new float[indices.Length*width];
                for(int i=0;i<indices.Length;i++)Array.Copy(input,indices[i]*width,output,i*width,width);
                return output;
            }
            var triangles=new List<int>();
            for(int z=0;z<zs.Length-1;z++)for(int x=0;x<xs.Length-1;x++)
            {int a=z*xs.Length+x,b=a+xs.Length,c=b+1,d=a+1;
                triangles.AddRange(alternate?(up?new[]{a,b,d,d,b,c}:new[]{a,d,b,d,c,b}):(up?new[]{a,b,c,a,c,d}:new[]{a,c,b,a,d,c}));}
            var result=new LandscapeMesh{name=source.name,water=source.water,decoration=source.decoration,
                vertices=Channel(source.vertices,3),normals=Channel(source.normals,3),uv=Channel(source.uv,2),colors=Channel(source.colors,4),
                lightmapUv=Channel(source.lightmapUv,2),flow=Channel(source.flow,3),tangents=Channel(source.tangents,4),triangles=triangles.ToArray()};
            result.Validate();return result;
        }
    }
}
