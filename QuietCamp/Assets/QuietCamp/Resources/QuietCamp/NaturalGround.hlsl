#ifndef QC_NATURAL_GROUND
#define QC_NATURAL_GROUND
// Continuous metre-scale relief; no triangle boundaries in colour or lighting.
// No texture fetches or subpixel grain; terrain silhouette and collision stay authored.
float GroundHash(float2 p)
{
    return frac(sin(dot(p,float2(127.1,311.7)))*43758.5453);
}
float GroundNoise(float2 p)
{
    float2 a=floor(p),f=frac(p);f=f*f*(3-2*f);
    return lerp(lerp(GroundHash(a),GroundHash(a+float2(1,0)),f.x),
        lerp(GroundHash(a+float2(0,1)),GroundHash(a+1),f.x),f.y);
}
float3 GroundRelief(float2 p,float scale)
{
    float2 q=float2(p.x*.86+p.y*.51,p.y*.86-p.x*.51)*scale;
    float2 a=floor(q),f=frac(q);
    float h0=GroundHash(a),h1=GroundHash(a+float2(1,0)),h2=GroundHash(a+float2(0,1)),h3=GroundHash(a+1);
    float2 blend=f*f*(3-2*f),derivative=6*f*(1-f);
    float2 slope=float2(lerp(h1-h0,h3-h2,blend.y),lerp(h2-h0,h3-h1,blend.x))*derivative;
    float height=lerp(lerp(h0,h1,blend.x),lerp(h2,h3,blend.x),blend.y);
    slope=float2(slope.x*.86-slope.y*.51,slope.x*.51+slope.y*.86)*scale;
    return float3(slope,height);
}
float3 NaturalGroundNormal(float3 authored,float2 p,float4 finish,float wet,float snow)
{
    float3 relief=GroundRelief(p,finish.y);
    relief*=finish.z*.45*lerp(1,.85,saturate(wet));
    float3 snowRelief=GroundRelief(p,.38)*.22+GroundRelief(p,1.4)*.025;
    relief=lerp(relief,snowRelief,saturate(snow));
    float weight=smoothstep(.4,.9,authored.y);
    return normalize(authored+float3(-relief.x,0,-relief.y)*weight);
}
float3 SettledSnowColor(float2 p)
{
    float broad=GroundNoise(p*.18+21);
    float powder=GroundNoise(p*1.1+73);
    return lerp(float3(.68,.77,.87),float3(.96,.98,1),.25+broad*.7)*(.985+powder*.015);
}
float3 NaturalGroundColor(float2 p,float3 low,float3 high,float3 soil,float4 finish,float wet)
{
    float broad=GroundNoise(p*finish.x+17);
    float medium=GroundNoise(p*.83+31);
    float patches=saturate(.25+broad*.55+medium*.08);
    float earth=smoothstep(.42,.86,GroundNoise(p*.19+61));
    float moss=smoothstep(.35,.85,GroundNoise(p*.16-23));
    float3 color=lerp(low,high,patches);
    color=lerp(color,soil,earth*.27);
    color=lerp(color,low*float3(.94,1,.96),moss*.14);
    color*=lerp(.99,1.01,GroundNoise(p*finish.y));
    return color*(1-saturate(wet)*(.05+earth*.07));
}
float3 MeadowSubstrate(float3 color,float2 p,float4 extent)
{
    // The same substrate extends through the whole landscape, without a board-shaped tint.
    return color;
}
#endif
