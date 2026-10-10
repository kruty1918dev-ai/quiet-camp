using System;
using System.Collections.Generic;
using System.Linq;

namespace QuietCamp.Composition
{
    public static class ParcelBoundary
    {
        static Point[] Outline(EnsembleTemplate t)=>t.boundary.Length>0?t.boundary:new[]{
            new Point(-t.width/2,-t.depth/2),new Point(t.width/2,-t.depth/2),
            new Point(t.width/2,t.depth/2),new Point(-t.width/2,t.depth/2)};
        public static float Distance(EnsembleTemplate t,float x,float z)
        {
            var p=Outline(t);float best=float.MaxValue;
            for(int i=0;i<p.Length;i++)
            {var a=p[i];var b=p[(i+1)%p.Length];float dx=b.x-a.x,dz=b.z-a.z,l=dx*dx+dz*dz;
                float u=Math.Max(0,Math.Min(1,((x-a.x)*dx+(z-a.z)*dz)/l));
                best=Math.Min(best,(float)Math.Sqrt(Math.Pow(x-a.x-u*dx,2)+Math.Pow(z-a.z-u*dz,2)));}
            return best;
        }
        public static bool Closed(EnsembleTemplate template,IEnumerable<EnsembleRole> roles,
            IReadOnlyDictionary<string,VisualAssetDefinition> assets,string fenceVariant)
        {
            var p=Outline(template);if(p.Length<3)return false;
            for(int side=0;side<p.Length;side++)
            {
                var a=p[side];var b=p[(side+1)%p.Length];float dx=b.x-a.x,dz=b.z-a.z,length=(float)Math.Sqrt(dx*dx+dz*dz);
                if(length<.01f)return false;dx/=length;dz/=length;
                var intervals=new List<(float a,float b)>();
                foreach(var role in roles)
                {
                    string id=role.asset=="$fence"?"ua_"+fenceVariant+"_fence":role.asset;
                    if(!assets.TryGetValue(id,out var asset))continue;
                    if(asset.placementClass!="boundary"&&role.id!="gate"&&role.id!="entrance"&&!role.entrance)continue;
                    float rx=role.x-a.x,rz=role.z-a.z;if(Math.Abs(rx*dz-rz*dx)>.15f)continue;
                    float yaw=role.yaw*(float)Math.PI/180,scale=(role.height>0?role.height:asset.height)/asset.height;
                    float w=role.width>0?role.width:asset.width*scale;
                    float extent=(float)(Math.Abs(Math.Cos(yaw)*dx-Math.Sin(yaw)*dz)*w+Math.Abs(Math.Sin(yaw)*dx+Math.Cos(yaw)*dz)*asset.depth*scale)*.5f;
                    float centre=rx*dx+rz*dz;intervals.Add((centre-extent,centre+extent));
                }
                float covered=0;
                foreach(var interval in intervals.OrderBy(i=>i.a))
                {if(interval.a>covered+.04f)return false;covered=Math.Max(covered,interval.b);}
                if(covered<length-.04f)return false;
            }return true;
        }
    }
}
