# Перевірка privacy-механізмів перед Android-релізом

Дата: 2026-10-21. Робочі гілки гри й сайту: `privacy-runtime-completion`. Основа гри — `main` 9d3e0246; сайту — e2220fa. На початку обидва робочі дерева були чистими. Старі privacy-гілки використані вибірково: нову інтеграцію Firebase не замінено старою реалізацією. Політика: **2026-10-21.privacy-4**, залишається review copy. Контролер — фізична особа Oleksii Havryliuk (публічно Oleksiy Havryliuk), Kruty1918 Games — бренд.

## Що реалізовано

Нейтральні категорії Unknown / Under13 / 13–15 / 16–17 / 18+ доступні на стартовому екрані й у Налаштуваннях → Дані й документи → Вікова категорія. Немає DOB, документів або батьківської верифікації. Для до 13 показано призначення гри 13+; локальна гра для 13–15 доступна. Відмова від необов’язкових цілей не блокує гру або юридичні документи. Підтвердження документа не створює жодної згоди.

Unknown/<16 не допускає власної ініціалізації Analytics/Crashlytics, передавання звітів, AdMob або UMP-запитів. Для 16+ є незалежні, початково вимкнені Analytics, crash і advertising opt-ins. optionalVideoBonuses — лише доступність бонусів. Зміна категорії відкликає всі три дозволи; застарілі версії/ревізії й некоректні категорії не приймаються.

Android install marker у noBackupFilesDir зв’язує локальне рішення з інсталяцією без передавання маркера. Відсутність/пошкодження/невідповідність marker та завантаження recovery-save скидають дозволи. Перед privacy-save marker змінюється: при невдалому записі старий save не має чинного receipt. Поточна помилка запису залишає цілі вимкненими; поведінку реального backup/D2D і файлових збоїв ще треба перевірити на пристрої.

Analytics зберігає десять власних подій і числові обмежені параметри, рекламні згоди Firebase — denied. Відкликання зупиняє події й робить local reset через вже створений транспорт. Crash reporting має окреме рішення, session receipt, послідовне видавання send/delete запитів і захист від пізнього завершення на боці гри; помилка видавання запиту вимикає транспорт до перезапуску. Auto-upload ніколи не записується як true. Ручні повідомлення фіксовані, ключі: game_version, game_mode, level_index, failure_area. Це не означає очищення всіх SDK-generated payloads.

Реклама лише rewarded: npa=1 для всіх; AgeRestrictedTreatment.Teen для 16–17, Unspecified для 18+, MaxAdContentRating.T. UMP оновлюється перед production-запитом; після форми додатково перевіряються device-storage purpose consent і Google vendor consent (IAB vendor 755 — Google Advertising Products, перевірено в поточному GVL 181). Відсутній/відхилений сигнал блокує показ замість Limited Ads fallback. Це додатковий fail-closed контроль, не власна повна реалізація CMP або доказ згоди всіх постачальників.

Після потрібного Google privacy-options рішення зберігається лише ознака доступності дії. Вона не дає рекламного дозволу й не запускає UMP автоматично. Допустимий користувач може явно відкрити Google choices після локального відкликання та перезапуску; тоді оновлюється UMP, без ініціалізації рекламного показу. Unknown/<16 не може виконати цю дію. Вебсторінка не імітує керування згодою гри.

Sample ads — лише Editor/development, sample app/unit IDs, та сама вікова й локальна consent-перевірка. Вони пропускають publisher UMP, бо sample IDs не мають повідомлення видавця. Release не активує sample inventory. Винагорода потребує reward callback і закриття реклами; відкликання не звільняє ігрове блокування, поки видима native форма/реклама не закрилася, але скасовує очікувану винагороду. Збережені idempotence, save-before-reward_granted, 10 життів/UTC-добу й ліхтарик за звичайну гру. Баланс не змінено.

## Порядок запуску і встановлені залежності

1. До C# Android може запускати ContentProviders і завантажувати native plugins. Вихідний manifest виключає FirebaseInitProvider; власний FirebasePrivacyProvider тепер не викликає Firebase API. Є DELAY_APP_MEASUREMENT_INIT=true; це не доказ бездіяльності MobileAdsInitProvider.
2. C# runtime registration лише реєструє фабрики. Boot читає документ/локальний вибір. GameServices перевіряє receipt, категорію, ревізії й publication gate, створює ліниві boundary-адаптери. Без належного opt-in фабрики SDK не викликаються.
3. Analytics/crash opt-in запускає спільний FirebaseRuntime: dependency check → DefaultInstance → явна заборона Analytics collection/storage/ad purposes і native Crashlytics auto-collection → окремий дозволений адаптер. Гра не звертається до Unity Crashlytics API в Analytics-only C# шляху; можливі внутрішні SDK handlers ще не перевірено.
4. Створення DefaultInstance може саме створити eager native компоненти встановлених Firebase-продуктів. Manifest false не перебиває гарантовано всі старі persistent overrides. Не доведено відсутність файлів, ідентифікаторів або трафіку в цьому проміжку. Це блокер релізу, а не пройдена перевірка.
5. Ads: чинний локальний дозвіл/вік → fresh UMP → потрібна форма → CanRequestAds і додатковий storage/vendor guard → teen/rating configuration → MobileAds.Initialize → NPA rewarded load → reward callback + close.

