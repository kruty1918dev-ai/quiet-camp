// Cinematic world: shared gameplay foliage response, with route-reveal atmosphere.
// Living-vegetation sway for URP: one shared wind owner drives every plant.
//
// Data contract (set by the scene atmosphere owner — never per plant):
//   Globals (Shader.SetGlobal*, one authority):
//     _AtmosWindXZ     float2  normalized world-space wind direction
//     _AtmosWindStrength float shared strength 0..1 (already includes gusts;
//                      the owner eases it to 0 under reduced motion)
//     _AtmosWindTime   float   controlled visual seconds (WindSim PhaseSeconds)
//     _AtmosWaveLen / _AtmosWaveSpeed  spatial wave field constants
//     _AtmosSunDirW / _AtmosSunColor / _AtmosAmbient  real scene light, tinted
//     _AtmosFlutterScale float  tier switch for the small tip flutter
//   Per-material (UnityPerMaterial, species response):
//     _BaseColor, _SwayAmp (tip fraction of mesh height), _SwayFreq (Hz),
//     _FlutterAmp, _FlutterFreq, _RootLock (rigid base fraction),
//     _PhaseLag (extra phase offset for flower heads etc.), _InnerShade
//   Per-renderer (MaterialPropertyBlock, written once at registration):
//     _MeshMinY, _MeshTopY — object-space bounds for the normalized mask
//
// Legacy _SwayAmp/_SwayFreq keep their meaning (amplitude & frequency), so
// existing materials stay valid.
Shader "QuietCamp/RoadmapLit"
{
    Properties
    {
        _BaseColor ("Color", Color) = (0.4, 0.6, 0.35, 1)
        _SnowCover ("Seasonal snow on upward faces", Range(0,1)) = 0
        _Dew ("Subtle morning dew glint", Range(0,.1)) = 0
        _SwayAmp ("Tip Sway (fraction of height)", Range(0, 0.4)) = 0.05
        _SwayFreq ("Sway Frequency (Hz)", Range(0, 6)) = 1.6
        _FlutterAmp ("Flutter (fraction of height)", Range(0, 0.05)) = 0.002
        _FlutterFreq ("Flutter Frequency (Hz)", Range(0, 6)) = 0.8
        _RootLock ("Rigid base fraction", Range(0, 0.9)) = 0.2
        _PhaseLag ("Phase lag (radians)", Range(-3, 3)) = 0
        _InnerShade ("Inner shading", Range(0, 0.6)) = 0.25
        [Enum(UnityEngine.Rendering.CullMode)] _Cull ("Face culling", Float) = 2
        [HideInInspector] _ClusterWind ("Per-plant roots in UV1", Float) = 0
        [HideInInspector] _VertexTint ("Batched plant colours", Float) = 0
        [HideInInspector] _TreeWind ("Shared trunk and crown bend", Float) = 0
        [HideInInspector] _CrownStart ("Crown attachment height", Float) = .35
    }
    SubShader
    {
        Tags { "Queue"="Geometry" "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" }
        LOD 100
        Cull [_Cull]

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

        CBUFFER_START(UnityPerMaterial)
            float4 _BaseColor;
            float _SwayAmp, _SwayFreq, _FlutterAmp, _FlutterFreq;
            float _RootLock, _PhaseLag, _InnerShade, _Cull, _ClusterWind, _VertexTint;
            float _TreeWind, _CrownStart, _SnowCover, _Dew;
        CBUFFER_END

        // Shared wind/light state — one scene owner writes these.
        float4 _RoadmapFogColor;
        float _RoadmapRevealZ;
        float4 _AtmosWindXZ;      // xy = direction, zw unused
        float _AtmosWindStrength;
        float _AtmosWindTime;
        float _AtmosWaveLen;
        float _AtmosWaveSpeed;
        float _AtmosFlutterScale;
        float4 _AtmosSunDirW;     // xyz = toward-sun direction
        float4 _AtmosSunColor;
        float4 _AtmosAmbient;
        #include "WindField.hlsl"

        // Per-renderer registration data (MaterialPropertyBlock).
        float _MeshMinY, _MeshTopY;

        struct SwayAttributes
        {
            float4 vertex : POSITION;
            float3 normal : NORMAL;
            // Combined understory meshes store (root.x, root.z, base.y, height).
            // Prefabs use their registered bounds and do not need this stream.
            float4 plant : TEXCOORD1;
            half4 color : COLOR;
        };

        /// Normalized 0..1 height mask inside this mesh's own bounds —
        /// a grass blade and a pine use the same code with their own scale.
        float HeightMask(float objectY, float baseY, float meshH)
        {
            float h = saturate((objectY - baseY) / meshH);
            return smoothstep(_RootLock, 1.0, h); // rigid base, soft transition
        }

        /// Shared spatial wave: phase = 2π(dot(anchorXZ,dir)/λ − v·t/λ) + seed.
        /// The cluster seed derives from the anchor position — stable, and a
        /// 90°-rotated prefab still bends along the world wind.
        float WavePhase(float3 rootWorld)
        {
            float2 dir = _AtmosWindXZ.xy;
            float wavelength = max(.1f, _AtmosWaveLen);
            float travel = (dot(rootWorld.xz, dir) - _AtmosWaveSpeed * _AtmosWindTime) / wavelength;
            float seed = frac(dot(rootWorld.xz, float2(0.311f, 0.177f))) * 6.2831853f;
            return 6.2831853f * travel + seed + _PhaseLag;
        }

        /// Deforms an object-space vertex by the shared wind. Displacement is
        /// authored in world space (along _AtmosWindXZ) and transformed back,
        /// so rotation/scale of the instance cannot skew the direction.
        void PlantFrame(float4 plant, out float3 rootWorld, out float baseY, out float meshH)
        {
            rootWorld = TransformObjectToWorld(float3(0, 0, 0));
            baseY = _MeshMinY;
            meshH = max(1e-4, _MeshTopY - _MeshMinY);
            if (_ClusterWind > .5f)
            {
                rootWorld = TransformObjectToWorld(float3(plant.x, plant.z, plant.y));
                baseY = plant.z;
                meshH = max(1e-4, plant.w);
            }
        }

        float3 DeformOS(float3 vertexOS, float4 plant)
        {
            // Negative height is a rigid ground patch (snow or settled litter).
            if(_ClusterWind>.5f&&plant.w<0)return vertexOS;
            if(_SwayAmp+_FlutterAmp<.000001f&&_TreeWind<.5f)return vertexOS;
            float3 rootWorld; float baseY, meshH;
            PlantFrame(plant, rootWorld, baseY, meshH);
            float bend = HeightMask(vertexOS.y, baseY, meshH);
            float phase = WavePhase(rootWorld);
            float4 localWind;

            // Subtract the travelling phase so advection adds to oscillation rather than cancelling slow tree species.
            float sway = sin(6.2831853f * _SwayFreq * _AtmosWindTime - phase);
            float swayB = sin(6.2831853f * _SwayFreq * 0.83f * _AtmosWindTime - phase * 1.7f);
            float osc = 0.55f * sway + 0.45f * swayB;
            // A gentle physical breeze must still read at the whole-clearing
            // camera distance. Zero wind/reduced motion remains exactly still.
            float h = saturate((vertexOS.y - baseY) / meshH);
            float worldHeight = length(TransformObjectToWorldDir(float3(0, meshH, 0), false));
            localWind = CampWindAt(rootWorld, worldHeight * h, _AtmosWindXZ.xy, _AtmosWindStrength, _AtmosWindTime);
            float visualWind = sqrt(localWind.z);
            float flex = _TreeWind > .5f ? smoothstep(min(.98f, _CrownStart), 1.0f, h) : bend;
            float tip = _SwayAmp * worldHeight * visualWind * osc * flex;
            if (_TreeWind > .5f)
                tip += .035f * worldHeight * visualWind * osc * smoothstep(.25f, 1.0f, h);

            // Small high-frequency flutter, weighted to the periphery (h²) —
            // canopy masses stay coherent instead of every leaf dancing.
            if (_AtmosFlutterScale > 0.0f && _FlutterAmp > 0.0f)
            {
                float flut = sin(6.2831853f * _FlutterFreq * _AtmosWindTime
                             + phase * 2.3f + vertexOS.x * 3.1f);
                tip += _FlutterAmp * worldHeight * flut * flex * flex
                     * visualWind * _AtmosFlutterScale;
            }

            // Approximate arc: tips drop slightly while bending so foliage
            // keeps its length instead of stretching rubber-like.
            float3 displacementW = float3(localWind.x * tip,
                -abs(tip) * .3f * (_TreeWind > .5f ? smoothstep(.25f, 1.0f, h) : bend), localWind.y * tip);
            return vertexOS + TransformWorldToObjectDir(displacementW, false);
        }

        ENDHLSL

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.5
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH

            struct Varyings
            {
                float4 pos : SV_POSITION;
                float3 col : TEXCOORD0;
                float3 world : TEXCOORD1;
                float3 sunlight : TEXCOORD2;
            };

            Varyings vert(SwayAttributes v)
            {
                Varyings o;
                float3 bentOS = DeformOS(v.vertex.xyz, v.plant);
                float3 wp = TransformObjectToWorld(bentOS);
                o.pos = TransformWorldToHClip(wp);
                o.world = wp;
                // Correct normal path for non-uniform scale; the bend angles
                // are small so normals keep their authored direction.
                float3 n = TransformObjectToWorldNormal(v.normal);
                float3 rootW; float baseY, meshH;
                PlantFrame(v.plant, rootW, baseY, meshH);
                bool rigid=_ClusterWind>.5f&&v.plant.w<0;
                float mask = rigid?1:HeightMask(v.vertex.y, baseY, meshH);
                // Use the pipeline light, including its linear colour conversion.
                // A second gamma-space global previously washed out batched foliage.
                Light sun=GetMainLight();
                float3 sunDirection=sun.direction;
                float ndl = dot(n,sunDirection);
                // Thin leaf ribbons catch warm light on both faces.
                ndl = saturate(!rigid && _Cull < .5f ? abs(ndl)*.85f+.15f : ndl);
                // Volume cue: inner/low canopy stays cooler and darker, the
                // lit side picks up the real sun color.
                float inner = lerp(1.0f, 1.0f - _InnerShade, 1.0f - mask);
                float hemisphere=.58f+.42f*saturate(n.y*.5f+.5f);
                float translucency=rigid?0:pow(saturate(dot(normalize(_WorldSpaceCameraPos-wp),-sunDirection)),3)*.14f*mask;
                half3 vertexTint=v.color.rgb;
                #ifndef UNITY_COLORSPACE_GAMMA
                // Unity's bounded polynomial avoids three per-vertex pow calls.
                // The plant palette stays in the same linear lighting space on mobile.
                vertexTint=FastSRGBToLinear(vertexTint);
                #endif
                half3 tint = _BaseColor.rgb * lerp(half3(1,1,1), vertexTint, _VertexTint);
                // Snow ground already has authored snow colours; its fine variation
                // and directional slopes must survive the coating used on tree crowns.
                tint=lerp(tint,half3(.88,.92,.98),_SnowCover*smoothstep(.35,.75,n.y)*(rigid?0:1));
                o.col=tint*SampleSH(n)*inner;
                o.sunlight=tint*sun.color*(ndl+translucency)*inner;
                float dew=pow(saturate(dot(reflect(-sunDirection,n),normalize(_WorldSpaceCameraPos-wp))),16)*_Dew;
                o.sunlight+=sun.color*dew;
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                half shadow = MainLightRealtimeShadow(TransformWorldToShadowCoord(i.world));
                half3 lit=i.col+i.sunlight*shadow;
                float air=1-exp(-pow(max(0,distance(i.world,_WorldSpaceCameraPos)-19)*.027,2));
                float future=smoothstep(_RoadmapRevealZ-3,_RoadmapRevealZ+7,i.world.z);
                return half4(lerp(lit,_RoadmapFogColor.rgb,max(air,future*.94)), _BaseColor.a);
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode"="DepthOnly" }
            ZWrite On
            ColorMask R
            Cull [_Cull]
            HLSLPROGRAM
            #pragma vertex DepthVert
            #pragma fragment DepthFrag
            #pragma target 3.5
            struct DepthVaryings { float4 pos:SV_POSITION; };
            DepthVaryings DepthVert(SwayAttributes v)
            { DepthVaryings o;o.pos=TransformObjectToHClip(DeformOS(v.vertex.xyz,v.plant));return o; }
            half4 DepthFrag(DepthVaryings i):SV_TARGET{return 0;}
            ENDHLSL
        }

        // The cast shadow follows the same deformation — a bent crown never
        // leaves an unmoving shadow underneath.
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }
            ZWrite On
            ZTest LEqual
            ColorMask 0
            Cull [_Cull]

            HLSLPROGRAM
            #pragma vertex ShadowVert
            #pragma fragment ShadowFrag
            #pragma target 3.5
            #pragma multi_compile _ _CASTING_PUNCTUAL_LIGHT_SHADOW

            float3 _LightDirection;
            float3 _LightPosition;

            struct ShadowVaryings { float4 pos : SV_POSITION; };

            ShadowVaryings ShadowVert(SwayAttributes v)
            {
                ShadowVaryings o;
                float3 wp = TransformObjectToWorld(DeformOS(v.vertex.xyz, v.plant));
                float3 normalWS = TransformObjectToWorldNormal(v.normal);
            #if _CASTING_PUNCTUAL_LIGHT_SHADOW
                float3 lightDirectionWS = normalize(_LightPosition - wp);
            #else
                float3 lightDirectionWS = _LightDirection;
            #endif
                float4 pos = TransformWorldToHClip(
                    ApplyShadowBias(wp, normalWS, lightDirectionWS));
                o.pos = ApplyShadowClamping(pos);
                return o;
            }

            half4 ShadowFrag(ShadowVaryings i) : SV_TARGET { return 0; }
            ENDHLSL
        }
    }
    Fallback Off
}
