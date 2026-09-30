# Quiet Camp — QA Report (MVP 0.1.0)

> **Статус**: кодова база MVP реалізована й компілюється чисто;
> EditMode-тести зелені. Android APK ще не зібрано і не встановлено —
> див. «Відомі обмеження».

## Середовище

- Unity 6000.6.2f1, Windows, batchmode для всіх автоматичних кроків.
- URP 17.6.0, Input System 1.20.0, uGUI 2.6.0, Newtonsoft JSON 3.2.2,
  DOTween 1.2.760 + Pro 1.0.380, Unity Test Framework 1.8.0.
- Локальні пакети `com.kruty1918.*` (9 шт.) — зафіксовані в
  `Packages/packages-lock.json`.

## Перевірено

| Перевірка | Результат |
|---|---|
| Чистий імпорт проєкту (batchmode) | OK |
| `QuietCampProjectSetup.Run` (TMP, шрифт, build profiles) | OK, ідемпотентно |
| Компіляція всіх asmdef (Domain/Application/Infrastructure/Presentation/Editor/Tests) | OK, без error CS |
| EditMode tests | **6/6 passed** — правила, witness-карти, undo/redo |
| Build profiles | Android Development (dev+QC_TEST) і Android Release створені, platformId Android |

Знайдені й виправлені дефекти під час збирання:

- `UiActionSource.Ui` не існує в API пакета → замінено на `Button`.
- `QuietCamp.Application` затіняв `UnityEngine.Application` →
  кваліфіковано `persistentDataPath`.
- TMP Essential Resources відсутні → ідемпотентний імпорт із
  `TMP Essential Resources.unitypackage` (ugui package).
- `TMP_FontAsset` без atlas-текстури → додається Texture2D-субасет.
- `BuildProfile.CreateBuildProfile` падав на lambda-callback →
  transient `ScriptableObject`-холдер (persistent listener вимагає
  `UnityEngine.Object`-target).
- `m_Development` живе в базовому класі `AndroidPlatformBuildSettings` —
  пошук поля по ієрархії типів.

## Ще НЕ перевірено (блокери / заплановано)

- Android APK build (dev і release) — не виконано.
- Встановлення на пристрій/емулятор ARM64, logcat, 15-хв тест,
  frame-time p95, пам'ять — не виконано.
- PlayMode-тести (сцена/ввід/дублікати сервісів) — не виконано.
- Реальні скриншоти гри, візуальна перевірка матеріалів/рожевих
  шейдерів — не виконано.
- Проходження 12 рівнів на пристрої — не виконано.

## Відомі обмеження

- `Assets/Plugins/Demigiant/DOTweenPro` — ліцензійна складання за
  умовами Demigiant; перевірити при релізі.
- Production keystore відсутній — тестовий APK підписуватиметься
  тестовим ключем (зберігається поза репозиторієм).
- За відсутності фізичного Android-пристрою тестування буде
  на емуляторі — це буде прямо зазначено у фінальному звіті.

## Як відтворити поточні перевірки

```bat
:: setup + компіляція
Unity.exe -batchmode -nographics -quit -projectPath QuietCamp ^
  -executeMethod QuietCamp.Editor.QuietCampProjectSetup.Run -logFile setup.log

:: EditMode тести (без -quit — тестраннер сам виходить)
Unity.exe -batchmode -projectPath QuietCamp -runTests ^
  -testPlatform EditMode -testResults TestResults_EditMode.xml -logFile tests.log
```
