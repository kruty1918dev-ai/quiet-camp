using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using QuietCamp.Infrastructure;
using UnityEngine;
using UnityEditor;

namespace QuietCamp.Editor.LevelAuthoring
{
    [Serializable] public sealed class AircraftCrashSettings
    {
        public int seed=1938, debris=16;
        public float damage=.8f, burial=.3f, gap=1.7f, age=.75f, scarDepth=.24f, scarWidth=2.5f;
        public float cameraSize=18.7f;
        public float[] noseEuler={3,87,8}, tailEuler={-3,96,-9};
        public float[] wingPosition={-.8f,0,-14.5f}, wingEuler={5,15,-6};
        public bool splitWing=true, people=true, terrain=true;
    }
    /// <summary>One old forest crash and one occupied clearing, frozen by the existing authoring pipeline.</summary>
    public static class AircraftCrashAuthoring
    {
        public static LevelAuthoringDocument Compose(LevelAuthoringDocument source,AircraftCrashSettings settings)
        {
            var d=LevelAuthoringDocument.Copy(source);d.art.atmosphere=null;d.terrain=null;
            var models=d.art.models.ToDictionary(m=>m.id);var instances=d.art.instances.Where(i=>!((models[i.modelId].sourceModelId??i.modelId).StartsWith("qc_aircraft_"))).ToList();
            d.art.orthographicSize=settings.cameraSize;d.art.generation="aircraft-crash/forest/v1";d.art.seed=settings.seed;
            if(settings.terrain)BakeGround(d,settings);
            foreach(var i in instances.Where(i=>i.surfaceAttached==true))i.y=d.art.GroundHeight(i.x,i.z)+i.surfaceOffset;
            foreach(var i in instances.Where(i=>i.id.StartsWith("aircraft/")&&i.surfaceAttached==true))
            {
                var model=models[i.modelId];var e=i.rotationEuler==null?new Vector3(0,i.yaw,0):CampArtDefinition.Vector(i.rotationEuler);
                if((model.sourceModelId??model.id)=="qc_bent_sheet")e=new Vector3(7,143,13);
                i.rotationEuler=new[]{e.x,e.y,e.z};i.y=GroundingHeight(d.art,model,new Vector3(i.x,0,i.z),Quaternion.Euler(e),i.scale)-.035f;
                i.surfaceOffset=i.y-d.art.GroundHeight(i.x,i.z);
            }
            CampArtDefinition.Model Age(CampArtDefinition.Model model,string kind,string material,float amount,float patch=2)
                =>ModelAuthoringWorkshop.Create(model,new ModelAuthoringRecipe{operation="age",backend=kind,amount=amount,seed=settings.seed,label=string.IsNullOrEmpty(model.authoringRecipe)?(model.sourceModelId??model.id):JsonConvert.DeserializeObject<ModelAuthoringRecipe>(model.authoringRecipe).label,category=string.IsNullOrEmpty(model.authoringRecipe)?"Декор":JsonConvert.DeserializeObject<ModelAuthoringRecipe>(model.authoringRecipe).category,
                    weathering=new SurfaceWeatheringSettings{material=material,patchSize=patch}},false).model;
            CampArtDefinition.Model Weather(CampArtDefinition.Model model,string material)
            {
                model=Age(model,material=="aluminium"?"oxidation":material=="steel"?"rust":"paint",material,settings.age*.8f,4.5f);
                model=Age(model,"dirt",material,settings.age,3.5f);
                model=Age(model,"streaks",material,settings.age*.65f,1.1f);
                if(material=="aluminium")model=Age(model,"moss",material,settings.age*.5f,3.2f);
                return model;
            }
            void Add(CampArtDefinition.Model model,string id,Vector3 position,Vector3 euler,float scale=1,bool attached=true,float offset=0)
            {
                models[model.id]=model;instances.Add(new CampArtDefinition.Instance{id="crash/"+id,modelId=model.id,x=position.x,y=position.y,z=position.z,scale=scale,
                    yaw=euler.y,rotationEuler=new[]{euler.x,euler.y,euler.z},surfaceAttached=attached,surfaceOffset=offset});
            }
            void Ground(CampArtDefinition.Model model,string id,float x,float z,Vector3 euler,float scale=1,float burial=0)
            {
                var q=Quaternion.Euler(euler);
                // The lowest supporting surface is grounded; a controlled negative offset embeds heavy fragments.
                float y=GroundingHeight(d.art,model,new Vector3(x,0,z),q,scale)-burial;
                Add(model,id,new Vector3(x,y,z),euler,scale,true,y-d.art.GroundHeight(x,z));
            }
            CampArtDefinition.Instance Half(bool fore)
            {
                var original=source.art.models.Single(m=>m.sourceModelId==(fore?"qc_aircraft_fore":"qc_aircraft_tail"));
                var model=CrashMeshAuthoring.Damage(original,fore,settings.damage,!fore&&settings.splitWing,out var wing);
                model=Weather(model,"aluminium");model=Age(model,"paint","aluminium",settings.age*.7f,5.5f);
                var e=CampArtDefinition.Vector(fore?settings.noseEuler:settings.tailEuler);var q=Quaternion.Euler(e);
                var points=Enumerable.Range(0,model.colors.Length).Select(k=>q*new Vector3(model.vertices[k*3],model.vertices[k*3+1],model.vertices[k*3+2])).ToArray();
                float x=fore?d.level.width*.5f+settings.gap-points.Min(p=>p.x):-d.level.width*.5f-settings.gap-points.Max(p=>p.x);
                float z=fore?.7f:-.65f;
                var belly=Enumerable.Range(0,model.colors.Length).Where(k=>Mathf.Abs(model.vertices[k*3])<1.55f&&Mathf.Abs(model.vertices[k*3+2])<5.5f)
                    .Select(k=>d.art.GroundHeight(x+points[k].x,z+points[k].z)-points[k].y).OrderByDescending(v=>v).ToArray();
                float y=belly.Length>0?belly[Math.Min(belly.Length-1,belly.Length/12)]-settings.burial:-1;
                Add(model,fore?"airframe/nose":"airframe/tail",new Vector3(x,y,z),e,1,true,y-d.art.GroundHeight(x,z));
                var half=instances.Last();
                if(wing!=null)
                {
                    var b=AuthoringPropCatalog.ModelBounds(wing);for(int k=0;k<wing.colors.Length;k++){wing.vertices[k*3]-=b.center.x;wing.vertices[k*3+1]-=b.center.y;wing.vertices[k*3+2]-=b.center.z;}
                    wing=Weather(wing,"aluminium");Ground(wing,"wing",settings.wingPosition[0],settings.wingPosition[2],CampArtDefinition.Vector(settings.wingEuler),1,.3f);
                }
                if(wing!=null)
                {
                    var item=instances.Single(i=>i.id=="crash/wing");var qWing=Quaternion.Euler(CampArtDefinition.Vector(item.rotationEuler));
                    var candidates=Enumerable.Range(0,wing.colors.Length/3).Where(t=>(qWing*new Vector3(wing.normals[t*9],wing.normals[t*9+1],wing.normals[t*9+2])).y<-.25f).Select(t=>
                    {
                        var p=Vector3.zero;for(int j=0;j<3;j++){int k=(t*3+j)*3;p+=new Vector3(wing.vertices[k],wing.vertices[k+1],wing.vertices[k+2])/3;}
                        return new Vector3(item.x,item.y,item.z)+qWing*p;
                    }).Where(p=>p.y>d.art.GroundHeight(p.x,p.z)+.18f).OrderBy(p=>(new Vector2(p.x-item.x,p.z-item.z)).sqrMagnitude).ToArray();
                    if(candidates.Length>0){var p=candidates[0];var rock=WorldStudyFactory.MetricModel("qc_kenney_nature_rock_largeA");var bRock=AuthoringPropCatalog.ModelBounds(rock);float scale=(p.y-d.art.GroundHeight(p.x,p.z)+.04f)/bRock.size.y;Ground(rock,"wing-support",p.x,p.z,Vector3.zero,scale,.04f);}
                }
                return half;
            }
            var nose=Half(true);var tail=Half(false);
            var rib=Weather(CrashMeshAuthoring.Prop("rib"),"aluminium");
            // Frames and storage sit in the actual broken ends, beyond the protected puzzle footprint.
            foreach(var half in new[]{nose,tail})
            {
                bool fore=half==nose;var body=models[half.modelId];var bounds=AuthoringPropCatalog.ModelBounds(body);
                float localZ=fore?bounds.min.z+.65f:bounds.max.z-.65f;
                var q=Quaternion.Euler(CampArtDefinition.Vector(half.rotationEuler));var p=new Vector3(half.x,half.y,half.z)+q*new Vector3(0,0,localZ);
                Add(rib,fore?"frame/nose":"frame/tail",p,CampArtDefinition.Vector(half.rotationEuler));
            }
            var brokenLog=CrashMeshAuthoring.Prop("broken_trunk");
            var stump=WorldStudyFactory.MetricModel("qc_kenney_nature_stump_old");
            foreach(var pos in new[]{new Vector2(8,-11),new Vector2(-19,5.7f),new Vector2(20,6)})
            {Ground(Weather(brokenLog,"wood"),"fallen-tree/"+pos.x,pos.x,pos.y,new Vector3(4,settings.seed%80+pos.x,-7),.7f,.12f);Ground(Weather(stump,"wood"),"stump/"+pos.x,pos.x+1.3f,pos.y+.3f,Vector3.zero,.72f,.05f);}
            var random=new System.Random(settings.seed);var panel=Weather(LevelAuthoringPalette.Get("qc_bent_sheet").model,"aluminium");
            for(int k=0;k<settings.debris;k++)
            {
                float side=k%2==0?1:-1,x=side*(d.level.width*.5f+settings.gap+2+(float)random.NextDouble()*15),z=(float)(random.NextDouble()-.5)*9;
                Ground(panel,"scatter/"+k,x,z,new Vector3((float)random.NextDouble()*18,(float)random.NextDouble()*360,(float)random.NextDouble()*28-14),.25f+(float)random.NextDouble()*.4f,.07f);
            }
            if(settings.people)
            {
                var suitcase=Weather(CrashMeshAuthoring.Prop("suitcase"),"cloth");var seat=Weather(CrashMeshAuthoring.Prop("seat"),"steel");
                var coat=Age(CrashMeshAuthoring.Prop("coat"),"dirt","cloth",.45f,.5f);var boots=Weather(CrashMeshAuthoring.Prop("boots"),"cloth");
                for(int k=0;k<3;k++)Ground(suitcase,"collected-bags/"+k,-d.level.width*.5f-1.1f-k*.55f,-2.9f+k*.15f,new Vector3(0,35+k*13,k==2?8:0),1,.015f);
                Ground(suitcase,"abandoned-bag",nose.x+3,-3.7f,new Vector3(7,153,62),1,.04f);
                Ground(suitcase,"cabin-bag",-d.level.width*.5f-settings.gap-.5f,.5f,new Vector3(0,70,0),1,.025f);
                Ground(seat,"seat-a",2.1f,-d.level.height*.5f-1.3f,new Vector3(0,205,4),1,.03f);
                Ground(seat,"seat-b",3.1f,-d.level.height*.5f-2.2f,new Vector3(0,255,-3),1,.04f);
                Ground(boots,"boots",-1,-d.level.height*.5f-1.25f,new Vector3(0,23,0),1,.015f);
                Ground(coat,"coat",-1.7f,-d.level.height*.5f-1.4f,new Vector3(0,59,3),1,.015f);
                Ground(Weather(CrashMeshAuthoring.Prop("drying"),"wood"),"drying",-.2f,-d.level.height*.5f-2.2f,new Vector3(0,20,0));
                Ground(Weather(CrashMeshAuthoring.Prop("awning"),"aluminium"),"awning",-d.level.width*.5f-3.5f,2.6f,new Vector3(0,12,0));
            }
            // Do not grow plants through cabins, engines, personal belongings or paths.
            var heavy=instances.Where(i=>i.id.StartsWith("crash/")).ToArray();
            instances=instances.Where(i=>
            {
                if(i.id.StartsWith("crash/"))return true;
                var id=models[i.modelId].sourceModelId??i.modelId;
                if(id.Contains("tree")&&heavy.Where(h=>h.id=="crash/wing").Any(h=>InsideHorizontal(models[h.modelId],h,new Vector2(i.x,i.z),2.7f)))return false;
                if(!(id.Contains("grass")||id.Contains("weeds")||id.Contains("bush")||id.Contains("mushroom")))return true;
                if(Mathf.Abs(i.z)<settings.scarWidth*.8f&&Mathf.Abs(i.x)>d.level.width*.5f+1&&Mathf.Abs(i.x)<26)return false;
                return !heavy.Any(h=>h.id.Contains("airframe")?InsideHorizontal(models[h.modelId],h,new Vector2(i.x,i.z),.3f):new Vector2(h.x-i.x,h.z-i.z).sqrMagnitude<.7f);
            }).ToList();
            d.art.instances=instances.ToArray();d.art.models=models.Values.Where(m=>instances.Any(i=>i.modelId==m.id)).ToArray();
            AuthoringWeatherExposure.Bake(d,d.art.instances.Where(i=>i.id.StartsWith("crash/")).Select(i=>i.id).ToArray());
            return d;
        }
        public static float GroundingHeight(CampArtDefinition art,CampArtDefinition.Model model,Vector3 origin,Quaternion rotation,float scale=1)
        {
            // Prevent levitation: at least one actual mesh support touches the sampled terrain.
            float y=float.MinValue;
            for(int k=0;k<model.colors.Length;k++){var p=rotation*new Vector3(model.vertices[k*3],model.vertices[k*3+1],model.vertices[k*3+2])*scale;y=Mathf.Max(y,art.GroundHeight(origin.x+p.x,origin.z+p.z)-p.y);}
            return y;
        }
        static bool InsideHorizontal(CampArtDefinition.Model model,CampArtDefinition.Instance i,Vector2 point,float margin)
        {
            var q=Quaternion.Inverse(Quaternion.Euler(CampArtDefinition.Vector(i.rotationEuler)))*new Vector3(point.x-i.x,0,point.y-i.z);
            var b=AuthoringPropCatalog.ModelBounds(model);return q.x>b.min.x-margin&&q.x<b.max.x+margin&&q.z>b.min.z-margin&&q.z<b.max.z+margin;
        }
        static void BakeGround(LevelAuthoringDocument d,AircraftCrashSettings s)
        {
            const int n=129;float size=d.art.surroundExtent,step=size/(n-1);var heights=new float[n*n];var baseArt=LevelAuthoringDocument.Copy(d.art);baseArt.landscape=null;
            float Scar(float x,float z)
            {
                float active=Mathf.SmoothStep(0,1,(Mathf.Abs(x)-d.level.width*.5f-1.5f)*.55f)*(1-Mathf.SmoothStep(0,1,(Mathf.Abs(x)-23)*.25f));
                float line=.03f*x+.5f*Mathf.Sin(x*.19f);return active*Mathf.Exp(-(z-line)*(z-line)/(s.scarWidth*s.scarWidth));
            }
            for(int z=0;z<n;z++)for(int x=0;x<n;x++)
            {
                float wx=x*step-size*.5f,wz=z*step-size*.5f;float flat=1-Mathf.SmoothStep(0,1,Mathf.Max(0,Mathf.Max(Mathf.Abs(wx)-d.level.width*.5f,Mathf.Abs(wz)-d.level.height*.5f))*.5f);
                heights[z*n+x]=baseArt.GroundHeight(wx,wz)+.045f*flat-s.scarDepth*Scar(wx,wz);
            }
            var mesh=new LandscapeMesh{name="Земля — зарослий слід катастрофи",vertices=new float[n*n*3],normals=new float[n*n*3],uv=new float[n*n*2],colors=new float[n*n*4],triangles=new int[(n-1)*(n-1)*6]};
            int t=0;

            for(int z=0;z<n;z++)for(int x=0;x<n;x++)
            {
                int i=z*n+x;float wx=x*step-size*.5f,wz=z*step-size*.5f;
                mesh.vertices[i*3]=wx;mesh.vertices[i*3+1]=heights[i];mesh.vertices[i*3+2]=wz;
                var normal=new Vector3(heights[z*n+Mathf.Max(0,x-1)]-heights[z*n+Mathf.Min(n-1,x+1)],2*step,heights[Mathf.Max(0,z-1)*n+x]-heights[Mathf.Min(n-1,z+1)*n+x]).normalized;
                mesh.normals[i*3]=normal.x;mesh.normals[i*3+1]=normal.y;mesh.normals[i*3+2]=normal.z;mesh.uv[i*2]=x/(float)(n-1);mesh.uv[i*2+1]=z/(float)(n-1);
                var color=Color.Lerp(CampArtDefinition.Color(d.art.groundLow),CampArtDefinition.Color(d.art.groundHigh),.35f+.18f*Mathf.PerlinNoise(wx*.22f,wz*.22f));
                color=Color.Lerp(color,new Color(.30f,.27f,.20f),Scar(wx,wz)*.8f);
                mesh.colors[i*4]=color.r;mesh.colors[i*4+1]=color.g;mesh.colors[i*4+2]=color.b;mesh.colors[i*4+3]=1;
                if(x<n-1&&z<n-1){mesh.triangles[t++]=i;mesh.triangles[t++]=i+n;mesh.triangles[t++]=i+n+1;mesh.triangles[t++]=i;mesh.triangles[t++]=i+n+1;mesh.triangles[t++]=i+1;}
            }
            d.art.landscape=new FrozenLandscape{resolution=n,size=size,originX=-size*.5f,originZ=-size*.5f,heights=heights,meshes=new[]{mesh}};
        }
        public static object BuildThroughPwb(string sourcePath,string settingsPath,string output)
        {
            if(PlacementToolIntegration.Controlled==null)throw new InvalidOperationException("Native PWB adapter is required; install Prefab World Builder 4.12.");
            var source=LevelAuthoringDocument.Open(sourcePath);var settings=JsonConvert.DeserializeObject<AircraftCrashSettings>(File.ReadAllText(settingsPath));
            var d=Compose(source,settings);var authored=d.art.instances.Where(i=>i.id.StartsWith("crash/")).ToArray();
            var workspaceDocument=LevelAuthoringDocument.Copy(d);workspaceDocument.art.instances=d.art.instances.Where(i=>!i.id.StartsWith("crash/")).ToArray();
            LevelAuthoringDocument captured;
            int count=0;Undo.IncrementCurrentGroup();int group=Undo.GetCurrentGroup();
            using(var workspace=new LevelAuthoringWorkspace(workspaceDocument))
            {
                foreach(var batch in authored.GroupBy(i=>i.modelId))
                {
                    var poses=batch.ToArray();var painted=PlacementToolIntegration.Controlled.PlaceBatch(LevelAuthoringPalette.Get(batch.Key).prefab,workspace.Scenery,workspace.Surface,poses);
                    if(painted.Length!=poses.Length)throw new InvalidOperationException("Incomplete PWB placement.");
                    for(int k=0;k<painted.Length;k++)
                    {
                        var marker=painted[k].GetComponent<LevelAuthoringAsset>();marker.instanceId=poses[k].id;marker.sourcePose=poses[k];marker.surfaceAttached=poses[k].surfaceAttached??true;marker.surfaceOffset=poses[k].surfaceOffset;
                        marker.attachmentInitialized=true;marker.attachedY=poses[k].y;marker.capturedPosition=painted[k].transform.localPosition;marker.capturedRotation=painted[k].transform.localRotation;marker.capturedScale=painted[k].transform.localScale;
                        Undo.ClearUndo(painted[k]);count++;
                    }
                }
                captured=workspace.Capture(false);captured.title="Лісовий табір на місці давньої авіакатастрофи";captured.art.atmosphere=null;
            }
            Undo.CollapseUndoOperations(group);
            var errors=captured.Validate(true);if(errors.Count>0)throw new InvalidDataException(string.Join("\n",errors));
            captured.Save(output);
            return new{status="crash-composed",draftPath=output,backend=PlacementToolIntegration.Controlled.Backend,painted=count,objects=captured.art.instances.Length,errors};
        }
    }
}
