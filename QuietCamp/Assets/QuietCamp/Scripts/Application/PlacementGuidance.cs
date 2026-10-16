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
