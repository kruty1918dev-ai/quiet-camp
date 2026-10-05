using Kruty1918.Atmos;
using QuietCamp.Domain;
using QuietCamp.Infrastructure;
using UnityEngine;
namespace QuietCamp.Presentation.World
{
    /// <summary>
    /// Seeded campsite and forest on one continuous XZ meadow. Distant trees
    /// use the same models, camera and lighting as the playable clearing.
    /// </summary>
    public static class DecorSpawner
    {
        /// <summary>Clear meadow margin before the surrounding forest.</summary>
        public const float Apron = 2.1f;

        // Slot order follows the source meshes: bark/wood first, foliage
        // second; flowers pair a grass stem with a coloured head.
        static readonly FoliageSway.Species[] BroadleafSlots =
            { FoliageSway.Species.Trunk, FoliageSway.Species.Canopy };
        static readonly FoliageSway.Species[] ConiferSlots =
            { FoliageSway.Species.Trunk, FoliageSway.Species.Conifer };
        static readonly FoliageSway.Species[] FlowerSlots =
            { FoliageSway.Species.FlowerStem, FoliageSway.Species.FlowerHead };

        // Fixed accents from the scene contract (position + height); pushed
        // outside the board ring automatically for larger levels.
        static readonly (string id, float x, float z, float height)[] TestDecor =
        {
            ("tree_default", -3.7f, -2.8f, 2.6f),
            ("tree_default", -2.0f, -3.6f, 2.4f),
            ("tree_pineRoundA", 0.3f, -3.7f, 2.8f),
            ("tree_pineRoundA", 2.8f, -3.6f, 2.6f),
            ("tree_default", -3.6f, 0.3f, 1.8f),
        };


        public static void Spawn(LevelData level, AssetCatalog catalog, Transform decorRoot)
        {
            if (catalog == null || decorRoot == null) return;
            var rng = new System.Random(level.decorSeed);

            float halfW = level.width / 2f, halfH = level.height / 2f;
            // Wide no-decor margin: tree canopies must not overlap the board
            // edge — the playable area has to read as a distinct region.
            float forbiddenX = halfW + 0.9f, forbiddenZ = halfH + 0.9f;
            float meadowX = halfW + Apron, meadowZ = halfH + Apron;

            // The v2 composer owns scenic trees and validates their swept
            // shadows against the puzzle after its fixed sun is initialized.
            // Retain the original accents only for archived v1 scenes.
            bool legacyTrees = level.ruleVersion < 2;
            if (legacyTrees) foreach (var d in TestDecor)
            {
                var pos = new Vector3(d.x, 0f, d.z);
                ClampOutside(ref pos, forbiddenX, forbiddenZ, meadowX, meadowZ);
                var go = Spawn(level, catalog, decorRoot, d.id, pos, rng.Next(360));
                if (go != null) {ScaleToHeight(go, d.height);SeasonalTreeVisual.Apply(go,level,d.id);}
            }

            if (legacyTrees)
            {
                Scatter(level, catalog, decorRoot, rng, "tree_pineRoundA",
                    7 - TestDecorCount("tree_pineRoundA"), forbiddenX, forbiddenZ, meadowX, meadowZ);
                Scatter(level, catalog, decorRoot, rng, "tree_default",
                    6 - TestDecorCount("tree_default"), forbiddenX, forbiddenZ, meadowX, meadowZ);
            }
            Scatter(level, catalog, decorRoot, rng, "stone_largeA", 3,
                forbiddenX, forbiddenZ, meadowX, meadowZ);
            // Groundcover belongs to VisibleForestFloor: an extra near-board
            // scatter would stack over it and make the clearing look hedged in.
            Scatter(level, catalog, decorRoot, rng, "log", 2,
                forbiddenX, forbiddenZ, meadowX, meadowZ);
            Scatter(level, catalog, decorRoot, rng, "stump_round", 1,
                forbiddenX, forbiddenZ, meadowX, meadowZ);

            if (legacyTrees) SpawnForest(level, catalog, decorRoot);
            // Ground is last so atmosphere's first decor renderer is foliage.
            SpawnMeadow(level, decorRoot);
            var density=decorRoot.GetComponent<DecorDensity>()??decorRoot.gameObject.AddComponent<DecorDensity>();density.Capture(decorRoot);
            var details=new GameObject("ForestDetails");details.transform.SetParent(decorRoot,false);
            details.AddComponent<ForestDetails>().Build(level,decorRoot);
            var understory=new GameObject("CozyUnderstory");understory.transform.SetParent(decorRoot,false);
            understory.AddComponent<CozyUnderstory>().Build(level);
        }

