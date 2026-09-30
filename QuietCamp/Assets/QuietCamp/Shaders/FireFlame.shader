// Procedural campfire flame: upright quad with a wavy teardrop body.
// Additive blend, animated by time; no textures, no particles needed.
Shader "QuietCamp/FireFlame"
{
    Properties
    {
        _CoreColor ("Core Color", Color) = (1.0, 0.9, 0.45, 1)
        _EdgeColor ("Edge Color", Color) = (1.0, 0.5, 0.12, 1)
        _Height ("Flame Height", Range(0.2, 3)) = 1.0
        _Speed ("Flicker Speed", Range(0, 10)) = 4.0
        _Seed ("Seed", Float) = 0
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Blend SrcAlpha One
        ZWrite Off
        Cull Off
        LOD 100

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            fixed4 _CoreColor, _EdgeColor;
            float _Height, _Speed, _Seed;

            struct appdata { float4 vertex : POSITION; float2 uv : TEXCOORD0; };
            struct v2f { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; };

            v2f vert(appdata v)
            {
                v2f o;
                float3 p = v.vertex;
                p.y *= _Height;
                o.pos = UnityObjectToClipPos(float4(p, 1));
                o.uv = v.uv;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float t = _Time.y * _Speed + _Seed;
                // Waver increases toward the tip.
                float sway = sin(i.uv.y * 9.0 + t) * 0.06
                           + sin(i.uv.y * 17.0 - t * 1.7) * 0.03;
                float x = i.uv.x - 0.5 + sway * i.uv.y;
                // Teardrop: wide at base, narrows to a point at the top.
                float halfW = 0.34 * (1.0 - i.uv.y * 0.82) * (0.75 + 0.25 * sin(t * 1.3));
                float body = 1.0 - smoothstep(halfW * 0.55, halfW, abs(x));
                float fadeTop = 1.0 - smoothstep(0.72, 1.0, i.uv.y);
                float fadeBase = smoothstep(0.0, 0.12, i.uv.y);
                float a = body * fadeTop * fadeBase;
                fixed3 col = lerp(_CoreColor.rgb, _EdgeColor.rgb,
                    saturate(i.uv.y * 0.9 + abs(x) * 2.4));
                // Inner core is hottest.
                col += _CoreColor.rgb * body * (1.0 - i.uv.y) * 0.9;
                return fixed4(col, a);
            }
            ENDCG
        }
    }
    Fallback Off
}