manifest.json, packages-lock.json і фактичні tgz підтверджують Firebase Core/Analytics/Crashlytics **14.0.0**. UPM AdMob — **v11.6.0**; фактичний cached Core DLL скомпільовано з новими API. Декларовані Android-залежності з SDK Dependencies.xml: firebase-common 23.0.0, firebase-analytics 24.0.0, firebase-crashlytics-ndk 21.0.0, firebase-*-unity 14.0.0, play-services-base 18.11.0, play-services-ads 25.5.0, UMP 4.0.0. Це заявлені залежності, не resolved Gradle tree і не merged manifest готового AAB.

## Доступні перевірки

- Privacy probe: **70/70** NUnit-перевірки реальних класів і focused EditMode test sources на .NET із платформними підмінами.
- Optional Google probe: **25/25** сценаріїв з SDK API doubles: callback/close/disposal, default deny, NPA/teen, незалежність цілей, failed-save, withdrawal races, serialized reports, explicit privacy options після відмови. Це не Google/Android runtime.
- Focused save probe: **6/6** груп; реальний SaveAdapter і пакет save-system, CRC/staged migration, backup recovery зі скиданням дозволів, IO rollback, винагороди й save transaction.
- ?????? MonetizationProbe ????????? 14 ????, ??????? ??????? ?????/??????????, ? ???? ????? ?? ??? ???????? content-baseline gen:qc_camp:106 (Campaign100Contracts). ?? ?????? ???????? ???? privacy-???????; ?????? suite ?? ????????? ????????.
- Поточні C# sources компілюються з реальними SDK DLL і cached Unity references; окремо перевірено Android-умови коду без UNITY_EDITOR. Це compiler checks, без Unity import/scene execution/player build.
- FirebasePrivacyProvider.java компілюється javac проти Android API 35. Це не перевірка manifest merger або стартового провайдера на телефоні.
- Повні статичні en/uk/de: 14 розділів, одна ревізія, канонічні адреси, без script, без unresolved placeholders; синхронізація з bundledBody перевіряється tools/sync_privacy_policy.py.

Команди з кореня: dotnet build tools/privacy-probe/Probe.csproj --artifacts-path QuietCamp/Temp/ai/privacy-probe; запуск відповідного Probe.dll --workers=0. Аналогічно tools/optional-google-probe. tools/MonetizationProbe.csproj підтримує --privacy-save-check; у поточному середовищі SDK .NET 8, тому використана тимчасова копія csproj з net8.0 й тими самими absolute Compile Includes. python tools/verify_monetization.py --google-sdk-check, а для Android sources додатково --player-source-check --android-source-check (це не player build). Логи лишаються в ignored QuietCamp/Temp/ai/.

## Невиконані релізні перевірки — owner checklist

