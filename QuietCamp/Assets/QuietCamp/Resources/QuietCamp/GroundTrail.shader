Shader "QuietCamp/GroundTrail"
{
    Properties
    {
        _Length("Path length",Float)=9 _AuthoredNetwork("Authored walking network",Float)=0
        _SoilDark("Worn centre dark",Color)=(.26,.275,.16,1) _SoilLight("Worn centre light",Color)=(.37,.35,.22,1)
        _MossDark("Seasonal margin dark",Color)=(.28,.40,.22,1) _MossLight("Seasonal margin light",Color)=(.40,.51,.28,1)
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Geometry+10" "RenderType"="Transparent" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off
        Pass
        {
            Tags {"LightMode"="UniversalForward"}
            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            CBUFFER_START(UnityPerMaterial)
                float _Length, _AuthoredNetwork;
                half4 _SoilDark,_SoilLight,_MossDark,_MossLight;
            CBUFFER_END
            struct A {float4 positionOS:POSITION;float2 uv:TEXCOORD0;float4 joins:TEXCOORD1;};
            struct V {float4 positionCS:SV_POSITION;float3 world:TEXCOORD0;float2 uv:TEXCOORD1;float4 joins:TEXCOORD2;};
            V Vert(A a){V v;v.world=TransformObjectToWorld(a.positionOS.xyz);v.positionCS=TransformWorldToHClip(v.world);v.uv=a.uv;v.joins=a.joins;return v;}
            float Hash(float2 p){return frac(sin(dot(p,float2(127.1,311.7)))*43758.5453);}
            float Noise(float2 p)
            {
                float2 cell=floor(p),t=frac(p);t=t*t*(3-2*t);
                return lerp(lerp(Hash(cell),Hash(cell+float2(1,0)),t.x),lerp(Hash(cell+float2(0,1)),Hash(cell+1),t.x),t.y);
            }
            half4 Frag(V v):SV_Target
            {
                // Foot-worn soil, with grass islands and irregular eroded edges.
                // Keep luminance close to the meadow instead of painting a pale road over it.
                float patches=Noise(v.world.xz*1.7+13);
                float detail=Noise(v.world.xz*8);
                float edge=abs(v.uv.x)+(patches-.5)*.45+(detail-.5)*.1;
                float border=1-smoothstep(.58,.98,edge);
                float margin=abs(v.uv.x);
                if(_AuthoredNetwork>.5)
                {
                    // A cell owns the union of its centre and all connected arms.
                    // Adjacent cells only meet at their boundaries: transparency
                    // is applied once, even at a four-way junction.
                    float2 p=v.uv;
                    float signedEdge=length(p)-.31;
                    if(v.joins.x>.5)signedEdge=min(signedEdge,max(abs(p.x)-.31,-p.y));
                    if(v.joins.y>.5)signedEdge=min(signedEdge,max(abs(p.y)-.31,-p.x));
                    if(v.joins.z>.5)signedEdge=min(signedEdge,max(abs(p.x)-.31,p.y));
                    if(v.joins.w>.5)signedEdge=min(signedEdge,max(abs(p.y)-.31,p.x));
                    margin=saturate((signedEdge+.31)/.31);
                    border=1-smoothstep(.58,.98,margin+(patches-.5)*.45+(detail-.5)*.1);
                }
                float wear=lerp(.38,.64,smoothstep(.22,.70,patches));
                float alpha=border*wear;
                if(_AuthoredNetwork<.5)alpha*=smoothstep(-.36,-.10,v.uv.y)*(1-smoothstep(_Length-1.1,_Length,v.uv.y));
                half3 earth=lerp(_SoilDark.rgb,_SoilLight.rgb,patches*.7+detail*.2);
                // Grass creeps into the less travelled margins, sharing the forest's palette.
                half3 moss=lerp(_MossDark.rgb,_MossLight.rgb,patches);
                earth=lerp(earth,moss,smoothstep(.20,.80,margin)*.7);
                Light sun=GetMainLight(TransformWorldToShadowCoord(v.world));
                half3 lighting=SampleSH(half3(0,1,0))+sun.color*saturate(sun.direction.y)*sun.shadowAttenuation;
                return half4(earth*max(lighting,half3(.16,.16,.16)),alpha);
            }
            ENDHLSL
        }
    }
}
