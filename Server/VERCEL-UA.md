# Complimentary access на чинному Vercel

## Поточний стан

**Не розгорнуто і не підключено.** Пакет підготовлений у цьому репозиторії;
код функції ще не перенесений у сайт. Production secrets не встановлені.
`Config/owner.local.json` лишається `enabled=false`, endpoint порожній.
Публічного підтвердженого URL сервісу немає. Android не збирався.

GitHub показує дві успішні Vercel-інтеграції сайту:

- `oleksiy-havryliuk / portfolio-oleksii-ha`;
- `kruty1918dev-ais-projects / project-u8d1i`.

Це підтверджує автоматичне розгортання сайту з main, але не тариф, доступ
до environment variables або належність production-домену конкретному проєкту.
Локального Vercel CLI login/token, `.vercel/project.json` чи підключеного
Vercel app під час перевірки немає. Після тимчасової помилки зовнішніх
TLS-з'єднань зв'язок відновився; запит запланованого HTTPS health повертає 404.

## Конкретний варіант

Додати одну Python Function до **наявного репозиторію сайту**
`portfolio-oleksii-ha`, без нового сайту, домену, DNS або пошти.
Канонічний код — `Server/` гри; `prepare_vercel.py` створює пакет без секретів,
`install_vercel_overlay.py` переносить його та додає маршрут у чинний генератор.
Billing і metrics у цій функції не відкриваються; Google Play ADC не потрібні.

**Запланований, ще не робочий endpoint:**
`https://oleksiy-havryliuk.de/v1/access`.
Методи: `POST /sign-in`, `POST /refresh`, `GET /health`;
`GET /maintenance` — лише для захищеного щоденного Vercel Cron.
Health перевіряє конфігурацію, а не доступність Redis чи правильність пароля;
лише реальні auth-запити можуть підтвердити працездатність.

Сесії та ліміти — у Redis через Upstash REST. SQLite лишається для звичайного
WSGI-хостингу з постійним диском; `/tmp` Vercel для цього непридатний.
Обирати лише **Free**, без Pay as you go, trial із подальшим списанням або
платних доповнень. Якщо Free недоступний — зупинитися, нічого не купувати.
Параметри Free та залишок квоти потрібно перевірити у чинному акаунті.
Публічний Free включає близько 250 MB і 500K commands/month за $0;
вирішальне значення має консоль. За неактивності базу можуть архівувати.
Перевищення безкоштовної квоти має зупиняти доступ, а не створювати витрати.

Друга умова: Vercel-проєкт уже має допускати комерційне використання без
нової оплати. **Hobby для комерційного Quiet Camp не підходить.** Не виконувати
upgrade. Якщо зараз лише Hobby, потрібен вибір іншого безкоштовного хостингу
власником; цей варіант не слід видавати за допустиме production-розгортання.
Для чинного сумісного тарифу перевірити залишок включеного використання та
налаштований hard stop витрат. Не змінювати спільні billing settings без
перевірки впливу на інші проєкти.

## Точні дії в консолях власника

1. У Vercel вибрати team → project → **Settings → Domains**. Знайти проєкт,
   який уже обслуговує `oleksiy-havryliuk.de`; не додавати й не переносити домен.
   У **Settings → Git** перевірити `kruty1918dev-ai/portfolio-oleksii-ha`, main.
   У team **Settings → Billing** перевірити чинний тариф, включені квоти й
   обмеження витрат. Не починати trial, upgrade або створення платного ресурсу.
2. Підключити Vercel app до цього акаунта/проєкту або виконати авторизований
   `vercel login` локально. Не передавати токен чи пароль у чат.
3. У чинному Upstash акаунті відкрити **Redis → Create Database**: ім'я
   `quietcamp-access`, доступний EU region, план **Free**. Перед Create ще раз
   перевірити, що немає оплати/автопереходу на платний план. Можна використати
   вже наявну Free базу за згодою власника; ключі мають префікс `qc:access:`.
   На сторінці бази скопіювати REST URL та REST Token лише у Vercel.
   Не використовувати anonymous/start-redis базу без claim: вона видаляється
   через 3 дні й не годиться для Google Review.
4. У Vercel production-проєкті **Settings → Environment Variables**, environment
   **Production**, додати наведені нижче значення. Пароль у Vercel не потрібний;
   хеш і Redis token позначити **Sensitive**. Не встановлювати їх у клієнтський
   owner JSON, Assets, GitHub repo variables чи публічний JS.

| Змінна | Значення / приватне джерело |
|---|---|
| `QC_ANDROID_PACKAGE` | `com.quietcamp.game` |
| `QC_PRO_PRODUCT` | `quietcamp_pro` |
| `QC_COMPLIMENTARY_USERNAME` | `play-review`, із приватного server-account.private.env |
| `QC_COMPLIMENTARY_PASSWORD_HASH` | Повне значення з того ж файла; зберегти всі символи `$` |
| `QC_COMPLIMENTARY_EXPLORE_PUBLISHED` | `true` — явне право на весь опублікований контент |
| `UPSTASH_REDIS_REST_URL` | HTTPS REST URL саме вибраної Free бази |
| `UPSTASH_REDIS_REST_TOKEN` | REST Token цієї бази, серверний секрет |
| `CRON_SECRET` | Випадковий секрет із server-account.private.env; також Sensitive |

Приватний каталог поточного облікового запису:
`~/.codex/private/quietcamp-play-review-20261023/`.
`play-console-access.private.json` містить пароль для Play Console;
`server-account.private.env` — тільки серверний хеш і параметри права.
Зберегти незмінні дані на час перевірки; не створювати новий пароль при redeploy.

