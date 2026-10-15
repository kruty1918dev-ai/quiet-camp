using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace QuietCamp.Infrastructure
{
    /// <summary>One sampled channel for bank geometry, ecology, rain contacts and floating debris.</summary>
    public sealed class RiverSurfaceField
    {
        public readonly struct Station
        {
            public readonly Vector3 centre;
            public readonly Vector2 side;
            public readonly float halfWidth, distance;
            public Station(Vector3 centre, Vector2 side, float halfWidth, float distance)
            { this.centre=centre;this.side=side;this.halfWidth=halfWidth;this.distance=distance; }
        }
        public readonly struct Sample
        {
            public readonly Vector3 centre;
            public readonly Vector2 direction;
            public readonly float halfWidth, signedDistance, across, distance;
            public Sample(Vector3 centre, Vector2 direction, float halfWidth, float across, float distance)
            { this.centre=centre;this.direction=direction;this.halfWidth=halfWidth;this.across=across;this.distance=distance;signedDistance=Mathf.Abs(across)-halfWidth; }
            public bool Wet => signedDistance<=0;
        }
        public readonly Station[][] reaches;
        public readonly Vector3[] contacts;
        readonly Dictionary<Vector2Int,List<(Station a,Station b)>> _segments=new Dictionary<Vector2Int,List<(Station,Station)>>();
        const float CellSize=2;
        public RiverSurfaceField(FrozenLandscape landscape)
        {
            var result=new List<Station[]>();
            var points=landscape?.riverContacts??Array.Empty<float>();
            contacts=Enumerable.Range(0,points.Length/3).Select(i=>new Vector3(points[i*3],points[i*3+1],points[i*3+2])).ToArray();
            foreach(var mesh in landscape?.meshes??Array.Empty<LandscapeMesh>())
            {
                if(!mesh.water)continue;
                // River Modeler exports contiguous cross-sections with a shared V coordinate.
                // Do not infer a straight river from the obsolete legacy stream parameters.
                var rows=new List<Station>();float distance=0;
                for(int begin=0;begin<mesh.vertices.Length/3;)
                {
                    int end=begin+1;float v=mesh.uv[begin*2+1];
                    while(end<mesh.vertices.Length/3&&Mathf.Abs(mesh.uv[end*2+1]-v)<.0001f)end++;
                    if(end-begin<2){rows.Clear();break;}
                    Vector3 P(int i)=>new Vector3(mesh.vertices[i*3],mesh.vertices[i*3+1],mesh.vertices[i*3+2]);
                    var a=P(begin);var b=P(end-1);var centre=(a+b)*.5f;
                    var side=new Vector2(b.x-a.x,b.z-a.z);float width=side.magnitude;
                    if(rows.Count>0)distance+=Vector3.Distance(rows[rows.Count-1].centre,centre);
                    if(width>.05f)rows.Add(new Station(centre,side/width,width*.5f,distance));
                    begin=end;
                }
                if(rows.Count>1)result.Add(rows.ToArray());
            }
            reaches=result.ToArray();
            foreach(var reach in reaches)for(int i=1;i<reach.Length;i++)
            {
                var a=reach[i-1];var b=reach[i];float margin=Mathf.Max(a.halfWidth,b.halfWidth)+4;
                int x0=Mathf.FloorToInt((Mathf.Min(a.centre.x,b.centre.x)-margin)/CellSize),x1=Mathf.FloorToInt((Mathf.Max(a.centre.x,b.centre.x)+margin)/CellSize);
                int z0=Mathf.FloorToInt((Mathf.Min(a.centre.z,b.centre.z)-margin)/CellSize),z1=Mathf.FloorToInt((Mathf.Max(a.centre.z,b.centre.z)+margin)/CellSize);
                for(int x=x0;x<=x1;x++)for(int z=z0;z<=z1;z++)
                {
                    var key=new Vector2Int(x,z);if(!_segments.TryGetValue(key,out var bucket)){bucket=new List<(Station,Station)>();_segments.Add(key,bucket);}bucket.Add((a,b));
                }
            }
        }
        public bool TrySample(float x,float z,out Sample sample)
        {
            float nearest=float.MaxValue;sample=default;var p=new Vector2(x,z);bool found=false;
            if(!_segments.TryGetValue(new Vector2Int(Mathf.FloorToInt(x/CellSize),Mathf.FloorToInt(z/CellSize)),out var segments))return false;
            foreach(var segment in segments)
            {
                var a=segment.a;var b=segment.b;var start=new Vector2(a.centre.x,a.centre.z);
                var delta=new Vector2(b.centre.x,b.centre.z)-start;float length=delta.magnitude;
                if(length<.0001f)continue;
                float t=Mathf.Clamp01(Vector2.Dot(p-start,delta)/(length*length));
                var centre=Vector3.Lerp(a.centre,b.centre,t);var offset=p-new Vector2(centre.x,centre.z);
                float squared=offset.sqrMagnitude;if(squared>=nearest)continue;
                // Outside the two end caps is land, not an infinitely extended water strip.
                var tangent=delta/length;float longitudinal=Vector2.Dot(offset,tangent);
                if((t<=0&&longitudinal<-.01f)||(t>=1&&longitudinal>.01f))continue;
                var side=Vector2.Lerp(a.side,b.side,t).normalized;
                sample=new Sample(centre,tangent,Mathf.Lerp(a.halfWidth,b.halfWidth,t),Vector2.Dot(offset,side),Mathf.Lerp(a.distance,b.distance,t));
                nearest=squared;found=true;
            }
            return found;
        }
        public Vector2 Velocity(float x,float z,float speed)
        {
            if(!TrySample(x,z,out var water)||!water.Wet)return Vector2.zero;
            float edge=Mathf.Clamp01(Mathf.Abs(water.across)/Mathf.Max(.1f,water.halfWidth));
            var direction=water.direction;
            foreach(var contact in contacts)
            {
                var away=new Vector2(x-contact.x,z-contact.z);float distance=away.magnitude;
                if(distance<.001f||distance>.85f)continue;
                // Slide around the obstacle even on a head-on approach. A purely
                // radial force would cancel the current or send a leaf through it.
                var side=new Vector2(-water.direction.y,water.direction.x);
                float lateral=Vector2.Dot(away,side);
                float sign=Mathf.Abs(lateral)>.01f?Mathf.Sign(lateral):1;
                var slip=(water.direction*.35f+side*sign*.85f+away/distance*.2f).normalized;
                direction=Vector2.Lerp(direction,slip,1-distance/.85f).normalized;
            }
            return direction*speed*Mathf.Lerp(1,.13f,edge*edge);
        }
    }
}
