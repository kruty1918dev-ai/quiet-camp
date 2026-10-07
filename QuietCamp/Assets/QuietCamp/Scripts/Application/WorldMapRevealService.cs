using System;
using System.Collections.Generic;
using QuietCamp.Domain;

namespace QuietCamp.Application
{
    /// <summary>Progression-derived visibility for the world map. The scroll
    /// position never decides what exists — the player's completed path does:
    /// completed world → current → a short near-future window → atmosphere.
    /// Everything beyond the horizon is Hidden and must render as misty
    /// silhouette, not a clickable node.</summary>
    public sealed class WorldMapRevealService
    {
        /// <summary>How many uncompleted main nodes stay visible past the
        /// current one — near future the player can anticipate.</summary>
        public const int Lookahead = 2;

        readonly WorldMapData _map;
        readonly ProgressionService _progression;
        readonly JourneyAccessService _journeys;
        readonly Dictionary<string, WorldMapNode> _byId = new Dictionary<string, WorldMapNode>(StringComparer.Ordinal);
        readonly Dictionary<string, WorldMapBranch> _branches = new Dictionary<string, WorldMapBranch>(StringComparer.Ordinal);

        public WorldMapRevealService(WorldMapData map, ProgressionService progression, JourneyAccessService journeys)
        {
            _map = map ?? throw new ArgumentNullException(nameof(map));
            _progression = progression ?? throw new ArgumentNullException(nameof(progression));
            _journeys = journeys;
            foreach (var node in map.Nodes) _byId[node.LevelId] = node;
            foreach (var branch in map.Branches) _branches[branch.Id] = branch;
        }

        /// <summary>Order of the node the journey is standing at — the first
        /// uncompleted main node, or the last one when everything is done.</summary>
        public int CurrentOrder
        {
            get
            {
                foreach (var node in _map.Nodes)
                    if (node.Main && !_progression.IsCompleted(node.LevelId)) return node.Order;
                return Math.Max(1, _map.Nodes.Length);
            }
        }

        /// <summary>Everything with Order > Horizon is beyond the atmosphere.</summary>
        public int Horizon => CurrentOrder + Lookahead;

        /// <summary>Main-path node state. Replaying a finished level never
        /// changes the world — completed stays Completed, nothing re-hides.</summary>
        public WorldNodeState State(string levelId)
        {
            if (!_byId.TryGetValue(levelId, out var node)) return WorldNodeState.Hidden;
            return node.Main ? MainState(node) : BranchNodeState(levelId);
        }

        public WorldNodeState MainState(WorldMapNode node)
        {
            if (_progression.IsCompleted(node.LevelId)) return WorldNodeState.Completed;
            if (node.Order == CurrentOrder) return WorldNodeState.Current;
            if (node.Order <= Horizon) return WorldNodeState.Available;
            return WorldNodeState.Hidden;
        }

        /// <summary>Branch visibility: gated branches tease their anchor + hero
        /// landmark only; once open, teaser depth applies from progress.</summary>
        public WorldNodeState BranchState(WorldMapBranch branch)
        {
            if (branch == null || branch.NodeIds.Length == 0) return WorldNodeState.Hidden;
            var gate = MainCompleted() >= branch.RequiredCompletions;
            var decision = _journeys?.Evaluate(branch.NodeIds[0]);
            if (!gate || (decision.HasValue && !decision.Value.CanStart && decision.Value.State == JourneyAccessState.Predecessor))
                return WorldNodeState.Teaser; // silhouette + landmark, content veiled
            var done = 0;
            foreach (var id in branch.NodeIds) if (_progression.IsCompleted(id)) done++;
            return done >= branch.NodeIds.Length ? WorldNodeState.Completed : WorldNodeState.Available;
        }

        /// <summary>State of the i-th node inside a branch: own progress within
        /// the branch plus the reveal taper — later nodes fade into mist.</summary>
        public WorldNodeState BranchNodeState(string branchId, int index)
        {
            if (!_branches.TryGetValue(branchId, out var branch) || index < 0 || index >= branch.NodeIds.Length)
                return WorldNodeState.Hidden;
            if (BranchState(branch) == WorldNodeState.Teaser)
                return index < branch.TeaserDepth ? WorldNodeState.Teaser : WorldNodeState.Hidden;
            var id = branch.NodeIds[index];
            if (_progression.IsCompleted(id)) return WorldNodeState.Completed;
            var firstOpen = -1;
            for (var i = 0; i < branch.NodeIds.Length; i++)
                if (!_progression.IsCompleted(branch.NodeIds[i])) { firstOpen = i; break; }
            if (firstOpen < 0) return WorldNodeState.Completed;
            if (index == firstOpen) return WorldNodeState.Current;
            return index <= firstOpen + branch.TeaserDepth - 1 ? WorldNodeState.Teaser : WorldNodeState.Hidden;
        }

        WorldNodeState BranchNodeState(string levelId)
        {
            foreach (var branch in _map.Branches)
            {
                var index = Array.IndexOf(branch.NodeIds, levelId);
                if (index >= 0) return BranchNodeState(branch.Id, index);
            }
            return WorldNodeState.Hidden;
        }

        /// <summary>Chunks allowed to exist as live geometry right now:
        /// every chunk touching the completed path or the visible horizon —
        /// the rest stay data-only.</summary>
        public List<int> ActiveChunks()
        {
            var active = new List<int>();
            var horizon = Horizon;
            foreach (var chunk in _map.Chunks)
                if (chunk.FirstOrder <= horizon) active.Add(chunk.Index);
            return active;
        }

        /// <summary>Branches whose anchor is inside the revealed world.</summary>
        public IEnumerable<WorldMapBranch> RevealedBranches()
        {
            foreach (var branch in _map.Branches)
                if (branch.AttachOrder <= Horizon) yield return branch;
        }

        int MainCompleted()
        {
            var done = 0;
            foreach (var node in _map.Nodes)
                if (node.Main && _progression.IsCompleted(node.LevelId)) done++;
            return done;
        }
    }
}
