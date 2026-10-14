using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using QuietCamp.Infrastructure;
using UnityEngine;

namespace QuietCamp.Editor.LevelAuthoring
{
    [Serializable] public sealed class ForestRegrowthSettings
    {
        public int seed=1938, attempts=18000, targetTrees;
        public float extent=24, spacing=.38f, density=.78f, height=.55f, fernShare=.14f, flowerShare=.1f, bushShare=.025f;
        public void Validate()
        {
            if(targetTrees<0||targetTrees>180||attempts<1||attempts>50000||!new[]{extent,spacing,density,height,fernShare,flowerShare,bushShare}.All(v=>!float.IsNaN(v)&&!float.IsInfinity(v))||extent<8||extent>60||spacing<.2f||spacing>2||height<.1f||height>2||density<0||density>1||fernShare<0||flowerShare<0||bushShare<0||fernShare+flowerShare+bushShare>1)
                throw new ArgumentException("Некоректні параметри заростання лісу.");
        }
    }

    // One repeatable operation: frozen, editable plants around existing geometry and use zones.
    public static class ForestRegrowthAuthoring
    {
        public static ForestRegrowthSettings Defaults()=>JsonConvert.DeserializeObject<ForestRegrowthSettings>(Resources.Load<TextAsset>("QuietCamp/forest_regrowth").text);
        public static int Apply(LevelAuthoringDocument d,ForestRegrowthSettings settings)
        {
            settings.Validate();AuthoringVegetationSnapshot.ExpandCover(d);
            var originals=d.art.instances.Where(i=>!(i.id??"").StartsWith("regrowth/",StringComparison.Ordinal)).ToList();
            var models=d.art.models.ToDictionary(m=>m.id);
            if(d.art.landscape!=null)
            {
                var land=d.art.landscape;
                originals.RemoveAll(i=>i.surfaceAttached==true&&i.modelId!=null&&new[]{"groundcover","flower","conifer","deciduous"}.Contains(models[i.modelId].seasonalRole)&&(i.x<land.originX+.3f||i.z<land.originZ+.3f||i.x>land.originX+land.size-.3f||i.z>land.originZ+land.size-.3f));
            }
            var trees=originals.Where(i=>i.modelId!=null&&(models[i.modelId].seasonalRole=="conifer"||models[i.modelId].seasonalRole=="deciduous")&&AuthoringPropCatalog.ModelBounds(models[i.modelId]).size.y*i.scale>3).Select(i=>new Vector2(i.x,i.z)).ToArray();
            var clear=new List<(Vector2 point,float radius)>();var paths=new List<(Vector2 a,Vector2 b)>();
            var blocked=new HashSet<Vector2Int>();var clearance=new Dictionary<Vector2Int,float>();const float raster=.32f;
            foreach(var i in originals.Where(i=>i.modelId!=null))
            {
                var m=models[i.modelId];if(m.shading=="foliage"||m.seasonalRole=="groundcover"||m.seasonalRole=="flower"||m.seasonalRole=="deciduous"||m.seasonalRole=="conifer")continue;
                var scale=i.scaleXYZ==null?Vector3.one*i.scale:CampArtDefinition.Vector(i.scaleXYZ);
                var pose=Matrix4x4.TRS(new Vector3(i.x,i.y,i.z),Quaternion.Euler(i.rotationEuler==null?new Vector3(0,i.yaw,0):CampArtDefinition.Vector(i.rotationEuler)),scale);
                var size=Vector3.Scale(AuthoringPropCatalog.ModelBounds(m).size,scale);
                if(size.x<3&&size.z<3&&size.y>.25f&&size.y<3)
                {
                    var point=new Vector2(i.x,i.z);clear.Add((point,.45f+Mathf.Min(.6f,size.magnitude*.15f)));
                    var edge=new Vector2(Mathf.Clamp(point.x,-d.level.width*.5f,d.level.width*.5f),Mathf.Clamp(point.y,-d.level.height*.5f,d.level.height*.5f));
                    if(Vector2.Distance(point,edge)<5)paths.Add((point,edge));
                }
                for(int t=0;t<m.vertices.Length;t+=9)
                {
                    Vector3 P(int n)=>pose.MultiplyPoint3x4(new Vector3(m.vertices[t+n],m.vertices[t+n+1],m.vertices[t+n+2]));
                    var a=P(0);var b=P(3);var c=P(6);float det=(b.z-c.z)*(a.x-c.x)+(c.x-b.x)*(a.z-c.z);if(Mathf.Abs(det)<.001f)continue;
                    float low=Mathf.Min(a.y,Mathf.Min(b.y,c.y));if(low>d.art.GroundHeight((a.x+b.x+c.x)/3,(a.z+b.z+c.z)/3)+1.8f)continue;
                    int minX=Mathf.FloorToInt(Mathf.Min(a.x,Mathf.Min(b.x,c.x))/raster),maxX=Mathf.CeilToInt(Mathf.Max(a.x,Mathf.Max(b.x,c.x))/raster);
                    int minZ=Mathf.FloorToInt(Mathf.Min(a.z,Mathf.Min(b.z,c.z))/raster),maxZ=Mathf.CeilToInt(Mathf.Max(a.z,Mathf.Max(b.z,c.z))/raster);
                    for(int x=minX;x<=maxX;x++)for(int z=minZ;z<=maxZ;z++)
                    {
                        float px=x*raster,pz=z*raster,u=((b.z-c.z)*(px-c.x)+(c.x-b.x)*(pz-c.z))/det,v=((c.z-a.z)*(px-c.x)+(a.x-c.x)*(pz-c.z))/det;
                        if(u<-.05f||v<-.05f||u+v>1.05f)continue;
                        float y=u*a.y+v*b.y+(1-u-v)*c.y;float gap=y-d.art.GroundHeight(px,pz);var key=new Vector2Int(x,z);
                        if(!clearance.TryGetValue(key,out var old)||gap<old)clearance[key]=gap;
                        if(gap<.65f)blocked.Add(key);
                    }
                }
            }
            var water=d.art.landscape?.meshes.Where(m=>m.water).SelectMany(m=>Enumerable.Range(0,m.triangles.Length/3).Select(t=>new[]{m.triangles[t*3],m.triangles[t*3+1],m.triangles[t*3+2]}.Select(i=>new Vector2(m.vertices[i*3],m.vertices[i*3+2])).ToArray())).ToArray()??Array.Empty<Vector2[]>();
            bool Wet(Vector2 p)
            {
                foreach(var triangle in water)
                {
                    var a=triangle[0];var b=triangle[1];var c=triangle[2];float det=(b.y-c.y)*(a.x-c.x)+(c.x-b.x)*(a.y-c.y);if(Mathf.Abs(det)<.001f)continue;
                    float u=((b.y-c.y)*(p.x-c.x)+(c.x-b.x)*(p.y-c.y))/det,v=((c.y-a.y)*(p.x-c.x)+(a.x-c.x)*(p.y-c.y))/det;
                    if(u>=0&&v>=0&&u+v<=1)return true;
                }
                if(d.art.landscape==null&&d.art.streamWidth>0)
                {
                    float a=d.art.frameYaw*Mathf.Deg2Rad;var uv=new Vector2(p.x*Mathf.Cos(a)-p.y*Mathf.Sin(a),p.x*Mathf.Sin(a)+p.y*Mathf.Cos(a));
                    float centre=d.art.streamCentre+Kruty1918.ScenePlacement.Composition.RiverGeometry.Centre(uv.y,d.art.streamSlope,d.art.streamBend);
                    float width=Kruty1918.ScenePlacement.Composition.RiverGeometry.Width(uv.y,d.art.streamWidth,d.art.streamWidthVariation);
                    return Mathf.Abs(uv.x-centre)<width*.5f;
                }
                return false;
            }
            CampArtDefinition.Model Plant(CampArtDefinition.Model source,string label,string role,float[] growth)
            {
                var copy=LevelAuthoringDocument.Copy(source);copy.seasonalRole=role;copy.seasonalGrowth=growth;
                var variant=ModelAuthoringWorkshop.Register(copy,new ModelAuthoringRecipe{operation="external",backend="forest-regrowth-v1",sourceId=source.sourceModelId??source.id,label=label,category=role=="flower"?"Квіти":role=="groundcover"?"Трава":"Кущі",seed=settings.seed},false).model;
                models[variant.id]=variant;return variant;
            }
            var grass=Plant(WorldStudyFactory.MetricModel("qc_kenney_nature_grass_large"),"Лісова висока трава","groundcover",new[]{.65f,1f,.42f,0f});
            var broad=Plant(WorldStudyFactory.MetricModel("qc_kenney_nature_grass_leafsLarge"),"Молоде широке листя","groundcover",new[]{.75f,1f,.48f,0f});
            var fern=Plant(Fern(),"Лісова папороть","groundcover",new[]{.62f,1f,.38f,0f});
            var flower=Plant(Flowers(),"Весняні лісові квіти","flower",new[]{1f,.28f,.05f,0f});
            var bush=Plant(WorldStudyFactory.MetricModel("qc_kenney_nature_plant_bushSmall"),"Молодий підлісок","deciduous",new[]{.72f,1f,.55f,1f});
            var random=new System.Random(settings.seed);var occupied=new Dictionary<Vector2Int,List<Vector2>>();int added=0;
            float Next()=>(float)random.NextDouble();Vector2Int Cell(Vector2 p)=>new Vector2Int(Mathf.FloorToInt(p.x/settings.spacing),Mathf.FloorToInt(p.y/settings.spacing));
            var treeModels=originals.Where(i=>i.modelId!=null&&(models[i.modelId].seasonalRole=="conifer"||models[i.modelId].seasonalRole=="deciduous")&&AuthoringPropCatalog.ModelBounds(models[i.modelId]).size.y*i.scale>3).Select(i=>models[i.modelId]).GroupBy(m=>m.id).Select(g=>g.First()).ToArray();
            if(treeModels.Length==0&&settings.targetTrees>0)
            {
                treeModels=new[]{WorldStudyFactory.MetricModel("qc_kenney_nature_tree_oak"),WorldStudyFactory.MetricModel("qc_kenney_nature_tree_pineRoundA")};
                foreach(var tree in treeModels){tree.seasonalRole=tree.id.Contains("pine")?"conifer":"deciduous";models[tree.id]=tree;}
            }
            var inverse=Quaternion.Inverse(Quaternion.Euler(CampArtDefinition.Vector(d.art.cameraEuler)));
            Bounds ScreenBounds(Bounds bounds,Matrix4x4 pose)
            {
                var result=new Bounds(inverse*pose.MultiplyPoint3x4(bounds.min),Vector3.zero);
                for(int x=0;x<2;x++)for(int y=0;y<2;y++)for(int z=0;z<2;z++)result.Encapsulate(inverse*pose.MultiplyPoint3x4(new Vector3(x==0?bounds.min.x:bounds.max.x,y==0?bounds.min.y:bounds.max.y,z==0?bounds.min.z:bounds.max.z)));
                return result;
            }
            var protectedViews=new List<Bounds>{ScreenBounds(new Bounds(Vector3.up*1.2f,new Vector3(d.level.width+2.5f,2.4f,d.level.height+2.5f)),Matrix4x4.identity)};
            const float screenCell=.65f;var heroDepth=new Dictionary<Vector2Int,float>();
            foreach(var i in originals.Where(i=>i.modelId!=null&&models[i.modelId].seasonalRole=="solid"))
            {
                var bounds=AuthoringPropCatalog.ModelBounds(models[i.modelId]);if(bounds.size.magnitude*i.scale<6)continue;
                var pose=Matrix4x4.TRS(new Vector3(i.x,i.y,i.z),Quaternion.Euler(i.rotationEuler==null?new Vector3(0,i.yaw,0):CampArtDefinition.Vector(i.rotationEuler)),i.scaleXYZ==null?Vector3.one*i.scale:CampArtDefinition.Vector(i.scaleXYZ));var m=models[i.modelId];
                for(int t=0;t<m.vertices.Length;t+=9)
                {
                    Vector3 Point(int k)=>inverse*pose.MultiplyPoint3x4(new Vector3(m.vertices[t+k],m.vertices[t+k+1],m.vertices[t+k+2]));
                    var a=Point(0);var b=Point(3);var c=Point(6);float det=(b.y-c.y)*(a.x-c.x)+(c.x-b.x)*(a.y-c.y);if(Mathf.Abs(det)<.001f)continue;
                    int minX=Mathf.FloorToInt(Mathf.Min(a.x,Mathf.Min(b.x,c.x))/screenCell),maxX=Mathf.CeilToInt(Mathf.Max(a.x,Mathf.Max(b.x,c.x))/screenCell);
                    int minY=Mathf.FloorToInt(Mathf.Min(a.y,Mathf.Min(b.y,c.y))/screenCell),maxY=Mathf.CeilToInt(Mathf.Max(a.y,Mathf.Max(b.y,c.y))/screenCell);
                    for(int x=minX;x<=maxX;x++)for(int y=minY;y<=maxY;y++)
                    {
                        float px=x*screenCell,py=y*screenCell,u=((b.y-c.y)*(px-c.x)+(c.x-b.x)*(py-c.y))/det,v=((c.y-a.y)*(px-c.x)+(a.x-c.x)*(py-c.y))/det;
                        if(u<-.03f||v<-.03f||u+v>1.03f)continue;
                        var key=new Vector2Int(x,y);float depth=u*a.z+v*b.z+(1-u-v)*c.z;if(!heroDepth.TryGetValue(key,out var old)||depth<old)heroDepth[key]=depth;
                    }
                }
            }
            var forest=trees.ToList();int treeAdded=0;
            float treeExtent=Mathf.Min(settings.extent,d.art.landscape==null?settings.extent:d.art.landscape.size*.5f-4);
            int visibleTreeCount=forest.Count(p=>Mathf.Abs(p.x)<treeExtent&&Mathf.Abs(p.y)<treeExtent);
            for(int n=0;n<3000&&visibleTreeCount<settings.targetTrees;n++)
            {
                var p=new Vector2((Next()-.5f)*treeExtent*2,(Next()-.5f)*treeExtent*2);
                if(p.magnitude<10||Wet(p)||forest.Any(t=>Vector2.Distance(t,p)<3.4f)||clear.Any(c=>Vector2.Distance(c.point,p)<c.radius+2))continue;
                var r=new Vector2Int(Mathf.RoundToInt(p.x/raster),Mathf.RoundToInt(p.y/raster));if(blocked.Contains(r))continue;
                var tree=treeModels[random.Next(treeModels.Length)];
                if(treeModels.Length>1&&originals.Any(i=>i.modelId==tree.id&&Vector2.Distance(new Vector2(i.x,i.z),p)<6.5f))tree=treeModels[(Array.IndexOf(treeModels,tree)+1)%treeModels.Length];
                var bounds=AuthoringPropCatalog.ModelBounds(tree);float scale=Mathf.Lerp(.72f,1.08f,Next()),yaw=Next()*360,ground=d.art.GroundHeight(p.x,p.y);
                var pose=Matrix4x4.TRS(new Vector3(p.x,ground-bounds.min.y*scale,p.y),Quaternion.Euler(0,yaw,0),Vector3.one*scale);var view=ScreenBounds(bounds,pose);
                bool hides=protectedViews.Any(h=>view.min.z<h.max.z&&view.min.x<h.max.x+.35f&&view.max.x>h.min.x-.35f&&view.min.y<h.max.y+.35f&&view.max.y>h.min.y-.35f);
                if(!hides)for(int x=Mathf.FloorToInt(view.min.x/screenCell);x<=Mathf.CeilToInt(view.max.x/screenCell)&&!hides;x++)for(int y=Mathf.FloorToInt(view.min.y/screenCell);y<=Mathf.CeilToInt(view.max.y/screenCell);y++)
                    if(heroDepth.TryGetValue(new Vector2Int(x,y),out var depth)&&view.min.z<depth-.25f){hides=true;break;}
                if(hides)continue;
                originals.Add(new CampArtDefinition.Instance{id="regrowth/tree/"+treeAdded++,modelId=tree.id,x=p.x,z=p.y,y=ground-bounds.min.y*scale,scale=scale,yaw=yaw,surfaceAttached=true,surfaceOffset=-bounds.min.y*scale});forest.Add(p);visibleTreeCount++;
            }
            trees=forest.ToArray();
            for(int n=0;n<settings.attempts;n++)
            {
                var p=new Vector2((Next()-.5f)*settings.extent*2,(Next()-.5f)*settings.extent*2);
                if(Mathf.Abs(p.x)<d.level.width*.5f+1||Mathf.Abs(p.y)<d.level.height*.5f+1)
                    if(Mathf.Abs(p.x)<d.level.width*.5f+.75f&&Mathf.Abs(p.y)<d.level.height*.5f+.75f)continue;
                if(Wet(p))continue;
                var r=new Vector2Int(Mathf.RoundToInt(p.x/raster),Mathf.RoundToInt(p.y/raster));bool solid=false;
                for(int dx=-1;dx<=1;dx++)for(int dz=-1;dz<=1;dz++)if(blocked.Contains(r+new Vector2Int(dx,dz)))solid=true;
                if(solid||clear.Any(c=>Vector2.Distance(p,c.point)<c.radius)||paths.Any(line=>Distance(p,line.a,line.b)<.38f))continue;
                float shade=trees.Length==0?0:Mathf.Clamp01(1-trees.Min(t=>Vector2.Distance(t,p))/5);
                float noise=Mathf.PerlinNoise(p.x*.17f+settings.seed*.023f,p.y*.17f-settings.seed*.013f);
                float probability=settings.density*Mathf.Lerp(.35f,1,Mathf.SmoothStep(0,1,(noise-.22f)*1.9f))*(.8f+shade*.35f);
                if(Next()>probability)continue;
                var cell=Cell(p);bool near=false;for(int dx=-1;dx<=1;dx++)for(int dz=-1;dz<=1;dz++)if(occupied.TryGetValue(cell+new Vector2Int(dx,dz),out var points)&&points.Any(q=>Vector2.Distance(p,q)<settings.spacing))near=true;
                if(near)continue;
                float kind=Next();var model=kind<settings.bushShare?bush:kind<settings.bushShare+settings.flowerShare?flower:kind<settings.bushShare+settings.flowerShare+settings.fernShare*(.65f+shade)?fern:Next()<.5f?grass:broad;
                float height=(model==bush?1f:model==flower?.48f:model==fern?.75f:model==broad?.36f:settings.height)*Mathf.Lerp(.7f,1.3f,Next());
                if(clearance.TryGetValue(r,out var room)&&room<height+.1f)continue;
                var bounds=AuthoringPropCatalog.ModelBounds(model);float scale=height/Mathf.Max(.01f,bounds.size.y),ground=d.art.GroundHeight(p.x,p.y);
                originals.Add(new CampArtDefinition.Instance{id="regrowth/"+added++,modelId=model.id,x=p.x,z=p.y,y=ground-bounds.min.y*scale,scale=scale,yaw=Next()*360,surfaceAttached=true,surfaceOffset=-bounds.min.y*scale});
                if(!occupied.TryGetValue(cell,out var bucket))occupied[cell]=bucket=new List<Vector2>();bucket.Add(p);
            }
            var used=new HashSet<string>(originals.Where(i=>i.modelId!=null).Select(i=>i.modelId));d.art.models=models.Values.Where(m=>used.Contains(m.id)).ToArray();d.art.instances=originals.ToArray();
            d.vegetationPreset="forest_recovery";d.vegetationSeed=settings.seed;if(d.art.atmosphere!=null)SeasonAtmosphereAuthoring.Apply(d,d.art.atmosphere);
            return added;
        }
        static float Distance(Vector2 p,Vector2 a,Vector2 b)=>Vector2.Distance(p,Vector2.Lerp(a,b,Mathf.Clamp01(Vector2.Dot(p-a,b-a)/Mathf.Max(.001f,(b-a).sqrMagnitude))));
        static CampArtDefinition.Model Fern()
        {
            var vertices=new List<Vector3>();var colors=new List<Color>();
            for(int leaf=0;leaf<7;leaf++)
            {
                float angle=leaf*137.5f*Mathf.Deg2Rad;var axis=new Vector3(Mathf.Cos(angle),0,Mathf.Sin(angle));var side=new Vector3(-axis.z,0,axis.x);
                for(int k=0;k<5;k++)for(int sign=-1;sign<=1;sign+=2)
                {
                    float t=(k+1)/6f;var at=axis*t*.6f+Vector3.up*(Mathf.Sin(t*2.2f)*.52f);var tip=at+side*sign*(1-t)*.23f+axis*.15f;
                    vertices.Add(at-axis*.055f);vertices.Add(at+axis*.08f);vertices.Add(tip);colors.AddRange(Enumerable.Repeat(new Color(.21f+k*.012f,.37f+k*.017f,.14f),3));
                }
            }
            return Model("qc_forest_fern",vertices,colors);
        }
        static CampArtDefinition.Model Flowers()
        {
            var vertices=new List<Vector3>();var colors=new List<Color>();
            for(int stem=0;stem<3;stem++)
            {
                var centre=new Vector3((stem-1)*.13f,.32f+stem*.06f,stem==1?.13f:0);
                vertices.AddRange(new[]{new Vector3(centre.x-.012f,0,centre.z),centre,new Vector3(centre.x+.012f,0,centre.z)});colors.AddRange(Enumerable.Repeat(new Color(.28f,.46f,.18f),3));
                for(int petal=0;petal<6;petal++)
                {
                    float a=petal*Mathf.PI/3;var dir=new Vector3(Mathf.Cos(a),0,Mathf.Sin(a));var side=new Vector3(-dir.z,0,dir.x);
                    vertices.AddRange(new[]{centre+Vector3.up*.005f,centre+dir*.11f-side*.045f,centre+dir*.11f+side*.045f});
                    colors.AddRange(new[]{new Color(.9f,.71f,.22f),new Color(.87f,.84f,.73f),new Color(.97f,.92f,.82f)});
                }
            }
            return Model("qc_forest_flowers",vertices,colors);
        }
        static CampArtDefinition.Model Model(string id,List<Vector3> vertices,List<Color> colors)
        {
            var normals=new List<Vector3>();for(int i=0;i<vertices.Count;i+=3){var n=Vector3.Cross(vertices[i+1]-vertices[i],vertices[i+2]-vertices[i]).normalized;normals.AddRange(Enumerable.Repeat(n,3));}
            return new CampArtDefinition.Model{id=id,shading="foliage",vertices=vertices.SelectMany(v=>new[]{v.x,v.y,v.z}).ToArray(),normals=normals.SelectMany(v=>new[]{v.x,v.y,v.z}).ToArray(),colors=colors.Select(QuietCamp.Presentation.World.SeasonModelAppearance.Pack).ToArray()};
        }
    }
}
