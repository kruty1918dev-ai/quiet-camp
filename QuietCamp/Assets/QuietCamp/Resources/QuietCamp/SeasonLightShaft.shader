Shader "QuietCamp/SeasonLightShaft"
{
    SubShader
    {
        Tags {"RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent"}
        Pass
        {
            Blend SrcAlpha One ZWrite Off ZTest LEqual Cull Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            float4 _RayColor,_RayFlow,_ClearHalfSize;float _RayOpacity;
            struct Varyings {float4 positionCS:SV_POSITION;float2 uv:TEXCOORD0;float3 world:TEXCOORD1;float4 projected:TEXCOORD2;};
            Varyings Vert(float3 p:POSITION,float2 uv:TEXCOORD0)
            {Varyings o;o.world=TransformObjectToWorld(p);o.positionCS=TransformWorldToHClip(o.world);o.projected=ComputeScreenPos(o.positionCS);o.uv=uv;return o;}
            half4 Frag(Varyings i):SV_Target
            {
                float2 screen=i.projected.xy/i.projected.w;
                float depth=SampleSceneDepth(screen);
                #if !UNITY_REVERSED_Z
                depth=lerp(UNITY_NEAR_CLIP_VALUE,1,depth);
                #endif
                float3 opaque=ComputeWorldSpacePosition(screen,depth,UNITY_MATRIX_I_VP);
                float3 viewRay=normalize(i.world-_WorldSpaceCameraPos);
                float soft=saturate(dot(opaque-i.world,viewRay)/.7);
                float edge=pow(saturate(1-abs(i.uv.x-.5)*2),2);
                float ends=smoothstep(0,.15,i.uv.y)*(1-smoothstep(.72,1,i.uv.y));
                float flow=.82+.18*sin(i.world.x*1.7+i.world.z*.6-_RayFlow.z*.25);
                float outside=max(abs(i.world.x)-_ClearHalfSize.x,abs(i.world.z)-_ClearHalfSize.y);
                float protect=smoothstep(0,1.3,outside);
                return half4(_RayColor.rgb,_RayOpacity*edge*ends*flow*soft*protect);
            }
            ENDHLSL
        }
    }
}
