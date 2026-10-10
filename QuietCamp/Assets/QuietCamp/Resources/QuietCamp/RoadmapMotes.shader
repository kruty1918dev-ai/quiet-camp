Shader "QuietCamp/RoadmapMotes"
{
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off
        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            float _AtmosWindTime, _AtmosWindStrength, _RoadmapRevealZ;
            struct Attributes {float3 vertex:POSITION;float4 seed:TEXCOORD1;half4 color:COLOR;};
            struct Varyings {float4 pos:SV_POSITION;half4 color:COLOR;float z:TEXCOORD0;};
            Varyings vert(Attributes i)
            {
                Varyings o;float t=_AtmosWindTime*.24+i.seed.w;
                float3 p=i.vertex+float3(sin(t)*.8,cos(t*1.3)*.35,sin(t*.7)*.6);
                float3 world=TransformObjectToWorld(p);o.pos=TransformWorldToHClip(world);o.z=world.z;
                o.color=half4(i.color.rgb,saturate(_AtmosWindStrength*5)*(.12+.18*pow(saturate(sin(t)),2)));
                return o;
            }
            half4 frag(Varyings i):SV_TARGET
            {
                i.color.a*=1-smoothstep(_RoadmapRevealZ-3,_RoadmapRevealZ+7,i.z);
                return i.color;
            }
            ENDHLSL
        }
    }
}
