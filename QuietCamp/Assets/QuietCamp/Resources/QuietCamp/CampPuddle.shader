Shader "QuietCamp/CampPuddle"
{
    Properties { _Fill("Water amount",Range(0,1))=0 _EnvCube("World reflection",Cube)=""{} _HasReflection("Captured world",Float)=0 _Sky("Sky",Color)=(.5,.65,.7,1) _Horizon("Horizon",Color)=(.8,.8,.7,1) _RippleTime("Ripple time",Float)=0 _Rain("Rain",Float)=0 _Cloud("Cloudiness",Float)=0 _SunColor("Sun",Color)=(1,.9,.7,1) _SunDirection("Sun direction",Vector)=(0,1,0,0) }
    SubShader
    {
        Tags {"RenderPipeline"="UniversalPipeline" "Queue"="Transparent-10" "RenderType"="Transparent"}
        Blend SrcAlpha OneMinusSrcAlpha ZWrite Off Cull Off
        Pass
        {
            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "CozySky.hlsl"
            TEXTURECUBE(_EnvCube);SAMPLER(sampler_EnvCube);
            CBUFFER_START(UnityPerMaterial)
            float _Fill,_HasReflection,_RippleTime,_Rain,_Cloud;
            half4 _Sky,_Horizon,_SunColor;float4 _CubeDecode,_ProbeMin,_ProbeMax,_ProbePosition,_SunDirection;
            CBUFFER_END
            struct A{float4 p:POSITION;float2 uv:TEXCOORD0;float2 hollow:TEXCOORD1;};struct V{float4 p:SV_POSITION;float3 w:TEXCOORD0;float2 uv:TEXCOORD1;float2 hollow:TEXCOORD2;};
            V vert(A a){V v;v.w=TransformObjectToWorld(a.p.xyz);v.p=TransformWorldToHClip(v.w);v.uv=a.uv;v.hollow=a.hollow;return v;}
            half4 frag(V v):SV_Target
            {
                float fill=saturate((_Fill-v.hollow.x)/max(.001,1-v.hollow.x));
                float radius=length(v.uv);float coverage=lerp(.12,.98,sqrt(fill));
                float edge=saturate((coverage-radius)*12)*smoothstep(0,.10,fill);
                float2 wave=float2(sin(v.w.x*18+v.w.z*7-_RippleTime*2.3+v.hollow.y),cos(v.w.z*21-v.w.x*5-_RippleTime*2.1+v.hollow.y));
                float3 normal=normalize(float3(wave.x*.018*_Rain,1,wave.y*.018*_Rain));
                float3 view=GetWorldSpaceNormalizeViewDir(v.w);float3 reflected=reflect(-view,normal);
                half3 reflection=CampSky(reflected,_Sky.rgb,_Horizon.rgb,_Cloud,_SunDirection.xyz,_SunColor.rgb);
                if(_HasReflection>.5)
                {
                    float3 safe=(step(0,reflected)*2-1)*max(abs(reflected),.0001);
                    float3 reach=((reflected>0?_ProbeMax.xyz:_ProbeMin.xyz)-v.w)/safe;
                    float distance=min(reach.x,min(reach.y,reach.z));
                    float3 lookup=v.w+reflected*max(0,distance)-_ProbePosition.xyz;
                    half4 captured=SAMPLE_TEXTURECUBE_LOD(_EnvCube,sampler_EnvCube,lookup,1.1);
                    reflection=DecodeHDREnvironment(captured,_CubeDecode);
                }
                Light sun=GetMainLight(TransformWorldToShadowCoord(v.w));
                float fresnel=.46+.34*pow(1-saturate(dot(view,normal)),3);
                half3 water=lerp(half3(.20,.29,.25)*(SampleSH(normal)+sun.color*.24*sun.shadowAttenuation),reflection,fresnel);
                float glint=pow(saturate(dot(reflected,sun.direction)),64)*sun.shadowAttenuation*.12;
                return half4(water+sun.color*glint,edge*.74);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
