using System.Collections.Generic;
using QuietCamp.Domain;
using QuietCamp.Infrastructure;
using UnityEngine;
namespace QuietCamp.Presentation.World
{
    /// <summary>
    /// Decorative whitelist models around the board: the five fixed QC_TEST
    /// positions from the scene contract, then a seeded scatter (decorSeed)
    /// kept outside the board edge + 0.4 m forbidden band.
    /// </summary>
    public static class DecorSpawner
    {
        static readonly (string id, float x, float z, float h)[] TestDecor =
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

            foreach (var d in TestDecor)
                Spawn(catalog, decorRoot, d.id, new Vector3(d.x, 0f, d.z), rng.Next(360));

            float halfW = level.width / 2f, halfH = level.height / 2f;
            float forbiddenX = halfW + 0.4f, forbiddenZ = halfH + 0.4f;

            Scatter(catalog, decorRoot, rng, "tree_pineRoundA", 5 - TestDecorCount("tree_pineRoundA"),
                forbiddenX, forbiddenZ, 3.2f);
            Scatter(catalog, decorRoot, rng, "tree_default", 5 - TestDecorCount("tree_default"),
                forbiddenX, forbiddenZ, 3.0f);
            Scatter(catalog, decorRoot, rng, "stone_largeA", 3, forbiddenX, forbiddenZ, 1.6f);
            Scatter(catalog, decorRoot, rng, "grass", 24, forbiddenX - 0.4f, forbiddenZ - 0.4f, 1.4f);
            Scatter(catalog, decorRoot, rng, "flower_yellowA", 6, forbiddenX - 0.4f, forbiddenZ - 0.4f, 1.2f);
            Scatter(catalog, decorRoot, rng, "log", 2, forbiddenX, forbiddenZ, 1.6f);
            Scatter(catalog, decorRoot, rng, "stump_round", 1, forbiddenX, forbiddenZ, 1.5f);

            // The sign sits just outside the entry edge, never inside the door path.
            var entry = new Cell(level.entry[0], level.entry[1]);
            var signPos = BoardMath.CellCenterWorld(level, entry);
            signPos.x += level.width / 2f - entry.X > 1 ? 0.9f : -0.9f;
            Spawn(catalog, decorRoot, "sign", signPos, rng.Next(360));
        }

        static int TestDecorCount(string id)
        {
            var n = 0;
            foreach (var d in TestDecor) if (d.id == id) n++;
            return n;
        }

        static void Scatter(AssetCatalog catalog, Transform root, System.Random rng,
            string assetId, int count, float forbiddenX, float forbiddenZ, float radius)
        {
            for (var i = 0; i < count; i++)
            {
                // Ring between the forbidden band and a soft outer radius.
                var angle = (float)(rng.NextDouble() * Mathf.PI * 2);
                var r = forbiddenX + 0.3f + (float)(rng.NextDouble() * (radius - 0.3f));
                var pos = new Vector3(Mathf.Cos(angle) * r, 0f, Mathf.Sin(angle) * r * (forbiddenZ / forbiddenX));
                if (Mathf.Abs(pos.x) < forbiddenX && Mathf.Abs(pos.z) < forbiddenZ)
                {
                    // Push out along the dominant axis.
                    if (Mathf.Abs(pos.x) / forbiddenX > Mathf.Abs(pos.z) / forbiddenZ)
                        pos.x = Mathf.Sign(pos.x) * (forbiddenX + 0.3f);
                    else pos.z = Mathf.Sign(pos.z) * (forbiddenZ + 0.3f);
                }
                Spawn(catalog, root, assetId, pos, rng.Next(360));
            }
        }

        static void Spawn(AssetCatalog catalog, Transform root, string assetId,
            Vector3 pos, int yaw)
        {
            if (!catalog.TryGet(assetId, out var entry) || entry.prefab == null) return;
            var go = Object.Instantiate(entry.prefab, root);
            go.transform.localPosition = pos;
            go.transform.localEulerAngles = new Vector3(0f, yaw, 0f);
            SetLayer(go, BoardRenderer.DecorLayer);
        }

        static void SetLayer(GameObject go, int layer)
        {
            go.layer = layer;
            foreach (Transform child in go.transform) SetLayer(child.gameObject, layer);
        }
    }
}
