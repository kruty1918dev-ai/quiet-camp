using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace QuietCamp.Infrastructure
{
    /// <summary>Shared deterministic presentation bake. Never changes a puzzle or its stored composition.</summary>
    public static class RiverLandscapeGeometry
    {
        public static float Patch(float x,float z,int seed)
            => Mathf.PerlinNoise(x+seed%997*.137f,z+seed%541*.193f);
        public static FrozenLandscape Resolve(CampArtDefinition art,RiverEnvironmentProfile profile)
        {
            var source=art.landscape;
            if(source==null||source.riverEnvironmentVersion==profile.version)return source;
            var original=new RiverSurfaceField(source);
            if(original.reaches.Length==0)return source;
            var water=new List<LandscapeMesh>();
            foreach(var reach in original.reaches)water.Add(BuildWater(art,profile,reach));
            var field=new RiverSurfaceField(new FrozenLandscape{meshes=water.ToArray()});
            var result=new FrozenLandscape{resolution=source.resolution,originX=source.originX,originZ=source.originZ,size=source.size,
                heights=(float[])source.heights.Clone(),riverEnvironmentVersion=profile.version};
            float Height(float x,float z)
            {
                float old=source.SampleHeight(x,z);
                if(Mathf.Abs(x)<art.width*.5f+.7f&&Mathf.Abs(z)<art.height*.5f+.7f)return old;
                if(!field.TrySample(x,z,out var sample)||sample.signedDistance>profile.bankWidth)return old;
                float n=Patch(x*.65f,z*.65f,art.seed),edge=sample.signedDistance;
                if(edge<0)
                {
                    float fraction=Mathf.Clamp01(-edge/Mathf.Max(.1f,sample.halfWidth*.85f));
                    float depth=Mathf.Lerp(profile.shallowDepth,profile.channelDepth,Mathf.SmoothStep(0,1,fraction));
                    return sample.centre.y-depth+(n-.5f)*profile.bedDetail;
                }
                float blend=Mathf.SmoothStep(0,1,edge/profile.bankWidth);
                float bank=sample.centre.y+.025f+edge*.12f+(n-.5f)*.035f;
                return Mathf.Lerp(bank,old,blend);
            }
            int count=source.resolution;
            for(int z=0;z<count;z++)for(int x=0;x<count;x++)
            {
                float px=source.originX+x*source.size/(count-1),pz=source.originZ+z*source.size/(count-1);
                result.heights[z*count+x]=Height(px,pz);
            }
            var meshes=new List<LandscapeMesh>();
            foreach(var mesh in source.meshes.Where(m=>!m.water))
            {
                if(mesh.decoration){meshes.Add(mesh);continue;}
                var copy=new LandscapeMesh{name=mesh.name,vertices=(float[])mesh.vertices.Clone(),normals=(float[])mesh.normals.Clone(),
                    colors=(float[])mesh.colors.Clone(),uv=mesh.uv,lightmapUv=mesh.lightmapUv,flow=mesh.flow,tangents=mesh.tangents,triangles=mesh.triangles};
                for(int i=0;i<copy.vertices.Length/3;i++)
                {
                    float x=copy.vertices[i*3],z=copy.vertices[i*3+2];
                    if(!field.TrySample(x,z,out var sample)||sample.signedDistance>profile.bankWidth+.2f)continue;
                    if(Mathf.Abs(x)<art.width*.5f+.7f&&Mathf.Abs(z)<art.height*.5f+.7f)continue;
                    float h=result.SampleHeight(x,z);copy.vertices[i*3+1]=h;
                    const float epsilon=.15f;
                    var normal=new Vector3(result.SampleHeight(x-epsilon,z)-result.SampleHeight(x+epsilon,z),epsilon*2,
                        result.SampleHeight(x,z-epsilon)-result.SampleHeight(x,z+epsilon)).normalized;
                    copy.normals[i*3]=normal.x;copy.normals[i*3+1]=normal.y;copy.normals[i*3+2]=normal.z;
                    float patch=Patch(x*.38f,z*.38f,art.seed);
                    float wet=(1-Mathf.SmoothStep(0,1,sample.signedDistance/profile.bankWidth))*(.45f+patch*.45f);
                    var grass=Color.Lerp(CampArtDefinition.Color(art.groundLow),CampArtDefinition.Color(art.groundHigh),patch);
                    var soil=CampArtDefinition.Color(art.soilColor)*Mathf.Lerp(.72f,1.06f,patch);
                    var colour=Color.Lerp(grass,soil,wet);
                    copy.colors[i*4]=colour.r;copy.colors[i*4+1]=colour.g;copy.colors[i*4+2]=colour.b;
                }
                meshes.Add(copy);
            }
            var contacts=new List<Vector3>();var pebbles=BuildPebbles(art,result,field,contacts);
            if(pebbles!=null)meshes.Add(pebbles);
            var debris=BuildDriftwood(art,result,field);
            if(debris!=null)meshes.Add(debris);
            result.riverContacts=contacts.SelectMany(v=>new[]{v.x,v.y,v.z}).ToArray();
            meshes.AddRange(water);result.meshes=meshes.ToArray();result.Validate();return result;
        }
        static LandscapeMesh BuildPebbles(CampArtDefinition art,FrozenLandscape ground,RiverSurfaceField field,List<Vector3> contacts)
        {
            var positions=new List<Vector3>();var indices=new List<int>();var colours=new List<Color>();
            var random=new System.Random(art.seed^7251);
            foreach(var reach in field.reaches)for(int row=2;row<reach.Length-2;row+=random.Next(2,7))
            {
                var s=reach[row];if(Mathf.Abs(s.centre.x)>30||Mathf.Abs(s.centre.z)>30||Patch(s.distance*.31f,21,art.seed)<.55f)continue;
                int sign=random.Next(2)==0?-1:1;float radius=.1f+(float)random.NextDouble()*.2f;
                var centre=s.centre+new Vector3(s.side.x,0,s.side.y)*s.halfWidth*(.78f+(float)random.NextDouble()*.2f)*sign;
                if(Mathf.Abs(centre.x)<art.width*.5f+1&&Mathf.Abs(centre.z)<art.height*.5f+1)continue;
                float waterY=centre.y;centre.y=ground.SampleHeight(centre.x,centre.z)+radius*.5f;
                int begin=positions.Count;
                var local=new[]{new Vector3(0,.7f,0),new Vector3(-1,0,0),new Vector3(0,0,.7f),new Vector3(1,0,0),new Vector3(0,0,-.7f),new Vector3(0,-.7f,0)};
                float yaw=(float)random.NextDouble()*360;var rotation=Quaternion.Euler(0,yaw,0);
                foreach(var v in local)
                {
                    positions.Add(centre+rotation*new Vector3(v.x*radius*1.4f,v.y*radius,v.z*radius));
                    float variation=.92f+(float)random.NextDouble()*.16f;
                    colours.Add((v.y>0?new Color(.51f,.53f,.45f):new Color(.32f,.37f,.31f))*variation);
                }
                foreach(int index in new[]{0,2,1,0,3,2,0,4,3,0,1,4,5,1,2,5,2,3,5,3,4,5,4,1})indices.Add(begin+index);
                if(centre.y+radius*.7f>waterY-.02f)contacts.Add(new Vector3(centre.x,waterY,centre.z));
            }
            if(positions.Count==0)return null;
            return FlatDecoration("River pebbles — submerged contact",positions,indices,colours);
        }
        static LandscapeMesh BuildDriftwood(CampArtDefinition art,FrozenLandscape ground,RiverSurfaceField field)
        {
            var positions=new List<Vector3>();var indices=new List<int>();var colours=new List<Color>();
            var random=new System.Random(art.seed^8129);int groups=0;
            foreach(var s in field.reaches.SelectMany(r=>r.Skip(1)).OrderBy(s=>s.centre.sqrMagnitude))
            {
                if(groups==3)break;
                if(Mathf.Abs(s.centre.x)>24||Mathf.Abs(s.centre.z)>24||Patch(s.distance*.41f,39,art.seed)<.63f)continue;
                int sign=random.Next(2)==0?-1:1;
                var p=s.centre+new Vector3(s.side.x,0,s.side.y)*(s.halfWidth+.16f)*sign;
                if(Mathf.Abs(p.x)<art.width*.5f+1&&Mathf.Abs(p.z)<art.height*.5f+1)continue;
                if(ground.SampleHeight(p.x,p.z)<s.centre.y+.005f)continue;
                if(positions.Any(v=>(v-p).sqrMagnitude<4))continue;
                float length=.55f+(float)random.NextDouble()*.55f,radius=.035f+(float)random.NextDouble()*.025f;
                var direction=Quaternion.Euler(0,(float)random.NextDouble()*40-20,0)*new Vector3(-s.side.y,0,s.side.x);
                var a=p-direction*length*.5f;var b=p+direction*length*.5f;
                a.y=ground.SampleHeight(a.x,a.z)+radius*.4f;b.y=ground.SampleHeight(b.x,b.z)+radius*.4f;
                Tube(a,b,radius);var tip=p+new Vector3(s.side.x,0,s.side.y)*length*.35f;
                tip.y=ground.SampleHeight(tip.x,tip.z)+radius*.25f;
                Tube(Vector3.Lerp(a,b,.55f),tip,radius*.55f);groups++;
            }
            // The ring basis faces along the tube; flip its inward side/cap winding.
            for(int i=0;i<indices.Count;i+=3){int swap=indices[i+1];indices[i+1]=indices[i+2];indices[i+2]=swap;}
            return positions.Count==0?null:FlatDecoration("River driftwood — bank contact",positions,indices,colours);
            void Tube(Vector3 a,Vector3 b,float radius)
            {
                var axis=(b-a).normalized;var side=Vector3.Cross(axis,Vector3.up).normalized;var up=Vector3.Cross(side,axis).normalized;int first=positions.Count;
                for(int end=0;end<2;end++)for(int j=0;j<6;j++)
                {
                    float angle=j*Mathf.PI/3;positions.Add((end==0?a:b)+(side*Mathf.Cos(angle)+up*Mathf.Sin(angle))*radius*(end==0?1:.72f));
                    colours.Add(end==0?new Color(.36f,.30f,.20f):new Color(.43f,.37f,.25f));
                }
                for(int j=0;j<6;j++){int k=(j+1)%6;indices.AddRange(new[]{first+j,first+k,first+6+j,first+k,first+6+k,first+6+j});}
                for(int j=1;j<5;j++)indices.AddRange(new[]{first,first+j+1,first+j,first+6,first+6+j,first+6+j+1});
            }
        }
        static LandscapeMesh FlatDecoration(string name,List<Vector3> positions,List<int> indices,List<Color> colours)
        {
            var normals=new List<Vector3>();
            for(int i=0;i<indices.Count;i+=3)
            {
                var normal=Vector3.Cross(positions[indices[i+1]]-positions[indices[i]],positions[indices[i+2]]-positions[indices[i]]).normalized;
                normals.Add(normal);normals.Add(normal);normals.Add(normal);
            }
            return new LandscapeMesh{name=name,decoration=true,vertices=indices.SelectMany(i=>new[]{positions[i].x,positions[i].y,positions[i].z}).ToArray(),
                normals=normals.SelectMany(v=>new[]{v.x,v.y,v.z}).ToArray(),uv=new float[indices.Count*2],colors=indices.SelectMany(i=>new[]{colours[i].r,colours[i].g,colours[i].b,1f}).ToArray(),triangles=Enumerable.Range(0,indices.Count).ToArray()};
        }
        static LandscapeMesh BuildWater(CampArtDefinition art,RiverEnvironmentProfile p,RiverSurfaceField.Station[] reach)
        {
            const int across=12;var stations=new List<RiverSurfaceField.Station>();
            for(int i=1;i<reach.Length;i++)
            {
                var a=reach[i-1];var b=reach[i];int steps=Mathf.Max(1,Mathf.CeilToInt(Vector3.Distance(a.centre,b.centre)/.85f));
                for(int j=0;j<steps;j++)
                {
                    float t=j/(float)steps;var centre=Vector3.Lerp(a.centre,b.centre,t);
                    var side=Vector2.Lerp(a.side,b.side,t).normalized;float width=Mathf.Lerp(a.halfWidth,b.halfWidth,t);
                    float noise=Patch(centre.x*.19f,centre.z*.19f,art.seed)*2-1;
                    float half=width*(1+p.widthVariation*noise);
                    float dx=Mathf.Max(0,Mathf.Abs(centre.x)-art.width*.5f-.7f),dz=Mathf.Max(0,Mathf.Abs(centre.z)-art.height*.5f-.7f);
                    half=Mathf.Min(half,Mathf.Max(width*.7f,Mathf.Sqrt(dx*dx+dz*dz)-.25f));
                    // Keep the centreline and bridge sockets; only the banks vary.
                    stations.Add(new RiverSurfaceField.Station(centre,side,half,Mathf.Lerp(a.distance,b.distance,t)));
                }
            }
            stations.Add(reach[reach.Length-1]);int n=stations.Count*(across+1);
            var mesh=new LandscapeMesh{name="River — shared natural channel",water=true,vertices=new float[n*3],normals=new float[n*3],
                colors=new float[n*4],uv=new float[n*2],flow=new float[n*3],tangents=new float[n*4],triangles=new int[(stations.Count-1)*across*6]};
            for(int row=0;row<stations.Count;row++)
            {
                var s=stations[row];var flow=new Vector2(-s.side.y,s.side.x);
                var step=row+1<stations.Count?stations[row+1].centre-s.centre:s.centre-stations[row-1].centre;
                if(Vector2.Dot(flow,new Vector2(step.x,step.z))<0)flow=-flow;
                for(int col=0;col<=across;col++)
                {
                    int i=row*(across+1)+col;float u=col/(float)across,f=u*2-1;
                    float irregular=(Patch(s.distance*.34f,(f<0?13:37),art.seed)-.5f)*p.bankIrregularity;
                    var point=s.centre+new Vector3(s.side.x,0,s.side.y)*(f*(s.halfWidth+irregular));
                    mesh.vertices[i*3]=point.x;mesh.vertices[i*3+1]=point.y;mesh.vertices[i*3+2]=point.z;mesh.normals[i*3+1]=1;
                    mesh.uv[i*2]=u*s.halfWidth*2;mesh.uv[i*2+1]=-s.distance;
                    mesh.flow[i*3]=flow.x*.5f+.5f;mesh.flow[i*3+1]=flow.y*.5f+.5f;
                    mesh.tangents[i*4]=s.side.x;mesh.tangents[i*4+2]=s.side.y;
                    mesh.tangents[i*4+3]=Vector3.Dot(Vector3.Cross(Vector3.up,new Vector3(s.side.x,0,s.side.y)),new Vector3(-flow.x,0,-flow.y))>=0?1:-1;
                    mesh.colors[i*4+1]=Mathf.Pow(Mathf.Abs(f),5)*.62f; // transparency at the shallow lip only
                    mesh.colors[i*4+2]=Mathf.Pow(Mathf.Abs(f),3)*.8f; // calm waves at banks
                    if(row==stations.Count-1||col==across)continue;
                    int k=(row*across+col)*6;
                    var a=point;var next=stations[row+1].centre-s.centre;
                    bool up=Vector3.Cross(new Vector3(s.side.x,0,s.side.y),next).y>0;
                    var indices=up?new[]{i,i+1,i+across+1,i+1,i+across+2,i+across+1}:new[]{i,i+across+1,i+1,i+1,i+across+1,i+across+2};
                    Array.Copy(indices,0,mesh.triangles,k,6);
                }
            }
            return mesh;
        }
    }
}
