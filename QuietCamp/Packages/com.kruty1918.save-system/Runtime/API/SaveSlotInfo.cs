using System;

namespace Kruty1918.SaveSystem
{
    /// <summary>
    /// Метадані слоту збереження (read-only snapshot).
    /// </summary>
    public sealed class SaveSlotInfo
    {
        public int      Slot             { get; }
        public bool     Exists           { get; }
        public long     FileSizeBytes    { get; }
        public DateTime LastWriteTimeUtc { get; }
        public string   WorldName        { get; }

        public SaveSlotInfo(int slot, bool exists, long fileSizeBytes, DateTime lastWriteTimeUtc, string worldName = null)
        {
            Slot             = slot;
            Exists           = exists;
            FileSizeBytes    = fileSizeBytes;
            LastWriteTimeUtc = lastWriteTimeUtc;
            WorldName        = worldName;
        }
    }
}
