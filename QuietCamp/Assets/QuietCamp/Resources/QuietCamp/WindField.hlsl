#ifndef QUIET_CAMP_WIND_FIELD
#define QUIET_CAMP_WIND_FIELD
TEXTURE2D(_CampWindShelter);SAMPLER(sampler_CampWindShelter);
float4 _CampWindShelterRect;
float _CampWindCanopyHeight;
float4 _CampWindShelterInfo;
float4 CampWindAt(float3 rootWorld,float height,float2 direction,float strength,float seconds)
{
    float2 uv=(rootWorld.xz-_CampWindShelterRect.xy)/max(_CampWindShelterRect.zw,float2(.001,.001));
    float canopy=0;
    if(all(uv>=0)&&all(uv<=1))
    {
        // CPU grid positions correspond to the texture's texel centres.
        float2 textureUV=uv*_CampWindShelterInfo.x+_CampWindShelterInfo.y;
        canopy=SAMPLE_TEXTURE2D_LOD(_CampWindShelter,sampler_CampWindShelter,textureUV,0).r;
    }
    float top=max(.1,_CampWindCanopyHeight);
    float exposure=1-canopy*.72*exp(-max(0,height)/top);
    float phase=(dot(rootWorld.xz,direction)-seconds*1.5)*.62831853;
    float coherent=.86+.14*sin(phase)+.06*sin(phase*.47+rootWorld.x*.09);
    float turn=.11*sin(phase*.31+rootWorld.z*.13)*saturate(height/top);
    float2 localDirection=float2(direction.x*cos(turn)-direction.y*sin(turn),direction.x*sin(turn)+direction.y*cos(turn));
    return float4(localDirection,saturate(strength*exposure*coherent),0);
}
#endif
