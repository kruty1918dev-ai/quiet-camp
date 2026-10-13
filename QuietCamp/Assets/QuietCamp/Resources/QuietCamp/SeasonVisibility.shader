Shader "QuietCamp/SeasonVisibility"
{
    SubShader
    {
        Tags{"RenderPipeline"="UniversalPipeline"}
        Pass
        {
            ZTest Always ZWrite Off Cull Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
            float4 _FogAxis,_WeatherVisibility,_FogRange,_Flow;
            float4 _FogColor;
            float Hash(float2 p){return frac(sin(dot(p,float2(127.1,311.7)))*43758.5453);}
            float Noise(float2 p){float2 i=floor(p),f=frac(p);f=f*f*(3-2*f);return lerp(lerp(Hash(i),Hash(i+float2(1,0)),f.x),lerp(Hash(i+float2(0,1)),Hash(i+1),f.x),f.y);}
            half4 Frag(Varyings i):SV_Target
            {
                float2 uv=i.texcoord;half4 color=SAMPLE_TEXTURE2D_X(_BlitTexture,sampler_LinearClamp,uv);
                float depth=SampleSceneDepth(uv);
                #if !UNITY_REVERSED_Z
                depth=lerp(UNITY_NEAR_CLIP_VALUE,1,depth);
                #endif
                float3 world=ComputeWorldSpacePosition(uv,depth,UNITY_MATRIX_I_VP);
                float distance=dot(world.xz-_FogAxis.zw,_FogAxis.xy);
                float farMask=smoothstep(_FogRange.y,_FogRange.z,distance);
                float drift=Noise(world.xz*.13-_Flow.xy*_WeatherVisibility.w*.07);
                float low=exp(-max(0,world.y)*.65)*_FogRange.w*(.55+.45*drift);
                float storm=_WeatherVisibility.y;
                float heightFog=low*saturate((distance+_FogRange.x)/max(1,_FogRange.x*1.5));
                float campClear=lerp(.48,1,smoothstep(4,12,length(world.xz)));
                float fog=saturate(farMask*_WeatherVisibility.x*.85+heightFog*.55+storm*(.07+farMask*.52+drift*.08));
                color.rgb=lerp(color.rgb,_FogColor.rgb,min(.84,fog*campClear));
                // Brief, sparse lens droplets at the outer view; centre stays clear.
                float2 grid=uv*float2(19,13);float2 cell=floor(grid);float random=Hash(cell);
                float phase=frac(_WeatherVisibility.w*.19+random);float2 dropPoint=frac(grid)-float2(random,frac(random*9));
                dropPoint.y+=phase*.45;float droplet=(1-smoothstep(.018,.08,length(dropPoint)))*step(.982,random)*smoothstep(.08,.22,phase)*(1-smoothstep(.5,.75,phase));
                float edge=smoothstep(.22,.42,abs(uv.x-.5));
                color.rgb=lerp(color.rgb,_FogColor.rgb,droplet*edge*_Flow.z*.3);
                return color;
            }
            ENDHLSL
        }
    }
}
