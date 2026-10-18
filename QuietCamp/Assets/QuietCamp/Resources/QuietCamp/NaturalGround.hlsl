#ifndef QC_NATURAL_GROUND
#define QC_NATURAL_GROUND
// A metre-scale triangular surface: continuous heights, one gentle normal per facet.
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
float3 GroundFacet(float2 p,float scale)
{
    // Skew and rotate so the facets cannot read as the gameplay grid.
    float2 q=float2(p.x*.86+p.y*.51,p.y*.86-p.x*.51)*scale;
    float2 a=floor(q),f=frac(q);
    float h0=GroundHash(a),h1=GroundHash(a+float2(1,0)),h2=GroundHash(a+float2(0,1)),h3=GroundHash(a+1);
    bool lower=f.x+f.y<1;
    float2 slope=lower?float2(h1-h0,h2-h0):float2(h3-h2,h3-h1);
    float height=lower?h0+dot(slope,f):h3+dot(slope,f-1);
    slope=float2(slope.x*.86-slope.y*.51,slope.x*.51+slope.y*.86)*scale;
    return float3(slope,height);
}
float3 NaturalGroundNormal(float3 authored,float2 p,float4 finish,float wet,float snow)
{
    float3 facet=GroundFacet(p,finish.y);
    float softness=lerp(1,.40,saturate(snow))*lerp(1,.85,saturate(wet));
    float weight=smoothstep(.4,.9,authored.y)*finish.z*softness;
    return normalize(authored+float3(-facet.x,0,-facet.y)*weight);
}
float3 NaturalGroundColor(float2 p,float3 low,float3 high,float3 soil,float4 finish,float wet)
{
    float broad=GroundNoise(p*finish.x+17);
    float medium=GroundNoise(p*.83+31);
    float patches=saturate((broad*.78+medium*.22-.5)*1.8+.5);
    float earth=smoothstep(.51,.75,GroundNoise(p*.46+61));
    float moss=smoothstep(.45,.74,GroundNoise(p*.31-23));
    float3 color=lerp(low,high,patches);
    color=lerp(color,soil,earth*.60);
    color=lerp(color,low*float3(.85,1,.91),moss*.28);
    float facet=GroundFacet(p,finish.y).z;
    color*=lerp(.97,1.03,facet);
    return color*(1-saturate(wet)*(.05+earth*.07));
}
float3 MeadowSubstrate(float3 color,float2 p,float4 extent)
{
    float edge=max(abs(p.x)-extent.x,abs(p.y)-extent.y);
    float fringe=max(.1,extent.z);
    float cover=(1-smoothstep(-.2,fringe,edge+(GroundNoise(p*.6+13)-.5)*.9))*extent.w;
    return color*lerp(float3(1,1,1),float3(.86,.94,.85),cover);
}
#endif
