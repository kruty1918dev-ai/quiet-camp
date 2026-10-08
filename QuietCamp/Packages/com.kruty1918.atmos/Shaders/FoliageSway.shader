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
Shader "Atmos/FoliageSway"
{
    Properties
    {
        _BaseColor ("Color", Color) = (0.4, 0.6, 0.35, 1)
        _SwayAmp ("Tip Sway (fraction of height)", Range(0, 0.3)) = 0.05
        _SwayFreq ("Sway Frequency (Hz)", Range(0, 6)) = 1.6
        _FlutterAmp ("Flutter (fraction of height)", Range(0, 0.05)) = 0.002
        _FlutterFreq ("Flutter Frequency (Hz)", Range(0, 6)) = 0.8
        _RootLock ("Rigid base fraction", Range(0, 0.9)) = 0.2
        _PhaseLag ("Phase lag (radians)", Range(-3, 3)) = 0
        _InnerShade ("Inner shading", Range(0, 0.6)) = 0.25
    }
    SubShader
    {
        Tags { "Queue"="Geometry" "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" }
        LOD 100

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

        CBUFFER_START(UnityPerMaterial)
            float4 _BaseColor;
            float _SwayAmp, _SwayFreq, _FlutterAmp, _FlutterFreq;
            float _RootLock, _PhaseLag, _InnerShade;
        CBUFFER_END

        // Shared wind/light state — one scene owner writes these.
        float4 _AtmosWindXZ;      // xy = direction, zw unused
        float _AtmosWindStrength;
        float _AtmosWindTime;
        float _AtmosWaveLen;
        float _AtmosWaveSpeed;
        float _AtmosFlutterScale;
        float4 _AtmosSunDirW;     // xyz = toward-sun direction
        float4 _AtmosSunColor;
        float4 _AtmosAmbient;

        // Per-renderer registration data (MaterialPropertyBlock).
        float _MeshMinY, _MeshTopY;

        struct SwayAttributes
        {
            float4 vertex : POSITION;
            float3 normal : NORMAL;
        };

        /// Normalized 0..1 height mask inside this mesh's own bounds —
        /// a grass blade and a pine use the same code with their own scale.
        float HeightMask(float objectY)
        {
            float h = saturate((objectY - _MeshMinY) / max(1e-4, _MeshTopY - _MeshMinY));
            return smoothstep(_RootLock, 1.0, h); // rigid base, soft transition
        }

        /// Shared spatial wave: phase = 2π(dot(anchorXZ,dir)/λ − v·t/λ) + seed.
        /// The cluster seed derives from the anchor position — stable, and a
        /// 90°-rotated prefab still bends along the world wind.
        float WavePhase(float3 rootWorld)
        {
            float2 dir = _AtmosWindXZ.xy;
            float travel = dot(rootWorld.xz, dir) / _AtmosWaveLen
                         - _AtmosWaveSpeed * _AtmosWindTime / _AtmosWaveLen;
            float seed = frac(dot(rootWorld.xz, float2(0.311f, 0.177f))) * 6.2831853f;
            return 6.2831853f * travel + seed + _PhaseLag;
        }

        /// Deforms an object-space vertex by the shared wind. Displacement is
        /// authored in world space (along _AtmosWindXZ) and transformed back,
        /// so rotation/scale of the instance cannot skew the direction.
        float3 DeformOS(float3 vertexOS, float3 rootWorld)
        {
            float bend = HeightMask(vertexOS.y);
            float meshH = max(1e-4, _MeshTopY - _MeshMinY);
            float phase = WavePhase(rootWorld);

            // Primary bend: species frequency on the shared visual clock.
            float sway = sin(6.2831853f * _SwayFreq * _AtmosWindTime + phase);
            float swayB = sin(6.2831853f * _SwayFreq * 0.83f * _AtmosWindTime + phase * 1.7f);
            float osc = 0.55f * sway + 0.45f * swayB;
            float tip = _SwayAmp * meshH * _AtmosWindStrength * osc * bend;

            // Small high-frequency flutter, weighted to the periphery (h²) —
            // canopy masses stay coherent instead of every leaf dancing.
            float h = saturate((vertexOS.y - _MeshMinY) / meshH);
            float flut = sin(6.2831853f * _FlutterFreq * _AtmosWindTime
                         + phase * 2.3f + vertexOS.x * 3.1f);
            tip += _FlutterAmp * meshH * flut * h * h
                 * _AtmosWindStrength * _AtmosFlutterScale;

            // Approximate arc: tips drop slightly while bending so foliage
            // keeps its length instead of stretching rubber-like.
            vertexOS.y -= abs(tip) * 0.3f * bend;

            float3 dirOS = TransformWorldToObjectDir(
                float3(_AtmosWindXZ.x, 0.0, _AtmosWindXZ.y));
            return vertexOS + dirOS * tip;
        }

        float3 RootWorld()
        {
            return TransformObjectToWorld(float3(0, 0, 0));
        }
        ENDHLSL

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            struct Varyings
            {
                float4 pos : SV_POSITION;
                float3 col : TEXCOORD0;
            };

            Varyings vert(SwayAttributes v)
            {
                Varyings o;
                float3 rootW = RootWorld();
                float3 bentOS = DeformOS(v.vertex.xyz, rootW);
                float3 wp = TransformObjectToWorld(bentOS);
                o.pos = TransformWorldToHClip(wp);
                // Correct normal path for non-uniform scale; the bend angles
                // are small so normals keep their authored direction.
                float3 n = TransformObjectToWorldNormal(v.normal);
                float mask = HeightMask(v.vertex.y);
                float ndl = saturate(dot(n, normalize(_AtmosSunDirW.xyz)) * 0.5f + 0.5f);
                // Volume cue: inner/low canopy stays cooler and darker, the
                // lit side picks up the real sun color.
                float inner = lerp(1.0f, 1.0f - _InnerShade, 1.0f - mask);
                float3 lit = _AtmosAmbient.rgb + _AtmosSunColor.rgb * ndl;
                o.col = _BaseColor.rgb * lit * inner;
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                return half4(i.col, _BaseColor.a);
            }
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
            Cull Back

            HLSLPROGRAM
            #pragma vertex ShadowVert
            #pragma fragment ShadowFrag
            #pragma multi_compile _ _CASTING_PUNCTUAL_LIGHT_SHADOW

            float3 _LightDirection;
            float3 _LightPosition;

            struct ShadowVaryings { float4 pos : SV_POSITION; };

            ShadowVaryings ShadowVert(SwayAttributes v)
            {
                ShadowVaryings o;
                float3 wp = TransformObjectToWorld(DeformOS(v.vertex.xyz, RootWorld()));
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
