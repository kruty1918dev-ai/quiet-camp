using UnityEngine;

namespace QuietCamp.Presentation.World
{
    /// <summary>Dimensions in the tent root's metres, never the scaled model's coordinates.</summary>
    public readonly struct TentLivingSpace
    {
        public readonly Bounds Bounds;
        public float Width=>Bounds.size.x;
        public float Height=>Bounds.size.y;
        public float Depth=>Bounds.size.z;
        public float Floor=>Bounds.min.y+.025f;
        public TentLivingSpace(Bounds bounds)=>Bounds=bounds;
        /// <summary>Fit one rigid item below the pitched roof, retaining its proportions.</summary>
        public Bounds FitInterior(Bounds item)
        {
            // Leave clearance for inward wind movement, canvas thickness and end panels.
            float margin=Mathf.Min(.07f,Width*.04f);
            float width=Mathf.Max(.01f,Width-2*margin),height=Mathf.Max(.01f,Height-margin-.025f);
            float depth=Mathf.Max(.01f,Depth-2*margin);
            float scale=Mathf.Min(1,depth/Mathf.Max(.001f,item.size.z),
                .9f/Mathf.Max(.001f,item.size.x/width+item.size.y/height));
            var size=item.size*scale;
            float top=Floor+height*(.9f-size.x/width);
            float y=Mathf.Clamp(item.center.y,Floor+size.y*.5f,top-size.y*.5f);
            float halfX=Mathf.Max(0,width*.5f*(.9f-(y+size.y*.5f-Floor)/height)-size.x*.5f);
            float halfZ=Mathf.Max(0,(depth-size.z)*.5f);
            return new Bounds(new Vector3(Mathf.Clamp(item.center.x,Bounds.center.x-halfX,Bounds.center.x+halfX),
                y,Mathf.Clamp(item.center.z,Bounds.center.z-halfZ,Bounds.center.z+halfZ)),size);
        }
        public static TentLivingSpace Measure(Transform root)
        {
            var bounds=new Bounds();bool found=false;
            foreach(var renderer in root.GetComponentsInChildren<MeshRenderer>(true))
            foreach(var material in renderer.sharedMaterials)
            {
                if(material==null||material.shader.name!="QuietCamp/TentCloth"||renderer.name=="Settling entrance cloth"||renderer.name=="Wind carried fabric")continue;
                Vector3 min=material.GetVector("_MeshMin"),size=material.GetVector("_MeshSize");
                if(size.y<.01f)continue;
                for(int i=0;i<8;i++)
                {
                    var p=root.InverseTransformPoint(renderer.transform.TransformPoint(min+Vector3.Scale(size,new Vector3(i&1,(i>>1)&1,(i>>2)&1))));
                    if(!found){bounds=new Bounds(p,Vector3.zero);found=true;}else bounds.Encapsulate(p);
                }
                break;
            }
            if(!found)bounds=new Bounds(new Vector3(0,.7f,0),new Vector3(1.8f,1.4f,1.7f));
            return new TentLivingSpace(bounds);
        }
        public Rect Footprint(Vector3 position,int rotation)
        {
            var min=new Vector2(float.MaxValue,float.MaxValue);var max=-min;var yaw=Quaternion.Euler(0,rotation*90,0);
            for(int i=0;i<4;i++)
            {
                var p=yaw*new Vector3((i&1)==0?Bounds.min.x:Bounds.max.x,0,(i&2)==0?Bounds.min.z:Bounds.max.z)+position;
                min=Vector2.Min(min,new Vector2(p.x,p.z));max=Vector2.Max(max,new Vector2(p.x,p.z));
            }
            return Rect.MinMaxRect(min.x,min.y,max.x,max.y);
        }
    }
}
