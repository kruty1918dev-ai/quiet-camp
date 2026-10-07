using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json;
using QuietCamp.Domain;

// Editor-independent authoring harness using the game's actual Domain code.
// This compiles tooling, never a Unity player or Android application.
CheckWalkingNetwork();
// World-map campaign plan (cozy-campaign-4). Deterministic (id, pacing-number)
// pairs; the pacing number drives size/motifs inside CampCampaignAuthoring.
// Frozen ids already shipped must keep their exact number to stay identical:
// QC001-010 + gen:qc_camp:1-32 (main 1-42), gen:qc_camp:33-35 (memories 43-45),
// gen:qc_camp:51-54 (bonus glades 61-64). New content uses salted seeds.
var plan = new List<(string Id, int Number)>();
for (int n = 1; n <= 42; n++) plan.Add((n <= 10 ? $"QC{n:000}" : $"gen:qc_camp:{n - 10}", n));
for (int n = 43; n <= 45; n++) plan.Add(($"gen:qc_camp:{n - 10}", n));       // memories 33-35 (frozen)
for (int n = 61; n <= 64; n++) plan.Add(($"gen:qc_camp:{n - 10}", n));       // legacy bonus 51-54
for (int n = 43; n <= 110; n++) plan.Add(($"gen:qc_camp:{n + 12}", n));      // main 43-110 -> 55-122
// Story branches mirror monetization.json journeys 1:1 — count and prefix
// must match or the catalog marks the journey unpublished.
for (int n = 1; n <= 12; n++) plan.Add(($"gen:qc_lh:{8 + n}", 35 + (n * 5) % 55));
for (int n = 1; n <= 18; n++) plan.Add(($"gen:qc_st:{n}", 46 + (n * 11) % 60));
for (int n = 1; n <= 28; n++) plan.Add(($"gen:qc_gd:{n}", 46 + (n * 7) % 60));
for (int n = 1; n <= 24; n++) plan.Add(($"gen:qc_mt:{n}", 60 + (n * 9) % 45));
for (int n = 1; n <= 30; n++) plan.Add(($"gen:qc_rs:{n}", 50 + (n * 8) % 55));
for (int n = 1; n <= 20; n++) plan.Add(($"gen:qc_cs:{n}", 55 + (n * 6) % 50));
// Bonus glades: number picks the pacing/season band so each slot's interior
// agrees with its window (spring/summer/autumn/winter rows below).
int[] bnNumbers = { 38, 52, 63, 77, 11, 36, 49, 62, 72,   // standard glades
                    28, 12, 17,                          // spring/summer/autumn windows
                    45, 58, 70,                          // premium overlooks
                    71, 94,                              // challenge dens
                    83, 66, 105, 59 };                   // spare pool
for (int n = 1; n <= bnNumbers.Length; n++) plan.Add(($"gen:qc_bn:{n}", bnNumbers[n - 1]));
bool Frozen(string id) => id.StartsWith("QC0") || id == "gen:qc_camp:33" || id == "gen:qc_camp:34"
    || id == "gen:qc_camp:35" || Array.Exists(new[] { 51, 52, 53, 54 }, v => id == $"gen:qc_camp:{v}")
    || (id.StartsWith("gen:qc_camp:") && int.TryParse(id.Substring(12), out var gi) && gi <= 32);
var levels = new List<LevelData>();
foreach (var (id, number) in plan)
{
    var level = CampCampaignAuthoring.Create(id, number,
        CampCampaignAuthoring.SeedForNumber(number) + (Frozen(id) ? 0 : IdSalt(id)));
    level.contentHash = "";
    level.contentHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(level)))).ToLowerInvariant();
    var errors = LevelContentValidator.Validate(level);
    if (errors.Count > 0) throw new InvalidOperationException(id + ": " + string.Join(",", errors));
    if (!RuleEvaluator.Evaluate(level, level.witness).IsSolved) throw new InvalidOperationException(id + ": invalid witness");
    var solver = new CampSolver(level, Array.Empty<Placement>(), 20);
    var status = solver.Step(); while (status == CampSolver.Status.Searching) status = solver.Step(.05);
    if (status != CampSolver.Status.Solved) throw new InvalidOperationException(id + ": independent solver " + status);
    levels.Add(level);
    Console.WriteLine($"[Campaign] {id} {level.width}x{level.height} {level.environment.seasonId}/{level.environment.biomeId}/{level.environment.weatherId} guests={level.guests.Length} objects={level.objects.Length} nodes={solver.Nodes}");
}
File.WriteAllText(args[0], JsonConvert.SerializeObject(levels, Formatting.Indented));
Console.WriteLine($"[Campaign] {levels.Count}/{levels.Count} structural validation + saved witness + independent solver passed");

/// <summary>Deterministic per-id seed offset — new ids get distinct layouts
/// even when they share a pacing number with the main path.</summary>
static int IdSalt(string id)
{
    int h = 17;
    foreach (char c in id) h = unchecked(h * 31 + c);
    return h;
}

static void CheckWalkingNetwork()
{
    LevelData Empty(int version)=>new LevelData {schemaVersion=1,ruleVersion=version,id="walking-check",width=6,height=6,
        entry=new[]{5,5},blocked=Array.Empty<int[]>(),shade=Array.Empty<int[]>(),noise=Array.Empty<int[]>(),friends=Array.Empty<string[]>(),
        guests=new[]{new GuestData{id="a",assetId="tent_smallOpen",nameKey="guest.a"},new GuestData{id="b",assetId="tent_smallOpen",nameKey="guest.b"}}};
    void Expect(bool condition,string message){if(!condition)throw new InvalidOperationException("Walking regression: "+message);}
    foreach(int version in new[]{1,2})
    {
        var level=Empty(version);level.friends=new[]{new[]{"a","b"}};
        var tents=new[]{new Placement{guestId="a",x=0,z=0},new Placement{guestId="b",x=2,z=1,rotation=3}};
        Expect(RuleEvaluator.Door(tents[0]).Equals(RuleEvaluator.Door(tents[1])),"fixture shares doorway");
        Expect(RuleEvaluator.Evaluate(level,tents).IsSolved,"shared door remains solved on v"+version);
    }
    var outside=Empty(2);outside.exteriorWalkable=Enumerable.Range(0,6).Select(x=>new[]{x,-1}).ToArray();
    var outward=new Placement{guestId="a",x=0,z=0,rotation=2};
    Expect(RuleEvaluator.Evaluate(outside,new[]{outward},false).Issues.Count==0,"authored exterior route accepts outward door");
    var solver=new CampSolver(outside,new[]{outward},2);var status=solver.Step();
    while(status==CampSolver.Status.Searching)status=solver.Step(.05);
    Expect(status==CampSolver.Status.Solved&&RuleEvaluator.Evaluate(outside,solver.Solution).IsSolved,"solver agrees with exterior route");
    outside.ruleVersion=1;
    Expect(RuleEvaluator.Evaluate(outside,new[]{outward},false).Issues.Any(i=>i.Code=="path"),"old rules reject exterior door");
    outside.ruleVersion=2;outside.exteriorWalkable=new[]{new[]{0,-1},new[]{1,-1},new[]{2,-1}};outside.blocked=new[]{new[]{2,0}};
    var trapped=RuleEvaluator.Evaluate(outside,new[]{outward},false).Routes.Single();
    Expect(!trapped.Reachable&&trapped.BlockingCells.Length>0,"exterior trail cannot cross occupied gateway");
    Console.WriteLine("[Walking] shared doors v1/v2 + connected authored exterior + blocked gateway + pinned exterior solver passed");
}
