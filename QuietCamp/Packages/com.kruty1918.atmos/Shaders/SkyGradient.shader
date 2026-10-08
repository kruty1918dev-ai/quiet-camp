// Procedural calm-sky skybox : vertical zenith→horizon→ground
// gradient, a soft sun disc with halo, and optional night stars.
// Rendered as a scene skybox (Background queue, writes at far plane).
Shader "Atmos/SkyGradient"
{
    Properties
    {
        _ZenithColor ("Zenith Color", Color) = (0.45, 0.66, 0.86, 1)
        _HorizonColor ("Horizon Color", Color) = (0.98, 0.88, 0.70, 1)
        _GroundColor ("Ground Color", Color) = (0.55, 0.62, 0.50, 1)
        _SunColor ("Sun Color", Color) = (1.0, 0.85, 0.6, 1)
        _SunDir ("Sun Direction", Vector) = (0.3, 0.6, 0.3, 0)
        _SunSize ("Sun Size", Range(0.001, 0.3)) = 0.06
        _SunHalo ("Sun Halo", Range(0, 1)) = 0.35
        _HorizonFalloff ("Horizon Falloff", Range(0.2, 8)) = 1.6
        _Stars ("Stars", Range(0, 1)) = 0
    }
    SubShader
    {
        Tags { "RenderType"="Background" "Queue"="Background" "PreviewType"="Skybox" }
        Cull Off
        ZWrite Off
        ZTest LEqual
        LOD 100

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            fixed4 _ZenithColor;
            fixed4 _HorizonColor;
            fixed4 _GroundColor;
            fixed4 _SunColor;
            float4 _SunDir;
            float _SunSize;
            float _SunHalo;
            float _HorizonFalloff;
            float _Stars;

            struct appdata { float4 vertex : POSITION; };
            struct v2f
            {
                float4 pos : SV_POSITION;
                float3 dir : TEXCOORD0;
            };

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                // Pin to far plane; reversed-Z platforms put far at z=0.
                // UNITY_REVERSED_Z is not defined on every target (GLES3), so
                // select the convention at compile time instead of lerping.
#if defined(UNITY_REVERSED_Z) && UNITY_REVERSED_Z
                o.pos.z = 0.00001f;
#else
                o.pos.z = o.pos.w * 0.99999f;
#endif
                o.dir = v.vertex.xyz;
                return o;
            }

            // Cheap stable hash for star cells.
            float hash21(float2 p)
            {
                p = frac(p * float2(234.34, 435.345));
                p += dot(p, p + 34.23);
                return frac(p.x * p.y);
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float3 dir = normalize(i.dir);
                float h = dir.y;

                // Sky: horizon->zenith; below horizon fades to ground color.
                float sky = saturate(h);
                float horizonBand = pow(1.0 - sky, _HorizonFalloff);
                fixed3 col = lerp(_ZenithColor.rgb, _HorizonColor.rgb, horizonBand);
                float below = saturate(-h * 3.0);
                col = lerp(col, _GroundColor.rgb, below);

                // Sun disc + soft halo.
                float3 sunDir = normalize(_SunDir.xyz);
                float sunDot = saturate(dot(dir, sunDir));
                float disc = smoothstep(1.0 - _SunSize, 1.0 - _SunSize * 0.55, sunDot);
                float halo = pow(sunDot, 24.0) * _SunHalo;
                col += _SunColor.rgb * (disc + halo);

                // Stars: only where the sky is dark (zenith weight) and _Stars > 0.
                if (_Stars > 0.001)
                {
                    float2 grid = dir.xz / max(0.15f, dir.y) * 60.0;
                    float2 cell = floor(grid);
                    float rnd = hash21(cell);
                    float2 starPos = float2(hash21(cell + 7.13), hash21(cell + 3.71));
                    float d = length(frac(grid) - starPos);
                    float star = step(0.955, rnd) * smoothstep(0.09, 0.0, d);
                    float twinkle = 0.75 + 0.25 * sin(_Time.y * 1.7 + rnd * 40.0);
                    col += star * twinkle * _Stars * sky * (1.0 - below) * float3(1, 1, 0.95);
                }

                return fixed4(col, 1.0);
            }
            ENDCG
        }
    }
    Fallback Off
}
