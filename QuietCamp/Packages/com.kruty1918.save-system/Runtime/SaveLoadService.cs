using System;
using System.Collections.Generic;
using System.IO;

namespace Kruty1918.SaveSystem
{
    public sealed class SaveLoadService : ISaveLoadService
    {
        private readonly SaveModuleOrdering _ordering;

        public SaveLoadService(SaveModuleOrdering ordering = null)
        {
            _ordering = ordering;
        }

        public bool TryLoad(int slot, IReadOnlyList<ISaveModule> modules, string requiredBlockModuleFullName, out string errorMessage)
        {
            errorMessage = null;
            if (!SavePipelineHelper.ValidateSlot(slot))
            {
                errorMessage = $"Invalid slot {slot}";
                return false;
            }

            string path = SavePipelineHelper.GetPath(slot);
            if (TryLoadFile(path, modules, requiredBlockModuleFullName, $"slot {slot}",
                    out errorMessage, out bool restoreStarted)) return true;
            // A failed legacy restore may already have changed the scene. Do not apply a second save over it.
            if (restoreStarted) return false;

            string primaryError = errorMessage;
            if (TryLoadFile(path + ".bak", modules, requiredBlockModuleFullName, $"slot {slot} backup",
                    out string backupError, out _))
            {
                errorMessage = null;
                return true;
            }
            errorMessage = $"{primaryError} Backup: {backupError}";
            return false;
        }

        private bool TryLoadFile(string path, IReadOnlyList<ISaveModule> modules,
            string requiredBlockModuleFullName, string label, out string errorMessage, out bool restoreStarted)
        {
            errorMessage = null;
            restoreStarted = false;
            if (!File.Exists(path))
            {
                errorMessage = $"{label}: save file not found.";
                return false;
            }

            byte[] bytes;
            try { bytes = File.ReadAllBytes(path); }
            catch (Exception exception)
            {
                errorMessage = $"{label}: cannot read file: {exception.Message}";
                return false;
            }
            return SavePipelineHelper.ExecuteLoad(bytes, modules, label, out errorMessage, out restoreStarted,
                requiredBlockModuleFullName, _ordering);
        }
    }
}
