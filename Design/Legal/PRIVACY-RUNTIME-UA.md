# Монетизація й privacy: підготовка до production

Оновлено 2026-10-22. Вихідний код аудиту: `e1cdf86e` з origin/main. У робочому
main був незавершений сторонній merge; зміни підготовлено в окремому worktree та
гілці `codex/production-readiness`. Сторонній merge не завершувався.

## Фактичний стан і зміни

| Частина | Під час аудиту | Поточна реалізація |
|---|---|---|
| Google Mobile Ads / UMP | Unity 11.6.0; Dependencies.xml: Android Ads 25.5.0, UMP 4.0.0; тестові App ID | Версії збережено. Owner prepare задає App ID до C#, окремі life/hint/interstitial units; порожні ID вимикають шлях |
| Firebase | Unity App/Analytics/Crashlytics 14.0.0; declared common 23.0.0, Analytics 24.0.0, Crashlytics NDK 21.0.0 | Версії збережено. Повний owner google-services object перевіряється; генерується лише allow-list полів вибраного Android client |
| Ранній старт | FirebaseInitProvider виключено, collection/consent defaults false; Ads measurement delayed | Правила збережено. Без App ID prepare також прибирає MobileAdsInitProvider. Потрібен новий merged-manifest/traffic огляд на Android |
| Вік | П'ять категорій уже існували, загальний gate 16+ | Unknown, Under13, 13–15, 16–17, Adult збережено. Analytics/diagnostics для підлітків потребують окремої вікової перевірки й кожен своєї згоди. Purchases незалежні від tracking |
| Політика | legal.json: draft=true, publishedOn порожнє, minimumAge==13 у gate | Жорсткий ==13 прибрано. Публікація та докази кожної цілі окремі. Чернетку не оголошено опублікованою |
| Firebase consent | Analytics-адаптер повторно записував denied для всіх рекламних цілей | Один FirebaseConsentSignals writer. Зміна Analytics не перезаписує окремий стан advertising. Рекламний export у Firebase консервативно denied без перевіреного окремого джерела; AdMob отримує UMP незалежно |
| Реклама | Rewarded life/hint уже інтегровані через один unit; pending reward лише RAM | Окремі units. Native earned callback зберігає receipt до dismissal; economy застосовує та очищує його одним save. Повторні callbacks/restore не видають двічі |
| Interstitial | Не було | Лише qc.next після завершення рівня. Окремі пороги рівнів, часовий інтервал, cooldown після reward, ліміт сесії, заборона для Pro. Немає реклами поверх перевірки головоломки |
| Billing | UnavailablePurchaseProvider; Pro unpublished, fake лише Editor/probe | Реальний Play Billing 8.0.0 через JNI, ціна ProductDetails, server verification/ack, pending/restore/refund/revocation. За відсутності endpoint/перевірених умов лишається Unavailable |
| Pro | Локальний прапорець; автоматично охоплював усі journeys | Серверне право на 24 години з перевіркою на старті/поверненні/кожні 15 хвилин. Наявні journeys явно includedInPro; нові DLC не включаються автоматично |
| Економіка | 5 життів, 3 підказки; до 10 ad-життів і 5 ad-підказок на UTC день, recovery 30 хв | Значення не змінено. Ресурси не стираються після refund/закінчення Pro |

Це заявлені залежності та compiler API checks, а не resolved Gradle tree чи
перевірка готового APK. Спільний Firebase Core може створювати локальні файли
інших встановлених продуктів; default-off не доводить відсутність файлів/трафіку.

## Дозволи й інтерфейс

Коротке повідомлення → нейтральна вікова категорія без preselection → тільки
доступні для віку цілі → локальне збереження рішення. Продовження після документа
не є згодою на tracking. Немає DOB, документів або обов'язкового parent account.
Для звичайної гри відмова нічого не блокує. Обробка, що потребує батьківської
авторизації, не активується: такого механізму тут немає.

Unknown не допускається до необов'язкових сервісів. Under13: Firebase tracking
вимкнений; дитячий рекламний шлях потребує незалежних child reviews, LTD review,
SDK disclosures, фактичних Android-format/close/identifier checks та поточного
повідомлення. Шлях задає Child/G і вимикає device storage до MobileAds.Initialize.
13–15: тільки окремо перевірений обмежений рекламний шлях, без фіктивного consent.
16–17: реклама неперсоналізована. Персоналізація можлива лише дорослим після
окремої перевірки та фактичних свіжих UMP-рішень. `CanRequestAds` обов'язковий.
UMP Update виконується на запуску дозволеного рекламного шляху й перед запитом;
privacy options залишаються доступними після локальної відмови.

Analytics і Crashlytics мають окремі рішення; crash automatic upload лишається
false. Попередні звіти надсилаються тільки за prior-session та current дозволом;
перша згода й відкликання запитують delete. Завершення native delete/send не
доводиться завершенням C# Task. Діагностичні повідомлення мають фіксовані категорії,
без save payload, user text, receipt/token чи сирого SDK error.

Зміна віку відкликає цілі; помилка privacy-save вимикає runtime-обробку.
Install marker у noBackupFilesDir та перевірка ревізії/хешу збережені. Нові поля
save мають безпечні defaults; історичний version=1 читається без втрати прогресу.
Зароблена нагорода відновлюється з pending receipt. Якщо диск не прийняв жодного
запису й процес загинув, відновлення доказу неможливо гарантувати; RAM retry
працює, але це не можна подавати як crash-durable запис при відмові сховища.

