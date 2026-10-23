# Complimentary Pro: Cloudflare Workers + EU D1

Працює на наданих власником ресурсах; Vercel/Upstash не використовуються.
Гра, сайт, DNS, пошта та тариф не змінювалися. APK/AAB/Unity Player не запускалися.

- Worker: `quietcamp-access`.
- Endpoint: `https://quietcamp-access.oleksiy-havryliuk.workers.dev/v1/access`.
- D1: `quietcamp-access`, UUID `f8e41926-e58f-4306-8a89-9b0616b367fc`.
- Jurisdiction: `eu`; read replication: disabled. EU стосується зберігання
  і виконання D1, а не гарантії обробки HTTP лише європейським Worker edge.
- Binding: `ACCESS_DB`. Міграція `0001_complimentary.sql` застосована до
  порожньої наявної бази. Створено тільки таблиці/індекси сервісу та migration metadata.

## Пароль і ліміт CPU

Початковий пароль цього облікового запису видано provisioner через
`secrets.choice`: 32 незалежні символи з алфавіту 62, приблизно 190,5 біт ентропії.
Це випадковий credential, а не придуманий людиною пароль. Ім'я й пароль збережені.
Перед конвертацією `cloudflare/provision.py` перевірив його відповідність
оригінальному PBKDF2-SHA256-хешу на **600000** ітерацій; ітерації не знижувалися.

Workerd виконав локальну 600000-iteration перевірку приблизно за 157 мс elapsed.
Це не вимір CPU на Cloudflare й не підстава вважати PBKDF2 сумісною з Free 10 мс.
У Worker для спеціально згенерованого credential використовується нативний
WebCrypto HMAC-SHA256: `HMAC(pepper, domain + username + password)` і `subtle.verify`.
Випадковий 256-bit pepper і verifier зберігаються в **Worker Secrets**; D1 не
містить verifier, pepper або пароль. Підбір 190-bit випадкового значення
непрактичний і без повільної KDF. Цю схему не можна переносити на людські паролі.
Provisioner вимагає metadata походження від random generator, формат alnum32
і перевірку оригінального PBKDF2. Ротація не відбувається при redeploy.

Фактична Cloudflare GraphQL-вибірка після HTTPS-тестів: 19 запитів,
CPU P50 **2757 µs = 2,757 мс**, P99 **6764 µs = 6,764 мс**, execution errors **0**.
Вибірка починається 2026-10-23T07:55:27Z. Одиниці підтверджено introspection
Cloudflare API. Це вимір обмеженої перевірки, не гарантія worst-case усіх запитів.
CPU/квоти треба надалі контролювати; upgrade чи trial не виконувалися.

## Контракт і захист

`POST /sign-in`: JSON з `packageName`, `username`, `password`.
`POST /refresh`: JSON з `packageName`, `sessionToken`.
Відповідь сумісна з чинним Unity transport: package/product, `scope=pro`,
`state=Granted`, випадковий 43-символьний sessionToken, `validUntilUtc` на 24 години,
`explorePublishedContent=true` для виданого повного доступу.
`GET /health` перевіряє конфігурацію та наявність таблиць D1.

401 — неправильні дані/відкликана сесія; 429 + Retry-After — ліміт;
503 — відмова сховища/конфігурації. Порожня конфігурація не видає доступ.
Немає signup, оплати, OTP, геоумов, reviewer detection або клієнтського master code.
Максимум JSON — 16 KiB, набір полів строгий, redirects у клієнті вимкнені.
Паролі/токени передаються лише в JSON body по HTTPS; код не журналює запити чи секрети.
Invocation logging вимкнено, query strings редагуються; CPU береться з Analytics API.

D1 зберігає SHA256 випадкового 256-bit токена, покоління права і час refresh.
Сесія приймається до 90 днів без активності; щоденний cron 03:17 UTC видаляє старі
сесії й прострочені лічильники. Пароль не має строку дії. Sign out стирає токен
локально, не є серверним відкликанням викраденої копії. Зміна verifier/pepper
або області права змінює generation; старі сесії відхиляються на наступній звірці.
Чинна offline lease клієнта може залишатися до завершення її 24 годин.

Ліміти атомарні через D1 UPSERT RETURNING, спільні між isolates і redeploy:
8 sign-in/10 хв на `CF-Connecting-IP`, 200 sign-in/10 хв загалом,
120 refresh/10 хв на адресу. У D1 — domain-separated HMAC адреси, не raw IP.
Лічильник перестає діяти за 10 хв; фізичне видалення HMAC-рядка — наступним
щоденним cleanup, до приблизно 24 годин після expiry. Cloudflare ingress окремо
бачить мережеві метадані. Process-local fallback при відмові D1 немає.

