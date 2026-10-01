# Quiet Camp — QA Report (MVP 0.1.0)

> **Статус**: MVP реалізовано, код компілюється чисто, EditMode і
> PlayMode тести зелені, обидва Android APK зібрані й перевірені
> статично (package/ABI/підпис). **Встановлення на пристрій не
> виконано** — фізичний пристрій відсутній, емулятор заблокований
> вимкненою віртуалізацією (VT-x у прошивці, драйвер гіпервізора
> не встановлено). Див. «Відомі обмеження».

## Середовище

- Unity 6000.6.2f1, Windows, batchmode для всіх автоматичних кроків.
- URP 17.6.0, Input System 1.20.0, uGUI 2.6.0, Newtonsoft JSON 3.2.2,
  DOTween 1.2.760 + Pro 1.0.380, Unity Test Framework 1.8.0.
- Локальні пакети `com.kruty1918.*` — зафіксовані в
  `Packages/packages-lock.json`.
- Android SDK/NDK/OpenJDK — модулі Unity 6000.6.2f1.

## Перевірено

| Перевірка | Результат |
|---|---|
| Чистий імпорт проєкту (batchmode) | OK |
| `QuietCampProjectSetup.Run` (TMP, шрифт, build profiles) | OK, ідемпотентно |
| Компіляція всіх asmdef | OK, без error CS |
| EditMode tests | **67/67 passed** — правила, witness-карти, undo/redo, портрет-лок |
| PlayMode tests | **30/30 passed** — bootstrap, сцени, атмосфера, vegetation slots, скриншот-кадри |
| `Tools/verify_bundle.py` (кіт) | **OK** — 61 карта, witness-розв'язки, хеші моделей/аудіо/маніфесту (потребує `PYTHONUTF8=1` на Windows) |
| Build profiles | Android Development (dev+QC_TEST) і Android Release |
| Android Release APK | `QuietCamp-MVP-0.1.0.apk`, 38 MB, `Result: Success` |
| Android Development APK | `QuietCamp-MVP-0.1.0-dev.apk`, `Result: Success` |
| Package/Version | `com.kruty1918.quietcamp`, versionCode 1, versionName 0.1.0 |
| minSdk/targetSdk | 26 / 35 |
| ABI | `lib/arm64-v8a/` присутній (ARM64 only) |
| Підпис APK | APK Signature Scheme v2, тестовий Android Debug cert |
| Рендер UI/сцени | Скриншоти `01_main_menu.png`, `02_camp_day.png` — локалізований текст, 3D-моделі, без рожевих матеріалів |

## Знайдені й виправлені дефекти

- `UiActionSource.Ui` не існує → замінено на `Button`.
- `QuietCamp.Application` затіняв `UnityEngine.Application`.
- TMP Essential Resources відсутні → ідемпотентний імпорт із
  `TMP Essential Resources.unitypackage`.
- `TMP_FontAsset` без atlas-текстури й матеріалу → створені
  Texture2D/Material-субасети; settings злиті з пакетним TMP Settings.
- `BuildProfile.CreateBuildProfile` падав на lambda-callback →
  transient `ScriptableObject`-холдер.
- `m_Development` живе в базовому класі `AndroidPlatformBuildSettings` —
  пошук поля по ієрархії типів.
- `Camp.unity` `m_Sun` вказував на видалений fileID → `fileID: 0`.
- **`LocalizedLabel` баг**: `Awake()` відкладається на неактивних
  об'єктах, `_baseSize` лишався 0, `Refresh()` обнуляв fontSize →
  текст меню був невидимий. Виправлено ледачою ініціалізацією
  в `Refresh()`. Скриншот меню підтверджує рендер.
- Batchmode play-mode не пампає `EditorApplication.update` —
  скриншоти перенесено у `[UnityTest]` (UTF пампає кадри).

## Immersive UI (ітерація за `Design/ImmersiveUI/`)

- `SafeAreaFitter` вносить `Screen.safeArea` у контентні шари меню й
  табору: інтерактивні елементи не потрапляють під виріз/жести,
  фонові текстури лишаються full-bleed.
- Мовний рядок замінено на сегментований вибір UK/EN/DE —
  крихкий шаблон `TMP_Dropdown` прибрано.
- Налаштування згруповані («Звук», «Зручність», «Мова й вигляд»),
  повзунки — плоский трек + кругла ручка, чекбокси зі спрайтованим
  станом; аудіо-семпл грає лише на відпусканні повзунка.
- HUD: людські назви рівнів через `LevelDisplay` («Галявина NN»,
  тестовий рівень підписаний явно), короткі чіпси правил +
  `MessageSlot` для повного тексту, контекстний док дій.
- Пауза — нижній sheet із видимим табором і однією первинною дією.
- Модалі блокують pointer-ввід і гарячі клавіші через context-стек;
  tutorial-підказки під ними не просвічують.
