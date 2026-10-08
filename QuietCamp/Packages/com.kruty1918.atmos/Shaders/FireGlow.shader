// Soft radial fire glow: additive quad lying under the campfire.
// Pulses gently via time; pure shader, mobile-cheap, no textures.
Shader "Atmos/FireGlow"
{
    Properties
    {
        _Color ("Glow Color", Color) = (1.0, 0.55, 0.18, 0.6)
        _Intensity ("Intensity", Range(0, 2)) = 1.0
        _PulseSpeed ("Pulse Speed", Range(0, 8)) = 2.2
        _PulseAmp ("Pulse Amplitude", Range(0, 1)) = 0.18
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

            fixed4 _Color;
            float _Intensity, _PulseSpeed, _PulseAmp, _Seed;

            struct appdata { float4 vertex : POSITION; float2 uv : TEXCOORD0; };
            struct v2f { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; };

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float2 c = i.uv - 0.5;
                float d = length(c) * 2.0;
                float pulse = 1.0 + _PulseAmp * sin(_Time.y * _PulseSpeed + _Seed)
                            + 0.5 * _PulseAmp * sin(_Time.y * _PulseSpeed * 2.7 + _Seed * 1.3);
                float falloff = saturate(1.0 - d);
                falloff = falloff * falloff * (3.0 - 2.0 * falloff); // smoothstep
                float a = falloff * _Intensity * pulse;
                return fixed4(_Color.rgb, _Color.a * a);
            }
            ENDCG
        }
    }
    Fallback Off
}
