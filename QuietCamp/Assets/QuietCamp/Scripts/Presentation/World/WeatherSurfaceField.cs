using System.Collections.Generic;
using System.Linq;
using QuietCamp.Infrastructure;
using UnityEngine;

namespace QuietCamp.Presentation.World
{
    public sealed class WeatherSurfaceField
    {
        const float Step=.45f;
        readonly Dictionary<Vector2Int,float> _heights=new Dictionary<Vector2Int,float>();
        public WeatherSurfaceField(CampArtDefinition art)
        {
            var models=art.models.ToDictionary(m=>m.id);
            foreach(var item in art.instances)
            {
                if(item.modelId==null||!models.TryGetValue(item.modelId,out var m)||m.seasonalRole!="solid"||item.motion!=null)continue;
                var pose=Matrix4x4.TRS(new Vector3(item.x,item.y,item.z),item.rotationEuler==null?Quaternion.Euler(0,item.yaw,0):Quaternion.Euler(CampArtDefinition.Vector(item.rotationEuler)),item.scaleXYZ==null?Vector3.one*item.scale:CampArtDefinition.Vector(item.scaleXYZ));
                for(int i=0;i<m.vertices.Length;i+=9)
                {
                    Vector3 Point(int n)=>pose.MultiplyPoint3x4(new Vector3(m.vertices[n],m.vertices[n+1],m.vertices[n+2]));
                    var a=Point(i);var b=Point(i+3);var c=Point(i+6);
                    float denominator=(b.z-c.z)*(a.x-c.x)+(c.x-b.x)*(a.z-c.z);if(Mathf.Abs(denominator)<.001f)continue;
                    int x0=Mathf.FloorToInt(Mathf.Min(a.x,b.x,c.x)/Step),x1=Mathf.CeilToInt(Mathf.Max(a.x,b.x,c.x)/Step);
                    int z0=Mathf.FloorToInt(Mathf.Min(a.z,b.z,c.z)/Step),z1=Mathf.CeilToInt(Mathf.Max(a.z,b.z,c.z)/Step);
                    for(int x=x0;x<=x1;x++)for(int z=z0;z<=z1;z++)
                    {
                        var p=new Vector2((x+.5f)*Step,(z+.5f)*Step);float u=((b.z-c.z)*(p.x-c.x)+(c.x-b.x)*(p.y-c.z))/denominator;
                        float v=((c.z-a.z)*(p.x-c.x)+(a.x-c.x)*(p.y-c.z))/denominator;
                        if(u<0||v<0||u+v>1)continue;float y=a.y*u+b.y*v+c.y*(1-u-v);
                        var key=new Vector2Int(x,z);if(!_heights.TryGetValue(key,out float previous)||y>previous)_heights[key]=y;
                    }
                }
            }
        }
        readonly Dictionary<Vector2Int,float> _tentHeights=new Dictionary<Vector2Int,float>();
        int _tentSignature;
        public void RefreshTents(IEnumerable<TentCloth> tents,Transform world)
        {
            var roots=tents.Where(t=>t!=null).ToArray();var filters=roots.SelectMany(t=>t.GetComponentsInChildren<MeshFilter>()).ToArray();int signature=17;
            unchecked{foreach(var filter in filters)signature=signature*31+filter.transform.localToWorldMatrix.GetHashCode();}
            if(signature==_tentSignature)return;_tentSignature=signature;_tentHeights.Clear();
            foreach(var filter in filters)
            {
                var mesh=filter.sharedMesh;if(mesh==null)continue;var points=mesh.vertices;var triangles=mesh.triangles;
                var pose=world.worldToLocalMatrix*filter.transform.localToWorldMatrix;
                for(int index=0;index<triangles.Length;index+=3)
                {
                    var a=pose.MultiplyPoint3x4(points[triangles[index]]);var b=pose.MultiplyPoint3x4(points[triangles[index+1]]);var c=pose.MultiplyPoint3x4(points[triangles[index+2]]);
                    float denominator=(b.z-c.z)*(a.x-c.x)+(c.x-b.x)*(a.z-c.z);if(Mathf.Abs(denominator)<.001f)continue;
                    int x0=Mathf.FloorToInt(Mathf.Min(a.x,b.x,c.x)/Step),x1=Mathf.CeilToInt(Mathf.Max(a.x,b.x,c.x)/Step);
                    int z0=Mathf.FloorToInt(Mathf.Min(a.z,b.z,c.z)/Step),z1=Mathf.CeilToInt(Mathf.Max(a.z,b.z,c.z)/Step);
                    for(int x=x0;x<=x1;x++)for(int z=z0;z<=z1;z++)
                    {
                        var p=new Vector2((x+.5f)*Step,(z+.5f)*Step);float u=((b.z-c.z)*(p.x-c.x)+(c.x-b.x)*(p.y-c.z))/denominator;
                        float v=((c.z-a.z)*(p.x-c.x)+(a.x-c.x)*(p.y-c.z))/denominator;if(u<0||v<0||u+v>1)continue;
                        float height=a.y*u+b.y*v+c.y*(1-u-v);var key=new Vector2Int(x,z);
                        if(!_tentHeights.TryGetValue(key,out float old)||height>old)_tentHeights[key]=height;
                    }
                }
            }
        }
        public float Height(float x,float z,float ground,bool includeTents=true)
        {
            var key=new Vector2Int(Mathf.FloorToInt(x/Step),Mathf.FloorToInt(z/Step));
            if(_heights.TryGetValue(key,out float surface))ground=Mathf.Max(ground,surface);
            if(includeTents&&_tentHeights.TryGetValue(key,out float roof))ground=Mathf.Max(ground,roof);
            return ground;
        }
    }
}
