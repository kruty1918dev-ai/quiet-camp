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
var levels = new List<LevelData>();
for (int number = 1; number <= 30; number++)
{
    string id = number <= 10 ? $"QC{number:000}" : $"gen:qc_camp:{number-10}";
    var level = CampCampaignAuthoring.Create(id, number, CampCampaignAuthoring.SeedForNumber(number));
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
Console.WriteLine("[Campaign] 30/30 structural validation + saved witness + independent solver passed");

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
