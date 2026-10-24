# Промпт агенту Unity — нативний вхід і спільний профіль Quiet Camp

Реалізуй зміни в існуючому Unity-проєкті, не лише план. Прочитай AGENTS.md і
поточні джерела, збережи чужі/локальні зміни. Підготовлена гілка репозиторію
kruty1918dev-ai/quiet-camp: feature/shared-player-accounts-20261024.
Контракт: Server/cloudflare/ACCOUNT-API.md. Консольні кроки власнику:
Server/cloudflare/PLAYER-ACCOUNTS-UA.md. Не роби reset/checkout поверх змін.

## Результат

Android Google → справжній системний Google Credential Manager account chooser,
без нашої проміжної вебсторінки → Firebase Auth проєкту quiet-camp → той самий
серверний профіль/прогрес, що й на сайті. Один Firebase UID → один accountId.
Створення профілю на сайті перед першим входом у гру має працювати.
Не підміняй native Custom Tabs/WebView/фейковим chooser. Вигляд/додаткові згоди
контролює Google. Browser flow зберегти для Editor і явного fallback; системна
Android панель не запускається всередині Unity Editor.

Facebook native SDK/app-switch додай, якщо є Meta Android конфіг. Той самий
Firebase Facebook provider. Meta може відкрити свій застосунок або браузер,
не обіцяй Google-подібний chooser. До готовності збережи browser Facebook.
X/Twitter, Apple/iCloud, email/password та anonymous cloud не додавати.

## Відомі значення та необхідні дані

- Firebase quiet-camp / number 327790204451.
- Android com.quietcamp.game; Firebase Android App ID
  1:327790204451:android:4e1bc5acc9a78bc3c30396.
- Auth domain quiet-camp.firebaseapp.com.
- Meta App ID 1476186057735345.
- API https://quietcamp-access.oleksiy-havryliuk.workers.dev/v1/cloud.
- Сайт https://oleksiy-havryliuk.de/quiet-camp/account (є /uk, /de).
- Кабінет /v1/cloud/account?lang=uk; форма /v1/cloud/delete-account.
- Нова API: /native/sign-in, /native/delete, /status з profile.
- Політика підготовленої гілки: 2026-10-24.public-policy-11-shared-player-accounts.

У перевіреному локальному google-services.json не було OAuth clients; поточну
консоль власника ще треба звірити. Власник додає SHA-1/SHA-256 debug/release/Play
App signing і дає новий google-services.json та Web OAuth client ID виду
*.apps.googleusercontent.com. Цих відбитків/Web client ID у завданні немає —
не вигадуй їх. Не плутай serverClientId з API key/Firebase App ID/Android ID.
Для Meta надай власнику точні Activity class/Key Hash/Client Token inputs,
відповідні реальному SDK/manifest. App Secret залишається у Firebase Console.
Не потрібні Gmail/Drive scopes, нові Firebase/Worker/D1, секрети в клієнті.

## Реалізація

1. Перевір фактичні Firebase/Google/EDM4U versions, asmdef, optional providers,
   Unity 6.6, min SDK 26/target 35. Додай Firebase Auth сумісної версії, без другої
   несумісної Firebase App та дубльованих Gradle classes.
2. Native Android Google — актуальний AndroidX Credential Manager + googleid
   через підтримуваний Unity bridge. Для явної кнопки дотримуйся офіційного
   GetSignInWithGoogleOption flow, якщо його рекомендують актуальні docs.
   Коректний serverClientId, Unity main/UI thread, lifecycle, cancel/no-credentials.
3. Google ID token → Firebase GoogleAuthProvider/SignInWithCredential. Meta
   token → Firebase FacebookAuthProvider. API приймає FRESH FIREBASE ID TOKEN,
   не raw provider token. auth_time ≤10 хвилин: forceRefresh не замінює новий
   sign-in/reauth. Credentials не логувати й не додавати в URL/save.
4. GET /options тепер додає nativeSignIn/accountPortal. Якщо сервер ще старий,
   збережи browser fallback без нескінченних retry/зламаного входу.
5. POST /native/sign-in {provider,mode:'signin',idToken}. CloudReply лишається:
   accountId,sessionToken (64 hex),provider,created; додано profile з ім'ям,
   email (може null), providers, hasCloudSave, cloudUpdated.
6. Інтегруй у справжній CloudSaveService/Remember/restore/Sync. Не створюй
   другий save manager. CloudCredentialStore Keystore/noBackup зберегти,
   Editor — лише пам'ять. Firebase refresh token не копіювати в ігрові дані;
   перевір реальне SDK storage/backup поводження.
