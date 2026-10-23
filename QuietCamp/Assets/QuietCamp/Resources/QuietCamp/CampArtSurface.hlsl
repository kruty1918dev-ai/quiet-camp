// Shared surface lighting for native meshes and the staged height-field experiment.
            #include "NaturalGround.hlsl"
            #include "MobileMeadow.hlsl"
            float ArtHash(float2 p) { return frac(sin(dot(p,float2(127.1,311.7)))*43758.5453); }
            float ArtNoise(float2 p)
            {
                float2 i=floor(p), f=frac(p); f=f*f*(3-2*f);
                return lerp(lerp(ArtHash(i),ArtHash(i+float2(1,0)),f.x),lerp(ArtHash(i+float2(0,1)),ArtHash(i+1),f.x),f.y);
            }
            half4 CampArtColor(Varyings input)
            {
                float3 normal=normalize(input.normalWS);
                MeadowDetail meadow=(MeadowDetail)0;
                if(_ArtGround>.5)
                {
                    float2 p=input.positionWS.xz;float4 use=CampUse(p);
                    normal=NaturalGroundNormal(normal,p,_GroundFinish,_CampClimate.x+use.a,_CampClimate.y);
                    if(_MobileMeadow>.5){meadow=SampleMeadowDetail(p);normal=DenseMeadowNormal(normal,meadow,use.g,_CampClimate.y);}
                }
                if(_ArtMeadow>.5)normal=normalize(lerp(float3(0,1,0),normal,.12));
                Light sun=GetMainLight(TransformWorldToShadowCoord(input.positionWS));
                float diffuse=_ArtGrass>.5?abs(dot(normal,sun.direction)):saturate(dot(normal,sun.direction));
                float3 lighting=_ArtAmbient.rgb*(.8+.2*abs(normal.y))+sun.color*((diffuse+.22*saturate(-dot(normal,sun.direction)))*sun.shadowAttenuation);
                // Grass receives the same weather-dependent light as its surroundings.
                // A fixed green light floor erased shadows and glowed through overcast weather.
                if(_ArtGrass>.5)
                {
                    // Thin leaves scatter daylight; retain the preset's light colour in shade.
                    float leafDiffuse=.32+.68*diffuse;
                    lighting=_ArtAmbient.rgb*1.35+sun.color*leafDiffuse*lerp(.16,1,sun.shadowAttenuation);
                }
                float3 baseColor=input.color.rgb;
                if(_ArtRiverDetail>.5)
                {
                    float2 p=input.positionWS.xz;
                    float wet=max(_CampClimate.x*.55,1-smoothstep(_ArtRiverWaterline-.02,_ArtRiverWaterline+.16,input.positionWS.y));
                    baseColor*=1-wet*.22;
                    float upward=smoothstep(.15,.72,normal.y);
                    float aboveWater=smoothstep(_ArtRiverWaterline+.015,_ArtRiverWaterline+.07,input.positionWS.y);
                    float snow=saturate(_CampClimate.y)*upward*aboveWater*smoothstep(.2,.7,ArtNoise(p*3.1+23));
                    baseColor=lerp(baseColor,float3(.83,.89,.92),snow);
                }
                if(_ArtGrass>.5)
                {
                    // Broad, coherent groups and darker roots, rather than noise on every blade.
                    float green=step(baseColor.r*1.05,baseColor.g)*step(baseColor.b*1.1,baseColor.g);
                    float rootToTip=lerp(_ArtMeadow>.5 ? .82 : .90,_ArtMeadow>.5?1.02:1.04,sqrt(saturate(input.response.x)));
                    float patchTone=lerp(.94,1.04,ArtNoise(input.positionWS.xz*.24+9));
                    baseColor*=lerp(float3(1,1,1),float3(.94,1,1.08)*rootToTip*patchTone,green);
                    float4 trampled=CampUse(input.positionWS.xz);
                    baseColor=lerp(baseColor,float3(.33,.37,.20),trampled.r*.65);
                }
                if(_ArtGround>.5)
                {
                    float2 p=input.positionWS.xz;float4 use=CampUse(p);
                    float3 natural=MeadowSubstrate(NaturalGroundColor(p,_GroundLow.rgb,_GroundHigh.rgb,_SoilTone.rgb,_GroundFinish,_CampClimate.x),p,_MeadowExtent);
                    if(_MobileMeadow>.5)natural=DenseMeadowColor(natural,_GroundLow.rgb,_GroundHigh.rgb,meadow,use.g,use.r,_CampClimate.y);
                    // Preserve authored scars and banks without retaining a differently painted puzzle rectangle.
                    float fieldDistance=max(abs(p.x)-_CampFieldSize.x,abs(p.y)-_CampFieldSize.y);
                    float undisturbed=1-smoothstep(.3,2.0,fieldDistance+(ArtNoise(p*.8f)-.5)*.45);
                    baseColor=lerp(baseColor,natural,lerp(.94,1,undisturbed));
                    float cover=saturate(_CampClimate.y);
                    float drift=ArtNoise(p*.23+71)*.7+ArtNoise(p*.81-29)*.3;
                    float snow=cover>=.999?1:smoothstep(1-cover-.12,1-cover+.12,drift)*smoothstep(0,.12,cover);
                    snow*=smoothstep(.2,.75,normal.y);
                    baseColor=lerp(baseColor,SettledSnowColor(p)*lerp(float3(1,1,1),input.color.rgb,saturate(_ArtSnowSurface)),snow);
                    // Snow scatters the overcast sky light; shadows remain directional,
                    // without turning the whole blizzard ground into dark grey earth.
                    float skyFacing=.78+.22*saturate(normal.y);
                    float snowDiffuse=.55+.45*saturate(dot(normal,normalize(sun.direction+float3(0,.25,0))));
                    float skyLuma=dot(_ArtAmbient.rgb,float3(.2126,.7152,.0722));
                    float3 snowSky=lerp(_ArtAmbient.rgb,skyLuma*float3(.94,1,1.06),.8);
                    float3 snowLight=(snowSky*1.9*skyFacing+sun.color*.65*snowDiffuse)*lerp(.76,1,sun.shadowAttenuation);
                    lighting=lerp(lighting,snowLight,snow);
                    baseColor=lerp(baseColor,lerp(natural*.92,_SoilTone.rgb,use.r),use.g*(1-snow)*.55);
                    baseColor=lerp(baseColor,_SoilTone.rgb*(1-_CampClimate.x*.23),use.r*(1-snow)*(.67+ArtNoise(p*2.8)*.23));
                    baseColor=lerp(baseColor,float3(.68,.78,.86),use.b*snow*.42);
                    float churn=ArtNoise(p*7+5)*.6+ArtNoise(p*3)*.4;
                    baseColor*=1-use.a*(.04+churn*.09)*(1-snow);
                    // Small irregular wet depressions emerge only after substantial real traffic.
                    float pool=use.a*smoothstep(.61,.79,ArtNoise(p*2.3+91))*(1-snow);
                    baseColor=lerp(baseColor,float3(.30,.37,.36),pool*.60);
                }
                if(_ArtGround<.5 && _ArtWater<.5 && _WeatherSnowCover>0)
                {
                    float retention=saturate(input.snowCatch);
                    float snow=smoothstep(lerp(.12,.05,retention),lerp(.62,.33,retention),normal.y)*_WeatherSnowCover*saturate(input.color.a)*WeatherSkyExposure(input.restWS,normal);
                    snow*=step(0,dot(normal,GetWorldSpaceNormalizeViewDir(input.positionWS)));
                    baseColor=lerp(baseColor,SettledSnowColor(input.positionWS.xz),snow);
                    float sky=dot(_ArtAmbient.rgb,float3(.2126,.7152,.0722));
                    float3 snowLight=lerp(_ArtAmbient.rgb,sky*float3(.94,1,1.06),.8)*1.9
                        +sun.color*(.35+.30*saturate(dot(normal,sun.direction)));
                    lighting=lerp(lighting,snowLight*lerp(.76,1,sun.shadowAttenuation),snow);
                }
                float3 color=baseColor*lighting;
                if(_ArtGround>.5)
                {
                    float4 use=CampUse(input.positionWS.xz);float2 p=input.positionWS.xz;
                    float pools=use.a*smoothstep(.61,.79,ArtNoise(p*2.3+91))*(1-_CampClimate.y);
                    float spec=pow(saturate(dot(reflect(-sun.direction,normal),GetWorldSpaceNormalizeViewDir(input.positionWS))),22);
                    color+=(sun.color*spec*.24*sun.shadowAttenuation+float3(.07,.10,.11))*pools;
                }
                if(_QCWeather.w>.5 && _ArtWater<.5)
                {
                    float exposed=saturate(input.color.a);
                    float wetness=_QCWeather.z*(_ArtGround>.5?1-saturate(_CampClimate.y):1);
                    color*=1-wetness*.18*exposed;
                    float wetSpec=pow(saturate(dot(reflect(-sun.direction,normal),GetWorldSpaceNormalizeViewDir(input.positionWS))),24);
                    color+=sun.color*wetSpec*wetness*.07*exposed*sun.shadowAttenuation;
                }
                if(_ArtGround>.5)
                {
                    float grainWeight=1-smoothstep(.12,.45,max(length(ddx(input.positionWS.xz)),length(ddy(input.positionWS.xz)))*6);
                    float exposedEarth=1-saturate(_CampClimate.y);
                    color*=lerp(1,.90+ArtNoise(input.positionWS.xz*.43)*.15+ArtNoise(input.positionWS.xz*2.4)*.06,exposedEarth);
                    color*=1+(ArtNoise(input.positionWS.xz*7)-.5)*.06*grainWeight*exposedEarth;
                }
                if(_ArtWater>.5)
                {
                    float wave=sin(input.positionWS.x*4.5+input.positionWS.z*2.1+_Time.y*.5)*sin(input.positionWS.z*7-_Time.y*.3);
                    color*=.85+ArtNoise(input.positionWS.xz*2.7)*.29+wave*.045;
                    float spec=pow(saturate(dot(reflect(-sun.direction,normal),GetWorldSpaceNormalizeViewDir(input.positionWS))),36);
                    color+=sun.color*spec*.12*sun.shadowAttenuation;
                }
                return half4(color,1);
            }