        /// <summary>The board base is buried in this shared ground; only
        /// the subtle playable grid rises above the surrounding clearing.</summary>
        static void SpawnMeadow(LevelData level, Transform decorRoot)
        {
            var meadow = new GameObject("Meadow");
            meadow.transform.SetParent(decorRoot, false);
            meadow.layer = BoardRenderer.DecorLayer;
            meadow.AddComponent<MeadowSurface>().Build(level.width / 2f + Apron,
                level.height / 2f + Apron);
        }

        /// <summary>Staggered forest belts replace camera-facing painted
        /// scenery. The open near wedge keeps tall crowns out of the board's
        /// sightline; foreground shrubs still frame the screen edges.</summary>
        static void SpawnForest(LevelData level, AssetCatalog catalog, Transform decorRoot)
        {
            var root = new GameObject("ForestSurround").transform;
            root.SetParent(decorRoot, false);
            root.gameObject.layer = BoardRenderer.DecorLayer;
            var rng = new System.Random(level.decorSeed * 3571 + 29);
            for (var belt = 0; belt < 3; belt++)
            for (var i = 0; i < 18; i++)
            {
                var angle = (i + belt * .41f + (float)rng.NextDouble() * .65f) * Mathf.PI * 2 / 18;
                var jitter = (float)rng.NextDouble() * 1.6f - .8f;
                var radiusX = level.width * .5f + Apron + 2.4f + belt * 3.2f + jitter;
                var radiusZ = level.height * .5f + Apron + 2.4f + belt * 3.2f + jitter;
                var pos = new Vector3(Mathf.Cos(angle) * radiusX, 0, Mathf.Sin(angle) * radiusZ);
                // The camera is on the +x/+z side of the camp.
                var near = pos.x + pos.z > 0;
                if (near && belt == 0) continue;
                var assetId=rng.NextDouble() < (level.environmentPreset=="pines"?.88:.45) ? "tree_pineRoundA" : "tree_default";
                var tree = Spawn(level, catalog, root,assetId,pos, rng.Next(360));
                if (tree == null) continue;
                var height = near ? Mathf.Lerp(2.7f, 3.8f, (float)rng.NextDouble())
                    : Mathf.Lerp(2.8f, 4.2f, (float)rng.NextDouble());
                ScaleToHeight(tree, height);
                SeasonalTreeVisual.Apply(tree,level,assetId);
                // Decorative forest cannot intercept placement rays.
                foreach (var collider in tree.GetComponentsInChildren<Collider>())
                    Object.Destroy(collider);
            }
        }

        /// <summary>Scales the instance so its rendered height matches the contract.</summary>
        public static void ScaleToHeight(GameObject go, float targetHeight)
        {
            var renderers = go.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) return;
            var bounds = renderers[0].bounds;
            foreach (var r in renderers) bounds.Encapsulate(r.bounds);
            if (bounds.size.y <= 0.001f) return;
            go.transform.localScale *= targetHeight / bounds.size.y;
        }

        static int TestDecorCount(string id)
        {
            var n = 0;
            foreach (var d in TestDecor) if (d.id == id) n++;
            return n;
        }

