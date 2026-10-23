# Доступ Google Play Review — HTTPS працює, Android ще не перевірений

Backend розгорнуто та перевірено через публічний HTTPS:
`https://quietcamp-access.oleksiy-havryliuk.workers.dev/v1/access`.
Secrets встановлено в Worker; owner config і згенерований клієнтський snapshot
містять перевірений endpoint та `enabled=true`. **Для Google Review ще не готово:**
придатної Android-збірки й перевірки UI/нативної мережі немає. Гру не збирали.

## Хостинг

Використано надані Worker `quietcamp-access` і D1 `quietcamp-access`,
UUID `f8e41926-e58f-4306-8a89-9b0616b367fc`, jurisdiction `eu`, replication disabled.
[Server/CLOUDFLARE-UA.md](../Server/CLOUDFLARE-UA.md) містить міграції,
Wrangler/browser login, secrets, обґрунтування HMAC та виміряний Cloudflare CPU.
Vercel і Upstash не використовуються; стару підготовку видалено. Платних ресурсів,
дублікатів, trial/upgrade немає. Сайт, DNS, пошту й домен не змінювали.

## Реалізований інтерфейс

У головному меню: **Settings → English → Complimentary Pro access**.
Останній пункт відкриває панель **Hints and lives**, унизу якої розділ доступу.
Увімкнений owner config показує **Username**, **Password** і
**Sign in**. Пароль маскується; після відправлення обидва поля очищуються.

Активне право показує **Complimentary Pro is active.** і **Sign out**.
За додаткового права на контент тут же, у головному меню, є **Browse published
levels**, кнопки journeys, список до 20 рівнів та **More levels** / **Previous
levels**. Кнопка рівня викликає звичайний `qc.play` і канонічну перевірку доступу.
У панелі під час гри потрібно повернутися до головного меню для вибору рівня.
Це опис джерел, ще не підтверджений на Android; шлях і прокручування звірити
на придатній збірці перед поданням інструкції.

## Поля Anmeldedaten — підготовлені, не готові до подання

Назва англійською — 28 символів, ліміт 60:

```text
Quiet Camp - full Pro access
```

Облікові дані **потрібні**: надані username/password із приватного файла
`~/.codex/private/quietcamp-play-review-20261023/play-console-access.private.json`.
Пароль не міститься в Git, Assets чи документації. Це не Google account.
Ті самі дані допускають повторний вхід і кілька пристроїв без реєстрації,
оплати, OTP, геообмеження або звернення до розробника.

Англійська інструкція — 451 символ, ліміт 500 (**ще не подавати**):

```text
In Settings, choose English, then open Complimentary Pro access. Scroll to the sign-in form. Enter the supplied username and password and tap Sign in. No payment, registration or developer contact is required. Pro removes ads and limits on lives/hints. Browse published levels opens every released level and journey without completing earlier levels. Choose a journey, then a level; More levels shows the next page. Privacy choices remain independent.
```

Канонічний текст для приватного файла і перевірки довжини —
`Server/provision_complimentary_access.py`. Оновлення тексту не перевипускає
пароль. `readyForPlayConsole=false` лишається до Android-перевірки.

## Повнота доступу

- Звичайний Pro: без рекламних запитів, без лімітів життів і підказок;
  опубліковані journeys з `includedInPro`.
- Окреме явне серверне право `explorePublishedContent=true` відкриває всі
  **опубліковані** рівні й journeys без required completions, prerequisite
  levels, послідовності або окремої оплати journeys. Звичайний Pro сам цього
  додаткового права не надає.
- Видимий список у панелі доступу обходить навігаційне обмеження мапи її
  набором waystones. Немає reviewer detection чи прихованої підміни гри.
- Поточний main catalog: 100 рівнів — 10 authored IDs і 90 generated IDs.
  Реальне HTTPS-право перевірено C# access authority на всіх 100 ID з Resources
  при нульовому прогресі; зараз published journey одна — main.
  Lighthouse/garden/station/memories/mountain/resort/coast зараз не опубліковані
  й не є доступними платними journeys цього релізу. Їх не обіцяємо перевіряльнику.
  Право охоплює опублікований runtime catalog, включно з published content packs,
  але не редакторські та чернеткові сцени.

Це загальний механізм доступу для будь-кого з виданими власником даними,
без обов'язкової реєстрації звичайних гравців чи універсального коду Pro.
Вхід не створює фальшивих покупок, завершень, нагород або згод. Фактичне
проходження обраного рівня зберігається звичайно; для перевірки Album можна
завершити обраний рівень без проходження всіх попередніх.

## Збереження та захист

Worker перевіряє HMAC-SHA256 згенерованого 190-bit випадкового credential.
Pepper і verifier — Worker Secrets; у D1 лише хеші токенів. Початковий
PBKDF2 на 600000 ітерацій звірено при provisioning, він не виконується
на кожен запит Worker. Пароль лишився незмінним.
Lease — 24 години, refresh на запуску, поверненні й кожні 15 хвилин.
CampEconomy зберігає токен, endpoint та додаткове право; після перезапуску
відновлює лише чинну lease. Без мережі право не подовжується. Вихід або
завершення lease знімає complimentary права, не стираючи окрему справжню покупку.
Неправильні дані — 401, сховище недоступне — 503, ліміт — 429 із Retry-After.
Сесії й ліміти постійні та спільні між isolates/redeploy; деталі — CLOUDFLARE-UA.md.

## Залишок перевірок

1. Публічні правильний/неправильний вхід, повторний вхід, refresh, збереження
   сесії й 429 після redeploy, контрольована помилка D1 (503) та її відновлення
   вже перевірені. C# lease не подовжується при помилці й відновлюється після
   перезапуску. CPU Cloudflare: P50 2,757 мс, P99 6,764 мс, execution errors 0
   у тестовій вибірці; це не навантажувальна гарантія.
2. На Android після окремого дозволу на збірку: форма/клавіатура, маскування
   й очищення пароля, запуск довільного пізнього рівня без прогресу, gameplay
   та Album, Pro-життя/підказки, відсутність рекламних запитів, незалежні згоди,
   cold start/force-stop, offline до/після lease, очищення даних і повторний вхід,
   sign out, повернення мережі. Billing/rewarded ads потребують окремих тестів.
3. Звірити фактичний Android-інтерфейс із 451 символом, лише тоді подати
   актуальні приватні дані Google та позначити доступ готовим.
4. Окреме оновлення від 23.10.2026 синхронізує політику en/uk/de із розгорнутим
   Cloudflare Workers + EU D1: `2026-10-23.public-policy-4-cloudflare`.
   [Джерела й межі перевірки](../docs/privacy-cloudflare-2026-10-23.md).
   Це не підтверджує Android або договори постачальників; юридичні review flags
   залишаються false.

Джерело: [Google Play — Requirements for providing sign in details for review](https://support.google.com/googleplay/android-developer/answer/15748846?hl=en).
