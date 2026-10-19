#ifndef QC_WEATHER_SKY
#define QC_WEATHER_SKY
TEXTURE2D(_WeatherSkyHeight); SAMPLER(sampler_WeatherSkyHeight);
// Parameters live in each shader's shared UnityPerMaterial buffer.
float WeatherSkyExposure(float3 world,float3 normal)
{
    if(_WeatherSkyBounds.z<=0)return 1;
    float3 local=mul(_WeatherSkyToLocal,float4(world,1)).xyz;
    float2 uv=(local.xz-_WeatherSkyBounds.xy)*_WeatherSkyBounds.zw;
    if(any(uv<0)||any(uv>1))return 1;
    float roof=SAMPLE_TEXTURE2D_LOD(_WeatherSkyHeight,sampler_WeatherSkyHeight,uv,0).r;
    float2 centre=(floor(uv*_WeatherSkyGrid.xy)+.5)/_WeatherSkyGrid.xy;
    float2 offset=(centre-uv)/_WeatherSkyBounds.zw;
    float3 localNormal=normalize(mul((float3x3)_WeatherSkyNormalToLocal,normal));
    // Compare at the sampled texel's centre on this exact surface plane. This avoids
    // falsely sheltering steep conifer/roof facets with their own higher neighbour.
    float surface=local.y-dot(localNormal.xz,offset)/max(.05,localNormal.y);
    return 1-smoothstep(.08,.22,roof-surface);
}
#endif
