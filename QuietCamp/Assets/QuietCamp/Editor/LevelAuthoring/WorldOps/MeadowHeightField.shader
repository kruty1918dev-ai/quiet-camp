Shader "QuietCamp/MeadowHeightField"
{
    Properties
    {
        _FieldAlbedo("Linear colour and wind response",2D)="black"{}
        _FieldNormalHeight("Normal and height",2D)="black"{}
        _FieldRect("World rectangle",Vector)=(0,0,8,8)
        _FieldHeight("Height interval",Vector)=(0,1,0,0)
        _ArtAmbient("Ambient",Color)=(.22,.24,.16,1)
        _ArtGrass("Grass",Float)=1 _ArtMeadow("Continuous meadow",Float)=1
        _CampUse("Observed surface use",2D)="black"{}
        _QCWeather("Weather time, wind, rain, active",Vector)=(0,0,0,0)
        _QCWind("Wind direction",Vector)=(.8,.35,0,0)
        _WeatherSkyHeight("Precipitation shelter",2D)="black"{}
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="TransparentCutout" "Queue"="AlphaTest" }
        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
        #include "Assets/QuietCamp/Resources/QuietCamp/CampArtWeather.hlsl"
        TEXTURE2D(_FieldAlbedo);SAMPLER(sampler_FieldAlbedo);
        TEXTURE2D(_FieldNormalHeight);SAMPLER(sampler_FieldNormalHeight);
        float4 _FieldRect,_FieldHeight;
        struct Attributes {float4 positionOS:POSITION;};
        struct Varyings
        {
            float4 positionCS:SV_POSITION;float3 positionWS:TEXCOORD0;
            float3 normalWS:TEXCOORD1;float2 response:TEXCOORD2;
            float3 restWS:TEXCOORD3;float snowCatch:TEXCOORD4;float4 color:COLOR;
        };
        Varyings FieldVert(Attributes input)
        {
            Varyings output=(Varyings)0;
            output.positionWS=TransformObjectToWorld(input.positionOS.xyz);
            output.positionCS=TransformWorldToHClip(output.positionWS);return output;
        }
        bool FieldSample(float3 p,out float4 colour,out float4 surface,out float3 rest)
        {
            rest=p;float3 local=TransformWorldToObject(rest);float2 uv=(local.xz-_FieldRect.xy)/_FieldRect.zw;
            colour=SAMPLE_TEXTURE2D_LOD(_FieldAlbedo,sampler_FieldAlbedo,uv,0);
            // Invert the same displacement as native blades, using sampled root-to-tip weight.
            float3 displacement=WeatherPosition(p,float2(colour.a*3,1))-p;
            rest-=displacement;local=TransformWorldToObject(rest);uv=(local.xz-_FieldRect.xy)/_FieldRect.zw;
            colour=SAMPLE_TEXTURE2D_LOD(_FieldAlbedo,sampler_FieldAlbedo,uv,0);
            surface=SAMPLE_TEXTURE2D_LOD(_FieldNormalHeight,sampler_FieldNormalHeight,uv,0);
            float height=lerp(_FieldHeight.x,_FieldHeight.y,surface.a);
            // Trace a thin surface, rather than turning each occupied texel into a solid column.
            return all(uv>=0)&&all(uv<=1)&&dot(surface.rgb,surface.rgb)>.04
                &&local.y<=height&&local.y>=height-.012;
        }
        float FieldTrace(inout Varyings input)
        {
            float3 direction=-GetWorldSpaceNormalizeViewDir(input.positionWS);
            clip(-direction.y-.025);
            float range=abs(TransformObjectToWorldDir(float3(0,_FieldHeight.y-_FieldHeight.x,0),false).y);
            float3 delta=direction*(range/(-direction.y*128));
            float3 rayPosition=input.positionWS;
            float4 colour=0,surface=0;float3 rest=0;bool hit=false;
            [loop]for(int step=0;step<=128;step++)
            {
                if(FieldSample(rayPosition,colour,surface,rest)){hit=true;break;}
                rayPosition+=delta;
            }
            clip(hit?1:-1);
            float3 low=rayPosition-delta,high=rayPosition;
            [unroll]for(int refine=0;refine<5;refine++)
            {
                float3 middle=(low+high)*.5;
                if(FieldSample(middle,colour,surface,rest))high=middle;else low=middle;
            }
            rayPosition=high;FieldSample(rayPosition,colour,surface,rest);
            input.positionWS=rayPosition;input.restWS=rest;
            input.normalWS=TransformObjectToWorldNormal(surface.rgb*2-1);input.color=float4(colour.rgb,1);
            input.response=float2(colour.a*3,1);
            float4 projected=TransformWorldToHClip(rayPosition);
            float depth=projected.z/projected.w;
            #if !UNITY_REVERSED_Z
            depth=(depth-UNITY_NEAR_CLIP_VALUE)/(1-UNITY_NEAR_CLIP_VALUE);
            #endif
            return depth;
        }
        #include "Assets/QuietCamp/Resources/QuietCamp/CampArtSurface.hlsl"
        struct FieldOutput {half4 colour:SV_Target;float depth:SV_Depth;};
        FieldOutput FieldForward(Varyings input)
        {FieldOutput output;output.depth=FieldTrace(input);output.colour=CampArtColor(input);return output;}
        FieldOutput FieldDepth(Varyings input)
        {FieldOutput output;output.depth=FieldTrace(input);output.colour=0;return output;}
        FieldOutput FieldMask(Varyings input)
        {FieldOutput output;output.depth=FieldTrace(input);output.colour=1;return output;}
        ENDHLSL
        Pass
        {
            Name "FieldForward" Tags {"LightMode"="UniversalForward"} Cull Off ZWrite On
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex FieldVert
            #pragma fragment FieldForward
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            ENDHLSL
        }
        Pass
        {
            Name "DepthOnly" Tags {"LightMode"="DepthOnly"} Cull Off ZWrite On ColorMask R
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex FieldVert
            #pragma fragment FieldDepth
            ENDHLSL
        }
        Pass
        {
            Name "VegetationMask" Tags {"LightMode"="QuietCampVegetationMask"} Cull Off ZWrite Off ColorMask R
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex FieldVert
            #pragma fragment FieldMask
            ENDHLSL
        }
    }
}
