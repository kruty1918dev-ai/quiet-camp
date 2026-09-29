using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Kruty1918.SaveSystem
{
    /// <summary>
    /// Спільна логіка для конвеєрів збереження/завантаження даних.
    /// Використовується SaveService та ConfigService для уникнення дублювання коду.
    /// Містить: збирання блоків, верифікацію буфера, атомарний запис,
    /// завантаження з декодуванням, утиліти файлової системи.
    /// </summary>
    public static class SavePipelineHelper
    {
        /// <summary>Максимальний розмір одного блоку даних (10 МБ).</summary>
        internal const int MaxBlockBytes = 10 * 1024 * 1024;

        /// <summary>Максимальна кількість слотів збереження.</summary>
        internal const int MaxSlots = 99;

        /// <summary>
        /// Збирає бінарні блоки з кожного ISaveModule у детермінованому
        /// dependency-aware порядку. Дубльована реєстрація одного типу модуля
        /// не створює дубльований blockId у файлі.
        /// </summary>
        internal static List<(uint blockId, byte[] payload)> CollectBlocks(
            IReadOnlyList<ISaveModule> modules, SaveModuleOrdering ordering = null)
        {
            List<ISaveModule> orderedModules = SaveModuleExecutionPlan.Build(modules, ordering);
            var blocks = new List<(uint, byte[])>(orderedModules.Count);
            var blockIds = new HashSet<uint>();

            for (int i = 0; i < orderedModules.Count; i++)
            {
                var module = orderedModules[i];
                if (module == null)
                {
                    Debug.LogError("[SaveSystem] Null ISaveModule instance — skipped.");
                    continue;
                }

                uint blockId = SaveFileCodec.ComputeBlockId(module.GetType());
                if (!blockIds.Add(blockId))
                    throw new InvalidDataException($"Duplicate save block identity: {module.GetType().FullName}.");

                using var ms = new MemoryStream();
                using var bw = new BinaryWriter(ms);

                try
                {
                    module.OnSave(new SaveContext(bw, null));
                }
                catch (Exception exception)
                {
                    throw new InvalidOperationException(
                        $"Cannot save module '{SaveModuleIdentity.GetStableId(module.GetType())}': {exception.Message}",
                        exception);
                }

                bw.Flush();
                byte[] payload = ms.ToArray();

                if (payload.Length == 0)
                {
                    continue;
                }

                if (payload.Length > MaxBlockBytes)
                {
                    throw new InvalidDataException(
                        $"[SaveSystem] '{module.GetType().FullName}' payload {payload.Length}b " +
                        $"> {MaxBlockBytes}b limit. Save aborted.");
                }

                blocks.Add((blockId, payload));
            }

            return blocks;
        }

        /// <summary>
        /// Перевіряє мінімальний розмір та magic-байти зібраного буфера.
        /// </summary>
        internal static bool VerifyAssembledBuffer(byte[] data)
        {
            if (data.Length < SaveFileCodec.FileLayout.MinFileSize)
            {
                Debug.LogError(
                    $"[SaveSystem] Assembled buffer {data.Length}b < min {SaveFileCodec.FileLayout.MinFileSize}b.");
                return false;
            }

            var magic = SaveFileCodec.FileLayout.Magic;
            if (data[0] != magic[0] || data[1] != magic[1] ||
                data[2] != magic[2] || data[3] != magic[3])
            {
                Debug.LogError("[SaveSystem] Header magic corrupted in assembled buffer.");
                return false;
            }

            return true;
        }

        /// <summary>
        /// Атомарний запис даних у файл: tmp → verify → backup → rename.
        /// Повертає true при успішному записі.
        /// </summary>
        internal static bool AtomicWrite(string finalPath, byte[] data)
        {
            string tmp = finalPath + ".tmp";
            string backup = finalPath + ".bak";
            try
            {
                using (var stream = new FileStream(tmp, FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    stream.Write(data, 0, data.Length);
                    stream.Flush(flushToDisk: true);
                }
                byte[] written = File.ReadAllBytes(tmp);
                if (written.Length != data.Length
                    || SaveFileCodec.TryDecode(written, out _, out _, out _) != SaveFileCodec.DecodeError.None)
                    throw new IOException("The temporary save failed integrity validation.");

                if (File.Exists(finalPath))
                {
                    // A load from backup may leave a corrupt primary. Preserve the last valid backup.
                    bool primaryValid = SaveFileCodec.TryDecode(
                        File.ReadAllBytes(finalPath), out _, out _, out _) == SaveFileCodec.DecodeError.None;
                    File.Replace(tmp, finalPath, primaryValid ? backup : null);
                }
                else File.Move(tmp, finalPath);
                return true;
            }
            catch (Exception exception)
            {
                Debug.LogError($"[SaveSystem] Save replacement failed: {exception.Message}");
                TryDelete(tmp);
                return false;
            }
        }

        /// <summary>
        /// Декодує байти та відновлює блоки в поточному dependency-aware порядку
        /// модулів, а не у фізичному порядку блоків у .mvs. Це робить legacy saves
        /// незалежними від історичного порядку Zenject/registrar реєстрації.
        /// </summary>
        internal static bool ExecuteLoad(byte[] bytes, IReadOnlyList<ISaveModule> modules,
            string contextLabel, SaveModuleOrdering ordering = null)
        {
            bool loaded = ExecuteLoad(bytes, modules, contextLabel, out string error, out _,
                ordering: ordering);
            if (!loaded) Debug.LogError($"[SaveSystem] {error}");
            return loaded;
        }

        internal static bool ExecuteLoad(byte[] bytes, IReadOnlyList<ISaveModule> modules,
            string contextLabel, out string errorMessage, out bool restoreStarted,
            string requiredBlockModuleFullName = null, SaveModuleOrdering ordering = null)
        {
            restoreStarted = false;
            errorMessage = null;
            var result = SaveFileCodec.TryDecode(bytes, out _, out var decodedBlocks, out string error);
            if (result != SaveFileCodec.DecodeError.None)
            {
                errorMessage = $"{contextLabel}: {error ?? result.ToString()}";
                return false;
            }

            var payloadByBlockId = new Dictionary<uint, byte[]>();
            foreach (var block in decodedBlocks)
            {
                if (payloadByBlockId.ContainsKey(block.blockId))
                {
                    errorMessage = $"{contextLabel}: duplicate block {block.blockId:X8}.";
                    return false;
                }
                payloadByBlockId.Add(block.blockId, block.payload);
            }

            var prepared = new List<(string Name, Action Commit)>();
            var blockNames = new Dictionary<uint, string>();
            foreach (var module in SaveModuleExecutionPlan.Build(modules, ordering))
            {
                string name = SaveModuleIdentity.GetStableId(module.GetType());
                uint id = SaveFileCodec.ComputeBlockId(module.GetType());
                if (blockNames.TryGetValue(id, out string existingName) && existingName != name)
                {
                    errorMessage = $"{contextLabel}: block identity collision between '{existingName}' and '{name}'.";
                    return false;
                }
                blockNames[id] = name;
                bool hasPayload = payloadByBlockId.TryGetValue(id, out byte[] payload);
                if (!hasPayload && string.Equals(name, requiredBlockModuleFullName, StringComparison.Ordinal))
                {
                    errorMessage = $"{contextLabel}: required save block '{name}' is missing.";
                    return false;
                }
                try
                {
                    Action commit;
                    if (module is IStagedSaveModule staged)
                    {
                        if (hasPayload)
                        {
                            using var stream = new MemoryStream(payload, false);
                            using var reader = new BinaryReader(stream);
                            commit = staged.PrepareLoad(new SaveContext(null, reader));
                        }
                        else commit = staged.PrepareMissingData();
                    }
                    else
                    {
                        if (!hasPayload) continue;
                        commit = () =>
                        {
                            using var stream = new MemoryStream(payload, false);
                            using var reader = new BinaryReader(stream);
                            module.OnLoad(new SaveContext(null, reader));
                        };
                    }
                    if (commit == null) throw new InvalidDataException("The module returned no restore action.");
                    prepared.Add((name, commit));
                }
                catch (Exception exception)
                {
                    errorMessage = $"{contextLabel}: validation of '{name}' failed: {exception.Message}";
                    return false;
                }
            }

            // Every staged module has validated its data before the first gameplay mutation.
            foreach (var operation in prepared)
            {
                restoreStarted = true;
                try { operation.Commit(); }
                catch (Exception exception)
                {
                    errorMessage = $"{contextLabel}: restoring '{operation.Name}' failed: {exception.Message}";
                    return false;
                }
            }
            return true;
        }

        /// <summary>
        /// Перевіряє валідність номера слоту (0–99).
        /// </summary>
        internal static bool ValidateSlot(int slot)
        {
            if (slot >= 0 && slot <= MaxSlots) return true;
            return false;
        }

        /// <summary>
        /// Забезпечує існування директорії збережень.
        /// </summary>
        internal static bool EnsureDirectoryExists(out string error)
        {
            try
            {
                string dir = GetDirectory();
                if (!Directory.Exists(dir))
                    Directory.CreateDirectory(dir);
                error = null;
                return true;
            }
            catch (Exception e)
            {
                error = e.Message;
                return false;
            }
        }

        /// <summary>Шлях до директорії збережень.</summary>
        internal static string GetDirectory()
            => Path.Combine(Application.persistentDataPath, "saves");

        /// <summary>Шлях до файлу ігрового слота (slot00–slot99).</summary>
        internal static string GetPath(int slot)
            => Path.Combine(GetDirectory(), $"slot{slot:D2}.mvs");

        /// <summary>Безпечне видалення файлу з логуванням.</summary>
        internal static void TryDelete(string path)
        {
            try { if (File.Exists(path)) File.Delete(path); }
            catch (Exception)
            {
            }
        }

        /// <summary>Відновлення з бекапу після невдалого запису.</summary>
        internal static void TryRestoreBackup(string backup, string final)
        {
            if (!File.Exists(backup)) return;
            try { File.Copy(backup, final, overwrite: true); }
            catch (Exception e)
            {
                Debug.LogError($"[SaveSystem] Backup restore failed: {e.Message}");
            }
        }
    }
}
