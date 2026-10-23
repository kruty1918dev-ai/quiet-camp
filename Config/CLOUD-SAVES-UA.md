# Акаунти та автоматичне збереження — 23.10.2026

Це реалізація джерел. Нову хмару не розгорнуто; реальні Google/Facebook/X,
Unity/Java compilation, Android UI, UnityWebRequest, Keystore й OAuth ще потрібно
перевірити. APK/AAB та запуск Unity не виконувалися. Чинний complimentary Pro
backend і його секрети не змінюються.

## Сценарії

1. Спершу чинне підтвердження політики, якщо його ще немає або документ змінився,
   і незалежні налаштування приватності. Після цього вибір способу входу.
2. **Анонімно**: прогрес лише локальний. Немає анонімної хмари, створення
   серверного профілю чи окремого cloud switch. Публічний GET options може
   перевірити доступність провайдерів, але не передає прогрес і не створює акаунт.
3. **Google / Facebook / X**: вхід одразу підключає автоматичне збереження.
   Наявний профіль відновлюється; новий починає з поточних локальних даних.
   На наступних запусках після privacy setup перевіряється збережена сесія.
   Повторний браузерний вхід кожного разу не потрібен, поки сесія чинна.
4. **Offline**: гра використовує локальну копію, зберігає нові зміни й повторює
   синхронізацію при поверненні зв’язку. HTTP має timeout 20 секунд. Foreground
   повтор після помилки — приблизно 45 секунд; повернення у застосунок дозволяє
   перевірку одразу. Немає обіцянки останнього HTTP-запиту при закритті процесу.
5. **Пізня прив’язка** у Settings → Account & cloud progress: зайнятий ігровим
   профілем акаунт відхиляється із проханням обрати інший. Немає переходу на чужий
   профіль, автоматичного merge чи пропозиції перезаписати його. Вільний акаунт
   отримує поточний локальний прогрес і ресурси, а не порожній початковий стан.
6. **Нагадування** у головному меню: після третього пройденого рівня, наступних
   п’яти рівнів або нової значної винагороди в memories. Не частіше одного разу
   на добу та за запуск; можна відкласти. Лічильники лише локальні. Прив’язаним
   або неeligible гравцям нагадування не показується.
7. **Два пристрої**: незмінена локальна копія автоматично отримує новішу хмарну.
   Локальні offline зміни завантажуються, якщо серверна ревізія не змінилася.
   Якщо обидві копії змінювалися, потрібен явний вибір. CAS revision не дозволяє
   одночасним записам непомітно перезаписати один одного; ресурси не додаються
   між двома копіями, щоб не подвоювати винагороди.
8. **Вихід / видалення**: вихід залишає локальну копію й пробує відкликати лише
   цю серверну сесію. Профіль залишається. Видалення хмарного профілю потребує
   свіжого входу у браузері та підтвердження. Видаляються D1 дані та Firebase
   Auth профіль гри, а не Google/Facebook/X акаунт. Локальне видалення окреме.

## Які дані синхронізуються

Snapshot schema **2**: progress/session/album/memories + allow-listed resources.
Проходження, розкладка, навчання/сюжет, альбом, зароблена косметика й винагороди,
життя, підказки, валюта, спроби, відновлення життів і лічильники використання
винагород. Максимум 768 KiB. Невалідні/завеликі дані не обрізаються мовчки.

Не передаються privacy/age decisions, налаштування пристрою, reminders,
платіжні токени, paid entitlement records, Pro чи службові credentials.
Покупки відновлюються окремо через чинний магазин/verification flow, який ще не
активований у production. Хмара не надає Pro. Ресурсні баланси залишаються
клієнтським прогресом; це не авторитетний платіжний ledger або доказ захисту
від модифікації save. Продаж не можна активувати на підставі цих тестів.

Перевірки 23.10.2026: **172/172** портативних C# тестів, **19/19** workerd/D1
серверних тестів, **13/13** owner-config; C#9 syntax для 15 змінених файлів
під player/editor symbols — без помилок. Сайт: build/test 33 сторінок і повна
синхронізація політики en/uk/de пройшли. Firebase API у серверних тестах —
контрольовані тестові відповіді, а не реальний OAuth. Це не повна компіляція
Unity/Java, не Android та не підтвердження готовності до публікації.

## Налаштування перед підключенням

1. У наявному Firebase project `quiet-camp` додати **Web app** для браузерної
   авторизації. Потрібні публічні `apiKey`, `projectId`, `authDomain`, `appId`.
   Auth domain: `quiet-camp.firebaseapp.com`; appId має форму `1:NUMBER:web:HEX`.
   Це не Android `google-services.json`. Web key має дозволяти Firebase Auth
   і серверний accounts:lookup, а не мати Android-only SHA/package restriction.
2. Увімкнути реальні providers: Google OAuth consent/support email;
   Facebook Login у Meta app; X developer app із входом. Callback Meta/X:
   `https://quiet-camp.firebaseapp.com/__/auth/handler`. Client secrets вносяться
   лише у Firebase/provider consoles, не в Git/Assets/чат. Перевірити provider
   live/test mode, доступ тестерів, review та умови. Платні плани/trials не створювати.
3. Firebase Authorized domains має містити
   `quietcamp-access.oleksiy-havryliuk.workers.dev`. Авторизація у системному
   браузері через Firebase JS 13.1.0, а не в ігровому WebView. Звірити код
   зв’язування в грі та браузері. Додатковий Firebase Unity Auth SDK не додається;
   Analytics/Crashlytics для входу не запускаються.