## Керування на цьому Windows-хості

Каталог: `C:\Users\yelyzaveta.khodos.TP-GE26-20\Documents\GitHub\quiet-camp\Server\cloudflare`.
Wrangler зафіксовано на 4.152.0. Вхід відбувся через браузер із потрібними
Workers/D1/account/user scopes, без прав на пошту. Токени в чат не передаються.

```powershell
npm.cmd ci
npx.cmd wrangler login --scopes account:read user:read workers:write workers_scripts:write workers_tail:read d1:write
python -X utf8 ops.py preflight
npx.cmd wrangler d1 migrations apply quietcamp-access --remote
npx.cmd wrangler deploy
python -X utf8 ops.py metrics --start 2026-10-23T07:55:27Z
```

Wrangler 4.152.0 не приймає `jurisdiction` у D1 binding і не має прапорця
`--jurisdiction` для info/migrations apply. Він адресує існуючу базу за UUID;
її immutable jurisdiction повертається API. Тому `wrangler.jsonc` містить саме
цей UUID, а **ops.py preflight обов'язково перевіряє `eu`, ім'я та replication**
перед змінами. Не створювати нову базу, не підставляти непідтримувані прапорці,
не змінювати location/jurisdiction чи тариф. Наявний Worker також перевіряється
до deploy; не користуватися temporary accounts або автоматичним provisioning.

Для secrets використано приватний каталог поза Git:
`~/.codex/private/quietcamp-play-review-20261023/`.

- `play-console-access.private.json` — ім'я/пароль для Anmeldedaten, не для Git.
- `server-account.private.env` — оригінальний PBKDF2 proof для reference/provisioning.
- `cloudflare-worker-secrets.private.json` — лише `QC_ACCESS_ACCOUNT` і
  `QC_ACCESS_PEPPER`, установлені через `wrangler secret bulk <private-file>`.
- Probe save/session files містять bearer token і також залишаються приватними.

Звичайний redeploy не потребує повторного provisioning чи зміни secrets.
`cloudflare/provision.py --credentials <private-json> --output <private-secrets>`
ідемпотентний; не допускає прихованої ротації й забороняє приватні файли в repo.
Зміни облікових даних слід узгоджено оновлювати в Worker і Play Console.

## Перевірено через справжній HTTPS

- Health 200; правильний пароль 200, неправильний 32-символьний пароль 401.
- Повторний вхід, refresh, C# CampEconomy Pro і право на весь опублікований контент.
- Серіалізація/відновлення C# save та refresh після перезапуску клієнта.
- 429 після вичерпання 8 спроб; після redeploy той самий ліміт лишається.
- Сесія, видана попередньою версією Worker, після redeploy поновлюється — 200.
- Після природного завершення rate window той самий акаунт знову входить — 200,
  також із UnityPlayer User-Agent. Лічильники для цього вручну не обнулялися.
- Контрольована помилка D1: нова таблиця сесій тимчасово перейменована, refresh
  дав 503 без подробиць; C# зберіг лише чинну lease, не подовжив прострочену й
  не видав нову. Таблицю відновлено у finally; кількість сесій збережена, refresh 200.
- Сім workerd/D1 тестів, включно з конкурентним лімітом і retention; C# privacy/access
  тести та owner validation. Точний Android-залишок — PLAY-REVIEW-UA.md.
- Реальне серверне право через C# access authority відкриває 100 рівнів поточного
  resource catalog при нульовому прогресі; published journey у ньому зараз одна —
  main. Окремі чернеткові journeys не відкриваються; прогрес/косметика не підроблені.

Cloudflare edge повертав 1010/403 стандартному `Python-urllib` User-Agent.
Звичайний probe User-Agent, Node fetch, C# HttpClient і зразок UnityPlayer UA
проходять. Код Worker не розрізняє ці клієнти. Це не підтвердження роботи
фактичного UnityWebRequest на Android: її ще потрібно перевірити.

Під час початкового розгортання сайт не змінювався. Окреме оновлення політики
від 23.10.2026 описує розгорнутий доступ у en/uk/de й ресурсах гри:
`2026-10-23.public-policy-4-cloudflare`.
[Джерела й межі перевірки](../docs/privacy-cloudflare-2026-10-23.md).
Правова оцінка договорів і реальні Android-перевірки залишаються окремими;
review flags не піднімалися.

Джерела: [Workers limits](https://developers.cloudflare.com/workers/platform/limits/),
[WebCrypto](https://developers.cloudflare.com/workers/runtime-apis/web-crypto/),
[D1 data location](https://developers.cloudflare.com/d1/configuration/data-location/),
[Workers metrics API](https://developers.cloudflare.com/analytics/graphql-api/tutorials/querying-workers-metrics/).
