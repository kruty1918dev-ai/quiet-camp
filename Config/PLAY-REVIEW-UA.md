# Доступ Google Play Review — ще не підключений

**Не подавати як робочу інструкцію.** Backend не розгорнуто, серверні secrets
не встановлені, `complimentaryAccess.enabled=false`, endpoint порожній.
Придатна Android-збірка та перевірка на пристрої відсутні. Локальні HTTP-тести
і компіляція джерел не означають готовності для Google Review.

## Хостинг

Підготовлено функцію для чинного Vercel-сайту з постійним Redis-сховищем.
[Server/VERCEL-UA.md](../Server/VERCEL-UA.md) містить проєкти, вісім змінних,
точні дії в консолях, команди перенесення коду та production-перевірок.
Потрібен доступ до Vercel акаунта власника: тариф і Free Redis наразі не
підтверджені. Hobby не підходить для комерційного Quiet Camp; upgrade не робити.
Платних ресурсів, нового сайту, DNS і пошти не створено й не змінено.
Запланована адреса `/v1/access` на чинному домені ще не є робочим сервісом.

## Реалізований інтерфейс

У головному меню: **Settings → English → Complimentary Pro access**.
Останній пункт відкриває панель **Hints and lives**, унизу якої розділ доступу.
Поки owner config вимкнений, показано **Complimentary access is not connected
yet.** Форми немає. Після підключення з'являться **Username**, **Password** і
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

Сервер перевіряє PBKDF2-хеш і зберігає лише хеші випадкових токенів.
Lease — 24 години, refresh на запуску, поверненні й кожні 15 хвилин.
CampEconomy зберігає токен, endpoint та додаткове право; після перезапуску
відновлює лише чинну lease. Без мережі право не подовжується. Вихід або
завершення lease знімає complimentary права, не стираючи окрему справжню покупку.
Неправильні дані — 401, сховище недоступне — 503, ліміт — 429 із Retry-After.
Сесії й ліміти постійні та спільні між workers/перезапусками; деталі — VERCEL-UA.md.

## Залишок перевірок

1. Дозволений без додаткової оплати хостинг, Vercel secrets і Free Redis;
   реальне HTTPS-розгортання та підключення owner config.
2. Публічні правильний/неправильний вхід, повторний вхід, refresh, redeploy зі
   збереженою сесією, 429, відмова Redis/сервера та відновлення доступності.
3. На Android після окремого дозволу на збірку: форма/клавіатура, маскування
   й очищення пароля, запуск довільного пізнього рівня без прогресу, gameplay
   та Album, Pro-життя/підказки, відсутність рекламних запитів, незалежні згоди,
   cold start/force-stop, offline до/після lease, очищення даних і повторний вхід,
   sign out, повернення мережі. Billing/rewarded ads потребують окремих тестів.
4. Звірити фактичний Android-інтерфейс із 451 символом, лише тоді подати
   актуальні приватні дані Google та позначити доступ готовим.

Джерело: [Google Play — Requirements for providing sign in details for review](https://support.google.com/googleplay/android-developer/answer/15748846?hl=en).
