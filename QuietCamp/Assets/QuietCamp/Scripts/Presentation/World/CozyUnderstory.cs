using System.Collections.Generic;
using Kruty1918.Atmos;
using QuietCamp.Domain;
using UnityEngine;
using UnityEngine.Rendering;

namespace QuietCamp.Presentation.World
{
    /// <summary>Small trail-side accents; continuous forest groundcover is owned by VisibleForestFloor.</summary>
    public sealed class CozyUnderstory : MonoBehaviour
    {
        sealed class Geometry
        {
            public Vector3[] vertices, normals;
            public int[][] triangles;
            public CozyVegetationLibrary.Plant source;
            public Geometry(CozyVegetationLibrary.Plant plant)
            {
                source = plant;vertices = plant.mesh.vertices;normals = plant.mesh.normals;
                triangles = new int[plant.mesh.subMeshCount][];
                for (int i = 0; i < triangles.Length; i++) triangles[i] = plant.mesh.GetTriangles(i);
            }
        }
        sealed class Batch
        {
            public readonly List<Vector3> vertices = new List<Vector3>(), normals = new List<Vector3>();
            public readonly List<int> indices = new List<int>();
            public readonly List<Vector4> roots = new List<Vector4>();
            public Color color;
            public FoliageSway.Species species;
            public void Append(Geometry geometry, int slot, Vector3 root, float height, Quaternion rotation)
            {
                int start = vertices.Count;
                var plant = new Vector4(root.x, root.z, root.y, height);
                // Only vertices used by this material slot; retain the source's flat facet normals.
                var remap = new Dictionary<int,int>();
                foreach (int index in geometry.triangles[slot])
                {
                    if (!remap.TryGetValue(index, out int mapped))
                    {
                        mapped = vertices.Count - start;remap.Add(index, mapped);
                        vertices.Add(root + rotation * geometry.vertices[index] * height);
                        normals.Add(rotation * geometry.normals[index]);roots.Add(plant);
                    }
                    indices.Add(start + mapped);
                }
            }
        }
        readonly List<Mesh> _meshes = new List<Mesh>();
        readonly List<Material> _materials = new List<Material>();
        readonly Transform[] _tiers = new Transform[3];
        readonly List<Vector3> _roots = new List<Vector3>();
        readonly int[] _counts = new int[3];
        int _quality = -1;
        SeasonProfile _season;
        public IReadOnlyList<Vector3> PlantRoots => _roots;
        public int ActivePlantCount { get { int count=0;for(int i=0;i<3;i++)if(_tiers[i]!=null&&_tiers[i].gameObject.activeSelf)count+=_counts[i];return count; } }

