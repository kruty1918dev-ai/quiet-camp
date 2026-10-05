# QuietCamp — затишна іконка гри

Оновлено 02.10.2026. Ілюстрацію створено вбудованим `image_gen`: теракотовий намет із теплим світлом і спальником, невелике вогнище, зелені ялини. Без логотипу Unity, написів і рамок. Повний промпт: [generation-prompt.txt](generation-prompt.txt).

## Файли

- `quietcamp-cozy-foreground-master.png` — незмінений оригінал генерації, 1254 × 1254 RGBA PNG із прозорістю.
- `quietcamp-icon-master.png` — непрозорий експорт 1024 × 1024 на темно-зеленому тлі; також використовується в Unity як `Assets/QuietCamp/Art/AppIcon.png` зі збереженим GUID.
- `quietcamp-google-play-512.png` — непрозорий квадратний RGBA PNG 512 × 512 для Google Play.
- `quietcamp-adaptive-background.png` — непрозоре зелене тло 1024 × 1024.
- `quietcamp-adaptive-foreground.png` — прозорий передній шар 1024 × 1024. Видиму композицію пропорційно масштабовано та центровано в колі діаметром 58% полотна; оригінальні значення альфа-каналу збережено.

Шари та експорт 512 × 512 також є в `QuietCamp/Assets/QuietCamp/Art/AppIcon/`. Unity використовує тло першим шаром, ілюстрацію — другим. Обидва шари призначено для всіх шести розмірів адаптивної Android-іконки.

Unity 6000.6 підтримує лише Adaptive Android icons: [посібник міграції Unity 6.6](https://docs.unity.com/en-us/engine/6000.6/manual/upgrade-guides/upgrade-guide-unity66). Окремі шари та захисне поле відповідають принципам [Android Adaptive icons](https://developer.android.com/develop/ui/compose/system/icon_design_adaptive). Магазин сам застосовує маску до квадратного експорту: [Google Play icon specifications](https://developer.android.com/distribute/google-play/resources/icon-design-specifications).

## Підключення й відтворення

Налаштування Player Settings уже збережено в проєкті. Команда **Tools → Quiet Camp → Apply App Icon** повторно призначає готові файли; вона не генерує й не перезаписує ілюстрацію. Перед кожною збіркою цю саму команду викликає `QuietCampIconBuildPreprocessor`.

Технічні експорти створює `tools/app-icon/QuietCampIconExport.cs` через Unity Texture2D: пропорційне масштабування, центрування, композиція на тлі, кодування PNG. Зображення не перемальовується. Для відтворення створіть тимчасовий Unity 6000.6.2f1 проєкт з порожнім `Packages/manifest.json` (`{"dependencies":{}}`), скопіюйте оригінал у `Assets/CozyForegroundMaster.png`, а `QuietCampIconExport.cs` та `QuietCampIcon.cs` — у `Assets/Editor/`. Запустіть Unity з `-batchmode -nographics -quit -buildTarget Android -executeMethod QuietCampIconExport.Export -projectPath <тимчасовий-проєкт>`. Результат — у `Assets/QuietCamp/Art/`. При перенесенні до основного проєкту зберігайте GUID уже наявних файлів.

## Перевірка

У Unity 6000.6.2f1 виконано експорт і повторне застосування іконки; повторне застосування не змінює PNG. Перевірено GUID і порядок обох шарів у всіх шести Android-слотах, непрозорість тла та стандартної іконки, прозорість переднього шару. Діаметр видимої композиції — 58,08% полотна, усередині захисного кола Android 66/108. Експорт Google Play — 512 × 512 RGBA, 232 094 байти. APK із цими змінами ще не збирався.
