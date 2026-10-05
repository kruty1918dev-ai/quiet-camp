Shader "QuietCamp/CozySky"
{
    Properties { _Zenith("Sky",Color)=(.45,.62,.69,1) _Horizon("Horizon",Color)=(.8,.83,.72,1) _Cloud("Cloudiness",Float)=0 _SunColor("Sun",Color)=(1,.9,.7,1) _SunDirection("Sun direction",Vector)=(0,1,0,0) }
    SubShader
    {
        Tags {"Queue"="Background" "RenderType"="Background" "PreviewType"="Skybox" "RenderPipeline"="UniversalPipeline"}
        Cull Off ZWrite Off
        Pass
        {
            HLSLPROGRAM
            #pragma target 2.0
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "CozySky.hlsl"
            CBUFFER_START(UnityPerMaterial)
            half4 _Zenith,_Horizon,_SunColor;float4 _SunDirection;float _Cloud;
            CBUFFER_END
            struct A{float4 p:POSITION;};struct V{float4 p:SV_POSITION;float3 d:TEXCOORD0;};
            V vert(A a){V v;v.p=TransformObjectToHClip(a.p.xyz);v.d=a.p.xyz;return v;}
            half4 frag(V v):SV_Target
            {
                return half4(CampSky(v.d,_Zenith.rgb,_Horizon.rgb,_Cloud,_SunDirection.xyz,_SunColor.rgb),1);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
