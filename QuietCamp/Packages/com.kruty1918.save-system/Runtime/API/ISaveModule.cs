namespace Kruty1918.SaveSystem
{
    /// <summary>
    /// Контракт для будь-якої системи, що хоче брати участь у циклі збереження/завантаження.
    /// Реалізуйте цей інтерфейс і зареєструйте його у Zenject-контейнері, щоб SaveService
    /// автоматично включив модуль у файл збереження.
    /// </summary>
    public interface ISaveModule
    {
        /// <summary>Серіалізує стан у контекст запису.</summary>
        void OnSave(ISaveContext context);

        /// <summary>Десеріалізує стан із контексту читання.</summary>
        void OnLoad(ISaveContext context);
    }

    /// <summary>Parses saved data without mutation; returned actions commit in dependency order.</summary>
    public interface IStagedSaveModule : ISaveModule
    {
        System.Action PrepareLoad(ISaveContext context);
        System.Action PrepareMissingData();
    }
}
