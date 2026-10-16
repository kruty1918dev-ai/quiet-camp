Shader "Hidden/QuietCamp/WorldOpsMask"
{
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            Cull Off ZWrite On ZTest LEqual Offset -1,-1
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            float _WorldOpsId;
            struct Varyings { float4 positionCS:SV_POSITION; float eye:TEXCOORD0; };
            Varyings Vert(float4 p:POSITION)
            {
                Varyings o;float3 world=TransformObjectToWorld(p.xyz);o.positionCS=TransformWorldToHClip(world);
                o.eye=-TransformWorldToView(world).z;return o;
            }
            float4 Frag(Varyings i):SV_Target
            {
                float depth=floor(saturate(i.eye/512.0)*65535.0);
                return float4(_WorldOpsId,floor(depth/256.0)/255.0,fmod(depth,256.0)/255.0,1);
            }
            ENDHLSL
        }
    }
}
