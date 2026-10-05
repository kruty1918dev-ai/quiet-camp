using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Kruty1918.SaveSystem;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using QuietCamp.Application;
using UnityEngine;
namespace QuietCamp.Infrastructure
{
    /// <summary>
    /// Save/load owner for slot 0. Staged modules parse into temporary DTOs and
    /// commit only after the whole file validates — CRC, atomic write and backup
    /// recovery stay inside the package pipeline; no parallel save system here.
    /// </summary>
    public sealed class SaveAdapter
    {
        public const int Slot = 0;

        readonly List<ISaveModule> _modules;
        readonly ISaveWriteService _writer;
        readonly SaveLoadService _loader = new SaveLoadService();
        bool _loadFailed, _unsupportedVersion;

        public SessionSaveData Session { get; set; } = new SessionSaveData();
        public ProgressSaveData Progress { get; set; } = new ProgressSaveData();
        public SettingsSaveData Settings { get; set; } = new SettingsSaveData();
        public AlbumSaveData Album { get; set; } = new AlbumSaveData();
        public EconomySaveData Economy { get; set; } = new EconomySaveData();
        public EntitlementSaveData Entitlements { get; set; } = new EntitlementSaveData();
        public PurchaseSaveData Purchases { get; set; } = new PurchaseSaveData();
        public MemorySaveData Memories { get; set; } = new MemorySaveData();

        public event Action<string> SaveFailed;
        public event Action BeforeSave;

        public SaveAdapter(ISaveWriteService writer = null)
        {
            _writer = writer ?? new SaveWriteService();
            _modules = new List<ISaveModule>
            {
                new SessionSaveModule(this),
                new ProgressSaveModule(this),
                new SettingsSaveModule(this),
                new AlbumSaveModule(this),
                new EconomySaveModule(this),
                new EntitlementSaveModule(this),
                new PurchaseSaveModule(this),
                new MemorySaveModule(this),
            };
        }

        public bool HasSave
        {
            get
            {
                var path = Path.Combine(UnityEngine.Application.persistentDataPath, "saves", "slot00.mvs");
                return File.Exists(path) || File.Exists(path + ".bak");
            }
        }

        public bool Load(out string error)
        {
            if (!HasSave) { error = null; _loadFailed = false; return true; }
            _unsupportedVersion = false;
            var ok = _loader.TryLoad(Slot, _modules, null, out error);
            if (_unsupportedVersion) { ok = false; error = "Unsupported save module version; files were not changed."; }
            _loadFailed = !ok;
            if (ok) CampContent.Migrate(this);
            return ok;
        }

        public void ResetAfterErase()
        {
            var settings = Settings;
            foreach (var module in _modules) ((IStagedSaveModule)module).PrepareMissingData()?.Invoke();
            Settings = settings; _loadFailed = false; _unsupportedVersion = false;
        }

        public bool Save()
        {
            if (_loadFailed) { SaveFailed?.Invoke("Existing save could not be loaded; writing is disabled."); return false; }
            BeforeSave?.Invoke();
            if (_writer.TrySave(Slot, _modules, null, out var error)) return true;
            Debug.LogError($"[QuietCamp] Save failed: {error}");
            SaveFailed?.Invoke(error);
            return false;
        }

        /// <summary>Erase only this game's slot, including recovery and interrupted-write copies.
        /// Deletes the primary last so a failed backup deletion leaves it available for retry.
        /// Does not touch OS backups, other slots, SDK storage or remote data.</summary>
        public static bool TryEraseLocalFiles(string persistentRoot, out string error)
        {
            error = null;
            try
            {
                if (string.IsNullOrWhiteSpace(persistentRoot)) throw new ArgumentException("Missing save root");
                var path = Path.Combine(persistentRoot, "saves", "slot00.mvs");
                File.Delete(path + ".bak");
                File.Delete(path + ".tmp");
                // Only publisher-document cache, never other SDK or save directories.
                var policyCache = Path.Combine(persistentRoot, "qc_policy_cache");
                if (Directory.Exists(policyCache)) Directory.Delete(policyCache, true);
                File.Delete(path);
                return true;
            }
            catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException
                || exception is ArgumentException || exception is System.Security.SecurityException)
            {
                // No paths or player data in diagnostics.
                error = "Local game data could not be completely erased. Retry or use OS app-data controls.";
                return false;
            }
        }

        abstract class JsonModule<T> : IStagedSaveModule where T : class, new()
        {
            protected readonly SaveAdapter Owner;
            JObject _original;
            protected JsonModule(SaveAdapter owner) => Owner = owner;
            protected abstract T Data { get; set; }

            public void OnSave(ISaveContext context)
            {
                var json = JObject.FromObject(Data);
                PreserveUnknown(json, _original);
                var bytes = Encoding.UTF8.GetBytes(json.ToString(Formatting.None));
                context.Writer.Write(bytes.Length);
                context.Writer.Write(bytes);
            }

