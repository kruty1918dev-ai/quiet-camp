// Identical weather displacement and material layout across passes.
        CBUFFER_START(UnityPerMaterial)
        float4 _QCWeather,_QCWind,_GroundFinish,_MeadowExtent;
        float4 _ArtAmbient,_GroundLow,_GroundHigh,_SoilTone,_CampFieldSize;
        float4 _CampUseBounds,_CampClimate,_WeatherSkyBounds,_WeatherSkyGrid;
        float4x4 _WeatherSkyToLocal,_WeatherSkyNormalToLocal;
        float _WeatherSnowCover;
        float _ArtGround,_ArtSnowSurface,_ArtWater,_ArtGrass,_ArtMeadow;
        float _ArtRiverDetail,_ArtRiverWaterline;
        CBUFFER_END
        #define QC_CAMP_SURFACE_UNIFORMS
        #include "CampSurfaceUse.hlsl"
        #include "WeatherSky.hlsl"
        float3 WeatherPosition(float3 p,float2 response)
        {
            float t=_QCWeather.x;
            float gust=.65+.35*sin(t*.7+p.x*.19+p.z*.24);
            float wave=sin(t*1.7+p.x*.54+p.z*.39);
            float weight=response.x*min(response.y,3);
            p.xz+=_QCWind.xy*wave*gust*_QCWeather.y*weight*.22;
            p.y-=abs(wave)*_QCWeather.z*weight*.028;
            if(_ArtGround>.5)
            {
                float mud=CampUse(p.xz).a*(1-_CampClimate.y);
                p.y+=sin(p.x*19+p.z*7)*sin(p.z*13-p.x*11)*mud*.009;
                if(_ArtSnowSurface>.5){float4 use=CampUse(p.xz);p.y-=use.b*.018+use.g*.01;}
            }
            return CampPressure(p,response.x*response.y);
        }