5. Перевірити, що ingress/runtime/log drains не записують тіла запитів,
   Authorization, passwords чи session tokens. Python handler сам нічого
   такого не журналює. Production API має бути доступний без Vercel login,
   геоблокування, challenge або OTP. Не послаблювати захист інших проєктів.
   Функція бере peer IP лише з Vercel-managed `x-vercel-forwarded-for`;
   відсутній/некоректний заголовок дає 503. Це обов'язково перевірити на Vercel.
6. Після підтвердження умов хостингу виконати з кореня гри:

```powershell
python -X utf8 Server/prepare_vercel.py --output Temp/vercel-access
python -X utf8 Server/install_vercel_overlay.py --site-root ../portfolio-oleksii-ha
# Переглянути dry run, потім встановити лише вихідний код:
python -X utf8 Server/install_vercel_overlay.py --site-root ../portfolio-oleksii-ha --apply
```

У репозиторії сайту:

```powershell
npm.cmd run routes
npm.cmd test
npm.cmd run build
git diff --check
git diff -- scripts/build.mjs vercel.json
git add scripts/build.mjs vercel.json api/quietcamp-access.py quietcamp_backend
git commit -m "Add Quiet Camp complimentary access function"
git push origin main
```

Обидві чинні Git-інтеграції можуть розгорнути код. Секрети встановлювати лише
у визначеному production-проєкті; на іншому функція без них поверне 503.
Не створювати нових Vercel projects. Після Ready перевірити HTTPS health,
реальний вхід, неправильні дані, refresh, повторний вхід і 429. Тест ліміту
виконувати окремо від позитивного сценарію: після 8 спроб на одній адресі
потрібно зачекати до 10 хвилин. Ліміт не блокує сам обліковий запис назавжди.

У **Settings → Cron Jobs** перевірити щоденний `/v1/access/maintenance`
о 05:00 UTC (`0 5 * * *`). Vercel додає `Authorization: Bearer CRON_SECRET`;
звичайний запит без нього має давати 401. З **Run** перевірити 200 і факт
запису `qc:access:heartbeat` у Redis; відмова Redis має дати 503. Не логувати
Authorization. Cron не видає права гравцям і не продовжує їхні сесії.
Щоденний запис підтримує активність Free бази. Підключити наявний моніторинг
збоїв cron: одна лише health-сторінка цього не робить.

## Підключення owner config після розгортання

До публічної перевірки не генерувати активний клієнтський конфіг.
Створити в ігнорованому `Temp/ai/` копію owner.local з
`complimentaryAccess.enabled=true` і **фактично перевіреною** HTTPS-адресою.
Для тесту вказати її через `QC_REVIEW_PROBE_OWNER_CONFIG`, endpoint через
`QC_REVIEW_PROBE_ENDPOINT`, шлях приватного JSON через
`QC_REVIEW_PROBE_CREDENTIALS`. Усі три — лише змінні поточної PowerShell-сесії.

```powershell
dotnet run --project tools/privacy-probe/Probe.csproj -p:UseSharedCompilation=false -- --complimentary-http-probe
```

Probe перевіряє правильні/неправильні дані, refresh, повторний вхід,
серіалізацію/відновлення C# збереження та вихід через публічний HTTPS.
Він не перевіряє UnityWebRequest або Android. Окремо перевірити redeploy
функції зі збереженою Redis-сесією, відмову Redis, недоступність сервера та
закінчення 24-годинного права без мережі. Не вважати ці кроки виконаними
на підставі локальних SQLite-тестів.

Після успіху перенести перевірені public values у `Config/owner.local.json`,
синхронізувати фактичний опис обробки даних на сайті/в bundled policy,
потім виконати:

```powershell
python -X utf8 tools/owner_config.py validate --config Config/owner.local.json
python -X utf8 tools/owner_config.py prepare --config Config/owner.local.json --preview Temp/owner-preview.json
python -X utf8 tools/owner_config.py apply --config Config/owner.local.json --preview Temp/owner-preview.json
python -X utf8 tools/owner_config.py readiness --config Config/owner.local.json
```

Prepare/apply не збирає гру. Android UI, клавіатуру, старт рівнів і відновлення
права на придатній збірці перевірити лише після окремого дозволу на збірку.

## Дані й обмеження

PBKDF2-SHA256, 600000 ітерацій, окрема 128-bit сіль. Випадкові токени 256 bit;
сервер зберігає лише SHA256 токена та покоління права. Redis sessions TTL 90 днів
поновлюється на refresh; пароль не має штучного строку дії. Клієнтська lease —
24 години. Зміна хешу/права відкликає старі сесії при наступній звірці.
Вихід видаляє токен локально; це не серверне відкликання викраденого токена.

Durable rate limit: 8 sign-in / 10 хв на адресу, 200 sign-in / 10 хв загалом;
refresh — 120 / 10 хв на адресу. IP зберігається тільки як HMAC-ключ ліміту
із TTL 10 хв; ingress провайдера окремо бачить мережеві метадані.
Redis недоступний або не налаштований → 503, ліміт → 429, помилка даних → 401.
Немає process-local fallback, який обходив би ліміт на іншому worker.

Джерела: [Vercel Hobby](https://vercel.com/docs/plans/hobby),
[Python Functions](https://vercel.com/docs/functions/runtimes/python),
[request headers](https://vercel.com/docs/headers/request-headers),
[Vercel Cron](https://vercel.com/docs/cron-jobs/manage-cron-jobs),
[Upstash Redis pricing](https://upstash.com/docs/redis/overall/pricing),
[Free inactivity](https://upstash.com/docs/redis/help/faq).
Тариф, регіон, безкоштовну квоту й доступність керованого заголовка потрібно
підтвердити в чинному акаунті; наявність GitHub deploy не підтверджує їх.
