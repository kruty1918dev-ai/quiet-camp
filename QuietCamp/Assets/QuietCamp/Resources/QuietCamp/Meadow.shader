Shader "QuietCamp/Meadow"
{
    Properties
    {
        _ClearingHalfSize ("Clearing half size", Vector) = (5,5,0,0)
        _GroundLow("Dark grass",Color)=(.27,.4,.23,1)
        _GroundHigh("Light grass",Color)=(.43,.57,.31,1)
        _SoilTone("Worn earth",Color)=(.43,.35,.25,1)
        _CellGrid("Quiet placement grid",Range(0,1))=0
        _SnowSeason("Snow-covered clearing",Range(0,1))=0
        _HeatMelt("Retained campfire heat",Range(0,1))=0
        _HeatSite0("Fire centre/radius/active",Vector)=(0,0,0,0)
        _HeatSite1("Fire centre/radius/active",Vector)=(0,0,0,0)
        _HeatSite2("Fire centre/radius/active",Vector)=(0,0,0,0)
        _HeatSite3("Fire centre/radius/active",Vector)=(0,0,0,0)
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Geometry" "RenderType"="Opaque" }
        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        CBUFFER_START(UnityPerMaterial)
            float4 _ClearingHalfSize;
            half4 _GroundLow,_GroundHigh,_SoilTone;
            float _CellGrid;
            float _SnowSeason,_HeatMelt;
            float4 _HeatSite0,_HeatSite1,_HeatSite2,_HeatSite3;
        CBUFFER_END
        ENDHLSL
        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }
            ZWrite On
            Cull Back
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            struct Attributes { float4 positionOS : POSITION; };
            struct Varyings { float4 positionCS : SV_POSITION; float3 positionWS : TEXCOORD0; };
            Varyings Vert(Attributes v)
            {
                Varyings o;
                o.positionWS = TransformObjectToWorld(v.positionOS.xyz);
                o.positionCS = TransformWorldToHClip(o.positionWS);
                return o;
            }
            float Hash(float2 p)
            {
                return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453);
            }
            float Noise(float2 p)
            {
                float2 cell = floor(p), t = frac(p);
                t = t * t * (3.0 - 2.0 * t);
                return lerp(lerp(Hash(cell), Hash(cell + float2(1,0)), t.x),
                    lerp(Hash(cell + float2(0,1)), Hash(cell + 1), t.x), t.y);
            }
            half4 Frag(Varyings i) : SV_Target
            {
                float2 ground = i.positionWS.xz;
                float clearing = 1.0 - smoothstep(.65, 1.65,
                    length(ground / max(_ClearingHalfSize.xy, float2(1,1))));
                float patches = Noise(ground * .32 + 17.0) * .7 + Noise(ground * 1.15) * .3;
                half3 grass = lerp(_GroundLow.rgb, _GroundHigh.rgb, patches);
                grass = lerp(grass, _GroundHigh.rgb, clearing * .22);
                // Small irregular earth patches share the ground instead of a flat painted lawn.
                float earth=smoothstep(.72,.88,Noise(ground*.75+41))*Noise(ground*2.1);
                grass=lerp(grass,_SoilTone.rgb,earth*.45);
                // An irregular damp thaw around the real fire pit, with a soft dirty-snow bank.
                if(_SnowSeason>.5)
                {
                    float thaw=0;
                    float irregular=(Noise(ground*2.3+7)-.5)*.12;
                    float4 sites[4]={_HeatSite0,_HeatSite1,_HeatSite2,_HeatSite3};
                    [unroll] for(int site=0;site<4;site++)
                    {
                        if(sites[site].w<=0)continue;
                        float radius=length(ground-sites[site].xy)/max(.01,sites[site].z);
                        radius+=irregular;
                        thaw=max(thaw,(1-smoothstep(.25,1,radius))*sites[site].w);
                    }
                    grass=lerp(grass,_SoilTone.rgb*half3(.75,.53,.38),thaw*_HeatMelt);
                }
                // Quiet granular soil/grass detail, filtered as the camera recedes.
                float grain=Noise(ground*12.0+3);
                float grainWeight=1-smoothstep(.18,.55,max(length(ddx(ground)),length(ddy(ground)))*12);
                grass*=1+(grain-.5)*.09*grainWeight;
                // Cell boundaries stay readable without alternating painted squares.
                // Derivatives keep the thin soil seams stable at phone and tablet scales.
                float2 phase=frac(ground+_ClearingHalfSize.xy);
                float2 cellEdge=min(phase,1-phase);
                float2 seam=1-smoothstep(float2(.006,.006),.006+max(fwidth(ground),float2(.001,.001)),cellEdge);
                grass*=1-max(seam.x,seam.y)*.14*_CellGrid;
                Light sun = GetMainLight(TransformWorldToShadowCoord(i.positionWS));
                half3 lighting = SampleSH(half3(0,1,0))
                    + sun.color * saturate(sun.direction.y) * sun.shadowAttenuation;
                return half4(grass * max(lighting, half3(.16,.16,.16)), 1);
            }
            ENDHLSL
        }
        Pass
        {
            Name "DepthOnly" Tags { "LightMode"="DepthOnly" }
            ZWrite On ColorMask R Cull Back
            HLSLPROGRAM
            #pragma vertex DepthVert
            #pragma fragment DepthFrag
            float4 DepthVert(float4 p:POSITION):SV_POSITION{return TransformObjectToHClip(p.xyz);}
            half4 DepthFrag():SV_Target{return 0;}
            ENDHLSL
        }
    }
}