            public Action PrepareLoad(ISaveContext context)
            {
                var length = context.Reader.ReadInt32();
                if (length < 0 || length > 1024 * 1024)
                    throw new InvalidDataException($"Bad block length {length}");
                var bytes = context.Reader.ReadBytes(length);
                if (bytes.Length != length) throw new InvalidDataException("Truncated save module");
                var json = JObject.Parse(Encoding.UTF8.GetString(bytes));
                if (json["version"] != null && json["version"].Value<int>() != 1)
                { Owner._unsupportedVersion = true; throw new InvalidDataException("Unsupported save module version"); }
                var dto = json.ToObject<T>() ?? new T();
                if (dto is EconomySaveData economy && (economy.lives < 0 || economy.hints < 0 || economy.currency < 0 || economy.adLivesUsed < 0
                    || Array.Exists(economy.attempts ?? Array.Empty<AttemptRecord>(), a => a == null || a.checks < 0 || string.IsNullOrWhiteSpace(a.levelId))))
                    throw new InvalidDataException("Invalid economy balances");
                return () => { Data = dto; _original = json; };
            }

            static void PreserveUnknown(JToken current, JToken previous)
            {
                if (current is JObject value && previous is JObject old)
                {
                    foreach (var property in old.Properties())
                    {
                        if (value.Property(property.Name) == null) value.Add(property.Name, property.Value.DeepClone());
                        else PreserveUnknown(value[property.Name], property.Value);
                    }
                }
                else if (current is JArray items && previous is JArray oldItems)
                    for (int i = 0; i < items.Count; i++)
                    {
                        JToken match = i < oldItems.Count ? oldItems[i] : null;
                        if (items[i] is JObject item)
                            foreach (var key in new[] { "levelId", "guestId", "id", "rewardId" })
                                if (item[key]?.Type == JTokenType.String)
                                { match = null; foreach (var oldItem in oldItems) if (oldItem is JObject candidate && JToken.DeepEquals(candidate[key], item[key])) { match = candidate; break; } break; }
                        PreserveUnknown(items[i], match);
                    }
            }

            public Action PrepareMissingData() => () => { Data = new T(); _original = null; };

            public void OnLoad(ISaveContext context) => PrepareLoad(context)?.Invoke();
        }

        [SaveModuleId("qc.session.v1")]
        sealed class SessionSaveModule : JsonModule<SessionSaveData>
        {
            public SessionSaveModule(SaveAdapter o) : base(o) { }
            protected override SessionSaveData Data { get => Owner.Session; set => Owner.Session = value; }
        }

        [SaveModuleId("qc.progress.v1")]
        sealed class ProgressSaveModule : JsonModule<ProgressSaveData>
        {
            public ProgressSaveModule(SaveAdapter o) : base(o) { }
            protected override ProgressSaveData Data { get => Owner.Progress; set => Owner.Progress = value; }
        }

        [SaveModuleId("qc.settings.v1")]
        sealed class SettingsSaveModule : JsonModule<SettingsSaveData>
        {
            public SettingsSaveModule(SaveAdapter o) : base(o) { }
            protected override SettingsSaveData Data { get => Owner.Settings; set => Owner.Settings = value; }
        }

        [SaveModuleId("qc.album.v1")]
        sealed class AlbumSaveModule : JsonModule<AlbumSaveData>
        {
            public AlbumSaveModule(SaveAdapter o) : base(o) { }
            protected override AlbumSaveData Data { get => Owner.Album; set => Owner.Album = value; }
        }
        [SaveModuleId("qc.economy.v1")]
        sealed class EconomySaveModule : JsonModule<EconomySaveData>
        {
            public EconomySaveModule(SaveAdapter o) : base(o) { }
            protected override EconomySaveData Data { get => Owner.Economy; set => Owner.Economy = value; }
        }
        [SaveModuleId("qc.entitlements.v1")]
        sealed class EntitlementSaveModule : JsonModule<EntitlementSaveData>
        {
            public EntitlementSaveModule(SaveAdapter o) : base(o) { }
            protected override EntitlementSaveData Data { get => Owner.Entitlements; set => Owner.Entitlements = value; }
        }
        [SaveModuleId("qc.purchases.v1")]
        sealed class PurchaseSaveModule : JsonModule<PurchaseSaveData>
        {
            public PurchaseSaveModule(SaveAdapter o) : base(o) { }
            protected override PurchaseSaveData Data { get => Owner.Purchases; set => Owner.Purchases = value; }
        }
        [SaveModuleId("qc.journeys.v1")]
        sealed class MemorySaveModule : JsonModule<MemorySaveData>
        {
            public MemorySaveModule(SaveAdapter o) : base(o) { }
            protected override MemorySaveData Data { get => Owner.Memories; set => Owner.Memories = value; }
        }
    }
}
