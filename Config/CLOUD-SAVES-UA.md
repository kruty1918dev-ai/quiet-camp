# Акаунти та хмарний прогрес Quiet Camp

Оновлення підготовки 24.10.2026: спільний вебкабінет, приватний список власника
та native API описані в `Server/cloudflare/PLAYER-ACCOUNTS-UA.md` і
`Server/cloudflare/ACCOUNT-API.md`. Для нового Unity-клієнта застосовується
`Config/NATIVE-SIGN-IN-UA.md`: нативний Google, той самий Firebase UID і старт
«ознайомлення з політикою → вибір входу → гра» без збору віку. Нижче збережений
опис поточного браузерного Unity-клієнта; native SDK ще не інтегрований.
Підготовлена політика 11 та Worker потребують описаного порядку публікації.

Підтримуються Google і Facebook через Firebase project `quiet-camp` та наявний
Worker `quietcamp-access`. X/Twitter прибрано з UI, клієнтського allow-list,
браузерного входу та серверної авторизації. Apple/iCloud не додаються.

## Чинне налаштування

- Android package: `com.quietcamp.game`.
- Cloud API: `https://quietcamp-access.oleksiy-havryliuk.workers.dev/v1/cloud`.
- Firebase Web app: `Quiet Camp Auth`, auth domain `quiet-camp.firebaseapp.com`.
- OAuth callback: `https://quiet-camp.firebaseapp.com/__/auth/handler`.
- D1: чинна `quietcamp-access`, binding `ACCESS_DB`, юрисдикція EU.
- Опублікована політика en/uk/de: `2026-10-23.public-policy-7-google-facebook`.

Перевірка 24.10.2026: Firebase `accounts:createAuthUri` прийняв обидва провайдери.
Facebook client ID у відповіді — `1476186057735345`, як у даних власника.
Це перевірка конфігурації, а не успішний OAuth, доказ Meta live mode чи Android QA.

Worker: `QC_CLOUD_ENABLED=true`, `QC_CLOUD_SCHEMA=3`,
`QC_CLOUD_PROVIDERS=google,facebook`. Публічний Web config уже є у Worker.
Секрети `QC_CLOUD_RATE_PEPPER`, `QC_ACCESS_ACCOUNT`, `QC_ACCESS_PEPPER` не
змінюються й не копіюються у гру. Meta App Secret лишається у Firebase console.

Перед deploy виконати `python Server/cloudflare/ops.py preflight` та звірити
dashboard variables. `wrangler deploy --keep-vars` зберігає неописані змінні й
секрети; явні cloud values у wrangler.jsonc не вимикають чинний сервіс.
Нові Worker, Firebase project або D1 не потрібні. Усі cloud tables уже наявні;
початкові міграції повторно не застосовувалися.

## Поведінка гри

1. Спершу — чинна політика, вікова категорія й незалежні privacy choices.
   Підтвердження політики не дає згоди на OAuth, Analytics, рекламу чи Crashlytics.
2. Без входу — лише локальний прогрес. Немає Firebase Anonymous Auth, гостьового
   cloud account, анонімної хмари чи cloud toggle. Публічний GET options після
   privacy setup не передає прогресу й не створює акаунта.
3. Google/Facebook відкриває системний браузер. Firebase виконує OAuth, Worker
   перевіряє ID token через Google `accounts:lookup` і видає власну сесію.
   Firebase Unity Auth SDK і WebView не додаються. Код у грі та браузері має збігатися.
4. Наявний акаунт відновлює свою копію. Новий отримує поточний локальний прогрес
   і ресурси. Android-сесія зберігається до відкликання або закінчення строку.
   Editor тримає credentials тільки в пам’яті.
5. Offline прогрес зберігається локально. Sync повторюється приблизно через
   45 секунд після помилки; foreground дозволяє перевірку одразу. HTTP timeout —
   20 секунд. Останній запит при закритті процесу не гарантується.
6. Якщо змінено обидві копії, потрібен явний вибір. CAS revision запобігає
   тихому перезапису; баланси, нагороди та профілі не додаються між собою.
