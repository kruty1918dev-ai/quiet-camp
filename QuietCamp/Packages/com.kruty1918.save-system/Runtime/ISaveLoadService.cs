using System.Collections.Generic;

namespace Kruty1918.SaveSystem
{
    public interface ISaveLoadService
    {
        bool TryLoad(
            int slot,
            IReadOnlyList<ISaveModule> modules,
            string requiredBlockModuleFullName,
            out string errorMessage);
    }
}
