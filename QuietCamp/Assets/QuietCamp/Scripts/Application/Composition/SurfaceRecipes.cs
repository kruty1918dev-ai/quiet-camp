using System;
using System.Collections.Generic;
using System.Linq;

namespace QuietCamp.Composition
{
    [Serializable] public sealed class SurfaceRecipe
    {
        public string id,kind,owner;
        public Point[] points=Array.Empty<Point>();
        public float heightOffset,feather=1;public bool relativeToOwner;
    }
    /// <summary>Convex terrain patches in source coordinates; baked only, no runtime sampling cost.</summary>
    public static class SurfaceRecipes
    {
        public static bool Wet(SurfaceRecipe surface)=>surface.kind=="channel";
        static float Cross(Point a,Point b,Point p)=>(b.x-a.x)*(p.z-a.z)-(b.z-a.z)*(p.x-a.x);
        public static bool Contains(SurfaceRecipe surface,float x,float z)
        {
            var p=new Point(x,z);bool positive=false,negative=false;
            for(int i=0;i<surface.points.Length;i++)
            {float cross=Cross(surface.points[i],surface.points[(i+1)%surface.points.Length],p);positive|=cross>.0001f;negative|=cross<-.0001f;}
            return !(positive&&negative);
        }
        public static float EdgeDistance(SurfaceRecipe surface,float x,float z)
        {
            float best=float.MaxValue;
            for(int i=0;i<surface.points.Length;i++)
            {
                var a=surface.points[i];var b=surface.points[(i+1)%surface.points.Length];float dx=b.x-a.x,dz=b.z-a.z;
                float t=Math.Clamp(((x-a.x)*dx+(z-a.z)*dz)/Math.Max(.00001f,dx*dx+dz*dz),0,1);
                float sx=x-a.x-dx*t,sz=z-a.z-dz*t;best=Math.Min(best,(float)Math.Sqrt(sx*sx+sz*sz));
            }return best;
        }
        public static bool Intersects(SurfaceRecipe surface,Point a,Point b)
        {
            if(Contains(surface,a.x,a.z)||Contains(surface,b.x,b.z))return true;
            for(int i=0;i<surface.points.Length;i++)
            {
                var c=surface.points[i];var d=surface.points[(i+1)%surface.points.Length];
                float abx=b.x-a.x,abz=b.z-a.z,cdx=d.x-c.x,cdz=d.z-c.z,den=abx*cdz-abz*cdx;
                if(Math.Abs(den)<.00001f)continue;
                float t=((c.x-a.x)*cdz-(c.z-a.z)*cdx)/den,u=((c.x-a.x)*abz-(c.z-a.z)*abx)/den;
                if(t>=0&&t<=1&&u>=0&&u<=1)return true;
            }return false;
        }
        public static float Height(IEnumerable<SurfaceRecipe> surfaces,float x,float z)
        {
            float height=0;
            foreach(var surface in surfaces)if(!surface.relativeToOwner&&Contains(surface,x,z))
            {
                float t=Math.Clamp(EdgeDistance(surface,x,z)/Math.Max(.001f,surface.feather),0,1);
                height=surface.heightOffset*t*t*(3-2*t);
            }return height;
        }
        public static SurfaceRecipe[] Resolve(SceneCompositionDocument doc,CompositionResult result)
        {
            return doc.surfaces.Select(surface=>
            {
                if(!surface.relativeToOwner)return surface;
                var parcel=result.parcels.Find(p=>p.id==surface.owner);
                if(parcel==null)throw new InvalidOperationException("Missing surface owner placement: "+surface.owner);
                float angle=parcel.yaw*(float)Math.PI/180,c=(float)Math.Cos(angle),s=(float)Math.Sin(angle);
                return new SurfaceRecipe{id=surface.id,kind=surface.kind,owner=surface.owner,feather=surface.feather*parcel.scale,
                    heightOffset=surface.heightOffset*parcel.scale,points=surface.points.Select(p=>new Point(
                        parcel.x+(p.x*c+p.z*s)*parcel.scale,parcel.z+(-p.x*s+p.z*c)*parcel.scale)).ToArray()};
            }).ToArray();
        }
        public static void Validate(SceneCompositionDocument doc,CompositionResult result)
        {
            void Error(string id,string code,string message)=>result.diagnostics.Add(new CompositionDiagnostic
            {entityId=id,code=code,message=message,sourcePointer="/surfaces/"+Array.FindIndex(doc.surfaces,s=>s?.id==id)});
            var ids=new HashSet<string>(doc.nodes.Select(n=>n.id).Concat(doc.zones.Select(z=>z.id)).Concat(doc.routes.Select(r=>r.id)).Concat(doc.ensembles.Select(e=>e.id)).Concat(doc.landmarks.Select(l=>l.id)));
            var owners=new HashSet<string>(doc.ensembles.Select(e=>e.id).Concat(doc.landmarks.Select(l=>l.compoundOwner??l.id)).Concat(doc.landmarks.Select(l=>l.id)));
            foreach(var surface in doc.surfaces)
            {
                if(surface==null){Error(null,"invalid-surface","Surface cannot be null.");continue;}
                if(string.IsNullOrWhiteSpace(surface.id)||!ids.Add(surface.id))Error(surface.id,"duplicate-id","Surface ID is missing or reused.");
                if(!new[]{"gravel","bus-bay","pothole","garden","reservoir-bed","channel","erosion","service-yard"}.Contains(surface.kind)
                    ||surface.points==null||surface.points.Length<3||surface.points.Any(p=>p==null||float.IsNaN(p.x)||float.IsInfinity(p.x)||float.IsNaN(p.z)||float.IsInfinity(p.z))
                    ||float.IsNaN(surface.heightOffset)||float.IsInfinity(surface.heightOffset)||Math.Abs(surface.heightOffset)>1
                    ||float.IsNaN(surface.feather)||float.IsInfinity(surface.feather)||surface.feather<=0)
                {Error(surface.id,"invalid-surface","Finite convex polygon, supported kind, bounded height and positive feather required.");continue;}
                float sign=0,area=0;bool invalid=false;
                for(int i=0;i<surface.points.Length;i++)
                {
                    var a=surface.points[i];var b=surface.points[(i+1)%surface.points.Length];var c=surface.points[(i+2)%surface.points.Length];
                    float cross=Cross(a,b,c);if(Math.Abs(cross)<.00001f)invalid=true;else if(sign==0)sign=Math.Sign(cross);else if(Math.Sign(cross)!=sign)invalid=true;
                    area+=a.x*b.z-b.x*a.z;
                }
                // Check every point against every oriented edge; also rejects self-intersecting stars.
                foreach(var a in surface.points)if(!Contains(surface,a.x,a.z))invalid=true;
                if(invalid||Math.Abs(area)<.01f){Error(surface.id,"invalid-surface","Polygon must be simple, strictly convex and nondegenerate.");continue;}
                if(surface.relativeToOwner&&(surface.owner==null||!doc.ensembles.Any(e=>e.id==surface.owner)
                    ||Wet(surface)))Error(surface.id,"orphan-surface","Local terrain patches require an ensemble owner and cannot define water.");
                if(surface.owner!=null&&!owners.Contains(surface.owner))Error(surface.id,"orphan-surface","Surface owner is missing.");
                if((surface.kind=="garden"||surface.kind=="bus-bay"||surface.kind=="service-yard")&&surface.owner==null)
                    Error(surface.id,"orphan-surface","Owned garden, bus bay or yard requires an ensemble/landmark owner.");
                if(Wet(surface))foreach(var route in doc.routes.Where(r=>r.kind=="road"||r.kind=="path"))
                    for(int i=1;i<route.points.Length;i++)if(Intersects(surface,route.points[i-1],route.points[i]))
                        Error(surface.id,"wet-route-crossing","Dry route intersects channel: "+route.id);
            }
        }
        public sealed class Terrain : ITerrainSample
        {
            readonly ITerrainSample basis;readonly SurfaceRecipe[] surfaces;
            public Terrain(ITerrainSample basis,SurfaceRecipe[] surfaces){this.basis=basis;this.surfaces=surfaces;}
            public bool Supported(float x,float z,float radius)
            {
                if(!basis.Supported(x,z,radius))return false;
                foreach(var surface in surfaces.Where(Wet))if(Contains(surface,x,z)||EdgeDistance(surface,x,z)<radius)return false;
                return true;
            }
            public float Height(float x,float z)=>basis.Height(x,z)+SurfaceRecipes.Height(surfaces,x,z);
        }
    }
}
