using System;
using System.Collections.Generic;

namespace Kruty1918.SaveSystem
{
    public sealed class SaveModuleRegistry : ISaveModuleRegistry
    {
        private readonly List<ISaveModule> _modules = new List<ISaveModule>();

        public void Register(ISaveModule module)
        {
            if (module == null || _modules.Contains(module))
                return;

            string moduleId = SaveModuleIdentity.GetStableId(module.GetType());
            for (int index = 0; index < _modules.Count; index++)
            {
                ISaveModule registered = _modules[index];
                if (registered == null)
                    continue;

                if (registered.GetType() == module.GetType())
                    return;

                string registeredId = SaveModuleIdentity.GetStableId(registered.GetType());
                if (string.Equals(registeredId, moduleId, StringComparison.Ordinal))
                {
                    throw new InvalidOperationException(
                        $"Save module ID '{moduleId}' is used by both " +
                        $"'{registered.GetType().FullName}' and '{module.GetType().FullName}'.");
                }
            }

            _modules.Add(module);
        }

        public void Unregister(ISaveModule module)
        {
            if (module == null)
                return;

            _modules.Remove(module);
        }

        public void AppendRegisteredModules(List<ISaveModule> target)
        {
            if (target == null)
                return;

            for (int index = 0; index < _modules.Count; index++)
            {
                var module = _modules[index];
                if (module == null || target.Contains(module))
                    continue;

                target.Add(module);
            }
        }
    }
}