Доступу до Firebase/GA4/AdMob/Play Console у цій сесії немає. Unity, ADB, player builds не запускалися. Public Android [AAR Crashlytics 21.0.0](https://dl.google.com/dl/android/maven2/com/google/firebase/firebase-crashlytics/21.0.0/firebase-crashlytics-21.0.0.aar) перевірено javap: sendUnsentReports/deleteUnsentReports повертають void; JNI використовує Call(method), не Call<AndroidJavaObject>. Підтримуваний API не дає Task завершення цих операцій. Game-side Task.CompletedTask означає лише видавання запиту; API-double сценарії із затримками не доводять native completion/order. Це окремий блокер перевірки реальних pending files і наступних запусків.

Придатного дозволеного Android build/device-сеансу з цими змінами немає. Не позначати наведене нижче перевіреним за JSON або документацією.

| Власник / налаштування | Потрібний результат і доказ |
|---|---|
| GA4 → Admin → Data collection and modification → Data retention | User/event-level retention **2 months**, **Reset user data on new activity off**; скриншот property ID + дати, не ключів |
| GA4 Account settings → Data sharing settings | Звірити всі чотири switches (Google products & services, Modeling contributions & business insights, Technical support, Recommendations for your business); записати фактичні значення й підставу, вимкнути необов’язкове sharing обраної моделі |
| GA4 Data collection / Advertising features / Product links | Google signals, advertising personalization і непотрібні Google Ads/AdMob/BigQuery links/exports відсутні або вимкнені; скриншоти і фактичні settings. Два місяці не обмежують автоматично агрегати й exports |
| AdMob → Apps → App settings / Ad units | Справжні app ID й rewarded ad-unit ID для com.quietcamp.game, readiness/status; у поточній грі production unit IDs порожні. Не підміняти sample app ID production unit ID |
| AdMob → Privacy & messaging → European regulations | Published message, саме ця app, EEA/UK/Switzerland, en/uk/de або явно перевірений fallback; явні відмова/керування, фактичні purpose/vendor choices; список ad-tech providers і version/date. Перевірити certified CMP/TCF requirements |
| UMP consent mode / Analytics synchronization | Перевірити й вимкнути автоматичне перенесення CMP-вибору на Firebase Analytics/storage, яке могло б обійти незалежну Analytics згоду або дозволити рекламні Firebase purposes. Потрібний доказ native consent state при ad-only, analytics-only і crash-only |
| AdMob NPA / providers / Limited ads | NPA для всіх; додаткові RTB/mediation providers лише після перевірки розкриття та сигналів. На пристрої підтвердити npa=1, teen tfat, рейтинг, Google vendor/storage consent; відмова purpose 1 не дає навіть Limited Ads |
| Firebase Crashlytics / exports / agreements | Auto-collection false, відсутні непотрібні exports; фактичні roles/contracts, retention та процедура data requests. Не обіцяти серверне видалення через local reset |
| Play Console → App content | Privacy policy URLs і контролер, Ads declaration, Target audience 13–15/16–17/18+ за фактичним призначенням, rating із анкети, остаточний Data safety. Наявність 13+ сама по собі не доводить звільнення від Families rules |
| Контролер / hosting / mail | Повна поштова адреса, робота privacy/support адрес, договори/одержувачі/трансфери, строки логів/backups і поштовий графік. Не просити паролі, токени, signing keys чи документи особи |

## Точний Android-сценарій для зняття блокерів

У дозволеному середовищі власник надає/готує відповідний QA build; не користуватися іншою активною ADB-сесією. Зафіксувати commit/build hash, package, Android version, resolved dependency tree і merged manifest з усіма providers, metadata та backup/extraction rules.

Для clean install, upgrade зі старої версії (включно з раніше true SDK overrides), restart, backup restore і D2D виконати Unknown, Under13, 13–15, 16–17, 18+. У кожній допустимій категорії: all-off, analytics-only, crash-only, ad-only, усі opt-ins; потім зміна віку, відкликання під час dependency check/UMP/load/видимого ad, негайне повторне ввімкнення, background/resume і app termination. Після відмови основна гра та документи мають працювати.

Порівняти app-private files/shared preferences/noBackup до UI, до/після кожного вибору й після restart. Перевірити FIS/Crashlytics/session/app-instance/advertising IDs, Crashlytics NDK/minidump/log файли та diagnostic payloads: ключі, paths, exception messages, breadcrumbs; не обмежуватися чотирма ручними custom keys. Зіставити датовані мережеві captures із SDK/серверними записами; зашифрований нерозшифрований capture не доводить відсутності даних. Unknown/<16 та all-off не повинні робити optional SDK requests.

Для pending reports: crash без згоди → перша згода → restart (старий звіт не передається); valid prior crash opt-in → restart з чинним поточним дозволом (можливий send); withdrawal/age change між send/delete completions (немає нового enable); помилка JNI-виклику (транспорт вимкнений); native timeout після успішного void-виклику адаптер не спостерігає, це окрема перевірка. Наявність send/delete виклику не доводить фактичного завершення видалення або можливості скасувати вже початий upload.

Для EEA/UK/CH перевірити UMP required/expired/refused/no-storage/no-vendor/error/offline, privacy options після локального withdrawal та restart. Debug geography — лише QA, не production. За неприйнятної/невідомої згоди немає ad load і Limited Ads fallback. Перевірити NPA/teen signals у native request, реальну взаємодію console consent mode із Firebase й sample/release isolation.

Лише після цих доказів, адреси й console review можна узгодити остаточний текст та відкрити legal/publication flags. Ця робота не публікує policy на production-хостингу й не сертифікує реліз.

Джерела: [Google age treatment](https://developers.google.com/admob/unity/targeting), [UMP](https://developers.google.com/admob/unity/privacy), [NPA](https://support.google.com/admob/answer/7676680), [European messages](https://support.google.com/admob/answer/10114014), [GA retention](https://support.google.com/analytics/answer/7667196), [Firebase privacy](https://firebase.google.com/support/privacy).
