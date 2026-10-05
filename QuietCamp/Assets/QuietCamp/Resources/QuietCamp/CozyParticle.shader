Shader "QuietCamp/CozyParticle"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _Tint ("Tint", Color) = (1,1,1,1)
        _Glow ("Gentle HDR glow", Range(0,2)) = 0
        _SoftDepth ("Surface fade enabled", Float) = 0
        _SoftDistance ("Surface fade distance", Range(0.01,1)) = 0.12
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Blend One OneMinusSrcAlpha
        ZWrite Off
        Cull Off
        Pass
        {
            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                half4 _Tint;
                half _Glow, _SoftDepth;
                float _SoftDistance;
            CBUFFER_END
            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; half4 color : COLOR; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; half4 color : COLOR; float eyeDepth : TEXCOORD1; };
            Varyings Vert(Attributes input)
            {
                Varyings output;
                float3 world = TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS = TransformWorldToHClip(world);
                output.eyeDepth = -TransformWorldToView(world).z;
                output.uv = TRANSFORM_TEX(input.uv, _MainTex);
                output.color = input.color * _Tint;
                return output;
            }
            half4 Frag(Varyings input) : SV_Target
            {
                half4 tex = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv);
                half alpha = tex.a * input.color.a;
                clip(alpha - 0.001h);
                // Uniform branch: Low never samples depth or asks the camera
                // for an extra texture. Balanced/High reuse the forest depth.
                if (_SoftDepth > 0.5h)
                {
                    float raw = SampleSceneDepth(GetNormalizedScreenSpaceUV(input.positionCS));
                    float sceneDepth;
                    if (unity_OrthoParams.w > 0.5)
                    {
                        #if UNITY_REVERSED_Z
                            raw = 1.0 - raw;
                        #endif
                        sceneDepth = lerp(_ProjectionParams.y, _ProjectionParams.z, raw);
                    }
                    else sceneDepth = LinearEyeDepth(raw, _ZBufferParams);
                    alpha *= saturate((sceneDepth - input.eyeDepth) / max(0.01, _SoftDistance));
                }
                // Premultiplied output keeps transparent edges free of dark
                // fringes; only explicitly glowing pools exceed unit radiance.
                return half4(tex.rgb * input.color.rgb * (1.0h + _Glow) * alpha, alpha);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
