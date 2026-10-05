using QuietCamp.Domain;
using UnityEngine;

namespace QuietCamp.Presentation.World
{
    /// <summary>One authored water band for scene meshes, map previews and scenic exclusions.</summary>
    public static class ShorelineGeometry
    {
        public const float MaximumBend=.6f;
        public static Vector3 Side(ShorelineData shore)
            =>shore.side=="left"?Vector3.left:shore.side=="right"?Vector3.right:shore.side=="back"?Vector3.back:Vector3.forward;
        public static float Bend(ShorelineData shore,float distance)
            =>Mathf.Sin(distance*.27f+shore.seed*.01f)*.48f+Mathf.Sin(distance*.63f)*.12f;
        public static Vector3 Point(ShorelineData shore,float distance,float fraction,float height=0)
        {
            var side=Side(shore);var along=Vector3.Cross(Vector3.up,side);
            float axis=shore.offset+Bend(shore,distance)+(fraction-.5f)*Mathf.Max(1.2f,shore.width);
            return side*axis+along*distance+Vector3.up*height;
        }
        public static bool Contains(ShorelineData shore,Vector3 point,float margin=0)
            => DistanceToBank(shore,point)<margin;
        public static float DistanceToBank(ShorelineData shore,Vector3 point)
        {
            if(shore==null)return float.PositiveInfinity;
            var side=Side(shore);float distance=Vector3.Dot(point,Vector3.Cross(Vector3.up,side));
            return Mathf.Abs(Vector3.Dot(point,side)-shore.offset-Bend(shore,distance))-Mathf.Max(1.2f,shore.width)*.5f;
        }
    }
}
