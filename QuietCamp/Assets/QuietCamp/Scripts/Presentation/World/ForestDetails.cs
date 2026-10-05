using System.Collections.Generic;
using Kruty1918.Atmos;
using QuietCamp.Domain;
using UnityEngine;
using UnityEngine.Rendering;

namespace QuietCamp.Presentation.World
{
    /// <summary>Small forest life in a few mesh batches; wind runs on the GPU.</summary>
    public sealed class ForestDetails : MonoBehaviour
    {
        sealed class Batch
        {
            public readonly List<Vector3> vertices=new List<Vector3>();
            public readonly List<int> triangles=new List<int>();
            public readonly List<Vector4> plants=new List<Vector4>();
            Vector4 _plant;
            public void Plant(Vector3 root,float height)=>_plant=new Vector4(root.x,root.z,root.y,height);
            public void Triangle(Vector3 a,Vector3 b,Vector3 c)
            {int s=vertices.Count;vertices.Add(a);vertices.Add(b);vertices.Add(c);plants.Add(_plant);plants.Add(_plant);plants.Add(_plant);triangles.Add(s);triangles.Add(s+1);triangles.Add(s+2);}
            public void Quad(Vector3 a,Vector3 b,Vector3 c,Vector3 d){Triangle(a,b,c);Triangle(a,c,d);}
        }
        readonly List<Mesh> _meshes=new List<Mesh>();readonly List<Material> _materials=new List<Material>();
        Texture2D _contactTexture;GameObject _lush;
        public void Build(LevelData level,Transform decor)
        {
            var fern=new Batch();var cap=new Batch();var stem=new Batch();var flowerStem=new Batch();var pebble=new Batch();var blossom=new Batch();var pink=new Batch();
            var rng=new System.Random(level.decorSeed*491+17);
            _lush=new GameObject("LushUnderstory");_lush.transform.SetParent(transform,false);
            var season=SeasonProfile.For(level);
            if(season.Winter){Contacts(decor);return;}
            for(int cluster=0;cluster<12;cluster++)
            {
                // Rare mushrooms/ferns across the landscape, with no contour
                // following the board. The pooled layer supplies ordinary greenery.
                var center = new Vector3((cluster % 4 + .2f + (float)rng.NextDouble() * .6f) / 4f * (level.width + 18) - level.width * .5f - 9,
                    .01f, (cluster / 4 + .2f + (float)rng.NextDouble() * .6f) / 3f * (level.height + 18) - level.height * .5f - 9);
                // A rectangular exclusion keeps the playable clearing and its doors open.
                if(Mathf.Abs(center.x)<level.width*.5f+1&&Mathf.Abs(center.z)<level.height*.5f+1 || CampTrail.IsCorridor(level,center,.9f))continue;
                if(ShorelineGeometry.Contains(EnvironmentCompositionData.For(level).shore,center,1))continue;
                if(rng.NextDouble()>season.ShrubWeight)continue;
                for(int shoot=0;shoot<3;shoot++)
                {
                    var at=center+new Vector3((float)rng.NextDouble()*.7f-.35f,0,(float)rng.NextDouble()*.7f-.35f);
                    fern.Plant(at,.13f);
                    float rotation=(float)rng.NextDouble()*Mathf.PI*2;
                    for(int arm=0;arm<5;arm++)
                    {
                        var dir=new Vector3(Mathf.Cos(rotation+arm*1.256f),0,Mathf.Sin(rotation+arm*1.256f));
                        var side=Vector3.Cross(Vector3.up,dir);
                        for(int segment=0;segment<4;segment++)
                        {
                            float a=segment/4f,b=(segment+1)/4f;
                            Vector3 Spine(float t)=>at+dir*(t*.24f)+Vector3.up*(Mathf.Sin(t*Mathf.PI*.75f)*.13f);
                            float wa=Mathf.Sin(a*Mathf.PI)*.025f,wb=Mathf.Sin(b*Mathf.PI)*.025f;
                            fern.Quad(Spine(a)-side*wa,Spine(b)-side*wb,Spine(b)+side*wb,Spine(a)+side*wa);
                        }
                    }
                    if(shoot==0)
                    {
                        var top=at+Vector3.up*.17f;
                        stem.Quad(at+Vector3.left*.014f,at+Vector3.right*.014f,top+Vector3.right*.014f,top+Vector3.left*.014f);
                        for(int petal=0;petal<6;petal++)
                        {
                            float a=petal*Mathf.PI/3,b=(petal+1)*Mathf.PI/3;
                            cap.Triangle(top+Vector3.up*.055f,top+new Vector3(Mathf.Cos(a)*.09f,0,Mathf.Sin(a)*.09f),top+new Vector3(Mathf.Cos(b)*.09f,0,Mathf.Sin(b)*.09f));
                        }
                    }
                    else if(shoot%2==0&&!season.Autumn)
                    {
                        var top=at+Vector3.up*.22f;
                        flowerStem.Plant(at,.226f);blossom.Plant(at,.226f);pink.Plant(at,.226f);
                        flowerStem.Triangle(at,top,at+Vector3.right*.015f);
                        for(int petal=0;petal<5;petal++)
                        {
                            float a=petal*Mathf.PI*.4f;var d=new Vector3(Mathf.Cos(a),.1f,Mathf.Sin(a))*.055f;
                            (cluster%3==0?pink:blossom).Triangle(top,top+d+Vector3.right*.022f,top+d+Vector3.forward*.025f);
                        }
                    }
                }
                var stone=center+Vector3.right*.5f;var peak=stone+Vector3.up*.095f;
                pebble.Triangle(stone+Vector3.left*.12f,peak,stone+Vector3.forward*.09f);
                pebble.Triangle(stone+Vector3.forward*.09f,peak,stone+Vector3.right*.12f);
                pebble.Triangle(stone+Vector3.right*.12f,peak,stone-Vector3.forward*.08f);
                pebble.Triangle(stone-Vector3.forward*.08f,peak,stone+Vector3.left*.12f);
            }
            Upload("Ferns",fern,season.Palette.GrassDark,FoliageSway.Species.Grass);
            Upload("MushroomCaps",cap,new Color(.68f,.37f,.21f));
            Upload("TinyStems",stem,new Color(.73f,.72f,.51f));
            Upload("FlowerStems",flowerStem,season.Palette.GrassLight,FoliageSway.Species.FlowerStem);
            Upload("RiverPebbles",pebble,new Color(.52f,.56f,.45f));
            // Stem and petals use the same bend so the flower stays attached.
            Upload("Wildflowers",blossom,new Color(.90f,.70f,.36f),FoliageSway.Species.FlowerStem);
            Upload("RoseWildflowers",pink,new Color(.76f,.47f,.51f),FoliageSway.Species.FlowerStem);
            Contacts(decor);
        }
        void Upload(string name,Batch batch,Color color,FoliageSway.Species? species=null)
        {
            var go=new GameObject(name);go.transform.SetParent(_lush.transform,false);go.layer=BoardRenderer.DecorLayer;
            var mesh=new Mesh{name=name+" batch"};mesh.SetVertices(batch.vertices);mesh.SetTriangles(batch.triangles,0);mesh.SetUVs(1,batch.plants);mesh.RecalculateNormals();mesh.RecalculateBounds();
            // Shader movement must remain inside the renderer's culling volume.
            if(species.HasValue){var bounds=mesh.bounds;bounds.Expand(.12f);mesh.bounds=bounds;}
            _meshes.Add(mesh);
            go.AddComponent<MeshFilter>().sharedMesh=mesh;
            var material=species.HasValue
                ?new Material(FoliageSway.Shared.MaterialForSpecies(color,species.Value)){shader=Shader.Find("QuietCamp/FoliageLit")}
                :new Material(Shader.Find("Universal Render Pipeline/Simple Lit"));
            material.name=name+" (scene owned)";material.SetColor("_BaseColor",color);material.SetFloat("_Cull",0);
            if(species.HasValue){material.SetFloat("_ClusterWind",1);CampFoliageResponse.Apply(material);}
            _materials.Add(material);
            var renderer=go.AddComponent<MeshRenderer>();renderer.sharedMaterial=material;renderer.shadowCastingMode=ShadowCastingMode.On;renderer.receiveShadows=true;
        }
        void Contacts(Transform decor)
        {
            var verts=new List<Vector3>();var triangles=new List<int>();var uv=new List<Vector2>();
            foreach(var renderer in decor.GetComponentsInChildren<MeshRenderer>())
            {
                var b=renderer.bounds;if(b.size.y<.6f||b.min.y>.15f)continue;
                float radius=Mathf.Clamp(b.size.x*.20f,.12f,.6f);var p=new Vector3(b.center.x,.006f,b.center.z);
                int s=verts.Count;verts.Add(p+new Vector3(-radius,0,-radius));verts.Add(p+new Vector3(-radius,0,radius));verts.Add(p+new Vector3(radius,0,radius));verts.Add(p+new Vector3(radius,0,-radius));
                uv.Add(Vector2.zero);uv.Add(Vector2.up);uv.Add(Vector2.one);uv.Add(Vector2.right);
                triangles.AddRange(new[]{s,s+1,s+2,s,s+2,s+3});
            }
            var mesh=new Mesh{name="Soft forest contacts"};mesh.SetVertices(verts);mesh.SetUVs(0,uv);mesh.SetTriangles(triangles,0);mesh.RecalculateBounds();_meshes.Add(mesh);
            var colors=new Color[verts.Count];for(int i=0;i<colors.Length;i++)colors[i]=Color.white;mesh.colors=colors;
            _contactTexture=new Texture2D(16,16,TextureFormat.RGBA32,false);var pixels=new Color[256];
            for(int y=0;y<16;y++)for(int x=0;x<16;x++){var p=new Vector2((x+.5f)/8-1,(y+.5f)/8-1);pixels[y*16+x]=new Color(1,1,1,Mathf.Pow(Mathf.Clamp01(1-p.sqrMagnitude),2));}
            _contactTexture.SetPixels(pixels);_contactTexture.Apply(false,true);
            var material=new Material(Shader.Find("QuietCamp/LeafParticle"));material.SetTexture("_MainTex",_contactTexture);material.SetColor("_Tint",new Color(.09f,.18f,.11f,.24f));_materials.Add(material);
            var go=new GameObject("ForestContactDepth");go.transform.SetParent(transform,false);go.AddComponent<MeshFilter>().sharedMesh=mesh;
            var r=go.AddComponent<MeshRenderer>();r.sharedMaterial=material;r.shadowCastingMode=ShadowCastingMode.Off;r.receiveShadows=false;
        }
        // Small story accents retain their silhouette on every profile.
        void OnDestroy(){foreach(var m in _meshes)Destroy(m);foreach(var m in _materials)Destroy(m);if(_contactTexture!=null)Destroy(_contactTexture);}
    }
}
