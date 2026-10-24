Shader "QuietCamp/CampArtLit"
{
    Properties { _MeadowTint("Seasonal meadow tint",Vector)=(1,1,1,1) _MobileMeadow("Dense meadow",Float)=0 _MeadowDetailMap("Dense meadow detail",2D)="gray"{} _WeatherSnowCover("Weather snow cover",Float)=0 _WeatherSkyHeight("Precipitation shelter",2D)="black"{} _WeatherSkyBounds("Shelter bounds",Vector)=(0,0,0,0) _ArtSnowSurface("Settled snow geometry",Float)=0 _MeadowExtent("Meadow extent",Vector)=(0,0,3,0) _ArtMeadow("Continuous meadow",Float)=0 _GroundFinish("Natural ground finish",Vector)=(.34,1.1,.12,0) _ArtRiverDetail("River-bank detail",Float)=0 _ArtRiverWaterline("River waterline",Float)=0 _CampUse("Observed surface use",2D)="black"{} _GroundLow("Ground low",Color)=(.23,.30,.14,1) _GroundHigh("Ground high",Color)=(.40,.48,.22,1) _SoilTone("Soil",Color)=(.43,.35,.25,1) _ArtAmbient("Ambient", Color) = (.22,.24,.16,1) _ArtWater("Water", Float) = 0 _ArtGrass("Grass", Float) = 0 _ArtGround("Ground", Float) = 0 _QCWeather("Weather time, wind, rain, active",Vector)=(0,0,0,0) _QCWind("Wind direction",Vector)=(.8,.35,0,0) }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry" }
        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        // Identical material layout in colour, depth, shadows and vegetation mask.
        #include "CampArtWeather.hlsl"
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
            #include "CampArtSurface.hlsl"
            half4 Frag(Varyings input):SV_Target { return CampArtColor(input); }
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
