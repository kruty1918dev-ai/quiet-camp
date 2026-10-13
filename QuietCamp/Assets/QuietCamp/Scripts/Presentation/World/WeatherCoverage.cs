using UnityEngine;

namespace QuietCamp.Presentation.World
{
    public static class WeatherCoverage
    {
        public static Rect Ground(Camera camera,Transform world,float margin)
        {
            var lo=new Vector2(float.MaxValue,float.MaxValue);var hi=new Vector2(float.MinValue,float.MinValue);
            for(int i=0;i<4;i++)
            {
                var ray=camera.ViewportPointToRay(new Vector3(i&1,(i>>1)&1));
                var origin=world.InverseTransformPoint(ray.origin);var direction=world.InverseTransformDirection(ray.direction);
                float distance=-origin.y/Mathf.Min(-.001f,direction.y);var point=origin+direction*Mathf.Max(0,distance);
                var p=new Vector2(point.x,point.z);lo=Vector2.Min(lo,p);hi=Vector2.Max(hi,p);
            }
            return Rect.MinMaxRect(lo.x-margin,lo.y-margin,hi.x+margin,hi.y+margin);
        }
    }
}
