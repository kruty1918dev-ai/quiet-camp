Shader "QuietCamp/ForestVolume"
{
    Properties { _FogColor("Scattering",Color)=(.7,.8,.65,1) _Density("Density",Float)=.08 _Steps("Steps",Float)=8 _Shaft("Shaft",Float)=0 _LightTime("Canopy time",Float)=0 _Seed("Canopy seed",Float)=0
        [HideInInspector] _EdgeFog("Forest edge volume",Float)=0
        [HideInInspector] _GladeHalfSize("Clear glade / margin / feather",Vector)=(3,3,3.4,9)
        [HideInInspector] _FogHeight("Fog layer height",Float)=4.5
    }
    SubShader
    {
        Tags {"RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent"}
        Blend SrcAlpha OneMinusSrcAlpha ZWrite Off Cull Back
        Pass
        {
            Name "ForestScattering"
            Tags { "LightMode"="UniversalForward" }
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            // Hard shadow lookup per step; integration softens the volume without a PCF kernel per sample.
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            CBUFFER_START(UnityPerMaterial)
            half4 _FogColor;float _Density,_Steps,_Shaft,_LightTime,_Seed,_EdgeFog,_FogHeight;float4 _GladeHalfSize;
            CBUFFER_END
            float4 _AtmosWindXZ;float _AtmosWindTime;
            struct A{float4 p:POSITION;};struct V{float4 p:SV_POSITION;float3 w:TEXCOORD0;};
            V vert(A a){V v;v.w=TransformObjectToWorld(a.p.xyz);v.p=TransformWorldToHClip(v.w);return v;}
            half4 frag(V v):SV_Target
            {
                float3 direction=unity_OrthoParams.w>.5?GetViewForwardDir():normalize(v.w-_WorldSpaceCameraPos);
                float3 originOS=TransformWorldToObject(v.w);float3 dirOS=mul((float3x3)unity_WorldToObject,direction);
                // A ray parallel to a box face must never divide by zero.
                float3 raySign=step(0,dirOS)*2-1;
                float3 safeDir=raySign*max(abs(dirOS),.00001);
                float3 exitDistance=(raySign*.5-originOS)/safeDir;
                float travel=max(0,min(exitDistance.x,min(exitDistance.y,exitDistance.z)));
                float sceneZ=SampleSceneDepth(GetNormalizedScreenSpaceUV(v.p));
                float sceneEye=unity_OrthoParams.w>.5?LinearDepthToEyeDepth(sceneZ):LinearEyeDepth(sceneZ,_ZBufferParams);
                float entryEye=-TransformWorldToView(v.w).z;
                travel=min(travel,max(0,(sceneEye-entryEye)/max(.01,dot(direction,GetViewForwardDir()))));
                if(_EdgeFog>.5)
                {
                    float2 clearHalf=_GladeHalfSize.xy+_GladeHalfSize.z;
                    // The protected rectangle is convex: if both ends of a
                    // ray are inside it, skip integration for the whole glade.
                    if(all(abs(v.w.xz)<=clearHalf)&&all(abs((v.w+direction*travel).xz)<=clearHalf))return 0;
                }
                float stride=travel/max(1,_Steps);float optical=0;
                [loop]for(int i=0;i<12;i++)
                {
                    if(i>=_Steps)break;
                    float3 p=originOS+dirOS*(i+.5)*stride;
                    float edge=saturate((.5-max(abs(p.x),max(abs(p.y),abs(p.z))))*5);
                    float time=lerp(_AtmosWindTime,_LightTime,_Shaft);
                    float noise=.82+.18*sin(p.x*7+p.z*5+time*.19+_Seed);
                    float radius=lerp(.48,.27,saturate(p.y+.5));
                    float shaft=lerp(1,pow(saturate(1-length(p.xz)/radius),1.4),_Shaft);
                    float3 world=v.w+direction*(i+.5)*stride;
                    if(_EdgeFog>.5)
                    {
                        float outside=length(max(abs(world.xz)-_GladeHalfSize.xy-_GladeHalfSize.z,0));
                        edge*=smoothstep(0,_GladeHalfSize.w,outside)*saturate(1-world.y/max(.2,_FogHeight));
                        noise=.82+.18*sin(world.x*.5+world.z*.39+_LightTime*.07);
                    }
                    float shadow=1;
                    if(_Shaft>.5)shadow=MainLightRealtimeShadow(TransformWorldToShadowCoord(world));
                    // Crowns and tents cut the light at the actual point in space, not just at the box centre.
                    optical+=edge*noise*shaft*stride*_Density*lerp(1,shadow,_Shaft);
                }
                Light light=GetMainLight(TransformWorldToShadowCoord(v.w+direction*travel*.5));
                float forwardScatter=pow(saturate(dot(-direction,light.direction)*.5+.5),4);
                half3 tint=_FogColor.rgb*(.55+light.color*(.35+forwardScatter*.45)*lerp(light.shadowAttenuation,1,_Shaft));
                return half4(tint,saturate(1-exp(-optical)));
            }
            ENDHLSL
        }
    }
    Fallback Off
}
