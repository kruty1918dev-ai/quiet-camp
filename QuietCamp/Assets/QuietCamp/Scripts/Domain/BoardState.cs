using System.Collections.Generic;
using System.Linq;
namespace QuietCamp.Domain
{
    /// <summary>
    /// Immutable validated board layout plus a monotonically increasing revision.
    /// Callers receive copies — UI can never mutate x/z in place.
    /// </summary>
    public sealed class BoardState
    {
        public static readonly BoardState Empty = new BoardState(new Placement[0], 0);

        readonly Placement[] _placements;
        public int Revision { get; }
        public int Count => _placements.Length;

        public BoardState(IEnumerable<Placement> placements, int revision)
        {
            _placements = (placements ?? Enumerable.Empty<Placement>())
                .Where(p => p != null).Select(p => p.Copy()).ToArray();
            Revision = revision;
        }

        /// <summary>Snapshot copy of every placement; mutating it does not touch state.</summary>
        public Placement[] Snapshot() => _placements.Select(p => p.Copy()).ToArray();

        public IReadOnlyList<Placement> Placements => _placements;

        public Placement Find(string guestId)
        {
            if (string.IsNullOrEmpty(guestId)) return null;
            foreach (var p in _placements) if (p.guestId == guestId) return p.Copy();
            return null;
        }

        public bool Contains(string guestId) => _placements.Any(p => p.guestId == guestId);

        /// <summary>New state with the given layout; revision is caller-owned.</summary>
        public BoardState With(IEnumerable<Placement> placements, int revision)
            => new BoardState(placements, revision);
    }
}
