Shader "Hidden/QuietCamp/TentThumbnail"
{
    Properties { _BaseColor ("Authored colour", Color) = (1,1,1,1) }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" }
        Pass
        {
            Tags { "LightMode"="SRPDefaultUnlit" }
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
            half4 _BaseColor;
            CBUFFER_END
            struct Attributes { float4 positionOS:POSITION; float3 normalOS:NORMAL; };
            struct Varyings { float4 positionCS:SV_POSITION; half3 normalWS:TEXCOORD0; };
            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS=TransformObjectToHClip(input.positionOS.xyz);
                output.normalWS=TransformObjectToWorldNormal(input.normalOS);
                return output;
            }
            half4 Frag(Varyings input):SV_Target
            {
                half daylight=saturate(dot(normalize(input.normalWS),normalize(half3(.5,.8,.3))));
                half3 light=half3(.65,.69,.64)+daylight*half3(.48,.43,.34);
                return half4(_BaseColor.rgb*light,1);
            }
            ENDHLSL
        }
    }
}
