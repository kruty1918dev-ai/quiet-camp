Shader "QuietCamp/TentCloth"
{
    Properties
    {
        _BaseColor("Fabric",Color)=(.8,.3,.15,1) _MeshMin("Minimum",Vector)=(0,0,0,0) _MeshSize("Size",Vector)=(1,1,1,0)
        _ShelterGlow("Interior warmth",Range(0,1))=0 _DoorAnchor("Door warmth centre and radius",Vector)=(0,0,0,1)
        _ClothWetness("Canvas moisture",Range(0,1))=0 _SnowCover("Settled snow",Range(0,1))=0 _LeafCover("Settled autumn leaves",Range(0,1))=0
        _StorageBulge("Packed corner and radius",Vector)=(0,0,0,0) _StorageAmount("Packed corner pressure",Range(0,1))=0
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" }
        Cull Off
        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
        CBUFFER_START(UnityPerMaterial)
        half4 _BaseColor;float4 _MeshMin;float4 _MeshSize;float4 _DoorAnchor;float4 _StorageBulge;
        half _ShelterGlow,_ClothWetness,_SnowCover,_LeafCover,_StorageAmount;
        CBUFFER_END
        float4 _AtmosWindXZ;float _AtmosWindStrength;float _AtmosWindTime;
        #include "WindField.hlsl"
        float3 Cloth(float3 p)
        {
            float3 u=saturate((p-_MeshMin.xyz)/max(_MeshSize.xyz,float3(.001,.001,.001)));
            // Edge seams, ground pegs and roof ridge remain anchored.
            float pin=sin(u.y*PI)*sin(u.x*PI)*(.35+.65*sin(u.z*PI));
            float3 world=TransformObjectToWorld(p);
            float wave=sin(_AtmosWindTime*2.1+dot(world.xz,float2(.8,1.1)))+.3*sin(_AtmosWindTime*4.4+world.x);
            float ripple=.18*sin(_AtmosWindTime*3.3+world.z*2.4-world.x*1.6);
            float heightWorld=length(TransformObjectToWorldDir(float3(0,_MeshSize.y,0),false));
            float3 rootWorld=TransformObjectToWorld(float3(0,0,0));
            float4 localWind=CampWindAt(rootWorld,heightWorld*.6,_AtmosWindXZ.xy,_AtmosWindStrength,_AtmosWindTime);
            float3 windWorld=float3(localWind.x*wave,ripple,localWind.y*wave)*pin*sqrt(localWind.z)*heightWorld*.22;
            // Local packing pressure shares the same seam/ridge pin mask as
            // wind. Rigid submeshes do not use this shader.
            float falloff=saturate(1-distance(p,_StorageBulge.xyz)/max(.001,_StorageBulge.w));falloff*=falloff;
            p.x+=(_StorageBulge.x>=_MeshMin.x+_MeshSize.x*.5?1:-1)*falloff*pin*_StorageAmount*_MeshSize.x*.24;
            p+=TransformWorldToObjectDir(windWorld,false);
            return p;
        }
        ENDHLSL
        Pass
        {
            Name "Fabric" Tags{"LightMode"="UniversalForward"}
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            struct A{float4 p:POSITION;float3 n:NORMAL;};
            struct V{float4 p:SV_POSITION;float3 w:TEXCOORD0;half3 n:TEXCOORD1;half warmth:TEXCOORD2;float3 local:TEXCOORD3;};
            V vert(A a){V v;v.w=TransformObjectToWorld(Cloth(a.p.xyz));v.p=TransformWorldToHClip(v.w);v.n=TransformObjectToWorldNormal(a.n);
                v.local=(a.p.xyz-_MeshMin.xyz)/max(_MeshSize.xyz,float3(.001,.001,.001));
                v.warmth=saturate(1-distance(a.p.xyz,_DoorAnchor.xyz)/max(.001,_DoorAnchor.w));v.warmth*=v.warmth;return v;}
            half4 frag(V v,bool front:SV_IsFrontFace):SV_Target
            {
                // Light the bent fabric facets, not the original rigid roof.
                float3 authored=normalize(v.n);
                float3 facet=cross(ddy(v.w),ddx(v.w));
                // Small projected triangles must not produce an undefined normal
                // on mobile GPUs. Keep the normal calculation in float precision.
                float facetLength=dot(facet,facet);
                float3 folded=facetLength>1e-12?facet*rsqrt(max(facetLength,1e-12)):authored;
                folded*=dot(folded,authored)<0?-1:1;
                half3 n=folded*(front?1:-1);Light sun=GetMainLight(TransformWorldToShadowCoord(v.w));
                half lambert=saturate(dot(n,sun.direction));
                // A lantern lights the entrance locally; it never recolours
                // the entire canopy yellow while rain extinguishes the fire.
                half3 canvas=_BaseColor.rgb*(1-_ClothWetness*.13);
                // A restrained, antialiased weave gives canvas a material rather than a plastic face.
                float2 weave=v.local.xy*float2(120,95);
                float detail=1-smoothstep(.25,1.0,max(length(ddx(weave)),length(ddy(weave))));
                canvas*=1+sin(weave.x*PI*2)*sin(weave.y*PI*2)*.025*detail;
                // Bounded, stable procedural coverage: no decals, textures,
                // transparent shells, cameras or extra render passes.
                half snow=0;
                UNITY_BRANCH if(_SnowCover+_LeafCover>.001&&front)
                {
                    float2 cell=floor(v.local.xz*float2(17,13));float2 tile=frac(v.local.xz*float2(17,13))-.5;
                    float hash=frac(sin(dot(cell,float2(127.1,311.7)))*43758.5453);
                    half upward=smoothstep(.25,.62,n.y);
                    // Soft connected deposits follow the roof, rather than a grid of painted squares.
                    float2 drift=v.local.xz*float2(11,8);
                    float deposit=.5+.22*sin(drift.x+drift.y*.4)+.16*sin(drift.y*.71-drift.x*.9)+.12*sin(drift.x*1.73+drift.y*1.3);
                    snow=smoothstep(deposit-.16,deposit+.16,_SnowCover)*smoothstep(0,.08,_SnowCover)*upward;
                    canvas=lerp(canvas,half3(.83,.88,.85),snow);
                    float leafEdge=abs(tile.x)+abs(tile.y)*.65;
                    float softness=max(.005,fwidth(leafEdge));
                    half leaf=step(hash,_LeafCover)*step(.001,_LeafCover)*(1-smoothstep(.30-softness,.30+softness,leafEdge))*upward*(1-snow);
                    canvas=lerp(canvas,lerp(half3(.47,.27,.12),half3(.72,.46,.18),hash),leaf);
                }
                half highlight=0;
                UNITY_BRANCH if(_ClothWetness>.001&&front)
                {
                    half3 view=GetWorldSpaceNormalizeViewDir(v.w);half3 halfway=normalize(view+sun.direction);
                    highlight=pow(saturate(dot(n,halfway)),24)*_ClothWetness*.12*sun.shadowAttenuation*(1-snow);
                }
                half3 warmth=_BaseColor.rgb*half3(1,.65,.32)*_ShelterGlow*v.warmth*(front?.22:.38)*(1-snow*.5);
                half sheen=pow(saturate(dot(n,normalize(GetWorldSpaceNormalizeViewDir(v.w)+sun.direction))),8)*.018*(1-_ClothWetness)*(1-snow);
                return half4(canvas*(SampleSH(n)+sun.color*(.15+.85*lambert)*sun.shadowAttenuation)*(front?1:.64)+sun.color*(highlight+sheen*sun.shadowAttenuation)+warmth,1);
            }
            ENDHLSL
        }
        Pass
        {
            Name "DepthOnly" Tags{"LightMode"="DepthOnly"} ZWrite On ColorMask R
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex vert
            #pragma fragment frag
            float4 vert(float4 p:POSITION):SV_POSITION{return TransformObjectToHClip(Cloth(p.xyz));}
            half4 frag():SV_Target{return 0;}
            ENDHLSL
        }
        Pass
        {
            Name "ShadowCaster" Tags{"LightMode"="ShadowCaster"} ZWrite On ZTest LEqual ColorMask 0
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex vert
            #pragma fragment frag
            float3 _LightDirection;
            struct A{float4 p:POSITION;float3 n:NORMAL;};
            float4 vert(A a):SV_POSITION
            {
                float3 w=TransformObjectToWorld(Cloth(a.p.xyz));float3 n=TransformObjectToWorldNormal(a.n);
                float4 p=TransformWorldToHClip(ApplyShadowBias(w,n,_LightDirection));
                #if UNITY_REVERSED_Z
                p.z=min(p.z,UNITY_NEAR_CLIP_VALUE*p.w);
                #else
                p.z=max(p.z,UNITY_NEAR_CLIP_VALUE*p.w);
                #endif
                return p;
            }
            half4 frag():SV_Target{return 0;}
            ENDHLSL
        }
    }
}