7. Website-created profile: created:false + /read snapshot:null,revision:0 —
   нормальний порожній профіль. Звичайний signin + чинний read/CAS flow.
   Не підставляй created:true, не створюй інший UID і не перезаписуй cloud
   прогрес без застосування поточного restore/conflict механізму.
8. CloudAccountView.LinkLocal зараз залежить від accountChoiceVersion. Late
   guest mode:'link' відхиляє будь-який зайнятий профіль, зокрема порожній із
   сайту. Розрізняй явну прив'язку гостя та вхід до наявного профілю. Дай
   можливість відкрити існуючий профіль із захистом локального прогресу й
   конфліктом, без тихого merge/reset/upload та додаткового onboarding.
9. Зберегти cancel, timeout, offline, restore, CAS revisions, album/memories,
   ресурси, late-link refusal, logout/relogin. Пізно видану після cancel/dispose
   server session відкликати; після storage failure не залишати orphan session.
10. /status також повертає profile: ім'я й методи входу показати в існуючих
    налаштуваннях акаунта. Null email нормальний. Кабінет/політика/допомога
    можуть відкриватися звичайним браузером.
    displayName/email — користувацькі дані: екранувати штатним HtmlUi.Text,
    не вставляти як raw HTML у Unity HtmlSurface.
11. Після публікації політики 11 звірити legal.publishedPolicyRevision у
    owner.local/owner_client та legal.revision штатним owner_config.py workflow
    validate/readiness/prepare/apply. Зберегти чинні ключі й налаштування власника,
    не замінювати їх шаблоном і не вмикати чужі review/ads/Analytics flags.

## Видалення та приватність

- Cloud deletion: свіжа reauth саме підключеного Firebase користувача → явне
  підтвердження → POST /native/delete {idToken,confirm:true}. API визначає target
  за verified token; client-supplied accountId/UID не підтримуються.
- Зістав reauth identity з показаним акаунтом перед destructive request.
  Вибір іншого Google у chooser не повинен видалити інший профіль. За потреби
  перевір accountId через native signin без застосування нового прогресу,
  зістав з поточним та відклич тимчасову session.
- Success: зупинити sync/stale requests, очистити cloud credentials, Firebase
  sign-out/Credential Manager state. Локальний прогрес видаляється ОКРЕМО.
  Після stale 401 не відтворювати видалений акаунт автоматично.
- deletion_retry: D1-дані вже прибрані, Firebase cleanup потребує свіжої reauth;
  deletion_pending блокує відтворення даних. Дати retry/вебформу без циклу.
- Provider linking — Firebase LinkWithCredential або кабінет, не email matching
  і не mode:'link'. Два UID/баланси автоматично не зливати.
- Старт ЛИШЕ: наша панель ознайомлення з політикою → Google/Facebook/без входу
  → гра. ЖОДНОГО віку/групи/дати, окремих опитувань про відеобонуси, Analytics,
  Crash чи summary. Переглянути старі eligibility залежності, не просто
  приховати панель, не підставляти Adult і не вигадувати згоду/review approval.
- Sign-in не вмикає Analytics/Crashlytics/Meta App Events/advertising ID чи
  персоналізацію. SDK Auth налаштувати без автоматичного трекінгу. Зберегти
  чинні UMP/AdMob/Pro правила попереднього промпту як окремі механізми.
- Не змінювати серверні секрети, billing, legal/service review flags. Не
  підробляти Origin для старого login-complete і не додавати wildcard CORS.

## Стиль, перевірка та результат

Стиль Quiet Camp: #132e25 / #f6efdf / #ddbf86, читабельні кнопки, >=48dp,
SafeArea, en/uk/de. Системний Google UI не перемальовувати. Debug fields із
ключами в гравецькому інтерфейсі не додавати.

Перевір: сайт→гра і гра→сайт той самий accountId; порожній website profile;
restore ненульового save; cancel/late response; інший користувач; occupied
link; без дублювання ресурсів; offline/restart; native delete/stale401/retry;
null email; provider linking не змінює accountId; credentials не в save/logs.

Дотримуйся AGENTS щодо Unity/ADB/інших сесій. Player builds/APK/AAB цим
завданням НЕ дозволені. Запусти доступні portable/compile checks. Моки не є
доказом справжнього chooser/production login. Commit/push перевірені етапи.
В кінці дай файли/commit, результати та точні відсутні console inputs.
Не вигадуй успішний deploy чи Android QA.

Офіційні джерела — перевір актуальні версії перед інтеграцією:
https://developer.android.com/identity/sign-in/credential-manager-siwg
https://firebase.google.com/docs/auth/android/google-signin
https://firebase.google.com/docs/auth/unity/google-signin
https://firebase.google.com/docs/auth/unity/facebook-login
https://firebase.google.com/docs/auth/unity/account-linking
