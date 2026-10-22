Shader "Hidden/QuietCamp/WorldOpsMeadowBake"
{
    SubShader
    {
        Pass
        {
            Cull Off ZWrite On ZTest LEqual
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag
            #include "UnityCG.cginc"
            float4 _BakeRect,_BakeHeights;
            struct Attributes {float4 position:POSITION;float3 normal:NORMAL;float4 colour:COLOR;float2 response:TEXCOORD1;};
            struct Varyings {float4 position:SV_POSITION;float3 normal:TEXCOORD0;float4 colour:COLOR;float2 response:TEXCOORD1;float height:TEXCOORD2;};
            Varyings Vert(Attributes input)
            {
                Varyings output;
                float2 uv=(input.position.xz-_BakeRect.xy)/_BakeRect.zw;
                #if UNITY_UV_STARTS_AT_TOP
                uv.y=1-uv.y;
                #endif
                output.height=saturate((input.position.y-_BakeHeights.x)/(_BakeHeights.y-_BakeHeights.x));
                output.position=float4(uv*2-1,1-output.height,1);
                output.normal=input.normal;output.colour=input.colour;output.response=input.response;return output;
            }
            struct Output {float4 albedo:SV_Target0;float4 normalHeight:SV_Target1;};
            Output Frag(Varyings input)
            {
                Output output;output.albedo=float4(input.colour.rgb,saturate(input.response.x*min(input.response.y,3)/3));
                output.normalHeight=float4(normalize(input.normal)*.5+.5,input.height);return output;
            }
            ENDHLSL
        }
    }
}
