Shader "QuietCamp/PlacementGrid"
{
    Properties{_GridVisible("Visible",Float)=0 _GridColor("Grid",Color)=(.88,.89,.72,.20) _GridOffset("Origin",Vector)=(0,0,0,0)}
    SubShader
    {
        Tags{"RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent"}
        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            float _GridVisible;float4 _GridColor,_GridOffset;
            struct A{float4 positionOS:POSITION;};struct V{float4 positionCS:SV_POSITION;float3 world:TEXCOORD0;};
            V Vert(A i){V o;o.world=TransformObjectToWorld(i.positionOS.xyz);o.positionCS=TransformWorldToHClip(o.world);return o;}
            half4 Frag(V i):SV_Target
            {
                float2 edge=min(frac(i.world.xz+_GridOffset.xy),1-frac(i.world.xz+_GridOffset.xy));
                float2 aa=max(fwidth(i.world.xz),.001);
                float gridLine=1-min(smoothstep(.009,.009+aa.x,edge.x),smoothstep(.009,.009+aa.y,edge.y));
                return half4(_GridColor.rgb,_GridColor.a*gridLine*_GridVisible);
            }
            ENDHLSL
        }
    }
}
