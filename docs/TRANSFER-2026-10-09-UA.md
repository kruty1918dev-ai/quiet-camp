# Передача Quiet Camp на інший ноутбук · 09.10.2026

[README](../README.md) · [План продовження](CONTINUE-2026-10-09-UA.md) · [Карта продуктивності](../PERFORMANCE_MAP.md)

Пакет для завантаження: **[приватний GitHub release](https://github.com/kruty1918dev-ai/quiet-camp-transfer-2026-10-09/releases/tag/transfer-2026-10-09)**. Потрібно увійти в GitHub як власник `kruty1918dev-ai`. Основний файл — `QuietCamp_Transfer_2026-10-09.zip`; поруч контрольна сума, цей звіт і план. Це передача вихідних матеріалів для продовження роботи, а не реліз гри.

**Завантаження завершено й перевірено:** ZIP **1 061 470 268 bytes**, **11 861 payload hashes**, сім release assets у стані `uploaded`; серверні sizes/SHA-256 збігаються з локальними. [Receipt із source commits і session guards](transfer/2026-10-09/receipt.json). Original handoff ZIP також завантажений і має окрему контрольну суму.

## Що збережено

| Папка в основному ZIP | Вміст і стан |
| --- | --- |
| `current-project/` | Повний tracked snapshot гілки `perf/optimize-transitions-2026-10-09`: поточна гра, оптимізації, документація, native receipts і скриншоти. `SOURCE-STATES.json` фіксує точний commit/tree. |
| `prepared-roadmap/` | Повний tracked snapshot `art/roadmap-native-finish-2026-10-08`, commit `eb7ee6d1348bbac61dc6bc764265c0c6e610b1ee`: моделі, авторинг, композиційний pipeline, portable CLI та ліцензії. Новий catalog ще не активований. |
| `references/selected/` | Три обрані концепти з handoff: основна мапа, поле з літаком, дамба. Це концепти, не нові Unity captures. |
| `asset-packages/` | Сім унікальних непорожніх пакетів асетів із UAC ZIP. Вони лежать окремо від Unity Assets; автоматичного імпорту немає. |
| `reports/` | Звіт, план, каталог назв моделей із трьох unitypackage, вихідна handoff інструкція як історичний контекст. |
| `MANIFEST.json`, `verify_transfer.py` | SHA-256 кожного payload-файлу та перевірка після розпакування. |

Оригінальний `QuietCamp_Roadmap_Handoff_2026-10-08.zip` також збережено окремим asset у тому самому приватному release. ZIP-пакет перенесення не містить `.git`, Unity `Library/Temp/Logs`, player builds, даних гравця, ключів підпису чи облікових даних аудиту. Git-історію можна відновити з двох уже опублікованих гілок основного репозиторію.

## Фактичний результат роботи

Оптимізації вже є в `current-project`: GPU рух листя, кеші геометрії, підготовка світу частинами, bounded map buttons зі стабільними IDs, окремі CSS для панелей. Native evidence: **14 EditMode і 13 PlayMode regressions Passed**. Timing matrix на checkpoint `e8dd7a0`: leaf CPU p95 **17,34 → 0,03 мс**, найдовші route кадри **960 → 237 мс**, перше відкриття мапи **1377 → 303 мс**. Цілі ≤100 мс для переходів і стабільні 30/60 FPS ще не доведені; деталі й залишкові проблеми — у `current-project/PERFORMANCE_MAP.md`. Пізніші settings/campaign-order виправлення мають окремі regression receipts; timing matrix не видається за повторний замір цих змін.

У prepared roadmap є **24 original EnvironmentKit meshes / 4208 трикутників**, rural kit, редаговані OBJ/MTL, streams/bounds/LOD/source hashes. Ship donor має LOD **1100/650/300**; aircraft і ship містять attribution. Підготовлені густі forest zones першої десятки, садиби з власниками/дворами/огорожами, bus bay, пошкоджені conductors, terrain surfaces, окремі aircraft/ship/dam staging compositions. Попередні portable contracts пройшли, але це не native scene acceptance.

Під час цієї передачі знову пройшов static portability check поточної гілки: **46 direct dependencies**, без зовнішніх local UPM paths і case collisions. Три UAC unitypackage переглянуто потоково як tar-архіви: прочитано тільки `pathname`, без імпорту або виконання скриптів.

Злиття prepared roadmap із поточними оптимізаціями **не завершено**. Пробне злиття в окремому worktree виявило конфлікти UI/renderer/localization/tests; його скасовано, обидва вихідні стани збережено. Немає незавершеного merge. Main і активний Editor не переключались на неперевірену композицію. Нового bake, рендерів, player builds чи device тестів у цій передачі немає. Unity Editor і ADB не перезапускалися.

## Що корисне в UAC асетах

| Пакет у ZIP | Фактичний вміст архіву | Придатні кандидати для Quiet Camp |
| --- | --- | --- |
| `POLYGON_Meadow_Forest_v1.02.unitypackage` | 226 FBX entries, 190 prefabs; counts включають collision/LOD variants | Берези, змішані дерева, каміння, купки каменів, бур'яни, пошкоджувані паркани/ворота. Найперспективніший набір для forest density та узлісь. |
| `POLYGON_Swamp_Marshland_v1.02.unitypackage` | 194 FBX entries, 152 prefabs, також collision/LOD | Очерет, коріння, повалені колоди, прибережна трава, jetty/planks. Доречні локально біля реальної води й дамби. Мангрові дерева та horror-декор не відповідають цілі українського ландшафту. |
| `POLYGON_City_v1.9.unitypackage` | 330 FBX entries, 344 prefabs | Bus stop, пошкоджуване дорожнє покриття, паркани, дорожні деталі. Готові cable meshes потребують socket/scale перевірки; власний typed conductor pipeline має залишитися джерелом зв'язків. |
| `POLYGON_Battle_Royale_v1.05.rar` | Перенесено як непрозорий пакет; внутрішню геометрію не перевірено | Лише можливі цивільні utility props після окремого огляду. Зброя, військові props і повні battle scenes не потрібні. |
| `POLYGON_Particle_FX.rar` | Перенесено без виконання/імпорту | Можливі локальні dust/water effects; наявні leaf transition і атмосфера вже мають власну реалізацію. Спершу виміряти overdraw. |
| `Abilities_Game_Creator_2_v1.9.0.7z` | Один примірник, дублікат вилучено | Це система gameplay, не набір моделей; не додавати заради візуалу. |
| `DesignOptimal.com - Unity Asset - POLYGON - Battle Royale Pack v1.04.zip` | Старіша альтернативна версія, збережена окремо | Порівняти лише за потреби; не імпортувати обидві версії в один проєкт. |

Нульові Nature/City placeholder-файли, сторонній uninstaller, audit scripts, JWT, cookies, account lists та інші службові матеріали UAC не перенесені. Зберігання пакетів у приватному backup **не підтверджує ліцензію на використання в грі**. До інтеграції потрібні законне отримання й права на потрібний сценарій. Synty забороняє передачу source files поза командою; Unity Asset Store має власні умови. [Synty licence](https://syntystore.com/pages/one-time-purchase-licence), [Unity Asset Store terms](https://unity.com/legal/as-terms).

Каталоги й актуальні офіційні versions: [Meadow Forest](https://syntystore.com/products/polygon-meadow-forest-nature-biome), [Swamp Marshland](https://syntystore.com/products/polygon-swamp-marshland-nature-biome). Наші старі пакетні версії ще не пройшли Unity 6000.6/URP 17.6 import. Рекомендації щодо форми — попередній висновок із назв моделей і обраних концептів; трикутники, матеріали та native appearance цих донорів ще не виміряно.

## Перевірка стилю перед включенням

Власний kit — основний шлях: muted sage/olive palette, flat normals, чіткі faceted silhouettes, однаковий масштаб і pivot, LOD. Оцінювати об'єкт із штатної orthographic camera pitch 55°, а не тільки зблизька в Inspector. Рослини ставити відповідно до географії; свіжий урожай прибрати. Садиби, ворота й проводи перевіряти як ансамблі. Candidate donor приймається лише після side-by-side native capture, grounding/footprint/socket tests і повторного map/transition benchmark. Суцільний імпорт великих demo scenes не є інтеграцією стилю.

На поточному host лишалося близько **6 ГБ** вільного диска, нижче правила >10%. За новим запитом користувача важкий native bake відкладено до іншого ноутбука; підготовлено перенесення матеріалів.