- Стейдж: пом'якшені шви тайлів і борт основи, контактні тіні під
  декором; перехід `transition.json` — маси листя з перекриттям.

## Статус дизайн-планів (`Design/`)

Аудит усіх промптів проєкту проти поточного коду:

| План | Статус |
|---|---|
| `Atmosphere/01` World shaders | Виконано: шари backdrop/rear/near, ProtectedViewport, тінти крон |
| `Atmosphere/02` Audio | Виконано: вітрове ложе, фази, позиційний вогонь, listener-proxy |
| `Atmosphere/03` Wind/particles | **Виконано**: один `WindSim`-власник; нульовий бюджет фази = OFF на всіх тірах; детермінований декоративний RNG; туман — обмежене коливання навколо якоря; пориви диму по `DirectionXZ`; Reduced Motion за спекою — листя/пил/дим догасають за ~0.3 с, світлячки лишаються рідкими нерухомими точками без пульсу, туман нерухомий; ближній лист — cap 1, 30–60 с, 3–6 % ширини в'юпорта, гейт на drag/модалку/перехід, перевірка траєкторії проти ProtectedViewport; порив відриває ≤1 листок за вільного бюджету (`GustStarted`); пилок живе кишенею на підвітряному краю, не над полем; жаринки — чинний emitter `Campfire` з пакета atmos (не дубльовано); відсутній шейдер/атлас = один лог + вимкнений декор |
| `Atmosphere/04` Transitions | Виконано: dive крізь крону, opaque-cover, скриншот-кадри |
| `Atmosphere/05` Time/PostFX | Виконано базовий шлях: runtime `VolumeProfile`, LDR-first, bloom лише на вищих тірах. HDR/High — опція, свідомо не вмикалася |
| `Atmosphere/06` Vegetation | **Виконано**: `Atmos/FoliageSway` переписано на URP HLSL — спільний world-space вітер (`_AtmosWind*`, один власник `CampAtmosphere`), spatial-phase хвиля від anchor-позиції, нормалізовані object-space маски висоти через MPB, species-responses (trunk/canopy/conifer/bush/grass/flower stem/head) per material slot, реальне сонце/ambient замість фіксованого `_LightDir`, ShadowCaster-пас з тією ж деформацією, bounds розширені під максимальний sway, reduced-motion eased до 0 за ~0.3 с, захоплення матеріалів обмежене `DecorRoot` і ідемпотентне |
| `MainMenu/` | Виконано |
| `ImmersiveUI/` | Виконано |

## НЕ перевірено (блокери)

- **Встановлення/запуск APK на Android** — немає фізичного пристрою;
  емулятор не стартує: драйвер Android Emulator hypervisor не
  встановлено, VT-x вимкнено у прошивці, WHPX/Hyper-V недоступні.
- logcat, 15-хв сесія, frame time/p95/пам'ять — не виміряно.
- Проходження 12 рівнів на пристрої — перевірено лише логікою в
  EditMode/PlayMode, не на залозі.
- `QC_TEST` рівень у dev-збірці не прогнаний у runtime.
- Релізний keystore — APK підписано тестовим Android Debug ключем;
  для Google Play потрібен справжній keystore (поза репозиторієм).

## Відомі обмеження

- `Licensing::Module Error: Access token is unavailable` у логах —
  помилка середовища ліцензування Unity при shutdown, на білд не
  впливає (`Build Finished, Result: Success`).
- Застарілий `AssetDatabase.ImportPackage(string, bool)` у
  `QuietCampProjectSetup` — warning, не блокер.
- `DOTweenPro` — ліцензія Demigiant, перевірити при релізі.
- FPS не заявляється — вимірювань на пристрої не було.

## Як відтворити перевірки

```bat
:: setup
Unity.exe -batchmode -projectPath QuietCamp ^
  -executeMethod QuietCamp.Editor.QuietCampProjectSetup.Run -logFile setup.log

:: EditMode
Unity.exe -batchmode -projectPath QuietCamp -runTests ^
  -testPlatform EditMode -testResults TestResults_EditMode.xml -logFile t1.log

:: PlayMode (включає скриншоти у QuietCamp\Screenshots)
Unity.exe -batchmode -projectPath QuietCamp -runTests ^
  -testPlatform PlayMode -testResults TestResults_PlayMode.xml -logFile t2.log

:: Android APK (release / dev)
Unity.exe -batchmode -projectPath QuietCamp ^
  -executeMethod QuietCamp.Editor.QuietCampBuild.BuildAndroidRelease -logFile b.log
Unity.exe -batchmode -projectPath QuietCamp ^
  -executeMethod QuietCamp.Editor.QuietCampBuild.BuildAndroidDev -logFile b2.log
```
