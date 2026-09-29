using System.IO;

namespace Kruty1918.SaveSystem
{
    /// <summary>
    /// Контекст, що передається ISaveModule під час збереження або завантаження.
    /// При збереженні доступний Writer; при завантаженні — Reader.
    /// </summary>
    public interface ISaveContext
    {
        BinaryWriter Writer { get; }
        BinaryReader Reader { get; }
    }
}
