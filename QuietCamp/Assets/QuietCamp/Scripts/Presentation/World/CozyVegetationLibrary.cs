using System;
using UnityEngine;

namespace QuietCamp.Presentation.World
{
    /// <summary>Small CC0 meshes, normalized once in the Editor; no model parsing in a player.</summary>
    public sealed class CozyVegetationLibrary : ScriptableObject
    {
        public enum PlantKind { Shrub, Leaf, Flower }
        [Serializable]
        public sealed class Plant
        {
            public string id;
            public Mesh mesh;
            public PlantKind kind;
            public Color[] colors;
        }
        public Plant[] plants = Array.Empty<Plant>();
        /// <summary>Low, wide leaf assets must be sized by their silhouette as well as height.</summary>
        public static float GroundcoverScale(PlantKind kind,Bounds bounds,float desiredHeight)
        {
            float span=Mathf.Max(bounds.size.x,bounds.size.z);
            float limit=kind==PlantKind.Shrub?.86f:kind==PlantKind.Leaf?.46f:.30f;
            return Mathf.Min(desiredHeight/Mathf.Max(.001f,bounds.size.y),limit/Mathf.Max(.001f,span));
        }
        public static CozyVegetationLibrary Load() => Resources.Load<CozyVegetationLibrary>("QuietCamp/CozyVegetationLibrary");
    }
}
