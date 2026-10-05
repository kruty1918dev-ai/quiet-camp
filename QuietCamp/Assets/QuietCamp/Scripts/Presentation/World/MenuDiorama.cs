using QuietCamp.Domain;
using QuietCamp.Infrastructure;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace QuietCamp.Presentation.World
{
    /// <summary>
    /// A quiet preview of a real clearing, using the gameplay renderers,
    /// environment seed and lighting. It has no session, solver or input.
    /// </summary>
    public static class MenuDiorama
    {
        /// <summary>The previous launch is excluded without altering Unity's shared random state.</summary>
        public static string SelectId(IReadOnlyList<LevelSummary> levels, string previousId, int ticket)
        {
            if (levels == null || levels.Count == 0) throw new InvalidOperationException("No menu clearings available.");
            previousId = CampContent.CanonicalId(previousId);
            int count = 0;
            foreach (var level in levels) if (level.id != previousId) count++;
            if (count == 0) return levels[0].id;
            int selected = (int)((uint)ticket % (uint)count);
            foreach (var level in levels)
                if (level.id != previousId && selected-- == 0) return level.id;
            throw new InvalidOperationException("Menu clearing selection failed.");
        }

        /// <summary>Decorative, reachable tents rather than the level's saved solution.
        /// This never reads witness placements or changes the level or player state.</summary>
        public static Placement[] DecorativePlacements(LevelData level)
        {
            var candidates = new List<Placement>();
            for (int x = 0; x < level.width - 1; x++)
            for (int z = 0; z < level.height - 1; z++)
            for (int rotation = 0; rotation < 4; rotation++)
                candidates.Add(new Placement { x = x, z = z, rotation = rotation });
            var random = new System.Random(unchecked(level.decorSeed * 577 + 391));
            for (int i = candidates.Count - 1; i > 0; i--)
            {
                int other = random.Next(i + 1);
                (candidates[i], candidates[other]) = (candidates[other], candidates[i]);
            }
            var tents = new List<Placement>();
            foreach (var guest in level.guests ?? Array.Empty<GuestData>())
            {
                foreach (var candidate in candidates)
                {
                    var p = candidate.Copy(); p.guestId = guest.id;
                    tents.Add(p);
                    var report = RuleEvaluator.Evaluate(level, tents, false);
                    if (report.CanCommit && !report.Issues.Exists(issue => issue.Code == "path")) break;
                    tents.RemoveAt(tents.Count - 1);
                }
                if (tents.Count >= 2) break;
            }
            return tents.ToArray();
        }

        /// <summary>One scene-owned board; the host releases its materials and presenters.</summary>
        public static BoardRenderer Build(Transform world, AssetCatalog catalog, LevelData level)
        {
            Transform Child(string name)
            {
                var child = new GameObject(name).transform;
                child.SetParent(world, false); return child;
            }
            var board = new BoardRenderer(level, catalog, Child("Base"), Child("Grid"), Child("Obstacle"), Child("Tents"), Child("Overlay"));
            board.SyncPlacements(DecorativePlacements(level), level, 1, true);
            foreach (var tent in board.Tents.Values)
                foreach (var child in tent.Root.GetComponentsInChildren<Transform>())
                    if (child.name == "DoorDot") child.gameObject.SetActive(false);
            DecorSpawner.Spawn(level, catalog, Child("DecorRoot"));
            // The menu has no world interaction; HTML retains its normal raycasts.
            foreach (var collider in world.GetComponentsInChildren<Collider>()) collider.enabled = false;
            return board;
        }

        /// <summary>Menu scene carries no lighting rig — one directional sun
        /// driven by the active atmosphere profile.</summary>
        public static Light CreateSun(Transform parent)
        {
            var go = new GameObject("DirectionalLight");
            go.transform.SetParent(parent, false);
            var light = go.AddComponent<Light>();
            light.type = LightType.Directional;
            light.shadows = LightShadows.Soft;
            RenderSettings.sun = light;
            return light;
        }

        public static void ApplySun(Light sun, AtmosphereCatalog.Profile profile)
        {
            if (sun == null || profile == null) return;
            sun.transform.localEulerAngles = new Vector3(profile.Elevation, 65f, 0f);
            sun.intensity = profile.SunIntensity;
            sun.color = profile.Sun;
        }

    }
}
