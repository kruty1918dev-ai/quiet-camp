using System;
using System.Collections.Generic;
using UnityEngine;

namespace QuietCamp.Infrastructure
{
    /// <summary>Three metric plants, separate readable stalks and angled flower heads.</summary>
    public static class SunflowerGeometry
    {
        public static CampArtDefinition.Model Create()
        {
            var vertices=new List<float>();var normals=new List<float>();var colors=new List<int>();
            void Tri(Vector3 a,Vector3 b,Vector3 c,int color)
            {
                var n=Vector3.Cross(b-a,c-a).normalized;
                foreach(var p in new[]{a,b,c}){vertices.AddRange(new[]{p.x,p.y,p.z});normals.AddRange(new[]{n.x,n.y,n.z});colors.Add(color);}
            }
            void Tube(Vector3 bottom,Vector3 top,float radius,int color)
            {
                for(int i=0;i<6;i++){float a=i*Mathf.PI/3,b=(i+1)*Mathf.PI/3;var u=new Vector3(Mathf.Cos(a),0,Mathf.Sin(a))*radius;var v=new Vector3(Mathf.Cos(b),0,Mathf.Sin(b))*radius;Tri(bottom+u,bottom+v,top+v,color);Tri(bottom+u,top+v,top+u,color);}
            }
            for(int plant=0;plant<3;plant++)
            {
                var root=new Vector3((plant-1)*.48f,0,plant==1?.16f:-.16f);float h=1.7f+plant*.10f;
                var head=root+new Vector3(.06f,h,0);Tube(root,head,.018f,0x49642d);
                for(int leaf=0;leaf<3;leaf++)
                {
                    float side=leaf%2==0?1:-1;var at=root+Vector3.up*(.55f+leaf*.32f);var tip=at+new Vector3(.26f*side,.06f,.05f);
                    Tri(at,at+new Vector3(.13f*side,.04f,-.09f),tip,0x4f6b30);Tri(at,tip,at+new Vector3(.14f*side,.04f,.13f),0x5b7836);
                }
                var rotation=Quaternion.Euler(24,plant==1?8:-6,0);Vector3 Point(float x,float y,float z)=>head+rotation*new Vector3(x,y,z);
                for(int i=0;i<14;i++)
                {
                    float a=(i-.42f)*Mathf.PI*2/14,b=i*Mathf.PI*2/14,c=(i+.42f)*Mathf.PI*2/14;
                    Tri(Point(Mathf.Sin(a)*.095f,Mathf.Cos(a)*.095f,0),Point(Mathf.Sin(b)*.19f,Mathf.Cos(b)*.19f,.012f),Point(Mathf.Sin(c)*.095f,Mathf.Cos(c)*.095f,0),i%3==0?0xd9a82d:0xf0c13e);
                }
                for(int i=0;i<10;i++)
                {
                    float a=i*Mathf.PI/5,b=(i+1)*Mathf.PI/5;
                    Tri(Point(0,0,-.02f),Point(Mathf.Sin(a)*.092f,Mathf.Cos(a)*.092f,-.02f),Point(Mathf.Sin(b)*.092f,Mathf.Cos(b)*.092f,-.02f),i%2==0?0x5d4028:0x694a2a);
                    Tri(Point(0,0,.028f),Point(Mathf.Sin(b)*.092f,Mathf.Cos(b)*.092f,.028f),Point(Mathf.Sin(a)*.092f,Mathf.Cos(a)*.092f,.028f),0x49642d);
                }
            }
            return new CampArtDefinition.Model{id="ua_sunflower_patch",shading="foliage",seasonalRole="flower",vertices=vertices.ToArray(),normals=normals.ToArray(),colors=colors.ToArray()};
        }
    }
}
