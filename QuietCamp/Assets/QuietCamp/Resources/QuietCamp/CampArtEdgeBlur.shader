Shader "QuietCamp/CampArtEdgeBlur"
{
    Properties { _BlurPixels("Blur pixels", Float)=3 _VegetationBlurPixels("Vegetation softness",Float)=2.2 _SharpRect("Sharp board", Vector)=(.2,.2,.8,.8) }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            ZWrite Off ZTest Always Cull Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma target 3.5
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
            TEXTURE2D_X(_CampVegetationMask);
            TEXTURE2D_X_FLOAT(_CampSoftDepth);
            float _BlurPixels,_VegetationBlurPixels; float4 _SharpRect;
            float EyeDepth(float2 uv)
            {
                float raw=SAMPLE_TEXTURE2D_X(_CampSoftDepth,sampler_PointClamp,uv).r;
                return unity_OrthoParams.w>.5?LinearDepthToEyeDepth(raw):LinearEyeDepth(raw,_ZBufferParams);
            }
            half4 Frag(Varyings input):SV_Target
            {
                float2 uv=input.texcoord;
                float outside=max(max(_SharpRect.x-uv.x,uv.x-_SharpRect.z),max(_SharpRect.y-uv.y,uv.y-_SharpRect.w));
                float edge=smoothstep(.28,.49,abs(uv.x-.5))*smoothstep(0,.035,outside);
                float vegetation=SAMPLE_TEXTURE2D_X(_CampVegetationMask,sampler_LinearClamp,uv).r;
                float eye=EyeDepth(uv);
                float tolerance=clamp(max(abs(ddx(eye)),abs(ddy(eye)))*2+.12,.12,.35);
                float2 offsets[4]={float2(1,0),float2(-1,0),float2(0,1),float2(0,-1)};
                // Fill tiny soil gaps between moving blades, without spreading across
                // the depth break onto a tent or another foreground solid object.
                [unroll]for(int i=0;i<4;i++)
                {
                    float2 sampleUV=uv+offsets[i]*_VegetationBlurPixels/_ScreenParams.xy;
                    float compatible=1-smoothstep(tolerance,tolerance*1.5,abs(EyeDepth(sampleUV)-eye));
                    vegetation=max(vegetation,SAMPLE_TEXTURE2D_X(_CampVegetationMask,sampler_LinearClamp,sampleUV).r*compatible);
                }
                float radius=max(_BlurPixels*edge,_VegetationBlurPixels*vegetation);
                if(radius<.01)return SAMPLE_TEXTURE2D_X(_BlitTexture,sampler_LinearClamp,uv);
                float2 d=radius/_ScreenParams.xy;
                if(_BlurPixels*edge>_VegetationBlurPixels*vegetation)
                {
                    // Retain the existing diorama edge filter.
                    half4 soft=SAMPLE_TEXTURE2D_X(_BlitTexture,sampler_LinearClamp,uv)*.25;
                    soft+=(SAMPLE_TEXTURE2D_X(_BlitTexture,sampler_LinearClamp,uv+float2(d.x,0))+SAMPLE_TEXTURE2D_X(_BlitTexture,sampler_LinearClamp,uv-float2(d.x,0)))*.125;
                    soft+=(SAMPLE_TEXTURE2D_X(_BlitTexture,sampler_LinearClamp,uv+float2(0,d.y))+SAMPLE_TEXTURE2D_X(_BlitTexture,sampler_LinearClamp,uv-float2(0,d.y)))*.125;
                    soft+=(SAMPLE_TEXTURE2D_X(_BlitTexture,sampler_LinearClamp,uv+d)+SAMPLE_TEXTURE2D_X(_BlitTexture,sampler_LinearClamp,uv-d)
                        +SAMPLE_TEXTURE2D_X(_BlitTexture,sampler_LinearClamp,uv+float2(d.x,-d.y))+SAMPLE_TEXTURE2D_X(_BlitTexture,sampler_LinearClamp,uv+float2(-d.x,d.y)))*.0625;
                    return soft;
                }
                half4 centre=SAMPLE_TEXTURE2D_X(_BlitTexture,sampler_LinearClamp,uv),color=centre*.4;
                [unroll]for(int j=0;j<4;j++)
                {
                    float2 sampleUV=uv+offsets[j]*d;
                    float compatible=edge>.01?1:1-smoothstep(tolerance,tolerance*1.5,abs(EyeDepth(sampleUV)-eye));
                    color+=lerp(centre,SAMPLE_TEXTURE2D_X(_BlitTexture,sampler_LinearClamp,sampleUV),compatible)*.15;
                }
                return color;
            }
            ENDHLSL
        }
    }
}
