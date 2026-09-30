using System.IO;
using System.Linq;
using Newtonsoft.Json;
using NUnit.Framework;
using QuietCamp.Domain;
namespace QuietCamp.Tests
{
    public sealed class RulesTests
    {
        const string Root="Assets/QuietCamp/Resources/QuietCamp/Levels/";
        LevelData Control()=>JsonConvert.DeserializeObject<LevelData>(File.ReadAllText(Root+"QC_TEST.json"));
        [Test] public void EveryShippedWitnessIsSolved()
        {
            var paths=Directory.GetFiles(Root,"*.json");Assert.That(paths.Length,Is.EqualTo(11));
            foreach(var path in paths)
            {
                var level=JsonConvert.DeserializeObject<LevelData>(File.ReadAllText(path));
                var result=RuleEvaluator.Evaluate(level,level.witness);
                Assert.That(result.IsSolved,Is.True,level.id+":"+string.Join(",",result.Issues.Select(i=>i.Code)));
            }
        }
        [Test] public void OverlapIsHardRejected()
        {var l=Control();l.witness[2].x=0;l.witness[2].z=0;Assert.That(RuleEvaluator.Evaluate(l,l.witness).CanCommit,Is.False);}
        [Test] public void NoiseIsSoftFailure()
        {var l=Control();l.witness[2].x=3;var r=RuleEvaluator.Evaluate(l,l.witness);Assert.That(r.CanCommit,Is.True);Assert.That(r.Issues.Any(i=>i.Code=="quiet"),Is.True);}
        [Test] public void RotationChangesDoor()
        {var p=new Placement{x=0,z=0};var expected=new[]{new Cell(1,2),new Cell(2,0),new Cell(0,-1),new Cell(-1,1)};for(int i=0;i<4;i++){p.rotation=i;Assert.That(RuleEvaluator.Door(p),Is.EqualTo(expected[i]));}}
        [Test] public void CommitUndoRedoUsesCopies()
        {var l=Control();var h=new CommandHistory();Assert.That(h.Commit(l,l.witness),Is.True);l.witness[0].x=99;Assert.That(h.Current[0].x,Is.EqualTo(0));Assert.That(h.Undo(),Is.True);Assert.That(h.Current.Length,Is.EqualTo(0));Assert.That(h.Redo(),Is.True);Assert.That(h.Current.Length,Is.EqualTo(3));}
        [Test] public void EmptyBoardIsNotSolved()
        {var l=Control();Assert.That(RuleEvaluator.Evaluate(l,new Placement[0]).IsSolved,Is.False);}
    }
}