        static void Scatter(LevelData level, AssetCatalog catalog, Transform root, System.Random rng,
            string assetId, int count,
            float forbiddenX, float forbiddenZ, float meadowX, float meadowZ)
        {
            for (var i = 0; i < Mathf.Max(0, count); i++)
            {
                // Reject the clearing instead of pushing many unrelated roots
                // onto exactly the same rectangular boundary.
                Vector3 pos = default;
                bool found = false;
                for (int attempt = 0; attempt < 24; attempt++)
                {
                    pos = new Vector3((float)(rng.NextDouble() * 2 - 1) * (meadowX - .15f), 0,
                        (float)(rng.NextDouble() * 2 - 1) * (meadowZ - .15f));
                    if (Mathf.Abs(pos.x) < forbiddenX && Mathf.Abs(pos.z) < forbiddenZ) continue;
                    found = true; break;
                }
                if (!found) continue;
                var go=Spawn(level, catalog, root, assetId, pos, rng.Next(360));
                if(assetId.StartsWith("tree"))SeasonalTreeVisual.Apply(go,level,assetId);
            }
        }

        /// <summary>Pushes a position outside the board band, inside the meadow.</summary>
        static void ClampOutside(ref Vector3 pos,
            float forbiddenX, float forbiddenZ, float meadowX, float meadowZ)
        {
            if (Mathf.Abs(pos.x) < forbiddenX && Mathf.Abs(pos.z) < forbiddenZ)
            {
                // Push out along the axis closest to its edge.
                if (Mathf.Abs(pos.x) / forbiddenX > Mathf.Abs(pos.z) / forbiddenZ)
                    pos.x = Mathf.Sign(pos.x >= 0f ? 1f : -1f) * forbiddenX;
                else
                    pos.z = Mathf.Sign(pos.z >= 0f ? 1f : -1f) * forbiddenZ;
            }
            pos.x = Mathf.Clamp(pos.x, -meadowX, meadowX);
            pos.z = Mathf.Clamp(pos.z, -meadowZ, meadowZ);
        }

        static GameObject Spawn(LevelData level, AssetCatalog catalog, Transform root, string assetId,
            Vector3 pos, int yaw)
        {
            if (!catalog.TryGet(assetId, out var entry) || entry.prefab == null) return null;
            float clearance=assetId.StartsWith("tree")?1.15f:assetId.StartsWith("stone")||assetId=="log"?.4f:.12f;
            if(CampTrail.IsCorridor(level,pos,clearance))return null;
            if(ShorelineGeometry.Contains(EnvironmentCompositionData.For(level).shore,pos,clearance+.25f))return null;
            var go = Object.Instantiate(entry.prefab, root);
            go.transform.localPosition = pos;
            go.transform.localEulerAngles = new Vector3(0f, yaw, 0f);
            // Species per material slot — a tree's trunk and crown are
            // separate submeshes with their own responses, never one
            // amplitude for the whole model.
            if (assetId == "grass")
                FoliageSway.Shared.ApplySpecies(go, FoliageSway.Species.Grass);
            else if (assetId.StartsWith("flower"))
                FoliageSway.Shared.ApplySlots(go, FlowerSlots);
            else if (assetId == "tree_default")
                FoliageSway.Shared.ApplySlots(go, BroadleafSlots);
            else if (assetId.StartsWith("tree"))
                FoliageSway.Shared.ApplySlots(go, ConiferSlots);
            SetLayer(go, BoardRenderer.DecorLayer);
            if(assetId.StartsWith("tree")||assetId.StartsWith("stone")||assetId.StartsWith("log")||assetId.StartsWith("stump"))RainSurface.Attach(go);
            SeasonalPropVisual.Apply(go, level, assetId);
            return go;
        }

        static void SetLayer(GameObject go, int layer)
        {
            go.layer = layer;
            foreach (Transform child in go.transform) SetLayer(child.gameObject, layer);
        }
    }
}
