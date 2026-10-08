[← Головна](../README.md) · [Довідник](README.md) · [Екрани](screens.md) · [Галерея](gallery.md) · [Розробка](development.md) · [Стан](status.md)

# Стан і походження доказів

![Справжній літній табір у тихому режимі альбому](images/captures/2026-10-08/album-season-summer.png)

Ця сторінка фіксує перевірений зріз **2026-10-08**. У repository є development project, source content і дизайн-плани; це не оголошення готового store release.

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

## Що потребує окремої перевірки

- Garden journey preview має порожню illustration area у захопленій конфігурації; текстові умови й підтвердження видимі.
- Wide gameplay HUD має порожню картку та невдалий верхній край кадру; для presentation gallery використано штатний тихий режим альбому. Наявність PNG guards не означає відсутність UI/layout issues.
- 110 ordered main places, 15 districts та journey counts — committed source catalog. Під час цього оновлення не виконувалася повна solver/visual/device acceptance усіх 260 frozen files.
- Нова semantic roadmap composition залишається в [art branch](https://github.com/kruty1918dev-ai/quiet-camp/tree/art/roadmap-native-finish-2026-10-08), не merged/activated новим native bake. Нові кадри main не є доказом цієї композиції.
- Реальні purchases, restore, ads, backend entitlement/wallet security і phone FPS не перевірялися. Default store/ad integrations не підключені.
- Player builds/APK/AAB, installation та публікація гри в store не виконувалися. [Workspace rules](../AGENTS.md).

Архівний seasonal baseline датований **2026-10-05**. Showcase run **2026-10-06** failed; старі картинки не називаються його новими успішними рендерами. [Архів і концепти](gallery.md#архів-та-концепти).
