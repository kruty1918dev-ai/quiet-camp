Shader "QuietCamp/MobileMeadowCards"
{
    Properties
    {
        _MobileMeadow("Dense meadow",Float)=1
        _MeadowDetailMap("Ground detail",2D)="gray"{}
        _MeadowCardMap("Clump silhouette",2D)="white"{}
        _CampUse("Observed surface use",2D)="black"{}
        _ArtAmbient("Ambient",Color)=(.22,.24,.16,1)
        _ArtGrass("Grass",Float)=1
        _QCWeather("Weather time, wind, rain, active",Vector)=(0,0,0,0)
        _QCWind("Wind direction",Vector)=(.8,.35,0,0)
    }
    SubShader
    {
        Tags {"RenderPipeline"="UniversalPipeline" "RenderType"="TransparentCutout" "Queue"="AlphaTest"}
        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "CampArtWeather.hlsl"
        TEXTURE2D(_MeadowCardMap); SAMPLER(sampler_MeadowCardMap);
        struct Attributes {float4 positionOS:POSITION;float2 uv:TEXCOORD0;float4 color:COLOR;};
        struct Varyings {float4 positionCS:SV_POSITION;float3 positionWS:TEXCOORD0;float2 uv:TEXCOORD1;float4 color:COLOR;};
        Varyings CardVert(Attributes input)
        {
            Varyings output;output.positionWS=WeatherPosition(TransformObjectToWorld(input.positionOS.xyz),float2(input.uv.y,1));
            output.positionCS=TransformWorldToHClip(output.positionWS);output.uv=input.uv;output.color=input.color;return output;
        }
        float4 CardSample(float2 uv)
        {float4 card=SAMPLE_TEXTURE2D(_MeadowCardMap,sampler_MeadowCardMap,uv);clip(card.a-.4);return card;}
        ENDHLSL
        Pass
        {
            Name "ForwardLit" Tags {"LightMode"="UniversalForward"}
            Cull Off ZWrite On Blend Off
            HLSLPROGRAM
            #pragma vertex CardVert
            #pragma fragment CardFrag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            half4 CardFrag(Varyings input):SV_Target
            {
                float4 card=CardSample(input.uv);float4 use=CampUse(input.positionWS.xz);
                // Pressure and snow remove the raised silhouette; the opaque ground remains dense.
                clip(1-use.g*.9-_CampClimate.y-input.uv.y*.15);
                Light sun=GetMainLight(TransformWorldToShadowCoord(input.positionWS));
                float3 light=_ArtAmbient.rgb*1.35+sun.color*(.32+.68*saturate(sun.direction.y))*lerp(.16,1,sun.shadowAttenuation);
                float3 tint=input.color.rgb*(.78+card.g*.38)*card.r;
                tint=lerp(tint,float3(.33,.37,.20),use.r*.65);
                return half4(tint*light*(1-_QCWeather.z*.12),1);
            }
            ENDHLSL
        }
        Pass
        {
            Name "DepthOnly" Tags {"LightMode"="DepthOnly"}
            Cull Off ZWrite On ColorMask R
            HLSLPROGRAM
            #pragma vertex CardVert
            #pragma fragment CardDepth
            half4 CardDepth(Varyings input):SV_Target
            {CardSample(input.uv);float4 use=CampUse(input.positionWS.xz);clip(1-use.g*.9-_CampClimate.y-input.uv.y*.15);return 0;}
            ENDHLSL
        }
    }
}
