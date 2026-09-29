using System.Collections.Generic;

namespace Kruty1918.UIActions.API
{
    public interface IUiActionJournal
    {
        int Capacity { get; }
        int Count { get; }
        void Record(in UiActionRequest request, in UiActionResult result);
        IReadOnlyList<UiActionJournalEntry> GetRecent(int count);
    }
}
