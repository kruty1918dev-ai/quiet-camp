# Налаштування власника Quiet Camp

Працюйте з кореня репозиторію, Python 3.10+. `owner.local.json` — поза Unity Assets,
виключений із Git. Створіть його копію з `owner.example.json`, якщо файлу ще немає.
Схема редактора — `owner.schema.json`; CLI додатково перевіряє семантику.

```powershell
Copy-Item Config/owner.example.json Config/owner.local.json # тільки якщо local відсутній
python tools/owner_config.py validate
python tools/owner_config.py readiness
python tools/owner_config.py prepare
# Перегляньте перелік файлів та хеші в Temp/owner-preview.json.
python tools/owner_config.py apply
git diff
```

`validate` не змінює проєкт. `prepare` записує лише попередній перегляд із хешами,
без приватних значень. `apply` звіряє конфігурацію й усі поточні файли з переглядом;
застарілий перегляд відхиляється. Запис кожного файла атомарний, але весь набір
не є транзакцією: після збою повторіть prepare та перевірте diff. Ручні поля
наявних legal/google JSON зберігаються; перелічені в конфігурації поля замінюються.
Не запускається Unity, компілятор Android, сервер, публікація чи DNS.

Порожні SDK-поля вимикають відповідний сервіс. Відсутність Firebase видаляє
його нативний `Assets/google-services.json` лише після перегляду. Відсутність AdMob
App ID прибирає ранній MobileAdsInitProvider і очищує ID в SDK settings.
Після заповнення повертається стандартний SDK provider. FirebaseInitProvider
лишається виключеним; збір вимкнено в Android manifest до згоди.
Згенерований `owner_client.json` містить лише дозволені клієнтські поля.
Сам `owner.local.json` ніколи не копіюється до Assets. Наявні Resources-конфігурації
стають підготовленим клієнтським результатом, а не місцем для повторного введення ID.

## Поле → консоль → місце отримання

Порожній рядок або `null` означає відсутність. Структура й ключі обов'язкові.

| Поле | Де взяти / формат | Потрібність і наслідок відсутності |
|---|---|---|
| schemaVersion | Шаблон: `1` | Обов'язкове; інші версії відхиляються |
| mode | Власник: `development` / `production` | Обов'язкове; production відхиляє Google sample IDs |
| android.packageName | Play Console → додаток; Firebase → Project settings → Android app; `com.quietcamp.game` | Обов'язкове; має збігатися з Firebase client |
| admob.appId | AdMob → Apps → App settings; `ca-app-pub-` + 16 цифр + `~` + 10 цифр | Для реклами; порожнє вимикає рекламу й ранній provider |
| admob.rewardedLifeUnitId | AdMob → Apps → Ad units → Rewarded; `ca-app-pub-…/…` | Для відео за життя; порожнє вимикає це розміщення |
| admob.rewardedHintUnitId | Окремий Rewarded unit тієї самої програми; формат `/` | Для відео за підказку; порожнє вимикає це розміщення |
| admob.interstitialUnitId | AdMob → Ad units → Interstitial; формат `/` | Для міжрівневої реклами; порожнє вимикає її |
| admob.publisherId | AdMob → Settings → Account information; `pub-` + 16 цифр | Для app-ads.txt на сайті; CLI не публікує сайт |
| firebase.projectId | Firebase → Project settings → General → Project ID; `quiet-camp` | Для Firebase; без документа Firebase вимкнений |
| firebase.androidGoogleServices | Там само → Your apps → Android → Download google-services.json | Вставте весь JSON-об'єкт; `null` вимикає Analytics і Crashlytics; client має відповідати пакету |
| billing.proProductId | Play Console → Monetize → Products → One-time products; `quietcamp_pro` | Для Pro; без опублікованого товару немає ціни/покупки |
| billing.verificationEndpoint | Власний HTTPS backend, наприклад `https://api.your-domain.de/v1/purchases/verify` | Без endpoint покупки вимкнені; сервер треба розгорнути окремо |
| operationalMetrics.endpoint | Власний HTTPS backend `/v1/metrics` | Порожнє вимикає; правова підстава перевіряється окремо |
| legal.privacyPolicyUrl | Опублікована HTML-політика на вашому домені | Без неї production-сервіси не допускаються |
| legal.supportUrl | Опублікована сторінка підтримки HTTPS | Необов'язкове посилання |
| legal.termsUrl | Опубліковані умови Pro, HTTPS | Потрібне для покупок |
| legal.publishedPolicyRevision | Власна версія опублікованого тексту; `2026-10-22.1` | Без неї публікація не підтверджується; синхронізуйте bundled uk/en/de через наявний tools/sync_privacy_policy.py |
| legal.publisherName | Play Console → Developer account, ім'я контролера | Для публікації політики |
| legal.supportEmail | Контрольована адреса підтримки | Для публікації політики |
| legal.privacyEmail | Контрольована адреса privacy-запитів | Заповніть для обробки звернень |
| legal.dataRequestUrl | Ваша HTTPS-форма/сторінка прав на дані | Для онлайн-сервісів; реальний процес потрібен окремо |
| legal.publishedOn | Дата фактичної публікації `YYYY-MM-DD` | Без неї політика не вважається опублікованою |
| legal.minimumAge | Рішення власника про аудиторію, ціле `0..18` | Не є згодою чи підтвердженням права на збір |
| reviews.policyPublished | Підтверджена публікація власником, `true/false` | За замовчуванням false: policy draft |
| reviews.sdkDisclosures | Перевірка SDK, data inventory, Data Safety | false блокує допуск SDK-сервісів |
| reviews.teenPrivacy | Перевірка правил для підлітків і країн | false обмежує підліткові сервіси |
| reviews.limitedAds | Перевірка LTD/storage, AdMob налаштування | false вимикає Limited Ads |
| reviews.underAgeAds | Окрема перевірка обмеженої реклами 13–15 | false вимикає цей шлях |
| reviews.childAds | Окрема правова/SDK Families перевірка | false вимикає дитячу рекламу |
| reviews.childAdFormatsAndroid | Фактичні Android-креативи, закриття, ідентифікатори | Не можна підтвердити portable-тестом; потрібне для childAds |
| reviews.adultPersonalization | Перевірка персоналізації дорослим | false: тільки неперсоналізований запит; UMP лишається обов'язковим |
| reviews.operationalMetricsLegalBasis | Підтверджена підстава для мінімальної статистики | false забороняє endpoint, включно з дитячим режимом |
| reviews.billingTerms | Перевірка умов покупок і підтримки | false вимикає production billing |
| monetization.firstInterstitialAfterCompletedLevels | Рішення власника, `5` рівнів | Обов'язкове; мінімум 0 |
| monetization.completedLevelsBetweenInterstitials | `4` завершені рівні | Обов'язкове; мінімум 1 |
| monetization.minimumInterstitialIntervalSeconds | `180` секунд | Обов'язковий незалежний часовий ліміт |
| monetization.interstitialCooldownAfterRewardSeconds | `180` секунд | Пауза після винагороди |
| monetization.maximumInterstitialsPerSession | `3` | Обмеження сесії; `0` вимикає interstitial |

Не вставляйте service-account JSON, private keys, паролі, purchase tokens,
Play signing/upload keys. Firebase client API key не є серверним секретом;
обмежте його у Google Cloud за Android package/SHA та потрібними API.
Використовуйте окрему службову ідентичність backend; не AES із ключем у грі.

Заповнений JSON **не є** юридичним допуском або Android-перевіркою. `readiness`
показує налаштування та відсутні докази окремо для кожного сервісу.
