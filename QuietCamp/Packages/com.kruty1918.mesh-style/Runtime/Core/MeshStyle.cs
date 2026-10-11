using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Kruty1918.ScenePlacement;

namespace Kruty1918.MeshStyle
{
    public enum NormalMode { Preserve, Smooth, Flat }
    [Serializable] public sealed class StyleProfile
    {
        public int referenceTriangles=120,minimumTriangles=12,maximumTriangles=100000,maximumCollapses=4096,maximumCandidateTests=2000000,maximumInputTriangles=100000;
        public float referenceArea=10000,areaExponent=.75f,worldScale=1,maximumWorldError=.015f,maximumFaceTurn=35,maximumUvError=.01f,maximumColorError=.06f;
        public bool preserveBoundaries=true;public NormalMode normals=NormalMode.Flat;
        public Box[] protectedVolumes=Array.Empty<Box>();
    }
    public sealed class StyleResult
    {
        public MeshData mesh;public int inputTriangles,targetTriangles,collapsedEdges,testedEdges;public float maximumDeviation;
        public readonly List<Diagnostic> diagnostics=new List<Diagnostic>();
    }
    public static class MeshHarmonizer
    {
        sealed class Triangle {public int a,b,c,material,original;public bool alive=true;}
        sealed class Vertex {public V3 p,n;public V2 uv;public Tint color;public float[] custom;public float error;public bool locked,alive=true;}
        sealed class Edge {public int a,b;public readonly List<Triangle> faces=new List<Triangle>();}
        public static StyleResult Harmonize(MeshData source,StyleProfile profile,float projectedArea,CancellationToken cancellation=default)
        {
            source.Validate();if(profile==null||profile.worldScale<=0||profile.referenceArea<=0||profile.maximumWorldError<0||profile.maximumCollapses<0||profile.minimumTriangles<1||profile.maximumTriangles<profile.minimumTriangles||!Numbers.Finite(projectedArea)||projectedArea<=0)throw new ArgumentException("Invalid style profile/view area");
            if(source.TriangleCount>profile.maximumInputTriangles||profile.maximumCandidateTests<1)throw new ArgumentException("Style input/work limit; split source or explicitly change bounded profile");
            var result=new StyleResult{inputTriangles=source.TriangleCount};
            result.targetTriangles=(int)Numbers.Clamp((float)(profile.referenceTriangles*Math.Pow(projectedArea/profile.referenceArea,profile.areaExponent)),profile.minimumTriangles,profile.maximumTriangles);
            var vertices=new List<Vertex>();var triangles=new List<Triangle>();var welded=new Dictionary<string,int>(StringComparer.Ordinal);
            var protectedIndices=new HashSet<int>();foreach(var part in source.parts.Where(p=>p.preserve))foreach(int t in part.triangles)for(int k=0;k<3;k++)protectedIndices.Add(source.indices[t*3+k]);
            string F(float f)=>BitConverter.ToInt32(BitConverter.GetBytes(f),0).ToString("X8");
            for(int t=0;t<source.TriangleCount;t++)
            {
                cancellation.ThrowIfCancellationRequested();var mapped=new int[3];int material=source.materials.Length>0?source.materials[t]:0;
                for(int k=0;k<3;k++)
                {
                    int index=source.indices[t*3+k];var p=source.positions[index];var n=source.normals.Length>0?source.normals[index]:V3.Up;var uv=source.uv.Length>0?source.uv[index]:default;var color=source.colors.Length>0?source.colors[index]:Tint.White;
                    var custom=new List<float>();foreach(var channel in source.channels)for(int c=0;c<channel.components;c++)custom.Add(channel.values[index*channel.components+c]);
                    string key=F(p.x)+F(p.y)+F(p.z)+F(uv.x)+F(uv.y)+F(color.r)+F(color.g)+F(color.b)+F(color.a)+material;
                    if(profile.normals==NormalMode.Preserve)key+=F(n.x)+F(n.y)+F(n.z);foreach(float c in custom)key+=F(c);
                    bool locked=protectedIndices.Contains(index)||profile.protectedVolumes.Any(b=>Math.Abs(p.x-b.center.x)<=b.half.x&&Math.Abs(p.y-b.center.y)<=b.half.y&&Math.Abs(p.z-b.center.z)<=b.half.z);
                    if(!welded.TryGetValue(key,out int target)){target=vertices.Count;welded.Add(key,target);vertices.Add(new Vertex{p=p,n=n,uv=uv,color=color,locked=locked,custom=custom.ToArray()});}else vertices[target].locked|=locked;
                    mapped[k]=target;
                }
                triangles.Add(new Triangle{a=mapped[0],b=mapped[1],c=mapped[2],material=material,original=t});
            }
            int live=triangles.Count;float errorLimit=profile.maximumWorldError/profile.worldScale;
            float cosine=(float)Math.Cos(profile.maximumFaceTurn*Numbers.Radians);
            while(live>result.targetTriangles&&result.collapsedEdges<profile.maximumCollapses)
            {
                cancellation.ThrowIfCancellationRequested();var edges=new Dictionary<(int,int),Edge>();var neighbors=new Dictionary<int,HashSet<int>>();var incident=new Dictionary<int,List<Triangle>>();
                void Connect(int a,int b,Triangle triangle)
                {
                    if(a>b){int tmp=a;a=b;b=tmp;}if(!edges.TryGetValue((a,b),out var edge))edges.Add((a,b),edge=new Edge{a=a,b=b});edge.faces.Add(triangle);
                    if(!neighbors.TryGetValue(a,out var an))neighbors.Add(a,an=new HashSet<int>());if(!neighbors.TryGetValue(b,out var bn))neighbors.Add(b,bn=new HashSet<int>());an.Add(b);bn.Add(a);
                }
                foreach(var t in triangles.Where(t=>t.alive)){Connect(t.a,t.b,t);Connect(t.b,t.c,t);Connect(t.c,t.a,t);foreach(int index in new[]{t.a,t.b,t.c}){if(!incident.TryGetValue(index,out var list))incident.Add(index,list=new List<Triangle>());list.Add(t);}}
                var boundary=new HashSet<int>();if(profile.preserveBoundaries)foreach(var e in edges.Values.Where(e=>e.faces.Count!=2)){boundary.Add(e.a);boundary.Add(e.b);}
                Edge best=null;float bestError=float.MaxValue;V3 bestPosition=default;
                foreach(var edge in edges.Values.OrderBy(e=>e.a).ThenBy(e=>e.b))
                {
                    cancellation.ThrowIfCancellationRequested();if(++result.testedEdges>profile.maximumCandidateTests)break;
                    var a=vertices[edge.a];var b=vertices[edge.b];if(a.locked||b.locked||boundary.Contains(edge.a)||boundary.Contains(edge.b)||edge.faces.Count>2||edge.faces.Select(f=>f.material).Distinct().Count()>1)continue;
                    var common=new HashSet<int>(neighbors[edge.a]);common.IntersectWith(neighbors[edge.b]);if(common.Count!=edge.faces.Count)continue;
                    if(Math.Abs(a.uv.x-b.uv.x)>profile.maximumUvError||Math.Abs(a.uv.y-b.uv.y)>profile.maximumUvError||Math.Abs(a.color.r-b.color.r)>profile.maximumColorError||Math.Abs(a.color.g-b.color.g)>profile.maximumColorError||Math.Abs(a.color.b-b.color.b)>profile.maximumColorError)continue;
                    bool attributes=true;for(int c=0;c<a.custom.Length;c++)if(Math.Abs(a.custom[c]-b.custom[c])>1e-6f){attributes=false;break;}if(!attributes)continue;
                    var p=(a.p+b.p)*.5f;float error=Math.Max(a.error+(p-a.p).Length,b.error+(p-b.p).Length);if(error>errorLimit||error>=bestError)continue;
                    bool valid=true;
                    var affected=new HashSet<Triangle>(incident[edge.a]);affected.UnionWith(incident[edge.b]);
                    foreach(var face in affected)
                    {
                        if(edge.faces.Contains(face))continue;var pa=vertices[face.a].p;var pb=vertices[face.b].p;var pc=vertices[face.c].p;var old=V3.Cross(pb-pa,pc-pa).Normalized;
                        V3 Next(int index)=>index==edge.a||index==edge.b?p:vertices[index].p;
                        var normal=V3.Cross(Next(face.b)-Next(face.a),Next(face.c)-Next(face.a));if(normal.LengthSquared<1e-12f||V3.Dot(normal.Normalized,old)<cosine){valid=false;break;}
                    }
                    if(valid){best=edge;bestError=error;bestPosition=p;}
                }
                if(best==null||result.testedEdges>profile.maximumCandidateTests)break;
                var keep=vertices[best.a];var drop=vertices[best.b];keep.p=bestPosition;keep.error=bestError;keep.n=(keep.n+drop.n).Normalized;keep.uv=V2.Lerp(keep.uv,drop.uv,.5f);keep.color=Tint.Lerp(keep.color,drop.color,.5f);drop.alive=false;
                foreach(var t in triangles.Where(t=>t.alive))
                {if(t.a==best.b)t.a=best.a;if(t.b==best.b)t.b=best.a;if(t.c==best.b)t.c=best.a;if(t.a==t.b||t.b==t.c||t.c==t.a){t.alive=false;live--;}}
                result.collapsedEdges++;result.maximumDeviation=Math.Max(result.maximumDeviation,bestError*profile.worldScale);
            }
            var remaining=triangles.Where(t=>t.alive).ToArray();var outVertices=new List<V3>();var outNormals=new List<V3>();var outUv=new List<V2>();var outColors=new List<Tint>();var outIndices=new List<int>();var outMaterials=new List<int>();var outCustom=new List<float[]>();var remap=new Dictionary<int,int>();
            foreach(var t in remaining)
            {
                var faceNormal=V3.Cross(vertices[t.b].p-vertices[t.a].p,vertices[t.c].p-vertices[t.a].p).Normalized;
                foreach(int index in new[]{t.a,t.b,t.c})
                {
                    if(profile.normals==NormalMode.Flat||!remap.TryGetValue(index,out _))
                    {var v=vertices[index];remap[index]=outVertices.Count;outVertices.Add(v.p);outNormals.Add(profile.normals==NormalMode.Flat?faceNormal:v.n);outUv.Add(v.uv);outColors.Add(v.color);outCustom.Add(v.custom);}
                    outIndices.Add(remap[index]);
                }
                outMaterials.Add(t.material);
            }
            var output=new MeshData{positions=outVertices.ToArray(),normals=outNormals.ToArray(),uv=source.uv.Length>0?outUv.ToArray():Array.Empty<V2>(),colors=source.colors.Length>0?outColors.ToArray():Array.Empty<Tint>(),indices=outIndices.ToArray(),materials=source.materials.Length>0?outMaterials.ToArray():Array.Empty<int>()};
            output.channels=new VertexChannel[source.channels.Length];int channelOffset=0;
            for(int c=0;c<output.channels.Length;c++){var src=source.channels[c];var values=new float[outVertices.Count*src.components];for(int i=0;i<outVertices.Count;i++)Array.Copy(outCustom[i],channelOffset,values,i*src.components,src.components);output.channels[c]=new VertexChannel{name=src.name,components=src.components,values=values};channelOffset+=src.components;}
            output.parts=source.parts.Select(part=>new MeshPart{id=part.id,role=part.role,materialKind=part.materialKind,preserve=part.preserve,triangles=remaining.Select((t,i)=>new{t,i}).Where(x=>part.triangles.Contains(x.t.original)).Select(x=>x.i).ToArray()}).ToArray();
            if(profile.normals==NormalMode.Smooth)output.RecalculateNormals();output.Validate();result.mesh=output;
            if(output.TriangleCount>result.targetTriangles)result.diagnostics.Add(new Diagnostic("protected-detail-limit",null,"Target stopped at topology, attributes or silhouette/error constraints",false));
            if(result.testedEdges>profile.maximumCandidateTests)result.diagnostics.Add(new Diagnostic("style-work-limit",null,"Stopped at the explicit candidate-test limit; original protected data retained",false));
            if(source.TriangleCount<result.targetTriangles)result.diagnostics.Add(new Diagnostic("below-reference-detail",null,"Source has fewer structural details than reference; authoring may be required",false));
            return result;
        }
    }
}
