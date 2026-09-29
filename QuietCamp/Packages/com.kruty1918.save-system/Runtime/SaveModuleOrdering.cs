using System.Collections.Generic;

namespace Kruty1918.SaveSystem
{
    /// <summary>
    /// Game-supplied ordering table for save modules, keyed by stable module
    /// identity (see <see cref="SaveModuleIdentity"/>). The save pipeline uses it
    /// to place known modules in a deterministic dependency-aware order; modules
    /// absent from the table fall back to a stable type-name order.
    /// </summary>
    public sealed class SaveModuleOrdering
    {
        public static readonly SaveModuleOrdering Empty = new SaveModuleOrdering(null);

        private readonly IReadOnlyDictionary<string, int> _orderByStableId;

        public SaveModuleOrdering(IReadOnlyDictionary<string, int> orderByStableId)
        {
            _orderByStableId = orderByStableId;
        }

        internal int OrderFor(string stableId)
        {
            if (_orderByStableId != null
                && _orderByStableId.TryGetValue(stableId, out int order))
                return order;
            return -1;
        }
    }
}
