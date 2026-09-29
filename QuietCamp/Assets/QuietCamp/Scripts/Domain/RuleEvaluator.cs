using System;
using System.Collections.Generic;
using System.Linq;
namespace QuietCamp.Domain
{
    // Assumes LevelData was structurally validated by the content loader.
    // Occupancy is entirely logical; no Unity Physics calls are allowed here.
    public static class RuleEvaluator
    {
        static readonly Cell[] Delta={new Cell(0,1),new Cell(1,0),new Cell(0,-1),new Cell(-1,0)};
        public static Cell[] Footprint(Placement p) => new[] {
            new Cell(p.x,p.z),new Cell(p.x+1,p.z),new Cell(p.x,p.z+1),new Cell(p.x+1,p.z+1) };
        public static Cell Door(Placement p)
        {
            switch(p.rotation)
            {
                case 0:return new Cell(p.x+1,p.z+2);
                case 1:return new Cell(p.x+2,p.z);
                case 2:return new Cell(p.x,p.z-1);
                case 3:return new Cell(p.x-1,p.z+1);
                default:throw new ArgumentOutOfRangeException(nameof(p.rotation));
            }
        }
        public static bool Inside(LevelData l,Cell c) => c.X>=0 && c.Z>=0 && c.X<l.width && c.Z<l.height;
        static HashSet<Cell> Cells(int[][] a) => new HashSet<Cell>(a.Select(c=>new Cell(c[0],c[1])));
        public static List<Cell> Path(LevelData l,HashSet<Cell> occupied,Cell start,Cell goal)
        {
            if(!Inside(l,start)||!Inside(l,goal)||occupied.Contains(start)||occupied.Contains(goal)) return null;
            var q=new Queue<Cell>();var prev=new Dictionary<Cell,Cell>();q.Enqueue(start);prev[start]=start;
            while(q.Count>0)
            {
                var c=q.Dequeue();
                if(c.Equals(goal))
                {
                    var r=new List<Cell>{c};while(!c.Equals(start)){c=prev[c];r.Add(c);}r.Reverse();return r;
                }
                foreach(var d in Delta)
                {
                    var n=new Cell(c.X+d.X,c.Z+d.Z);
                    if(Inside(l,n)&&!occupied.Contains(n)&&!prev.ContainsKey(n)){prev[n]=c;q.Enqueue(n);}
                }
            }
            return null;
        }
        public static RuleReport Evaluate(LevelData l,IReadOnlyList<Placement> placements,bool requireAll=true)
        {
            var report=new RuleReport();var occupied=Cells(l.blocked);var shaded=Cells(l.shade);
            var entry=new Cell(l.entry[0],l.entry[1]);var byId=new Dictionary<string,Placement>();
            var guests=l.guests.ToDictionary(g=>g.id);
            foreach(var p in placements)
            {
                if(p==null||string.IsNullOrEmpty(p.guestId)||!guests.ContainsKey(p.guestId)||byId.ContainsKey(p.guestId))
                {report.Issues.Add(new RuleIssue("guest-id",p?.guestId,true));continue;}
                byId.Add(p.guestId,p);
                if(p.rotation<0||p.rotation>3){report.Issues.Add(new RuleIssue("rotation",p.guestId,true));continue;}
                var f=Footprint(p);
                if(f.Any(c=>!Inside(l,c)))report.Issues.Add(new RuleIssue("bounds",p.guestId,true,f));
                if(f.Any(c=>occupied.Contains(c)||c.Equals(entry)))report.Issues.Add(new RuleIssue("overlap",p.guestId,true,f));
                occupied.UnionWith(f);
            }
            // Hard-invalid previews cannot produce reliable route/personal-rule diagnostics.
            if(!report.CanCommit)return report;
            if(requireAll)foreach(var g in l.guests)
                if(!byId.ContainsKey(g.id))report.Issues.Add(new RuleIssue("missing",g.id,false));
            foreach(var p in placements)
            {
                var f=Footprint(p);var g=guests[p.guestId];var door=Door(p);
                if(Path(l,occupied,entry,door)==null)report.Issues.Add(new RuleIssue("path",p.guestId,false,door));
                if(g.shade && f.Any(c=>!shaded.Contains(c)))report.Issues.Add(new RuleIssue("shade",p.guestId,false,f.Where(c=>!shaded.Contains(c)).ToArray()));
                if(g.quiet && l.noise.Any(n=>f.Any(c=>Math.Abs(c.X-n[0])+Math.Abs(c.Z-n[1])<=2)))
                    report.Issues.Add(new RuleIssue("quiet",p.guestId,false,f));
            }
            foreach(var pair in l.friends)
            {
                if(!byId.TryGetValue(pair[0],out var a)||!byId.TryGetValue(pair[1],out var b))
                {if(requireAll)report.Issues.Add(new RuleIssue("friends-missing",pair[0],false));continue;}
                var path=Path(l,occupied,Door(a),Door(b));
                if(path==null||path.Count-1>3)report.Issues.Add(new RuleIssue("friends",pair[0],false,Door(a),Door(b)));
            }
            return report;
        }
    }
}
