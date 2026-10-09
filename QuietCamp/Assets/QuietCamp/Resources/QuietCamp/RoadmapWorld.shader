Shader "QuietCamp/RoadmapWorld"
{
    Properties { _SceneFog("Local reveal",Float)=0 _WorldMotion("Wind",Float)=1 _WorldTime("Wind time",Float)=0 }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry" }
        Cull Back
        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
        CBUFFER_START(UnityPerMaterial)
        float _WorldMotion, _WorldTime, _SceneFog;
        float4 _WorldSun, _WorldSunColor, _WorldAmbient, _RevealFront, _RevealFog;
        CBUFFER_END
        struct Attributes { float4 positionOS:POSITION;float3 normalOS:NORMAL;half4 color:COLOR;float2 wind:TEXCOORD1; };
        struct Varyings { float4 positionCS:SV_POSITION;float3 positionWS:TEXCOORD0;half3 normalWS:TEXCOORD1;half4 color:COLOR; };
        float3 Bend(float3 p,float2 wind)
        {
            float sway=(sin(_WorldTime*.83+p.x*.24+p.z*.15)+.3*sin(_WorldTime*1.53+p.z*.23))*max(0,wind.x)*.035*_WorldMotion;
            p.x+=sway;p.z+=sway*.32;return p;
        }
        Varyings Vert(Attributes input)
        {
            Varyings o;float3 p=Bend(input.positionOS.xyz,input.wind);
            o.positionWS=TransformObjectToWorld(p);o.positionCS=TransformWorldToHClip(o.positionWS);
            o.normalWS=TransformObjectToWorldNormal(input.normalOS);o.color=input.color;return o;
        }
        ENDHLSL
        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma target 3.0
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            half4 Frag(Varyings input):SV_Target
            {
                Light main=GetMainLight(TransformWorldToShadowCoord(input.positionWS));
                half diffuse=saturate(dot(normalize(input.normalWS),normalize(_WorldSun.xyz)));
                half3 lit=input.color.rgb*(_WorldAmbient.rgb*.70+_WorldSunColor.rgb*diffuse*main.shadowAttenuation*.72);
                float lateral=abs(input.positionWS.x-_WorldSpaceCameraPos.x);
                float haze=smoothstep(12,27,lateral)*.48;
                // Progress horizon in world space, independent of scroll/camera position.
                float distance=-input.positionWS.z*_RevealFront.z;
                float depth=smoothstep(_RevealFront.x,_RevealFront.y,distance);
                // Ground mist and high-canopy silhouettes provide real height-dependent depth.
                float lowMist=depth*(1-saturate(input.positionWS.y/5))*.13;
                float fog=saturate(depth+lowMist);
                fog=max(max(fog,1-input.color.a),_SceneFog);
                if(input.color.a>1.05)fog=min(.93,fog); // Explicit distant landmark: low-contrast silhouette only.
                half luma=dot(lit,half3(.2126,.7152,.0722));
                lit=lerp(lit,luma.xxx,fog*.55);
                half variation=.985+.015*sin(input.positionWS.x*.18+input.positionWS.z*.07+_WorldTime*.025*_WorldMotion);
                lit=lerp(lit,_RevealFog.rgb*variation,max(fog,haze));
                return half4(lit,1);
            }
            ENDHLSL
        }
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode"="ShadowCaster" }
            ZWrite On ZTest LEqual ColorMask 0
            HLSLPROGRAM
            #pragma vertex ShadowVert
            #pragma fragment ShadowFrag
            #pragma target 3.0
            float3 _LightDirection;
            struct ShadowVaryings {float4 positionCS:SV_POSITION;float casts:TEXCOORD0;};
            ShadowVaryings ShadowVert(Attributes input)
            {
                float3 p=TransformObjectToWorld(Bend(input.positionOS.xyz,input.wind));
                float3 n=TransformObjectToWorldNormal(input.normalOS);
                float4 clip=TransformWorldToHClip(ApplyShadowBias(p,n,_LightDirection));
                #if UNITY_REVERSED_Z
                clip.z=min(clip.z,UNITY_NEAR_CLIP_VALUE*clip.w);
                #else
                clip.z=max(clip.z,UNITY_NEAR_CLIP_VALUE*clip.w);
                #endif
                float distance=-p.z*_RevealFront.z;
                ShadowVaryings o;o.positionCS=clip;o.casts=input.wind.x>=0&&input.color.a<=1.05&&distance<_RevealFront.y?1:0;return o;
            }
            half4 ShadowFrag(ShadowVaryings input):SV_Target { clip(input.casts-.5);clip(.25-_SceneFog);return 0; }
            ENDHLSL
        }
    }
}
