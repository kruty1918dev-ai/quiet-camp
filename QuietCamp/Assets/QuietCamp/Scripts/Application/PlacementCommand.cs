using System.Collections.Generic;
using System.Linq;
using QuietCamp.Domain;
namespace QuietCamp.Application
{
    /// <summary>
    /// One atomic placement change: place new, move/rotate existing, or remove.
    /// Before/After are stored as copies; Apply removes the guest's old footprint
    /// from the temporary state before the new one is validated.
    /// </summary>
    public sealed class PlacementCommand
    {
        public string GuestId;
        public Placement Before; // null = guest was unplaced
        public Placement After;  // null = removed
        public int Revision;

        public static PlacementCommand Place(string guestId, int x, int z, int rotation)
            => new PlacementCommand
            {
                GuestId = guestId,
                After = new Placement { guestId = guestId, x = x, z = z, rotation = rotation },
            };

        public static PlacementCommand Remove(string guestId)
            => new PlacementCommand { GuestId = guestId };

        public Placement[] Apply(IReadOnlyList<Placement> current)
        {
            var next = (current ?? new List<Placement>())
                .Where(p => p != null && p.guestId != GuestId)
                .Select(p => p.Copy()).ToList();
            if (After != null) next.Add(After.Copy());
            return next.ToArray();
        }
    }
}
