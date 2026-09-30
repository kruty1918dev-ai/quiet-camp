// Gentle vertex sway for grass/flower decor: tips drift in the breeze
// while roots stay planted. Cheap stylized lighting baked from normals.
Shader "QuietCamp/FoliageSway"
{
    Properties
    {
        _BaseColor ("Color", Color) = (0.4, 0.6, 0.35, 1)
        _SwayAmp ("Sway Amplitude", Range(0, 0.3)) = 0.05
        _SwayFreq ("Sway Frequency", Range(0, 6)) = 1.6
        _LightDir ("Fake Light Dir", Vector) = (0.4, 0.8, 0.3, 0)
        _Ambient ("Ambient", Range(0, 1)) = 0.55
    }
    SubShader
    {
        Tags { "Queue"="Geometry" "RenderType"="Opaque" }
        LOD 100

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            fixed4 _BaseColor;
            float _SwayAmp, _SwayFreq, _Ambient;
            float4 _LightDir;

            struct appdata { float4 vertex : POSITION; float3 normal : NORMAL; };
            struct v2f { float4 pos : SV_POSITION; fixed3 col : TEXCOORD0; };

            v2f vert(appdata v)
            {
                v2f o;
                float3 wp = mul(unity_ObjectToWorld, v.vertex).xyz;
                float3 pivot = mul(unity_ObjectToWorld, float4(0,0,0,1)).xyz;
                // Sway strength grows with height above the object pivot.
                float h = saturate((wp.y - pivot.y) / 0.6);
                float phase = wp.x * 1.7 + wp.z * 2.3;
                wp.x += sin(_Time.y * _SwayFreq + phase) * _SwayAmp * h;
                wp.z += cos(_Time.y * _SwayFreq * 0.83 + phase * 1.3) * _SwayAmp * 0.7 * h;
                o.pos = UnityWorldToClipPos(float4(wp, 1));
                // Cheap half-lambert vs fake sun for a bit of volume.
                float3 n = normalize(mul((float3x3)unity_ObjectToWorld, v.normal));
                float ndl = saturate(dot(n, normalize(_LightDir.xyz)) * 0.5 + 0.5);
                o.col = _BaseColor.rgb * (_Ambient + ndl * (1.0 - _Ambient));
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                return fixed4(i.col, _BaseColor.a);
            }
            ENDCG
        }
    }
    Fallback Off
}
