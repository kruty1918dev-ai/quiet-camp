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
            float4 _AirLight,_AirSunColor,_AirSunDirection;
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
                // Integrate a shallow moving layer along the actual view ray, including orthographic cameras.
                #if UNITY_REVERSED_Z
                float nearDepth=1;
                #else
                float nearDepth=UNITY_NEAR_CLIP_VALUE;
                #endif
                float3 origin=ComputeWorldSpacePosition(uv,nearDepth,UNITY_MATRIX_I_VP);
                if(unity_OrthoParams.w<.5)origin=_WorldSpaceCameraPos;
                float3 ray=world-origin;float low=0;int steps=(int)_AirLight.y;
                [loop] for(int s=0;s<steps;s++)
                {
                    float t=(s+.75)/steps;float3 at=origin+ray*t;
                    float layer=exp(-max(0,at.y)*.9);
                    float patch=Noise(at.xz*.10-_Flow.xy*_WeatherVisibility.w*.065);
                    low+=layer*(.25+.75*smoothstep(.2,.8,patch));
                }
                low=low/max(1,steps)*min(28,length(ray))*_FogRange.w*.22;
                float storm=_WeatherVisibility.y;
                float heightFog=low*saturate((distance+_FogRange.x)/max(1,_FogRange.x*1.5));
                float campClear=lerp(lerp(.32,.23,storm),1,smoothstep(4,14,length(world.xz-_FogAxis.zw)));
                float fog=saturate(farMask*_WeatherVisibility.x*.85+heightFog*.55+storm*(.07+farMask*.52+drift*.08));
                float forward=pow(saturate(dot(normalize(ray),_AirSunDirection.xyz)*.5+.5),3);
                float3 litFog=_FogColor.rgb+_AirSunColor.rgb*forward*_AirLight.x*.16;
                litFog=lerp(litFog,float3(.76,.83,.89),storm*.22);
                color.rgb=lerp(color.rgb,litFog,min(.84,fog*campClear));
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
