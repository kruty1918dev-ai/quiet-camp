[← Головна](../README.md) · [Довідник](README.md) · [Екрани](screens.md) · [Галерея](gallery.md) · [Розробка](development.md) · [Стан](status.md)

# Стан і походження доказів

![Справжній літній табір у тихому режимі альбому](images/captures/2026-10-08/album-season-summer.png)

Довідник фіксує перевірений visual зріз **2026-10-08**; native performance audit додано **2026-10-09**. У repository є development project, source content і дизайн-плани; це не оголошення готового store release.

## Що показує новий довідник

**44 нові native PNG**: Boot, меню/мапа/journeys/bonus, gameplay, pause/знаки/налаштування, completion, album, усі основні settings/privacy categories та contextual confirmations. 36 portrait screens/states мають окремі картки на [GitHub Pages](https://kruty1918dev-ai.github.io/quiet-camp/); чотири сезонні альбомні кадри та чотири широкі gameplay кадри — у [галереї](gallery.md).

| Перевірка | Результат / межі |
| --- | --- |
| Повний capture flow | **Passed**, один PlayMode test, 2026-10-08 21:20:05–21:22:11 UTC. |
| Context panels + seasonal AlbumQuiet | **Passed**, окремий етап із synthetic wallet/progress/album entries. |
| Camp/album/seasonal gameplay phase | **Passed**, окремий етап; broad gameplay framing має помітні обмеження. |
| PNG guards | Expected resolution, magenta менше 2%, nonempty pixels більше 10%; це не повна visual acceptance. |
| Storage/settings guards | Primary/backup game save SHA-256 збережено; ProjectSettings відновлено byte-for-byte; початкові dirty inputs збережено. |
| ADB/session guards | Той самий ADB process identity та незмінні global ADB preferences; новий Editor не запускався. |
| Guide/site verification | **Passed**: local links/anchors, image hashes/sidecars, 36 content IDs і UA/EN/DE coverage; [checker](../tools/verify_github_docs.py). |
| Browser UI | **Passed**: image loading, 3 languages/persistence, Unicode search/filters, viewer/ESC, 320–1920 px без horizontal overflow та no-JS fallback; [local receipt](verification/site-local-2026-10-08.json). |
| Live GitHub Pages | **Published / Passed**, HTTP 200, HTML збігається з локальними bytes; усі browser scenarios повторено на [живому сайті](https://kruty1918dev-ai.github.io/quiet-camp/). [Deployed receipt](verification/site-deployed-2026-10-08.json). |
| GitHub Markdown | README та screens atlas успішно rendered через GitHub GFM API; 9/36 inline images та 4/10 tables. |

[Machine-readable capture receipt](images/captures/2026-10-08/verification.json) · [44-image manifest](images/captures/2026-10-08/manifest.json) · [Fixture/workflow](../tools/qa/DOCS-CAPTURE-UA.md).

## Середовище знімків

Unity **6000.6.2f1**, desktop OpenGL, High quality. Portrait Game View 720 × 1600, seasonal tablet/album 1280 × 800. Renderer — main runtime source checkpoint [`e2373e1`](https://github.com/kruty1918dev-ai/quiet-camp/commit/e2373e1fbe93038671d900bd9522014a585272ff); capture fixture розвивався окремо й має власний SHA-256 у sidecars. У відкритій локальній сесії були незакомічені `level_summaries.json` та URP settings; їх не включено в docs-публікацію, їхні реальні hashes збережено в metadata. Кадри фіксують цю локальну конфігурацію, а не доводять відтворення clean clone на кожній ОС.

QA-профіль має окрему product/save identity. Прогрес, wallet та сезонні album snapshots підготовлені fixture; це не player achievements, реальна покупка або rewarded ad. Альбом відтворює справжні solved placements через native runtime, PNG не ретушували.

Ранні capture attempts завершувалися помилками fixture; їх не зараховано як Passed. Cleanup спочатку відновив product identity зарано, і фінальний QA write потрапив у основний save slot. Його відновлено з копії, що точно збігається з початковим SHA-256, до повторних успішних етапів. Driver тепер чекає `RunFinished`, повного виходу з Play Mode й cleanup delay. Контракт hashes охоплює game slots; Unity internal Editor preferences/session data не заявляються незмінними.

## Native performance audit · 2026-10-09

[Повна карта проблем і пріоритетів](../PERFORMANCE_MAP.md) · [інтерактивні сценарії/timeline](performance.html) · [workflow](../tools/qa/PERFORMANCE-AUDIT-UA.md).

| Перевірка | Результат / межі |
| --- | --- |
| Повна performance матриця | **Passed**, 309.46 с; 6 140 кадрів / 34 576 method scopes / 35 operations. Три tiers, leaf-only, repeated normal/reduced routes, п'ять sampled camps, мапа/journeys/settings/album. [Receipt](performance/2026-10-09/native-result.json). |
| Прицільний замір мапи | **Passed**, 45.99 с; 640 кадрів / 98 964 scopes; stationary/full sweep/5% scroll. [Receipt](performance/2026-10-09/roadmap/native-result.json). |
| Foliage regression | **5/5 Passed**: visible mesh, sound scope recovery, cancellation, reduced motion, reveal/input recovery. [Receipt](performance/2026-10-09/foliage-tests.json). |
| Current-source compilation | **Passed**, runtime, Editor та PlayMode sources через cached Bee references; це не player build. [Receipt](performance/2026-10-09/source-compilation.json). |
| Storage/session guards | ProjectSettings byte-for-byte, original save hashes та початкові dirty inputs збережено. Той самий ADB identity й незмінні shared kill preferences; Editor повторно не запускався. |
| Report/data/site checks | **Passed**: raw/source hashes, percentiles, 3 route repetitions per mode/direction, 754 guide/site links; [interactive browser checks](verification/performance-site-local-2026-10-09.json), search/datasets/timeline/no-JS і 320–1440 px. Main guide UA/EN/DE та 36 cards повторно Passed після nav update. |
| Performance budgets | **Не пройдені**: leaf p95 до 17.34 мс; повторні route frames до 960 мс; map open 1.27–1.38 с; slower scroll p95 151.63 мс. Оптимізації відкриті. |

Unity 6000.6.2f1, 720 × 1600, i7-2600/GT 730/OpenGL, Jobs worker 1. Це Editor wall/method/counter data, **не device FPS**. GPU timing, Render Thread і method-local allocations недоступні; zeros не означають zero cost. Global memory/GC включають Editor, QA та audit overhead; leak не доведено. Основний runtime checkpoint `c9c7bf5`, opt-in audit source/data `235804a`, додаткова мапа/regressions `ca3476b`; точні source/fixture hashes є у raw provenance.

Перший matrix attempt мав стандартний 180-секундний timeout; published numbers походять із завершеного повторного run. Перший запит regression після заміни driver прийняла стара assembly й performance test був Skipped через QA guard; цей запит не зараховано як Passed. Після завершення import окремо виконано та перевірено правильні п'ять foliage tests. Нових screenshots ця діагностика не заявляє.

## Що потребує окремої перевірки

- Garden journey preview має порожню illustration area у захопленій конфігурації; текстові умови й підтвердження видимі.
- Wide gameplay HUD має порожню картку та невдалий верхній край кадру; для presentation gallery використано штатний тихий режим альбому. Наявність PNG guards не означає відсутність UI/layout issues.
- 110 ordered main places, 15 districts та journey counts — committed source catalog. Під час цього оновлення не виконувалася повна solver/visual/device acceptance усіх 260 frozen files.
- Нова semantic roadmap composition залишається в [art branch](https://github.com/kruty1918dev-ai/quiet-camp/tree/art/roadmap-native-finish-2026-10-08), не merged/activated новим native bake. Нові кадри main не є доказом цієї композиції.
- Реальні purchases, restore, ads, backend entitlement/wallet security і phone FPS не перевірялися. Default store/ad integrations не підключені.
- Player builds/APK/AAB, installation та публікація гри в store не виконувалися. [Workspace rules](../AGENTS.md).

Архівний seasonal baseline датований **2026-10-05**. Showcase run **2026-10-06** failed; старі картинки не називаються його новими успішними рендерами. [Архів і концепти](gallery.md#архів-та-концепти).
