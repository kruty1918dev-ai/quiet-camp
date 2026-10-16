using System;
using System.Collections.Generic;
using QuietCamp.Domain;

namespace QuietCamp.Application
{
    /// <summary>Exhaustive, rotation-specific placement hints using the game's actual rules.</summary>
    public static class PlacementGuidance
    {
        public enum Fit { Blocked, UnmetWish, Ready }
        public readonly struct Site
        {
            public readonly int X, Z;
            public readonly Fit Status;
            public Site(int x, int z, Fit status) { X=x; Z=z; Status=status; }
        }
        public readonly struct Feedback
        {
            public readonly string Key, GuestId;
            public bool IsIssue => Key != null;
            public Feedback(string key, string guestId = null) { Key=key; GuestId=guestId; }
        }

        /// <summary>Explain evidence from the canonical evaluator, never a preferred layout.
        /// An unplaced guest is a next action, not a mistake. Hard failures take priority.</summary>
        public static Feedback Explain(LevelData level, RuleReport report, string focusedGuestId = null)
        {
            if(report==null)return default;
            bool Relevant(RuleIssue issue)=>issue.Code!="missing"&&issue.Code!="friends-missing";
            bool Focused(RuleIssue issue)=>issue.GuestId==focusedGuestId || issue.Code=="friends"
                && Array.Exists(level.friends,p=>p.Length==2&&p[0]==issue.GuestId&&p[1]==focusedGuestId);
            var issue=report.Issues.Find(i=>i.Hard&&Relevant(i))
                ??report.Issues.Find(i=>Relevant(i)&&Focused(i))??report.Issues.Find(Relevant);
            if(issue==null)return default;
            if(issue.Code=="overlap"&&issue.Cells!=null&&Array.Exists(issue.Cells,c=>CampAccess.IsReserved(level,c)))
                return new Feedback("rule.entry",issue.GuestId);
            if(issue.Code=="path")
            {
                var route=report.Routes.Find(r=>!r.Reachable&&r.GuestId==issue.GuestId
                    &&(issue.Cells==null||Array.IndexOf(issue.Cells,r.Goal)>=0));
                if(route!=null)return new Feedback(route.Reason==RouteFailureReason.OutsideTrail?"rule.path.outside-trail"
                    :route.Reason==RouteFailureReason.Blocked?"rule.path.blocked":"rule.path.disconnected",issue.GuestId);
            }
            return new Feedback("rule."+issue.Code,issue.GuestId);
        }
        public static Site[] Sites(CampSession session, string guestId, int rotation)
        {
            var result=new List<Site>();
            if (session.IsCompleted || guestId==null) return result.ToArray();
            for(int x=0;x<session.Level.width-1;x++)
            for(int z=0;z<session.Level.height-1;z++)
            {
                var report=session.Preview(PlacementCommand.Place(guestId,x,z,rotation));
                result.Add(new Site(x,z,!report.CanCommit?Fit.Blocked:
                    report.Issues.Count==0?Fit.Ready:Fit.UnmetWish));
            }
            return result.ToArray();
        }
    }
}
