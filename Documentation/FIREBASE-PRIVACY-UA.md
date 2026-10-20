# Firebase і приватність Quiet Camp

Android Application Identifier: `com.quietcamp.game`. Firebase Project ID:
`quiet-camp`. Unity у перевіреному проєкті: **6000.6.2f1**. Android: мінімальний
API **26**, цільовий API **35**, **ARM64 / IL2CPP**. Мінімальний API не знижено.

## Вихідний стан і рішення

Гра має один `GameServices`, спільний bootstrap і локальний `SaveAdapter`.
Analytics уже мав `ComfortAnalytics`, події реальної гри, умовний Firebase-адаптер
і власну згоду. AdMob 11.6.0 уже підключений через UPM; тестові rewarded ads
обслуговують життя й косметичну винагороду. Firebase SDK і Crashlytics у цій
Git-копії ще не були встановлені. Підтвердження читання документа не було згодою
на обробку — ця відмінність збережена.

Встановлено лише **Firebase Core, Analytics і Crashlytics 14.0.0**. Auth,
Firestore, Remote Config, облікові записи, хмарні збереження та білінг не активовано.
`Assets/google-services.json` скопійовано з наданого файла: його Firebase Project
ID та Android package збігаються з конфігурацією гри. Реєстрацію в Play Console
через цей файл підтвердити неможливо.

AdMob використовує `com.unity.external-dependency-manager` **2.1.0**. Офіційний
Firebase Core 14.0.0 натомість декларує Google EDM **1.2.190** з тими самими
resolver-асемблеями. Щоб не встановлювати два EDM, у локальному UPM-архіві Core
замінено тільки залежність у `package.json` на наявний Unity EDM. Бінарні файли,
Editor hooks, Android-архіви, ліцензії та інші файли SDK не змінено. Відтворення
й побайтова перевірка: `python tools/prepare_firebase_packages.py`; походження
архівів описано в `QuietCamp/GooglePackages/README.md`. Це адаптація метаданих
проєкту, а не твердження про офіційну сертифікацію цієї комбінації. Native Gradle
resolution і фактичний імпорт Unity залишаються окремою перевіркою.

## Публічна політика і стан публікації

