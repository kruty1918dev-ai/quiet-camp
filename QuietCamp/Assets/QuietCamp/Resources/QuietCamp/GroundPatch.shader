Shader "QuietCamp/GroundPatch"
{
    Properties { _SoilTone("Worn earth",Color)=(.43,.34,.25,1) }
    SubShader
    {
        Tags {"RenderPipeline"="UniversalPipeline" "Queue"="Transparent-10" "RenderType"="Transparent"}
        Blend SrcAlpha OneMinusSrcAlpha ZWrite Off Cull Back
        Pass
        {
            Tags{"LightMode"="UniversalForward"}
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            CBUFFER_START(UnityPerMaterial)
            half4 _SoilTone;
            CBUFFER_END
            struct A{float4 p:POSITION;float2 uv:TEXCOORD0;};struct V{float4 p:SV_POSITION;float3 w:TEXCOORD0;float2 uv:TEXCOORD1;};
            V vert(A a){V v;v.w=TransformObjectToWorld(a.p.xyz);v.p=TransformWorldToHClip(v.w);v.uv=a.uv;return v;}
            half4 frag(V v):SV_Target
            {
                float grain=sin(v.w.x*11.2+sin(v.w.z*5.3))*sin(v.w.z*13.1)*.12;
                float edge=1-smoothstep(.32,.98,length(v.uv)+grain);
                Light sun=GetMainLight(TransformWorldToShadowCoord(v.w));
                half3 lit=SampleSH(half3(0,1,0))+sun.color*saturate(sun.direction.y)*sun.shadowAttenuation;
                return half4(_SoilTone.rgb*lit,edge*.66);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
