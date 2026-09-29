namespace Kruty1918.SaveSystem
{
    /// <summary>
    /// Публічний API системи збереження.
    /// Дозволяє зберігати/завантажувати стан гри у/з слоту, перевіряти наявність збереження
    /// і отримувати метадані слоту.
    /// </summary>
    public interface ISaveService
    {
        void Save(int slot = 0);
        void Load(int slot = 0);
        bool HasSave(int slot = 0);
        void Delete(int slot = 0);
        SaveSlotInfo GetSlotInfo(int slot = 0);
    }
}
