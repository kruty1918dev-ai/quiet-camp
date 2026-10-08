Shader "QuietCamp/UI/LeafFlight"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture",2D)="white" {}
        _Color ("Tint",Color)=(1,1,1,1)
        _Travel ("Travel",Float)=0
        _BreezeTime ("Breeze",Float)=0
        _LeafTint ("Leaf tint",Color)=(.35,.45,.38,1)
        _StencilComp ("Stencil Comparison",Float)=8
        _Stencil ("Stencil ID",Float)=0
        _StencilOp ("Stencil Operation",Float)=0
        _StencilWriteMask ("Stencil Write Mask",Float)=255
        _StencilReadMask ("Stencil Read Mask",Float)=255
        _ColorMask ("Color Mask",Float)=15
        [Toggle(UNITY_UI_ALPHACLIP)] _UseUIAlphaClip ("Use Alpha Clip",Float)=0
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" "PreviewType"="Plane" "CanUseSpriteAtlas"="True" }
        Stencil { Ref [_Stencil] Comp [_StencilComp] Pass [_StencilOp] ReadMask [_StencilReadMask] WriteMask [_StencilWriteMask] }
        Cull Off Lighting Off ZWrite Off ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha
        ColorMask [_ColorMask]
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 2.0
            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            #pragma multi_compile_local _ UNITY_UI_ALPHACLIP
            #include "UnityCG.cginc"
            #include "UnityUI.cginc"
            struct appdata { float4 vertex:POSITION;fixed4 color:COLOR;float4 leaf:TEXCOORD0;float4 shape:TEXCOORD1;float4 flight:TEXCOORD2;float drift:TEXCOORD3;UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct v2f { float4 vertex:SV_POSITION;fixed4 color:COLOR;float2 uv:TEXCOORD0;float2 local:TEXCOORD1;UNITY_VERTEX_OUTPUT_STEREO };
            sampler2D _MainTex;
            fixed4 _Color,_TextureSampleAdd;
            float4 _ClipRect;
            float _Travel,_BreezeTime;
            float4 _LeafTint;
            float smoothFlight(float t) { t=saturate(t);return t*t*t*(t*(t*6-15)+10); }
            v2f vert(appdata v)
            {
                v2f o;UNITY_SETUP_INSTANCE_ID(v);UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                float px=v.leaf.x,py=v.leaf.y,seed=v.leaf.z,variation=v.leaf.w;
                float unit=v.shape.x,layer=v.shape.y;
                float across=saturate((px-(v.flight.z-unit*1.8))/(v.flight.w-v.flight.z+unit*3.6));
                float incoming=smoothFlight((_Travel-(.10*across+.08*seed+layer*.035))/(.70+.06*variation));
                float outgoing=smoothFlight((_Travel-1-(.10*across+.18*variation+layer*.06))/(.70+.04*seed));
                float flight=1-incoming,moving=flight+outgoing;
                float length=unit*(2.20+variation*.48);
                float breeze=_BreezeTime*(1.15+variation*.55)+px/unit*.24+py/unit*.18;
                float flutter=breeze*(2.3+seed)+seed*6.283185307;
                // Original seeded drift is packed separately into the uv3 channel.
                float lift=sin(3.141592654*incoming)*flight+sin(3.141592654*outgoing)*outgoing;
                float2 centre=float2(lerp(v.flight.z-length*1.4-unit*seed,px,incoming)
                    +(v.flight.w+length*1.4+unit*variation-px)*outgoing,
                    py+v.drift*unit*2.3*moving+unit*(.45+seed)*lift);
                centre+=float2(sin(breeze)*.045,cos(breeze*.83+seed)*.065)*unit*(layer==0?.88:1.08);
                float angle=(-68+seed*136+(seed-.5)*42*flight+(variation-.5)*45*outgoing
                    +sin(breeze+seed*5)*5+sin(flutter)*4*moving)*.01745329252;
                float2 axis=float2(cos(angle),sin(angle)),normal=float2(-axis.y,axis.x);
                float scale=1-.16*moving+.025*sin(breeze+seed);
                float fold=.90+.07*sin(flutter)-.18*moving*abs(sin(flutter));
                float bend=.035*sin(breeze+seed*6)+.065*moving*sin(flutter);
                float2 local=axis*v.vertex.x+normal*(v.vertex.y*fold+v.shape.z*bend);
                if(v.flight.y>.5)centre+=float2(unit*.022,-unit*.035);
                v.vertex.xy=centre+local*length*scale*v.flight.x;
                float shade=lerp(1,lerp(.65,.91,fold)/.91*(.94+.06*cos(angle)),v.shape.w);
                o.local=v.vertex.xy;o.vertex=UnityObjectToClipPos(v.vertex);o.uv=0;
                o.color=v.color*_LeafTint;o.color.rgb*=shade*2;return o;
            }
            fixed4 frag(v2f i):SV_Target
            {
                fixed4 color=(tex2D(_MainTex,i.uv)+_TextureSampleAdd)*i.color;
                #ifdef UNITY_UI_CLIP_RECT
                color.a*=UnityGet2DClipping(i.local,_ClipRect);
                #endif
                #ifdef UNITY_UI_ALPHACLIP
                clip(color.a-.001);
                #endif
                return color;
            }
            ENDCG
        }
    }
}
