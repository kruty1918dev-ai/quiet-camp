using System.Collections.Generic;
using Kruty1918.Atmos;
using QuietCamp.Domain;
using QuietCamp.Infrastructure;
using UnityEngine;
namespace QuietCamp.Presentation.World
{
    /// <summary>
    /// Floating-diorama decor around the board, like the reference render:
    /// no ground slab — a seeded ring of pines/trees/stones/grass/flowers
    /// floats at board level just outside the field (decorSeed keeps the
    /// layout deterministic per level), plus the camp sign just outside the
    /// entry edge. All decor lives on the Decor layer with colliders
    /// stripped by the prefabs' configuration.
    /// </summary>
    public static class DecorSpawner
    {
        /// <summary>How far the decor ring extends beyond the board edge.</summary>
        public const float Apron = 2.1f;

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
            float forbiddenX = halfW + 0.45f, forbiddenZ = halfH + 0.45f;
            float meadowX = halfW + Apron, meadowZ = halfH + Apron;

            foreach (var d in TestDecor)
            {
                var pos = new Vector3(d.x, 0f, d.z);
                ClampOutside(ref pos, forbiddenX, forbiddenZ, meadowX, meadowZ);
                var go = Spawn(catalog, decorRoot, d.id, pos, rng.Next(360));
                if (go != null) ScaleToHeight(go, d.height);
            }

            Scatter(catalog, decorRoot, rng, "tree_pineRoundA",
                7 - TestDecorCount("tree_pineRoundA"), forbiddenX, forbiddenZ, meadowX, meadowZ);
            Scatter(catalog, decorRoot, rng, "tree_default",
                6 - TestDecorCount("tree_default"), forbiddenX, forbiddenZ, meadowX, meadowZ);
            Scatter(catalog, decorRoot, rng, "stone_largeA", 3,
                forbiddenX, forbiddenZ, meadowX, meadowZ);
            Scatter(catalog, decorRoot, rng, "grass", 30,
                forbiddenX - 0.15f, forbiddenZ - 0.15f, meadowX, meadowZ);
            Scatter(catalog, decorRoot, rng, "flower_yellowA", 8,
                forbiddenX - 0.15f, forbiddenZ - 0.15f, meadowX, meadowZ);
            Scatter(catalog, decorRoot, rng, "log", 2,
                forbiddenX, forbiddenZ, meadowX, meadowZ);
            Scatter(catalog, decorRoot, rng, "stump_round", 1,
                forbiddenX, forbiddenZ, meadowX, meadowZ);

            // The sign sits just outside the entry edge, facing the door path.
            var entry = new Cell(level.entry[0], level.entry[1]);
            var signPos = BoardMath.CellCenterWorld(level, entry);
            signPos.x += halfW - entry.X > 1 ? 0.95f : -0.95f;
            signPos.y = 0f;
            Spawn(catalog, decorRoot, "sign", signPos, rng.Next(360));
        }

        /// <summary>Scales the instance so its rendered height matches the contract.</summary>
        static void ScaleToHeight(GameObject go, float targetHeight)
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

        static void Scatter(AssetCatalog catalog, Transform root, System.Random rng,
            string assetId, int count,
            float forbiddenX, float forbiddenZ, float meadowX, float meadowZ)
        {
            for (var i = 0; i < Mathf.Max(0, count); i++)
            {
                // Uniform scatter across the meadow, then clamp outside the board.
                var pos = new Vector3(
                    (float)(rng.NextDouble() * 2 - 1) * (meadowX - 0.15f),
                    0f,
                    (float)(rng.NextDouble() * 2 - 1) * (meadowZ - 0.15f));
                ClampOutside(ref pos, forbiddenX, forbiddenZ, meadowX - 0.1f, meadowZ - 0.1f);
                Spawn(catalog, root, assetId, pos, rng.Next(360));
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

        static GameObject Spawn(AssetCatalog catalog, Transform root, string assetId,
            Vector3 pos, int yaw)
        {
            if (!catalog.TryGet(assetId, out var entry) || entry.prefab == null) return null;
            var go = Object.Instantiate(entry.prefab, root);
            go.transform.localPosition = pos;
            go.transform.localEulerAngles = new Vector3(0f, yaw, 0f);
            if (assetId == "grass" || assetId.StartsWith("flower"))
                FoliageSway.Shared.Apply(go);
            SetLayer(go, BoardRenderer.DecorLayer);
            return go;
        }

        static void SetLayer(GameObject go, int layer)
        {
            go.layer = layer;
            foreach (Transform child in go.transform) SetLayer(child.gameObject, layer);
        }
    }
}
