#ifndef QC_CAMP_SURFACE_USE
#define QC_CAMP_SURFACE_USE
TEXTURE2D(_CampUse); SAMPLER(sampler_CampUse);
float4 _CampUseBounds, _CampClimate;
float4 CampUse(float2 position)
{
    float2 uv=(position-_CampUseBounds.xy)*_CampUseBounds.zw;
    float inside=step(0,uv.x)*step(uv.x,1)*step(0,uv.y)*step(uv.y,1)*_CampClimate.w*step(.00001,_CampUseBounds.z)*step(.00001,_CampUseBounds.w);
    return SAMPLE_TEXTURE2D_LOD(_CampUse,sampler_CampUse,uv,0)*inside;
}
float3 CampPressure(float3 position,float response)
{
    float pressure=CampUse(position.xz).g;
    // Root-pinned low plants respond; crowns, props, terrain and tent cloth keep their geometry.
    if(position.y<1.4&&response>0)position.y-=min(max(position.y-.018,0),response*.85)*pressure;
    return position;
}
#endif
