using System;

namespace Kruty1918.SaveSystem
{
    /// <summary>
    /// Розв'язує стабільні ідентифікатори save-модулів із сумісним fallback на CLR-ім'я.
    /// </summary>
    public static class SaveModuleIdentity
    {
        /// <summary>Повертає явно заданий ID або legacy повне ім'я типу.</summary>
        /// <param name="moduleType">Тип зареєстрованого save-модуля.</param>
        /// <returns>Стабільний непорожній ідентифікатор.</returns>
        internal static string GetStableId(Type moduleType)
        {
            if (moduleType == null)
                throw new ArgumentNullException(nameof(moduleType));

            var attribute = (SaveModuleIdAttribute)Attribute.GetCustomAttribute(
                moduleType,
                typeof(SaveModuleIdAttribute),
                inherit: false);

            return attribute != null
                ? attribute.Id
                : moduleType.FullName ?? moduleType.Name;
        }
    }
}
