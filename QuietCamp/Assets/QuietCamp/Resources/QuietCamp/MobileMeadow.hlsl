#ifndef QC_MOBILE_MEADOW
#define QC_MOBILE_MEADOW
TEXTURE2D(_MeadowDetailMap); SAMPLER(sampler_MeadowDetailMap);
struct MeadowDetail { float tone; float height; float2 slope; float fade; };
MeadowDetail SampleMeadowDetail(float2 position)
{
    // One filtered sample. No parallax search, extra surfaces or transparent carpet.
    float2 uv=position*.38;
    float footprint=max(length(ddx(uv)),length(ddy(uv)))*256;
    float fade=1-smoothstep(3,14,footprint);
    float4 packed=SAMPLE_TEXTURE2D(_MeadowDetailMap,sampler_MeadowDetailMap,uv);
    MeadowDetail detail;detail.tone=lerp(.56,packed.g,fade);detail.height=lerp(.70,packed.r,fade);
    detail.slope=(packed.ba-.5)*fade;detail.fade=fade;return detail;
}
float3 DenseMeadowColor(float3 substrate,float3 low,float3 high,MeadowDetail detail,float pressure,float wear,float snow)
{
    float cover=(1-saturate(snow))*(1-saturate(wear))*(1-pressure*.8);
    float3 grass=lerp(low,high,saturate(.22+detail.tone*.90))*_MeadowTint.rgb;
    grass*=.72+detail.height*.38;
    // Low-frequency substrate colour breaks the repeated tile without another texture lookup.
    grass*=clamp(substrate/max(lerp(low,high,.5),float3(.08,.08,.08)),.82,1.12);
    return lerp(substrate,grass,cover*.94);
}
float3 DenseMeadowNormal(float3 normal,MeadowDetail detail,float pressure,float snow)
{
    return normalize(normal+float3(-detail.slope.x,0,-detail.slope.y)*.72*(1-pressure)*(1-snow));
}
#endif