## Події

| Події | Дані / джерело |
|---|---|
| qc_level_begin, qc_level_summary, level_fail | Індекс рівня, числові категорії дій/результату, діапазони часу; реальні session/check/completion callbacks |
| qc_screen, qc_comfort_setting, qc_comfort_feedback, qc_frame_budget події | Збережений обмежений каталог ComfortAnalytics; немає вільного тексту |
| rewarded_ad_requested, rewarded_ad_completed, reward_granted | Наявні life/hint/cosmetic callbacks, category 0..2 |
| qc_ad_requested, qc_ad_loaded, qc_ad_shown, qc_ad_closed, qc_ad_failed | Реальні SDK lifecycle callbacks, placement_category 0 life / 1 hint / 2 cosmetic / 3 interstitial |
| qc_pro_offer, qc_pro_purchase_started, qc_pro_verified | Відкриття economy panel, реальна спроба покупки після отримання ціни, перше підтверджене виконання receipt |
| ad_impression / рекламний дохід | Автоматичний Firebase–AdMob зв'язок після налаштування в консолях. Ручного дубля виручки немає; DebugView/звірку AdMob треба перевірити на Android |
| Crashlytics | Фіксована failure_area, app version, mode, level index; незалежно від Analytics |

Окрема дитяча operational-statistics заготовка вимкнена за замовчуванням з обох
боків; тільки агрегати й окремо підтверджена правова підстава. Це не твердження
«анонімно». Поля, строки й мережеві ризики описані в [серверній інструкції](../../Server/README-UA.md).

## Перевірка й готовність

Використовуйте [конфігурацію власника](../../Config/README-UA.md): validate →
prepare → перегляд → apply → readiness. Публікація політики, legal review,
перевірка backend і Android не виводяться з факту заповненого JSON.

Переносимі перевірки: tools/test_owner_config.py, Server/test_service.py,
tools/privacy-probe (реальні EditMode-джерела через NUnitLite),
tools/optional-google-probe (SDK doubles), tools/verify_monetization.py
(cached compiler references, зокрема Android-conditioned source). Java bridge
компілюється javac проти справжніх Billing 8.0.0 та Android API jars. Це не
Unity import, native scene tests, player build чи інтеграційна перевірка Android.
Старі captures/Android-звіти не є доказами цього набору змін.

| Сервіс | Статус до даних власника й перевірок пристрою |
|---|---|
| Реклама | Код підготовлено; live units/App ID, AdMob/UMP setup, policy й фактичні креативи не підтверджені |
| Аналітика | Код підготовлено; потрібні owner Firebase config, policy, consent/DebugView/traffic checks |
| Діагностика | Окремий робочий код; потрібні crash/restart/send/delete та native file/traffic checks |
| Покупки | Реальний provider і локальний backend-код; backend не розгорнуто, Play товар і license tests не підтверджені |
| Політика | Чернетка; звірити опубліковану та bundled uk/en/de версії, перевірити дані й подати Data Safety власником |

На Android лишаються: clean install і backup/D2D restore; усі віки та незалежні
згоди; privacy options; старі й змінені revisions; offline/no fill/timeouts;
background/foreground та подвійні callbacks; kill між earned/save/dismiss/grant;
disk-full; відновлення pending reward; Pro без реклами й зайвих DLC; pending,
cancel, ITEM_ALREADY_OWNED, reinstall restore, ack, refund/RTDN/voided reconciliation,
повторна покупка з новим token; expiration/offline; ціна й локаль Play; child G
formats, закриття та ідентифікатори; merged manifest до runtime; фактичний
network capture, Firebase DebugView/Crashlytics dashboard та revenue без дубля.
Жодного з цих пунктів не позначено пройденим portable-тестами.

Результат перевірок 2026-10-22: 116/116 NUnit-контрактів через privacy-probe,
37/37 сценаріїв optional-google-probe, 8/8 owner-config і 8/8 backend Python-тестів.
C# runtime компілюється з Android-умовами та справжніми SDK DLL; Java-мостик
компілюється проти Billing 8.0.0 / Android API 35 (10 попереджень про Kotlin
annotation metadata у перевірочному classpath). Console/Unity-native не запускалися.

Повний старий MonetizationProbe з .NET 8 override пройшов 14 груп, включно з
actual SaveAdapter/session, IO rollback, backup/recovery й невідомими JSON-полями,
але завершився помилкою `Preserved historical level gen:qc_camp:106` під час
перевірки архівної головоломки. Domain validator, CampContent і Baseline fixtures
не змінені щодо e1cdf86e. Цей повний probe не вважається пройденим; його збій
не виправлявся зміною контенту або послабленням перевірки в цьому завданні.

Джерела API: [UMP Unity](https://developers.google.com/admob/unity/privacy),
[Firebase defaults](https://firebase.google.com/docs/analytics/android/configure-data-collection),
[Play Billing](https://developer.android.com/google/play/billing/integrate),
[server verification](https://developers.google.com/android-publisher/api-ref/rest/v3/purchases.productsv2/getproductpurchasev2).
