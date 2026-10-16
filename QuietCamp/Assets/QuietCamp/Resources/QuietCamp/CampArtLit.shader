Shader "QuietCamp/CampArtLit"
{
    Properties { _ArtRiverDetail("River-bank detail",Float)=0 _ArtRiverWaterline("River waterline",Float)=0 _CampUse("Observed surface use",2D)="black"{} _GroundLow("Ground low",Color)=(.23,.30,.14,1) _GroundHigh("Ground high",Color)=(.40,.48,.22,1) _SoilTone("Soil",Color)=(.43,.35,.25,1) _ArtAmbient("Ambient", Color) = (.22,.24,.16,1) _ArtWater("Water", Float) = 0 _ArtGrass("Grass", Float) = 0 _ArtGround("Ground", Float) = 0 _QCWeather("Weather time, wind, rain, active",Vector)=(0,0,0,0) _QCWind("Wind direction",Vector)=(.8,.35,0,0) }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry" }
        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "CampSurfaceUse.hlsl"
        float4 _QCWeather,_QCWind;
        float _ArtGround;
        float3 WeatherPosition(float3 p,float2 response)
        {
            float t=_QCWeather.x;
            float gust=.65+.35*sin(t*.7+p.x*.19+p.z*.24);
            float wave=sin(t*1.7+p.x*.54+p.z*.39);
            float weight=response.x*min(response.y,3);
            p.xz+=_QCWind.xy*wave*gust*_QCWeather.y*weight*.22;
            p.y-=abs(wave)*_QCWeather.z*weight*.028;
            if(_ArtGround>.5)
            {
                float mud=CampUse(p.xz).a*(1-_CampClimate.y);
                p.y+=sin(p.x*19+p.z*7)*sin(p.z*13-p.x*11)*mud*.009;
            }
            return CampPressure(p,response.x*response.y);
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
            float4 _ArtAmbient,_GroundLow,_GroundHigh,_SoilTone,_CampFieldSize; float _ArtWater; float _ArtGrass;
            float _ArtRiverDetail, _ArtRiverWaterline;
            CBUFFER_END
            struct Attributes { float4 positionOS:POSITION; float3 normalOS:NORMAL; float4 color:COLOR; float2 response:TEXCOORD1; };
            struct Varyings { float4 positionCS:SV_POSITION; float3 positionWS:TEXCOORD0; float3 normalWS:TEXCOORD1; float2 response:TEXCOORD2; float4 color:COLOR; };
            Varyings Vert(Attributes input)
            {
                Varyings output; output.positionWS=TransformObjectToWorld(input.positionOS.xyz);
                output.positionWS=WeatherPosition(output.positionWS,input.response);
                // WeatherPosition is shared by forward, depth and shadow passes.
                // Do not add a second offset here: it detaches moving blades from their shadows.
                output.positionCS=TransformWorldToHClip(output.positionWS); output.normalWS=TransformObjectToWorldNormal(input.normalOS);
                output.response=input.response; output.color=input.color; return output;
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
                if(_ArtGround>.5)
                {
                    float2 p=input.positionWS.xz;float4 use=CampUse(p);
                    float grain=ArtNoise(p*3.7),dx=ArtNoise((p+float2(.03,0))*3.7)-grain,dz=ArtNoise((p+float2(0,.03))*3.7)-grain;
                    normal=normalize(normal+float3(dx,0,dz)*(.7+use.a*2));
                }
                Light sun=GetMainLight(TransformWorldToShadowCoord(input.positionWS));
                float diffuse=_ArtGrass>.5?abs(dot(normal,sun.direction)):saturate(dot(normal,sun.direction));
                float3 lighting=_ArtAmbient.rgb*(.8+.2*abs(normal.y))+sun.color*((diffuse+.22*saturate(-dot(normal,sun.direction)))*sun.shadowAttenuation);
                // Grass receives the same weather-dependent light as its surroundings.
                // A fixed green light floor erased shadows and glowed through overcast weather.
                if(_ArtGrass>.5)
                {
                    // Thin leaves scatter daylight; retain the preset's light colour in shade.
                    float leafDiffuse=.32+.68*diffuse;
                    lighting=_ArtAmbient.rgb*1.35+sun.color*leafDiffuse*lerp(.16,1,sun.shadowAttenuation);
                }
                float3 baseColor=input.color.rgb;
                if(_ArtRiverDetail>.5)
                {
                    float2 p=input.positionWS.xz;
                    float wet=max(_CampClimate.x*.55,1-smoothstep(_ArtRiverWaterline-.02,_ArtRiverWaterline+.16,input.positionWS.y));
                    baseColor*=1-wet*.22;
                    float upward=smoothstep(.15,.72,normal.y);
                    float aboveWater=smoothstep(_ArtRiverWaterline+.015,_ArtRiverWaterline+.07,input.positionWS.y);
                    float snow=saturate(_CampClimate.y)*upward*aboveWater*smoothstep(.2,.7,ArtNoise(p*3.1+23));
                    baseColor=lerp(baseColor,float3(.83,.89,.92),snow);
                }
                if(_ArtGrass>.5)
                {
                    // Broad, coherent groups and darker roots, rather than noise on every blade.
                    float green=step(baseColor.r*1.05,baseColor.g)*step(baseColor.b*1.1,baseColor.g);
                    float rootToTip=lerp(.80,1.08,sqrt(saturate(input.response.x)));
                    float patchTone=lerp(.91,1.06,ArtNoise(input.positionWS.xz*.24+9));
                    baseColor*=lerp(float3(1,1,1),float3(.94,1,1.08)*rootToTip*patchTone,green);
                    float4 trampled=CampUse(input.positionWS.xz);
                    baseColor=lerp(baseColor,float3(.33,.37,.20),trampled.r*.65);
                }
                if(_ArtGround>.5)
                {
                    float2 p=input.positionWS.xz;float4 use=CampUse(p);
                    // The main variation must survive a phone-sized view; small grain is secondary.
                    float patches=ArtNoise(p*.27+17)*.70+ArtNoise(p*.92+31)*.22+ArtNoise(p*2.4-17)*.08;
                    patches=saturate((patches-.5)*1.7+.5);
                    float earth=smoothstep(.45,.74,ArtNoise(p*.48+61))*lerp(.45,1,ArtNoise(p*1.3));
                    float moss=smoothstep(.40,.73,ArtNoise(p*.32-23))*.42;
                    float3 natural=lerp(_GroundLow.rgb,_GroundHigh.rgb,patches);
                    natural=lerp(natural,_GroundLow.rgb*float3(.76,.94,.88),moss);
                    natural=lerp(natural,_SoilTone.rgb,earth*.72);
                    // Preserve authored scars and banks without retaining a differently painted puzzle rectangle.
                    float fieldDistance=max(abs(p.x)-_CampFieldSize.x,abs(p.y)-_CampFieldSize.y);
                    float undisturbed=1-smoothstep(.3,2.0,fieldDistance+(ArtNoise(p*.8f)-.5)*.45);
                    baseColor=lerp(baseColor,natural,lerp(.72,1,undisturbed));
                    float cover=saturate(_CampClimate.y);
                    float drift=ArtNoise(p*.23+71)*.7+ArtNoise(p*.81-29)*.3;
                    float snow=cover>=.999?1:smoothstep(1-cover-.12,1-cover+.12,drift)*smoothstep(0,.12,cover);
                    snow*=smoothstep(.2,.75,normal.y);
                    baseColor=lerp(baseColor,lerp(float3(.73,.81,.85),float3(.84,.89,.91),ArtNoise(p*.38+21)),snow);
                    baseColor=lerp(baseColor,lerp(natural*.92,_SoilTone.rgb,use.r),use.g*(1-snow)*.55);
                    baseColor=lerp(baseColor,_SoilTone.rgb*(1-_CampClimate.x*.23),use.r*(1-snow)*(.67+ArtNoise(p*2.8)*.23));
                    baseColor=lerp(baseColor,float3(.59,.69,.73),use.b*snow*.72);
                    float churn=ArtNoise(p*7+5)*.6+ArtNoise(p*3)*.4;
                    baseColor*=1-use.a*(.04+churn*.09)*(1-snow);
                    // Small irregular wet depressions emerge only after substantial real traffic.
                    float pool=use.a*smoothstep(.61,.79,ArtNoise(p*2.3+91))*(1-snow);
                    baseColor=lerp(baseColor,float3(.30,.37,.36),pool*.60);
                }
                float3 color=baseColor*lighting;
                if(_ArtGround>.5)
                {
                    float4 use=CampUse(input.positionWS.xz);float2 p=input.positionWS.xz;
                    float pools=use.a*smoothstep(.61,.79,ArtNoise(p*2.3+91))*(1-_CampClimate.y);
                    float spec=pow(saturate(dot(reflect(-sun.direction,normal),GetWorldSpaceNormalizeViewDir(input.positionWS))),22);
                    color+=(sun.color*spec*.24*sun.shadowAttenuation+float3(.07,.10,.11))*pools;
                }
                if(_QCWeather.w>.5 && _ArtWater<.5)
                {
                    float exposed=saturate(input.color.a);
                    color*=1-_QCWeather.z*.18*exposed;
                    float wetSpec=pow(saturate(dot(reflect(-sun.direction,normal),GetWorldSpaceNormalizeViewDir(input.positionWS))),24);
                    color+=sun.color*wetSpec*_QCWeather.z*.07*exposed*sun.shadowAttenuation;
                }
                if(_ArtGround>.5)
                {
                    float grainWeight=1-smoothstep(.12,.45,max(length(ddx(input.positionWS.xz)),length(ddy(input.positionWS.xz)))*6);
                    color*=.90+ArtNoise(input.positionWS.xz*.43)*.15+ArtNoise(input.positionWS.xz*2.4)*.06;
                    color*=1+(ArtNoise(input.positionWS.xz*7)-.5)*.06*grainWeight;
                }
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
