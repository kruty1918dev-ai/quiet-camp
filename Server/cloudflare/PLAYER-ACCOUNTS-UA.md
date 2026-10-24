# Спільні профілі Quiet Camp — 24 жовтня 2026 року

Підготовлено кабінет en/uk/de, приватний список гравців і API нативного входу.
Unity runtime ще використовує браузер. Production Worker/D1 цим пакетом не
змінені: у середовищі немає налаштованих Cloudflare credentials.

## 1. Що вже відомо

| Поле | Значення |
| --- | --- |
| Firebase project / number | quiet-camp / 327790204451 |
| Android package | com.quietcamp.game |
| Android Firebase App ID | 1:327790204451:android:4e1bc5acc9a78bc3c30396 |
| Web Firebase App ID | 1:327790204451:web:46d23109cb501dcbc30396 |
| Auth domain | quiet-camp.firebaseapp.com |
| OAuth callback | https://quiet-camp.firebaseapp.com/__/auth/handler |
| Meta App ID | 1476186057735345 |
| API | https://quietcamp-access.oleksiy-havryliuk.workers.dev/v1/cloud |
| Cloudflare account | 54bd1b2351d1bbd189427b2a4e43aa5b |
| D1 | quietcamp-access / f8e41926-e58f-4306-8a89-9b0616b367fc / EU |
| D1 binding | ACCESS_DB |
| Website | https://oleksiy-havryliuk.de |

Worker уже має QC_FIREBASE_WEB_CONFIG. Зберегти його, QC_ACCESS_ACCOUNT,
QC_ACCESS_PEPPER, QC_CLOUD_RATE_PEPPER та ACCESS_DB. Серверні секрети не
копіювати у гру/сайт. Meta App Secret залишається у Firebase.

## 2. Firebase та Meta для вебкабінету

https://console.firebase.google.com/project/quiet-camp/authentication/providers
Перевірити Google та Facebook. Не створювати новий Firebase project.
Authentication → Settings → Authorized domains: додати, якщо немає,
`quietcamp-access.oleksiy-havryliuk.workers.dev` (без https/шляхів). Зберегти
`quiet-camp.firebaseapp.com`. Основний сайт лише посилається на Worker-кабінет;
йому нові Firebase variables, CORS або OAuth callback не потрібні.

Meta Facebook Login → Valid OAuth Redirect URIs: зберегти callback із таблиці.
Meta Development Mode дозволяє лише призначені ролі/тестерів; для загального
входу перевірити Live mode та вимоги Meta. Чинний App ID/Secret Firebase не міняти.

## 3. Cloudflare D1: нова міграція

Dashboard → Storage & databases → D1 → quietcamp-access → Console.
Виконати `0004_player_accounts.sql` із пакета ОДИН раз, за потреби по одному
SQL-блоку. Це додає profile, web sessions, deletion markers та nullable поле
profile_json у handoff. Акаунти, saves і чинні сесії не стираються.
Не створювати іншу базу, не запускати вручну старі міграції 0001–0003 повторно.
Повторний ALTER поверне duplicate-column: не видаляти таблицю для виправлення.

Для вже авторизованого Wrangler, якщо попередні migrations застосовувалися
через цей механізм, альтернатива із Server/cloudflare:

    npx wrangler d1 migrations apply quietcamp-access --remote

Якщо попередні SQL застосовувалися вручну без migration ledger — використати
Dashboard і лише 0004. Спершу звірити назву/ID/юрисдикцію з таблицею вище.

## 4. Cloudflare Worker

Workers & Pages → quietcamp-access → Edit code → вставити весь
`worker-dashboard.mjs` із цього пакета → Deploy. Не вставляти окремий
src/index.mjs без залежних модулів. Зберегти bindings/variables/secrets/cron.
У Settings → Variables and Secrets додати текстову змінну:

    QC_ACCOUNT_PORTAL_ENABLED=true

Залишити QC_CLOUD_ENABLED=true, QC_CLOUD_SCHEMA=3, QC_CLOUD_PROVIDERS=google,facebook.
SQL migration 0004 НЕ означає cloud schema 4. Зберегти cron `17 3 * * *`.
CLI-альтернатива за наявності auth: `npx wrangler deploy --keep-vars`.

Перевірити `/v1/cloud/options`: accountPortal і nativeSignIn мають бути true.
Потім відкрити:

- Кабінет: https://quietcamp-access.oleksiy-havryliuk.workers.dev/v1/cloud/account?lang=uk
- Власник: https://quietcamp-access.oleksiy-havryliuk.workers.dev/v1/cloud/admin?lang=uk
- Стара форма: https://quietcamp-access.oleksiy-havryliuk.workers.dev/v1/cloud/delete-account

## 5. Дозволити власнику список гравців

Увійти власним Google у кабінет. Відкрити Firebase → Authentication → Users:
https://console.firebase.google.com/project/quiet-camp/authentication/users
Знайти свій акаунт і скопіювати User UID, не email чи App ID.
У Worker → Settings → Variables and Secrets додати:

    QC_ADMIN_FIREBASE_UIDS=ТВІЙ_FIREBASE_UID

