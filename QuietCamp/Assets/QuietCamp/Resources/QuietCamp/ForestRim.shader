Shader "QuietCamp/ForestRim"
{
    Properties { _FogColor("Forest air",Color)=(.7,.8,.65,.14) }
    SubShader
    {
        Tags {"RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent"}
        Blend SrcAlpha OneMinusSrcAlpha ZWrite Off Cull Off
        Pass
        {
            HLSLPROGRAM
            #pragma target 2.0
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
            half4 _FogColor;
            CBUFFER_END
            struct A{float4 p:POSITION;half4 color:COLOR;};
            struct V{float4 p:SV_POSITION;half alpha:TEXCOORD0;};
            V vert(A a){V v;v.p=TransformObjectToHClip(a.p.xyz);v.alpha=a.color.a;return v;}
            half4 frag(V v):SV_Target{return half4(_FogColor.rgb,_FogColor.a*v.alpha);}
            ENDHLSL
        }
    }
    Fallback Off
}
