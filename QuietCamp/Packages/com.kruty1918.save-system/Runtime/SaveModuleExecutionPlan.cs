using System;
using System.Collections.Generic;
using UnityEngine;

namespace Kruty1918.SaveSystem
{
    /// <summary>
    /// Builds one deterministic execution plan for both save capture and restore.
    /// Module order comes from an injected <see cref="SaveModuleOrdering"/> while
    /// unknown modules fall back to a stable type-name order. Explicit module
    /// order (ISaveModuleExecutionOrder) always wins.
    /// </summary>
    public static class SaveModuleExecutionPlan
    {
        internal const int DefaultOrder = 600;

        private readonly struct Candidate
        {
            public Candidate(ISaveModule module, int order, string typeName)
            {
                Module = module;
                Order = order;
                TypeName = typeName;
            }

            public ISaveModule Module { get; }
            public int Order { get; }
            public string TypeName { get; }
        }

        internal static List<ISaveModule> Build(IReadOnlyList<ISaveModule> modules,
            SaveModuleOrdering ordering = null)
        {
            if (modules == null || modules.Count == 0)
                return new List<ISaveModule>();

            var candidates = new List<Candidate>(modules.Count);
            var seenTypes = new HashSet<Type>();

            for (int index = 0; index < modules.Count; index++)
            {
                ISaveModule module = modules[index];
                if (module == null)
                    continue;

                Type type = module.GetType();
                if (!seenTypes.Add(type))
                {
                    continue;
                }

                string typeName = SaveModuleIdentity.GetStableId(type);
                candidates.Add(new Candidate(module, ResolveOrder(module, typeName, ordering), typeName));
            }

            candidates.Sort(CompareCandidates);

            var result = new List<ISaveModule>(candidates.Count);
            for (int index = 0; index < candidates.Count; index++)
                result.Add(candidates[index].Module);
            return result;
        }

        private static int ResolveOrder(ISaveModule module, string typeName,
            SaveModuleOrdering ordering)
        {
            if (module is ISaveModuleExecutionOrder ordered)
                return ordered.SaveLoadOrder;

            int configured = ordering?.OrderFor(typeName) ?? -1;
            return configured >= 0 ? configured : DefaultOrder;
        }

        private static int CompareCandidates(Candidate left, Candidate right)
        {
            int byOrder = left.Order.CompareTo(right.Order);
            if (byOrder != 0)
                return byOrder;
            return string.CompareOrdinal(left.TypeName, right.TypeName);
        }
    }
}
