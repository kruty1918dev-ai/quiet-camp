using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Kruty1918.ScenePlacement;

namespace Kruty1918.ModelWeathering
{
    [Serializable] public sealed class MaterialWeathering
    {
        public string kind="wood";public float warp=.025f,sag=.012f,chip=.01f,fade=.16f,soil=.2f;
        public Tint aged=new Tint(.37f,.34f,.27f),soilTint=new Tint(.26f,.29f,.19f);
    }
    [Serializable] public sealed class WeatheringProfile
    {
        public int seed;public float intensity=.4f,moisture=.5f,damageProbability=.12f,rootLock=.15f;
        public MaterialWeathering fallback=new MaterialWeathering();public MaterialWeathering[] materials=Array.Empty<MaterialWeathering>();
        public string[] removableRoles=new[]{"board","roof-panel","rail"};public int maximumRemovedParts=1;public bool emitGrowth=true;
    }
    [Serializable] public sealed class GrowthRequest {public string owner,part;public V3 anchor;public float weight;}
    public sealed class WeatheringResult
    {
        public MeshData mesh;public float[] ageMask,soilMask;public readonly List<string> operations=new List<string>();
        public readonly List<Diagnostic> diagnostics=new List<Diagnostic>();public readonly List<GrowthRequest> growth=new List<GrowthRequest>();
    }
    public static class Weathering
    {
        public static WeatheringResult Apply(string id,MeshData source,WeatheringProfile profile,CancellationToken cancellation=default)
        {
            source.Validate();if(profile==null||!Numbers.Finite(profile.intensity)||profile.intensity<0||profile.intensity>1||profile.rootLock<0||profile.rootLock>1||profile.damageProbability<0||profile.damageProbability>1||profile.maximumRemovedParts<0)throw new ArgumentException("Invalid weathering profile");
            var output=source.Copy();var result=new WeatheringResult{mesh=output,ageMask=new float[source.positions.Length],soilMask=new float[source.positions.Length]};
            if(profile.intensity==0)return result;
            var bounds=source.Bounds;float height=Math.Max(.001f,bounds.half.y*2);float bottom=bounds.center.y-bounds.half.y;
            var locked=new HashSet<int>();var vertexPart=new MeshPart[source.positions.Length];var removed=new HashSet<int>();
            var protectedTriangles=new HashSet<int>(source.parts.Where(p=>p.preserve).SelectMany(p=>p.triangles));
            foreach(var part in source.parts.OrderBy(p=>p.id,StringComparer.Ordinal))
            {
                foreach(int t in part.triangles)for(int k=0;k<3;k++){int v=source.indices[t*3+k];vertexPart[v]=part;if(part.preserve)locked.Add(v);}
                if(!part.preserve&&!part.triangles.Any(protectedTriangles.Contains)&&result.operations.Count<profile.maximumRemovedParts&&profile.removableRoles.Contains(part.role)&&Numbers.Unit(id+"/"+part.id,profile.seed)<profile.damageProbability*profile.intensity)
                {foreach(int t in part.triangles)removed.Add(t);result.operations.Add("removed-part:"+part.id);}
            }
            if(removed.Count==source.TriangleCount){removed.Clear();result.operations.Clear();result.diagnostics.Add(new Diagnostic("all-parts-protected",id,"Removal rejected because no geometry would remain",false));}
            if(source.parts.Length==0)result.diagnostics.Add(new Diagnostic("no-structural-parts",id,"Only bounded surface wear/deformation; removal requires semantic parts",false));
            string PositionKey(V3 p)=>BitConverter.ToInt32(BitConverter.GetBytes(p.x),0)+":"+BitConverter.ToInt32(BitConverter.GetBytes(p.y),0)+":"+BitConverter.ToInt32(BitConverter.GetBytes(p.z),0);
            var lockedPositions=new HashSet<string>(locked.Select(v=>PositionKey(source.positions[v])),StringComparer.Ordinal);
            var jointParts=new Dictionary<string,HashSet<string>>(StringComparer.Ordinal);
            foreach(var part in source.parts)foreach(int t in part.triangles)for(int k=0;k<3;k++){string key=PositionKey(source.positions[source.indices[t*3+k]]);if(!jointParts.TryGetValue(key,out var parts))jointParts.Add(key,parts=new HashSet<string>(StringComparer.Ordinal));parts.Add(part.id);}
            foreach(var pair in jointParts.Where(p=>p.Value.Count>1))lockedPositions.Add(pair.Key);
            if(output.colors.Length==0)output.colors=Enumerable.Repeat(Tint.White,output.positions.Length).ToArray();
            for(int i=0;i<source.positions.Length;i++)
            {
                cancellation.ThrowIfCancellationRequested();var p=source.positions[i];var part=vertexPart[i];var material=profile.materials.FirstOrDefault(m=>m.kind==part?.materialKind)??profile.fallback;
                float y=Numbers.Clamp((p.y-bottom)/height,0,1),bend=Numbers.Smooth((y-profile.rootLock)/Math.Max(.001f,1-profile.rootLock));
                float noise=(float)(Math.Sin(p.x*3.7+p.z*5.9+Numbers.Unit(id,profile.seed)*6.28)*.5+.5);
                float age=profile.intensity*(.65f+noise*.35f),soil=profile.intensity*profile.moisture*(1-Numbers.Smooth(y/.28f));
                result.ageMask[i]=age;result.soilMask[i]=soil;
                if(!lockedPositions.Contains(PositionKey(p)))
                {
                    float side=(float)Math.Sin(p.z*2.1+p.x*1.3+Numbers.Unit(id,profile.seed,2)*6.28);
                    float chip=material.chip*age*bend*Math.Max(0,noise-.72f);
                    // Geometric displacement is position-based across UV/normal seams.
                    output.positions[i]=p+new V3(material.warp*age*bend*side,-material.sag*age*bend*(.35f+noise*.65f),material.warp*age*bend*(float)Math.Sin(p.x*2.4))-(p-bounds.center).Normalized*chip;
                }
                var color=Tint.Lerp(source.colors.Length>0?source.colors[i]:Tint.White,material.aged,material.fade*age);
                output.colors[i]=Tint.Lerp(color,material.soilTint,material.soil*soil);
            }
            if(removed.Count>0)
            {
                var kept=Enumerable.Range(0,source.TriangleCount).Where(t=>!removed.Contains(t)).ToArray();output.indices=kept.SelectMany(t=>source.indices.Skip(t*3).Take(3)).ToArray();
                output.materials=source.materials.Length>0?kept.Select(t=>source.materials[t]).ToArray():Array.Empty<int>();
                output.parts=source.parts.Select(p=>new MeshPart{id=p.id,role=p.role,materialKind=p.materialKind,preserve=p.preserve,triangles=kept.Select((old,index)=>new{old,index}).Where(x=>p.triangles.Contains(x.old)).Select(x=>x.index).ToArray()}).ToArray();
            }
            output.RecalculateNormals();output.Validate();result.operations.Add("surface-wear");result.operations.Add("bounded-deformation");
            if(profile.emitGrowth)
            {
                foreach(var part in source.parts.Where(p=>!p.preserve))
                {
                    var points=part.triangles.SelectMany(t=>source.indices.Skip(t*3).Take(3)).Distinct().Select(v=>source.positions[v]).ToArray();if(points.Length==0)continue;
                    var b=Box.FromPoints(points);result.growth.Add(new GrowthRequest{owner=id,part=part.id,anchor=new V3(b.center.x,b.center.y-b.half.y,b.center.z),weight=profile.intensity*profile.moisture});
                }
                if(result.growth.Count==0)result.growth.Add(new GrowthRequest{owner=id,part="base",anchor=new V3(bounds.center.x,bottom,bounds.center.z),weight=profile.intensity*profile.moisture});
            }
            return result;
        }
    }
}
