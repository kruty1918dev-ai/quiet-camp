using System;
using UnityEngine;

namespace QuietCamp.Infrastructure
{
    /// <summary>Scene-local near-surface occlusion from projected source triangles.
    /// Open arches and spaces between legs stay open; elevated geometry casts no ground stain.</summary>
    public sealed class CampGroundContactField
    {
        public readonly Rect Bounds;
        public readonly int Size;
        readonly float[] _ground,_contact;
        public CampGroundContactField(Rect bounds,int size,Func<float,float,float> ground)
        {
            Bounds=bounds;Size=size;_ground=new float[size*size];_contact=new float[size*size];
            for(int z=0;z<size;z++)for(int x=0;x<size;x++)
                _ground[z*size+x]=ground(bounds.xMin+(x+.5f)/size*bounds.width,bounds.yMin+(z+.5f)/size*bounds.height);
        }
        public void Clear()=>Array.Clear(_contact,0,_contact.Length);
        Vector3 Pixel(Vector3 p)=>new Vector3((p.x-Bounds.xMin)/Bounds.width*Size-.5f,p.y,(p.z-Bounds.yMin)/Bounds.height*Size-.5f);
        void Mark(int x,int z,float y)
        {
            if(x<0||z<0||x>=Size||z>=Size)return;int at=z*Size+x;float gap=y-_ground[at];
            if(gap<-.12f||gap>.38f)return;
            float weight=1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(.025f,.38f,Mathf.Max(0,gap)));
            _contact[at]=Mathf.Max(_contact[at],weight);
        }
        public void Triangle(Vector3 a,Vector3 b,Vector3 c)
        {
            a=Pixel(a);b=Pixel(b);c=Pixel(c);
            int x0=Math.Max(0,Mathf.FloorToInt(Math.Min(a.x,Math.Min(b.x,c.x)))),x1=Math.Min(Size-1,Mathf.CeilToInt(Math.Max(a.x,Math.Max(b.x,c.x))));
            int z0=Math.Max(0,Mathf.FloorToInt(Math.Min(a.z,Math.Min(b.z,c.z)))),z1=Math.Min(Size-1,Mathf.CeilToInt(Math.Max(a.z,Math.Max(b.z,c.z))));
            for(int z=z0;z<=z1;z++)for(int x=x0;x<=x1;x++)
                if(CampTerrainSurface.Height(a,b,c,x,z,out float y))Mark(x,z,y);
            // Conservative edge sampling retains thin poles and vertical faces smaller than a texel.
            void Edge(Vector3 p,Vector3 q)
            {int steps=Math.Min(Size*2,Math.Max(1,Mathf.CeilToInt(Mathf.Max(Mathf.Abs(p.x-q.x),Mathf.Abs(p.z-q.z))*2)));
                for(int i=0;i<=steps;i++){var s=Vector3.Lerp(p,q,i/(float)steps);Mark(Mathf.RoundToInt(s.x),Mathf.RoundToInt(s.z),s.y);}}
            Edge(a,b);Edge(b,c);Edge(c,a);
        }
        public Color32[] Pixels()
        {
            // One small spatial filter, in metres: no object-centred circles or large black halos.
            var result=new Color32[_contact.Length];float step=Mathf.Max(Bounds.width,Bounds.height)/Size;
            float neighbour=Mathf.Clamp01(.14f/step)*.45f;
            for(int z=0;z<Size;z++)for(int x=0;x<Size;x++)
            {
                float value=_contact[z*Size+x];
                for(int dz=-1;dz<=1;dz++)for(int dx=-1;dx<=1;dx++)
                {int px=x+dx,pz=z+dz;if(px<0||pz<0||px>=Size||pz>=Size||dx==0&&dz==0)continue;
                    value=Mathf.Max(value,_contact[pz*Size+px]*neighbour*(dx!=0&&dz!=0?.65f:1));}
                result[z*Size+x]=new Color32((byte)Mathf.RoundToInt(value*255),0,0,255);
            }
            return result;
        }
    }
}
