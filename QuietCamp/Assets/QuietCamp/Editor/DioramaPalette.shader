Shader "Hidden/QuietCamp/DioramaPalette"
{
    // Offline source photography: authored RGB + the same facet light as tent thumbnails.
    Properties { _Tint ("Season tint", Color) = (1,1,1,1) }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" }
        Pass
        {
            Tags { "LightMode"="SRPDefaultUnlit" }
            Cull Back
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
            half4 _Tint;
            CBUFFER_END
            struct Attributes {float4 positionOS:POSITION;float3 normalOS:NORMAL;half4 color:COLOR;};
            struct Varyings {float4 positionCS:SV_POSITION;half3 normalWS:TEXCOORD0;half4 color:COLOR;};
            Varyings Vert(Attributes i)
            {
                Varyings o;o.positionCS=TransformObjectToHClip(i.positionOS.xyz);
                o.normalWS=TransformObjectToWorldNormal(i.normalOS);o.color=i.color;return o;
            }
            half4 Frag(Varyings i):SV_Target
            {
                half daylight=saturate(dot(normalize(i.normalWS),normalize(half3(.5,.8,.3))));
                half3 light=half3(.65,.69,.64)+daylight*half3(.48,.43,.34);
                return half4(i.color.rgb*_Tint.rgb*light,1);
            }
            ENDHLSL
        }
    }
}
