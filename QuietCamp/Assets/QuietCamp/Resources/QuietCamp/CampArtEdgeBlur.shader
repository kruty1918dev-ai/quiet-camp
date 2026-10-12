Shader "QuietCamp/CampArtEdgeBlur"
{
    Properties { _BlurPixels("Blur pixels", Float)=3 _SharpRect("Sharp board", Vector)=(.2,.2,.8,.8) }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            ZWrite Off ZTest Always Cull Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
            float _BlurPixels; float4 _SharpRect;
            half4 Frag(Varyings input):SV_Target
            {
                float2 uv=input.texcoord;
                float inside=step(_SharpRect.x,uv.x)*step(_SharpRect.y,uv.y)*step(uv.x,_SharpRect.z)*step(uv.y,_SharpRect.w);
                float edge=smoothstep(.30,.48,abs(uv.x-.5));
                edge=max(edge,smoothstep(.32,.49,abs(uv.y-.5))*.65)*(1-inside);
                float2 d=_BlurPixels*edge/_ScreenParams.xy;
                half4 color=SAMPLE_TEXTURE2D_X(_BlitTexture,sampler_LinearClamp,uv)*.25;
                color+=(SAMPLE_TEXTURE2D_X(_BlitTexture,sampler_LinearClamp,uv+float2(d.x,0))+SAMPLE_TEXTURE2D_X(_BlitTexture,sampler_LinearClamp,uv-float2(d.x,0)))*.125;
                color+=(SAMPLE_TEXTURE2D_X(_BlitTexture,sampler_LinearClamp,uv+float2(0,d.y))+SAMPLE_TEXTURE2D_X(_BlitTexture,sampler_LinearClamp,uv-float2(0,d.y)))*.125;
                color+=(SAMPLE_TEXTURE2D_X(_BlitTexture,sampler_LinearClamp,uv+d)+SAMPLE_TEXTURE2D_X(_BlitTexture,sampler_LinearClamp,uv-d)
                    +SAMPLE_TEXTURE2D_X(_BlitTexture,sampler_LinearClamp,uv+float2(d.x,-d.y))+SAMPLE_TEXTURE2D_X(_BlitTexture,sampler_LinearClamp,uv+float2(-d.x,d.y)))*.0625;
                return color;
            }
            ENDHLSL
        }
    }
}
