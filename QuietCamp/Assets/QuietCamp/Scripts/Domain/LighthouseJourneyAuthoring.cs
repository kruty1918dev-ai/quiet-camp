using System;
using System.Linq;

namespace QuietCamp.Domain
{
    public static class LighthouseJourneyAuthoring
    {
        static readonly (int width, int height, int guests)[] Sizes =
        { (6,6,2), (7,5,3), (6,7,3), (7,6,3), (6,8,4), (8,6,4), (7,7,5), (8,7,5) };
        static readonly Cell[][] Rocks =
        {
            new[] { new Cell(3,1) }, new[] { new Cell(3,2), new Cell(5,0) },
            new[] { new Cell(2,1), new Cell(4,3) }, new[] { new Cell(1,1), new Cell(4,4) },
            new[] { new Cell(2,3), new Cell(4,6), new Cell(5,0) }, new[] { new Cell(2,1), new Cell(4,3), new Cell(7,0) },
            new[] { new Cell(2,0), new Cell(5,2), new Cell(6,0) }, new[] { new Cell(3,1), new Cell(4,4), new Cell(7,0) }
        };
        public static LevelData Create(int number)
        {
            if (number < 1 || number > 8) throw new ArgumentOutOfRangeException(nameof(number));
            var size = Sizes[number - 1]; bool fire = number == 2 || number >= 5;
            bool shade = number == 3 || number == 6 || number == 8;
            var level = new LevelData
            {
                id = "QC_LH" + number.ToString("000"), schemaVersion = 1, ruleVersion = 2, order = number, chapter = 1,
                seed = 610500 + number, decorSeed = 719000 + number, generatorVersion = "lighthouse-authored-1",
                width = size.width, height = size.height, entry = new[] { 0, size.height / 2 },
                blocked = Rocks[number - 1].Select(c => new[] { c.X, c.Z }).ToArray(),
                noise = fire ? new[] { new[] { number == 2 ? 5 : size.width - 1, 0 } } : Array.Empty<int[]>(),
                shade = Array.Empty<int[]>(), lighting = number == 8 ? "night" : number >= 5 ? "evening" : "day",
                friends = number == 4 || number == 7 || number == 8 ? new[] { new[] { "g1", "g2" } } : Array.Empty<string[]>(),
                environmentPreset = "meadow", tutorialKey = "",
                guests = Enumerable.Range(1, size.guests).Select(i => new GuestData
                {
                    id = "g" + i, nameKey = "guest." + i, assetId = i == 3 ? "tent_detailedOpen" : "tent_smallOpen",
                    shade = shade && i == 1, quiet = fire && i == size.guests
                }).ToArray(),
                environment = new EnvironmentCompositionData
                {
                    biomeId = "shore", seasonId = number <= 3 ? "summer" : number <= 6 ? "autumn" : "winter",
                    weatherId = number == 3 || number == 5 ? "rain" : number == 4 || number == 7 ? "mist" : "clear",
                    moisture = .58f, treeDensity = .23f, clusterSeed = 88100 + number,
                    meadowSpecies = new[] { "grass", "reed" },
                    storyMotifs = number == 8 ? new[] { "lighthouse", "pier", "bench", "bag", "beacon-ready" }
                        : number >= 4 ? new[] { "lighthouse", "pier", "bench", "bag" } : new[] { "pier", "bench", "bag" },
                    shore = new ShorelineData { kind = "lake", side = "right", offset = size.width * .5f + 4.2f, width = 4, seed = 8100 + number }
                }
            };
            if (number == 4 || number == 7)
                level.exteriorWalkable = Enumerable.Range(Math.Max(0, size.height / 2 - 2), 5).Where(z => z < size.height).Select(z => new[] { -1, z }).ToArray();
            if (shade)
            {
                level.canopies = new[] { new ShadeCanopyData { x = number == 6 ? 6.3f : 1.3f, z = size.height - 1.7f, radiusX = 1.8f, radiusZ = 1.8f } };
                level.shade = ShadeProjection.Cells(level);
            }
            level.objects = level.blocked.Select((c, i) => new EnvironmentObjectData
            {
                x = c[0], z = c[1], rotation = i % 4,
                assetId = level.noise.Any(n => n[0] == c[0] && n[1] == c[1]) ? "campfire_stones" : i % 2 == 0 ? "stone_largeA" : "log"
            }).ToArray();
            var solver = new CampSolver(level, Array.Empty<Placement>(), 20);
            var status = solver.Step(); while (status == CampSolver.Status.Searching) status = solver.Step(.05);
            if (status != CampSolver.Status.Solved) throw new InvalidOperationException(level.id + ": " + status);
            level.witness = solver.Solution;
            var issues = LevelContentValidator.Validate(level);
            if (issues.Count != 0) throw new InvalidOperationException(level.id + ": " + string.Join(",", issues));
            return level;
        }
    }
}
