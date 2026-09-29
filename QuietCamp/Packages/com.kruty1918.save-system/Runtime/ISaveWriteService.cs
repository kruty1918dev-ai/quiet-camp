using System.Collections.Generic;

namespace Kruty1918.SaveSystem
{
    public interface ISaveWriteService
    {
        bool TrySave(int slot, IReadOnlyList<ISaveModule> modules, string requiredBlockModuleFullName, out string errorMessage);
    }
}
