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
            float4 _AtmosWindXZ;
            struct Attributes {float3 vertex:POSITION;float2 uv:TEXCOORD0;float4 seed:TEXCOORD1;half4 color:COLOR;};
            struct Varyings {float4 pos:SV_POSITION;half4 color:COLOR;float z:TEXCOORD0;float2 uv:TEXCOORD1;};
            Varyings vert(Attributes i)
            {
                Varyings o;float t=_AtmosWindTime*.42+i.seed.w;
                float3 p=i.seed.xyz+float3(_AtmosWindXZ.x*sin(t)*.8,cos(t*1.3)*.35,_AtmosWindXZ.y*sin(t)*.8);
                float3 world=TransformObjectToWorld(p);
                float3 right=UNITY_MATRIX_I_V._m00_m10_m20,up=UNITY_MATRIX_I_V._m01_m11_m21;
                float size=max(abs(i.uv.x),abs(i.uv.y));
                // Soft camera-facing specks, animated by the shared wind clock.
                // No CPU particles, texture, additional light or shadow pass.
                world+=(right*i.uv.x+up*i.uv.y)*length(GetObjectToWorldMatrix()._m00_m10_m20);
                o.pos=TransformWorldToHClip(world);o.z=world.z;o.uv=i.uv/max(size,.001);
                o.color=half4(i.color.rgb,saturate(_AtmosWindStrength*5)*i.color.a*(.45+.55*saturate(sin(t))));
                return o;
            }
            half4 frag(Varyings i):SV_TARGET
            {
                i.color.a*=saturate(1-dot(i.uv,i.uv))*(1-smoothstep(_RoadmapRevealZ-3,_RoadmapRevealZ+7,i.z));
                return i.color;
            }
            ENDHLSL
        }
    }
}