Перевірено всі три версії наданої сторінки від **20 жовтня 2026 року**:
[українську](https://oleksiy-havryliuk.de/quiet-camp/privacy/uk/),
[англійську](https://oleksiy-havryliuk.de/quiet-camp/privacy/),
[німецьку](https://oleksiy-havryliuk.de/quiet-camp/privacy/de/).
Сторінки називають себе **review copy / копія для перевірки**, залишаючи
невирішеними адресу відповідальної особи, правила для молодших користувачів
і перевірку релізної обробки даних. Наявність доступного URL не знімає ці умови.

У `legal.json` внесено підтверджені сторінкою контакти:

- Kruty1918 Games / Oleksiy Havryliuk;
- `privacy@oleksiy-havryliuk.de` для приватності;
- `support@oleksiy-havryliuk.de` для підтримки;
- веброзділ `#retention` і окрему дію email для запитів щодо серверних даних.

`policyIsDraft: true`, порожній `publishedOn`, `teenPrivacyReviewed: false` та
`sdkDisclosuresReviewed: false` залишають релізні онлайн-провайдери вимкненими.
Флаги наявності Analytics/Crashlytics у `google_services.json` не обходять цю
перевірку. Bootstrap не викликає керовану Firebase-ініціалізацію, доки умови
публікації не виконані. Це не є доказом повної відсутності native SDK network
traffic: його треба перевірити на релізному Android-застосунку.

У `privacy_content.json` збережено повні тексти трьох наданих review-сторінок
і окремі короткі пояснення поточної поведінки гри. Копія має revision
`review-2026-10-20` та залишається чернеткою; автоматичного завантаження з сайту
при старті немає. Перехід на джерело виконується за явною дією користувача.

## Карта інтеграції

| Точка | Поведінка |
|---|---|
| `QuietCampBootstrap` після створення `GameServices` | Відновлення окремих дозволів без затримування основного startup; ініціалізація тільки після публікаційних перевірок |
| `FirebaseRuntime` | Один спільний `CheckAndFixDependenciesAsync`, обмеження очікування 20 секунд, невдача залишає гру доступною |
| `ComfortAnalytics` → `FirebaseComfortTransport` | Числові події лише після актуальної згоди й готовності SDK; 120 подій за життя сервісу; без черги для минулих дій |
| `CrashDiagnostics` → `FirebaseCrashTransport` | Окрема збережена згода, перевірка revision, дозволений контекст рівня, подавлення повторних ручних звітів за категорією |
| `SaveAdapter.SaveFailed` | Ручний nonfatal з фіксованою категорією Save, без тексту помилки чи вмісту збереження |
| `Debug.LogException` у наявних шляхах | Штатний механізм SDK для Unity exceptions; той самий exception додатково вручну не дублюється |
| Налаштування → Дані й документи | Окремі теми Analytics, звітів про збої, локальних даних і документів; посилання політики та email |
| Тестова діагностика в темі збоїв | Тільки Editor / Development Build, лише при активному окремому дозволі; автоматичного fatal-crash тесту немає |
| `GameServices.Dispose` | Відписка від подій і звільнення адаптерів; спільний Firebase runtime має час життя застосунку |

Analytics дозволяє тільки `AnalyticsStorage`. `AdStorage`, `AdUserData` й
`AdPersonalization` залишаються denied. Google UMP має окремий рекламний шлях.
AdMob не залежить від готовності Analytics і не ініціалізується ним.

В Android manifest вимкнено початкову Analytics collection, Analytics ad-ID
collection та автоматичне передавання Crashlytics. Native
`FirebasePrivacyProvider` вимикає persisted Analytics collection до
`FirebaseInitProvider`; його порядок та фактичну відсутність раннього
передавання ще треба підтвердити на пристрої. Firebase Core і native SDK
все одно можуть створювати локальні службові дані.

Crashlytics ніколи не отримує постійний `IsCrashlyticsCollectionEnabled=true`.
Android `sendUnsentReports` викликається тільки для попереднього дозволеного
сеансу при актуальній поточній згоді. Перша згода посеред сеансу не дозволяє
відправляти його історичні звіти; `crashSessionEligible` відрізняє ці випадки.
Відсутня/застаріла згода й відкликання спрямовують збережені pending reports на
`deleteUnsentReports`. Доставка, видалення й поведінка native SDK ще потребують
пристроєвої перевірки; локальні crash-файли можуть існувати до очищення.

## Події Analytics

Схема централізована в `ComfortAnalytics`; gameplay не викликає Firebase API.

| Подія | Фактичний тригер і параметри |
|---|---|
| `qc_level_begin` | Активна сесія рівня; числовий індекс, ширина/висота, пресет, schema. Підтримуються всі 100 рівнів базової кампанії та майбутні індекси до 10000 |
| `qc_level_summary` | Підтверджене завершення, вихід або згортання; outcome, діапазон часу, обмежені лічильники дій/правил, до 25 параметрів |
| `level_fail` | Реальний `FailedAttempt` після перевірки рівня; індекс рівня. Відсутність життів і помилка запису не видаються за невдачу гравця |
| `qc_screen` | Зміна категорії екрана, з подавленням повтору |
| `qc_comfort_setting` | Зміна згрупованого налаштування; числова категорія й діапазон |
| `qc_comfort_feedback` | Один із трьох фіксованих добровільних варіантів за сеанс |
| `qc_frame_budget` | Наочні діапазони часу кадру й категорія якості; не raw frame trace |
| `rewarded_ad_requested` | Дозволений запит користувача на відео; category 0 = життя, 1 = косметика |
| `rewarded_ad_completed` | SDK reward callback разом із закриттям відео |
| `reward_granted` | Успішне фактичне надання й збереження винагороди; невдале збереження не створює подію |

Окремі `game_started`, `level_complete`, `interstitial_ad_shown`, автоматичні
purchase/revenue події не додані. `Completed` уже є в `qc_level_summary`;
робочого interstitial формату немає. Analytics policy version підвищено до 2:
нові категорії не використовують стару згоду. Автоматичні події самого Firebase
та його ідентифікатори потрібно враховувати окремо від власної числової схеми.

## Дані для підготовки Data safety та політики

| Процес | Дані / одержувач | Керування / строк |
|---|---|---|
| Локальна гра | Прогрес, розкладки, альбом, налаштування, ресурси й privacy receipts; локальний `SaveAdapter` | Видалення в головному меню; Android backups / transfer залежать від кінцевої платформи і manifest |
| Analytics, після дозволу | Власні події вище; Firebase додатково може обробляти app-instance ID, device/OS/app/session дані; Google | Окремий opt-in і відкликання; local reset не видаляє серверні записи. 2 місяці GA4 retention — потрібне налаштування й підтвердження в Console |
| Crashlytics, після дозволу | Native/Unity crash traces, exception messages, device/app metadata та diagnostic identifiers; фіксовані ручні категорії й level index; Google | Окремий opt-in; pending reports надсилаються/видаляються окремо. Публічна політика посилається на строк провайдера, а не на встановлений у грі таймер |
| AdMob / UMP | Взаємодії з рекламою, мережеві й технічні дані та дозволені SDK identifiers; Google і погоджені рекламні партнери | Зараз тільки development sample inventory. Publisher inventory проходить publication + UMP перевірки. Власного фіксованого строку зберігання гра не встановлює |
| Email | Адреса, текст і вкладення, які користувач сам надсилає розробнику / поштовому провайдеру | Строк підтримки на сторінці — операційне зобов’язання розробника; таймерів поштового сервісу в грі немає |
| Відкриття вебполітики | HTTP-запит до сайту; хост одержує IP та технічні дані | Лише за переходом на сайт; його hosting/log settings не контролюються грою |

Не встановлено власних player IDs, user IDs, fingerprinting, контактів,
location permission, довільного feedback, завантаження save-файлів або сирих
координат дотиків. Відсутність таких власних параметрів не робить SDK-обробку
анонімною. Остаточні Play Data safety / Apple App Privacy заяви мають відповідати
фактичному релізному бінарному файлу, Google account settings і договорам.

## Що потрібно уточнити в публічному тексті

Сторінка чесно описує старий аудит і неперевірені наступні зміни. Для нової
релізної редакції треба оновити **одночасно uk/en/de**:

1. Firebase Core/Analytics/Crashlytics тепер додані в UPM як 14.0.0, але публікаційний
   gate блокує їхній керований startup і відправлення власних подій у поточній конфігурації.
2. Обмеження Analytics «1–30» виправлено; аналіз охоплює базову кампанію зі 100 рівнів.
3. Додано `level_fail`, `rewarded_ad_requested`, `rewarded_ad_completed`, `reward_granted`;
   їхні значення не є revenue events або підтвердженням реального доходу.
4. Crashlytics має власний перемикач, вимкнені auto-uploads і правила для pending reports,
   але native delivery / withdrawal ще не перевірено на телефоні.
5. Є добровільний косметичний відеобонус поряд із життям. Він також доступний через гру.
6. Для publisher inventory UMP information тепер оновлюється на старті, якщо відеобонуси
   дозволені, і повторно перед явним запитом rewarded ad. Launch-refresh не завантажує
   рекламу й не відкриває форму над основною грою; потрібна форма й `CanRequestAds`
   перевіряються при запиті відео. Native форми й vendor choices ще потребують перевірки.
   Google sample units не мають publisher UMP message й не запускають цей шлях.

Цей документ не змінює опублікований сайт і не підтверджує правову відповідність.

## Перевірка й залишкові умови

- **49/49** реальних focused EditMode test sources виконано через NUnitLite на .NET
  із платформними doubles (`tools/privacy-probe`); це не native Unity Test Runner.
- **19/19** SDK-adapter contract scenarios виконано з doubles (`tools/optional-google-probe`).
- Поточний runtime, Editor, EditMode/PlayMode test sources й optional adapters
  компілюються проти справжніх Firebase 14.0.0 та AdMob 11.6.0 DLL. Окремо пройшла
  компіляція Android-умовних runtime-джерел. Це source compiler checks із кешованими
  Unity references, а не Android player build.
- `FirebasePrivacyProvider.java` компілюється проти Android API 35.
- Офіційний Firebase resource generator обробив наданий JSON; Project ID і App ID
  збігаються. У трьох native source AAR є ARM64 ELF-бібліотеки App/Analytics/Crashlytics.
- Ширший `MonetizationProbe` (для локального .NET 8 виконання) пройшов 13 груп
  контрактів навігації, економіки, save/migration та rewarded ads, після чого
  зупинився на незміненому історичному контенті: `Preserved historical level
  gen:qc_camp:106` у `Campaign100Contracts`. Повний цей probe не пройшов;
  baseline і його валідатор цією інтеграцією не змінювалися.
- Manifest defaults та UPM graph перевірено статично: оголошено один Unity EDM.
  Final Gradle dependency resolution, manifest merge, R8 та Console errors після
  native import не засвідчені цими перевірками.

Перш ніж знімати release gate, потрібні: підтверджена адреса/дані відповідальної
особи; рішення для аудиторії 13–15 і 16–17 років; перевірені поштовий канал і
операційні процедури; актуальний текст політики; Android native перевірки згоди,
реклами та мережевого трафіку. Прапорці review не встановлюються самим існуванням URL.

У Firebase/GA: перевірити Android app `com.quietcamp.game`, увімкнення Analytics
для проєкту, disclosure/data-sharing налаштування, GA4 retention 2 місяці,
відсутність непотрібних exports/Signals/advertising linking, Crashlytics dashboard
і прийняті processing terms. У AdMob: створити власні App/Ad Unit IDs, production
UMP message, vendor list, потрібні вікові обмеження й перевірити privacy options.
Firebase–AdMob linking виконується в акаунтах окремо; воно тут не підтверджене.

Після дозволеного Android запуску перевіряти Analytics у DebugView командою
`adb shell setprop debug.firebase.analytics.app com.quietcamp.game`; після перевірки
повернути `.none.`. ADB команди й APK/AAB збірка в цій роботі не запускались.
Для Crashlytics використати тільки development-кнопку nonfatal, завершити й
повторно запустити застосунок із чинною окремою згодою та перевірити dashboard.
Не робити автоматичного fatal-crash тесту. Перевірити також відмову, відкликання,
нову revision, фон/повторний запуск і відсутність дублювання reward events.

Статус: **реалізовано в коді; релізні умови й пристроєва перевірка залишаються відкритими**.

Джерела SDK: [офіційне встановлення через UPM](https://firebase.google.com/docs/unity/setup-alternative),
[Firebase 14 release notes](https://firebase.google.com/support/release-notes/unity),
[Crashlytics opt-in](https://firebase.google.com/docs/crashlytics/unity/customize-crash-reports),
[Android pending reports API](https://firebase.google.com/docs/reference/android/com/google/firebase/crashlytics/FirebaseCrashlytics).
