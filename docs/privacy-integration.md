# Політика Quiet Camp і офіційний сайт

Канонічна адреса: https://oleksiy-havryliuk.de/quiet-camp/privacy, українська /uk, німецька /de. Офіційний сайт — окремий репозиторій kruty1918dev-ai/portfolio-oleksii-ha; GitHub Pages у цьому репозиторії є ілюстрованим посібником і посилається на офіційну політику. Custom domain не переноситься на Pages.

Поточна реалізація гри об’єднана в main. Firebase Core/Analytics/Crashlytics 14.0.0 та AdMob 11.6.0 встановлені; віковий вибір і три незалежні дозволи реалізовані. Unknown/<16 блокує необов’язкові SDK-запити; допустима реклама неперсоналізована, з teen treatment для 16–17. Production gates залишаються закритими. Ревізія вбудованої політики — 2026-10-21.privacy-4.

Повний статичний текст сайту готується з legal/quiet-camp/content.json через scripts/render-quiet-camp-policy.py у репозиторії сайту. Вбудований документ синхронізується командою python tools/sync_privacy_policy.py --site-root ../portfolio-oleksii-ha --write; без --write команда лише перевіряє відповідність. Ця синхронізація не публікує сайт і не активує production SDK.

[Поточні перевірки, owner checklist і Android-блокери](../Design/Legal/PRIVACY-RUNTIME-UA.md). [Облік даних і Data safety](../Design/Legal/DATA-SAFETY-UA.md). Попередній прототип описаний у Design/Privacy/2026-10-20/IMPLEMENTATION.md як історична версія; актуальні probes — tools/privacy-probe та tools/optional-google-probe.

Об’єднання гілок гри не об’єднує й не публікує окремий репозиторій сайту. Реальні консолі, native handlers/файли/трафік, merged manifest і resolved dependencies потребують окремих доказів. Публічна політика або прапорці JSON не замінюють перевірку Android-релізу.
