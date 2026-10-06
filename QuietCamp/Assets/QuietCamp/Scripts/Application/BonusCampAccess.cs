using System;
using System.Collections.Generic;
using QuietCamp.Domain;

namespace QuietCamp.Application
{
    public enum BonusCampState { Locked, ComingSoon, Available, Completed }
    public readonly struct BonusCampAccess
    {
        public readonly int Completed, Required;
        public readonly bool HasPremium, Published, SeasonMatch;
        public readonly string SeasonId;
        public readonly BonusCampState State;
        public bool CanPlay => State == BonusCampState.Available || State == BonusCampState.Completed;
        public BonusCampAccess(int completed, int required, bool hasPremium, bool published, BonusCampState state,
            string seasonId = null, bool seasonMatch = true)
        { Completed=completed;Required=required;HasPremium=hasPremium;Published=published;State=state;
            SeasonId=seasonId;SeasonMatch=seasonMatch; }
    }
    /// <summary>Read-only gates. Previews never grant purchases, mark completion or alter campaign unlocks.</summary>
    public sealed class BonusCampAccessService
    {
        readonly ProgressionService _progression;
        readonly IReadOnlyList<string> _campaign;
        readonly Func<BonusCampDefinition,bool> _published;
        readonly Func<string,bool> _premium;
        readonly Func<string> _season;
        public BonusCampAccessService(ProgressionService progression,IReadOnlyList<string> campaign,
            Func<BonusCampDefinition,bool> published,Func<string,bool> premium=null,Func<string> season=null)
        { _progression=progression;_campaign=campaign;_published=published;_premium=premium;
            _season=season??SeasonalWindow.Now; }
        public BonusCampAccess Evaluate(BonusCampDefinition definition)
        {
            if(definition==null||definition.afterLevel<10||definition.afterLevel%10!=0
                ||definition.afterLevel>_campaign.Count||definition.requiredCompletions<0||definition.requiredCompletions>10)
                return new BonusCampAccess(0,10,false,false,BonusCampState.Locked);
            int completed=0;
            for(int i=definition.afterLevel-10;i<definition.afterLevel;i++)
                if(_progression.IsCompleted(_campaign[i]))completed++;
            bool premium=!definition.requiresPremium||(_premium?.Invoke(definition.id)??false);
            bool published=!string.IsNullOrEmpty(definition.levelId)&&(_published?.Invoke(definition)??false);
            // Seasonal stories only accept players while their window is open.
            bool seasonMatch=string.IsNullOrEmpty(definition.seasonId)||_season()==definition.seasonId;
            bool eligible=completed>=definition.requiredCompletions&&premium&&seasonMatch;
            var state=!eligible?BonusCampState.Locked:!published?BonusCampState.ComingSoon:
                _progression.IsCompleted(definition.levelId)?BonusCampState.Completed:BonusCampState.Available;
            return new BonusCampAccess(completed,definition.requiredCompletions,premium,published,state,
                definition.seasonId,seasonMatch);
        }
    }
}
