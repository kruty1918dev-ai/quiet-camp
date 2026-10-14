using System;
using UnityEngine;
using QuietCamp.Infrastructure;

namespace QuietCamp.Editor.LevelAuthoring
{
    [Serializable] public sealed class SurfaceWeatheringSettings
    {
        public string material = "generic", distribution = "natural";
        public float patchSize = 1.5f;
        public int? tint;
    }

    /// <summary>Deterministic face-level material wear, baked into the existing vertex palette.</summary>
    public static class SurfaceWeathering
    {
        public static void Apply(CampArtDefinition.Model model, ModelAuthoringRecipe recipe)
        {
            var s = recipe.weathering;
            if (s == null || s.patchSize <= .01f || float.IsNaN(s.patchSize) || float.IsInfinity(s.patchSize))
                throw new ArgumentException("Масштаб старіння має бути додатним.");
            if (recipe.backend == "rust" && s.material == "aluminium")
                throw new ArgumentException("Для алюмінію оберіть окиснення або зношення фарби; іржа призначена для сталі.");
            var bounds = AuthoringPropCatalog.ModelBounds(model);
            float height = Mathf.Max(.01f, bounds.size.y), seed = recipe.seed * .137f;
            Color target = s.tint.HasValue ? CampArtDefinition.Color(s.tint.Value) :
                recipe.backend == "rust" ? new Color(.42f,.25f,.13f) :
                recipe.backend == "oxidation" ? new Color(.53f,.58f,.55f) :
                recipe.backend == "paint" ? new Color(.63f,.65f,.60f) :
                recipe.backend == "moss" ? new Color(.25f,.32f,.13f) :
                recipe.backend == "scorch" ? new Color(.16f,.15f,.12f) : new Color(.27f,.22f,.15f);
            for (int t = 0; t < model.colors.Length; t += 3)
            {
                Vector3 centre = Vector3.zero, normal = Vector3.zero;
                for (int j = 0; j < 3; j++)
                {
                    int v = (t+j)*3;
                    centre += new Vector3(model.vertices[v],model.vertices[v+1],model.vertices[v+2])/3;
                    normal += new Vector3(model.normals[v],model.normals[v+1],model.normals[v+2])/3;
                }
                float lower = 1-Mathf.Clamp01((centre.y-bounds.min.y)/height);
                float patch = Mathf.PerlinNoise(centre.x/s.patchSize+seed,centre.z/s.patchSize-seed*.7f);
                float mask = Mathf.SmoothStep(0,1,(patch-.3f)*2.2f);
                if (recipe.backend == "dirt" || recipe.backend == "moss") mask *= lower*lower;
                if (recipe.backend == "streaks")
                {
                    // Narrow coherent vertical runs, rather than independent speckles.
                    float run = Mathf.PerlinNoise(centre.x/s.patchSize*4+seed,centre.z/s.patchSize*4-seed);
                    mask = Mathf.SmoothStep(0,1,(run-.52f)*5)*( .25f+.75f*lower)*(1-Mathf.Abs(normal.y));
                }
                if (recipe.backend == "paint") mask = Mathf.SmoothStep(0,1,(patch-.4f)*3);
                if (s.distribution == "lower") mask *= lower;
                else if (s.distribution == "upward") mask *= Mathf.Max(0,normal.y);
                else if (s.distribution == "sides") mask *= 1-Mathf.Abs(normal.y);
                else if (s.distribution == "edges") mask *= Mathf.Max(lower,1-lower);
                float amount = Mathf.Clamp01(recipe.amount)*mask;
                for (int j = 0; j < 3; j++)
                {
                    var old = CampArtDefinition.Color(model.colors[t+j]);
                    // Preserve dark apertures, rubber and windows as readable material changes.
                    float protection = old.maxColorComponent < .41f && s.material == "aluminium" ? .16f : 1;
                    if(s.material=="aluminium" && (recipe.backend=="oxidation"||recipe.backend=="paint") && old.maxColorComponent-Mathf.Min(old.r,old.g,old.b)>.14f)protection=0;
                    if(s.material=="steel" && recipe.backend=="rust" && old.maxColorComponent-Mathf.Min(old.r,old.g,old.b)>.085f)protection=0;
                    model.colors[t+j] = Pack(Color.Lerp(old,target,amount*protection));
                }
            }
        }
        static int Pack(Color c) => Mathf.RoundToInt(Mathf.Clamp01(c.r)*255)<<16 | Mathf.RoundToInt(Mathf.Clamp01(c.g)*255)<<8 | Mathf.RoundToInt(Mathf.Clamp01(c.b)*255);
    }
}
