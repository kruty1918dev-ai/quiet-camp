using System.Collections.Generic;
using QuietCamp.Domain;
using QuietCamp.Infrastructure;
using UnityEngine;
namespace QuietCamp.Presentation.World
{
    /// <summary>
    /// Decorative meadow around the board: a wide grass slab ("apron") the
    /// board sits in, a seeded ring of pines/trees/stones/grass/flowers on it
    /// (decorSeed keeps layout deterministic per level), plus the camp sign
    /// just outside the entry edge. All decor lives on the Decor layer with
    /// colliders stripped by the prefabs' configuration.
    /// </summary>
    public static class DecorSpawner
    {
        /// <summary>How far the meadow apron extends beyond the board edge.</summary>
        public const float Apron = 2.1f;

        static readonly Color MeadowColor = new Color(0.37f, 0.49f, 0.30f);
        static readonly Color MeadowSideColor = new Color(0.28f, 0.22f, 0.16f);

        // Fixed QC_TEST accents from the scene contract; pushed outside the
        // board ring automatically for larger levels.
        static readonly (string id, float x, float z)[] TestDecor =
        {
            ("tree_default", -3.7f, -2.8f),
            ("tree_default", -2.0f, -3.6f),
            ("tree_pineRoundA", 0.3f, -3.7f),
            ("tree_pineRoundA", 2.8f, -3.6f),
            ("tree_default", -3.6f, 0.3f),
        };

        public static void Spawn(LevelData level, AssetCatalog catalog, Transform decorRoot)
        {
            if (catalog == null || decorRoot == null) return;
            var rng = new System.Random(level.decorSeed);

            float halfW = level.width / 2f, halfH = level.height / 2f;
            float forbiddenX = halfW + 0.45f, forbiddenZ = halfH + 0.45f;
            float meadowX = halfW + Apron, meadowZ = halfH + Apron;

            BuildMeadow(decorRoot, meadowX, meadowZ);

            foreach (var d in TestDecor)
            {
                var pos = new Vector3(d.x, 0f, d.z);
                ClampOutside(ref pos, forbiddenX, forbiddenZ, meadowX, meadowZ);
                Spawn(catalog, decorRoot, d.id, pos, rng.Next(360));
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

        /// <summary>Grass apron + dark soil side — the board floats on a meadow.</summary>
        static void BuildMeadow(Transform root, float meadowX, float meadowZ)
        {
            const float thickness = 0.26f;
            var top = Primitive(PrimitiveType.Cube, "Meadow", root,
                new Vector3(meadowX * 2f, thickness, meadowZ * 2f),
                new Vector3(0f, -thickness / 2f - 0.015f, 0f), MeadowColor);
            var side = Primitive(PrimitiveType.Cube, "MeadowSide", root,
                new Vector3(meadowX * 2f + 0.05f, thickness - 0.06f, meadowZ * 2f + 0.05f),
                new Vector3(0f, -thickness / 2f - 0.035f, 0f), MeadowSideColor);
            _ = top; _ = side;
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

        static void Spawn(AssetCatalog catalog, Transform root, string assetId,
            Vector3 pos, int yaw)
        {
            if (!catalog.TryGet(assetId, out var entry) || entry.prefab == null) return;
            var go = Object.Instantiate(entry.prefab, root);
            go.transform.localPosition = pos;
            go.transform.localEulerAngles = new Vector3(0f, yaw, 0f);
            SetLayer(go, BoardRenderer.DecorLayer);
        }

        static GameObject Primitive(PrimitiveType type, string name, Transform parent,
            Vector3 scale, Vector3 localPos, Color color)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localScale = scale;
            go.transform.localPosition = localPos;
            var collider = go.GetComponent<Collider>();
            if (collider != null) Object.Destroy(collider);
            var renderer = go.GetComponent<Renderer>();
            var shader = Shader.Find("Universal Render Pipeline/Simple Lit");
            var mat = new Material(shader);
            mat.SetColor("_BaseColor", color);
            renderer.sharedMaterial = mat;
            go.layer = BoardRenderer.DecorLayer;
            return go;
        }

        static void SetLayer(GameObject go, int layer)
        {
            go.layer = layer;
            foreach (Transform child in go.transform) SetLayer(child.gameObject, layer);
        }
    }
}
