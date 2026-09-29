using System;

namespace Kruty1918.SaveSystem
{
    /// <summary>
    /// Фіксує стабільний ідентифікатор save-модуля незалежно від його CLR-імені або namespace.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, Inherited = false)]
    public sealed class SaveModuleIdAttribute : Attribute
    {
        /// <summary>
        /// Ініціалізує атрибут непорожнім стабільним ідентифікатором.
        /// </summary>
        /// <param name="id">Ідентифікатор, з якого обчислюється block ID у save-файлі.</param>
        public SaveModuleIdAttribute(string id)
        {
            if (string.IsNullOrWhiteSpace(id))
                throw new ArgumentException("Save module ID cannot be empty.", nameof(id));

            Id = id.Trim();
        }

        /// <summary>Отримує стабільний ідентифікатор save-модуля.</summary>
        public string Id { get; }
    }
}