4. До наявної EU D1 бази застосувати **всі три** migration files. Не створювати
   нову базу й не змінювати 0001/0002, якщо їх уже застосовано:

   ```sh
   cd Server/cloudflare
   npm ci
   npx wrangler d1 migrations apply quietcamp-access --remote
   ```

   `0003_linked_accounts_only.sql` додає accountless login handoff; старі
   експериментальні anonymous credentials більше не дозволяють cloud operations.
   Legacy anonymous rows, якщо вони існують, зберігаються до старого cleanup
   (180 днів recorded inactivity); нові гостьові профілі не створюються.
5. Worker vars: `QC_CLOUD_SCHEMA="3"`, `QC_CLOUD_ENABLED="true"`,
   `QC_CLOUD_PROVIDERS="google,facebook,x"` лише для справді готових providers.
   `QC_ANONYMOUS_CLOUD` більше не використовується. `QC_FIREBASE_WEB_CONFIG`
   містить чотири публічні поля з кроку 1. `QC_CLOUD_RATE_PEPPER` — окремий
   випадковий secret ≥32 символи, створений локально через Wrangler Secrets.
   **Не змінювати** `QC_ACCESS_ACCOUNT` і `QC_ACCESS_PEPPER` чинного Pro.
6. `npx wrangler deploy` збирає index.mjs + cloud.mjs. Перевірити справжні HTTPS
   cloud routes, provider OAuth та незмінений `/v1/access/health`.
   `/v1/cloud/anonymous` має бути відсутнім/відхилятися. Cron 03:17 UTC очищує
   expired handoffs/sessions. Нові routes зараз не deployed; source defaults
   залишають `QC_CLOUD_ENABLED=false`, schema `0`, providers порожніми.

## Налаштування гри й політики

Зберігаючи інші поля приватного `Config/owner.local.json`:

```json
"cloudSave": {
  "enabled": true,
  "endpoint": "https://quietcamp-access.oleksiy-havryliuk.workers.dev/v1/cloud",
  "reviewed": false,
  "teenReviewed": false
}
```

`reviewed` — правовий/конфігураційний допуск після фактичних providers,
публікації політики й процедури видалення; не доказ Android-тестування.
`teenReviewed` окремо відкриває 16–17 після правової оцінки. Unknown/<16 —
локальна гра, без реалізованої батьківської авторизації. Вік гри не змінюється.
Старі owner configs без cloudSave лишають його вимкненим.

Кандидат політики en/uk/de: `2026-10-23.public-policy-6-linked-accounts`.
Лише після публікації саме цього тексту оновити legal.publishedPolicyRevision
і reviews.policyPublished. Після цього validate → prepare → review diff → apply
за Config/README-UA.md. Owner tool звіряє три мови/URL/revision і публікаційні
поля bundled policy. Tracking, Ads, Crashlytics і покупки мають окремі прапорці.

## Безпека та строки

Непрозорі 256-bit session tokens; D1 зберігає SHA-256, Android — AES-GCM Keystore
у noBackupFilesDir. Тільки короткий одноразовий ticket у URL fragment; окремий
poll secret залишається у грі. До підтвердження Firebase ID token сервер не
створює ігрового профілю чи прогресу. UID/provider/project/expiry/recent auth
звіряються з Google accounts:lookup; просте декодування JWT не авторизує вхід.
Унікальний Firebase UID і атомарний INSERT блокують одночасну подвійну прив’язку.
Видалення також прибирає незавершені handoffs цього UID.

Linked progress — до видалення; device session — 90 днів від використання;
handoff — 10 хвилин. Cleanup щоденний. Rate windows — 10 хвилин, HMAC-IP ключі
очищуються щодня спільним Pro/cloud cleanup. D1 Free Time Travel може лишати
попередні стани до 7 днів. Backup restore потребує повторного застосування
виконаних запитів видалення. Firebase/provider processing не обмежено EU D1.
Незавершений вхід може залишити Firebase Auth record; browser deletion/privacy
request доступні й без завершеного повернення до гри.

Це не незалежний security audit. Cloud CPU/навантаження, Free quotas, signup abuse,
monitoring, provider CSP/popup й Android ще перевірити. JNI-клас
`com.quietcamp.game.privacy.CloudCredentialStorage` треба preserve при майбутньому
ввімкненні Android minification; зараз minify вимкнено.

Перед release перевірити справжні три входи, перезапуск/два пристрої/offline,
автовідновлення, локальні винагороди й прив’язку, occupied-account refusal,
конфлікти без подвоєння ресурсів, reminders/cooldown, Keystore та видалення.
Оновити Google Play App access, Data safety (User IDs, gameplay/resources,
застосовні Firebase profile/contact fields), account-deletion URL, age/Families.
Публікація політики або push джерел не активує backend чи SDK review flags.

Джерела: [Firebase setup](https://firebase.google.com/docs/web/setup),
[Google](https://firebase.google.com/docs/auth/web/google-signin),
[Facebook](https://firebase.google.com/docs/auth/web/facebook-login),
[X](https://firebase.google.com/docs/auth/web/twitter-login),
[ID tokens](https://firebase.google.com/docs/auth/admin/verify-id-tokens),
[REST](https://firebase.google.com/docs/reference/rest/auth),
[Play deletion](https://support.google.com/googleplay/android-developer/answer/13327111?hl=en).
