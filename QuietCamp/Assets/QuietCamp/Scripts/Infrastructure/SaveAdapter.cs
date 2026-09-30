using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Kruty1918.SaveSystem;
using Newtonsoft.Json;
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
        readonly SaveWriteService _writer = new SaveWriteService();
        readonly SaveLoadService _loader = new SaveLoadService();

        public SessionSaveData Session { get; set; } = new SessionSaveData();
        public ProgressSaveData Progress { get; set; } = new ProgressSaveData();
        public SettingsSaveData Settings { get; set; } = new SettingsSaveData();
        public AlbumSaveData Album { get; set; } = new AlbumSaveData();

        public event Action<string> SaveFailed;

        public SaveAdapter()
        {
            _modules = new List<ISaveModule>
            {
                new SessionSaveModule(this),
                new ProgressSaveModule(this),
                new SettingsSaveModule(this),
                new AlbumSaveModule(this),
            };
        }

        public bool HasSave
            => File.Exists(Path.Combine(UnityEngine.Application.persistentDataPath, "saves", "slot00.mvs"));

        public bool Load(out string error)
        {
            var ok = _loader.TryLoad(Slot, _modules, null, out error);
            if (!ok && error != null && error.Contains("not found")) { error = null; return true; }
            return ok;
        }

        public bool Save()
        {
            if (_writer.TrySave(Slot, _modules, null, out var error)) return true;
            Debug.LogError($"[QuietCamp] Save failed: {error}");
            SaveFailed?.Invoke(error);
            return false;
        }

        abstract class JsonModule<T> : IStagedSaveModule where T : class, new()
        {
            protected readonly SaveAdapter Owner;
            protected JsonModule(SaveAdapter owner) => Owner = owner;
            protected abstract T Data { get; set; }

            public void OnSave(ISaveContext context)
            {
                var bytes = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(Data));
                context.Writer.Write(bytes.Length);
                context.Writer.Write(bytes);
            }

            public Action PrepareLoad(ISaveContext context)
            {
                var length = context.Reader.ReadInt32();
                if (length < 0 || length > 1024 * 1024)
                    throw new InvalidDataException($"Bad block length {length}");
                var dto = JsonConvert.DeserializeObject<T>(
                    Encoding.UTF8.GetString(context.Reader.ReadBytes(length))) ?? new T();
                return () => Data = dto;
            }

            public Action PrepareMissingData() => () => { };

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
    }
}
