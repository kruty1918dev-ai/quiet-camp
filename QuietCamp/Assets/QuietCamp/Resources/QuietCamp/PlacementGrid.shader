Shader "QuietCamp/PlacementGrid"
{
    Properties{_GridVisible("Visible",Float)=0 _GridColor("Grid",Color)=(.94,.93,.82,.17) _GridOffset("Origin",Vector)=(0,0,0,0) _PlacementFocus("Held tent",Vector)=(0,0,3.2,0)}
    SubShader
    {
        Tags{"RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent"}
        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off
            ZTest Always
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            float _GridVisible;float4 _GridColor,_GridOffset,_PlacementFocus;
            struct A{float4 positionOS:POSITION;};struct V{float4 positionCS:SV_POSITION;float3 world:TEXCOORD0;};
            V Vert(A i){V o;o.world=TransformObjectToWorld(i.positionOS.xyz);o.positionCS=TransformWorldToHClip(o.world);return o;}
            half4 Frag(V i):SV_Target
            {
                float2 edge=min(frac(i.world.xz+_GridOffset.xy),1-frac(i.world.xz+_GridOffset.xy));
                float2 aa=max(fwidth(i.world.xz),.001);
                float gridLine=1-min(smoothstep(.009,.009+aa.x,edge.x),smoothstep(.009,.009+aa.y,edge.y));
                // A quiet dark rim keeps the same thin grid readable on snow and pale soil.
                // Only lines are drawn; the natural ground remains continuous between cells.
                float rim=1-min(smoothstep(.024,.024+aa.x,edge.x),smoothstep(.024,.024+aa.y,edge.y));
                float coreAlpha=_GridColor.a*gridLine;
                float rimAlpha=max(0,rim-gridLine)*_GridColor.a*.65;
                float alpha=coreAlpha+rimAlpha;
                float3 color=(_GridColor.rgb*coreAlpha+float3(.10,.16,.13)*rimAlpha)/max(.0001,alpha);
                // A local guide, fading into the scene instead of covering the whole camp.
                float2 fromTent=abs(i.world.xz-_PlacementFocus.xy);
                float outsideTent=smoothstep(1.02,1.12,max(fromTent.x,fromTent.y));
                float focus=(1-smoothstep(1.7,_PlacementFocus.z,length(fromTent)))*outsideTent;
                return half4(color,alpha*_GridVisible*lerp(1,focus,_PlacementFocus.w));
            }
            ENDHLSL
        }
    }
}
