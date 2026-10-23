# Privacy Policy та сайт Quiet Camp

Поточні runtime-правила й підготовка монетизації від 2026-10-22 описані в
[Design/Legal/PRIVACY-RUNTIME-UA.md](../Design/Legal/PRIVACY-RUNTIME-UA.md).
Наведені нижче відомості про редакцію privacy-7 є історією підготовки сайту,
а не підтвердженням нових вікових правил або дозволом на публікацію.
Для конфігурації використовуйте [owner JSON](../Config/README-UA.md).

Канонічний URL: https://oleksiy-havryliuk.de/quiet-camp/privacy; `/uk` і `/de` — повні локалізації. Website repo: `kruty1918dev-ai/portfolio-oleksii-ha`; Unity docs GitHub Pages залишається окремим сайтом. Custom domain не переноситься на Pages.

Поточна підготовлена редакція: `2026-10-21.privacy-7-revenue`, базаmain030e76d. Основна аудиторія13+ з безпечним локальним fallback для молодших/невідомого віку. Це кандидат, не native Android clearance. 16+ мають незалежні optional choices;13–15 мають окремо підготовлений, не активований LTD шлях. Читання політики не є consent. Виробничі IDs/UMP/GA4/Play target groups власником ще не перевірені.

[Реалізація](../reports/monetization-review/IMPLEMENTATION-UA.md), [аудит монетизації](../reports/monetization-review/AUDIT-UA.md). Families18+ / instant-free кандидат відкликано; історичні звіти не є поточними вимогами. Поточний website inventory/DataSafety/checklist — `reports/revenue-privacy` репозиторію сайту.

Синхронізація: `python3 tools/sync_privacy_policy.py --site-root <website> --write`, потім без`--write` для перевірки. Після зміни тексту змінити policy revision та оцінити повторні згоди. Source-presence14секцій×3мови не доводить Google console settings.

Termux передає повний сайт у website main і окрему game гілку `privacy/revenue-privacy`; не пушитьmainгри, не створюєAPK й не змінюєDNS/пошту. Повна публікація сайту допустима з чесною позначкою кандидата. `npm run release:check` / `build:release` вимагають реальні evidence для фінального допуску; не обходити їх видаленням плашки або вигаданими хешами.
