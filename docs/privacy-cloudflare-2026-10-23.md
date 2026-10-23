# Політика після розгортання Cloudflare — 23.10.2026

Ревізія `2026-10-23.public-policy-4-cloudflare` синхронізована з канонічного
`legal/quiet-camp/content.json` репозиторію `portfolio-oleksii-ha` у повний
вбудований документ en/uk/de. Адреса політики:
https://oleksiy-havryliuk.de/quiet-camp/privacy/uk.

Стан публікації в цій сесії: локально закомічено, але не відправлено в main.
Git push отримав HTTP 403, а GitHub connector — `Resource not accessible by integration`.
Наявність файлів у worktree не підтверджує оновлення публічного сайту.

Оновлено опис розгорнутого complimentary Pro API, Cloudflare Workers і D1 у ЄС,
передавання виданих даних входу й токенів, псевдонімних HMAC-ключів лімітів,
мережевих даних, міжнародної обробки й видалення. Платна верифікація Google Play
залишається окремою непідключеною можливістю.

Джерела: game main `89c3728f`, зокрема `Server/cloudflare/src/index.mjs`,
`0001_complimentary.sql`, `wrangler.jsonc`, transport/service access,
`CampEconomy.cs` та `GameServices.cs`; site main до оновлення — `53fb79a`.
Право діє 24 години; сесія приймається до 90 днів без успішного поновлення;
ліміт триває 10 хвилин, а фізичне видалення старих рядків виконує щоденне очищення.
Історія D1 Time Travel на Free може зберігати попередні стани до 7 днів.
Sign out прибирає локальні дані, але не відкликає серверний токен і не стирає
серверний рядок. Ротація/відкликання виданого доступу — окрема операція.
Налаштування EU D1 не гарантує обробки виключно в ЄС усіма сервісами Cloudflare.

`policyIsDraft`, `draft` і review flags залишено без змін. Вік, незалежні згоди,
реклама, Analytics/Crashlytics, покупки та серверна логіка не змінені.
APK/AAB не створювалися. Ця робота не підтверджує Android, договори провайдерів
або готовність до Google Play Review.

Перевірки: повний збіг усіх 14 секцій у трьох мовах із сайтом, owner validation,
10 owner-тестів; на сайті — build, перевірки маршрутів/посилань і release fixtures.
Фокусований C# probe: 143/143 пройшли на .NET 10.0.13 із `DOTNET_ROLL_FORWARD=Major`
(проєкт net8.0, SDK 10.0.402, без Unity). Ці платформні підміни не перевіряють
UnityWebRequest на телефоні.

Офіційні джерела:
[D1 location](https://developers.cloudflare.com/d1/configuration/data-location/),
[Time Travel](https://developers.cloudflare.com/d1/reference/time-travel/),
[Cloudflare DPA](https://www.cloudflare.com/cloudflare-customer-dpa/),
[Cloudflare privacy](https://www.cloudflare.com/privacypolicy/),
[Google Play Data safety](https://support.google.com/googleplay/android-developer/answer/10787469?hl=en).
