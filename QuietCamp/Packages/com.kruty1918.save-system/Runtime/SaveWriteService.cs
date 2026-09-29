using System;
using System.Collections.Generic;

namespace Kruty1918.SaveSystem
{
    public sealed class SaveWriteService : ISaveWriteService
    {
        private readonly SaveModuleOrdering _ordering;

        public SaveWriteService(SaveModuleOrdering ordering = null)
        {
            _ordering = ordering;
        }

        public bool TrySave(int slot, IReadOnlyList<ISaveModule> modules, string requiredBlockModuleFullName, out string errorMessage)
        {
            errorMessage = null;

            if (!SavePipelineHelper.ValidateSlot(slot))
            {
                errorMessage = $"Invalid slot {slot}";
                return false;
            }

            if (!SavePipelineHelper.EnsureDirectoryExists(out string dirError))
            {
                errorMessage = dirError;
                return false;
            }

            List<(uint blockId, byte[] payload)> blocks;
            try { blocks = SavePipelineHelper.CollectBlocks(modules, _ordering); }
            catch (Exception exception)
            {
                errorMessage = exception.Message;
                return false;
            }
            if (HasRegisteredModule(modules, requiredBlockModuleFullName) && !ContainsBlock(blocks, requiredBlockModuleFullName))
            {
                errorMessage = "Generated world block is missing; save aborted to avoid corrupting the world slot.";
                return false;
            }

            byte[] data = SaveFileCodec.Encode(blocks);
            if (!SavePipelineHelper.VerifyAssembledBuffer(data))
            {
                errorMessage = "Buffer verification failed";
                return false;
            }

            if (!SavePipelineHelper.AtomicWrite(SavePipelineHelper.GetPath(slot), data))
            {
                errorMessage = "Write failed";
                return false;
            }

            return true;
        }

        private static bool HasRegisteredModule(IReadOnlyList<ISaveModule> modules, string moduleTypeFullName)
        {
            if (modules == null || string.IsNullOrWhiteSpace(moduleTypeFullName))
                return false;

            for (int i = 0; i < modules.Count; i++)
            {
                var module = modules[i];
                if (module == null)
                    continue;

                if (string.Equals(
                        SaveModuleIdentity.GetStableId(module.GetType()),
                        moduleTypeFullName,
                        StringComparison.Ordinal))
                    return true;
            }

            return false;
        }

        private static bool ContainsBlock(List<(uint blockId, byte[] payload)> blocks, string moduleTypeFullName)
        {
            if (blocks == null || string.IsNullOrWhiteSpace(moduleTypeFullName))
                return false;

            uint blockId = SaveFileCodec.ComputeBlockId(moduleTypeFullName);
            for (int i = 0; i < blocks.Count; i++)
            {
                if (blocks[i].blockId == blockId)
                    return true;
            }

            return false;
        }
    }
}
