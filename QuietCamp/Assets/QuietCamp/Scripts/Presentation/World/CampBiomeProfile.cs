using QuietCamp.Domain;
using UnityEngine;

namespace QuietCamp.Presentation.World
{
    /// <summary>Surface and sound character derived from the saved environment, not quality settings.</summary>
    public readonly struct CampBiomeProfile
    {
        public readonly string BedKey, SurfaceKey;
        public readonly Color TraceColor;
        public readonly float Birds, Crickets, BedWeight, WaterWeight;
        public readonly bool Snow;

        public CampBiomeProfile(LevelData level, float rain = 0)
        {
            var environment = EnvironmentCompositionData.For(level);
            var season=SeasonProfile.For(level);
            Snow = season.IsContinuous?season.SnowCoverage>.55f:environment.seasonId == "winter";
            bool autumn = environment.seasonId == "autumn";
            bool forest = environment.biomeId == "pines" || environment.treeDensity > .65f;
            bool wet = !Snow && (rain > .18f || environment.moisture > .65f);
            BedKey = Snow ? "ambience.biome.winter" : autumn ? "ambience.biome.autumn"
                : forest ? "ambience.biome.forest" : "ambience.biome.meadow";
            SurfaceKey = Snow ? "sfx.surface.snow" : wet ? "sfx.surface.mud"
                : autumn ? "sfx.surface.leaves" : forest ? "sfx.surface.soil" : "sfx.surface.grass";
            TraceColor = Snow ? new Color(.55f,.63f,.71f) : wet ? new Color(.30f,.27f,.22f)
                : autumn ? new Color(.42f,.30f,.20f) : forest ? new Color(.40f,.34f,.26f)
                : new Color(.35f,.43f,.25f);
            Birds = Snow ? .16f : autumn ? .42f : environment.seasonId == "spring" ? 1 : .85f;
            Crickets = Snow ? 0 : autumn ? .12f : environment.seasonId == "spring" ? .35f : 1;
            BedWeight = Snow ? .42f : forest ? .60f : .45f;
            if(season.IsContinuous)
            {
                TraceColor=Color.Lerp(season.Palette.Soil,new Color(.55f,.63f,.71f),season.SnowCoverage);
                Birds=Mathf.Lerp(1,.16f,Mathf.Max(season.BareFraction*.7f,season.SnowCoverage));
                Crickets=(1-season.SnowCoverage)*Mathf.Lerp(.8f,.12f,season.BareFraction);
                BedWeight=Mathf.Lerp(forest?.60f:.45f,.42f,season.SnowCoverage);
            }
            WaterWeight = environment.shore == null ? 0 : environment.shore.kind == "stream" ? .65f : .42f;
        }
    }
}
