using System;
using System.Collections.Generic;
using System.Reflection;
using System.Linq;
using QuietCamp.Application;
using QuietCamp.Domain;
using QuietCamp.Presentation.World;

namespace QuietCamp.Marketing
{
    /// <summary>Editor boundary to existing player-facing overlays. No new rule or runtime state authority.</summary>
    static class TeaserGameplayEvidence
    {
        static readonly FieldInfo Board=typeof(CampSceneHost).GetField("_renderer",BindingFlags.Instance|BindingFlags.NonPublic);
        public static bool Execute(CampSceneHost host,string command)
        {
            if(!command.StartsWith("route-")&&!command.StartsWith("preview-")&&!command.StartsWith("shade-"))return false;
            var board=Board?.GetValue(host) as BoardRenderer??throw new InvalidOperationException("The CampSceneHost board contract changed.");
            if(command=="shade-on"){board.ShowShade(true);return true;}
            if(command=="shade-off"){board.ShowShade(false);return true;}
            if(command=="preview-off"){board.HidePlacementPreview();return true;}
            if(command=="route-off"){board.HidePath();return true;}
            int split=command.IndexOf('-');if(!int.TryParse(command.Substring(split+1),out int index)||index<0||index>=host.Session.Level.witness.Length)throw new InvalidOperationException("Invalid overlay cue: "+command);
            var p=host.Session.Level.witness[index];
            if(command.StartsWith("preview-"))board.ShowPlacementPreview(p,host.Session.Preview(PlacementCommand.Place(p.guestId,p.x,p.z,p.rotation)));
            else
            {
                var report=host.Session.Preview(PlacementCommand.Place(p.guestId,p.x,p.z,p.rotation));
                var route=report.Routes.FirstOrDefault(r=>r.GuestId==p.guestId)??throw new InvalidOperationException("Missing canonical route evidence.");
                board.ShowRoute(route);
            }
            return true;
        }
    }
}
