Shader "QuietCamp/CampArtLit"
{
    Properties { _ArtAmbient("Ambient", Color) = (.22,.24,.16,1) _ArtWater("Water", Float) = 0 _ArtGrass("Grass", Float) = 0 _ArtGround("Ground", Float) = 0 _QCWeather("Weather time, wind, rain, active",Vector)=(0,0,0,0) _QCWind("Wind direction",Vector)=(.8,.35,0,0) }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry" }
        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        float4 _QCWeather,_QCWind;
        float3 WeatherPosition(float3 p,float2 response)
        {
            float t=_QCWeather.x;
            float gust=.65+.35*sin(t*.7+p.x*.19+p.z*.24);
            float wave=sin(t*1.7+p.x*.54+p.z*.39);
            float weight=response.x*min(response.y,3);
            p.xz+=_QCWind.xy*wave*gust*_QCWeather.y*weight*.22;
            p.y-=abs(wave)*_QCWeather.z*weight*.028;
            return p;
        }
        ENDHLSL
        Pass
        {
            Name "CampArtForward" Tags { "LightMode"="UniversalForward" }
            Cull Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            CBUFFER_START(UnityPerMaterial)
            float4 _ArtAmbient; float _ArtWater; float _ArtGrass; float _ArtGround;
            CBUFFER_END
            float4 _AtmosWindXZ; float _AtmosWindStrength; float _AtmosWindTime;
            struct Attributes { float4 positionOS:POSITION; float3 normalOS:NORMAL; float4 color:COLOR; float2 response:TEXCOORD1; };
            struct Varyings { float4 positionCS:SV_POSITION; float3 positionWS:TEXCOORD0; float3 normalWS:TEXCOORD1; float4 color:COLOR; };
            Varyings Vert(Attributes input)
            {
                Varyings output; output.positionWS=TransformObjectToWorld(input.positionOS.xyz);
                output.positionWS=WeatherPosition(output.positionWS,input.response);
                if(_ArtGrass>.5)
                {
                    float sway=sin(_AtmosWindTime*1.3+output.positionWS.x*.65+output.positionWS.z*.8);
                    output.positionWS.xz+=_AtmosWindXZ.xy*sway*_AtmosWindStrength*saturate(input.positionOS.y)*.025;
                }
                output.positionCS=TransformWorldToHClip(output.positionWS); output.normalWS=TransformObjectToWorldNormal(input.normalOS);
                output.color=input.color; return output;
            }
            float ArtHash(float2 p) { return frac(sin(dot(p,float2(127.1,311.7)))*43758.5453); }
            float ArtNoise(float2 p)
            {
                float2 i=floor(p), f=frac(p); f=f*f*(3-2*f);
                return lerp(lerp(ArtHash(i),ArtHash(i+float2(1,0)),f.x),lerp(ArtHash(i+float2(0,1)),ArtHash(i+1),f.x),f.y);
            }
            half4 Frag(Varyings input):SV_Target
            {
                float3 normal=normalize(input.normalWS);
                Light sun=GetMainLight(TransformWorldToShadowCoord(input.positionWS));
                float diffuse=_ArtGrass>.5?abs(dot(normal,sun.direction)):saturate(dot(normal,sun.direction));
                float3 lighting=_ArtAmbient.rgb*(.8+.2*abs(normal.y))+sun.color*((diffuse+.22*saturate(-dot(normal,sun.direction)))*sun.shadowAttenuation);
                if(_ArtGrass>.5) lighting=max(lighting,float3(.42,.48,.36));
                float3 color=input.color.rgb*lighting;
                if(_QCWeather.w>.5 && _ArtWater<.5)
                {
                    float exposed=saturate(input.color.a);
                    color*=1-_QCWeather.z*.18*exposed;
                    float wetSpec=pow(saturate(dot(reflect(-sun.direction,normal),GetWorldSpaceNormalizeViewDir(input.positionWS))),24);
                    color+=sun.color*wetSpec*_QCWeather.z*.07*exposed*sun.shadowAttenuation;
                }
                if(_ArtGround>.5) color*=.89+ArtNoise(input.positionWS.xz*.43)*.18+ArtNoise(input.positionWS.xz*2.4)*.07;
                if(_ArtWater>.5)
                {
                    float wave=sin(input.positionWS.x*4.5+input.positionWS.z*2.1+_Time.y*.5)*sin(input.positionWS.z*7-_Time.y*.3);
                    color*=.85+ArtNoise(input.positionWS.xz*2.7)*.29+wave*.045;
                    float spec=pow(saturate(dot(reflect(-sun.direction,normal),GetWorldSpaceNormalizeViewDir(input.positionWS))),36);
                    color+=sun.color*spec*.12*sun.shadowAttenuation;
                }
                return half4(color,1);
            }
            ENDHLSL
        }
        Pass
        {
            Name "DepthOnly" Tags { "LightMode"="DepthOnly" }
            ZWrite On ColorMask R Cull Off
            HLSLPROGRAM
            #pragma vertex DepthVert
            #pragma fragment DepthFrag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            struct DepthAttributes {float4 p:POSITION;float2 response:TEXCOORD1;};
            float4 DepthVert(DepthAttributes input):SV_POSITION { return TransformWorldToHClip(WeatherPosition(TransformObjectToWorld(input.p.xyz),input.response)); }
            half4 DepthFrag():SV_Target { return 0; }
            ENDHLSL
        }
        Pass
        {
            Name "ShadowCaster" Tags { "LightMode"="ShadowCaster" }
            ZWrite On ZTest LEqual ColorMask 0 Cull Off
            HLSLPROGRAM
            #pragma vertex ShadowVert
            #pragma fragment ShadowFrag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"
            float3 _LightDirection;
            struct Attributes { float4 positionOS:POSITION; float3 normalOS:NORMAL;float2 response:TEXCOORD1; };
            float4 ShadowVert(Attributes input):SV_POSITION
            {
                float3 position=TransformObjectToWorld(input.positionOS.xyz);
                position=WeatherPosition(position,input.response);
                float3 normal=TransformObjectToWorldNormal(input.normalOS);
                float4 clip=TransformWorldToHClip(ApplyShadowBias(position,normal,_LightDirection));
                #if UNITY_REVERSED_Z
                clip.z=min(clip.z,UNITY_NEAR_CLIP_VALUE*clip.w);
                #else
                clip.z=max(clip.z,UNITY_NEAR_CLIP_VALUE*clip.w);
                #endif
                return clip;
            }
            half4 ShadowFrag():SV_Target { return 0; }
            ENDHLSL
        }
    }
}