Кілька власників — через кому. Після збереження оновити /admin. Без allow-list
жоден користувач не має прав. Сервер перевіряє UID на кожному запиті; поля,
передані гравцем, не надають ролі. Прибраний UID втрачає доступ одразу на
наступному запиті. Сесія сайту — одна година, cookie HttpOnly/Secure/Strict.

Список містить ідентифікатори, ім'я, email за наявності, способи входу, часові
позначки та кількість чинних ігрових сесій. Немає passwords/tokens/save payload.
Пошук точний, сторінка — 50 рядків. Старі профілі без email заповнять метадані
після наступного входу через оновлений Worker, без зміни accountId/save.

## 6. Google/Firebase для нативного Android

Firebase → Project settings → General → Android com.quietcamp.game.
Додати SHA-1 і SHA-256 фактичних сертифікатів: debug, release для прямого APK,
Play **App signing key certificate** для встановлень із Google Play.
Play Console → Quiet Camp → Test and release → Setup → App integrity /
App-Integrität. Потрібен не лише Upload key certificate.

Після цього завантажити оновлений google-services.json у
QuietCamp/Assets/google-services.json і передати агенту. Він ignored у Git.
Поточний перевірений локальний конфіг не мав OAuth clients; це не доказ стану
живої консолі. Новий конфіг має містити потрібну OAuth інформацію.

Google Cloud Console → той самий project → Google Auth Platform → Clients:
https://console.cloud.google.com/auth/clients?project=quiet-camp
Перевірити Android OAuth client для package + SHA-1 та Web application client ID
для serverClientId у Credential Manager. Агенту потрібен Web ID виду
`...apps.googleusercontent.com`, не API key/Firebase App ID/Android client ID.
Немає потреби в Gmail/Drive scopes, client secret у грі або другому проєкті.

## 7. Meta Android, якщо додається нативний Facebook

У чинному Meta App 1476186057735345 додати Android platform/package
com.quietcamp.game. Точний Activity class має надати агент із фактичного
Unity SDK/manifest. Не вигадувати MainActivity.
Додати Facebook Key Hash для debug/release/Play сертифікатів. Це Base64 SHA-1
DER-сертифіката, не hex SHA-1 із двокрапками.
Для завантаженого Play .der у PowerShell (підставити реальне ім'я файлу):

    [Convert]::ToBase64String([Security.Cryptography.SHA1]::Create().ComputeHash([IO.File]::ReadAllBytes((Resolve-Path '.\deployment_cert.der'))))

Для локального keystore експортувати лише публічний сертифікат через
`keytool -exportcert ... -file quietcamp-signing.der` і порахувати hash так само.
Якщо SDK вимагає Client Token, взяти в Meta App settings та передати через SDK
конфіг. App Secret у гру не додавати. Автоматичні Meta App Events/рекламний ID
для входу не вмикати. Meta може відкрити свій застосунок або браузер.

## 8. Сайт / Vercel / Play / AdMob

Обидва репозиторії мають підготовлену гілку
`feature/shared-player-accounts-20261024`. Після перевірки Worker злити PR
сайту в main: Vercel виконає звичайний deploy. Це навмисно не зроблено до
готовності Worker, щоб не публікувати посилання на неробочий кабінет.

Перевірити /quiet-camp/account/uk, кнопку кабінету, політику revision
2026-10-24.public-policy-11-shared-player-accounts і сторінку видалення.
Сайт/політику публікувати до злиття оновленого bundled policy гри в main.
Персональний кабінет/admin — noindex/no-store; sitemap містить лише публічні
сторінки допомоги. Vercel secrets/env для цих посилань не потрібні.

Play privacy URL лишається /quiet-camp/privacy; deletion URL може лишатися
/quiet-camp/delete-account, де є доступ до реальної форми. Data safety звірити
з фактичним релізом: optional name, email за наявності, user IDs і хмарний
ігровий прогрес, цілі account management/app functionality/security. Умови
«sharing» оцінити за ролями провайдерів та чинними критеріями Play, не ставити
автоматично No лише через назву сервісу. Попередні SDK/ad disclosures зберегти.
AdMob, Analytics consent і рекламні налаштування ця зміна не вмикає.

## 9. Перевірка

На окремому тестовому профілі: сайт → ім'я → logout/relogin → та сама
авторизація в грі → той самий accountId → save → timestamp у кабінеті.
Перевірити cancel, offline, перезапуск, guest-link collision, звичайний акаунт
проти owner admin. Видалення тестового профілю має відкликати всі сесії й
видалити cloud/profile; локальні копії й Google/Facebook лишаються окремо.
Не вважати mocks доказом реального OAuth/Android chooser. Player builds не
дозволені цим завданням. Агенту передати Config/NATIVE-SIGN-IN-UA.md.

Офіційні джерела:
https://firebase.google.com/docs/auth/android/google-signin
https://developer.android.com/identity/sign-in/credential-manager-siwg
https://firebase.google.com/docs/auth/unity/facebook-login
https://firebase.google.com/docs/auth/unity/account-linking
https://support.google.com/googleplay/android-developer/answer/9842756