7. Пізня прив’язка гостя до зайнятого профілю відхиляється. Вільний акаунт
   отримує локальний прогрес. Немає merge профілів або підміни прогресу.
8. UI cancel негайно перериває очікування. Скасування popup або закриття сторінки
   відкликає незавершений handoff; збій мережі може перешкодити browser beacon,
   тому є ручне скасування та шестихвилинний клієнтський timeout. Foreground
   будить poll. Пізня відповідь після cancel не зберігає credential;
   уже видану такою відповіддю сесію відкликають.
9. Logout лишає локальний прогрес і відкликає лише поточну device session.
   Видалення хмарного профілю потребує свіжого OAuth та підтвердження на
   `/v1/cloud/delete-account`; видаляються D1-профіль і Firebase Auth record гри,
   а не Google/Facebook-акаунт. Локальне видалення — окрема дія.

Локальна гра лишається доступною для всіх. Хмарний вхід зараз дозволено дорослим;
`cloudSaveTeenReviewed=false` збережено. Для 16–17 потрібен окремий правовий допуск;
unknown/<16 не отримують OAuth. Мінімальний вік локальної гри не змінено.

## Конфігурація клієнта й політики

Не замінювати `Config/owner.local.json` шаблоном. Змінено лише cloudSave,
`legal.publishedPolicyRevision` та `reviews.policyPublished`. `cloudSave.reviewed`
означає конфігураційний/правовий допуск опублікованого account flow, не Android QA.
Інші review-прапорці, native SDK inputs та complimentary Pro не змінено.

Штатний порядок: `owner_config.py validate` → `readiness` → `prepare` → перегляд
полів і diff → `apply`. Публічний allow-listed `owner_client.json` та його наявний
meta-файл версіонуються: новий checkout отримує cloud endpoint, а не мовчки
вимикає вхід. Приватний owner.local і native provider config лишаються ignored.
У публічному snapshot немає серверних секретів або user credentials.

Bundled policy містить усі 14 секцій у трьох мовах і звірена з опублікованими
сторінками. Sync використовує канонічний content/controller сайту. Публікація
політики не вмикає tracking, рекламу, billing чи teen access автоматично.

## Захист і межі перевірок

Session tokens — випадкові 256-bit значення; D1 зберігає SHA-256. Android зберігає
credentials у AES-GCM Keystore/noBackupFilesDir з атомарним записом. Тимчасові
plaintext buffers очищуються також після помилки. Немає tokens у save або логах.

Ticket одноразовий, у URL fragment, живе 10 хвилин; незалежний poll secret
залишається в грі. UID/provider/project/expiry/recent auth/revocation перевіряються;
декодований JWT сам по собі не авторизує. Browser operations мають same-origin
перевірку й CSP. Rate limit: 10 login-start та 240 запитів на інший route за
10 хвилин на HMAC-IP key; невідомі routes не створюють buckets.
Device session живе до 90 днів від використання. Cron прибирає expired дані.

Cloud snapshot schema 2 — лише progress/session/album/memories та allow-listed
ресурси, максимум 768 KiB. Він не переносить Pro, покупки, платіжні токени,
налаштування приватності або credentials. Чинний Pro endpoint і секрети збережено.
Старі X-сесії не авторизують запити. Дані таких профілів не видаляються; якщо той
самий Firebase UID законно входить через Google/Facebook, профіль і save зберігаються.
Різні Firebase UID автоматично не об’єднуються.

Доступні перевірки: 187 portable C# tests, 24 workerd/D1 tests, 13 owner-config
checks, Unity source compilation та окрема javac-перевірка Keystore-класу.
Firebase у серверних тестах є контрольованою відповіддю. HTTP200, createAuthUri
і ці тести не доводять реальний OAuth або роботу Android Keystore.

Перед Android-прийманням власнику потрібні: справжній вхід Google й Facebook
(для Meta перевірити live/test mode та доступ тестерів), cancel/закриття браузера,
рівень і перезапуск, restore на другому пристрої, offline→online, late link до
вільного й зайнятого акаунта, конфлікт без подвоєння ресурсів, logout/relogin та
cloud deletion. Player build/APK/AAB не виконувалися; потрібен окремий дозвіл.
