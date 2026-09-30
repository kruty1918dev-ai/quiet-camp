Shader "QuietCamp/AtmosphereLayer"
{
    Properties
    {
        _MainTex ("Layer", 2D) = "white" {}
        _Tint ("Tint", Color) = (1,1,1,1)
        _ProtectedRect ("Protected viewport bounds", Vector) = (0,0,0,0)
        _Protection ("Protect board", Float) = 0
        _Feather ("Protection feather", Float) = 0.025
        _EdgeOnly ("Only side edges", Float) = 0
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent" }
        Pass
        {
            Tags { "LightMode"="SRPDefaultUnlit" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            ZTest Always
            Cull Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                half4 _Tint;
                float4 _ProtectedRect;
                float _Protection, _Feather, _EdgeOnly;
            CBUFFER_END
            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; float4 screen : TEXCOORD1; };
            Varyings Vert(Attributes v)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                o.screen = ComputeScreenPos(o.positionCS);
                return o;
            }
            half4 Frag(Varyings i) : SV_Target
            {
                half4 color = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv) * _Tint;
                float2 screen = i.screen.xy / i.screen.w;
                // A signed distance to the projected board rectangle. Exactly zero alpha inside.
                float2 d = max(_ProtectedRect.xy - screen, screen - _ProtectedRect.zw);
                float outside = smoothstep(0, max(_Feather, 0.0001), max(d.x, d.y));
                color.a *= lerp(1, outside, _Protection);
                float edge = 1 - smoothstep(0.06, 0.27, min(screen.x, 1 - screen.x));
                // A treeline has an opaque base in the source PNG: dissolve it into the meadow.
                float groundFade = smoothstep(0, 0.35, i.uv.y);
                color.a *= lerp(1, edge * groundFade, _EdgeOnly);
                return color;
            }
            ENDHLSL
        }
    }
}
