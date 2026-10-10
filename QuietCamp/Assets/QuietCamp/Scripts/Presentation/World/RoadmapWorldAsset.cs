using System;
using UnityEngine;

namespace QuietCamp.Presentation.World
{
    [Serializable] public sealed class RoadmapWaypoint
    {
        public string levelId, titleUk;
        public Vector3 position, focus;
        public float yaw, distance = 66, pitch = 48;
    }
    /// <summary>Small index only. Native meshes are loaded through resource leases.</summary>
    public sealed class RoadmapWorldAsset : ScriptableObject
    {
        public string revision, sourceHash;
        public RoadmapWaypoint[] waypoints;
        public string[] chunks;
        public Mesh horizon, markerMesh;
        public Mesh[] distantForest, horizonTerrain;
        public Mesh river;
        public Mesh[] riverByFrontier;
        public float worldScale = .4f;
        public Material ground, structure, foliage, water, marker, motes;
        public Color sky = new Color(.73f,.81f,.83f);
        public float fieldOfView = 30;
        public float[] chunkStarts, chunkEnds;
    }
    /// <summary>Shared authoring sampler: all seams sample identical world coordinates.</summary>
    public static class RoadmapLandscape
    {
        // Matches the authored old-road corridor and its owned stop/yard approaches.
        public static float RoadX(float z) => z<=40?0:z<=80?Mathf.Lerp(0,2,(z-40)/40):z<=120?Mathf.Lerp(2,-3,(z-80)/40):z<=170?Mathf.Lerp(-3,1,(z-120)/50):Mathf.Lerp(1,-2,Mathf.Clamp01((z-170)/35));
        public static float RoadHalfWidth(float z) => Mathf.Lerp(3.2f,1.15f,Mathf.SmoothStep(0,1,Mathf.InverseLerp(22,60,z)));
        public static float RiverX(float z) => 14 + 6 * Mathf.Sin(z * .029f);
        public static float RiverWidth(float z) => Mathf.Lerp(2.5f, 10, Mathf.SmoothStep(0,1,Mathf.InverseLerp(138,180,z)));
        public const float WaterHeight = -.7f;
        public static float WaterDistance(float x,float z) => z < 88 ? 100 : Mathf.Abs(x - RiverX(z)) - RiverWidth(z);
        public static float Height(float x,float z)
        {
            // The road follows a shallow valley floor. Its flanking folds must be
            // inside the portrait view, rather than beyond the camera's edges.
            float h = 1.7f + .65f*Mathf.Sin(x*.10f+z*.017f) + .55f*Mathf.Cos(z*.046f)
                + (5.5f+2.6f*Mathf.Sin(z*.042f+.4f))*Mathf.SmoothStep(0,1,Mathf.InverseLerp(7,30,Mathf.Abs(x-RoadX(z))))
                + Hill(x,z,-22,23,16,26,8.5f) + Hill(x,z,19,61,13,25,9.5f)
                + Hill(x,z,-20,112,18,32,10) + Hill(x,z,34,168,18,36,8)
                - 1.9f*Mathf.SmoothStep(0,1,Mathf.InverseLerp(65,135,z));
            if(z>88)
            {
                // Dry valley land cannot dip below the river plane outside its authored channel.
                h=Mathf.Max(h,WaterHeight+.85f);
                float waterDistance=WaterDistance(x,z);
                // A low floodplain receives the crossing. Hills begin beyond it;
                // extending the full hill height to the shoreline makes a trench.
                h=Mathf.Lerp(WaterHeight+1.5f,h,Mathf.SmoothStep(0,1,Mathf.InverseLerp(3,16,waterDistance)));
                float bank = Mathf.SmoothStep(0,1,Mathf.InverseLerp(-.7f,5,waterDistance));
                h = Mathf.Lerp(WaterHeight-.55f,h,bank * Mathf.SmoothStep(0,1,Mathf.InverseLerp(88,101,z))
                    + 1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(88,101,z)));
            }
            return h;
        }
        static float Hill(float x,float z,float cx,float cz,float rx,float rz,float height)
        {
            float dx=(x-cx)/rx,dz=(z-cz)/rz;
            return height*Mathf.Exp(-(dx*dx+dz*dz)*1.5f);
        }
        public static Color GroundColor(float x,float z)
        {
            float patch = .5f + .25f*Mathf.Sin(x*.23f+z*.11f) + .25f*Mathf.Sin(x*.071f-z*.19f);
            var grass=Color.Lerp(new Color(.29f,.42f,.25f),new Color(.49f,.57f,.32f),patch);
            // Broad meadow variation follows the landform; tiny random colour
            // noise cannot describe a slope at the bird's-eye viewing distance.
            float crest=Mathf.SmoothStep(0,1,Mathf.InverseLerp(5,13,Height(x,z)));
            grass=Color.Lerp(grass,new Color(.55f,.58f,.36f),crest*.35f);
            float path=Mathf.SmoothStep(0,1,Mathf.InverseLerp(2.4f,.65f,Mathf.Abs(x-RoadX(z))));
            var soil=z<26?new Color(.37f,.39f,.35f):new Color(.61f,.56f,.42f);
            grass=Color.Lerp(grass,soil,path*.8f);
            if(z>88)grass=Color.Lerp(grass,new Color(.60f,.58f,.44f),Mathf.Clamp01(1-Mathf.Abs(WaterDistance(x,z))*.45f)*.6f);
            if(z>96)grass=Color.Lerp(grass,new Color(.24f,.30f,.24f),Mathf.Clamp01(-WaterDistance(x,z)*1.2f)*.65f);
            return grass;
        }
    }
}
