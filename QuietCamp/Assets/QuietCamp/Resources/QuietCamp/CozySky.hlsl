#ifndef QUIET_CAMP_COZY_SKY_INCLUDED
#define QUIET_CAMP_COZY_SKY_INCLUDED

// One small procedural sky for the skybox, probes and the Low water fallback.
// No textures, fullscreen passes or separate camera are needed for Low.
half3 CampSky(float3 direction, half3 zenith, half3 horizon, float cloud,
    float3 sunDirection, half3 sunColor)
{
    direction = normalize(direction);
    half3 color = lerp(horizon, zenith, smoothstep(-.12, .85, direction.y));
    float patches = sin(direction.x * 7 + direction.z * 3) * .23
        + sin(direction.z * 11 - direction.x * 4) * .17 + .48;
    float cover = smoothstep(.28, .72, patches) * saturate(cloud);
    half3 clouds = lerp(horizon, zenith, .35) * lerp(1.12, .93, cloud);
    color = lerp(color, clouds, cover);
    float sun = pow(saturate(dot(direction, sunDirection)), 96) * (1 - cloud * .95);
    return color + sunColor * sun * .25;
}

#endif
