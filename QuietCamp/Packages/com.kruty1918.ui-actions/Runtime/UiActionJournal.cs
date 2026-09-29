using System;
using System.Collections.Generic;
using Kruty1918.UIActions.API;
using UnityEngine;

namespace Kruty1918.UIActions.Runtime
{
    public sealed class UiActionJournal : IUiActionJournal
    {
        public const int DefaultCapacity = 500;

        private readonly Queue<UiActionJournalEntry> _entries = new();

        public int Capacity { get; } = DefaultCapacity;
        public int Count => _entries.Count;

        public void Record(in UiActionRequest request, in UiActionResult result)
        {
            if (!UiActionId.IsValid(request.ActionId.Value))
                return;

            while (_entries.Count >= Capacity)
                _entries.Dequeue();

            _entries.Enqueue(new UiActionJournalEntry(
                Time.realtimeSinceStartupAsDouble,
                Time.frameCount,
                request.ActionId,
                request.Source,
                request.ContextId,
                request.TargetId,
                result.Status,
                result.Reason,
                result.Consumed,
                result.Details));
        }

        public IReadOnlyList<UiActionJournalEntry> GetRecent(int count)
        {
            if (count <= 0 || _entries.Count == 0)
                return Array.Empty<UiActionJournalEntry>();

            UiActionJournalEntry[] snapshot = _entries.ToArray();
            int take = Math.Min(count, snapshot.Length);
            var result = new UiActionJournalEntry[take];
            Array.Copy(snapshot, snapshot.Length - take, result, 0, take);
            return result;
        }
    }
}
