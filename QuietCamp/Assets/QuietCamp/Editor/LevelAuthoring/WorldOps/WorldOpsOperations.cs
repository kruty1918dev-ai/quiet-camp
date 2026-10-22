using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using QuietCamp.Infrastructure;
using UnityEngine;

namespace QuietCamp.Editor.LevelAuthoring.WorldOps
{
    public static class WorldOpsOperations
    {
        public static readonly string[] Kinds={"audit","camera.frame","ground.refine","vegetation.refine","vegetation.heightfield-preview","vegetation.redistribute","vegetation.scale","vegetation.preset","building.move","building.ground","building.refresh-model","model.age","model.simplify","path.upsert","path.remove","atmosphere.preset","river.edit","terrain.stamp"};
        public static void Validate(WorldRecipe recipe)
        {
            if(recipe==null||recipe.version!=1||string.IsNullOrWhiteSpace(recipe.id)||recipe.maxRepairRounds<1||recipe.maxRepairRounds>5)throw new ArgumentException("Invalid WorldOps recipe/version/repair budget.");
            if(recipe.previewOnly&&(!recipe.capture||recipe.autoApply))throw new ArgumentException("Preview-only recipes require capture and cannot auto-apply.");
            if(recipe.levelIds==null||recipe.orders==null)throw new ArgumentException("Level selections must be arrays.");
            if(recipe.criteria==null||recipe.criteria.profiles==null||recipe.criteria.profiles.Length==0||recipe.criteria.profiles.Any(p=>p!="portrait"&&p!="landscape"))throw new ArgumentException("Expected portrait/landscape capture profiles.");
            if(recipe.operations==null||recipe.operations.Any(o=>o==null||string.IsNullOrWhiteSpace(o.id)||!Kinds.Contains(o.kind)||o.parameters==null||o.selector==null)||recipe.operations.Select(o=>o.id).Distinct().Count()!=recipe.operations.Length)throw new ArgumentException("Operations need unique IDs and allow-listed kinds.");
            if(recipe.operations.Any(o=>o.kind=="vegetation.heightfield-preview")&&!recipe.previewOnly)throw new ArgumentException("Experimental height fields require previewOnly; publication is not supported.");
            if(recipe.operations.Any(o=>o.selector.ids==null||o.selector.modelIds==null||o.selector.roles==null))throw new ArgumentException("Object selectors must use arrays.");
            if(!recipe.capture&&(recipe.autoApply||recipe.criteria.autoRepair||recipe.operations.Any(o=>o.kind!="audit")))throw new ArgumentException("Skipping capture is allowed only for a read-only technical audit without repairs.");
            if(!Finite(recipe.criteria.contactTolerance)||recipe.criteria.contactTolerance<=0||recipe.criteria.contactTolerance>.1f||!Finite(recipe.criteria.minimumCellPixels)||recipe.criteria.minimumCellPixels<24||!Finite(recipe.criteria.maximumCentreError)||recipe.criteria.maximumCentreError<=0||recipe.criteria.maximumCentreError>2||recipe.criteria.quality<0||recipe.criteria.quality>3)throw new ArgumentException("Invalid acceptance limits.");
            foreach(var op in recipe.operations)
                foreach(var token in op.parameters.DescendantsAndSelf().OfType<JValue>())
                    if(token.Type==JTokenType.Float&&(double.IsNaN(token.Value<double>())||double.IsInfinity(token.Value<double>())))throw new ArgumentException("Non-finite operation parameter.");
        }
        static bool Finite(float v)=>!float.IsNaN(v)&&!float.IsInfinity(v);
        public static string Role(CampArtDefinition art,CampArtDefinition.Instance pose,CampArtDefinition.Model model)
        {
            if(pose.id==art.primaryLandmarkInstanceId)return "primary";
            // The shared wind shader also renders hanging fabric. Shader choice is not botany.
            if(pose.motion=="cloth"||model.seasonalRole=="solid")return "prop";
            if(model.seasonalRole=="deciduous"||model.seasonalRole=="conifer")return "tree";
            if(model.seasonalRole=="groundcover"||model.shading=="foliage")return "vegetation";
            return "prop";
        }
        public static bool Generated(string id)=>new[]{"cover/","vegetation/","regrowth/","overgrowth/","worldops/"}.Any(p=>(id??"").StartsWith(p,StringComparison.Ordinal));
        public static WorldObjectRecord Record(LevelAuthoringDocument d,string id,bool create=false)
        {
            var existing=d.worldOps?.objects.FirstOrDefault(r=>r.id==id);
            if(existing!=null||!create)return existing;
            if(d.worldOps==null)d.worldOps=new WorldAuthoringMetadata();
            var result=new WorldObjectRecord{id=id,owner=Generated(id)?"generated":"manual"};d.worldOps.objects.Add(result);return result;
        }
        public static CampArtDefinition.Instance[] Select(LevelAuthoringDocument d,WorldSelector selector)
        {
            var models=d.art.models.ToDictionary(m=>m.id);
            var records=(d.worldOps?.objects??new List<WorldObjectRecord>()).ToDictionary(r=>r.id);
            return d.art.instances.Where(p=>
            {
                records.TryGetValue(p.id,out var record);
                if(p.modelId==null||!models.TryGetValue(p.modelId,out var m)||record?.pinned==true)return false;
                bool explicitId=selector.ids.Contains(p.id)||selector.primaryOnly&&p.id==d.art.primaryLandmarkInstanceId;
                if(selector.ids.Length>0&&!selector.ids.Contains(p.id))return false;
                if(selector.primaryOnly&&p.id!=d.art.primaryLandmarkInstanceId)return false;
                if(selector.modelIds.Length>0&&!selector.modelIds.Contains(m.sourceModelId??m.id)&&!selector.modelIds.Contains(m.id))return false;
                if(selector.roles.Length>0&&!selector.roles.Contains(Role(d.art,p,m)))return false;
                return explicitId||!selector.generatedOnly||Generated(p.id)||record?.owner=="generated";
            }).ToArray();
        }
        static float Number(JObject p,string key,float fallback,float min,float max)
        {float value=p[key]?.Value<float>()??fallback;if(!Finite(value)||value<min||value>max)throw new ArgumentException("Invalid "+key);return value;}
        static int ColorParameter(JObject p,string key,int fallback)
        {
            if(p[key]==null)return fallback;
            string hex=p[key].Value<string>();
            if(hex==null||hex.Length!=7||hex[0]!='#'||!ColorUtility.TryParseHtmlString(hex,out var c))throw new ArgumentException("Expected #RRGGBB for "+key);
            return (Mathf.RoundToInt(c.r*255)<<16)|(Mathf.RoundToInt(c.g*255)<<8)|Mathf.RoundToInt(c.b*255);
        }
        public static void Apply(LevelAuthoringDocument d,WorldOperation op,int seed)
        {
            if(!Kinds.Contains(op.kind))throw new ArgumentException("Unsupported operation: "+op.kind);
            var p=op.parameters;
            if(op.kind=="audit")return;
            if(op.kind=="vegetation.heightfield-preview")
            {
                WorldOpsMeadowHeightFields.Bake(d,(int)Number(p,"resolution",512,256,1024),p["adaptive"]?.Value<bool>()??false);return;
            }
            if(op.kind=="ground.refine")
            {
                var settings=p.ToObject<CampArtDefinition.GroundSurfaceSettings>();settings.Validate();
                d.art.groundSurface=settings;
                d.art.groundLow=ColorParameter(p,"groundLow",d.art.groundLow);
                d.art.groundHigh=ColorParameter(p,"groundHigh",d.art.groundHigh);
                d.art.soilColor=ColorParameter(p,"soilColor",d.art.soilColor);return;
            }
            if(op.kind=="vegetation.refine"){WorldOpsVegetation.Refine(d,op,seed);return;}
            if(op.kind=="camera.frame")
            {
                d.art.frameGameplayBoard=true;d.art.gameplayCameraMargin=Number(p,"margin",d.art.gameplayCameraMargin,1.02f,1.6f);
                if(p["euler"]!=null)
                {var e=p["euler"].ToObject<float[]>();if(e.Length!=3||e.Any(v=>!Finite(v)))throw new ArgumentException("Invalid camera Euler angles.");d.art.cameraEuler=e;}
                return;
            }
            if(op.kind=="atmosphere.preset")
            {
                var preset=SeasonAtmosphereCatalog.Copy(SeasonAtmosphereCatalog.Get((string)p["preset"]));
                if(p["sunIntensity"]!=null)preset.sunIntensity=Number(p,"sunIntensity",preset.sunIntensity,.2f,2);
                if(p["contrast"]!=null)preset.contrast=Number(p,"contrast",preset.contrast,-15,30);
                if(p["saturation"]!=null)preset.saturation=Number(p,"saturation",preset.saturation,-20,20);
                if(p["temperature"]!=null)preset.temperature=Number(p,"temperature",preset.temperature,-25,25);
                if(p["fog"]!=null)preset.fog=Number(p,"fog",preset.fog,0,.8f);
                if(p["lowFog"]!=null)preset.lowFog=Number(p,"lowFog",preset.lowFog,0,.7f);
                if(p["rays"]!=null)preset.rays=Number(p,"rays",preset.rays,0,1);
                if(p["airLight"]!=null)preset.airLight=Number(p,"airLight",preset.airLight,0,.7f);
                SeasonAtmosphereAuthoring.Apply(d,preset);return;
            }
            if(op.kind=="vegetation.preset"){VegetationAuthoring.Apply(d,VegetationAuthoring.Presets.Single(x=>x.id==(string)p["preset"]),seed);return;}
            if(op.kind=="path.upsert"){UpsertPath(d,op);return;}
            if(op.kind=="path.remove")
            {
                string id=(string)p["pathId"];if(d.worldOps==null)return;
                var path=d.worldOps.paths.SingleOrDefault(x=>x.id==id);if(path==null)return;
                if(path.owner!=op.id)throw new InvalidOperationException("Another operation owns this path.");
                d.worldOps.paths.Remove(path);d.art.instances=d.art.instances.Where(i=>i.id!="worldops/path/"+id).ToArray();return;
            }
            if(op.kind=="river.edit"||op.kind=="terrain.stamp")
            {
                if(d.terrain==null)throw new InvalidOperationException("This level needs an explicit Terrain conversion before terrain operations.");
                d.terrain=d.terrain.EditingCopy();
                if(op.kind=="river.edit")
                {
                    d.terrain.width=Number(p,"width",d.terrain.width,.2f,20);d.terrain.depth=Number(p,"depth",d.terrain.depth,.05f,8);
                    d.terrain.bankWidth=Number(p,"bankWidth",d.terrain.bankWidth,.1f,12);d.terrain.bankSoftness=Number(p,"bankSoftness",d.terrain.bankSoftness,0,1);
                    if(p["points"]!=null)d.terrain.points=p["points"].ToObject<RiverPoint[]>();
                }
                else
                {
                    var stamp=new LandscapeStamp{id=op.id,x=Number(p,"x",0,-200,200),z=Number(p,"z",0,-200,200),radius=Number(p,"radius",3,.2f,40),height=Number(p,"height",.5f,-8,8)};
                    d.terrain.stamps=d.terrain.stamps.Where(s=>s.id!=op.id).Concat(new[]{stamp}).ToArray();
                }
                d.terrain.Validate();return;
            }
            var chosen=Select(d,op.selector);var models=d.art.models.ToDictionary(m=>m.id);
            var records=(d.worldOps?.objects??new List<WorldObjectRecord>()).ToDictionary(r=>r.id);
            WorldObjectRecord Find(string id)=>records.TryGetValue(id,out var value)?value:null;
            if(op.kind=="building.move")
            {
                var affected=new HashSet<string>();
                foreach(var root in chosen)
                {
                    var rotation=Quaternion.Euler(0,Number(p,"yaw",0,-180,180),0);
                    var origin=new Vector3(root.x,root.y,root.z);var shift=new Vector3(Number(p,"dx",0,-100,100),Number(p,"dy",0,-20,20),Number(p,"dz",0,-100,100));
                    var ensemble=d.art.instances.Where(i=>i.id==root.id||Find(i.id)?.ensembleId==root.id||Find(i.id)?.support=="object:"+root.id||i.id.StartsWith("overgrowth/"+root.id+"/",StringComparison.Ordinal)||i.id.StartsWith(root.id+"/craft/",StringComparison.Ordinal)).ToArray();
                    if(ensemble.Any(i=>Find(i.id)?.pinned==true))throw new InvalidOperationException("A pinned ensemble member prevents moving the group.");
                    foreach(var item in ensemble)
                    {
                        if(!affected.Add(item.id))continue;
                        var at=origin+shift+rotation*(new Vector3(item.x,item.y,item.z)-origin);item.x=at.x;item.y=at.y;item.z=at.z;
                        var old=item.rotationEuler==null?Quaternion.Euler(0,item.yaw,0):Quaternion.Euler(CampArtDefinition.Vector(item.rotationEuler));
                        var e=(rotation*old).eulerAngles;item.yaw=e.y;item.rotationEuler=new[]{e.x,e.y,e.z};
                        item.surfaceOffset=item.y-d.art.GroundHeight(item.x,item.z);
                    }
                }
                AuthoringWeatherExposure.Bake(d);return;
            }
            float spacing=op.kind=="vegetation.redistribute"?Number(p,"spacing",.18f,.02f,3):.18f;
            var occupied=new Dictionary<Vector2Int,List<CampArtDefinition.Instance>>();
            Vector2Int Cell(float x,float z)=>new Vector2Int(Mathf.FloorToInt(x/spacing),Mathf.FloorToInt(z/spacing));
            void Occupy(CampArtDefinition.Instance item){var cell=Cell(item.x,item.z);if(!occupied.TryGetValue(cell,out var list)){list=new List<CampArtDefinition.Instance>();occupied.Add(cell,list);}list.Add(item);}
            bool Near(float x,float z,string id)
            {
                var cell=Cell(x,z);
                for(int dx=-1;dx<=1;dx++)for(int dz=-1;dz<=1;dz++)
                    if(occupied.TryGetValue(cell+new Vector2Int(dx,dz),out var list)&&list.Any(i=>i.id!=id&&(new Vector2(i.x-x,i.z-z)).sqrMagnitude<spacing*spacing))return true;
                return false;
            }
            var sites=op.kind=="vegetation.redistribute"?new WorldOpsGeometry.PlantSites(d):null;
            if(sites!=null)foreach(var instance in d.art.instances.Where(i=>i.modelId!=null&&models.TryGetValue(i.modelId,out var m)&&(Role(d.art,i,m)=="vegetation"||Role(d.art,i,m)=="tree")))Occupy(instance);
            foreach(var item in chosen)
            {
                var m=models[item.modelId];
                if(op.kind.StartsWith("vegetation.",StringComparison.Ordinal)&&item.id.StartsWith("overgrowth/",StringComparison.Ordinal)&&!op.selector.ids.Contains(item.id))continue;
                if(op.kind=="building.ground")
                {
                    if(Role(d.art,item,m)=="vegetation"||Role(d.art,item,m)=="tree")
                    {item.y=d.art.GroundHeight(item.x,item.z)+item.surfaceOffset;item.surfaceAttached=true;continue;}
                    var record=Record(d,item.id,true);record.support=(string)p["support"]??record.support;
                    record.maximumBurial=Number(p,"maximumBurial",record.maximumBurial,0,2);
                    float previousY=item.y;WorldOpsGeometry.Ground(d,item,m,record);float shift=item.y-previousY;
                    foreach(var child in d.art.instances.Where(i=>i.id!=item.id&&(Find(i.id)?.ensembleId==item.id||Find(i.id)?.support=="object:"+item.id||i.id.StartsWith("overgrowth/"+item.id+"/",StringComparison.Ordinal)||i.id.StartsWith(item.id+"/craft/",StringComparison.Ordinal))))
                    {if(Find(child.id)?.pinned==true)throw new InvalidOperationException("A pinned ensemble member prevents grounding the group.");child.y+=shift;child.surfaceOffset=child.y-d.art.GroundHeight(child.x,child.z);}
                    continue;
                }
                if(op.kind=="vegetation.scale")
                {
                    if(Role(d.art,item,m)!="vegetation"&&Role(d.art,item,m)!="tree")throw new InvalidOperationException("Vegetation scaling cannot change a solid prop.");
                    float factor=Number(p,"factor",1,.2f,3);item.scale*=factor;if(item.scaleXYZ!=null)item.scaleXYZ=item.scaleXYZ.Select(v=>v*factor).ToArray();
                    if(item.surfaceAttached==true)item.y=d.art.GroundHeight(item.x,item.z)+item.surfaceOffset;
                    continue;
                }
                if(op.kind=="vegetation.redistribute")
                {
                    if(Role(d.art,item,m)!="vegetation")continue;
                    var random=new System.Random(seed^Stable(item.id));float radius=Number(p,"radius",.4f,0,3);
                    float x=item.x,z=item.z;
                    for(int attempt=0;attempt<12;attempt++)
                    {
                        float px=x+((float)random.NextDouble()*2-1)*radius,pz=z+((float)random.NextDouble()*2-1)*radius;
                        if(WorldOpsGeometry.ProtectedPlant(d,px,pz)||sites.Wet(px,pz)||sites.InSolid(new Vector3(px,d.art.GroundHeight(px,pz)+.12f,pz))||Near(px,pz,item.id))continue;
                        occupied[Cell(item.x,item.z)].Remove(item);
                        item.x=px;item.z=pz;item.y=d.art.GroundHeight(px,pz)+item.surfaceOffset;Occupy(item);break;
                    }
                    continue;
                }
                if(op.kind=="model.age"||op.kind=="model.simplify"||op.kind=="building.refresh-model")
                {
                    CampArtDefinition.Model variant;
                    if(op.kind=="building.refresh-model")variant=LevelAuthoringDocument.Copy(WorldStudyFactory.MetricModel(m.sourceModelId??m.id));
                    else
                    {
                        var recipe=new ModelAuthoringRecipe{operation=op.kind=="model.age"?"age":"simplify",backend=(string)p["backend"]??"dirt",amount=Number(p,"amount",.35f,0,1),density=Number(p,"density",.7f,.05f,1),seed=seed^Stable(item.id),label="WorldOps",category="Декор"};
                        if(p["material"]!=null)recipe.weathering=new SurfaceWeatheringSettings{material=(string)p["material"],patchSize=Number(p,"patchSize",1.5f,.1f,12),distribution=(string)p["distribution"]??"natural"};
                        variant=ModelAuthoringWorkshop.Create(m,recipe,false).model;
                    }
                    models[variant.id]=variant;item.modelId=variant.id;
                }
            }
            d.art.models=models.Values.ToArray();
            if(op.kind=="building.ground"&&chosen.Length>0)AuthoringWeatherExposure.Bake(d);
        }
        internal static int Stable(string text){unchecked{int h=17;foreach(char c in text??"")h=h*31+c;return h;}}
        static void UpsertPath(LevelAuthoringDocument d,WorldOperation op)
        {
            var path=op.parameters.ToObject<WorldPath>();path.id=(string)op.parameters["pathId"]??op.id;path.owner=op.id;
            if(path.points==null||path.points.Length<2||path.points.Length>128||!Finite(path.width)||path.width<.25f||path.width>4||path.points.Any(p=>p==null||p.Length!=2||p.Any(v=>!Finite(v))))throw new ArgumentException("Invalid path points/width.");
            if(d.worldOps==null)d.worldOps=new WorldAuthoringMetadata();
            var previous=d.worldOps.paths.SingleOrDefault(x=>x.id==path.id);
            if(previous!=null&&previous.owner!=op.id)throw new InvalidOperationException("Another operation owns this path.");
            var vertices=new List<float>();var normals=new List<float>();var colors=new List<int>();
            void Triangle(Vector3 a,Vector3 b,Vector3 c)
            {foreach(var v in new[]{a,b,c}){vertices.AddRange(new[]{v.x,v.y,v.z});normals.AddRange(new[]{0f,1f,0f});colors.Add(path.color);}}
            var controls=path.points.Select(point=>new Vector2(point[0],point[1])).ToArray();
            Vector2 Side(int index)
            {
                Vector2 previous=(controls[Math.Max(0,index-1)]-controls[index]).normalized;
                Vector2 next=(controls[Math.Min(controls.Length-1,index+1)]-controls[index]).normalized;
                if(index==0)previous=-next;if(index==controls.Length-1)next=-previous;
                var incoming=new Vector2(previous.y,-previous.x);var outgoing=new Vector2(-next.y,next.x);
                var bisector=(incoming+outgoing).normalized;float denominator=Vector2.Dot(bisector,outgoing);
                if(denominator<.3f)throw new InvalidOperationException("A path corner reverses too sharply; add a wider bend.");
                return bisector*(path.width*.5f/denominator);
            }
            for(int segment=1;segment<path.points.Length;segment++)
            {
                var a=new Vector2(path.points[segment-1][0],path.points[segment-1][1]);var b=new Vector2(path.points[segment][0],path.points[segment][1]);
                if(Vector2.Distance(a,b)<.01f)throw new ArgumentException("Duplicate path point.");
                var sideA=Side(segment-1);var sideB=Side(segment);int steps=Mathf.CeilToInt(Vector2.Distance(a,b)/.3f);
                Vector3 At(float t,float sign){var xz=Vector2.Lerp(a,b,t)+Vector2.Lerp(sideA,sideB,t)*sign;if(Protected(d,xz.x,xz.y))throw new InvalidOperationException("Decorative paths cannot clear the puzzle field.");return new Vector3(xz.x,d.art.GroundHeight(xz.x,xz.y)+.008f,xz.y);}
                for(int k=0;k<steps;k++){float t=(float)k/steps,u=(float)(k+1)/steps;var l=At(t,-1);var r=At(t,1);var nl=At(u,-1);var nr=At(u,1);Triangle(l,r,nl);Triangle(r,nr,nl);}
            }
            var model=new CampArtDefinition.Model{id="worldops_path_"+WorldOpsStorage.Hash(path.id).Substring(0,24),shading="solid",seasonalRole="solid",vertices=vertices.ToArray(),normals=normals.ToArray(),colors=colors.ToArray()};
            string instance="worldops/path/"+path.id;
            d.art.models=d.art.models.Where(m=>m.id!=model.id).Concat(new[]{model}).ToArray();
            d.art.instances=d.art.instances.Where(i=>i.id!=instance).Concat(new[]{new CampArtDefinition.Instance{id=instance,modelId=model.id,scale=1,surfaceAttached=false}}).ToArray();
            d.worldOps.paths.RemoveAll(x=>x.id==path.id);d.worldOps.paths.Add(path);Record(d,instance,true).owner=op.id;
        }
        static bool Protected(LevelAuthoringDocument d,float x,float z)=>WorldOpsGeometry.Protected(d,x,z);
    }
}
