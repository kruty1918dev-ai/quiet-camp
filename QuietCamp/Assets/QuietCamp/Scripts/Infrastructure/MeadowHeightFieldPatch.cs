#if UNITY_EDITOR
using System;
using System.IO;

namespace QuietCamp.Infrastructure
{
    /// <summary>Staged, world-space surface samples. This experiment is never authored by hand.</summary>
    public sealed class MeadowHeightFieldPatch
    {
        public string name;
        public float[] rect, heights;
        public int resolution;
        public string albedoPng, normalHeightPng;
        public MeadowRetainedMesh flowers;

        public void Validate()
        {
            if(rect==null||rect.Length!=4||heights==null||heights.Length!=2||resolution<64||resolution>1024
                ||!Finite(rect)||!Finite(heights)||rect[2]<=0||rect[3]<=0||heights[1]<=heights[0]
                ||string.IsNullOrEmpty(albedoPng)||string.IsNullOrEmpty(normalHeightPng))
                throw new InvalidDataException("Invalid staged meadow height field.");
            flowers?.Validate();
        }
        static bool Finite(float[] values)
        {foreach(float value in values)if(float.IsNaN(value)||float.IsInfinity(value))return false;return true;}
    }
    public sealed class MeadowRetainedMesh
    {
        public float[] vertices,normals,colours,response;
        public void Validate()
        {
            int count=vertices?.Length/3??0;
            if(count==0||count%3!=0||vertices.Length!=count*3||normals?.Length!=count*3||colours?.Length!=count*4||response?.Length!=count*2)
                throw new InvalidDataException("Invalid retained meadow flower geometry.");
            foreach(var channel in new[]{vertices,normals,colours,response})foreach(float value in channel)
                if(float.IsNaN(value)||float.IsInfinity(value))throw new InvalidDataException("Non-finite retained flower channel.");
        }
    }
}

#endif
