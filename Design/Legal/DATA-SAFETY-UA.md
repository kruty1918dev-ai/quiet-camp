# Інвентар даних і проєкт Data Safety

Оновлено 2026-10-22. Це робоча декларація для перевірки власником, не подана форма
Play Console і не юридичний допуск. Публічна та bundled політики лишаються
перевірочними, доки власник не підтвердить актуальний текст і дані консолей.

| Дані | Де й навіщо | Умови / очищення / строк |
|---|---|---|
| Прогрес, puzzle session, налаштування, wallet, cosmetic/journey entitlements | Локальні save-модулі; гра, нагороди й відновлення | Не надсилаються як save payload; local erase; реальні OS backup/D2D та виключення перевірити окремо |
| Вік, purpose choices, policy revision/hash/language, crash-session eligibility, Google privacy-options flag | Локальні privacy receipts; не містять DOB чи документів | Зміна віку/ревізії й помилка запису відкликають дозволи; noBackup install marker не передається |
| Pending earned reward receipt і applied receipt hashes | Локальна економіка, одноразова видача й crash recovery | Запис у native earned callback; очищення pending разом із grant; applied IDs зберігаються для deduplication |
| Analytics custom events | Firebase/Google: рівень, результат, числові категорії дій, діапазони performance, fixed feedback, ad/purchase funnel | Окрема згода, policy/SDK review; для підлітків окрема перевірка. Unknown/Under13 вимкнені. Withdrawal зупиняє й запитує local reset; uploaded deletion — окремий процес |
| Firebase automatic events / identifiers / device metadata | Firebase SDK може додавати власні installation/app/device дані; це не тільки наші numeric events | Default-off; перевірити точні native файли й трафік shared Core навіть для іншого purpose. Retention задається у консолі та політиці |
| Crash stacks, fixed failure category, version/mode/level, device/install diagnostic metadata | Crashlytics/Google: технічні збої | Окреме crash рішення; не залежить від Analytics. Automatic upload false. Prior/current permissions для старих звітів; delete/send completion перевірити native |
| UMP/AdMob choices, ad requests/impressions, SDK identifiers і metadata | Google: реклама, privacy options, fraud prevention та рекламний дохід | Свіжий UMP і purpose/age gates; adults personalization окремо. Child/limited modes потребують окремих перевірок; NPA не означає відсутність збору |
| Product ID, Play purchase token, purchase state | Google Play + власний verification backend: Pro, pending, ack, restore/refund | Покупки незалежні від Analytics. Token іде по TLS; native history у приватних prefs для повторної перевірки, не Analytics/logs/owner JSON |
| SHA-256 purchase token, product, state, checked time | Backend SQLite: authoritative entitlement/revocation ledger | Сирі tokens сервер не зберігає. Захищений диск/backup; retention і data-rights процес визначає власник за обліковими обов'язками |
| Pro lease expiry | Локальний save, максимум 24 години від відповіді сервера | Перевірка на старті, поверненні та кожні 15 хв; після expiry потрібна мережа. Не стирає ресурси |
| Operational day/name/bucket/count | Окремий backend, агрегати до 30 днів | Вимкнено за замовчуванням з обох боків. Потрібна підтверджена незалежна підстава; не можна вмикати обробку, яка потребує відсутньої parental authorization |
| IP, TLS й ingress metadata | Мережа/оператор хостингу | Не називати механізм анонімним. Вимкнути body/token/IP access logs для metrics; перевірити хостинг, регіон і строки |

## Проєкт відповідей Play Console

- **App activity / interactions:** можливе збирання через Firebase Analytics,
  ad lifecycle і purchase funnel. Позначення optional залежить від фінального
  consent-flow; це треба звірити з фактичним release-конфігом.
- **App info and performance:** Crashlytics crash logs/diagnostics та performance
  analytics; незалежні вибори. Не стверджуйте «збору немає» лише через false defaults.
- **Device or other IDs:** можливі Firebase/Google Ads/Play SDK identifiers.
  Точні ID та режими child/LTD визначаються перевіркою SDK і Android-трафіку.
- **Financial info / purchase history:** product, purchase token/state і право Pro
  обробляються Play та verification backend. Платіжні картки/банківські реквізити
  гра й цей сервер не збирають; оплата відбувається в Google Play.
- **Personal info / accounts:** власного player account немає; parent identity
  і DOB не збираються. Вікова категорія лишається локально, не є доказом особи.
- **Sharing / purposes:** звірте роль Google та хостингу, advertising/analytics,
  diagnostics, app functionality і fraud/security з актуальними SDK disclosures
  та умовами. Не класифікуйте всіх постачальників автоматично як service providers.
- **Encryption in transit:** власні endpoints — HTTPS, redirect заборонено;
  перевірити release network capture і SDK transport. Криптографічної обіцянки
  щодо модифікованого клієнта немає.
- **Deletion:** local erase не видаляє uploaded Google/backend дані. Публічний
  data-request route має мати реальний процес перевірки/виконання звернень.
  Якщо з'являться accounts, потрібен окремий in-app і web account-deletion шлях.

Фінальні checkbox-відповіді, retention, країни, Families декларація та підстава
дитячої обробки належать власнику після перевірки release-конфігу, консолей,
політики й пристрою. Заповнення owner JSON не підтверджує жодного з них.

Див. [поточні механізми й Android-список](PRIVACY-RUNTIME-UA.md),
[owner setup](../../Config/README-UA.md), [backend](../../Server/README-UA.md).
