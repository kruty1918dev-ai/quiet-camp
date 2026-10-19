Shader "QuietCamp/CampArtLit"
{
    Properties { _WeatherSnowCover("Weather snow cover",Float)=0 _WeatherSkyHeight("Precipitation shelter",2D)="black"{} _WeatherSkyBounds("Shelter bounds",Vector)=(0,0,0,0) _ArtSnowSurface("Settled snow geometry",Float)=0 _MeadowExtent("Meadow extent",Vector)=(0,0,3,0) _ArtMeadow("Continuous meadow",Float)=0 _GroundFinish("Natural ground finish",Vector)=(.34,1.1,.12,0) _ArtRiverDetail("River-bank detail",Float)=0 _ArtRiverWaterline("River waterline",Float)=0 _CampUse("Observed surface use",2D)="black"{} _GroundLow("Ground low",Color)=(.23,.30,.14,1) _GroundHigh("Ground high",Color)=(.40,.48,.22,1) _SoilTone("Soil",Color)=(.43,.35,.25,1) _ArtAmbient("Ambient", Color) = (.22,.24,.16,1) _ArtWater("Water", Float) = 0 _ArtGrass("Grass", Float) = 0 _ArtGround("Ground", Float) = 0 _QCWeather("Weather time, wind, rain, active",Vector)=(0,0,0,0) _QCWind("Wind direction",Vector)=(.8,.35,0,0) }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry" }
        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        // Identical material layout in colour, depth, shadows and vegetation mask.
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
        ENDHLSL
        Pass
        {
            Name "CampArtForward" Tags { "LightMode"="UniversalForward" }
            Cull Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            struct Attributes { float4 positionOS:POSITION; float3 normalOS:NORMAL; float4 color:COLOR; float2 response:TEXCOORD1; float2 snowCatch:TEXCOORD2; };
            struct Varyings { float4 positionCS:SV_POSITION; float3 positionWS:TEXCOORD0; float3 normalWS:TEXCOORD1; float2 response:TEXCOORD2; float3 restWS:TEXCOORD3; float snowCatch:TEXCOORD4; float4 color:COLOR; };
            Varyings Vert(Attributes input)
            {
                Varyings output; output.positionWS=TransformObjectToWorld(input.positionOS.xyz);
                output.restWS=output.positionWS;
                output.snowCatch=input.snowCatch.x;
                output.positionWS=WeatherPosition(output.positionWS,input.response);
                // WeatherPosition is shared by forward, depth and shadow passes.
                // Do not add a second offset here: it detaches moving blades from their shadows.
                output.positionCS=TransformWorldToHClip(output.positionWS); output.normalWS=TransformObjectToWorldNormal(input.normalOS);
                output.response=input.response; output.color=input.color; return output;
            }
            #include "NaturalGround.hlsl"
            float ArtHash(float2 p) { return frac(sin(dot(p,float2(127.1,311.7)))*43758.5453); }
            float ArtNoise(float2 p)
            {
                float2 i=floor(p), f=frac(p); f=f*f*(3-2*f);
                return lerp(lerp(ArtHash(i),ArtHash(i+float2(1,0)),f.x),lerp(ArtHash(i+float2(0,1)),ArtHash(i+1),f.x),f.y);
            }
            half4 Frag(Varyings input):SV_Target
            {
                float3 normal=normalize(input.normalWS);
                if(_ArtGround>.5)
                {
                    float2 p=input.positionWS.xz;float4 use=CampUse(p);
                    normal=NaturalGroundNormal(normal,p,_GroundFinish,_CampClimate.x+use.a,_CampClimate.y);
                }
                if(_ArtMeadow>.5)normal=normalize(lerp(float3(0,1,0),normal,.12));
                Light sun=GetMainLight(TransformWorldToShadowCoord(input.positionWS));
                float diffuse=_ArtGrass>.5?abs(dot(normal,sun.direction)):saturate(dot(normal,sun.direction));
                float3 lighting=_ArtAmbient.rgb*(.8+.2*abs(normal.y))+sun.color*((diffuse+.22*saturate(-dot(normal,sun.direction)))*sun.shadowAttenuation);
                // Grass receives the same weather-dependent light as its surroundings.
                // A fixed green light floor erased shadows and glowed through overcast weather.
                if(_ArtGrass>.5)
                {
                    // Thin leaves scatter daylight; retain the preset's light colour in shade.
                    float leafDiffuse=.32+.68*diffuse;
                    lighting=_ArtAmbient.rgb*1.35+sun.color*leafDiffuse*lerp(.16,1,sun.shadowAttenuation);
                }
                float3 baseColor=input.color.rgb;
                if(_ArtRiverDetail>.5)
                {
                    float2 p=input.positionWS.xz;
                    float wet=max(_CampClimate.x*.55,1-smoothstep(_ArtRiverWaterline-.02,_ArtRiverWaterline+.16,input.positionWS.y));
                    baseColor*=1-wet*.22;
                    float upward=smoothstep(.15,.72,normal.y);
                    float aboveWater=smoothstep(_ArtRiverWaterline+.015,_ArtRiverWaterline+.07,input.positionWS.y);
                    float snow=saturate(_CampClimate.y)*upward*aboveWater*smoothstep(.2,.7,ArtNoise(p*3.1+23));
                    baseColor=lerp(baseColor,float3(.83,.89,.92),snow);
                }
                if(_ArtGrass>.5)
                {
                    // Broad, coherent groups and darker roots, rather than noise on every blade.
                    float green=step(baseColor.r*1.05,baseColor.g)*step(baseColor.b*1.1,baseColor.g);
                    float rootToTip=lerp(_ArtMeadow>.5 ? .82 : .90,_ArtMeadow>.5?1.02:1.04,sqrt(saturate(input.response.x)));
                    float patchTone=lerp(.94,1.04,ArtNoise(input.positionWS.xz*.24+9));
                    baseColor*=lerp(float3(1,1,1),float3(.94,1,1.08)*rootToTip*patchTone,green);
                    float4 trampled=CampUse(input.positionWS.xz);
                    baseColor=lerp(baseColor,float3(.33,.37,.20),trampled.r*.65);
                }
                if(_ArtGround>.5)
                {
                    float2 p=input.positionWS.xz;float4 use=CampUse(p);
                    float3 natural=MeadowSubstrate(NaturalGroundColor(p,_GroundLow.rgb,_GroundHigh.rgb,_SoilTone.rgb,_GroundFinish,_CampClimate.x),p,_MeadowExtent);
                    // Preserve authored scars and banks without retaining a differently painted puzzle rectangle.
                    float fieldDistance=max(abs(p.x)-_CampFieldSize.x,abs(p.y)-_CampFieldSize.y);
                    float undisturbed=1-smoothstep(.3,2.0,fieldDistance+(ArtNoise(p*.8f)-.5)*.45);
                    baseColor=lerp(baseColor,natural,lerp(.94,1,undisturbed));
                    float cover=saturate(_CampClimate.y);
                    float drift=ArtNoise(p*.23+71)*.7+ArtNoise(p*.81-29)*.3;
                    float snow=cover>=.999?1:smoothstep(1-cover-.12,1-cover+.12,drift)*smoothstep(0,.12,cover);
                    snow*=smoothstep(.2,.75,normal.y);
                    baseColor=lerp(baseColor,SettledSnowColor(p)*lerp(float3(1,1,1),input.color.rgb,saturate(_ArtSnowSurface)),snow);
                    // Snow scatters the overcast sky light; shadows remain directional,
                    // without turning the whole blizzard ground into dark grey earth.
                    float skyFacing=.78+.22*saturate(normal.y);
                    float snowDiffuse=.55+.45*saturate(dot(normal,normalize(sun.direction+float3(0,.25,0))));
                    float skyLuma=dot(_ArtAmbient.rgb,float3(.2126,.7152,.0722));
                    float3 snowSky=lerp(_ArtAmbient.rgb,skyLuma*float3(.94,1,1.06),.8);
                    float3 snowLight=(snowSky*1.9*skyFacing+sun.color*.65*snowDiffuse)*lerp(.76,1,sun.shadowAttenuation);
                    lighting=lerp(lighting,snowLight,snow);
                    baseColor=lerp(baseColor,lerp(natural*.92,_SoilTone.rgb,use.r),use.g*(1-snow)*.55);
                    baseColor=lerp(baseColor,_SoilTone.rgb*(1-_CampClimate.x*.23),use.r*(1-snow)*(.67+ArtNoise(p*2.8)*.23));
                    baseColor=lerp(baseColor,float3(.68,.78,.86),use.b*snow*.42);
                    float churn=ArtNoise(p*7+5)*.6+ArtNoise(p*3)*.4;
                    baseColor*=1-use.a*(.04+churn*.09)*(1-snow);
                    // Small irregular wet depressions emerge only after substantial real traffic.
                    float pool=use.a*smoothstep(.61,.79,ArtNoise(p*2.3+91))*(1-snow);
                    baseColor=lerp(baseColor,float3(.30,.37,.36),pool*.60);
                }
                if(_ArtGround<.5 && _ArtWater<.5 && _WeatherSnowCover>0)
                {
                    float retention=saturate(input.snowCatch);
                    float snow=smoothstep(lerp(.12,.05,retention),lerp(.62,.33,retention),normal.y)*_WeatherSnowCover*saturate(input.color.a)*WeatherSkyExposure(input.restWS,normal);
                    snow*=step(0,dot(normal,GetWorldSpaceNormalizeViewDir(input.positionWS)));
                    baseColor=lerp(baseColor,SettledSnowColor(input.positionWS.xz),snow);
                    float sky=dot(_ArtAmbient.rgb,float3(.2126,.7152,.0722));
                    float3 snowLight=lerp(_ArtAmbient.rgb,sky*float3(.94,1,1.06),.8)*1.9
                        +sun.color*(.35+.30*saturate(dot(normal,sun.direction)));
                    lighting=lerp(lighting,snowLight*lerp(.76,1,sun.shadowAttenuation),snow);
                }
                float3 color=baseColor*lighting;
                if(_ArtGround>.5)
                {
                    float4 use=CampUse(input.positionWS.xz);float2 p=input.positionWS.xz;
                    float pools=use.a*smoothstep(.61,.79,ArtNoise(p*2.3+91))*(1-_CampClimate.y);
                    float spec=pow(saturate(dot(reflect(-sun.direction,normal),GetWorldSpaceNormalizeViewDir(input.positionWS))),22);
                    color+=(sun.color*spec*.24*sun.shadowAttenuation+float3(.07,.10,.11))*pools;
                }
                if(_QCWeather.w>.5 && _ArtWater<.5)
                {
                    float exposed=saturate(input.color.a);
                    float wetness=_QCWeather.z*(_ArtGround>.5?1-saturate(_CampClimate.y):1);
                    color*=1-wetness*.18*exposed;
                    float wetSpec=pow(saturate(dot(reflect(-sun.direction,normal),GetWorldSpaceNormalizeViewDir(input.positionWS))),24);
                    color+=sun.color*wetSpec*wetness*.07*exposed*sun.shadowAttenuation;
                }
                if(_ArtGround>.5)
                {
                    float grainWeight=1-smoothstep(.12,.45,max(length(ddx(input.positionWS.xz)),length(ddy(input.positionWS.xz)))*6);
                    float exposedEarth=1-saturate(_CampClimate.y);
                    color*=lerp(1,.90+ArtNoise(input.positionWS.xz*.43)*.15+ArtNoise(input.positionWS.xz*2.4)*.06,exposedEarth);
                    color*=1+(ArtNoise(input.positionWS.xz*7)-.5)*.06*grainWeight*exposedEarth;
                }
                if(_ArtWater>.5)
                {
                    float wave=sin(input.positionWS.x*4.5+input.positionWS.z*2.1+_Time.y*.5)*sin(input.positionWS.z*7-_Time.y*.3);
                    color*=.85+ArtNoise(input.positionWS.xz*2.7)*.29+wave*.045;
                    float spec=pow(saturate(dot(reflect(-sun.direction,normal),GetWorldSpaceNormalizeViewDir(input.positionWS))),36);
                    color+=sun.color*spec*.12*sun.shadowAttenuation;
                }
                return half4(color,1);
            }
            ENDHLSL
        }
        Pass
        {
            Name "VegetationMask" Tags { "LightMode"="QuietCampVegetationMask" }
            ZWrite Off ZTest LEqual Cull Off ColorMask R
            HLSLPROGRAM
            #pragma vertex VegetationVert
            #pragma fragment VegetationFrag
            struct VegetationAttributes {float4 p:POSITION;float2 response:TEXCOORD1;};
            float4 VegetationVert(VegetationAttributes input):SV_POSITION
            {return TransformWorldToHClip(WeatherPosition(TransformObjectToWorld(input.p.xyz),input.response));}
            half4 VegetationFrag():SV_Target
            {clip(_ArtGrass-.5);return _ArtMeadow>.5?1:.65;}
            ENDHLSL
        }
        Pass
        {
            Name "DepthOnly" Tags { "LightMode"="DepthOnly" }
            ZWrite On ColorMask R Cull Off
            HLSLPROGRAM
            #pragma vertex DepthVert
            #pragma fragment DepthFrag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            struct DepthAttributes {float4 p:POSITION;float2 response:TEXCOORD1;};
            float4 DepthVert(DepthAttributes input):SV_POSITION { return TransformWorldToHClip(WeatherPosition(TransformObjectToWorld(input.p.xyz),input.response)); }
            half4 DepthFrag():SV_Target { return 0; }
            ENDHLSL
        }
        Pass
        {
            Name "ShadowCaster" Tags { "LightMode"="ShadowCaster" }
            ZWrite On ZTest LEqual ColorMask 0 Cull Off
            HLSLPROGRAM
            #pragma vertex ShadowVert
            #pragma fragment ShadowFrag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"
            float3 _LightDirection;
            struct Attributes { float4 positionOS:POSITION; float3 normalOS:NORMAL;float2 response:TEXCOORD1; };
            float4 ShadowVert(Attributes input):SV_POSITION
            {
                float3 position=TransformObjectToWorld(input.positionOS.xyz);
                position=WeatherPosition(position,input.response);
                float3 normal=TransformObjectToWorldNormal(input.normalOS);
                float4 clip=TransformWorldToHClip(ApplyShadowBias(position,normal,_LightDirection));
                #if UNITY_REVERSED_Z
                clip.z=min(clip.z,UNITY_NEAR_CLIP_VALUE*clip.w);
                #else
                clip.z=max(clip.z,UNITY_NEAR_CLIP_VALUE*clip.w);
                #endif
                return clip;
            }
            half4 ShadowFrag():SV_Target { return 0; }
            ENDHLSL
        }
    }
}
