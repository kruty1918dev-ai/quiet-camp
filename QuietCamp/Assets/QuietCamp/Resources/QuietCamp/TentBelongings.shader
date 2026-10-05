Shader "QuietCamp/TentBelongings"
{
    Properties
    {
        _BaseColor("Tint",Color)=(1,1,1,1)
        _ShelterGlow("Lantern warmth",Range(0,1))=0
        _LifeMotion("Quiet lantern movement",Range(0,1))=1
    }
    SubShader
    {
        Tags{"RenderPipeline"="UniversalPipeline" "RenderType"="Opaque"}
        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
        CBUFFER_START(UnityPerMaterial)
        half4 _BaseColor;half _ShelterGlow,_LifeMotion;
        CBUFFER_END
        ENDHLSL
        Pass
        {
            Name "Belongings" Tags{"LightMode"="UniversalForward"}
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            struct A{float4 p:POSITION;float3 n:NORMAL;half4 color:COLOR;};
            struct V{float4 p:SV_POSITION;float3 w:TEXCOORD0;half3 n:TEXCOORD1;half3 color:TEXCOORD2;half2 life:TEXCOORD3;};
            V vert(A a){V v;v.w=TransformObjectToWorld(a.p.xyz);v.p=TransformWorldToHClip(v.w);v.n=TransformObjectToWorldNormal(a.n);v.color=a.color.rgb*_BaseColor.rgb;v.life=half2(step(1.1,a.color.r),a.color.a);return v;}
            half4 frag(V v):SV_Target
            {
                half3 n=normalize(v.n);Light sun=GetMainLight(TransformWorldToShadowCoord(v.w));
                half3 light=SampleSH(n)+sun.color*saturate(dot(n,sun.direction))*sun.shadowAttenuation;
                // One emissive part in the combined mesh; no lamp GameObject,
                // realtime point light, particle system or update loop.
                half pulse=1+.035*sin(_Time.y*.83+dot(TransformObjectToWorld(float3(0,0,0)).xz,float2(.4,.7)))*_LifeMotion;
                half3 lamp=half3(1,.61,.22)*v.life.x*_ShelterGlow*1.4*pulse;
                half3 shelteredFill=half3(1,.72,.44)*v.life.y*_ShelterGlow*.18*pulse;
                return half4(v.color*(light+shelteredFill)+lamp,1);
            }
            ENDHLSL
        }
        Pass
        {
            Name "ShadowCaster" Tags{"LightMode"="ShadowCaster"} ZWrite On ZTest LEqual ColorMask 0
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex vert
            #pragma fragment frag
            float3 _LightDirection;
            struct A{float4 p:POSITION;float3 n:NORMAL;};
            float4 vert(A a):SV_POSITION
            {
                float3 w=TransformObjectToWorld(a.p.xyz);float4 p=TransformWorldToHClip(ApplyShadowBias(w,TransformObjectToWorldNormal(a.n),_LightDirection));
                #if UNITY_REVERSED_Z
                p.z=min(p.z,UNITY_NEAR_CLIP_VALUE*p.w);
                #else
                p.z=max(p.z,UNITY_NEAR_CLIP_VALUE*p.w);
                #endif
                return p;
            }
            half4 frag():SV_Target{return 0;}
            ENDHLSL
        }
        Pass
        {
            Name "DepthOnly" Tags{"LightMode"="DepthOnly"} ZWrite On ColorMask R
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex vert
            #pragma fragment frag
            float4 vert(float4 p:POSITION):SV_POSITION{return TransformObjectToHClip(p.xyz);}
            half4 frag():SV_Target{return 0;}
            ENDHLSL
        }
    }
}