        public void Build(LevelData level)
        {
            _season=SeasonProfile.For(level);if(_season.Winter)return;
            var library = CozyVegetationLibrary.Load();
            if (library == null || library.plants.Length < 6) return;
            var geometry = new Geometry[library.plants.Length];
            for (int i=0;i<geometry.Length;i++) geometry[i]=new Geometry(library.plants[i]);
            var rng = new System.Random(unchecked(level.decorSeed * 1973 + 271));
            for (int tier=0;tier<3;tier++)
            {
                _tiers[tier] = new GameObject("UnderstoryTier"+tier).transform;
                _tiers[tier].SetParent(transform,false);
                var batches = new Dictionary<int,Batch>();
                // Low keeps the trail-side greenery too. Staggered pockets, never a hedge
                // or a decorative barrier across the walking lane; reuse the existing batches.
                foreach(var access in CampAccess.Points(level))
                {
                    int pockets=tier==0?16:tier==1?10:8;
                    for(int pocket=0;pocket<pockets;pocket++)
                    {
                        float distance=.85f+pocket*(7.7f/pockets)+Next(rng)*.25f;
                        var centre=CampTrail.Centre(level,access,distance);
                        var tangent=CampTrail.Centre(level,access,distance+.05f)-CampTrail.Centre(level,access,distance-.05f);tangent.y=0;
                        var side=Vector3.Cross(Vector3.up,tangent.normalized)*(pocket%2==0?1:-1);
                        int id=pocket%7==0?0:pocket%5==0?4:2;
                        float height=id==0?.24f+Next(rng)*.12f:.17f+Next(rng)*.09f;
                        var plant=geometry[id];height=CozyVegetationLibrary.GroundcoverScale(plant.source.kind,plant.source.mesh.bounds,height);
                        var extents=plant.source.mesh.bounds.extents;
                        float radius=new Vector2(extents.x,extents.z).magnitude*height+height*.36f;
                        var root=centre+side*(.62f+radius+Next(rng)*.25f);root.y=.012f;
                        if(Next(rng)>_season.ShrubWeight||!CanPlant(level,root,radius))continue;
                        if(plant.source.kind==CozyVegetationLibrary.PlantKind.Flower&&_season.Autumn)continue;
                        Append(batches,plant,root,height,pocket%2,tier,rng);
                    }
                }
                foreach (var pair in batches) Upload(_tiers[tier],pair.Value,pair.Key);
            }
            ApplyQuality();
        }
        static float Next(System.Random rng) => (float)rng.NextDouble();
        void Append(Dictionary<int,Batch> batches,Geometry source,Vector3 root,float height,int tint,int tier,System.Random rng)
        {
            var rotation=Quaternion.Euler(0,Next(rng)*360,0);
            _roots.Add(root);_counts[tier]++;
            var species=source.source.kind==CozyVegetationLibrary.PlantKind.Shrub?FoliageSway.Species.Bush:
                source.source.kind==CozyVegetationLibrary.PlantKind.Leaf?FoliageSway.Species.Grass:FoliageSway.Species.FlowerStem;
            for(int slot=0;slot<source.triangles.Length;slot++)
            {
                var color=source.source.colors[slot];bool leaf=color.g>color.r;
                if(leaf)color=Color.Lerp(_season.Palette.GrassDark,_season.Palette.GrassLight,tint==0?.35f:.8f);
                int colorKey=leaf?tint:color.r>.8f?2:3,key=(int)species*4+colorKey;
                if(!batches.TryGetValue(key,out var batch))batches.Add(key,batch=new Batch{color=color,species=species});
                batch.Append(source,slot,root,height,rotation);
            }
        }
        internal static bool CanPlant(LevelData level,Vector3 position,float radius)
        {
            // Include the whole bent silhouette, not just the root, in the exclusion.
            if (Mathf.Abs(position.x)<level.width*.5f+.18f+radius && Mathf.Abs(position.z)<level.height*.5f+.18f+radius) return false;
            if (ShorelineGeometry.Contains(EnvironmentCompositionData.For(level).shore,position,radius+.15f)) return false;
            return !CampTrail.IsCorridor(level,position,radius+.1f);
        }
        void Upload(Transform parent,Batch batch,int key)
        {
            if (batch.vertices.Count==0) return;
            var mesh = new Mesh{name="Cozy understory "+key};
            if (batch.vertices.Count>65535) mesh.indexFormat=IndexFormat.UInt32;
            mesh.SetVertices(batch.vertices);mesh.SetNormals(batch.normals);mesh.SetTriangles(batch.indices,0);
            mesh.SetUVs(1,batch.roots);mesh.RecalculateBounds();
            var bounds=mesh.bounds;bounds.Expand(.65f);mesh.bounds=bounds;_meshes.Add(mesh);
            var material = new Material(FoliageSway.Shared.MaterialForSpecies(batch.color,batch.species))
                {shader=Shader.Find("QuietCamp/FoliageLit"),name="Cozy "+batch.species+" (scene owned)"};
            material.SetFloat("_ClusterWind",1);material.SetFloat("_Cull",0);CampFoliageResponse.Apply(material);_materials.Add(material);
            var go=new GameObject(batch.species+"-"+key);go.transform.SetParent(parent,false);go.layer=BoardRenderer.DecorLayer;
            go.AddComponent<MeshFilter>().sharedMesh=mesh;
            var renderer=go.AddComponent<MeshRenderer>();renderer.sharedMaterial=material;
            renderer.shadowCastingMode=ShadowCastingMode.On;renderer.receiveShadows=true;
        }
        void Update() => ApplyQuality();
        void ApplyQuality()
        {
            int quality=QuietCampBootstrap.ServicesRef?.EffectiveQuality??1;
            if (quality==_quality) return;_quality=quality;
            for (int i=0;i<3;i++) if(_tiers[i]!=null) _tiers[i].gameObject.SetActive(i<=quality);
        }
        void OnDestroy()
        {
            foreach (var mesh in _meshes) Destroy(mesh);
            foreach (var material in _materials) Destroy(material);
        }
    }
}
